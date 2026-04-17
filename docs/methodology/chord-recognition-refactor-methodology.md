# Methodology: Chord Recognition Architecture Refactor

**Status:** Active
**Session:** 2026-04-16 → 2026-04-17 (ongoing)
**Retained for:** executive reports, retrospectives, future similar refactors

## Purpose of this document

Capture the end-to-end methodology we followed — from problem discovery through diagnosis, planning, measurement baseline, and phased execution — so that:

1. **Executive reports** have an authoritative source of what was done, why, and what the measurable outcome was.
2. **Future refactors** in GA/IX can reuse the same diagnostic→plan→execute→measure loop.
3. **New team members** can understand the chord-recognition pipeline without rediscovering the problem.

## The problem, in one paragraph

The OPTIC-K embedding pipeline produces 112-dim vectors that drive the GA chatbot's voicing search. The pipeline has five partitions with fixed weights: STRUCTURE (0.45), MORPHOLOGY (0.25), CONTEXT (0.20), SYMBOLIC (0.10), MODAL (0.10). Only MORPHOLOGY *should* be instrument-specific; the other four should be content-invariant. In practice, labeling quality in the chord recognizer was corrupting the content-based partitions via mislabeled `ChordName` strings, and register-dependent slash notation was leaking into the SYMBOLIC partition. Cross-instrument consistency (same pitch-class set → same chord name on guitar / bass / ukulele) was 29.4% — meaning 70% of the time, the same chord was labeled differently depending on the instrument that played it.

## Methodology phases

### Phase 1 — Diagnosis before prescription

**Step 1.1: Survey existing capability**

We enumerated what IX already provided (`ix-topo`, `ix-supervised`, `ix-ensemble`, `ix-unsupervised`, `ix-stats`) and what GA already contained (`VoicingAnalyzer`, `ProgrammaticForteCatalog`, `ModalFamily`, `EmbeddingSchema`). This prevented re-inventing infrastructure and revealed the existing `ModalFamily` already grouped PC-sets by ICV — we just needed to expose it via a YAML bridge.

**Step 1.2: Catalog inventory**

Before touching code, we inventoried all taxonomies: enums (`ChordQuality`, `HarmonicFunction`, `DropVoicing`), YAML catalogs (`Scales.yaml`, `Modes.yaml`, `IconicChords.yaml`, `ExtendedChords.yaml`), and new structures we were about to add. Found:

- `Modes.yaml` had 31 named families already
- `ProgrammaticForteCatalog` had 224 set classes
- `ChordQuality` enum was missing altered-dominant values used 12+ times in `IconicChords.yaml`
- `Diminished (Octatonic) Family` in `Modes.yaml` was a duplicate of `Diminished Family`

**Step 1.3: Multi-dimensional audits (parallel agents)**

We dispatched four read-only audits simultaneously:

- **Corpus audit** — ran `VoicingAnalyzer` over all 313,047 deduped voicings, emitted quality statistics
- **Taxonomy audit** — catalogued every enum + YAML categorical field, found cross-references that didn't resolve
- **Guitarist research** — surveyed real user questions and documented LLM failure modes
- **F# config survey** — mapped what ground truth already existed in GA for deterministic verification

These ran in parallel, took ~5 min each, and produced ~2,000 words of findings per audit. Running them serially would have taken 30+ minutes of sequential work.

### Phase 2 — Knowledge-base expansion (additive, no risk)

Before touching analyzer code, we added MORE ground truth so the refactor had richer targets to verify against:

