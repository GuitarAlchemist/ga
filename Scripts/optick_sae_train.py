"""
Local OPTIC-K Sparse Autoencoder trainer.

Reads state/voicings/optick.index (OPTK v4 compact, 124-dim L2-normalized vectors),
trains a top-k SAE, emits a contract-conforming artifact at
state/quality/optick-sae/<date>-local/.

Contract: docs/contracts/2026-05-02-optick-sae-artifact.contract.md
Schema:   docs/contracts/optick-sae-artifact.schema.json

WHY local: the cloud agent's checkout doesn't include optick.index (gitignored, 175 MB).
This script runs natively on a machine that has the index, so we get real-data
reconstruction MSE / partition_purity numbers instead of the synthetic-data smoke
the cloud agent produces.

USAGE:
    python Scripts/optick_sae_train.py --epochs 5    # smoke
    python Scripts/optick_sae_train.py --epochs 100  # real run
"""
from __future__ import annotations

import argparse
import hashlib
import json
import struct
import sys
import time
import uuid
from datetime import datetime, timezone
from pathlib import Path

import numpy as np
import torch
import torch.nn as nn

# v1.8 compact layout (similarity partitions only). See
# Common/GA.Business.ML/Embeddings/EmbeddingSchema.cs : SimilarityPartitions.
PARTITIONS_124 = [
    ("STRUCTURE",   0,   24),
    ("MORPHOLOGY",  24,  48),
    ("CONTEXT",     48,  60),
    ("SYMBOLIC",    60,  72),
    ("MODAL",       72,  112),
    ("ROOT",        112, 124),
]


# =============================================================================
# OPTK v4 reader
# =============================================================================

def read_header(path: Path) -> dict:
    with path.open("rb") as f:
        if f.read(4) != b"OPTK":
            raise SystemExit(f"Not an OPTK file: {path}")
        version = struct.unpack("<I", f.read(4))[0]
        if version != 4:
            raise SystemExit(f"Unsupported version {version}")
        f.read(4)  # header_size
        f.read(4)  # schema_hash
        f.read(2)  # endian
        f.read(2)  # padding
        dim = struct.unpack("<I", f.read(4))[0]
        count = struct.unpack("<Q", f.read(8))[0]
        f.read(8)  # instruments + pad
        f.read(48)  # 3 * 16 bytes instr ranges
        f.read(8)  # meta_offsets_off
        vectors_off = struct.unpack("<Q", f.read(8))[0]
        return {"dim": dim, "count": count, "vectors_off": vectors_off}


def load_vectors(path: Path) -> np.ndarray:
    h = read_header(path)
    arr = np.memmap(
        path,
        dtype=np.float32,
        mode="r",
        offset=h["vectors_off"],
        shape=(h["count"], h["dim"]),
    )
    return np.array(arr)  # copy out so we can drop the mmap


def file_sha256(path: Path) -> str:
    h = hashlib.sha256()
    with path.open("rb") as f:
        for chunk in iter(lambda: f.read(1 << 20), b""):
            h.update(chunk)
    return h.hexdigest()


# =============================================================================
# Top-k SAE
# =============================================================================

class TopKSAE(nn.Module):
    def __init__(self, input_dim: int, dict_size: int, k: int):
        super().__init__()
        self.input_dim = input_dim
        self.dict_size = dict_size
        self.k = k
        self.encoder = nn.Linear(input_dim, dict_size, bias=True)
        self.decoder = nn.Linear(dict_size, input_dim, bias=True)
        # Initialize decoder weights as transpose of encoder for tied-ish start.
        with torch.no_grad():
            self.decoder.weight.copy_(self.encoder.weight.T)

    def encode_pre_topk(self, x: torch.Tensor) -> torch.Tensor:
        return torch.relu(self.encoder(x))

    def encode(self, x: torch.Tensor) -> torch.Tensor:
        h = self.encode_pre_topk(x)
        # Top-k mask: keep only the k largest activations per row.
        topk_vals, topk_idx = torch.topk(h, k=self.k, dim=-1)
        mask = torch.zeros_like(h)
        mask.scatter_(-1, topk_idx, 1.0)
        return h * mask

    def forward(self, x: torch.Tensor) -> tuple[torch.Tensor, torch.Tensor]:
        a = self.encode(x)
        x_hat = self.decoder(a)
        return x_hat, a

    def auxk_reconstruct(self, x: torch.Tensor, dead_mask: torch.Tensor, k_aux: int) -> torch.Tensor | None:
        """Reconstruct using only dead features (Anthropic ghost-grads / AuxK).
        WHY: dead features get zero gradient through the main top-k path; AuxK threads a
        small auxiliary loss through them so they can wake up. Returns x_hat_aux or None
        if no dead features available."""
        if not dead_mask.any():
            return None
        h = self.encode_pre_topk(x)
        h_dead = h * dead_mask.to(h.dtype)
        n_dead = int(dead_mask.sum().item())
        if k_aux >= n_dead:
            return self.decoder(h_dead)
        topk_vals, topk_idx = torch.topk(h_dead, k=k_aux, dim=-1)
        mask = torch.zeros_like(h_dead)
        mask.scatter_(-1, topk_idx, 1.0)
        return self.decoder(h_dead * mask)


