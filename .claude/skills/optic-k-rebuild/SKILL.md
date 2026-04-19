---
name: optic-k-rebuild
description: "Rebuild the OPTIC-K voicing index (state/voicings/optick.index). Invoke when schema hash bumps, tag enrichment changes, new chord qualities are added, or the leak-test / invariant-coverage tools flag regressions. Mandatory precondition: stop any process that has the index mmap-locked — GaApi and GaMcpServer both hold the file open at runtime, and the write step WILL fail with IOException otherwise."
metadata:
  domain: music
  triggers: rebuild optick, rebuild voicings, OPTIC-K rebuild, regenerate index, corpus rebuild, optick.index, v4-pp, schema hash mismatch
  role: operations
  scope: corpus-generation
---

# OPTIC-K Index Rebuild Procedure

Operational runbook for rebuilding `ga/state/voicings/optick.index`. The index holds 313k guitar/bass/ukulele voicings as 112-dim OPTK v4-pp vectors. Rebuild is needed whenever:

- Schema hash changes (`EmbeddingSchema.BuildCompactLayoutV4` layout string edit → CRC32 bump → old index hard-rejected by reader).
- Writer normalization semantics change (e.g. v4 global-L2 → v4-pp per-partition).
- Tag-enrichment logic changes (`VoicingTagEnricher`, `VoicingAnalyzer`, `SymbolicTagRegistry`).
- Chord-quality enumeration expands (new `ChordPitchClasses` entries that the corpus generator also uses).
- A leak-test or invariant-coverage run flags that corpus-encoded vectors drifted from the generator's intent.

## Total wall-clock expectation

**~140 seconds for 313k voicings** on a modern dev machine. Breakdown:
- Guitar: ~136 s (667k raw → 298k unique via structural dedup → OPTIC-K embedding).
- Bass: ~2.5 s (12k raw → 7.8k unique).
- Ukulele: ~2.3 s (8.6k raw → 7.3k unique).
- Write step: seconds.

**Always run in background** (`run_in_background: true`) so the session isn't blocked. Poll the output file for progress.

## ⚠ Preflight — MANDATORY file-lock check

`optick.index` is memory-mapped by every running `OptickSearchStrategy` consumer. On Windows, any open mmap handle locks the file for writing. The rebuild's **embedding phase succeeds normally, then the 5-second write step crashes with IOException** if anything's still holding the file.

**Known holders:**
- `GaApi.exe` — the web service. Started manually or via launch profile.
- `GaMcpServer.exe` — spawned by Claude Code on demand via `dotnet run --no-build`.
- Any ad-hoc CLI or test runner that called `OptickSearchStrategy` and didn't dispose it.

**Stop them first:**

```powershell
Get-Process -Name GaApi, GaMcpServer -ErrorAction SilentlyContinue | Stop-Process -Force
# verify
Get-Process -Name GaApi, GaMcpServer -ErrorAction SilentlyContinue
```

Or in bash:

```bash
tasklist | grep -iE "GaApi|GaMcpServer"
# then taskkill each PID
taskkill -F -PID <pid>
```

**Do not rebuild with processes running.** The embedding phase will happily burn 2+ minutes of CPU before failing at the write, producing nothing.

## Backup the current index

One line, 168 MB copy, takes ~1 s:

```bash
cp "C:/Users/spare/source/repos/ga/state/voicings/optick.index" \
   "C:/Users/spare/source/repos/ga/state/voicings/optick.index.backup-$(date +%Y-%m-%d)"
```

If the rebuild produces a bad index (invariant regressions, search quality drops), restore from backup with `cp optick.index.backup-<date> optick.index`.

## Build the generator

The writer lives in `FretboardVoicingsCLI`. Make sure its compiled DLL reflects the current writer/encoder changes before running:

```bash
dotnet build "C:/Users/spare/source/repos/ga/Demos/Music Theory/FretboardVoicingsCLI/FretboardVoicingsCLI.csproj"
```

Expect `Build succeeded. 0 Error(s)`. Any MSB3027/MSB3021 errors mean a process is still holding the output DLL — recheck step 1.

## Run the rebuild

From the FretboardVoicingsCLI directory (output path is resolved relative to CWD if not absolute — always pass **absolute** path to avoid surprises):

```bash
cd "C:/Users/spare/source/repos/ga/Demos/Music Theory/FretboardVoicingsCLI"
dotnet run --no-build -- --export-embeddings \
  --output "C:/Users/spare/source/repos/ga/state/voicings/optick.index"
```

