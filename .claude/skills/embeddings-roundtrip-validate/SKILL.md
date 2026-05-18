---
name: "Embeddings Roundtrip Validate"
description: "Per-commit roundtrip validator for the embeddings /auto-optimize loop. Re-runs OPTIC-K leak-detection diagnostics after a proposed change to the encoder / embedding pipeline and rejects if the leak metric regressed, the snapshot schema broke, or a protected path was modified. Mirrors chatbot-qa-roundtrip-validate but with reversed metric polarity (lower is better) and a heavier oracle (~10–30 min). Unblocks the third gap in state/quality/embeddings/baseline.json _open_gaps.roundtrip_validator."
allowed-tools: Read, Bash, Grep, Glob
last_verified: 2026-05-16
---

# /embeddings-roundtrip-validate

The contract the embeddings `/auto-optimize` loop calls **before every commit**.
Returns `pass` / `reject` per the same Harness-Engine rollback wiring the
chatbot-qa domain uses. Drafted to close the third gap in
`state/quality/embeddings/baseline.json` `_open_gaps.roundtrip_validator`.

This skill is intentionally read-only — it runs the oracle, checks the
diff, and returns a verdict. It never edits code, never touches the
oracle binary, never modifies the baseline. That's `/auto-optimize`'s job
to react to (revert if reject, commit if pass).

## Critical difference from chatbot-qa: metric polarity

Embeddings metric is **lower-is-better** (per baseline.json
`metric_polarity`). The metric is
`leak_detection.full_classifier_accuracy` — a random-forest classifier's
ability to predict instrument from the embedding vector. The OPTIC-K
geometry is supposed to be instrument-agnostic, so a perfect embedding
scores near `baseline_random` (~0.333). Higher accuracy = the embedding
leaks instrument identity, which IS the regression direction.

Concretely:

```
delta = after_metric - before_metric
regression if delta > +regression_threshold   (positive delta = leak increased = WORSE)
improvement if delta < 0                       (negative delta = leak decreased = BETTER)
```

This is the inverse of the chatbot-qa direction. Get the sign right or
the loop will commit regressions and revert improvements.

## Inputs

| Input            | Source                                                            |
|------------------|-------------------------------------------------------------------|
| `before_metric`  | `full_classifier_accuracy` from prior snapshot (loop history tail) |
| `after_metric`   | `full_classifier_accuracy` from the snapshot just produced        |
| `diff_paths`     | `git diff --name-only HEAD` — what the proposed edit changed       |
| `baseline_path`  | `state/quality/embeddings/baseline.json` (for gates)              |

In practice the loop passes the first three as values; the skill loads
the baseline itself.

## Verdict rubric

The skill returns `pass` if **all four** conditions hold; `reject`
otherwise. Reject diagnostics include the failing condition by name so
the loop can record a learning rather than just retry.

### 1. Metric did not regress (LOWER-IS-BETTER)

```
delta = after_metric - before_metric
reject if delta > +regression_threshold
```

`regression_threshold` defaults to `0.01` per baseline.json
`_harness.rollback_metadata.regression_threshold`. A leak increase of
0.01 (e.g. 0.7522 → 0.7622) is the rejection boundary. Larger increase
→ reject(`metric_regression`).

A negative delta is always an improvement (lower leak = closer to the
instrument-agnostic ideal of 0.333).

### 2. Snapshot schema did not break

```bash
# Parse the new snapshot; required fields per ix-quality-trend snapshot.rs.
SNAP="state/quality/embeddings/$(date -u +%Y-%m-%d).json"
jq -e '.leak_detection.full_classifier_accuracy and .retrieval_consistency and .cluster_baseline and .topology' "$SNAP"
```

`jq -e` returns non-zero if any required field is missing. The snapshot
needs all four top-level diagnostic blocks per the producer contract
(`ix/crates/ix-embedding-diagnostics/src/main.rs`). Missing fields →
reject(`snapshot_schema_break`).

### 3. No protected path modified

```powershell
$baseline = Get-Content state/quality/embeddings/baseline.json -Raw | ConvertFrom-Json
$protected = $baseline.scope_boundary.protected
$changed = git diff --name-only HEAD
foreach ($p in $changed) {
    foreach ($glob in $protected) {
        if ($p -like $glob) {
            return @{ verdict = 'reject'; reason = 'protected_path_modified'; path = $p; glob = $glob }
        }
    }
}
```

Protected globs include `EmbeddingSchema.cs` (the 240-dim contract is a
one-way door per CLAUDE.md), `state/voicings/optick.index` (the corpus
itself), `state/quality/embeddings/baseline.json` (the contract this
skill reads), `Tests/**/*.cs`, `Scripts/run-prompt-corpus.ps1`, and
`.github/workflows/*`.

Override is operator-only: a commit subject containing
`[allow-protected: <path>]` exempts that one path. The loop never adds
this marker.

### 4. Build + fast tests still pass

```powershell
dotnet build AllProjects.slnx -c Release --nologo
# exit 0 required
dotnet test Tests/Common/GA.Business.ML.Tests --no-build -c Release `
  --filter "FullyQualifiedName~MusicalQueryEncoder|FullyQualifiedName~Embedding|FullyQualifiedName~Optick" `
  --nologo
# exit 0 required
```

