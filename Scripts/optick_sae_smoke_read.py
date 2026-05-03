"""
Smoke test: parse OPTK v4 header + first few vectors from state/voicings/optick.index.
Validates that we can read the binary format from Python before committing to a full trainer.

Format reference: Common/GA.Business.ML/Search/OptickIndexReader.cs lines 57-104.
"""
from __future__ import annotations

import struct
import sys
from pathlib import Path

import numpy as np


def read_header(path: Path) -> dict:
    """Parse the OPTK v4 header. Returns offsets + dims + count."""
    with path.open("rb") as f:
        magic = f.read(4)
        if magic != b"OPTK":
            raise SystemExit(f"Not an OPTK file: magic={magic!r}")

        version = struct.unpack("<I", f.read(4))[0]
        if version != 4:
            raise SystemExit(f"Unsupported version {version}")

        header_size = struct.unpack("<I", f.read(4))[0]
        schema_hash = struct.unpack("<I", f.read(4))[0]
        endian = struct.unpack("<H", f.read(2))[0]
        _r = f.read(2)  # padding

        dim = struct.unpack("<I", f.read(4))[0]
        count = struct.unpack("<Q", f.read(8))[0]

        instruments = f.read(1)[0]
        _pad = f.read(7)

        # 3 instrument ranges of (byte_offset_u64, count_u64) — 16 bytes each
        instr_ranges = []
        for _ in range(3):
            byte_off = struct.unpack("<Q", f.read(8))[0]
            cnt = struct.unpack("<Q", f.read(8))[0]
            instr_ranges.append((byte_off, cnt))

        meta_offsets_off = struct.unpack("<Q", f.read(8))[0]
        vectors_off = struct.unpack("<Q", f.read(8))[0]
        meta_off = struct.unpack("<Q", f.read(8))[0]
        meta_len = struct.unpack("<Q", f.read(8))[0]

        return {
            "version": version,
            "header_size": header_size,
            "schema_hash_hex": f"{schema_hash:08x}",
            "endian_marker": f"0x{endian:04x}",
            "instruments": instruments,
            "dim": dim,
            "count": count,
            "instrument_ranges_byteoff_count": instr_ranges,
            "meta_offsets_off": meta_offsets_off,
            "vectors_off": vectors_off,
            "meta_off": meta_off,
            "meta_len": meta_len,
        }


def load_vectors_subset(path: Path, vectors_off: int, dim: int, count: int, n: int) -> np.ndarray:
    """Memory-map and read first n vectors as float32."""
    n = min(n, count)
    bytes_per_vec = dim * 4
    arr = np.memmap(
        path,
        dtype=np.float32,
        mode="r",
        offset=vectors_off,
        shape=(count, dim),
    )
    return np.array(arr[:n])  # copy out of mmap


def main() -> int:
    repo = Path(__file__).resolve().parent.parent
    index_path = repo / "state" / "voicings" / "optick.index"
    if not index_path.exists():
        print(f"FAIL: {index_path} not found", file=sys.stderr)
        return 2

    header = read_header(index_path)
    print("=== OPTK v4 header ===")
    for k, v in header.items():
        print(f"  {k}: {v}")

    expected_dim = 124  # v1.8 compact = STRUCTURE+MORPHOLOGY+CONTEXT+SYMBOLIC+MODAL+ROOT
    if header["dim"] != expected_dim:
        print(f"WARN: header dim={header['dim']} != expected {expected_dim} (v1.8 compact). Schema bumped?")

    sample = load_vectors_subset(
        index_path,
        header["vectors_off"],
        header["dim"],
        header["count"],
        n=10,
    )
    print(f"\n=== first 10 vectors ===")
    print(f"  shape: {sample.shape}")
    print(f"  dtype: {sample.dtype}")
    print(f"  per-vector L2 norms (expect ≈ 1 since the index is L2-normalized):")
    for i, v in enumerate(sample):
        print(f"    [{i}] norm={np.linalg.norm(v):.6f}, mean={v.mean():.6f}, std={v.std():.6f}")

    # Pull a representative slice across the full corpus to check distribution
    full = np.memmap(
        index_path,
        dtype=np.float32,
        mode="r",
        offset=header["vectors_off"],
        shape=(header["count"], header["dim"]),
    )
    n_sample = min(50_000, header["count"])
    rng = np.random.default_rng(42)
    idx = rng.choice(header["count"], size=n_sample, replace=False)
    sub = np.array(full[idx])
    print(f"\n=== {n_sample}-row random sample ===")
    norms = np.linalg.norm(sub, axis=1)
    print(f"  norm mean: {norms.mean():.6f}  std: {norms.std():.6f}")
    print(f"  per-dim mean abs: {np.abs(sub).mean():.6f}")
    nonzero_per_row = (sub != 0).sum(axis=1)
    print(f"  nonzero entries per vector: p50={int(np.percentile(nonzero_per_row, 50))}  p95={int(np.percentile(nonzero_per_row, 95))}  max={nonzero_per_row.max()}")

    return 0


if __name__ == "__main__":
    raise SystemExit(main())
