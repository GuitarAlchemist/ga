---
date: 2026-05-12
status: issue-ready cleanup tracker
reversibility: two-way
owners: needs assignment
audience: GA chatbot maintainers
---

# Chatbot Cleanup Tracker

This tracker converts the documentation sync findings into issue-ready work.
No runtime behavior changes are implied by this document.

## 1. Remove Or Implement `/api/chatbot/ask` — resolved 2026-05-12

**GitHub:** https://github.com/GuitarAlchemist/ga/issues/202

**Resolution:** `TriageDropZone.tsx` no longer calls `POST /api/chatbot/ask`.
Its best-effort summary helper now calls `POST /api/chatbot/chat` and falls
back to `POST /api/nebula/chat`.

**Original problem:** `TriageDropZone.tsx` called `POST /api/chatbot/ask`, but
no matching server endpoint was found under `Apps/`.

**Evidence:** grep on 2026-05-12 found the caller at
`ReactComponents/ga-react-components/src/components/PrimeRadiant/TriageDropZone.tsx`
and no controller/route implementation.

**Decision options:**

- Add a real GaApi route if Prime Radiant still needs this action.
- Remove or retarget the frontend caller if the workflow is obsolete.

**Done when:** the caller either reaches a tested endpoint or is removed.

## 2. Decide Fate Of Legacy REST/SSE Chatbot Routes

**GitHub:** https://github.com/GuitarAlchemist/ga/issues/204

**Problem:** `/api/chatbot/chat` and `/api/chatbot/chat/stream` remain useful
parallel surfaces, but AG-UI and SignalR are the active public paths.

**Decision options:**

- Keep them as supported API surfaces and document wire parity expectations.
- Deprecate them and move remaining callers to AG-UI or SignalR.

**Done when:** `chat-surfaces.md` lists explicit support status and caller
inventory for each route.

## 3. Clean Up `ChatbotSessionOrchestrator`

**GitHub:** https://github.com/GuitarAlchemist/ga/issues/205

**Problem:** `NormalizeHistory` is used by `ChatbotHub`, but
`GetResponseAsync` / `StreamResponseAsync` are registered and not on the
canonical request path.

**Decision options:**

- Delete unused response methods and keep only history normalization.
- Move history normalization into the hub/application-service path and delete
  the class.
- Keep the methods if a new consumer is named and tested.

**Done when:** dead-code-adjacent methods are removed or have a documented
consumer.

## 4. Decide `GaChatbot.Api` Host Future

**GitHub:** https://github.com/GuitarAlchemist/ga/issues/203

**Problem:** `GaChatbot.Api` compiles and has useful patterns, but the public
demo is served through GaApi SignalR.

**Decision options:**

- Promote `GaChatbot.Api` with AppHost + ingress + SPA route changes.
- Keep it frozen as a reference host.
- Delete it after porting any remaining useful code to GaApi/common
  orchestration.

**Done when:** the decision is recorded in `chat-surfaces.md` and the codebase
matches it.

## 5. Reconcile Startup / Script References To Removed Chatbot Names — resolved 2026-05-12

**GitHub:** https://github.com/GuitarAlchemist/ga/issues/206

**Resolution:** `docker-compose.yml` now builds `gaapi` from
`Apps/ga-server/GaApi/Dockerfile` and no longer defines the removed
`GuitarAlchemistChatbot` service. `Scripts/start-all.ps1` now builds
`AllProjects.slnx` and describes the public chatbot demo as served by GaApi.
`Scripts/health-check.ps1` now checks the GaApi-hosted `/chatbot/` demo instead
of a deleted standalone Blazor service on port 7002.

**Problem:** scripts and Docker Compose still reference historical
`GuitarAlchemistChatbot` / `GA.AI.Service` names.

**Evidence:** grep on 2026-05-12 found references in `docker-compose.yml` and
several scripts under `Scripts/`.

**Decision options:**

- Remove stale references.
- Mark scripts as historical.
- Update scripts to use the current GaApi / GaChatbot.Api story.

**Done when:** startup docs/scripts no longer imply deleted or frozen chatbot
services are current defaults.
