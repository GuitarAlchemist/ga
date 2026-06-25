# Architecture Deepening Campaign 2 — 2026-06-23

> Origin: second `/improve-codebase-architecture` sweep (5 Explore agents across the regions the
> [2026-06-21 campaign](2026-06-21-arch-deepening-campaign-plan.md) under-walked — F# config, non-chat
> GaApi, MCP/closure dispatch, React dashboard data, tag/mood analysis). Full visual report:
> `%TEMP%/architecture-review-20260623-223030.html`. User said **"explore all"** → execute every
> candidate as its own **vertical slice / PR** (tracer-bullet discipline, CLAUDE.md), in risk +
> dependency order. Not one mega-refactor.

## Relationship to Campaign 1

Campaign 1 (#1–#9) is still in flight (chat intake seam #1 in progress; #2/#5/#7/#9 queued). **These
eight are a disjoint set** — none overlaps a Campaign-1 slice or an ADR. Where they're adjacent it's
called out (C2-#1 shares the `ErrorResponse` shape with the chat seam; C2-#5 hits the same Demerzel
tribunal gate as C1-#2).

## Dropped / off-limits (do not re-suggest)
- **Split `MusicalVoicingAnalysis`** — an Explore agent re-suggested it this sweep; **ADR-0004** keeps it
  wide. Dropped on sight.
- Chat cluster (ADR-0005, C1 in flight), ChordIdentification (C1-#3 sealed), DuckDB lens (ADR-0001),
  CPU↔GPU filter parity (ADR-0002), TARS consistency (ADR-0003).

## Verification status (what changed under the hood)
- **C2-#1, #2, #3** — read by hand this sweep; evidence holds.
- **C2-#7 reframed.** Not "delete a pass-through": there are **two** `TonalBspService` types
  (`BSPCore.cs:260` vs `Spatial/TonalBSPService.cs:13`) and the `TonalPartitionStrategy` arg **is**
  consumed (`BSPController.cs:49`, `SimpleBSPDemo.cs:83`). The real friction = the duplicate type + a
  **stubbed distance metric** (`Spatial/TonalBSPService.cs:332,334` return constant `0.5`/`0.3`).
- **C2-#4, #8** — agent-sourced; each slice **starts with a confirm/spike step** before any edit.

## Sequence (risk + dependency order)

| Order | Slice | Strength | Risk | One-way door? | Verify |
|---|---|---|---|---|---|
| 1 | **C2-#3** F# config loader seam | Strong | low — isolated layer-2 | no | build + config tests |
| 2 | **C2-#2** ChordClassificationEngine | Strong | med — feeds SYMBOLIC tags | corpus re-tag (not a dim change) | tests + tag snapshot |
| 3 | **C2-#1** HTTP result seam (non-chat) | Strong | med — ~8 controllers | no | tests; coordinate w/ chat #1 |
| 4 | **C2-#5** MCP validate-envelope | Worth | low | Demerzel tribunal gate | tests + tribunal |
| 5 | **C2-#6** LSP block-analyzer seam | Worth | low — isolated F# DSL | no | LSP tests |
| 6 | **C2-#7** Resolve duplicate `TonalBspService` + stubbed metric | Worth | med | no | tests + BSP demo |
| 7 | **C2-#4** `useChordDataQuery` data module | Strong | low — ga-client only | no | confirm dup → npm build/lint |
| 8 | **C2-#8** Prime Radiant IXQL dispatcher | Speculative | — | no | **spike first**, then decide |

Rationale: bank the **verified, isolated** config seam first (C2-#3 — no cross-layer blast radius). Then
the top recommendation **C2-#2** (a correctness smell, not just dup). **C2-#1** lands the non-chat sibling
of the keystone while the chat seam is fresh, so they agree on one `ErrorResponse`. The two F#/MCP
worth-exploring slices (C2-#5, #6) are low-risk infill. C2-#7 is a clarity/correctness fix once the
duplicate type is understood. The two frontend slices tail the campaign: C2-#4 is a clean hook; C2-#8 is
a **read-spike → decide**, the only slice that may close as an ADR instead of shipping.

## Per-slice details

### C2-#3 — F# config loader seam  ✓ verified
`ScalesConfig.fs:79–91` carries both the four-path `findYaml()` probe **and** the
`mutable cache + Guid version + Reload` dance; the same two patterns repeat in `ModesConfig`,
`InstrumentsConfig`, `TabSourcesConfig`, `ExtendedScalesConfig`, `AtonalModalFamiliesConfig`,
`YamlKnowledgeLoader` (subtly inconsistent: `ConcurrentDictionary` vs `option`; `ReloadConfig` vs
`reloadConfig`). **Deepen:** `ConfigFileLocator.find name` + generic `ConfigCache<'T>` (Get / Reload /
Version); each config supplies a filename + parse fn. **Keep per-config:** the hardcoded fallback (F#
convention) — only the probe + cache mechanics move. Tracer-bullet: migrate `ScalesConfig` alone, tests
green, then the rest. Proposed `CONTEXT.md` term: **Config loader seam**.

### C2-#2 — ChordClassificationEngine  ✓ verified  ★ top recommendation
Two divergent implementations of the same words: `VoicingTagEnricher.cs:94–140`
(`melancholy = minor && midiMean<60`; `bright = major && midiMean≥67 && consonance≥0.60`) vs
`VoicingHarmonicAnalyzer.GenerateSemanticTags:170–206` (`melancholy = minor && consonance>0.6`;
`bright = consonance>0.8 && major`), with a probable third in `HybridChordNamingService:105–200`. **Deepen:**
one pure `ChordClassificationEngine.Classify(quality, consonance, register) → tags`; all callers cross it.
**Grilling decision needed (one-way-ish):** behavior-preserving (adopt one existing threshold set as
canonical so tags don't move — no re-tag) **vs** reconcile-and-re-tag (corpus re-tag; instrument the
SYMBOLIC-partition tag distribution per Karpathy rule 6). **Deletion-test bonus:** confirm whether
`GenerateSemanticTags` is already dead (the enricher header says it was built to *close the gap* that
older path left) — if so this is partly a deletion. Touches no embedding **dimension**. Proposed term:
**Chord classification engine**.

**Resolved design (grilling, 2026-06-24):**
- **Premise corrections (read by hand).** `HybridChordNamingService` is *naming*, not mood — **out of
  scope**. The real third tagger is `InterpretationService.GenerateSemanticTags` (layer 4,
  `GA.Business.AI`), the **name-substring** tagger the corpus indexer actually calls
  (`IndexVoicingsCommand.cs:281`). `GenerateSemanticTags` is **not dead** (`VoicingHarmonicAnalyzer.cs:27`).
- **Scope (Q1).** All three taggers, **staged**. The two runtime taggers (layer 3) first; the corpus
  tagger (layer 4) second.
- **Seam (Q2).** `ChordClassificationEngine.Classify(ChordClassificationContext) → IReadOnlyList<string>`,
  layer-3 static in `GA.Domain.Services` (the lowest layer all three callers reference, so layer-4
  `InterpretationService` can depend down). Flat canonical strings out (every consumer flattens into one
  `SemanticTags` bag anyway). Mood-cap (≤2) stays **internal** to `Classify`.
- **Canonical basis (Q3).** "Behaviour-preserving" is impossible — the taggers disagree (e.g. `melancholy`
  = `minor&&midiMean<60` vs `minor&&consonance>0.6` vs name~minor). **One rule per tag**, the
  register-aware `VoicingTagEnricher` rules win (newest, conservative, the *only* tested tagger —
  `VoicingTagEnricherTests.cs`). Fold in the name/quality long-tail additively (iconic chords,
  `dim`→tense, Lydian→floating). **Reject** the union approach (would fire `melancholy` on ~every minor).
- **Vocabulary enforcement (Q4).** `SymbolicTagRegistry` (also layer 3) is the canonical name source via
  `SemanticNomenclature.yaml`. The engine emits **only registry-known names**; a test asserts every emitted
  tag has a non-null `GetBitIndex`. This is the real deepening — today's hardcoded literals can typo into a
  dead tag that fires no SYMBOLIC bit (the 2026-04-18 "thin bits" gap). Slice 2 repoints the layer-4
  `InterpretationTags` constants onto the registry.
- **No silent tag loss (Q5).** `consonant/dissonant/wide-voicing/close-voicing/balanced` live *only* in
  `GenerateSemanticTags`. Before deleting it, **reassign** every output: structural `wide/close-voicing` →
  `VoicingPhysicalAnalyzer`; perceptual `consonant/dissonant/balanced` → engine. Nothing dropped.
- **Staging (Q6).** **Slice 1 (layer-3, behaviour-internal, reversible, CI-gated):** build engine + port
  `VoicingTagEnricherTests` to it, repoint `VoicingTagEnricher` + `VoicingHarmonicAnalyzer`, delete the two
  bodies, green build/tests — **no corpus re-index**. **Slice 2 (corpus, one-way door):** repoint
  `InterpretationService` → engine, baseline+after SYMBOLIC tag-density under `state/quality/` (guardrail:
  density must not drop), then OPTIC-K re-index per the `optic-k-rebuild` skill (stop GaApi/GaMcpServer
  mmap lock first).

**Implementation progress (2026-06-24):**
- **Step A — DONE, verified, uncommitted.** `ChordClassificationEngine.Classify(ChordClassificationContext)`
  + `ChordClassificationContext` added in `GA.Domain.Services` (VoicingTagEnricher rules moved verbatim =
  canonical basis); `VoicingTagEnricher.Enrich` reduced to a thin adapter delegating to the engine. Build
  clean; 18 `VoicingTagEnricherTests` + 18 `VoicingCharacterization`/`VoicingAnalyzer` tests green —
  behaviour-preserving. The seam now exists; one caller crosses it.
- **Step B — DONE, verified, uncommitted.** Stripped `jazz`/`melancholy`/`bright` from
  `VoicingHarmonicAnalyzer.GenerateSemanticTags` — the engine owns them via `AnalyzeEnhanced`'s `Enrich`
  path with register-aware, one-rule-per-tag logic (deliberate runtime canonicalization; no test asserts
  them — verified). Build 0W/0E; 36 tests green. **New evidence (registry cross-check):**
  `GenerateSemanticTags` emits mostly **dead tags** — `consonant`/`dissonant`/`wide-voicing`/
  `close-voicing`/`pop`/`tension`/`dark`/`balanced` are **not** in `SemanticNomenclature.yaml`, so they fire
  no SYMBOLIC bit (note the near-misses: it emits `"tension"` where canonical is `tense`, `"close-voicing"`
  where canonical is `closed-voicing`). Its only *live* tags (`jazz`/`melancholy`/`bright`) duplicate &
  diverge from the engine — confirming the deletion-test bonus. Dead-tag cleanup needs a downstream-
  consumption trace (text-embedding? display?) before removal, so it is **separate** from the divergence
  kill. Also add the Q4 registry-enforcement test (every engine tag has non-null `GetBitIndex`).

### C2-#1 — HTTP result seam (non-chat transports)  ✓ verified
`MonadicChordsController.cs:90–110` returns a rich `ErrorResponse` DTO via an `error.Type` switch (×3 in
the file); `ContextualChordsController.cs:54–69` returns bare strings (`"Invalid request."`); Voicings /
Search / Tts / YouTubeTab repeat `BadRequest(new { error })`; GraphQL `MusicTheoryQuery` /
`ChordNamingQuery` inline the same validate-then-call. **Deepen:** one `Result<T,E> → IActionResult`
mapper (the `ErrorResponse` DTO already exists — make it the single authority); controllers become
`this.ToResult(result)`; GraphQL resolvers cross the **same** seam (anti-corruption). **Coordinate with
chat #1** so both use one `ErrorResponse` shape. Excludes the chat surfaces (ADR-0005). Proposed term:
**HTTP result seam**.

### C2-#5 — MCP validate-envelope  ✓ verified (narrowed)
Domain logic already deep (`ChordVocabulary` / `ChordSpelling`, PR #102). Remaining dup = the thin outer
ring: `length-guard → McpEchoSanitizer.SanitizeEcho → Failure` across `ChordMcpTools.cs:47–58`,
`ScaleMcpTools`, `FretSpanMcpTools`, `IntervalMcpTools`, `KeyIdentificationMcpTools`; **plus** the
`resolve-closure → category-guard → error-shape` ring across `DslEvalMcpTools` ListClosures /
GetClosureSchema / EvalClosure (`:91–250`). **Deepen:** `McpToolDispatcher.Dispatch(input, handler)` owns
the envelope; each tool = handler lambda. Moderate win (heavy logic already shared) → Worth-exploring.
**Demerzel tribunal gate** applies (touches `GA.Business.ML/Agents`).

### C2-#6 — LSP block-analyzer seam  ✓ verified
`GaBlockDetector.fs:27–117` exposes low-level primitives, so `LanguageServer.validateDocument:211–230`
hand-orchestrates `find → per-block validate → offset-translate`, and CompletionProvider /
DiagnosticsProvider repeat the loop. **Deepen:** `analyzeBlocks text analyzers` owns find + iterate +
offset; each handler registers an analyzer. Depth-for-future-leverage (current code works) → adding a
new analyzer (hover, semantic tokens) stops touching three handlers.

### C2-#7 — Resolve duplicate `TonalBspService` + stubbed distance metric  (reframed)
**Two** `TonalBspService` types in `GA.BSP.Core`: `BSPCore.cs:260` (pass-through `SpatialQuery`) and
`Spatial/TonalBSPService.cs:13` (threads `strategy` through `SpatialQueryRecursive`, but
`CalculateNodeDistance`/`CalculateRegionDistance` at `:332,:334` return constant `0.5`/`0.3`). The
strategy arg is **live** (`BSPController.cs:49`, `SimpleBSPDemo.cs:83`). **Step 1 (clarity):** establish
which `TonalBspService` is canonical and collapse/namespace the duplicate. **Step 2 (depth, optional):**
make the partition-strategy distance metric real (the enum has 5 strategies — CircleOfFifths /
ChromaticDistance / HarmonicSeries / ModalBrightness / TonalStability — but the metric is a constant).
Audit `BSP.Service` consumers first.

### C2-#4 — `useChordDataQuery` data module  (confirm-first)
Agent reports `useEffect → AbortController → setState` duplicated across `ChordPalette.tsx:57–93`,
`ProgressionExplorer.tsx:65–80`, `SmartVoicingDisplay.tsx:30–50`, with `GAChatPanel.tsx:64–87` building
its voicing URL inline (bypassing `musicService`). **Step 1:** re-read the four fetch sites to confirm the
duplication and the GAChatPanel drift (line numbers unverified). **Deepen:** `useChordDataQuery(chord,
type) → {data, loading, error}` owns Abort + URL + optional cache. Isolated to `ga-client`; npm
build/lint is the gate. Aligns with the IXQL-native preference (declarative query, not ad-hoc handlers).

### C2-#8 — Prime Radiant IXQL dispatcher  (spike, may close as ADR)
`PrimeRadiant/` is 100+ files; `IxqlDispatcher.ts`, `IxqlPipeEngine.ts`, `IxqlWidgetSpec.ts`,
`IxqlControlParser.ts`, `DataFetcher.ts`, multiple Godot/SignalR bridges all confirmed present.
**Step 1 (spike, no edits):** read `ForceRadiant.tsx` + one IXQL panel + `DataFetcher.ts` to confirm or
refute "panels each repeat parse→dispatch→render; DataFetcher tangles governance + Godot + GraphQL."
**Then decide:** declarative `IxqlQuerySpec` + `useIxqlQuery` + `PanelRenderer` factory and split a
`GovernanceDataService` — **or** close as an ADR if the coupling is intentional. Largest, speculative;
strongly aligned with IXQL-native architecture.

## Reversibility / one-way doors
- **C2-#2** can move SYMBOLIC tags → grilling must choose behavior-preserving vs re-tag; instrument tag
  distribution either way (Karpathy rule 6). No embedding-dimension change.
- **C2-#5** touches `GA.Business.ML/Agents` → Demerzel tribunal gate (as C1-#2).
- All others are reversible refactors/deletions; CI build is the authoritative gate.

## Next step
Each slice needs design grilling (`/grilling`) before implementation per the skill. Order above is the
queue. C2-#3 is the cleanest tracer-bullet to start; C2-#2 is the highest-value.
