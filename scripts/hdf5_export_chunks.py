#!/usr/bin/env python3
"""Export sealed hourly chunks from farm telemetry.h5 to sync_queue/*.h5 for Cloud upload."""
from __future__ import annotations

import json
import os
import sys
from datetime import datetime, timezone

import h5py


def _hdf5_dir() -> str:
    return os.environ.get("HDF5_DIR", "/data/hdf5")


def _farm_id() -> str:
    fid = (os.environ.get("FARM_ID") or os.environ.get("DEFAULT_FARM_ID") or "").strip()
    if not fid:
        raise ValueError("FARM_ID required")
    return fid.replace("-", "").lower()


def _is_sealed(chunk_id: str, now: datetime) -> bool:
    try:
        day_s, hour_s = chunk_id.split("_", 1)
        day = datetime.strptime(day_s, "%Y%m%d").replace(tzinfo=timezone.utc)
        hour = int(hour_s)
    except (ValueError, IndexError):
        return False
    if day.date() < now.date():
        return True
    return hour < now.hour


def _chunk_bounds_ms(chunk_id: str) -> tuple[int, int]:
    from datetime import timedelta

    day_s, hour_s = chunk_id.split("_", 1)
    day = datetime.strptime(day_s, "%Y%m%d").replace(tzinfo=timezone.utc)
    hour = int(hour_s)
    start_dt = day.replace(hour=hour, minute=0, second=0, microsecond=0)
    end_dt = start_dt + timedelta(hours=1) - timedelta(milliseconds=1)
    return int(start_dt.timestamp() * 1000), int(end_dt.timestamp() * 1000)


def _copy_chunk_to_file(src: h5py.Group, dest_path: str, chunk_id: str) -> int:
    with h5py.File(dest_path, "w") as out:
        out.attrs["schema_version"] = 3
        out.attrs["chunk_id"] = chunk_id
        out.attrs["mac"] = str(src.attrs.get("mac", ""))
        out.attrs["exported_from"] = "farm_archive"
        for name in ("timestamp_ms", "pin", "val"):
            if name in src:
                data = src[name][:]
                out.create_dataset(name, data=data, compression="gzip")
        return int(out["pin"].shape[0]) if "pin" in out else 0


def export() -> dict:
    farm = _farm_id()
    archive = os.path.join(_hdf5_dir(), farm, "telemetry.h5")
    if not os.path.isfile(archive):
        return {"ok": True, "exported": [], "message": "no archive"}

    sync_dir = os.path.join(_hdf5_dir(), farm, "sync_queue")
    os.makedirs(sync_dir, exist_ok=True)
    now = datetime.now(timezone.utc)
    exported = []

    with h5py.File(archive, "a") as f:
        if "chunks" not in f:
            return {"ok": True, "exported": []}
        for chunk_id in sorted(list(f["chunks"].keys())):
            grp = f[f"chunks/{chunk_id}"]
            rows = int(grp["pin"].shape[0]) if "pin" in grp else 0
            if rows == 0:
                continue
            if not _is_sealed(chunk_id, now):
                continue
            if bool(grp.attrs.get("cloud_synced")):
                continue

            out_name = f"telemetry_{chunk_id}.h5"
            out_path = os.path.join(sync_dir, out_name)
            n = _copy_chunk_to_file(grp, out_path, chunk_id)
            cs, ce = _chunk_bounds_ms(chunk_id)
            grp.attrs["sealed"] = True
            exported.append(
                {
                    "file": out_name,
                    "path": out_path,
                    "chunk_id": chunk_id,
                    "rows": n,
                    "chunk_start_ms": cs,
                    "chunk_end_ms": ce,
                    "mac": str(grp.attrs.get("mac", "")),
                }
            )

    return {"ok": True, "farm_id": farm, "exported": exported}


def mark_synced(req: dict) -> dict:
    """After successful upload: chunk_id in req."""
    farm = _farm_id()
    archive = os.path.join(_hdf5_dir(), farm, "telemetry.h5")
    chunk_id = req.get("chunk_id")
    if not chunk_id or not os.path.isfile(archive):
        return {"ok": False, "error": "missing chunk_id or archive"}
    with h5py.File(archive, "a") as f:
        if "chunks" in f and chunk_id in f["chunks"]:
            f[f"chunks/{chunk_id}"].attrs["cloud_synced"] = True
    sync_file = os.path.join(_hdf5_dir(), farm, "sync_queue", f"telemetry_{chunk_id}.h5")
    if os.path.isfile(sync_file):
        os.remove(sync_file)
    return {"ok": True, "chunk_id": chunk_id}


def main() -> int:
    req = {}
    if not sys.stdin.isatty():
        try:
            req = json.load(sys.stdin)
        except json.JSONDecodeError:
            req = {}
    action = req.get("action", "export")
    try:
        if action == "mark_synced":
            result = mark_synced(req)
        else:
            result = export()
    except Exception as ex:
        print(json.dumps({"ok": False, "error": str(ex)}))
        return 1
    print(json.dumps(result))
    return 0


if __name__ == "__main__":
    sys.exit(main())
