#!/usr/bin/env python3
"""
Replay telemetry từ HDF5 v3 (telemetry.h5 hoặc sync_queue/*.h5) lên Cloud API.

Dùng khi không có ESP: team FE vẫn thấy realtime + chart (telemetry_samples)
và có thể seed Archive (POST /api/sync/hdf5).

Env / args:
  CLOUD_URL          http://168.144.38.133:8084 hoặc http://ras-api.duckdns.org
  TELEMETRY_API_KEY  (bắt buộc nếu VPS bật key)
  FARM_ID            11111111-1111-1111-1111-111111111111
  HDF5_PATH          file .h5 hoặc thư mục farm (có telemetry.h5)
  CHUNK_ID           optional, vd 20260520_14
  INTERVAL_SEC       2.0 giữa các gói
  MAX_PACKETS        0 = all (sau subsample)
  SUBSAMPLE_EVERY    1 = giữ mọi gói; 5 = mỗi 5 gói lấy 1
  LOOP               1 = lặp vô hạn
  UPLOAD_CHUNKS      1 = upload file chunk lên /api/sync/hdf5
"""
from __future__ import annotations

import argparse
import json
import os
import sys
import time
import urllib.error
import urllib.request
from collections import defaultdict
from pathlib import Path

try:
    import h5py
    import numpy as np
except ImportError:
    print("Cần: pip install h5py numpy", file=sys.stderr)
    sys.exit(1)


def _chunk_bounds_ms(chunk_id: str) -> tuple[int, int]:
    from datetime import datetime, timedelta, timezone

    day_s, hour_s = chunk_id.split("_", 1)
    day = datetime.strptime(day_s, "%Y%m%d").replace(tzinfo=timezone.utc)
    hour = int(hour_s)
    start_dt = day.replace(hour=hour, minute=0, second=0, microsecond=0)
    end_dt = start_dt + timedelta(hours=1) - timedelta(milliseconds=1)
    return int(start_dt.timestamp() * 1000), int(end_dt.timestamp() * 1000)


def _resolve_archive(path: str, farm_id: str) -> Path:
    p = Path(path)
    if p.is_file():
        return p
    if p.is_dir():
        fid = farm_id.replace("-", "").lower()
        cand = p / fid / "telemetry.h5"
        if cand.is_file():
            return cand
        cand2 = p / "telemetry.h5"
        if cand2.is_file():
            return cand2
    raise FileNotFoundError(f"Không tìm thấy telemetry.h5 trong {path}")


def _mac_from_group(grp: h5py.Group, fallback: str) -> str:
    m = str(grp.attrs.get("mac", fallback or "")).strip()
    return m or fallback


def _packets_from_group(grp: h5py.Group, max_rows: int = 0) -> list[dict]:
    n = int(grp["pin"].shape[0]) if "pin" in grp else 0
    if n == 0:
        return []
    cap = max_rows or int(os.environ.get("REPLAY_MAX_ROWS", "12000"))
    if cap > 0 and n > cap:
        start = n - cap
        sl = slice(start, n)
    else:
        sl = slice(None)
    ts = np.asarray(grp["timestamp_ms"][sl], dtype=np.int64)
    pins = np.asarray(grp["pin"][sl], dtype=np.int32)
    vals = np.asarray(grp["val"][sl], dtype=np.float32)
    n = ts.shape[0]
    by_ts: dict[int, list[dict]] = defaultdict(list)
    for i in range(n):
        t = int(ts[i])
        by_ts[t].append({"pin": int(pins[i]), "val": float(vals[i])})
    out = []
    for t in sorted(by_ts.keys()):
        out.append({"timestamp_ms": t, "readings": by_ts[t]})
    return out


def load_packets(archive: Path, chunk_id: str | None, mac_default: str) -> tuple[str, list[dict]]:
    packets: list[dict] = []
    mac = mac_default
    with h5py.File(archive, "r") as f:
        if "chunks" in f:
            if chunk_id:
                names = [chunk_id]
            else:
                # Mặc định chỉ chunk mới nhất — tránh load 40MB+ toàn bộ archive
                latest_only = os.environ.get("REPLAY_LATEST_CHUNK_ONLY", "1").lower() not in (
                    "0",
                    "false",
                    "no",
                )
                all_names = sorted(f["chunks"].keys())
                names = [all_names[-1]] if latest_only and all_names else all_names
            print(f"Loading chunks: {', '.join(names)} …", flush=True)
            for cid in names:
                if cid not in f["chunks"]:
                    continue
                grp = f[f"chunks/{cid}"]
                mac = _mac_from_group(grp, mac)
                packets.extend(_packets_from_group(grp))
            print(f"  chunk {cid}: {len(packets)} packets so far", flush=True)
        else:
            mac = _mac_from_group(f, mac)
            packets = _packets_from_group(f)
    packets.sort(key=lambda x: x["timestamp_ms"])
    return mac, packets