CLI flags:
- `--export-embeddings` — mode flag (required).
- `--output <abs-path>` — output path; default `state/voicings/optick.index` relative to CWD.
- `--tuning guitar|bass|ukulele` — optional; omit for all three.
- `--export-max <N>` — optional per-instrument cap (useful for smoke tests on a sample).
- `--no-dedup` — disables structural prime-form dedup (produces a bigger, noisier index).

**For smoke testing** a pipeline change without a full 313k rebuild:

```bash
dotnet run --no-build -- --export-embeddings \
  --output "C:/tmp/optick-smoke.index" \
  --tuning guitar --export-max 2000
```

## Verification sweep (run after every rebuild)

All four steps expected to pass. If any regresses versus the prior baseline, investigate before continuing.

### 1. Diagnostics snapshot

```bash
mkdir -p "C:/Users/spare/source/repos/ga/state/baseline/$(date +%Y-%m-%d)"
"C:/Users/spare/source/repos/ix/target/release/baseline-diagnostics.exe" \
  --index "C:/Users/spare/source/repos/ga/state/voicings/optick.index" \
  --out-dir "C:/Users/spare/source/repos/ga/state/baseline/$(date +%Y-%m-%d)"
```

Key numbers to watch (post v4-pp, pre-enrichment baseline was):
- Full-dim acc: **0.752** — classification accuracy of a forest over all 112 dims.
- STRUCTURE leak acc: **0.596** (target: drop toward 0.33 random chance).
- MORPHOLOGY acc: **0.790** — by design, instrument-correlated.
- CONTEXT/SYMBOLIC/MODAL leak acc: 0.522 / 0.614 / 0.498 (target: drop).
- Retrieval PC-set match: **92.8%** (target: hold or improve).

### 2. Invariant coverage

```bash
"C:/Users/spare/source/repos/ix/target/release/ix-invariant-produce.exe" --pretty \
  --out "C:/Users/spare/source/repos/ga/state/baseline/$(date +%Y-%m-%d)-firings.json"
"C:/Users/spare/source/repos/ix/target/release/ix-invariant-coverage.exe" \
  --catalog "C:/Users/spare/source/repos/ga/docs/methodology/invariants-catalog.md" \
  --firings "C:/Users/spare/source/repos/ga/state/baseline/$(date +%Y-%m-%d)-firings.json"
```

After v4-pp rebuild: #25, #28, #32 should flip from FAIL to PASS (cross-instrument STRUCTURE equality, 3-class leak test, cross-octave cosine=1.0).

### 3. C# integration tests

```bash
dotnet test "C:/Users/spare/source/repos/ga/Tests/Common/GA.Business.ML.Tests/GA.Business.ML.Tests.csproj" \
  --filter "FullyQualifiedName~Optick|FullyQualifiedName~MusicalQuery|FullyQualifiedName~VoicingTag"
```

After rebuild the previously-skipped retrieval tests in `OptickIntegrationTests` and `OptickHardPromptBatteryTests` should **pass** (not skip). Expect **all 72 tests green**.

### 4. Live MCP probe

If GaMcpServer is restarted (via `/mcp` in Claude Code), fire:

```
mcp__ga__ga_voicing_index_info   # liveness + new count + new schema hash
mcp__ga__ga_search_voicings { query: "Cmaj7", limit: 5 }
mcp__ga__ga_search_voicings { query: "Cmaj7 jazz", limit: 5 }
```

**The P5 regression fingerprint flips post-enrichment:** `"Cmaj7 jazz"` should score higher than bare `"Cmaj7"` (was the other way around pre-enrichment).

## Rollback

If invariants regress or search quality drops:

```bash
cp "C:/Users/spare/source/repos/ga/state/voicings/optick.index.backup-<date>" \
   "C:/Users/spare/source/repos/ga/state/voicings/optick.index"
```

Then **revert the schema-hash bump** in `EmbeddingSchema.BuildCompactLayoutV4` (and `ix/crates/ix-optick/src/lib.rs` `SCHEMA_SEED`) so the old index validates again.

## Known-good baselines

