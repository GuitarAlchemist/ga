# ICV Format Reconciliation — OPTIC-K Writer/Reader Symmetry

**Status:** Pending
**Date:** 2026-05-12
**Owner:** spareilleux
**Reversibility:** **One-way door** for the corpus rebuild step; two-way for the parser/writer code itself.
**Revisit trigger:** Re-baseline `state/telemetry/voicing-search/` after corpus rebuild; if 4-PC-chord queries (e.g. `Dm7`) still return their 3-PC subsets at top-K, escalate.

## Problem statement

The 2026-05-12 telemetry sweep (PR #186 review) surfaced that the OPTIC-K v1.8 corpus has been writing its STRUCTURE partition's interval-class-vector (ICV) slice with a **misparsed string** for the entire life of the index. The defect is silent: cosine similarity still ranks something, just not what the schema docstring claims.

| Side | What it writes / reads | Result for major scale `<2 5 4 3 6 1>` |
|---|---|---|
| Writer (`MusicalEmbeddingGenerator.cs:98` ← `VoicingDocumentFactory.cs:75` ← `VoicingHarmonicAnalyzer.cs:37`) | `pcSet.IntervalClassVector.ToString()` → `"<2 5 4 3 6 1>"` (angle-bracketed, space-separated, see `IntervalClassVectorId.cs:24`) | string passed verbatim to `TheoryVectorService` |
| `ParseIcv` (`TheoryVectorService.cs:92-101`) | `for i in 0..5: if char.IsDigit(s[i]) counts[i] = s[i] - '0'` | `s = "<2 5 4"` → `counts = [0, 2, 0, 5, 0, 4]` — values misaligned by position, last three IC counts discarded entirely |

The encoder's docstring (`MusicalQueryEncoder.cs:30-31`) claims "Query and corpus therefore live in the identical semantic space — no alignment training required." That claim has been false since the v1.0 of the index.

Consequences:
- STRUCTURE dims 13-18 (ICV bins) carry positionally-shifted garbage on the corpus side.
- Dims 20 (`IcvConsonance`) and 21 (`IcvBrightness`) are computed from the misparsed counts, so they're also wrong.
- Pre-PR-#186 the query side wrote zeros to these dims, so the cosine on that 8-dim slice was effectively zero — neutral.
- The initial fix in PR #186 tried to populate the query side with the correct format. That widened the gap rather than closing it, and was reverted in commit `27a0b8ec`.

## What's blocked

- **`Dm7` returns Dm/F triads at identical 0.5066** (the original telemetry finding). The query side has no ICV signal, so the partition is decided entirely by chroma + cardinality + tonal — and chroma overlap of `{0,2,5,9}` vs `{2,5,9}` is 3-of-4, ranked identically.
- **All 4+ PC chord queries collapse to nearest 3-PC subset** when the corpus has subset voicings indexed alongside the full chord.
- **Higher-cardinality chord retrieval is generally degraded** (Cmaj9#11 → Esus2, F#m11 → Gbm(add9), G13b9 → E7(shell)).

## Decision

Two independent reconciliation paths. Each is internally consistent; mixing them is the bug.

### Path A — Fix the reader (lower risk, requires corpus rebuild)

Make `ParseIcv` tolerant of both formats:
- The intended digit-per-position form `"NNNNNN"` (what the parser was designed for and what `EmbeddingSchemaValidationTests` fixtures use).
- The actual writer form `"<a b c d e f>"` — split on whitespace, drop `<`/`>`, parse decimal integers, cap at 9.

```csharp
private static int[] ParseIcv(string? icv)
{
    var counts = new int[6];
    if (string.IsNullOrEmpty(icv)) return counts;
    if (icv.IndexOf('<') >= 0 || icv.IndexOf(' ') >= 0)
    {
        // Bracket-space form: "<2 5 4 3 6 1>"
        var parts = icv.Trim('<', '>').Split(' ', StringSplitOptions.RemoveEmptyEntries);
        for (var i = 0; i < Math.Min(6, parts.Length); i++)
            if (int.TryParse(parts[i], out var v)) counts[i] = Math.Min(v, 9);
        return counts;
    }
    // Digit-per-position form: "012120"
    for (var i = 0; i < Math.Min(6, icv.Length); i++)
        if (char.IsDigit(icv[i])) counts[i] = icv[i] - '0';
    return counts;
}
```

Then re-add the query-side `ComputeIcvString` (the helper that was in PR #186 commit `d2e7590e`), which emits `"NNNNNN"` form so the query and corpus pass through the same reader logic and produce aligned counts.

**Required follow-on:** rebuild the corpus. Existing on-disk vectors are frozen with the misparsed values; only re-running `OptickIndexWriter` against the corrected reader fixes them. Per `state/voicings/README.md` and the `optic-k-rebuild` skill, this is the standard ~140s procedure (stop GaApi+GaMcpServer, run `FretboardVoicingsCLI`, restart).

### Path B — Fix the writer (higher risk, also requires rebuild)

Change `IntervalClassVectorId.ToString()` to emit `"NNNNNN"`. This is the wider blast radius — `ToString()` is used by `DisplayName` properties, debug output, logs, JSON-serialized exports across the whole domain. Every consumer that has a regex or string match on the `<2 5 4 3 6 1>` form would break.

Not recommended unless we find concrete benefit beyond fixing the embedding pipeline.

## Recommended path

**Path A**, in three sub-steps:

1. **Code change** (this PR's follow-up):
   - Patch `ParseIcv` to accept both formats.
   - Re-add `MusicalQueryEncoder.ComputeIcvString` and the `intervalClassVector:` pass-through call (port from reverted commit `27a0b8ec`).
   - Add a **corpus-vs-query integration test** that:
     - Encodes a known voicing (e.g. C major triad on guitar) via `MusicalEmbeddingGenerator` (corpus path).
     - Encodes the same query via `MusicalQueryEncoder` (query path).
     - Asserts `compactQuery[13..18]` (ICV slice) matches `compactCorpus[13..18]` to within float tolerance after per-partition normalization.
   - This test would have failed pre-PR — it's the symmetry guarantee the encoder docstring claims.

2. **Corpus rebuild**:
   - Stop GaApi + GaMcpServer (mmap lock).
   - Run `OptickIndexWriter` against the corrected `ParseIcv`. ~140s for 313k voicings per `optic-k-rebuild` skill.
   - Restart services.

3. **Verification**:
   - Re-run the 25-query telemetry sweep from PR #186's review.
   - Confirm `Dm7` returns 4-PC `Dm7` voicings before any 3-PC `Dm/F` subset.
   - Confirm `Cmaj9#11`, `F#m11`, `G13b9` retrievals show their actual PC sets at top-K, not subset-of-PCs fallbacks.
   - Compare `state/telemetry/voicing-search/2026-05-12.jsonl` distribution before/after — expect score distribution to broaden (more discrimination, less ties).

## Out of scope (for now)

- `IntervalClassVector` consumers outside the embedding pipeline. The `<2 5 4 3 6 1>` form remains the canonical display string.
- Adding a `ParseIcv`-equivalent helper to `EmbeddingSchema` as the canonical format spec. Worth doing once the asymmetry is closed; tracked in this plan's follow-up rather than this PR.
- The other findings from PR #186 review (paren-wrapped alterations, drop2 alias) — those landed in PR #186 and are independent.

## One-way door log

- **Corpus rebuild is a one-way door.** Once the new `OptickIndexWriter` runs, the old misparsed-ICV vectors are overwritten. Downstream consumers (chord-recommendation, GA-Nightly-Quality baselines under `ga/state/quality/`, any pinned expected-results in `Tests/.../OptickIntegrationTests.cs`) need a re-baseline pass.
- **Schema hash unchanged.** The partition layout / weight matrix is untouched; `SchemaHashV4` stays. Existing tests that snapshot the schema hash will continue to pass.

## Cross-repo coordination

ix's `crates/ix-optick` and `crates/ix-optick-invariants` read the OPTIC-K index. The Rust readers only do dot-products on stored vectors; they don't re-parse ICV strings. So no ix code change is required for Path A. The invariant-coverage tests in `ix-optick-invariants` may shift scores after the rebuild — re-baseline if any invariant regresses.

`tars` and `Demerzel` don't touch the OPTIC-K corpus directly.

## Acceptance criteria

- [ ] `ParseIcv` accepts both `"NNNNNN"` and `"<a b c d e f>"` forms — test in `TheoryVectorServiceTests`.
- [ ] Corpus-vs-query integration test exists and passes.
- [ ] Corpus rebuilt against the new writer/reader.
- [ ] 25-query telemetry sweep confirms `Dm7 > Dm/F` and 4 other 4+ PC chord queries return their actual PC sets at top-K.
- [ ] Re-baseline check: `OptickIntegrationTests.cs:118` (`results[0].Score > 0.4` for Cmaj7) still holds — score should rise, not fall.
- [ ] No invariant regression in `ix-optick-invariants` (current passes #25, #32 at 100%).
