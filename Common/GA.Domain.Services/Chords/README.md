Chords namespace layout

This directory groups chord-related types by responsibility to improve discoverability, reduce coupling, and enable clean layering.

Sub-namespaces

- GA.Business.Core.Chords.Core
  - Fundamental chord primitives and value objects used across the module (e.g., Chord, ChordFormula, ChordExtension, ChordStackingType).

- GA.Business.Core.Chords.Templates
  - Chord template definitions, registries, and utilities describing chord tone collections independent of context.

- GA.Business.Core.Chords.Construction
  - Builders, factories, and helpers used to construct chord templates from scales/modes.
  - Example: ChordStackingPatternGenerator (tertian, quartal, quintal stacking utilities).

- GA.Business.Core.Chords.Naming
  - Naming helpers/services focused on generating human-readable chord names/symbols.
  - Example: ChordTemplateNamingService; legacy adapter stub: ChordFormulaNamingService.

- GA.Business.Core.Chords.Parsing
  - Parsing of chord symbols into structured representations (if/when implemented here).

- GA.Business.Core.Chords.Analysis
  - Tonal analysis helpers that derive properties from chord templates (may depend on Templates).

- GA.Business.Core.Chords.Analysis.Atonal
  - Atonal/set-theory analysis that provides prime form, Forte numbers, interval class vectors, etc.
  - Example: AtonalChordAnalysisService and its adapter for IChordAnalysisService.

Layering rules (allowed references)

- Core has no dependencies on siblings.
- Templates depends only on Core.
- Construction depends on Core and Templates.
- Naming depends on Core (and Templates if needed).
- Parsing depends on Core and Naming (optionally Templates).
- Analysis depends on Core and Templates (optionally Naming for output).
- Analysis.Atonal depends on Core and Templates; no UI/Fretboard dependencies.

Notes

- Legacy files under Intervals/Chords remain as no-op placeholders for VCS history; the implementations live here.
- The old Chords/AtonalChordAnalysisService remains as a backward-compatible forwarding shim. Prefer using GA.Business.Core.Chords.Analysis.Atonal.AtonalChordAnalysisService directly in new code.