The expensive part — re-running `baseline-diagnostics` — is NOT done
here. That's what produced `after_metric` at step 0 (cost: 10–30 min on
the loop's machine). The fast-test subset confirms the build is intact
and that the encoder / embedding code still compiles and passes its
unit tests.

A build break or fast-test failure is reject `build_or_test_break`.

## Output shape

```json
{
  "verdict": "pass" | "reject",
  "reason": "metric_regression" | "snapshot_schema_break" | "protected_path_modified" | "build_or_test_break" | null,
  "before_metric": 0.7522,
  "after_metric": 0.7480,
  "delta": -0.0042,
  "metric_polarity": "lower-is-better",
  "diagnostic": "<one-line human description>",
  "detail": {
    "path": "<if protected>",
    "missing_fields": ["<if schema broke>"],
    "exit_code": <int>
  }
}
```

The loop records this verbatim into `state/quality/embeddings/loop-history.jsonl`.

## Process

1. Read `state/quality/embeddings/baseline.json` for thresholds + protected globs.
2. Compute `delta = after_metric - before_metric`.
3. If `delta > +regression_threshold` → return reject(`metric_regression`). **(Note: positive delta is bad for this domain.)**
4. Parse today's snapshot at `state/quality/embeddings/$(date -u +%Y-%m-%d).json` with `jq -e` for required fields. If non-zero, return reject(`snapshot_schema_break`).
5. Walk `git diff --name-only HEAD`, check each path against `scope_boundary.protected`. If any match (and no `[allow-protected: ...]` override), return reject(`protected_path_modified`).
6. Run `dotnet build AllProjects.slnx -c Release` (exit 0 required) then
   `dotnet test ... --filter "FullyQualifiedName~MusicalQueryEncoder|FullyQualifiedName~Embedding|FullyQualifiedName~Optick"` (exit 0 required).
   If either non-zero, return reject(`build_or_test_break`).
7. Return pass.

## Oracle cost note for the loop

The chatbot-qa oracle is ~30s per cycle (Scripts/run-prompt-corpus.ps1).
The embeddings oracle (`baseline-diagnostics`) is **10–30 min** per cycle:

- Rust workspace cold build: ~5 min
- Diagnostics 4-phase run: ~5–25 min depending on `--cluster-sample` cap

The `/auto-optimize` loop driver should set per-cycle wall-clock
budgets accordingly. Cold-cache cycles will exceed any 5-minute budget;
plan in 30-min units for this domain.

## When the loop should call this

- After every implementation edit, BEFORE `git commit`.
- The loop should `git stash` if reject, OR `git revert HEAD --no-edit` if a commit slipped through somehow.

## What this skill does NOT cover

- **Roundtrip-equivalence semantics** — the chatbot-qa version has a
  trace-shape gate (`compare-trace-to-canonical.ps1`). The embeddings
  equivalent would be a sanity check on the OPTIC-K geometry — e.g.,
  "embedding a known voicing twice yields cosine distance < ε" or
  "C and Cm cluster apart by a known margin." Not implemented in v1;
  the schema check is the v1 fallback.
- **Cross-domain interference** — a change to `MusicalQueryEncoder`
  could regress the chatbot-qa loop too. The loop driver should run
  the chatbot-qa oracle as a secondary gate when this skill's diff
  touches `Common/GA.Business.ML/Search/MusicalQueryEncoder.cs`.
- **Multi-LLM review** — PR-time, not per-commit.
- **Latency regression** — none of the diagnostics measure inference
  latency. Future enhancement.

## Open improvements (not blockers for v1)

- **Roundtrip-equivalence gate.** A small set of "golden voicings"
  embedded both before and after the change; cosine distance between
  matching pairs must be < ε. Closes the gap with the chatbot-qa
  trace-shape gate. Out of scope for v1 — file under future work.
- **Per-partition gates.** STRUCTURE / MORPHOLOGY / CONTEXT /
  SYMBOLIC / MODAL / ROOT partitions each have an expected leak
  hypothesis (per `ix/crates/ix-embedding-diagnostics/src/main.rs`).
  A v2 could reject if MORPHOLOGY (by_design) regresses even if the
  full-classifier metric improves — that would be a leak-shape change
  even at constant magnitude.
- **Trend-aware threshold.** Currently `regression_threshold` is a
  fixed 0.01. Once 4+ weeks of cadence data exists, derive it from
  rolling std-dev of `full_classifier_accuracy` to catch slow drift.

## Related

- `.claude/skills/auto-optimize/SKILL.md` — the loop driver that calls this skill.
- `.claude/skills/chatbot-qa-roundtrip-validate/SKILL.md` — the sibling validator (this skill mirrors its shape with reversed metric polarity).
- `state/quality/embeddings/baseline.json` — the contract this skill reads.
- `.github/workflows/embeddings-snapshot.yml` — the daily producer.
- `docs/plans/2026-05-16-arch-embeddings-snapshot-pipeline-plan.md` — the cadence + filename plan.
- `ix/crates/ix-embedding-diagnostics/src/main.rs` — the oracle binary `baseline-diagnostics`.
- task #182 — the auto-optimize loop this validator unblocks.
