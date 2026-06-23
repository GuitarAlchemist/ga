# Architecture Deepening Campaign — 2026-06-21

> Origin: `/improve-codebase-architecture` review (3 Explore agents across layers 1–5),
> grounded in `CONTEXT.md` + ADR-0001…0004. Full visual report was generated to the OS
> temp dir (`architecture-review-20260621-*.html`). User said **"all"** — execute every
> candidate as its own **vertical slice / PR** (tracer-bullet discipline, CLAUDE.md), in
> risk + dependency order. Not one mega-refactor.

## Vocabulary
- **Deep module** = lots of behaviour behind a small interface (high *leverage*); **shallow** = interface ≈ implementation.
- **Seam** = where an interface lives; **deletion test** = delete it → complexity *vanishes* (pass-through) vs *concentrates across callers* (earns keep).

## Dropped (do not re-suggest)
- Facade over `MusicalVoicingAnalysis` — **ADR-0004 (2026-06-21)** decided it stays wide. Re-suggesting a same-day decision is out of bounds.

## ✅ Gating decision RESOLVED (2026-06-22) → ADR-0005

The chat cluster (#1, #4, #6, #8a, #8b) was gated on one question — *is GaApi the single
**canonical chat host**?* **Resolved: yes (Option C).** See
[`docs/adr/0005-gaapi-single-canonical-chat-host.md`](../adr/0005-gaapi-single-canonical-chat-host.md).
GaChatbot.Api and GA.AI.Service are retired; their richer shapes (Grounding / Trace /
readiness) + the CRLF SSE fix fold into GaApi via the shared seam (#1).

**Migration ordering is load-bearing — cloudflared serves the public demo from GaChatbot.Api
today, so it is deleted *last*:**

1. **#6** delete `GA.AI.Service` — now unambiguously safe. Also strip the orphaned
   `ai-cluster`/`ai-service` routes in `appsettings.ReverseProxy.json:114-134,350-356`.
2. **#8b** delete dead `ChatbotSessionOrchestrator` methods (keep `NormalizeHistory`).
3. **#1** chat intake seam in `Common/…Orchestration`; route GaApi through it (tracer-bullet).
4. **#4 + #8a** fold GaChatbot's readiness/fallback + CRLF SSE fix into the seam (GaApi parity).
5. **Re-point cloudflared** `/chatbot/` + `/api/chatbot/*` → GaApi; verify the live demo.
6. **Retire GaChatbot.Api** — only after 3–5 are verified in deployment.

The **domain-core candidates (#2, #3, #5, #7, #9) remain independent** and can proceed any time.

## Sequence (revised — chat cluster gated)

| Order | Slice | Strength | Risk | Depends on | Verify |
|---|---|---|---|---|---|
| 1 | **#6** Delete `GA.AI.Service` | Strong (delete) | low — no refs | — | CI build |
| 2 | **#8b** Delete dead `ChatbotSessionOrchestrator` methods | Worth | low — no callers | — | CI build (zero-warnings) |
| 3 | **#1** Chat intake seam (keystone) | Strong | med — 4 transports | — | dev stack down + tests |
| 4 | **#4** Shared readiness/fallback seam | Strong | med | #1 | tests |
| 5 | **#8a** One SSE-framing seam (fixes CRLF bug) | Worth | low-med | #1 | tests |
| 6 | **#2** Agent prompt/parse engine | Strong | med — tribunal gate | — | tests + Demerzel tribunal |
| 7 | **#3** Seal chord-identity dual-mode | Strong | med-high — ~40 call sites | — | tests |
| 8 | **#5** Bind embedding schema into search adapters | Strong | med — schema-adjacent | — | tests + leak/invariant tools |
| 9 | **#7** `VoicingAnalyzer` strategy + kill silent stubs | Worth | med | — | tests |
| 10 | **#9** Unify set-class indexes | Speculative | — | decide first | maybe close as ADR |

Rationale for order: bank the two **pure deletions** first (safe, CI-verifiable). Then the
**keystone #1**, which turns #4 into "add one decorator to the seam" and #8a into "the
framing layer of each adapter," and makes #6's safety self-evident. The domain-core
refactors (#2, #3, #5, #7) are independent and can interleave; each needs design grilling
per the skill before implementation.

## Per-slice details

### #6 — delete `GA.AI.Service` (in flight)
Body is `await orchestrator.AnswerAsync(request)`; Aspire registration commented out; in
`AllProjects.slnx` but referenced by nothing. Remove dir + slnx entry + commented AppHost
lines. If a headless AI host is ever wanted, rebuild with a real contract (batch / A-B /
eval), not a copy of GaApi.

### #8b — dead `ChatbotSessionOrchestrator` methods
`GetResponseAsync` / `StreamResponseAsync` have no callers; only `NormalizeHistory` is live
(sole caller `ChatbotHub`). Remove the two methods + any now-unused private helpers/ctor
deps (zero-warnings). Optionally relocate `NormalizeHistory` to a static util and shrink the
hub's constructor.

### #1 — chat intake seam (keystone)
Concentrate validate → concurrency-gate → session-cookie → orchestrate behind one module;
transports (GaApi REST/SSE, SignalR hub, AG-UI, GaChatbot REST) become thin adapters that
only frame the result. Tracer-bullet: route ONE transport through the seam end-to-end, test,
then migrate the rest. Grounded by `docs/architecture/chat-surfaces.md` (canonical vs dead).

### #4 — shared readiness/fallback seam
Move `OrchestratedChatApplicationService`'s readiness-probe + bounded-timeout +
low-confidence fallback into `Common/…Orchestration`; both hosts resolve it so the public
GaApi demo gains Ollama-wedge resilience it currently lacks.

### #8a — one SSE-framing seam
Replace copy-pasted `WriteSseLineAsync`; carry the GaChatbot CRLF fix (fixes GaApi
markdown-table truncation). AG-UI writer delegates low-level framing.

### #2 — agent prompt/parse engine
One deep engine owns prompt-build + structured-response parse; the 5 agents become specs.
**Demerzel tribunal gate** applies (touches `GA.Business.ML/Agents`).

### #3 — seal chord-identity dual-mode
`ChordIdentification` always-canonical at construction; retire legacy fields or push behind
one adapter; kill the `HasCanonicalIdentity` branch across callers. Respects *Chord
recognizer = PC-set-only*.

### #5 — bind embedding schema into search adapters
Inject `EmbeddingSchema` partitions into Optick/CPU/GPU adapters; validate at construction
(fail fast on drift) instead of hardcoded OPTIC-K version/offset constants. Respects ADR-0002
(filter parity unchanged). Schema is a one-way door — instrument per Karpathy rule 6.

### #7 — `VoicingAnalyzer` strategy
Static 214-line orchestrator → strategy(ies) owning invariants; `"Unknown"` placeholders
become fail-fast when the feature is enabled; minimal-vs-full becomes an explicit choice.
Distinct from ADR-0004 (that's about the record's width; this is about silent stubs).

### #9 — set-class indexes (speculative)
`SetClassOpticIndex` (voice-leading) vs `SetClassSpectralIndex` (L1 spectral) — near-identical
APIs, no shared seam. Only unify if the two notions *should* be one behind a metric-by-context
seam; they may be intentionally distinct → possibly close as an ADR instead.

## Reversibility / one-way doors
- #5 touches the OPTIC-K schema neighbourhood — coordinate, instrument, no dimension change.
- #2 touches `GA.Business.ML/Agents` — Demerzel tribunal gate.
- All others are reversible refactors/deletions; CI build is the authoritative gate
  (the revived `.githooks/pre-commit` build step skips when the dev stack is running).
