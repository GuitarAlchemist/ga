# JEPA / OPTICK issue cluster — triage and sequencing

**Status: triage complete, 2026-07-28.** Disposition of the seven-issue JEPA cluster filed 2026-07-25/26 (ga#605, #606, #607, #610, #611, #612 + GuitarAlchemist/tars#212). No model is trained, no contract is frozen, no code changes beyond the one producer fix filed as its own issue.

Type: research (triage + sequencing). Reversibility: two-way — labels, comments, and this document only. The one-way doors (OPTIC-K dimensions, index schema hash) are explicitly **not** touched.

---

## 1. Why this triage exists

Two problems, both organisational rather than technical:

1. **The cluster was orphaned.** Six ga issues were filed 2026-07-26 and referenced from nowhere in the repository — neither `BACKLOG.md` (which already carries epic **M4 — Progression-JEPA** and epic **J4 — learned predictive world model**) nor `docs/plans/2026-07-04-feat-spectral-music-track-groundwork-plan.md` (which already states the organising insight: *OPTIC-K is already the frozen encoder the JEPA recipe requires*). Two parallel descriptions of the same ambition, one in the backlog and one in the tracker.
2. **All six sat on `needs-triage`** even after a read-only spike had already answered the central question.

## 2. The spike already ran — the verdict stands

The read-only spike campaign on ga#605 (comments dated 2026-07-28, live-queried against `state/voicings/optick.index`: 313,047 voicings, schema hash `0x37CD8ECF`, compact dim 124) returned:

> **VERDICT: NOT-YET — confidence 0.78.** A deferral with named, checkable conditions, not a decline.

The two measurements that drive every disposition below:

| Measurement | Consequence |
|---|---|
| `CONTEXT` partition measured empty — 11 of 12 dims dead, the live one constant across samples — while carrying similarity weight **0.20** (`EmbeddingSchema.cs:122`) | A fifth of the similarity weight is spent on a constant. Any JEPA would be trained against a representation that is a fifth blind. |
| 40 of 124 compact dims identically zero | A Jacobian over OPTIC-K is structurally rank-deficient by ~⅓ *before* analysis starts, so ga#606 would trivially "confirm" collapsed dimensions rather than discover anything. |

**This triage does not re-litigate the verdict.** It records dispositions consistent with it and closes the gaps the spike left open (labels, backlog linkage, the unfiled producer fix).

## 3. Dependency graph (authoritative)

```text
ga#610  general musical state/action/observation/transition/goal contracts
   └─> ga#612  contact-centric physical-performance contracts
          └─> ga#605  latent world model / JEPA spike        ga#611  COCONUT latent planning
                 └─> ga#606  Jacobian geometry of the latent space

ga#607  Grothendieck / categorical constructions   — parallel, feeds #605 (path equivalences, hard negatives)
tars#212  agentic JEPA                             — blocked by ga#507, NOT by ga#605 (corrected by the spike)
```

`ga#610` is the schema authority; `ga#612` specialises physical execution beneath it. `tars#212` shares the *pattern* only — agent-delivery state is a separate domain and must not reuse the musical contracts.

## 4. Dispositions applied (2026-07-28)

`needs-triage` removed from all six ga issues; canonical role labels per `docs/agents/triage-labels.md`.

| Issue | Label | Disposition | Revisit trigger |
|---|---|---|---|
| **ga#605** JEPA world models (P1) | `ready-for-human` | **Deferred, stays open.** NOT-YET is evidence-backed; reviving it is a human go/no-go, not agent work. | ga#616 landed (§5) **and** the honest deterministic baseline measured on the refilled index. |
| **ga#610** state/action/goal contracts (P1, L) | `ready-for-human` | **Narrow before starting.** The full contract catalogue (six sub-states, ten action families, invariant catalogue) is speculative scaffolding while its only consumer is deferred — Karpathy rule 2. One slice is worth doing independently: reconcile the `MusicalTransition` record with the **already-shipped** typed transition logging of ga#507 instead of redefining it. | Scope decision by the owner; or ga#605 revived. |
| **ga#612** contact-centric physical contracts (P1, XL) | `ready-for-human` | **Parked behind #610.** XL, and it carries the review-gated parts: biomechanical assumptions, medical-claim safeguards, consent/licensing for media capture. Not agent-safe unattended. | ga#610 narrowed and landed. |
| **ga#606** Jacobian geometry (P2) | `ready-for-human` | **Parked — hard technical blocker.** Rank deficiency (§2) makes the analysis uninformative regardless of #605. | ≥90% of compact dims carrying variance, i.e. after ga#616 and a dead-dim audit. |
| **ga#611** COCONUT latent planning (P2) | `ready-for-human` | **Parked — premise unmeasured.** Its entire value proposition is cutting explicit-reasoning cost, and the baseline cost of the existing deterministic planner has never been measured. Cheapest honest first step needs no model at all. | The deterministic planner's latency/compute measured and recorded under `state/quality/`. |
| **ga#607** Grothendieck / categorical (P3) | `ready-for-human` | **Genuinely actionable, but human-initiated.** No dependency on any trained model; bounded F# prototype + property tests over existing GA theory data, with an explicit stop/go. Initially labelled `ready-for-agent` — reverted, see §7. | — |
| **tars#212** agentic JEPA (P2) | — (tars uses no triage labels) | **Already corrected by the spike**: `not_ready` retained, blocker restated as ga#507. No further action; cross-linked to this document. | ga#507 transition corpus usable. |

## 5. The actual next move — and it is not a model

The spike's one unactioned recommendation, now filed as **ga#616**: **fill the `CONTEXT` partition.**

Root cause, confirmed by reading the producer:

- `Common/GA.Business.ML/Embeddings/MusicalEmbeddingGenerator.cs:99-104` passes `stabilityDelta: 0.0` and `isResolution: false` as literals, and `harmonicFunction: doc.HarmonicFunction` which is absent for statically indexed voicings. Only `tension: 1.0 - doc.Consonance` is fed.
- `Common/GA.Business.ML/Embeddings/Services/ContextVectorService.cs` writes slots 0–5 and leaves **6–11 "Reserved for Key Relationship (Circle of Fifths distance, etc)"** — unwritten since v1.1. Its own doc comment concedes: *"For static indexing of voicings, this may be largely zero or generic."*

Why this ranks above every issue in the cluster: it is a **producer fix, cheaper than any model**, it improves every existing consumer (similarity search, RAG retrieval, the SAE) at once, and it builds the honest baseline a future JEPA would have to beat. Per rule 6 it is a metric-moving change and therefore declares baseline + direction + guardrail; the partition layout and weights are untouched, so **no re-index of the schema hash is required** — only a corpus rebuild.

ga#616 is labelled `bug` + `ready-for-agent` — the only issue deliberately left in the agent lane (see §7).

## 5b. Backlog linkage (orphan problem closed)

`BACKLOG.md` now cross-links the cluster from the two epics that already described the same ambition:

- **M4 — Progression-JEPA** carries the six ga issues plus ga#616 as the prerequisite that outranks them.
- **J4 — learned predictive world model** carries `tars#212` with its corrected ga#507 blocker.

Every future reader arrives at the tracker through the backlog, not in parallel with it.

## 6. What this triage explicitly did not do

- No contract was drafted, no schema frozen, no dimension changed.
- The NOT-YET verdict was not re-derived — it was taken as evidence and applied.
- The spike report itself still lives only as GitHub comments on ga#605; promoting it into `docs/research/` per the protocol in `docs/research/README.md` remains open.

## 7. Incident during this triage — `ready-for-agent` is a trigger, not a description

Applying `ready-for-agent` to ga#607 fired `.github/workflows/jules-auto-delegate.yml`, which added the `jules` label within seconds and dispatched a paid agent run on a P3 research spike — with no cost decision. Both labels were removed and ga#607 is now `ready-for-human`; a correction comment is on the issue.

The revert was too late: the run had already completed and opened **PR #617** (`research/grothendieck-optick-…`). It is unreviewed agent output on a spike whose own acceptance criteria demand an explicit stop/go decision and preserved counterexamples — evaluate it against those criteria, do not merge it as a fix. The intended dispatch, ga#616, produced **PR #618** (`fix/optick-context-partition-…`), which must be checked against the §5 guardrail (dead-dim count down, retrieval quality not worse, schema hash unchanged) before merge.

Lesson for future triage in this repo: in `docs/agents/triage-labels.md` the label reads as a readiness statement, but mechanically it is an **execution trigger** (with a dead-letter sweep that re-routes anything that escaped). Open-ended research spikes must not carry it, however well-specified they are. Only ga#616 — a bounded producer fix with an explicit guardrail — was left in that lane, deliberately.
