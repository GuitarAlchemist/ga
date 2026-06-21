---
status: accepted
date: 2026-06-21
---

# `MusicalVoicingAnalysis` stays wide — it is the analyzer→corpus-document carrier, not a bag of dead fields

## Context

`MusicalVoicingAnalysis` (`Common/GA.Business.Core/Analysis/Voicings/MusicalVoicingAnalysis.cs`)
is a 15-field record. An architecture review flagged it as a **shallow** value type:
its interface is nearly as wide as its implementation, and the sole production
producer — `VoicingAnalyzer.AnalyzeEnhanced` (`Common/GA.Domain.Services/Fretboard/Voicings/Analysis/VoicingAnalyzer.cs:154–197`) —
hardcodes several fields to placeholders:

- `EquivalenceInfo = new("Unknown", "Unknown", "Unknown", 0)`
- `ToneInventory   = new([], false, [], [])`
- `AlternateChordNames = []`
- `SymmetricalInfo = new("Unknown")`

The obvious deepening — "drop the never-populated fields and the duplicate
`ChordId`" — looks like a clean deletion test. It is not.

## Decision

**Keep `MusicalVoicingAnalysis` at its current width.** The placeholder fields
are **unfilled pipeline slots**, not dead weight, and the record is the shared
carrier between the analyzer (layer 3) and the OPTIC-K corpus/index document
factory (layer 4). Deleting fields is a corpus-document schema change, not a
refactor.

## Why (the trade-off)

- **The "empty" fields are read downstream into the index document.**
  `VoicingDocumentFactory` (`Common/GA.Business.ML/Rag/VoicingDocumentFactory.cs:41–71`)
  and `IndexVoicingsCommand` (`GaCLI/Commands/IndexVoicingsCommand.cs:475–497`)
  read `analysis.ToneInventory.{HasGuideTones,OmittedTones,DoubledTones}`,
  `analysis.EquivalenceInfo?.{PrimeFormId,ForteCode,TranslationOffset}`, and
  `analysis.AlternateChordNames` to populate `ChordVoicingRagDocument` /
  `VoicingEntity` fields. They currently receive `"Unknown"`/empties — but the
  **plumbing to the OPTIC-K document exists**. Removing the carrier fields means
  removing those reads and the corresponding document fields — a coordinated
  re-index, which is the OPTIC-K **one-way door** (CLAUDE.md).
- **`ChordId` is not a clean duplicate.** The top-level `analysis.ChordId` is
  read everywhere (factory, index, tests, demos). The copy inside
  `VoicingCharacteristics.ChordId` is read via a *different* handle —
  `VoicingTagEnricher` and `VoicingAnalyzer` consume `curVoiceChars.ChordId`
  directly, not through `analysis`. Both access paths are live; collapsing them
  is not free.
- **The real work is a feature, not a deletion.** The honest fix for the
  placeholders is to *populate* them (equivalence info, tone inventory, alternate
  names are genuine analysis the producer hasn't implemented yet), which **adds**
  implementation behind the existing interface — deepening it — rather than
  shrinking the interface toward a thinner implementation.

## Consequences

- Future architecture reviews will re-flag "these fields are always `Unknown`,
  delete them." This ADR is the answer: they are stubbed slots wired to the
  corpus document; deletion is a one-way-door schema change, and the value is in
  filling them, not removing them.
- If a producer is written that genuinely fills `EquivalenceInfo` /
  `ToneInventory` / `AlternateChordNames`, that is a layer-3 feature and does not
  reopen this ADR.
- A separate, observed friction stands on its own: `MusicalEmbeddingGenerator`
  and `MusicalQueryEncoder` inject vector services they never use (they call the
  static `*.ComputeEmbedding` overloads), producing `CS9113` warnings. That is a
  constructor-interface cleanup unrelated to this record's width and is tracked
  outside this ADR.

## Related

- Carrier: `Common/GA.Business.Core/Analysis/Voicings/MusicalVoicingAnalysis.cs`
- Producer: `Common/GA.Domain.Services/Fretboard/Voicings/Analysis/VoicingAnalyzer.cs`
- Consumers (the wiring): `Common/GA.Business.ML/Rag/VoicingDocumentFactory.cs`,
  `GaCLI/Commands/IndexVoicingsCommand.cs`
- OPTIC-K one-way door: `CLAUDE.md` ("never change dimension without coordinated re-index").