def _export_chunk_file(archive: Path, chunk_id: str, out_dir: Path) -> Path | None:
    out_dir.mkdir(parents=True, exist_ok=True)
    out_path = out_dir / f"telemetry_{chunk_id}.h5"
    with h5py.File(archive, "r") as src:
        if "chunks" not in src or chunk_id not in src["chunks"]:
            return None
        grp = src[f"chunks/{chunk_id}"]
        with h5py.File(out_path, "w") as out:
            out.attrs["schema_version"] = 3
            out.attrs["chunk_id"] = chunk_id
            out.attrs["mac"] = str(grp.attrs.get("mac", ""))
            out.attrs["exported_from"] = "replay_script"
            for name in ("timestamp_ms", "pin", "val"):
                if name in grp:
                    out.create_dataset(name, data=grp[name][:], compression="gzip")
    return out_path


def _http_json(
    url: str,
    body: dict,
    api_key: str,
    farm_id: str,
    method: str = "POST",
) -> tuple[int, str]:
    data = json.dumps(body).encode("utf-8")
    req = urllib.request.Request(url, data=data, method=method)
    req.add_header("Content-Type", "application/json")
    if api_key:
        req.add_header("X-API-Key", api_key)
    if farm_id:
        req.add_header("X-Farm-Id", farm_id)
    try:
        with urllib.request.urlopen(req, timeout=30) as resp:
            return resp.status, resp.read().decode("utf-8", errors="replace")
    except urllib.error.HTTPError as e:
        return e.code, e.read().decode("utf-8", errors="replace")


def _http_multipart_hdf5(
    url: str,
    file_path: Path,
    mac: str,
    farm_id: str,
    api_key: str,
    chunk_start_ms: int,
    chunk_end_ms: int,
    idempotency_key: str,
) -> tuple[int, str]:
    boundary = f"----RasReplay{int(time.time() * 1000)}"
    file_bytes = file_path.read_bytes()
    parts: list[bytes] = []

    def add_field(name: str, value: str) -> None:
        parts.append(
            f"--{boundary}\r\nContent-Disposition: form-data; name=\"{name}\"\r\n\r\n{value}\r\n".encode()
        )

    add_field("mac", mac)
    add_field("chunk_start_ms", str(chunk_start_ms))
    add_field("chunk_end_ms", str(chunk_end_ms))
    add_field("idempotency_key", idempotency_key)
    parts.append(
        (
            f"--{boundary}\r\n"
            f"Content-Disposition: form-data; name=\"file\"; filename=\"{file_path.name}\"\r\n"
            f"Content-Type: application/x-hdf5\r\n\r\n"
        ).encode()
    )
    parts.append(file_bytes)
    parts.append(f"\r\n--{boundary}--\r\n".encode())
    body = b"".join(parts)

    req = urllib.request.Request(url, data=body, method="POST")
    req.add_header("Content-Type", f"multipart/form-data; boundary={boundary}")
    if api_key:
        req.add_header("X-API-Key", api_key)
    if farm_id:
        req.add_header("X-Farm-Id", farm_id)
    try:
        with urllib.request.urlopen(req, timeout=120) as resp:
            return resp.status, resp.read().decode("utf-8", errors="replace")
    except urllib.error.HTTPError as e:
        return e.code, e.read().decode("utf-8", errors="replace")


def replay_telemetry(
    cloud_base: str,
    api_key: str,
    farm_id: str,
    mac: str,
    packets: list[dict],
    interval_sec: float,
    subsample_every: int,
    max_packets: int,
    loop: bool,
) -> None:
    url = cloud_base.rstrip("/") + "/api/telemetry"
    if subsample_every > 1:
        packets = packets[::subsample_every]
    if max_packets > 0:
        packets = packets[-max_packets:]

    print(f"Replay {len(packets)} packets -> {url} (mac={mac}, interval={interval_sec}s)")
    n_sent = 0
    while True:
        for i, pkt in enumerate(packets):
            body = {"mac": mac, "readings": pkt["readings"]}
            try:
                code, resp = _http_json(url, body, api_key, farm_id)
            except Exception as ex:
                print(f"  [{i}] network error: {ex} — retry in 5s", flush=True)
                time.sleep(5)
                continue
            if code not in (200, 201):
                print(f"  [{i}] HTTP {code}: {resp[:200]}", file=sys.stderr, flush=True)
            else:
                n_sent += 1
                if n_sent % 20 == 0:
                    print(f"  sent {n_sent} …", flush=True)
            time.sleep(interval_sec)
        if not loop:
            break
        print("Loop — replay from start")


