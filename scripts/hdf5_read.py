#!/usr/bin/env python3
"""Read v3 chunk inside telemetry.h5 or legacy flat file."""
from __future__ import annotations

import json
import os
import sys

import h5py
import numpy as np


def _hdf5_dir() -> str:
    return os.environ.get("HDF5_DIR", "/data/hdf5")


def _resolve_path(req: dict) -> tuple[str, str | None]:
    file_req = req.get("file") or "telemetry.h5"
    chunk = req.get("chunk")
    farm_id = (req.get("farm_id") or os.environ.get("FARM_ID") or "").replace("-", "").lower()

    if os.path.isabs(file_req) and os.path.isfile(file_req):
        return file_req, chunk

    d = _hdf5_dir()
    if file_req == "telemetry.h5" and farm_id:
        return os.path.join(d, farm_id, "telemetry.h5"), chunk

    path = os.path.join(d, file_req)
    if os.path.isfile(path):
        return path, chunk

    if farm_id:
        alt = os.path.join(d, farm_id, file_req)
        if os.path.isfile(alt):
            return alt, chunk
    return path, chunk


def _read_chunk(grp: h5py.Group, limit: int, pin, from_ms, to_ms) -> dict:
    n = int(grp["pin"].shape[0]) if "pin" in grp else 0
    if n == 0:
        return {"rows": [], "total": 0, "total_matched": 0, "returned": 0}

    ts = np.asarray(grp["timestamp_ms"][:], dtype=np.int64)
    pins = np.asarray(grp["pin"][:], dtype=np.int32)
    vals = np.asarray(grp["val"][:], dtype=np.float64)

    mask = np.ones(n, dtype=bool)
    if pin is not None:
        mask &= pins == int(pin)
    if from_ms is not None:
        mask &= ts >= int(from_ms)
    if to_ms is not None:
        mask &= ts <= int(to_ms)

    indices = np.nonzero(mask)[0]
    total_matched = int(indices.shape[0])
    if total_matched > limit:
        indices = indices[-limit:]

    rows = [
        {"timestamp_ms": int(ts[i]), "pin": int(pins[i]), "val": float(vals[i])}
        for i in indices
    ]
    return {
        "rows": rows,
        "total": n,
        "total_matched": total_matched,
        "returned": len(rows),
    }


def main() -> int:
    req = json.load(sys.stdin)
    limit = min(int(req.get("limit", 5000)), 20000)
    pin = req.get("pin")
    from_ms = req.get("from_ms")
    to_ms = req.get("to_ms")

    path, chunk = _resolve_path(req)
    if not os.path.isfile(path):
        print(json.dumps({"ok": False, "error": "file not found", "path": path}))
        return 1

    with h5py.File(path, "r") as f:
        schema = int(f.attrs.get("schema_version", 1))
        if chunk and "chunks" in f and chunk in f["chunks"]:
            body = _read_chunk(f[f"chunks/{chunk}"], limit, pin, from_ms, to_ms)
        elif schema >= 3 and chunk is None:
            all_rows = []
            total = 0
            matched = 0
            for name in sorted(f.get("chunks", {}).keys()):
                part = _read_chunk(f[f"chunks/{name}"], limit, pin, from_ms, to_ms)
                total += part["total"]
                matched += part["total_matched"]
                all_rows.extend(part["rows"])
            if len(all_rows) > limit:
                all_rows = all_rows[-limit:]
            body = {
                "rows": all_rows,
                "total": total,
                "total_matched": matched,
                "returned": len(all_rows),
            }
        else:
            body = _read_chunk(f, limit, pin, from_ms, to_ms)

    print(
        json.dumps(
            {
                "ok": True,
                "file": os.path.basename(path),
                "path": path,
                "chunk": chunk,
                "schema_version": schema,
                **body,
            }
        )
    )
    return 0


if __name__ == "__main__":
    sys.exit(main())
