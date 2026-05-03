---
title: "arch: OPTIC-K Sparse Autoencoder — feature decomposition + drift tracking"
type: arch
status: draft
date: 2026-05-02
origin: in-conversation, 2026-05-02 evening (post-Phase-0 QA Architect)
contract: docs/contracts/2026-05-02-optick-sae-artifact.contract.md
reversibility: two-way doors except artifact schema (one-way at v1.0.0 freeze, target 2026-Q3)
revisit_trigger: end of Phase 2 (first drift signal in a real verdict) → review whether features earn their cost
companion_plan: docs/plans/2026-05-02-arch-qa-architect-tribunal-plan.md
---

# OPTIC-K Sparse Autoencoder — Feature Decomposition + Drift Tracking

## Overview

Train a sparse autoencoder over the 313k-voicing OPTIC-K v1.8 corpus (240-dim, 11 partitions including the 12-dim ROOT partition added 2026-04-19) to:
1. **Validate the partition design.** Hand-engineered partitions (STRUCTURE / MORPHOLOGY / etc.) are a hypothesis; SAE features either respect those boundaries or expose where the hypothesis is wrong.
2. **Discover latent musical concepts.** Features that don't map cleanly to a single partition are interpretation candidates ("voicings that resolve like dominants but aren't formally dominant," etc.).
3. **Drift detection for the QA tribunal.** Plug feature population stats into `qa_score_quality_drift` so the tribunal sees more than just retrieval-latency p50.

Artifact contract: [2026-05-02-optick-sae-artifact.contract.md](../contracts/2026-05-02-optick-sae-artifact.contract.md).

## Problem Frame

OPTIC-K is hand-engineered, fixed-dimension, and reused everywhere (similarity search, RAG, voicing-by-hand, the chatbot). Today the only feedback loop is the `ix-autoresearch` weights-tuning driver — which optimizes *one number* (retrieval-vs-leak score) and never tells us *what the embedding actually captures*.

A senior data engineer's instinct here is: train an interpretable decomposition, look at what the features are, and use feature stability as a drift signal. The cost is bounded (training is small for 313k × 240), the upside is a permanent interpretability layer that compounds.

This isn't a refactor of OPTIC-K. The 240-dim embedding stays untouched. The SAE is a *lens* over it.

## Requirements Trace

- R1. SAE training fits in < 30 minutes on a single GPU (or ≤ 2 hours CPU) for the current corpus size.
- R2. Artifact conforms to [contract v0.1+](../contracts/2026-05-02-optick-sae-artifact.contract.md).
- R3. Reconstruction MSE ≤ 0.05 on a held-out 5% slice (artifact-level guardrail).
- R4. Dead-feature rate ≤ 30% (auto-retrain with smaller dict if exceeded).
- R5. Cross-repo handoff is JSON-on-disk only (same pattern as `optick-weights-config.contract.md`).
- R6. `qa_score_quality_drift` consumes the artifact and emits `evidence{kind: quality_snapshot}` with feature-population delta vs the prior artifact.
- R7. Manual feature labels ≥ 50% of high-frequency features by end of Phase 3.
- R8. Demerzel `qa-architect-cycle.ixql` orchestrates re-training opportunistically (when index SHA changes).

## Scope Boundaries

- In scope: ix Rust crate (orchestrator), Python training (PyTorch + `sae-lens`), GA consumer, Demerzel orchestration.
- In scope: feature interpretation as manual labeling — not auto-naming.
- Out of scope: changing OPTIC-K embedding dimensions or partition definitions. SAE is read-only over OPTIC-K.
- Out of scope: retrieval reranking via SAE features (Phase 4 / future).
- Out of scope: cross-repo deployment of the SAE model itself — artifact JSON is the only cross-repo surface.

## Context & Research

