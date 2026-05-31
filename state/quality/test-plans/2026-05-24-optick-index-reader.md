---
title: OptickIndexReader High-Value Test Plan
target: Common/GA.Business.ML/Search/OptickIndexReader.cs
status: draft
generated_at: 2026-05-24T19:30:00Z
generator: claude-opus-4-7
business_value_confidence: 0.95
effort_tshirt: L
---

# OptickIndexReader High-Value Test Plan

`OptickIndexReader` mmaps the **313k-voicing OPTIC-K index** and serves the
zero-copy vectors + msgpack metadata that every voicing search depends on.
A subtle parser or header-validation regression here corrupts every downstream
chatbot answer silently. Annotated `@ai:business-value conf=0.95`.

## Coverage gap summary

`Tests/Common/GA.Business.ML.Tests/Search/OptickIntegrationTests.cs` and
`OptickHardPromptBatteryTests.cs` exercise the **real production index** and
auto-skip on a clean checkout (good for local, useless for CI). Gaps:

- **No synthetic-fixture unit tests** — every header-validation branch
  (magic mismatch, version mismatch, schema-hash mismatch, dim mismatch,
  endian mismatch) is unreachable from CI today.
- **No msgpack parser tests** — `OptickMetadataParser` has 7+ wire-format
  branches (`fixmap`/`map16`/`map32`, `fixstr`/`str8`/`str16`/`str32`, etc.)
  none of which have a unit. The `CheckedLength` overflow guard was added
  as defensive code with no test pinning it.
- **No bounds-check assertions** — `GetVector(-1)`, `GetVector(Count)`,
  `GetMetadata(Count)` should all throw `ArgumentOutOfRangeException`.
- **No instrument-range test** — `GetInstrumentRange("guitar"|"bass"|"ukulele"|null|"foo")`
  must return the four documented behaviors.
- **No `Dispose` idempotency / double-dispose safety** test (production hits
  this on host shutdown).
- **No concurrent-read test** — the class docs claim "reads are safe from any
  thread"; that claim has no proof.

## Test cases (10 proposed)

| # | Name | Type | What it covers | Fixtures / mocks | Overlaps |
|---|---|---|---|---|---|
| 1 | `Ctor_BadMagic_Throws` | unit | First 4 bytes != `OPTK` → `InvalidDataException` with the "magic bytes missing" message. | hand-built minimal fixture written to a temp file. | none. |
| 2 | `Ctor_UnsupportedVersion_Throws` | unit | Magic OK + version=3 (or 5) → throws with version-mismatch message. | minimal fixture. | none. |
| 3 | `Ctor_SchemaHashMismatch_Throws` | unit | Header bytes correct except `schemaHash` → throws naming both expected + actual hashes. | minimal fixture. | none. |
| 4 | `Ctor_DimensionMismatch_Throws` | unit | Header `dim` != `EmbeddingSchema.CompactDimension` → throws naming both values. | minimal fixture. | none. |
| 5 | `Ctor_EndianMarkerWrong_Throws` | unit | endian == 0xFFFE → throws endian-mismatch. | minimal fixture. | none. |
| 6 | `GetVector_OutOfRange_Throws` | unit | `GetVector(-1)` and `GetVector(Count)` throw `ArgumentOutOfRangeException`. | use real or synthetic 4-voicing fixture. | none. |
| 7 | `GetInstrumentRange_KnownInstruments_ReturnExpectedRanges` | unit | `"guitar"` / `"bass"` / `"ukulele"` return the per-instrument ranges; `null` and `"foo"` return `(0, Count)`. | synthetic 12-voicing fixture (4 per instrument). | none. |
| 8 | `Dispose_IsIdempotent` | unit | Calling `Dispose()` twice does not throw and does not double-release the pointer (verified by no `AccessViolationException`). | synthetic fixture. | none. |
| 9 | `MsgPack_ParseRecord_CoversAllStringSizes` | unit | `ParseRecord` handles `fixstr` (≤31), `str8`, `str16`, and `str32` (small) lengths for `diagram` field. | hand-built msgpack fixtures (4 cases). | none. |
| 10 | `MsgPack_CheckedLength_RejectsOverflow` | unit | A crafted `array32` with raw length `> int.MaxValue` throws `InvalidDataException` (pin defensive code). | byte array with `0xDD` + 4 bytes of `0xFF`. | none. |

## Suggested file locations

- `Tests/Common/GA.Business.ML.Tests/Search/OptickIndexReaderUnitTests.cs`
  (cases #1–#8 with a `MinimalOptkBuilder` helper).
- `Tests/Common/GA.Business.ML.Tests/Search/OptickMetadataParserTests.cs`
  (cases #9, #10, internal-friend access via `InternalsVisibleTo`).

## Effort estimate

**L** (large). The unit tests need a **minimal OPTK file builder** (≈100 LOC,
implements header writer + per-instrument offsets + msgpack record writer).
That helper is reusable and worth it — same builder enables future round-trip
tests against `OptickIndexWriter`. Estimate 3–4 dev-days.

## Rubric

This file is the **highest-blast-radius** of the 8 — a silent corruption here
poisons every chatbot voicing answer without raising any test failure. Cases
#1–#5 (header validation) are cheap once the builder exists; cases #9–#10
(msgpack) are where production bugs actually live.