- **ExtendedScales.yaml** (4,095 pitch-class sets, 1.3 MB) — binary IDs, interval vectors, Forte numbers, both Forte + Rahn prime forms
- **NeoRiemannian.yaml** (144 transform mappings on 24 triads + 7 cycles)
- **AtonalModalFamilies.yaml** (200 atonal families, 53 named, 147 positional, with cross-tonal-family bridging)
- **WorldScales/** (10 per-tradition YAMLs, 97 scales across Maqam, Raga, Japanese, Klezmer, Flamenco, etc.)

Every entry cross-validated: each world-scale `BinaryScaleId` was verified to exist in `ExtendedScales.yaml` with matching pitch classes. Legal guardrails enforced — no scraped text, no copyrighted coinages, all provenance tagged.

**Why this came before the refactor**: a chord recognizer is only as good as the catalog it matches against. Expanding the catalog first meant fewer edge cases the recognizer would flunk.

### Phase 3 — Measurement baseline (the scientific prerequisite)

Before any refactor that could plausibly improve or harm embedding quality, we built a baseline diagnostic tool that produced hard numbers we could later compare against.

**`ix-embedding-diagnostics` crate** measured:

1. **Leak detection per partition** — train a supervised classifier to predict instrument from each partition's embedding dimensions. Random baseline on 3-class balanced problem = 33.3%. Anything above 40% = instrument identity is leaking into that partition.

2. **Retrieval consistency** — inject STRUCTURE-only queries, measure % of top-10 results sharing the query's pitch-class set.

3. **Cluster labels (baseline for ARI)** — k-means with k=50, save all 313,047 cluster assignments. After refactor, re-cluster and compute Adjusted Rand Index. ARI ≥ 0.85 = semantic meaning preserved.

4. **Topological profile per instrument** — persistent homology β₀ (components), β₁ (loops) on 1000-point samples per instrument.

**Baseline results (pre-refactor, 2026-04-17):**

| Partition | Classifier accuracy | Verdict |
|---|---:|---|
| STRUCTURE | 56.0% | LEAK (1.68× random) |
| MORPHOLOGY | 79.0% | by design |
| CONTEXT | 50.7% | LEAK |
| SYMBOLIC | 63.6% | LEAK (worst) |
| MODAL | 50.2% | LEAK |

Topology: guitar β₁=48, bass β₁=135, ukulele β₁=134 — the 4-string instruments have structurally different embedding manifolds.

**This baseline is the contract.** After the refactor lands, the same tool re-runs. Acceptance gate: STRUCTURE + CONTEXT + SYMBOLIC + MODAL all drop below 40% and ARI ≥ 0.85.

### Phase 4 — Plan with supersession

Two plans were written as the diagnosis unfolded:

1. **Tactical plan (Sprint 1)** — proposed surgical fixes (reject degenerate templates, strip slash notation, dyad fallback, delete duplicate family). Would have gotten consistency from 29% to ~85% in 1-2 days.

2. **Architectural plan (supersedes Sprint 1)** — after ultrathinking the problem, we recognized two conflations that Sprint 1 patched but didn't fix:
   - Template generation mixed enumeration with naming (factory-produced ~1500 templates from Cartesian product of modes × degrees × extensions × stackings)
   - `ChordIdentification` fused invariant identity (`C Major`) with voicing-specific slash (`/E`)
   
   The architectural plan (Phases A-D, ~5-6 days) replaces the foundation and subsumes every Sprint 1 fix as a sub-case.

**Methodological note**: we explicitly documented the supersession in the second plan's header. When a later reviewer asks "why didn't you do Sprint 1 first?", the answer is right there.

### Phase 5 — Execution (phased, measurable, rollback-safe)

**Phase A — Canonical Chord Pattern Catalog**
- ~55 hand-curated interval patterns: triads, 7ths, 9/11/13 extensions, altered dominants, add chords, quartal, symmetric set classes, shell voicings
- `ChordIntervalPattern` record with `TryMatch()` returning `(missing, extra, overlap)` metrics
- Priority ordering: 0-9 core, 10-29 extensions, 30-49 altered, 50-69 add/sus, 70-79 quartal, 80-89 symmetric, 90-99 edge
- Zero risk: additive, 0 errors on build

**Phase B — Canonical Chord Recognizer** *(just committed)*
- Constraint satisfaction: for each candidate root, compute intervals-from-root, match against catalog, score by `(distance, priority, rootCommonness)`
- Three ranges handled distinctly: cardinality 1 (unison), 2 (dyad with interval name), 3+ (pattern match with Forte fallback)
- No bass-dependent tie-breaking — same PC-set → same result on every instrument
- `CanonicalChordResult` splits `CanonicalName` (invariant) from `SlashSuffix` (voicing-specific)

**Phase C — Split `ChordIdentification`** (pending)
- Add `CanonicalName`, `SlashSuffix`, structured fields to existing record
- `DisplayName` property composes them for UI layer
- `VoicingDocumentFactory` reads `CanonicalName` into SYMBOLIC embedding dims

**Phase D — Integration in `VoicingHarmonicAnalyzer`** (pending)
- Route `IdentifyChord` to `CanonicalChordRecognizer`
- Existing `ChordTemplateFactory` stays for Roman-numeral modal context (its legitimate job)

**Phase E — Post-refactor re-measurement** (pending)
- Re-run `VoicingAnalysisAudit` → expect consistency > 98%, Unknown < 5%
- Re-run `ix-embedding-diagnostics` → expect all non-MORPHOLOGY partitions < 40% leak
- Regenerate `optick.index`, re-run conformance tests

### Phase 6 — Rollback procedure

Every deliverable is a separate commit. No schema migrations. No index format changes. `git revert` sufficient at any phase.

## Octopus / multi-agent methodology

Beyond the refactor itself, we established a repeatable pattern for knowledge-heavy tasks:

- **Parallel dispatch** — 3 to 5 agents per wave, each with a focused deliverable
- **Legal guardrails as SendMessage** — mid-flight corrections when legal scope changed (no-scraping, no-copyrighted-coinages, CC-BY-SA-contamination avoidance)
- **Sandbox awareness** — when an agent hits shell restrictions (Forte catalog agent), it refused to fabricate output and returned BLOCKED with diagnostic context. We then took over directly.
- **Read-only audits before edits** — diagnose before prescribing. No write-mode agent dispatched until the read-mode audits completed.

## Token / cost methodology

For the executive report: each parallel-agent wave consumed ~100-200K tokens across 3-5 concurrent agents, but reduced wall time by ~70%. We traded token cost for time-to-insight — a good trade when diagnostic results gate downstream decisions.

The harness engineering trims earlier in the session (CLAUDE.md files 189 → 35, 279 → 57 lines across 4 repos) saved ~15K tokens per turn, which multiplied across 100+ turns in this session = ~1.5M tokens saved *before* we even started the refactor. **Harness engineering is upstream of any task ROI calculation.**

## Measurable outcomes so far

| Metric | Value |
|---|---|
| Lines of code added | ~5,000 (GA) + ~900 (IX) |
| New YAMLs | 12 tradition files + 4 systematic catalogs |
| Ground-truth anchors added | ~4,500 (4095 scales + 200 families + 97 world + 144 transforms) |
| Parallel agents dispatched | 15+ this session |
| Agent blockages (sandbox / guardrail) | 1 (Forte catalog — agent refused contaminated output) — CORRECT behavior |
| Embedding index size | 664 MB (v3) → 365 MB (v4) → 165 MB (v4+dedup) |
| Search latency (313k voicings) | 17.3 ms unfiltered / 220 µs per-instrument |
| Legal contamination risk | Zero (all guardrails honored, no scraping, no copyrighted text copied) |

## Template for future similar refactors

1. Survey capability + inventory catalogs (read-only)
2. Audit in parallel (corpus, taxonomy, research, config-surface)
3. Expand knowledge base (additive, zero risk)
4. Build baseline diagnostic (measure before touching)
5. Write two plans: tactical and architectural; pick one; document supersession
6. Execute in phases, each with own commit, acceptance gate, rollback
7. Re-measure against baseline, commit the comparison

This document will be updated as Phases C-E complete. Each phase commit references back to this methodology so the executive narrative stays coherent end-to-end.

## Links

- Architecture plan: `docs/plans/2026-04-17-chord-recognition-architecture-plan.md`
- Original tactical plan (superseded): `docs/plans/2026-04-17-fix-voicing-analysis-quality-plan.md`
- Corpus audit output: `state/audit/voicing-audit-2026-04-17.json`
- Baseline embedding diagnostics: `state/baseline/embedding-diagnostics-2026-04-17.json`
- Baseline cluster labels: `state/baseline/embedding-clusters-k50.json` (gitignored, regenerable)
- IX diagnostic tool: `crates/ix-embedding-diagnostics/` in ix repo
