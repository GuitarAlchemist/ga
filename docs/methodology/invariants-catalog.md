# Invariants Catalog — GA + IX Ecosystem

**Status:** Active
**Date:** 2026-04-17
**Retained for:** executive reports, CI automation roadmap, quality gates

## Purpose

This document catalogues every claimable structural, algebraic, and semantic invariant across the GuitarAlchemist ecosystem (GA C#/F# + IX Rust). For each, it records: what the invariant asserts, where it should hold, current test status, and the IX primitive (if any) that can automate its verification.

Produced by ultrathink investigation dispatched 2026-04-17 — see task #85 for full context.

## Legend

- **T** — Has tests (automated, executed in CI or unit test suite)
- **C** — Claimed (asserted by comments, docstrings, or by construction) but not tested
- **N** — Not claimed anywhere; latent invariant worth surfacing
- **FAIL** — Known to fail based on empirical measurement

## 1. Invariant catalog (35 entries)

| # | Domain | Invariant | Artifact | Status |
|---|---|---|---|---|
| 1 | enum | Every `ChordQuality` enum value appears in `IconicChords.yaml` at least once | GA.Domain.Core + IconicChords.yaml | C |
| 2 | enum | Every `HarmonicFunction` value is emitted by at least one corpus voicing | HarmonicFunction.cs + voicing-audit | T |
| 3 | enum | Every string in `.yaml` that names a `ChordQuality` / `ModalFamily` / `ScaleDegreeFunction` parses to a valid enum | all YAMLs + matching enum .cs | C |
| 4 | enum | No enum value is dead: every variant is read somewhere (code or YAML) | entire `GA.Domain.Core` | N |
| 5 | catalog | Every `BinaryScaleId` in `WorldScales/*.yaml` exists in `ExtendedScales.yaml` with matching pitch classes | WorldScales/, ExtendedScales.yaml | T |
| 6 | catalog | Every `Forte` number referenced in any YAML is in `ProgrammaticForteCatalog` | ForteCatalog.cs, YAMLs | C |
| 7 | catalog | Every YAML cross-ref ID dereferences (no dangling FK) | all YAMLs | N |
| 8 | catalog | Every `NeoRiemannian.yaml` triad name resolves to a valid `SetClass` | NeoRiemannian.yaml + SetClass.cs | C |
| 9 | catalog | `IconicChords.yaml` pitch-class content matches its `quality` | IconicChords.yaml | N |
| 10 | round-trip | `PitchClassSet` → string → `PitchClassSet` is identity for all 4096 subsets | PitchClassSet.cs | C |
| 11 | round-trip | `Voicing` → msgpack → `Voicing` is byte-identical | OPTIC-K writer | C |
| 12 | round-trip | F# YAML record → serialize → deserialize is field-identical | YamlKnowledgeLoader.fs | N |
| 13 | round-trip | C# `ChordName` → display string → parse produces same `ChordName` | ChordIdentification.cs | T (127 cases) |
| 14 | algebraic | Neo-Riemannian P, L, R are involutions: T∘T = id | NeoRiemannianConfig.fs | T (by construction) |
| 15 | algebraic | PLR composites match: L∘P∘R = S, P∘L = N | same | C |
| 16 | algebraic | T12 on any `PitchClassSet` returns the same set | PitchClassSet.cs | C |
| 17 | algebraic | Double inversion: I∘I = id on PitchClassSet | same | N |
| 18 | algebraic | Rotating a scale by its cardinality returns the same scale | ModeCatalog.cs | N |
| 19 | algebraic | `PrimeFormId` is self-representative: PrimeForm(x).PrimeFormId == x.Id iff x is prime | PitchClassSetId.cs | N |
| 20 | algebraic | `IntervalClassVector` is palindrome-invariant under inversion | IntervalClassVector.cs | N |
| 21 | cardinality | Triad template → exactly 3 distinct PCs after octave reduction | VoicingCharacteristics.cs | T (Phase-A/D fix) |
| 22 | cardinality | Seventh template → exactly 4 distinct PCs | same | T |
| 23 | cardinality | Voicing MIDI count ≤ instrument string count | Instruments.yaml + PhysicalLayout.cs | C |
| 24 | cardinality | `IntervalClassVector` has exactly 6 entries summing to C(n,2) where n=cardinality | IntervalClassVector.cs | N |
| 25 | embedding | Cross-instrument: voicings with identical PC-set have identical STRUCTURE partition | optick.index | **FAIL** (56% leak) |
| 26 | embedding | MIDI-octave invariance: transposing by 12 leaves STRUCTURE vector unchanged | optick.index | C |
| 27 | embedding | Embedding norm ∈ [0.99, 1.01] for all partitions (partition-normalized) | embedding-diagnostics | T |
| 28 | embedding | No partition accuracy exceeds 1/3 + 3σ in 3-class leak test | same | **FAIL** (STRUCTURE) |
| 29 | schema | `SchemaHashV4` computed in C# == computed in Rust for same input | EmbeddingSchema.cs + ix-optick | T |
| 30 | schema | OPTIC-K mmap header dims == `EmbeddingSchema.TotalDimensions` | optick.index header | C |
| 31 | schema | Dim count never changes without a corpus rebuild + new schema hash | CLAUDE.md states "never change" | N (CI-gatable) |
| 32 | text-vs-pc | Same PC-set across octaves → cosine(STRUCTURE, STRUCTURE') == 1.0 | optick.index | **FAIL** (29.4%) |
| 33 | text-vs-pc | `ChordName` consistency across instruments for same PC-set | corpus | **FAIL** (29.4%) |
| 34 | governance | Every persona YAML has `affordances`, `goal_directedness`, `estimator_pairing` | governance/demerzel/personas/ | T |
| 35 | governance | Every belief file is tetravalent-valid (T/F/U/C only) | state/beliefs/*.json | C |

## 2. C#/F# mistake tracker roadmap (16 categories)

| # | Mistake | IX primitive | Difficulty | FP rate | Severity |
|---|---|---|---|---|---|
| M1 | Methods > 100 LOC in hot paths | `ix_code_analyze` (SLOC) | S | 5% | Medium |
| M2 | Cyclomatic complexity > 15 | `ix_code_analyze` | S | 5% | Medium |
| M3 | `throw new` in service layer (ROP violation) | `ix_ast_query` | S | 15% (boundary throws) | High |
| M4 | `.Result` / `.Wait()` on Tasks (deadlock risk) | `ix_ast_query` | S | 5% | High |
| M5 | Deep nesting ≥ 5 | `ix_code_smells` (AST) | S | 10% | Medium |
| M6 | Magic numbers in music code | `ix_code_smells` (lexical) | S | 40% (0..11 PCs legit) | Low |
| M7 | `mutable` > 5 in F# file | `ix_code_smells` F# | S | 15% | Low |
| M8 | Public record field added/removed across commits (breaking) | **gap**: ix-context C# symbol diff | L | 5% | High |
| M9 | Dead enum value (defined but never read) | **gap**: C# call graph + YAML scan | M | 10% | Medium |
| M10 | Empty `catch { }` / `catch (Exception)` too broad | `ix_ast_query` | S | 10% | High |
| M11 | Primitive obsession (string param where value object exists) | **gap**: type-signature scan + registry | M | 30% | Medium |
| M12 | God class (> 500 SLOC, > 20 public methods) | `ix_code_analyze` per-file | S | 5% | Medium |
| M13 | Public API without `///` doc comment | `ix_ast_query` | S | 5% | Low |
| M14 | Nullability regression (`?` removed from a public type) | **gap**: cross-version diff | L | 5% | High |
| M15 | Cross-project type duplication (same record in two namespaces) | **gap**: C# symbol index + MinHash | M | 15% | Medium |
| M16 | Cyclic project dependency | **gap**: `.csproj` reference DAG → ix-pipeline::Dag | S | 0% | High |

## 3. IX primitive gaps (ranked by unlocks × 1/effort)

1. **`ix-context` C#/F# cross-file symbol index** — unlocks M8, M9, M11, M14, M15 + invariants 1, 3, 4, 7, 8, 9. Effort: **L**. *Foundational; highest leverage.*
2. **`ix-catalog-lint` crate** — generic YAML↔enum/catalog FK checker. Takes YAMLs + a schema (extracted from C# enums via tree-sitter) and emits dangling-ref report. Unlocks invariants 1, 3, 4, 5, 6, 7, 8. Effort: **M**.
3. **`ix-roundtrip` harness** — property-test driver that takes `(serialize, deserialize, sample_generator)` and runs N iterations. Unlocks invariants 10, 11, 12. Effort: **S**.
4. **`.csproj` DAG analyzer** — thin reader emitting `ix-pipeline::Dag<ProjectId>`; detects cycles, computes layer depth against the 5-layer rule in `ga/CLAUDE.md`. Unlocks M16 + enforces architecture. Effort: **S**.
5. **`ix-embedding-invariant` crate** — generalizes `ix-embedding-diagnostics` to accept arbitrary partition specs and check: (a) leak-bounds, (b) transposition-invariance, (c) cross-instrument PC-equality → cosine = 1. Unlocks invariants 25, 26, 28, 32, 33. Effort: **M**. (Partly exists; needs generalization.)

## 4. Top 10 invariants to add tests for NEXT

| # | Invariant | Bug it catches | Est. pre-test failure freq |
|---|---|---|---|
| 1 | #25 Cross-instrument STRUCTURE equality for same PC-set | 56% leak (confirmed) — blocks ga-chatbot correctness | **CERTAIN FAIL today** |
| 2 | #33 `ChordName` cross-instrument consistency | 29.4% today; test gates the refactor | **CERTAIN FAIL today** |
| 3 | #19 `PrimeFormId` is self-representative | Silent corruption of equivalence classes → wrong chord IDs | 1-3 violations likely |
| 4 | #3 YAML strings parse to enum values | Typos ("Sus4" vs "sus4") silently fall through to Unknown | 5-10 violations likely |
| 5 | #17 Double inversion is identity | Catches PC-set rotation bugs immediately | Low, but cheap |
| 6 | #15 PLR composites (L∘P∘R = S, P∘L = N) | Protects NeoRiemannian refactors | Medium |
| 7 | #24 `IntervalClassVector` length + sum | Off-by-one in IC histogram | Medium |
| 8 | #29 `SchemaHashV4` C# ↔ Rust equality (in CI) | Silent schema drift = stale OPTIC-K index | ~once per release |
| 9 | #30 mmap header dims == schema dims | Build-time rebuild omissions | Once per dim change |
| 10 | #23 Voicing MIDI count ≤ string count | Corpus regression bug historically seen | Unknown; cheap |

## 5. CI automation tiers (proposed)

### Per-commit (< 30 s budget, run in PR)
- `ix_code_smells` + `ix_code_analyze` on changed `.cs`/`.fs` files (M1, M2, M5, M6, M7, M12)
- `ix_ast_query` patterns for M3, M4, M10, M13
- Invariants #13, #17, #19, #21, #22, #24 as xUnit/NUnit tests under `Tests/GA.Business.Core.Tests/Invariants/`

### Per-PR merge (5-minute budget, GitHub Action)
- `ix-catalog-lint` over every YAML (invariants #3, #5, #6, #7, #8)
- `.csproj` cycle check via `ix-pipeline::Dag` (M16)
- Invariants #10, #14, #15 (round-trip + algebraic) as full suites

### Per-release (nightly or manual)
- Rebuild OPTIC-K index + run `ix-embedding-invariant` (invariants #25, #26, #28, #32, #33)
- Corpus-level invariants #23, #30 against freshly-built `state/voicings/optick.index`
- Schema-hash cross-check: C# prints hash, Rust prints hash, CI diffs (invariant #29)

### On-demand (manual / auditor persona)
- Dead-enum scan (M9), cross-project duplication (M15), public-API breaking-change diff (M8, M14) — needs `ix-context` C#/F# symbol index first

**Wiring mechanism:** GitHub Action invokes `ix-skill` binary directly (not the MCP server — MCP is for agent loops). One composite action per budget tier; each emits SARIF so it surfaces in the PR "Files changed" view.

## Key prioritization insight

The two invariants already known to fail (#25, #33) should ship as tests **first** — not last. Put them in red immediately, then the refactor that fixes them flips the test to green. That is the round-trip methodology applied to the methodology itself.

The `ix-context` C#/F# symbol index (gap #1) is the single highest-leverage IX investment: it gates five mistake trackers and seven invariant categories. Budget for it first.

## Links

- Methodology: `docs/methodology/chord-recognition-refactor-methodology.md`
- Architecture plan: `docs/plans/2026-04-17-chord-recognition-architecture-plan.md`
- Baseline diagnostics: `state/baseline/embedding-diagnostics-2026-04-17.json`
- Corpus audit: `state/audit/voicing-audit-2026-04-17.json`
- IX diagnostic tool: `crates/ix-embedding-diagnostics/` in ix repo
- First red tests: `Tests/Common/GA.Business.Core.Tests/Voicings/EmbeddingInvariantsTests.cs`
