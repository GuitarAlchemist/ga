# Backlog

Future ideas not yet in active planning. One bullet per idea. When an idea is ready to build, run `/feature` — it gets a brainstorm + plan in `docs/plans/`, then remove it from here.

See `CLAUDE.md` Planning & Backlog section for the full workflow.

---

## Chatbot & RAG

- **Chatbot orchestration extraction** — extract `ChatbotSessionOrchestrator` into `GA.Business.Core.Orchestration`; move all agent routing logic out of the API layer (plan: `docs/plans/2026-03-02-refactor-chatbot-orchestration-extraction-plan.md`)
- **Spectral RAG spike** — complete Phase 5 (Chat Orchestrator intent pipeline + constraint extraction); next sprint is Phase 23 (Performance & Distributed Inference); see `docs/archive/conductor/tracks/spectral-rag-chatbot/`
- **Chatbot streaming backpressure** — evaluate whether SSE chunking needs adaptive flushing under high concurrency (follow-on to the 3-slot concurrency gate added in March 2026)

## API Quality (from `.agent/api-team/BACKLOG.md`)

- **API-003** (P1) — Integration tests for ChatbotController `status` and `examples` endpoints
- **API-006** (P1) — Unify `ApiResponse<T>` — 8 services each have their own copy; consolidate to `AllProjects.ServiceDefaults`
- **API-008** (P2) — Audit `GA.MusicTheory.Service` (8 controllers) for `[ProducesResponseType]` completeness
- **API-009** (P2) — Audit `GA.Analytics.Service` (5 controllers) for `[ProducesResponseType]` completeness
- **API-010** (P2) — Audit `GA.AI.Service` (7 controllers) for `[ProducesResponseType]` completeness
- **API-011** (P2) — Integration tests for `GA.MusicTheory.Service` ChordsController
- **API-014** (P2) — Verify `ErrorHandlingMiddleware` is registered in all microservices; add centralized exception handling where missing
- **API-015 – API-018** (P3) — Audit `GA.Knowledge.Service`, `GA.Fretboard.Service`, `GA.BSP.Service`, `GA.DocumentProcessing.Service` for `[ProducesResponseType]` completeness
- **API-019** (P3) — Integration tests for HealthController
- **API-020** (P3) — API versioning strategy — document or implement `/api/v1/` prefix consistently

## Agent Infrastructure

- **Semantic event routing** — pub/sub architecture for agent-to-agent communication; proposed in `docs/archive/conductor/tracks/semantic-event-routing/`; no plan yet — needs spike
- **Agent marketplace MVP** — plugin marketplace where agents can be discovered and composed dynamically; spec was drafted but needs re-evaluation against current agent infrastructure
- **Fast voicing indexing (ILGPU batch pipeline)** — GPU-accelerated batch embedding pipeline for the voicing index; plan: `docs/plans/2026-03-05-feat-fast-voicing-ilgpu-batch-pipeline-plan.md`

## Domain & Infrastructure

- **Domain project structure cleanup** — follow-on from the March 2026 domain refactor; check for any remaining misplaced types
- **MEAI integration reconciliation** — Microsoft.Extensions.AI integration was "Needs Reconciliation" in conductor; check current state vs. `GA.Business.ML` ONNX/Ollama setup
- **Core schema design reconciliation** — `core-schema-design` conductor track was "Needs Reconciliation"; evaluate if remaining items apply

## Future / Not Started

- **Kubernetes deployment** — `k8s-deployment` conductor track; see `docs/archive/conductor/tracks/`; no active plan; needs re-evaluation (currently using Aspire for local orchestration)
- **Voice input** — guitar voice-to-tab or voice commands; flagged in Nov 2025 roadmap; no 2026 activity; needs re-evaluation before planning
- **Vision features** — image recognition for chord diagrams or notation; flagged in Nov 2025 roadmap; no 2026 activity; needs re-evaluation before planning
- **Tab ingestion pipeline improvements** — `.agent/skills/tab-ingestion/` skill exists; production-scale tab parsing and indexing not yet planned