### Relevant precedents
- **`ix-autoresearch` ↔ GA contract** ([optick-weights-config.contract.md](../contracts/2026-04-27-optick-weights-config.contract.md)): same JSON-on-disk handoff pattern. Same producer-in-Rust / consumer-in-C# split. Reuse the structure, don't reinvent.
- **QA Architect Tribunal Phase 0** ([2026-05-02-arch-qa-architect-tribunal-plan.md](2026-05-02-arch-qa-architect-tribunal-plan.md)): the verdict contract and the MCP-primitive shape both inform this work. SAE drift evidence flows through the same `qa_score_quality_drift` primitive.
- **Anthropic's *Scaling Monosemanticity*** (2024): trained SAEs over Claude 3 Sonnet activations and recovered ~16M interpretable features. Methodology directly transfers; scale is much smaller (hundreds of features over 240-dim, not millions over thousands of dims).
- **`sae-lens` library** (Joseph Bloom et al.): the field-standard PyTorch tooling. Avoid reimplementing in Rust.

### Tooling decision
- **Rust orchestrator + Python trainer via subprocess.** Same pattern as `FretboardVoicingsCLI ↔ ix-autoresearch`. Rust owns the CLI surface, file I/O, and contract serialization. Python owns the actual SAE training. JSON on disk between them.
- **Why not pure Rust:** Every SAE library, paper, and reference implementation is PyTorch. `tch-rs` exists but the ecosystem is thin. Speed of iteration > language purity.
- **Why not pure Python:** ix already lives in Rust. Putting a Python project at the top level breaks the established pattern. The orchestrator is small.

## Key Technical Decisions

- **Top-k SAE for Phase 1**, ReLU/Gated as Phase-3 alternatives. Top-k is the most interpretable starting point and `sae-lens` supports it directly.
- **Dictionary size 1024** (~4.3× embedding dim of 240). Standard SAE expansion ratio. Tunable in Phase 2 if dead-feature rate exceeds 30%.
- **k_sparse = 32**. Each voicing activates ~3% of features. Reasonable starting point; Phase 2 sweeps this if reconstruction is poor.
- **Train over partitions [IDENTITY, STRUCTURE, MORPHOLOGY, CONTEXT, SYMBOLIC, MODAL, ROOT]** — the similarity-weighted partitions plus IDENTITY. ROOT was added in v1.8 (2026-04-19) and is similarity-weighted (0.05); include from Phase 1. Skip info-only partitions (EXTENSIONS, SPECTRAL, HIERARCHY, ATONAL_MODAL) — they don't carry similarity weight and adding them inflates the input space without adding signal the SAE can decompose meaningfully.
- **Held-out 5% slice for reconstruction MSE.** Standard practice; protects against overfitting on the dictionary.
- **Artifact-level supersedes chain.** Each retrain references its prior. Lets the drift detector walk back N artifacts.

## Phasing

Each phase ends with a working slice. No phase is gated on later phases.

### Phase 0 — Contract + scheduling (this document)
- Artifact contract drafted (v0.1.0).
- Plan reviewed.
- Implementation agent scheduled for Tue 2026-05-19 09:00 UTC (after QA Architect Phase 1 lands).
- **Done when:** contract + plan merged to `main`, agent scheduled.

### Phase 1 — Train + emit (week of 2026-05-19)
- `ix/crates/ix-optick-sae` skeleton: reads `state/voicings/optick.index`, shells out to a Python trainer, validates artifact JSON, writes to `state/quality/optick-sae/<date>/`.
- Python trainer: `sae-lens` config, top-k SAE, dict_size=1024, k=32. Held-out validation. Logs to plain text.
- Artifact JSON conforms to contract; companion JSON Schema generated from the contract markdown.
- One end-to-end smoke run on the current OPTIC-K index. Reconstruction MSE recorded.
- **Done when:** `cargo run --bin ix-optick-sae train` produces a valid artifact + parquet; reconstruction MSE under 0.05.

