# Progression-to-voicing evaluation corpus (v1, draft)

Held-out deterministic evaluation corpus for the progression-to-voicing vertical
slice. Added by [GuitarAlchemist/ga#627](https://github.com/GuitarAlchemist/ga/issues/627)
under story [#623](https://github.com/GuitarAlchemist/ga/issues/623).

| File | What it is |
|---|---|
| `progression-corpus.v1.schema.json` | Language-neutral contract. The document other repos and other languages read. |
| `progression-corpus.v1.json` | The twelve cases. |
| `../ProgressionCorpus.cs` | Deterministic loader. |
| `../JsonSchemaSubsetValidator.cs` | Closed-world validator for the keyword subset the schema uses. |
| `../CorpusPitchMath.cs` | Note/fret arithmetic, independent of production code. |
| `../ProgressionCorpusLoaderTests.cs` | The corpus parses and satisfies its schema. |
| `../ProgressionCorpusStructureTests.cs` | Coverage and self-consistency invariants. |
| `../ProgressionCorpusMatrixTests.cs` | Runs the corpus against today's deterministic seams; emits the evidence artifact. |
| `state/quality/progression-corpus/progression-corpus-matrix.json` | Machine-readable pass/fail matrix. Also the regression baseline. |

## The rule that makes this useful

**Expectations state what a musically correct system must answer. They are not
trimmed to what the product currently does.** Several cases fail today, on
purpose, because [#614](https://github.com/GuitarAlchemist/ga/issues/614),
[#567](https://github.com/GuitarAlchemist/ga/issues/567) and
[#554](https://github.com/GuitarAlchemist/ga/issues/554) are open. Weakening an
expectation to turn a red cell green destroys the only thing the corpus is for.

This is evaluation data, not production configuration. Nothing in the runtime
reads it, and it lives under the test project rather than
`Common/GA.Business.Config` so that stays true.

## The twelve cases

| id | category | progression | tuning | expected centre | pins |
|---|---|---|---|---|---|
| `pc-01-major-ii-v-i` | major ii-V-I | Dm7 G7 Cmaj7 | standard | C major | #614 |
| `pc-02-minor-ii-v-i` | minor ii-V-i | Bm7b5 E7 Am | standard | A minor | - |
| `pc-03-major-i-vi-iv-v` | I-vi-IV-V | C Am F G | standard | C major | #567 |
| `pc-04-deceptive-cadence` | deceptive cadence | C F G7 Am | standard | C major | - |
| `pc-05-borrowed-iv` | borrowed iv | C F Fm C | standard | C major | #567 |
| `pc-06-borrowed-bvii` | borrowed bVII | C Bb F C | standard | C major | - |
| `pc-07-secondary-dominant` | secondary dominant | C E7 Am F G7 C | standard | C major | #567 |
| `pc-08-modal-dorian-vamp` | modal | Dm7 Em7 Dm7 Em7 | standard | D dorian | - |
| `pc-09-starts-off-tonic` | starts away from tonic | Fmaj7 Em7 Dm7 G7 Cmaj7 | standard | C major | #614 |
| `pc-10-spelled-out-accidentals` | spelled-out accidentals | "F minor 7" "B-flat 7" "E-flat major 7" | standard | Eb major | #554 |
| `pc-11-drop-c-alternate-tuning` | alternate tuning | Cm Ab Eb Bb | **Drop C** | C minor | - |
| `pc-12-ambiguous-relative-pair` | ambiguous | Am F C G | standard | C major *or* A minor | - |

Two cases pin #614 and three pin #567, satisfying "at least two cases must
directly pin regressions from #614 and #567". `pc-10` pins #554 and carries
spelling groups for all three accidentals that issue names (E-flat, B-flat,
F-sharp) - the F-sharp group is present even though F#m is not in the
progression, because they share one parse boundary.

Two cases are deliberately unsatisfiable by any current seam:

- **`pc-08`** expects the modal centre `D dorian`. `KeyIdentificationService`
  knows 30 major/minor keys and cannot express a mode. `C major` - the parent
  key - is listed as *forbidden*, because answering the parent key is exactly
  the error this case exists to catch.
- **`pc-12`** expects two readings. A single confident answer fails the case
  even when that answer is `C major`.

## Per-case contract

Every case records, per `progression-corpus.v1.schema.json`:

- stable `id` and the corpus `schema_version`;
- `input.chords` (as a user would type them), `input.canonical_chords`,
  `input.instrument`, `input.tuning`, `input.key_hint`;
- `expected.tonal_center` plus `expected.acceptable_tonal_centers`;
- `expected.roman_numerals`, `expected.alternative_roman_numerals`,
  `expected.functions`;
- `expected.scale_families` - acceptable scale/arpeggio families per chord;
- `expected.required_chord_tones` - quality, root, pitch classes, spelled notes;
- `forbidden.tonal_centers` and `forbidden.facts`;
- `uncertainty.expected_behavior` (`confident` / `alternatives` / `warn` /
  `abstain`), `min_alternatives`, `max_confidence`;
- `expected.voicing_sequence` - one complete valid realisation in the case's own
  tuning;
- `expected.explanation_facts`;
- `pins` and `provenance`.

## Generation and verification

Pitch classes, note spellings and fret numbers are machine generated from the
chord symbols and then re-verified from the other direction by
`ProgressionCorpusStructureTests`: every fretting is re-sounded against the
case's declared open strings and must produce exactly the declared chord tones.
That is fret arithmetic only - the harness re-implements no key detection and no
harmonic analysis, and `CorpusPitchMath` deliberately calls no production code so
a defect there cannot validate wrong corpus data.

## Running it

```bash
# structure, loader and schema conformance + the current pass/fail matrix
dotnet test Tests/Common/GA.Business.ML.Tests/GA.Business.ML.Tests.csproj \
  --filter "FullyQualifiedName~ProgressionCorpus"

# human-readable status report
dotnet test Tests/Common/GA.Business.ML.Tests/GA.Business.ML.Tests.csproj \
  --filter "FullyQualifiedName~Matrix_ReportCurrentState" \
  --logger "console;verbosity=detailed"

# regenerate the evidence artifact after an intentional change, then commit it
GA_CORPUS_WRITE_MATRIX=1 dotnet test Tests/Common/GA.Business.ML.Tests/GA.Business.ML.Tests.csproj \
  --filter "FullyQualifiedName~ProgressionCorpusMatrixTests"
```

The matrix test gates on *movement*: a check that passes today and fails
tomorrow fails the build; a check that is already failing keeps failing without
breaking it. Checks with no seam at all are recorded as `blocked` with the issue
they wait on, so the artifact shows the real size of the remaining work rather
than a flattering subset.

## Status

`status: draft`. Following this repo's contract convention, v0.1.x-style drafts
are not frozen. Freeze at the Phase 4 milestone of #623, not before.