# =============================================================================
# Metrics
# =============================================================================

def compute_reconstruction(model: TopKSAE, X: torch.Tensor, batch: int = 8192) -> tuple[float, float]:
    model.eval()
    sse = 0.0
    sst = 0.0
    mean = X.mean(dim=0)
    with torch.no_grad():
        for i in range(0, len(X), batch):
            xb = X[i:i + batch]
            xh, _ = model(xb)
            sse += float(((xh - xb) ** 2).sum())
            sst += float(((xb - mean) ** 2).sum())
    n_elem = X.numel()
    mse = sse / n_elem
    r2 = 1.0 - (sse / sst) if sst > 0 else 0.0
    return mse, r2


def compute_activation_stats(model: TopKSAE, X: torch.Tensor, batch: int = 8192) -> dict:
    model.eval()
    dict_size = model.dict_size
    feature_active_count = torch.zeros(dict_size)
    active_per_row = []
    with torch.no_grad():
        for i in range(0, len(X), batch):
            a = model.encode(X[i:i + batch])
            feature_active_count += (a > 0).float().sum(dim=0)
            active_per_row.append((a > 0).sum(dim=-1).cpu().numpy())
    active_per_row = np.concatenate(active_per_row)
    n_rows = len(X)
    frequency = (feature_active_count / n_rows).numpy()
    dead = int((feature_active_count == 0).sum())
    return {
        "frequency": frequency,
        "active_per_row_p50": int(np.percentile(active_per_row, 50)),
        "active_per_row_p95": int(np.percentile(active_per_row, 95)),
        "dead_features": dead,
        "high_freq_count": int((frequency >= 0.10).sum()),
        "low_freq_count": int((frequency < 0.001).sum()),
    }


def compute_partition_purity(model: TopKSAE) -> tuple[float, float]:
    """For each feature, fraction of |decoder_weight| concentrated in its dominant partition."""
    W = model.decoder.weight.detach().cpu().numpy()  # (input_dim, dict_size)
    purities = []
    for j in range(W.shape[1]):
        col = np.abs(W[:, j])
        total = col.sum() + 1e-12
        partition_mass = []
        for _, lo, hi in PARTITIONS_124:
            partition_mass.append(col[lo:hi].sum())
        purity = max(partition_mass) / total
        purities.append(purity)
    purities = np.array(purities)
    return float(purities.mean()), float(np.percentile(purities, 10))


# =============================================================================
# Training
# =============================================================================

