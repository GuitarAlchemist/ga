"""
Post-process a local SAE training run into a contract-conforming canonical artifact.
Loads the latest <date>-local/ trainer output, generates feature_manifest.jsonl,
fixes metadata (optick_dim per contract, supersedes pointer, narrative), and writes
to state/quality/optick-sae/<date>/ (no -local suffix).

WHY: optick_sae_train.py writes to <date>-local/ (gitignored, per-machine) and
records optick_dim as the actual training dim (124 for v1.8 compact). The contract
field optick_dim wants TotalDimension (240). This script reconciles.
"""
from __future__ import annotations

import json
import sys
from datetime import datetime, timezone
from pathlib import Path

import torch


SUPERSEDED_ARTIFACT_ID = "optick-sae-2026-05-03T20-07-36Z-019def74-topk-sae"


def main() -> int:
    repo = Path(__file__).resolve().parent.parent
    today = datetime.now(timezone.utc).strftime("%Y-%m-%d")
    src_dir = repo / "state" / "quality" / "optick-sae" / f"{today}-local"
    dst_dir = repo / "state" / "quality" / "optick-sae" / today

    if not src_dir.exists():
        print(f"FAIL: {src_dir} not found — run optick_sae_train.py first", file=sys.stderr)
        return 2

    src_artifact = src_dir / "optick-sae-artifact.json"
    if not src_artifact.exists():
        print(f"FAIL: {src_artifact} missing (trainer didn't pass guardrails?)", file=sys.stderr)
        return 2

    src_weights = src_dir / "sae_weights.pt"

    dst_dir.mkdir(parents=True, exist_ok=True)

    # ── 1. Load original artifact + adjust ────────────────────────────────
    art = json.loads(src_artifact.read_text(encoding="utf-8"))

    # WHY override: the trainer records the actual training dim (124 for v1.8
    # compact) but the contract field optick_dim wants TotalDimension (240).
    # Schema's input object has additionalProperties=false so we can't add a
    # parallel compact_training_dim slot; narrative records it explicitly.
    art["input"]["optick_dim"] = 240

    # Update narrative for clarity
    metrics = art["metrics"]
    art["narrative"] = (
        f"Real-corpus run on OPTIC-K v1.8 (240-dim total; 124-dim compact format used for "
        f"SAE input). Top-k SAE + AuxK ghost-grads (aux_alpha=0.1) achieves "
        f"reconstruction_mse {metrics['reconstruction_mse']:.6f}, dead_features_pct "
        f"{metrics['dead_features_pct']:.1f}%, partition_purity_mean "
        f"{metrics['feature_partition_purity_mean']:.2f} on the full 313047-voicing "
        f"corpus. Supersedes the synthetic-data smoke from PR #82 with real-data baseline."
    )[:500]

    # Set supersedes
    art["links"]["supersedes"] = SUPERSEDED_ARTIFACT_ID

    # Rewrite link paths to point at canonical dir (not -local)
    rel_canonical = f"state/quality/optick-sae/{today}"
    art["links"]["feature_activations_parquet"] = f"{rel_canonical}/feature_activations.parquet"
    art["links"]["feature_manifest_jsonl"]      = f"{rel_canonical}/feature_manifest.jsonl"
    art["links"]["training_log"]                = f"{rel_canonical}/training.log"
    art["links"]["model_weights"]               = f"{rel_canonical}/sae_weights.pt"

    # Producer slug: "manual" since local trainer ≠ ix-optick-sae
    # (artifact already records "manual" via the trainer; verify)
    if art.get("trainer") != "manual":
        art["trainer"] = "manual"

    dst_artifact = dst_dir / "optick-sae-artifact.json"
    dst_artifact.write_text(json.dumps(art, indent=2, ensure_ascii=False) + "\n", encoding="utf-8")
    print(f"wrote {dst_artifact}")

    # ── 2. Generate feature_manifest.jsonl ────────────────────────────────
    if src_weights.exists():
        sd = torch.load(src_weights, map_location="cpu", weights_only=True)
        decoder_w = sd.get("decoder.weight")  # (input_dim, dict_size)
        if decoder_w is None:
            print("FAIL: sae_weights.pt missing decoder.weight", file=sys.stderr)
            return 2
        dict_size = decoder_w.shape[1]

        # We don't have activation counts cached; approximate with decoder_norm as
        # a proxy for "feature usefulness" and mark is_alive based on threshold.
        # Activation counts would require re-running the encoder over the corpus —
        # skip for this manifest, emit decoder norms only.
        decoder_norms = decoder_w.norm(dim=0).numpy()
        # is_alive: features whose decoder norm is non-trivial. The trainer's
        # actual dead count (243 alive=781) differs slightly from this proxy
        # since dead means "never activated in training", not "decoder is zero".
        # The artifact JSON's metrics.dead_features_pct is the source of truth;
        # this manifest gives per-feature norm signal for downstream filtering.
        alive_threshold = 1e-3
        dst_manifest = dst_dir / "feature_manifest.jsonl"
        with dst_manifest.open("w", encoding="utf-8") as f:
            for i in range(dict_size):
                norm = float(decoder_norms[i])
                rec = {
                    "feature_idx": i,
                    "is_alive_proxy": bool(norm > alive_threshold),
                    "decoder_norm": round(norm, 6),
                }
                f.write(json.dumps(rec) + "\n")
        print(f"wrote {dst_manifest} ({dict_size} features)")
    else:
        print(f"WARN: {src_weights} not found — skipping feature_manifest.jsonl")

    # ── 3. Subfolder .gitignore (mirror PR #82 pattern) ───────────────────
    gi = dst_dir / ".gitignore"
    gi.write_text(
        "# Large binary outputs from SAE training — not tracked in git.\n"
        "# Regenerate with the canonical trainer (sae-lens or Scripts/optick_sae_train.py).\n"
        "*.parquet\n"
        "*.safetensors\n"
        "*.pt\n"
        "*.log\n",
        encoding="utf-8",
    )
    print(f"wrote {gi}")

    print(f"\nDone. Files to commit:")
    for f in sorted(dst_dir.iterdir()):
        if not any(f.name.endswith(ext) for ext in (".parquet", ".safetensors", ".pt", ".log")):
            print(f"  {f.relative_to(repo)}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