def upload_chunks(
    cloud_base: str,
    api_key: str,
    farm_id: str,
    mac: str,
    archive: Path,
    chunk_ids: list[str],
) -> None:
    url = cloud_base.rstrip("/") + "/api/sync/hdf5"
    tmp = Path(os.environ.get("TEMP", "/tmp")) / "ras-replay-export"
    for cid in chunk_ids:
        out = _export_chunk_file(archive, cid, tmp)
        if not out or not out.is_file():
            print(f"Skip chunk {cid} (empty)", file=sys.stderr)
            continue
        cs, ce = _chunk_bounds_ms(cid)
        key = f"replay-{cid}-{farm_id}"
        code, resp = _http_multipart_hdf5(url, out, mac, farm_id, api_key, cs, ce, key)
        print(f"Upload {out.name}: HTTP {code} {resp[:120]}")


def main() -> int:
    ap = argparse.ArgumentParser(description="Replay HDF5 → Cloud cho FE dev")
    ap.add_argument("--cloud-url", default=os.environ.get("CLOUD_URL", "http://ras-api.duckdns.org"))
    ap.add_argument("--api-key", default=os.environ.get("TELEMETRY_API_KEY") or os.environ.get("EDGE_REMOTE_SYNC_API_KEY", ""))
    ap.add_argument("--farm-id", default=os.environ.get("FARM_ID") or os.environ.get("DEFAULT_FARM_ID", ""))
    ap.add_argument("--hdf5", default=os.environ.get("HDF5_PATH", ""), help="telemetry.h5 hoặc thư mục hdf5")
    ap.add_argument("--mac", default=os.environ.get("DEVICE_MAC", "68:FE:71:16:A5:18"))
    ap.add_argument("--chunk", default=os.environ.get("CHUNK_ID", ""))
    ap.add_argument("--interval", type=float, default=float(os.environ.get("INTERVAL_SEC", "2")))
    ap.add_argument("--subsample", type=int, default=int(os.environ.get("SUBSAMPLE_EVERY", "1")))
    ap.add_argument("--max-packets", type=int, default=int(os.environ.get("MAX_PACKETS", "900")))
    ap.add_argument("--loop", action="store_true", default=os.environ.get("LOOP", "").lower() in ("1", "true", "yes"))
    ap.add_argument("--upload-chunks", action="store_true", default=os.environ.get("UPLOAD_CHUNKS", "").lower() in ("1", "true", "yes"))
    ap.add_argument("--upload-only", action="store_true", help="Chỉ upload archive HDF5, không stream realtime")
    args = ap.parse_args()

    if not args.hdf5:
        print("Cần --hdf5 hoặc HDF5_PATH", file=sys.stderr)
        return 1
    if not args.farm_id:
        print("Cần --farm-id hoặc FARM_ID", file=sys.stderr)
        return 1

    archive = _resolve_archive(args.hdf5, args.farm_id)
    chunk_id = args.chunk.strip() or None
    mac, packets = load_packets(archive, chunk_id, args.mac)
    if not mac:
        mac = args.mac
    if not packets:
        print("Không có gói trong HDF5", file=sys.stderr)
        return 1

    print(f"Archive: {archive} | mac={mac} | packets={len(packets)}", flush=True)

    if args.upload_chunks:
        with h5py.File(archive, "r") as f:
            cids = [chunk_id] if chunk_id else sorted(f.get("chunks", {}).keys())
        upload_chunks(args.cloud_url, args.api_key, args.farm_id, mac, archive, list(cids))

    if not args.upload_only:
        replay_telemetry(
            args.cloud_url,
            args.api_key,
            args.farm_id,
            mac,
            packets,
            args.interval,
            args.subsample,
            args.max_packets,
            args.loop,
        )
    return 0


if __name__ == "__main__":
    sys.exit(main())
