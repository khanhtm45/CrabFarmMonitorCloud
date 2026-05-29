#!/usr/bin/env python3
"""v3: one telemetry.h5 per farm, hourly groups under /chunks/YYYYMMDD_HH/."""
from __future__ import annotations

import json
import os
import sys
from datetime import datetime, timezone

import h5py
import numpy as np

SCHEMA_VERSION = 3


def _hdf5_dir() -> str:
    return os.environ.get("HDF5_DIR", "/data/hdf5")


def _farm_id() -> str:
    fid = (os.environ.get("FARM_ID") or os.environ.get("DEFAULT_FARM_ID") or "").strip()
    if not fid:
        raise ValueError("FARM_ID required for HDF5 v3")
    return fid.replace("-", "").lower()


def _archive_path() -> str:
    root = os.path.join(_hdf5_dir(), _farm_id())
    os.makedirs(root, exist_ok=True)
    return os.path.join(root, "telemetry.h5")


def _chunk_id(now: datetime) -> str:
    return now.strftime("%Y%m%d_%H")


def _chunk_group(f: h5py.File, chunk_id: str) -> h5py.Group:
    base = f.require_group("chunks")
    return base.require_group(chunk_id)


def _ensure_chunk_datasets(grp: h5py.Group) -> None:
    for name, dtype in (("timestamp_ms", "i8"), ("pin", "i4"), ("val", "f4")):
        if name not in grp:
            grp.create_dataset(
                name,
                shape=(0,),
                maxshape=(None,),
                dtype=dtype,
                compression="gzip",
                chunks=True,
            )


def _row_count(grp: h5py.Group) -> int:
    return int(grp["pin"].shape[0]) if "pin" in grp else 0


def append(mac: str, readings: list) -> dict:
    mac = mac.upper()
    pins: list[int] = []
    vals: list[float] = []
    for item in readings:
        if not isinstance(item, dict):
            continue
        pin = item.get("pin")
        val = item.get("val")
        if pin is None or val is None:
            continue
        pins.append(int(pin))
        vals.append(float(val))
    if not pins:
        return {"ok": True, "appended": 0}

    now = datetime.now(timezone.utc)
    chunk_id = _chunk_id(now)
    path = _archive_path()
    ts_ms = int(now.timestamp() * 1000)
    n = len(pins)

    with h5py.File(path, "a") as f:
        f.attrs["schema_version"] = SCHEMA_VERSION
        f.attrs["farm_id"] = _farm_id()
        grp = _chunk_group(f, chunk_id)
        grp.attrs["mac"] = mac
        grp.attrs["chunk_id"] = chunk_id
        grp.attrs["sealed"] = False
        _ensure_chunk_datasets(grp)
        for name, arr in (
            ("timestamp_ms", np.full(n, ts_ms, dtype=np.int64)),
            ("pin", np.array(pins, dtype=np.int32)),
            ("val", np.array(vals, dtype=np.float32)),
        ):
            ds = grp[name]
            old = ds.shape[0]
            ds.resize(old + n, axis=0)
            ds[old : old + n] = arr
        grp.attrs["row_count"] = _row_count(grp)

    return {
        "ok": True,
        "appended": n,
        "path": path,
        "farm_id": _farm_id(),
        "chunk_id": chunk_id,
        "row_count": n,
        "schema_version": SCHEMA_VERSION,
    }


def main() -> int:
    data = json.load(sys.stdin)
    mac = data.get("mac")
    if not mac:
        print(json.dumps({"ok": False, "error": "mac required"}))
        return 1
    try:
        result = append(str(mac), data.get("readings") or [])
    except Exception as ex:
        print(json.dumps({"ok": False, "error": str(ex)}))
        return 1
    print(json.dumps(result))
    return 0


if __name__ == "__main__":
    sys.exit(main())