def train(args) -> dict:
    repo = Path(__file__).resolve().parent.parent
    index_path = repo / "state" / "voicings" / "optick.index"
    if not index_path.exists():
        raise SystemExit(f"Missing index: {index_path}")

    print(f"[1/6] Loading {index_path}", flush=True)
    t0 = time.time()
    X_np = load_vectors(index_path)
    sha = file_sha256(index_path)
    print(f"    shape {X_np.shape}  sha256 {sha[:16]}...  load {time.time() - t0:.1f}s", flush=True)

    rng = np.random.default_rng(args.seed)
    perm = rng.permutation(len(X_np))
    n_val = int(0.05 * len(X_np))
    val_idx = perm[:n_val]
    train_idx = perm[n_val:]
    X_train = torch.from_numpy(X_np[train_idx]).float()
    X_val = torch.from_numpy(X_np[val_idx]).float()
    print(f"    train {len(X_train)}  val {len(X_val)}", flush=True)

    print(f"[2/6] Building TopKSAE (dict={args.dict_size}, k={args.k_sparse})", flush=True)
    torch.manual_seed(args.seed)
    model = TopKSAE(input_dim=X_np.shape[1], dict_size=args.dict_size, k=args.k_sparse)
    optimizer = torch.optim.Adam(model.parameters(), lr=args.lr)

    aux_msg = f", aux_alpha={args.aux_alpha} aux_k={args.aux_k}" if args.use_ghost_grads else ""
    print(f"[3/6] Training {args.epochs} epochs (batch={args.batch_size}, lr={args.lr}{aux_msg})", flush=True)
    n_train = len(X_train)
    losses = []
    train_t0 = time.time()
    dead_mask = torch.zeros(args.dict_size, dtype=torch.bool)
    for epoch in range(args.epochs):
        epoch_t0 = time.time()
        model.train()
        order = torch.randperm(n_train)
        epoch_loss = 0.0
        epoch_aux_loss = 0.0
        n_batches = 0
        epoch_active_counts = torch.zeros(args.dict_size)
        for i in range(0, n_train, args.batch_size):
            batch_idx = order[i:i + args.batch_size]
            xb = X_train[batch_idx]
            xh, a = model(xb)
            main_loss = ((xh - xb) ** 2).mean()
            with torch.no_grad():
                epoch_active_counts += (a > 0).float().sum(dim=0)
            total_loss = main_loss
            aux_term = 0.0
            if args.use_ghost_grads and dead_mask.any():
                residual = (xb - xh).detach()
                xh_aux = model.auxk_reconstruct(xb, dead_mask, args.aux_k)
                if xh_aux is not None:
                    aux_loss = ((xh_aux - residual) ** 2).mean()
                    total_loss = main_loss + args.aux_alpha * aux_loss
                    aux_term = float(aux_loss.item())
            optimizer.zero_grad()
            total_loss.backward()
            optimizer.step()
            epoch_loss += main_loss.item()
            epoch_aux_loss += aux_term
            n_batches += 1
        avg = epoch_loss / max(1, n_batches)
        avg_aux = epoch_aux_loss / max(1, n_batches)
        losses.append(avg)
        # Update dead_mask for next epoch's ghost-grad pass.
        dead_mask = (epoch_active_counts == 0)
        n_dead_now = int(dead_mask.sum().item())
        aux_str = f"  aux {avg_aux:.6f}  dead {n_dead_now}" if args.use_ghost_grads else ""
        print(f"    epoch {epoch + 1:3d}/{args.epochs}  loss {avg:.6f}{aux_str}  {time.time() - epoch_t0:.1f}s", flush=True)
    print(f"    total train time {time.time() - train_t0:.1f}s", flush=True)

    print(f"[4/6] Computing metrics on val slice + full train (for accurate dead-feature count)", flush=True)
    mse, r2 = compute_reconstruction(model, X_val)
    # WHY full-train activation counts: a feature that activates 1-in-30K rows would look dead
    # on a 15K val slice; the contract's dead_features_pct should reflect the full corpus.
    act = compute_activation_stats(model, X_train)
    purity_mean, purity_p10 = compute_partition_purity(model)
    print(f"    reconstruction_mse {mse:.6f}  r2 {r2:.4f}", flush=True)
    print(f"    active_per_row p50 {act['active_per_row_p50']} p95 {act['active_per_row_p95']}", flush=True)
    print(f"    dead {act['dead_features']}/{model.dict_size} ({100 * act['dead_features'] / model.dict_size:.1f}%)", flush=True)
    print(f"    partition_purity mean {purity_mean:.3f}  p10 {purity_p10:.3f}", flush=True)

    # ── Guardrail enforcement (contract §5) ──
    dead_pct = 100.0 * act["dead_features"] / args.dict_size
    guardrail_violations = []
    if mse > 0.05:
        guardrail_violations.append(f"reconstruction_mse {mse:.6f} > 0.05")
    if dead_pct > 30.0:
        guardrail_violations.append(f"dead_features_pct {dead_pct:.1f} > 30.0")
    if guardrail_violations and not args.allow_guardrail_violation:
        print("\n[FAIL] Contract §5 guardrail violation — NOT emitting artifact:", flush=True)
        for v in guardrail_violations:
            print(f"    {v}", flush=True)
        print("    (rerun with --allow-guardrail-violation to emit anyway, or smaller --dict-size)", flush=True)
        return None

    # ── Build artifact ──
    print(f"[5/6] Writing artifact", flush=True)
    produced_at = datetime.now(timezone.utc)
    artifact_id = (
        f"optick-sae-{produced_at.strftime('%Y-%m-%dT%H-%M-%SZ')}"
        f"-{uuid.uuid4().hex[:8]}-local-trainer"
    )
    out_dir = repo / "state" / "quality" / "optick-sae" / produced_at.strftime("%Y-%m-%d-local")
    out_dir.mkdir(parents=True, exist_ok=True)

    artifact = {
        "schema_version": 1,
        "artifact_id": artifact_id,
        "trained_at": produced_at.strftime("%Y-%m-%dT%H:%M:%SZ"),
        "trainer": "manual",
        "trainer_version": "0.1.0",
        "input": {
            "optick_index_path": "state/voicings/optick.index",
            "optick_index_sha": f"sha256:{sha}",
            "optick_dim": int(X_np.shape[1]),
            "schema_version": "OPTIC-K-v1.8",
            "corpus_size": int(len(X_np)),
            "partitions_used": [name for name, _, _ in PARTITIONS_124],
        },
        "model": {
            "kind": "topk_sae",
            "dict_size": args.dict_size,
            "k_sparse": args.k_sparse,
            "training": {
                "epochs": args.epochs,
                "batch_size": args.batch_size,
                "lr": args.lr,
                "seed": args.seed,
                "loss_final": losses[-1],
                "sparsity_actual_mean": float(act["active_per_row_p50"]),
            },
        },
        "metrics": {
            "reconstruction_mse": mse,
            "reconstruction_r2": r2,
            "active_features_per_voicing_p50": act["active_per_row_p50"],
            "active_features_per_voicing_p95": act["active_per_row_p95"],
            "dead_features_pct": 100.0 * act["dead_features"] / args.dict_size,
            "feature_partition_purity_mean": purity_mean,
            "feature_partition_purity_p10": purity_p10,
        },
        "features_summary": {
            "total": args.dict_size,
            "alive": args.dict_size - act["dead_features"],
            "high_frequency_count": act["high_freq_count"],
            "low_frequency_count": act["low_freq_count"],
        },
        "links": {
            "feature_activations_parquet": str((out_dir / "feature_activations.parquet").relative_to(repo)).replace("\\", "/"),
            "feature_manifest_jsonl":      str((out_dir / "feature_manifest.jsonl").relative_to(repo)).replace("\\", "/"),
            "training_log":                str((out_dir / "training.log").relative_to(repo)).replace("\\", "/"),
            "model_weights":               str((out_dir / "sae_weights.pt").relative_to(repo)).replace("\\", "/"),
            "supersedes": None,
        },
        "narrative": (
            f"Local Phase 1 trainer run on real OPTIC-K v1.8 (124-dim compact, {len(X_np)} voicings). "
            f"Reconstruction MSE {mse:.4f}, dead-features {100 * act['dead_features'] / args.dict_size:.1f}%, "
            f"partition purity mean {purity_mean:.2f}. "
            f"Pre-Phase-1-agent local validation."
        )[:500],
    }

    artifact_path = out_dir / "optick-sae-artifact.json"
    with artifact_path.open("w", encoding="utf-8") as f:
        json.dump(artifact, f, indent=2, ensure_ascii=False)

    # Save model weights (.pt; safetensors omitted to avoid extra dep)
    torch.save(model.state_dict(), out_dir / "sae_weights.pt")

    # Training log
    with (out_dir / "training.log").open("w", encoding="utf-8") as f:
        for i, l in enumerate(losses):
            f.write(f"epoch {i + 1:3d}  loss {l:.6f}\n")
        f.write(f"\nfinal: mse={mse:.6f} r2={r2:.4f} purity_mean={purity_mean:.3f}\n")

    print(f"[6/6] {artifact_path}", flush=True)
    print(f"    out dir: {out_dir}", flush=True)
    return artifact


