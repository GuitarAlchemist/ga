---
title: OPTIC-K SAE Phase 1 — partition drift and Windows python3 stub
date: 2026-05-05
category: integration-issues
module: ix-optick-sae
problem_type: integration_issue
component: tooling
symptoms:
  - "PHASE1_PARTITIONS in Rust + Python used [IDENTITY, STRUCTURE, MORPHOLOGY, CONTEXT, SYMBOLIC, MODAL] (118 dims) instead of canonical [STRUCTURE, MORPHOLOGY, CONTEXT, SYMBOLIC, MODAL, ROOT] (124 dims)"
  - "Producer artifacts and canonical baseline measure different SAE feature spaces, making cross-artifact drift detection meaningless"
  - "Recurrence of PR #82 bug class — ROOT partition (OPTIC-K v1.8, slots 228-239) silently omitted again"
  - "Rust trainer hardcoded Command::new(\"python3\") — on Windows resolves to Microsoft Store stub with no packages, crashing with ModuleNotFoundError: No module named 'numpy'"
  - "Local Windows build success masked the runtime divergence across python3 (POSIX) vs python (Windows python.org) executable resolution"
root_cause: config_error
resolution_type: code_fix
severity: high
tags:
  - optic-k
  - sae
  - cross-platform
  - schema-drift
  - rust
  - python
  - windows
  - phase-1
related_components:
  - GA.Business.ML/Embeddings/EmbeddingSchema.cs
  - ix-optick-sae
  - optick-sae-artifact.json
---

# OPTIC-K SAE Phase 1 — partition drift and Windows python3 stub

## Problem

