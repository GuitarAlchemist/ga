---
status: accepted
date: 2026-06-22
---

# GaApi is the single canonical chat host — GaChatbot.Api and GA.AI.Service are retired

## Context

GA accreted **three** chat-host services, and `docs/architecture/chat-surfaces.md`
left the canonical choice **open** (§5b, lines 213–223, three paths A/B/C). The
2026-06-21 architecture-deepening review found that five slices (#1 chat intake
seam, #4 readiness/fallback seam, #6 delete `GA.AI.Service`, #8a SSE-framing
seam, #8b dead `ChatbotSessionOrchestrator` methods) **all gate on one question:
is GaApi the single canonical chat host?** The freeze docs explicitly refuse to
act until it is answered:

- `Apps/ga-server/GA.AI.Service/DEPRECATED.md` — delete only once the canonical
  surface is chosen; coupled to the GaChatbot.Api freeze-flip ("removed in the
  same pass"). Frozen 2026-05-07 per a Codex CLI review.
- `docs/architecture/audit-2026-04-25.md:69` — `ChatbotSessionOrchestrator`
  marked **CONSOLIDATE**, not delete, "pending clarification of role vs
  NebulaSidekickService."
- `docs/architecture/chat-surfaces.md:363` — the dead `GetResponseAsync` /
  `StreamResponseAsync` methods left as "remove vs keep for an upcoming surface?"

Current deployed state at decision time:

| Host | Aspire AppHost | Deployed reality |
|---|---|---|
| **GaApi** (5232) | ✅ active (`AllProjects.AppHost/Program.cs:131`) | SignalR `ChatbotHub` is the de-facto deployed hub; REST `/api/chatbot/*` reachable only via `localhost` |
| **GaChatbot.Api** (5252) | ❌ not registered | **what cloudflared actually serves** at `demos.guitaralchemist.com/chatbot/`; richer response shapes (Grounding, Trace, readiness probe) |
| **GA.AI.Service** (7003) | 💤 commented out (`Program.cs:100`) | broken DI, no consumers, frozen |

## Decision

**GaApi is the single canonical chat host.** GaChatbot.Api and GA.AI.Service are
retired. The shared chat plumbing (validate → concurrency-gate → session-cookie
→ orchestrate, plus readiness probe + bounded-timeout + low-confidence fallback +
SSE framing) lives in `Common/GA.Business.Core.Orchestration` behind one seam (#1), and GaApi's
transports become thin adapters.

## Why (the trade-off)

- **One concept, worn four times.** The same intake plumbing is copy-pasted
  across GaApi REST/SSE, the SignalR hub, AG-UI, and GaChatbot REST. The seam is
  missing, not the behaviour — collapsing onto one host makes the seam (#1) the
  natural home and turns #4/#8a into decorators on it.
- **GaApi is already the platform host** (Mongo, Redis, GraphQL, Nebula,
  music-theory service, SignalR). Splitting chat onto a second deployable
  (paths A/B) keeps two session/gating implementations in sync forever.
- **GaChatbot.Api's value is its richer shapes, not its separateness.** Grounding,
  Trace, and the readiness probe are features to *fold into* GaApi via the shared
  seam — not reasons to keep a parallel host.

## Consequences — migration ordering is load-bearing

cloudflared serves the **public demo** from GaChatbot.Api today. Therefore
**GaChatbot.Api is retired LAST**, only after GaApi reaches parity. Sequence:

1. **#6 delete `GA.AI.Service`** — now unambiguously safe (C confirms no separate
   AI host). Remove dir + `AllProjects.slnx` entry + commented AppHost lines +
   the orphaned `ai-cluster`/`ai-service` routes in
   `Apps/ga-server/GaApi/appsettings.ReverseProxy.json:114-134,350-356`. CI build.
2. **#8b delete dead `ChatbotSessionOrchestrator` methods** — `GetResponseAsync` /
   `StreamResponseAsync` have no callers; keep `NormalizeHistory`. CI build.
3. **#1 chat intake seam** in `Common/GA.Business.Core.Orchestration`; route GaApi through it
   (tracer-bullet: one transport end-to-end, then the rest).
4. **#4 + #8a** — fold GaChatbot's readiness/fallback + CRLF SSE fix into the
   seam, so GaApi *gains parity* (incl. the markdown-table-truncation fix).
5. **Re-point cloudflared** `/chatbot/` (and `/api/chatbot/*`) to GaApi; verify
   the live demo against GaApi.
6. **Retire GaChatbot.Api** — only once 3–5 are verified in deployment.

Reversibility: steps 1–4 are in-repo refactors/deletions gated by CI build. Step
5 (cloudflared) is the one operational one-way-ish action — verify the demo
end-to-end before step 6 deletes the old host.

## Related

- Supersedes the open question in `docs/architecture/chat-surfaces.md` §5b — that
  file's canonical declaration is updated to point at GaApi.
- Unblocks campaign slices #1, #4, #6, #8a, #8b
  (`docs/plans/2026-06-21-arch-deepening-campaign-plan.md`).
- Freeze docs released: `GA.AI.Service/DEPRECATED.md`,
  `docs/architecture/audit-2026-04-25.md:69`.