def parse_args() -> argparse.Namespace:
    p = argparse.ArgumentParser(description="OPTIC-K SAE trainer")
    p.add_argument("--dict-size", type=int, default=1024)
    p.add_argument("--k-sparse", type=int, default=32)
    p.add_argument("--epochs", type=int, default=5)
    p.add_argument("--batch-size", type=int, default=4096)
    p.add_argument("--lr", type=float, default=1e-3)
    p.add_argument("--seed", type=int, default=42)
    p.add_argument(
        "--allow-guardrail-violation",
        action="store_true",
        help="Emit an artifact even when contract §5 guardrails are violated. "
             "Use ONLY for diagnostic sweeps; production runs should leave this off.",
    )
    p.add_argument(
        "--use-ghost-grads",
        action="store_true",
        help="Enable AuxK / ghost-grads auxiliary loss to revive dead features. "
             "WHY: vanilla top-k SAE has structural ~40-65%% dead-feature rate on "
             "this corpus per docs/learnings/2026-05-03-optick-sae-vanilla-topk-dead-features.md.",
    )
    p.add_argument("--aux-alpha", type=float, default=0.03,
                   help="Weight on the AuxK auxiliary loss (default 0.03 per Anthropic 2024).")
    p.add_argument("--aux-k", type=int, default=64,
                   help="Top-k_aux from the dead-feature pool (default 64 = 2x main k).")
    return p.parse_args()


if __name__ == "__main__":
    args = parse_args()
    sys.exit(0 if train(args) else 1)