Phase 1 OPTIC-K SAE scaffold (ix #29) shipped with two correctness bugs that survived a clean `cargo build` and 7 passing unit tests: the producer's `PHASE1_PARTITIONS` constant drifted from the canonical artifact baseline (omitting `ROOT`, including `IDENTITY`), and the Rust trainer hardcoded `python3` which resolves to the Microsoft Store stub on Windows. Together they meant cross-artifact drift detection compared incomparable feature spaces, and end-to-end smoke runs failed instantly on Windows.

## Symptoms

- First synthetic smoke run aborted with `ModuleNotFoundError: No module named 'numpy'` despite numpy being installed in the working Python.
- Rust trainer exited with `error: Python trainer exited with unexpected code 1` on Windows.
- Producer emitted `partitions_used = [IDENTITY, STRUCTURE, MORPHOLOGY, CONTEXT, SYMBOLIC, MODAL]` (118 dims) while consumer baseline expected `[STRUCTURE, MORPHOLOGY, CONTEXT, SYMBOLIC, MODAL, ROOT]` (124 dims).
- `qa_score_quality_drift` would silently compare incomparable SAE artifacts across the cross-repo boundary, producing meaningless drift numbers.
- Same partition-list bug class as PR #82 — `ROOT` missed for the second time.

## What Didn't Work

- **Trusting green local checks.** `cargo build` clean and 7/7 unit tests passing implied correctness. The tests only validated structural shape, never asserted `PHASE1_PARTITIONS` equals the canonical artifact's `partitions_used`. Symptom-free until end-to-end smoke.
- **Assuming a pip install gap.** The numpy `ModuleNotFoundError` initially looked like a missing dependency. Investigation revealed two distinct Pythons on the machine: `python3` → Microsoft Store stub at `C:/Users/spare/AppData/Local/Microsoft/WindowsApps/python3.exe` (no packages), `python` → real interpreter at `C:/Users/spare/AppData/Local/Programs/Python/Python314/python.exe` (with PyTorch and numpy).
- **`git stash` mid-rescue.** Stash ran clean but the cherry-pick that followed conflicted because the source branch contained an unrelated parallel-session commit. Had to fall back to `git checkout <commit> -- <file>` to extract only the relevant changes.

## Solution

`crates/ix-optick-sae/src/lib.rs`:

```rust
// Before
pub const PHASE1_PARTITIONS: &[&str] = &[
    "IDENTITY", "STRUCTURE", "MORPHOLOGY", "CONTEXT", "SYMBOLIC", "MODAL",
];

// After
// Canonical Phase 1 partition set — similarity-relevant only.
// IDENTITY (0..6) is excluded because it encodes the lowest pitch's
// (octave, pitch_class) as identity tags, not similarity features.
// ROOT (228..240) is included because it carries chord-root identity
// which is critical for similarity comparisons.
// Source: state/quality/optick-sae/2026-05-04/optick-sae-artifact.json (canonical baseline).
pub const PHASE1_PARTITIONS: &[&str] = &[
    "STRUCTURE", "MORPHOLOGY", "CONTEXT", "SYMBOLIC", "MODAL", "ROOT",
];
```

`crates/ix-optick-sae/src/trainer.rs`:

```rust
// Added
pub const fn default_python_bin() -> &'static str {
    if cfg!(target_os = "windows") {
        "python"
    } else {
        "python3"
    }
}

// Changed signature
pub fn run_python_trainer(
    script: &Path,
    config: &TrainConfig,
    python_bin: &str,           // new
) -> Result<(), TrainerError> {
    let mut cmd = Command::new(python_bin);
    // ...
}
```

`crates/ix-optick-sae/src/bin/ix-optick-sae.rs`:

```rust
/// Python interpreter to invoke. Default is platform-appropriate
/// (`python` on Windows because `python3` typically resolves to the
/// Microsoft Store stub; `python3` on POSIX per PEP 394).
/// Override to point at a venv: `--python-bin .venv/bin/python`.
#[arg(long, default_value_t = default_python_bin().to_string())]
python_bin: String,
```

`python/train.py` (consumer side of the partition list):

- `PHASE1_PARTITIONS` updated to match (124 dims, includes `ROOT`, excludes `IDENTITY`).
- Added `compact_training_dim` field to emitted artifact (contract v0.1.1 compliance — disambiguates the 240-dim total embedding from the 124-dim slice the SAE trains on).

End-to-end synthetic smoke (Rust binary → Python subprocess → emitted JSON → Rust validation): MSE 0.0039, dead 5.5%, schema validates. Merged as ix #29 with admin override on the inherited nightly failures.

## Why This Works

Two distinct root causes, two distinct fixes:

**Cross-repo schema drift.** The SAE producer (ix crate) and consumer (GA `qa_score_quality_drift`) share an artifact contract whose canonical baseline lives in the consumer repo at `state/quality/optick-sae/2026-05-04/optick-sae-artifact.json`. The producer's hardcoded constant had no mechanical link to that baseline, so any edit on either side could drift undetected. Aligning `PHASE1_PARTITIONS` to the canonical 6-partition set (similarity-relevant only — `IDENTITY` excluded as identity tags, `ROOT` included as the load-bearing pitch-class signal added in OPTIC-K v1.8 to close the T-invariance gap exposed by invariant test #25) restores comparability. Adding `compact_training_dim = 124` alongside `optick_dim = 240` makes the dimensional slice explicit in the artifact, so future drift is detectable rather than silent.

**Platform portability.** PEP 394 says `python3` is the POSIX convention; on Windows the Python.org installer ships `python` and `py`, while `python3` is squatted by the Microsoft Store stub. `default_python_bin()` returns the right name per platform via `cfg!(target_os = "windows")`, and the CLI exposes `--python-bin` so venv users override it explicitly. This pattern generalises to any Rust-spawns-Python pipeline.

## Prevention

- **Producer/consumer schema contract test.** Add a unit test in `crates/ix-optick-sae` that loads the canonical artifact JSON (vendored as a test fixture, refreshed by CI from the GA repo) and asserts `PHASE1_PARTITIONS == artifact["partitions_used"]` and `compact_training_dim == sum(partition_widths)`. This is the test that would have caught both the original PR #82 miss and this recurrence on first run, before any smoke. Worth the hard assertion because similarity-feature partition lists are a one-way door — once an SAE is trained on a given slice, the cost of re-indexing the whole 313k-voicing corpus is high enough that drift between producer and consumer must fail loudly, not silently.
- **Cross-platform interpreter discovery.** Never hardcode `python3` in tools that run on Windows. Use a `default_python_bin()` helper gated on `cfg!(target_os)`, expose `--python-bin` for venv overrides, and document the Microsoft Store stub trap. Same rule applies to `node` vs `node.exe`, `pwsh` vs `powershell`.
- **End-to-end smoke for cross-language pipelines.** For pipelines where one language spawns another (Rust → Python here, .NET → F#, frontend → API), green unit tests on either side prove nothing about the boundary. Add an end-to-end smoke job that runs the full producer → subprocess → artifact → consumer-validation loop. The contract test above is the cheap gate; the smoke is the safety net that catches issues the test fixture didn't anticipate.

## Related

- **Sibling learning** — [`docs/learnings/2026-05-03-optick-sae-vanilla-topk-dead-features.md`](../../learnings/2026-05-03-optick-sae-vanilla-topk-dead-features.md) — explicitly states the canonical compact partition list is `[STRUCTURE 24, MORPHOLOGY 24, CONTEXT 12, SYMBOLIC 12, MODAL 40, ROOT 12]` = 124 dims, "IDENTITY etc. are NOT in the file." That document is the ground truth that PR #82 *and* PR #29 violated.
- **Contract** — [`docs/contracts/2026-05-02-optick-sae-artifact.contract.md`](../../contracts/2026-05-02-optick-sae-artifact.contract.md) §3.1 introduced `compact_training_dim` (PR #108) specifically to disambiguate `optick_dim` (240, total) from the slice the SAE actually trained on (124).
- **Plan** — [`docs/plans/2026-05-02-arch-optick-sae-plan.md`](../../plans/2026-05-02-arch-optick-sae-plan.md) — the Phase 1 brief should be updated with a one-liner: *use `python` on Windows / `python3` on POSIX (PEP 394); never hardcode `python3`*.
- **Direct ancestor PR** — ga #82 (merged 2026-05-03): first occurrence of the partition mistake; produced an artifact that omitted ROOT and labeled itself "118-dim" while declaring `optick_dim: 240`.
- **Artifact correction PR** — ga #106 (merged 2026-05-04): fixed the artifact data but not the producer code; ix #29 then re-introduced the same constant in Rust + Python.
- **Contract patch PR** — ga #108 (merged 2026-05-04): added `compact_training_dim` to the schema in response to #82.
- **Test coverage PR** — ga #107 (merged 2026-05-04): schema validator + qa-tools tests would catch a missing `compact_training_dim` going forward but did not catch the producer-side partition-list drift.
- **The fix itself** — ix #29 (merged 2026-05-05): three commits — initial Phase 1 scaffold by cloud agent, partition + Windows fix, three nightly-clippy fixes by parallel session.
- **Phase 2 consumer** — ga #112 (open at time of writing): Phase 2 drift consumer that will read the artifact this fix now correctly emits.
