---
date: 2026-05-02
status: shipped 2026-05-02 (all 6 cleanup steps landed)
reversibility: deletes are recoverable from git; rename + frontmatter changes are reversible
revisit-trigger: any new chatbot surface gets added (a 6th place to "ask the chatbot") OR a roadmap doc is added without superseding the existing index
owners: needs assignment
---

> **Update 2026-05-02 — all 6 cleanup steps landed.** Summary at the bottom
> of this doc. Original plan + verdicts preserved below for context.

# Chatbot cleanup — inventory and proposed phases

The chatbot lives in 5 places across `ga` plus 1 place in `ix`, with 4 open
TODOs, 3 separate roadmap docs, and 2 misnamed TODO files. Before any cleanup
deletes anything, this doc establishes the canonical-vs-dead map so the next
phases work from facts.

## Surface verdict

| # | Surface | Last touched | Verdict |
|---|---|---|---|
| 1 | `Apps/ga-server/GaApi/` (Controller + Hub + SessionOrchestrator + React `components/Chat/` + `chatService.ts`) | 2026-03-24 (PR #59) | **Canonical** — production HTTP + SignalR + UI |
| 2 | `Apps/GaChatbotCli/` | 2026-03-14 | **Active utility** — CLI wrapper over `IHarmonicChatOrchestrator`, supports `--json` and interactive |
| 3 | `Apps/GaChatbot/` (console) | 2026-03-24 | **Secondary** — interactive console for local dev/testing, not a service |
| 4 | `Experiments/ChatbotExample1/` (Blazor) | 2026-03-02 | **Experimental reference** — uses GitHub Models, isolated from main stack |
| 5 | `Apps/GuitarAlchemistChatbot/` | 2026-01-11 | **DEAD** — directory only contains `obj/`; test `.csproj` literally says "app removed" |
| 6 | `ix/crates/ga-chatbot/` | 2026-04-XX | **Misnamed but legitimate** — Rust QA harness + MCP bridge using `ix-sanitize`/`ix-bracelet`/`ix-game`; only `ix-quality-trend/bootstrap.rs` consumes it inside ix |
| 7 | `Apps/ga-dashboard/src/app/chat/chat.component.ts` (Angular) | unknown | **Stale shim** — calls `localhost:7001/api/chat` (different shape from canonical `/api/chatbot/*`) |

## Open TODO status

The 4 chatbot TODOs in `ga/todos/` — note 2 of them are misnamed:

| File | Filename status | Frontmatter status | Reality |
|---|---|---|---|
| `002-complete-p2-chatbothub-no-concurrency-gate.md` | complete | complete | Done — Hub now has `SemaphoreSlim` + 25s budget |
| `010-complete-p1-gachatbot-stale-types.md` | complete | complete | Done — stale type files in `Apps/GaChatbot/` removed |
| `010-wontfix-p2-chatbotsessionorchestrator-dead-registration.md` | wontfix | **pending** | Filename lies — DI cleanup of `ChatbotSessionOrchestrator` still open |
| `013-complete-p2-no-mcp-tool-for-chatbot.md` | complete | **pending** | Filename lies — no MCP tool yet exposes `ProductionOrchestrator` |

**Two pending items, two filename lies.** The filename lies are a small mess
in themselves — anyone scanning `ls todos/*chatbot*` thinks all 4 are done
when 2 are still open.

## Roadmap docs (3 of them, in 3 different directories)

| Doc | Location |
|---|---|
| `implementation_plan_chatbot_rewrite.md` | `ga/docs/` |
| `2026-03-02-feat-functional-chatbot-agentic-routing-plan.md` | `ga/docs/plans/` |
| `Chatbot_Technical_Roadmap.md` + `Chatbot_Backlog.md` | `ga/Common/GA.Business.ML/Documentation/Architecture/` |

Plus a fourth, on the Demerzel side:
- `governance/demerzel/docs/superpowers/plans/2026-03-17-live-mcp-chatbot.md`
- `governance/demerzel/docs/superpowers/specs/2026-03-17-live-mcp-chatbot-design.md`

These have not been read in this pass — that happens in step 2 below. The
plain question for step 2 will be: which one is current, and what becomes
of the other two (archive, merge, delete).

## Cleanup phases, ordered low-risk → high-risk

### Step 2 — Consolidate roadmap docs (two-way door)

- Read all 4 roadmap docs in parallel
- Determine which one is current (likely the most recent, but verify by
  checking which one PR #59 references)
- Produce a single `ga/docs/chatbot/README.md` that captures the current
  plan + status + open questions
- Move superseded docs to `ga/docs/parked/` with a one-line "superseded by
  X" header
- Effort: ~1h, low risk, fully reversible from git

### Step 3 — Resolve the 2 pending TODOs (two-way door)

- `010-wontfix-p2-chatbotsessionorchestrator-dead-registration.md` — pick
  Option A (delete the DI registration) or Option B (rename to
  `DirectOllamaChatOrchestrator` and document); single-line change either way
- `013-complete-p2-no-mcp-tool-for-chatbot.md` — depends on todo 007
  (non-streaming endpoint); if 007 is done, add `GaMcpServer/Tools/ChatTool.cs`
  and register in `mcp-servers/augment-settings-complete.json`
- **Plus**: rename the 2 misnamed TODO files so filename status matches
  frontmatter. Trivial mv.
- Effort: ~2h total, low risk

### Step 4 — Decide the fate of the secondary surfaces (one-way door)

Three secondary surfaces need a verdict:

- `Apps/GaChatbot/` (console) — keep for local dev OR move to
  `Experiments/`?
- `Apps/GaChatbotCli/` (CLI) — keep as production utility OR delete?
- `Experiments/ChatbotExample1/` (Blazor) — keep as reference OR delete?

These deletes are recoverable from git but break any external scripts that
invoke them. Need explicit sign-off per surface.

### Step 5 — Delete the dead surface (one-way door)

- `Apps/GuitarAlchemistChatbot/` — directory has only `obj/`, but the
  `.csproj` file may still exist + the solution file may still reference it
  + `Tests/Apps/GuitarAlchemistChatbot.Tests/` exists with a .csproj that
  literally documents "app removed."
- Need to: remove `.csproj`, remove from `.sln`, remove the test project,
  and run a clean build to confirm no broken references.
- Effort: ~30 min, single PR, low real risk because the app is already gone

### Step 6 — Decide what to do about `ix/crates/ga-chatbot/` (two-way door)

Two options, both small:

- **Option A**: Rename the crate to `ix-chatbot-harness` so the IX/GA
  boundary is obvious. Mechanical update to `Cargo.toml`, the IX workspace
  manifest, and `ix-quality-trend/bootstrap.rs`. Workspace-internal name —
  no published consumers.
- **Option B**: Keep the name, add a 5-line comment at the top of `lib.rs`
  explaining "this crate tests the GA chatbot using IX primitives; the
  production chatbot lives in ga, not here." Single-file edit.

Option B is cheaper. Option A is more honest. Default recommendation: B.

### Step 7 — Clean up the Angular dashboard chat shim (two-way door)

- `Apps/ga-dashboard/src/app/chat/chat.component.ts` calls
  `localhost:7001/api/chat` with a request shape that doesn't match the
  canonical `/api/chatbot/*` endpoints
- Either point it at the canonical API OR delete it (the dashboard may not
  even use this component anymore)
- Need to grep dashboard routes/templates to confirm whether it's wired in

## What this inventory does NOT cover

- The actual *quality* of the canonical surface (Controller / Hub /
  Orchestrator). The "hard to govern" pain may be inside there. That's a
  separate read once we agree on canonical.
- Whether to adopt external observability tooling (LangFuse, Promptfoo,
  Helicone). Premature until the surface count drops.
- The Demerzel side of the chatbot governance contract (the 2026-03-17 docs).

## Decision points for the next message

Pick the steps you want me to do, and in what order:

- [x] Step 2 (consolidate roadmaps) — moved 3 docs, created `docs/chatbot/README.md` index
- [x] Step 3 (resolve 2 TODOs + rename 2 misnamed files) — re-investigation showed the *filenames* were correct; updated frontmatter + bodies of 010-wontfix and 013-complete to match
- [x] Step 4 (decide fate of secondary surfaces) — kept GaChatbot console + GaChatbotCli, deleted ChatbotExample1
- [x] Step 5 (delete dead GuitarAlchemistChatbot project + test) — `git rm`'d the orphaned test project, deleted local empty dirs
- [x] Step 6 (rename or comment ix-side crate) — added intent comment in `ix/crates/ga-chatbot/src/lib.rs`
- [x] Step 7 (fix or delete Angular dashboard chat shim) — deleted `Apps/ga-dashboard/src/app/chat/` and removed the `/chat` route

## What landed (2026-05-02)

### Files deleted
- `Apps/GuitarAlchemistChatbot/` (only `obj/` left; no tracked files)
- `Tests/Apps/GuitarAlchemistChatbot.Tests/` (orphaned: not in any `.slnx`, broken `ProjectReference`, only test source was already excluded)
- `Experiments/ChatbotExample1/` (Blazor sandbox using GitHub Models; not on canonical path)
- `Apps/ga-dashboard/src/app/chat/` (Angular component calling `localhost:7001/api/chat` — endpoint isn't even running)

### Files moved (history preserved via `git mv`)
- `Common/GA.Business.ML/Documentation/Architecture/Chatbot_Technical_Roadmap.md` → `docs/chatbot/Chatbot_Technical_Roadmap.md`
- `Common/GA.Business.ML/Documentation/Architecture/Chatbot_Backlog.md` → `docs/chatbot/Chatbot_Backlog.md`
- `docs/implementation_plan_chatbot_rewrite.md` → `docs/archive/architecture-pre-2026-04/implementation_plan_chatbot_rewrite.md`

### Files edited
- `AllProjects.slnx` — removed `Experiments/ChatbotExample1/ChatbotExample1.csproj` reference
- `Apps/ga-dashboard/src/app/app.routes.ts` — removed `/chat` route
- `docs/architecture/chat-surfaces.md` — added a note pointing to this cleanup; flagged stale AppHost open question
- `docs/plans/2026-03-08-feat-ag-ui-protocol-integration-plan.md` — updated stale doc path
- `Scripts/health-check.ps1` — removed stale `GuitarAlchemistChatbot` line from docstring
- `Scripts/reorganize-solution-final.ps1` — replaced `GuitarAlchemistChatbot` echo with `GaChatbot`
- `todos/010-wontfix-p2-chatbotsessionorchestrator-dead-registration.md` — frontmatter `pending` → `wontfix`; added Resolution note (NormalizeHistory still in production use)
- `todos/013-complete-p2-no-mcp-tool-for-chatbot.md` — frontmatter `pending` → `complete`; verified `GaMcpServer/Tools/ChatTool.cs` exists and is correct
- `ix/crates/ga-chatbot/src/lib.rs` — added crate-level disambiguation comment

### Files created
- `docs/chatbot/README.md` — single landing page; points at `chat-surfaces.md` for live architecture

### Files NOT touched
- `Apps/GaChatbot/` (kept — local-dev REPL, active 2026-03-24)
- `Apps/GaChatbotCli/` (kept — production CLI, active 2026-03-14)
- `governance/demerzel/docs/superpowers/plans/2026-03-17-live-mcp-chatbot.md` (different chatbot — GitHub discussions bot)
- `ix/crates/ga-chatbot/` was *not* renamed; the comment is the lighter touch (Option B)

### Net surface count: 7 → 4
Before: 7 chatbot-name surfaces. After: 4 (canonical NebulaChat/AgUi/legacy in GaApi + 2 console hosts + 1 Rust harness).