### Phase 2 — Drift integration (week of 2026-05-26)
- `Apps/GaQaMcp/Tools/QaTools.cs`: `qa_score_quality_drift` reads the latest two SAE artifacts and computes feature-population deltas. Emits `evidence{kind: quality_snapshot}` with diff summary.
- Demerzel `qa-architect-cycle.ixql`: detects when `optick.index` SHA changed, triggers `ix-optick-sae train`, then runs the drift evaluation.
- **Done when:** a deliberately-modified index produces a verdict with non-trivial drift evidence.

### Phase 3 — Feature interpretation (week of 2026-06-02)
- Notebook (Jupyter or `.dib`) under `Notebooks/Research/`: walks top-K voicings per feature, renders them via the existing voicing renderer, prompts the analyst for a manual label.
- Labels write back to `feature_manifest.jsonl` (out-of-band — not strict producer obligation).
- Goal: label ≥ 50% of high-frequency features.
- **Done when:** ≥ 50% of `high_frequency_count` features have a non-null `manual_label`; surprising findings logged in `docs/learnings/`.

### Phase 4 — Optional retrieval reranking (deferred)
- Use SAE feature similarity as an alternative / supplement to OPTIC-K cosine for retrieval.
- Out of scope for now. Revisit only if Phase 3 finds genuinely useful features.

## Instrumentation

| Metric | Baseline | Direction | Guardrail | Storage |
|---|---|---|---|---|
| Reconstruction MSE | none (new) | ≤ 0.02 by Phase 2; ≤ 0.05 hard limit | > 0.05 fails the run, no artifact emitted | artifact field |
| Dead-feature rate | none (new) | ≤ 15% by Phase 3 | > 30% retrains with smaller dict | artifact field |
| Feature partition purity (mean) | none (new) | ≥ 0.7 expected; if ≪ 0.5 the schema needs revisiting | < 0.4 escalates to a schema-review issue | artifact field |
| Manually labeled feature % | 0 | ≥ 50% by end of Phase 3 | < 20% by 2026-06-30 = invest more | manifest |
| Time-to-detect-drift after re-index | currently undetected | ≤ 1 hour after index SHA changes | > 24h = orchestration broken | sweep verdict |

## One-Way Doors

- **D1.** Artifact schema v1.0.0 freeze (target 2026-Q3 after 2-sprint soak).
- **D2.** `trainer` slug enum.
- **D3.** Storage layout under `state/quality/optick-sae/`.

Two-way doors: SAE architecture choice, dictionary size, sparsity penalty, training corpus subset, language of trainer (could move to Rust later).

## Risks

| Risk | Mitigation |
|---|---|
| SAE finds nothing — corpus is already monosemantic by hand-engineering | That's a *positive* null result. Schema validated. Cost was a few hours of training; phase plan halts at Phase 1 if reconstruction works but features don't add information. |
| Training cost balloons | Phase 1 caps dict_size=1024. If we ever scale to 16k features, revisit cost. |
| Drift detection produces noise more than signal | Phase 2 gate: only emit drift evidence when feature population deltas exceed a learned threshold. |
| OPTIC-K schema changes (v1.8) invalidate existing artifacts | Contract pins `input.schema_version`. Old artifacts auto-supersede on schema bump. |
| Manual feature labeling never catches up | Track labeled_features_pct as a governance metric; Phase 4 deferred until labels exist. |
| Conflict with the user's Phase 1 QA WIP (Common/GA.Business.ML/Quality/*) | Implementation agent's brief explicitly avoids modifying that namespace; SAE drift integrates via the existing `qa_score_quality_drift` primitive surface, not via Quality/* internals. |

## Sign-off Required Before Phase 1

1. Approve the artifact JSON shape (especially metric definitions and partition_purity computation).
2. Confirm `ix-optick-sae` crate name + location.
3. Confirm `sae-lens` as the trainer dependency.
4. Confirm Tue 2026-05-19 09:00 UTC schedule (1 day after QA Architect Phase 1 fires).

Phase 0 work is reversible — no code shipped yet, no schema frozen.
