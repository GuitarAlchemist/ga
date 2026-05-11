# state/quality — Quality Snapshot Archive

This directory is the canonical history of per-release quality measurements for
the GuitarAlchemist ecosystem. Each subdirectory is a snapshot **category**;
each file is one run dated `YYYY-MM-DD.json`.

## Layout

```
state/quality/
├── embeddings/              # ix-embedding-diagnostics output
│   └── YYYY-MM-DD.json
├── voicing-analysis/        # Demos/VoicingAnalysisAudit output
│   └── YYYY-MM-DD.json
├── chatbot-qa/              # ga-chatbot qa --benchmark summary
│   └── YYYY-MM-DD.json
└── routing-eval-YYYY-MM-DD.json   # SemanticIntentRouter F1 by intent
```

## Routing eval

Top-level `routing-eval-YYYY-MM-DD.json` files are produced by
`Tests/Common/GA.Business.ML.Tests/Eval/RoutingEvalHarness.RunBaseline_EmitReport`
(NUnit `[Explicit]` — requires a live Ollama at `localhost:11434` with
`nomic-embed-text`). Each file records overall accuracy plus per-intent
precision / recall / F1 against `Tests/.../Data/routing-eval-prompts.json`.

Run a new baseline:

```powershell
dotnet test Tests/Common/GA.Business.ML.Tests/GA.Business.ML.Tests.csproj `
    --filter "FullyQualifiedName~RoutingEvalHarness.RunBaseline_EmitReport"
```

The baseline file gets overwritten if the date matches; rename the prior
file (`routing-eval-2026-05-11.json` → `routing-eval-2026-05-11-pre-fix.json`)
before a re-run if you want to compare two same-day runs.

## How new snapshots get added

The `.github/workflows/quality-snapshot.yml` GitHub Action runs on every push
to `main` (and on `workflow_dispatch`). It:

1. Builds `ix-embedding-diagnostics`, `ix-quality-trend`, and the .NET audit.
2. Runs the audit tools, writing JSON into the appropriate subdirectory.
3. Regenerates `docs/quality/README.md` via `ix-quality-trend`.
4. Commits and pushes the new snapshot + refreshed report, tagged `[skip ci]`
   to avoid re-triggering itself.

Embedding diagnostics are **skipped** when `state/voicings/optick.index` is
absent — that file is a ~660 MB build artifact kept out of git, and the
diagnostic is per-release-only.

## Running the trend report locally

```bash
cargo run -p ix-quality-trend -- \
    --snapshots-dir /path/to/ga/state/quality \
    --out /path/to/ga/docs/quality/README.md
```

Optional flags:
- `--baseline YYYY-MM-DD` — informational marker for the baseline date
- `--regression-threshold-pct N` — default 5.0 (percent Δ vs 7-day average)

## Retention policy

Keep all snapshots. Each JSON is a few KB to a few tens of KB; the whole
archive should comfortably stay under 10 MB for years. Do **not** prune.

## Schema evolution

Snapshot producers may add new fields at any time. The `ix-quality-trend`
structs are all `Option<T>` with `#[serde(default)]` — **new fields must be
additive**. If a field is removed or renamed, keep the old field available
for at least one release so historical snapshots remain parseable.

## References

- Methodology: [`docs/methodology/invariants-catalog.md`](../../docs/methodology/invariants-catalog.md)
- Report generator: [`crates/ix-quality-trend`](https://github.com/GuitarAlchemist/ix/tree/main/crates/ix-quality-trend) (in `ix`)
- Latest rendered report: [`docs/quality/README.md`](../../docs/quality/README.md)
