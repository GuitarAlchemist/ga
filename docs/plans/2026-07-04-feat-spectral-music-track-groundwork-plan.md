# Spectral Music Intelligence Track — groundwork capture

**Status: pre-epic groundwork, captured 2026-07-04.** The epics themselves land in BACKLOG.md once the frugal deep-research run (`wf_7b53e48d-cbc`: corpora/licensing, chord-prediction SOTA, Fourier music theory validation, product/legal landscape) delivers its verified facts. This document captures everything already established so the epics only have to add the external evidence.

Type: feat (track groundwork). Reversibility: two-way (analysis + planned seams only; the one-way doors — OPTIC-K dims, index format — are explicitly NOT touched by anything here).

---

## 1. The organizing insight

**OPTIC-K is already the frozen encoder that the JEPA recipe requires.** The strongest actionable finding of the J4 world-models research (docs/research/2026-07-04-…-j4-world-models.md) was Meta's JEPA-WM recipe: never train the encoder — freeze a foundation model and train only a small action-conditioned predictor in latent space. For process telemetry this failed on "no domain backbone exists". For **music**, GA hand-built the backbone: 240 musically-structured dims, stable by one-way-door policy, 313k indexed voicings, partition-level interpretability for free. The recipe applies with **zero encoder training**.

Corollary from ga#513 (docs/research/2026-07-04-optick-spectral-phase-alignment.md): a **transposition-equivariant prediction loss** — predict shape (Fourier magnitudes, Tₙ-invariant) and transposition (phases) separately, using the phase-aligned similarity `S` as the distance — so a predictor learns "after ii–V comes I" once, not twelve times.

## 2. The four feature threads (ideation, 2026-07-04 session)