| Date | Commit / change | Voicings | Full-dim | STRUCTURE | MORPHOLOGY | CONTEXT | SYMBOLIC | MODAL | PC-set | β₀ rng | β₁ rng |
|---|---|---|---|---|---|---|---|---|---|---|---|
| 2026-04-18 pre | v4 global-L2, sparse SYMBOLIC | 313,047 | 0.752 | 0.596 | 0.790 | 0.522 | 0.614 | 0.498 | 92.8% | 178 | 105 |
| 2026-04-19 post | **v4-pp per-partition**, tag enricher, +20 qualities | 313,047 | 0.867 | **0.517** | 0.903 | **0.333** ✅ | **0.526** | 0.493 | **100.0%** ✅ | **79** | **5** |
| 2026-04-19 v1.8 | **+ ROOT partition (12 dims, w=0.05), root-boost removed** | 313,047 | 0.835 | **0.503** | 0.903 | **0.333** ✅ | 0.524 | 0.493 | **100.0%** ✅ | **36** | 20 |

**v1.8 headline: invariant #25 flipped from 8.4% PASS (67/793) to 100% (793/793).** Under v4-pp + root-boost, same-PC-set voicings across instruments had bit-different STRUCTURE slices because STRUCTURE's `v[rootPitchClass] += 1.0` broke T-invariance. Removing the root-boost and moving root identity to a dedicated 12-dim ROOT similarity partition (weight 0.05) restored the schema's O+P+T+I claim.

Additional diagnostic signals in v1.8:
- **ROOT partition at acc 0.345** — correctly near-random (1/3 + 1σ); diagnostic flipped its label from `[LEAK]` to `[ok]`. Root is PC-level data, genuinely cross-instrument-invariant by construction.
- **β₀ range halved again** (79 → 36). With STRUCTURE cleaned of root-boost, the topological geometry is even more instrument-agnostic.
- **Full-dim acc DROPPED** (0.867 → 0.835). Counter-intuitive but correct: cleaner STRUCTURE = less instrument signal smuggled via root-boost = less predictability of instrument from the full vector.
- **Retrieval held at 100% PC-match.** Adding the ROOT partition did not degrade retrieval quality.

Residual STRUCTURE leak (0.503) comes from genuine data-level cardinality differences: a 4-string ukulele cannot represent 6-note chords, so the cardinality dim legitimately differs across instruments for large PC-sets. That's an encoding-consistent truth, not a normalization bug.

Interpretation of the post-rebuild deltas:

- **Retrieval 92.8% → 100.0%.** Every top-K in the 50-query test now matches the query's PC set exactly. The combined effect of per-partition normalization + tag enrichment + extended quality coverage.
- **CONTEXT leak completely eliminated** (0.522 → 0.333, exactly random-chance). The partition no longer encodes instrument signal.
- **STRUCTURE leak cut by ~13%** (0.596 → 0.517). Residual signal comes from genuine cardinality differences across instruments (a 4-string ukulele can't represent the same note-count as a 6-string guitar for large chords) — not from normalization coupling, which is now clean.
- **SYMBOLIC leak cut by ~14%** (0.614 → 0.526) despite the enricher adding more bits. Genuinely instrument-specific tags (barre, stretch) keep some residual.
- **MORPHOLOGY up** (0.790 → 0.903) — by design; fretboard geometry IS instrument-distinctive.
- **β₀ range halved, β₁ range collapsed 20×** (178→79, 105→5). Topological structure is now far more instrument-agnostic — the per-partition norm de-couples global structure from instrument-specific details.

Invariant implications:
- **#25** (cross-instrument STRUCTURE equality for same PC-set): leak test reduced substantially; a targeted same-PC-set comparison would now pass. Broader classifier still shows residual due to cardinality effects, which is a data-coverage issue, not an encoding bug.
- **#28** (no partition accuracy > 1/3 + 3σ): CONTEXT now satisfies this cleanly. STRUCTURE/SYMBOLIC still slightly over threshold.
- **#32** (cross-octave STRUCTURE cosine = 1.0): 100% retrieval is strong evidence this now holds in practice.
- **#33** (ChordName consistency across instruments): corpus-metadata issue, unchanged by normalization fix — addressed in a separate pass.

(Add a row after each confirmed-good rebuild.)

## Related

- Design doc: `ix/docs/plans/2026-04-18-optic-k-v4-pp-per-partition-norm.md`.
- Auto-memory: `project_optic_k_v4pp.md`, `reference_optic_k_rebuild_procedure.md`.
- Writer source: `ga/Demos/Music Theory/FretboardVoicingsCLI/OptickIndexWriter.cs`.
- Encoder source: `ga/Common/GA.Business.ML/Search/MusicalQueryEncoder.cs`.
- Reader source: `ga/Common/GA.Business.ML/Search/OptickIndexReader.cs`, `ix/crates/ix-optick/src/lib.rs`.
