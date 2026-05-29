#!/usr/bin/env python3
"""List farm archive (v3) and legacy flat *.h5 files."""
from __future__ import annotations

import json
import os
import sys
from datetime import datetime, timezone

import h5py


def _hdf5_dir() -> str:
    return os.environ.get("HDF5_DIR", "/data/hdf5")


def _is_sealed_chunk(chunk_id: str, now: datetime) -> bool:
    try:
        day_s, hour_s = chunk_id.split("_", 1)
        day = datetime.strptime(day_s, "%Y%m%d").replace(tzinfo=timezone.utc)
        hour = int(hour_s)
    except (ValueError, IndexError):
        return False
    if day.date() < now.date():
        return True
    return hour < now.hour


def _list_archive(path: str, farm_id: str) -> dict:
    chunks = []
    with h5py.File(path, "r") as f:
        schema = int(f.attrs.get("schema_version", 0))
        if "chunks" not in f:
            return {"farm_id": farm_id, "archive": "telemetry.h5", "schema_version": schema, "chunks": chunks}
        now = datetime.now(timezone.utc)
        for name in sorted(f["chunks"].keys()):
            grp = f[f"chunks/{name}"]
            rows = int(grp["pin"].shape[0]) if "pin" in grp else 0
            sealed = bool(grp.attrs.get("sealed")) or _is_sealed_chunk(name, now)
            chunks.append(
                {
                    "chunk_id": name,
                    "rows": rows,
                    "mac": str(grp.attrs.get("mac", "")),
                    "sealed": sealed,
                    "size_bytes": 0,
                }
            )
    st = os.stat(path)
    return {
        "farm_id": farm_id,
        "archive": "telemetry.h5",
        "path": path,
        "schema_version": schema,
        "size_bytes": st.st_size,
        "modified_utc": st.st_mtime,
        "chunks": chunks,
    }


def main() -> int:
    d = _hdf5_dir()
    archives = []
    legacy = []
    if os.path.isdir(d):
        for name in sorted(os.listdir(d)):
            full = os.path.join(d, name)
            if name.endswith(".h5") and os.path.isfile(full):
                legacy.append({"name": name, "path": full, "legacy": True})
                continue
            if not os.path.isdir(full):
                continue
            archive = os.path.join(full, "telemetry.h5")
            if os.path.isfile(archive):
                try:
                    archives.append(_list_archive(archive, name))
                except Exception as ex:
                    archives.append({"farm_id": name, "error": str(ex)})
    print(json.dumps({"ok": True, "dir": d, "archives": archives, "legacy_files": legacy}))
    return 0


if __name__ == "__main__":
    sys.exit(main())