1. **Chord⊂scale inclusion lattice** — exhaustive and *provable* over the 4096 pitch-class sets (the finite-universe sweep doctrine of docs/solutions/architecture/2026-06-19). Powers chatbot answers and fretboard overlays. Zero ML.
2. **Chord→scale matching by phase distance** — the ga#513 operator applied between a voicing and the scale catalog; magnitude k=5 = diatonicity, its phase locates the key on the circle of fifths (Quinn/Amiot); modes = same set + ROOT partition. Ranks candidates *within* the lattice.
3. **Arpeggio paths on the fretboard** — arpeggios are ordered sequences + fingerings, not sets. v1 is deterministic composition of existing pieces (see §4 gap 3); `t*` from ga#513 gives "this shape, transposed to your key". ML only later at the lick/line level.
4. **Progression-JEPA** — next-chord prediction in OPTIC-K latent space; tiny predictor (per-partition MLP or ix's GBDT), honest baselines (persistence, Markov-on-symbols), pause rule if it can't beat Markov.

**Data doctrine (tabs question, resolved in principle):** free faithful tabs are rare and legally awkward (tab transcriptions are derivative works; progressions themselves generally are not protectable). The J4-validated pattern (UI-Simulator, Dreamer-7B) inverts the scarcity: **synthesize training volume from GA's own harmony grammars** (unlimited, faithful by construction, natively key-normalized — matching the equivariant loss; and 100% owned, a compounding proprietary asset), fine-tune/evaluate on small expert corpora (McGill Billboard, RS200, Hooktheory/TheoryTab, Weimar Jazz DB, KernScores — sizes/licenses pending research verification), Lakh MIDI only as noisy pre-training if volume is short. **Scarce faithful human data = the held-out eval set, never the bulk training set.** Curated human tabs answer exactly one question: does the model prefer fingerings humans actually choose?

## 3. Code capability map (Explore agent inventory, 2026-07-04)

What already EXISTS and gets reused as-is:

| Capability | Where | Note |
|---|---|---|
| PC-set algebra: `IsSubsetOf`/`IsSupersetOf`, full 4096 universe (`PitchClassSet.Items`), `PrimeForm`, 24-form orbits | `Common/GA.Domain.Core/Theory/Atonal/PitchClassSet.cs` | Complete |
| **Fourier at domain level**: `SetClass.GetFourierCoefficients()`, `GetPhaseSpectrum()`, `GetSpectralDistance()` | `SetClass.cs` (layer 2!) | ga#513's theory has native domain anchors |
| Lattice pattern in miniature: `GetCompatibleKeys()` = "scales containing this chord" scoped to 24 keys | `PitchClassSet.cs:592` | Generalize, don't invent |
| Scale/mode catalog: tonic-aware modes, world scales via YAML (maqam, raga, klezmer, bebop…) | `…/Tonal/Scales/`, `GA.Business.Config/*.yaml` | Rich |
| All fingerings of a PC-set: `ShapeGraphBuilder.GenerateShapes` | `GA.Domain.Services/Fretboard/Shapes/` | |
| Pairwise transition costs: `PhysicalCostService`, `ShapeTransition`, `ProgressionVoiceLeadingAnalyzer` | `GA.Domain.Services/Fretboard/…` | |
| Pitch-sequence → playable positions: `TabSequenceSolver`, `AdvancedTabSolver` | `GA.Business.ML/Tabs/` | |
| **ONNX inference precedent**: `MlNaturalnessRanker` (trained elsewhere, inferred in GA) | `GA.Business.ML/Naturalness/` | The train-in-ix / infer-in-GA pattern exists |
| **Phase un-quantization precedent** + phase barycenter over progressions | `GA.Business.ML/Retrieval/ModulationAnalyzer.cs:81` | `S` has a natural neighbor |
| One-hop next-chord suggestion (spectral + transition cost) | `GA.Business.ML/Retrieval/NextChordSuggestionService.cs` | Progression-JEPA's seed |
| Progression types + Roman-numeral config queries | `…/Harmony/Progressions/`, `ChordProgressionsConfigLoader` | Sequence *shape* exists |
| mmap index reader | `GA.Business.ML/Search/OptickIndexReader.cs` | See gap 2 |

## 4. The five real gaps (the answer to "do we need domain or ML improvements?")

**Verdict: no domain overhaul; exactly one ML improvement is architectural; the rest is composition of existing pieces.**

| # | Gap | Blocks | Layer | Size |
|---|---|---|---|---|
| 1 | Exhaustive chord⊂scale lattice + cache: generalize `GetCompatibleKeys()` over `Scale.Items`; API like `PitchClassSet.ContainingScales()` | lattice, phase matching | 2–3 | small |
| 2 | **No per-candidate scorer injection in the mmap scan** — `OptickSearchStrategy.SearchInternal` hardcodes `TensorPrimitives.Dot`; `IVoicingSearchStrategy` is pluggable at strategy level only. Design a proper seam (`ICandidateScorer` or a re-ranking pass) since every future operator (phase-aligned `S`, TnI flag, Quinn-weighted variants) flows through it | phase matching in retrieval | 4 | **the one architectural item — design it once, properly** |
| 3 | No multi-node path generator: everything exists pairwise (`ShapeGraphBuilder` × `ShapeTransition` × solvers) but nothing chains shapes into an arpeggio/lick path | arpeggio paths | 3 | medium, pure composition |
| 4 | No sequence-of-embeddings type + no DSL→training-sequence adapter (`Progression` exists but bare); model *training* stays in ix per the realtime/offline boundary | Progression-JEPA | 4 (+ix) | small on GA side |
| 5 | Cached/indexed inclusion query (perf twin of #1 — without it every consumer re-derives via LINQ on the hot path) | lattice at query time | 3 | small |

## 5. What the running deep-research must settle before the epics freeze

- Exact sizes/licenses/access of McGill Billboard, RS200, Hooktheory/TheoryTab, Weimar Jazz DB, iRb, Lakh, KernScores — which are usable for training vs eval-only.
- Chord-progression-prediction SOTA 2020-2026 (models, benchmarks, metrics, data scale) — the bar Progression-JEPA must beat; existence of music JEPAs (Stem-JEPA, MERT…) as prior art.
- Published Quinn/Amiot/Tymoczko results that validate or bound the phase-matching approach (esp. k=5 phase ↔ key-finding).
- Product landscape + tab licensing reality (Ultimate Guitar/MPA) — differentiation and legal red lines.

**Next step:** epics M1–M5 into BACKLOG.md (tracer-bullet discipline, one per thread + one for the data pipeline), referencing this document and the research report; then `[Epic]` issues.
