---
title: Harness engineering adoption — map industry techniques to GA's multi-agent stack
created: 2026-05-23
owner: spareilleux
status: draft
revisit_trigger: when the next agent-coordination friction surfaces (Codex bin-lock contention, agent stepping on agent, repeated context loss across sessions) OR after one of the referenced specs hits a 1.0
reversibility: each technique is independent; partial adoption is fine. The plan grades current state per technique so we can move pieces independently.
---

# Harness engineering adoption

## Problem

The session of 2026-05-23 made the multi-agent reality concrete: Claude, Codex, and Antigravity edited the same `ga` repo in parallel for hours. The friction wasn't about who was smarter — it was about **harness gaps**:

- Codex started a Vite from `Apps/ga-client` and stole port 5176 (fixed in #289 after ~25 min of public outage).
- The squash-merged PR #288 needed a worktree-side merge because Codex's 155 uncommitted files blocked a normal pull.
- GaApi PID 21036 ran elevated; no agent in this PowerShell context could kill it to unblock the AppHost build.
- Three pre-existing security findings (\`/dev-data/*\` exposure, \`/pr/*\` SSE bus, \`.mcp.json\` env leak) had been waiting in the codebase for weeks because no agent ran a structured security pass over the diff before merging.

These are all "the prompt was fine, the harness wasn't." This plan grades GA's current harness against the canonical industry techniques (from Anthropic, OpenAI, LangChain, Martin Fowler, walkinglabs/awesome-harness-engineering, [pablomarin/claude-codex-forge](https://github.com/pablomarin/claude-codex-forge), and the Reddit /r/ClaudeAI synthesis) and lists what's worth adopting.

## Reference index (so we don't re-research)

- [Anthropic — Effective Harnesses for Long-Running Agents](https://www.anthropic.com/engineering/effective-harnesses-for-long-running-agents)
- [Anthropic — Harness Design for Long-Running Application Development](https://www.anthropic.com/engineering/harness-design-long-running-apps)
- [OpenAI — Harness Engineering: Leveraging Codex in an Agent-First World](https://openai.com/index/harness-engineering/)
- [LangChain — Your Harness, Your Memory](https://www.langchain.com/blog/your-harness-your-memory)
- [LangChain — Improving Deep Agents with Harness Engineering](https://www.langchain.com/blog/improving-deep-agents-with-harness-engineering)
- [Martin Fowler — Harness Engineering for Coding Agent Users](https://martinfowler.com/articles/harness-engineering.html)
- [Red Hat — Structured Workflows for AI-Assisted Development](https://developers.redhat.com/articles/2026/04/07/harness-engineering-structured-workflows-ai-assisted-development)
- [walkinglabs/awesome-harness-engineering](https://github.com/walkinglabs/awesome-harness-engineering)
- [walkinglabs/learn-harness-engineering](https://github.com/walkinglabs/learn-harness-engineering)
- [celesteanders/harness — best-practices.md](https://github.com/celesteanders/harness/blob/main/docs/best-practices.md)
- [pablomarin/claude-codex-forge](https://github.com/pablomarin/claude-codex-forge) — concrete v5+ implementation; closest to our stack

## Grading rubric

For each technique:
- ✅ **Have** — GA already does this, possibly under a different name.
- 🟡 **Partial** — exists but not enforced or not consistent across the multi-agent surface.
- ❌ **Missing** — clear gap; needs a concrete artifact (script, hook, doc) to close.

Effort:
- **S** — under an hour
- **M** — half day
- **L** — multi-day

## Context & memory management

### Progressive disclosure navigation

Don't flood the LLM with a 5,000-line CLAUDE.md; provide a ~100-line table of contents + symbol map and let the agent fetch deeper files on demand.

- **Status: 🟡 Partial.** `CLAUDE.md` is ~90 lines (good) but it doesn't enumerate the .claude/skills/ surface or the per-package CONTEXT.md files that other agents rely on. New agents waste tokens grepping to find what already exists.
- **Action (S):** add a `## Where to find things` section in `CLAUDE.md` that lists: `docs/plans/` (in-flight work), `docs/solutions/` (compounded fixes), `docs/architecture/layers.md` (the 5-layer rule), `.agent/skills/` (language standards), `state/digests/latest.md` (session resume), `state/handoffs/` (cross-agent baton), and `/dev-data/manifest` (live everything-summary).

### Disk-persistent state

Critical decisions go to disk immediately, not to the conversation. Survives compaction.

- **Status: ✅ Have.** `state/digests/`, `state/handoffs/`, `state/quality/`, `docs/solutions/`, `docs/plans/` — all are disk-first. Pre-commit hooks already maintain `AGENTS.md` from `CLAUDE.md`. Phase 2 of DESIGN.md adoption (just shipped in #294) follows the same pattern: edit DESIGN.md, regenerate `theme.ts`, commit.

### Cross-session memory compaction

Auto-summarize the volatile conversation; off-load durable knowledge to external persistence.

- **Status: ✅ Have.** Auto-compact triggers at 40% across all 6 repos (set globally in `~/.claude/settings.json` 2026-05-23). The `digest` skill + the `precompact-digest.ps1` hook + the `sessionstart-digest.ps1` hook implement Cherny's pattern from the 2026 Sequoia talk. Memory under `~/.claude/projects/<repo>/memory/` survives compaction. The Stop-hook stomp issue is tracked as a session-learned rule in `CLAUDE.md`.

## Verification & feedback loops

### Cybernetic governance loops (feed-forward + feedback)

Pre-defined skills + automated post-action evaluation against architecture boundaries.

- **Status: 🟡 Partial.** We have feed-forward via skills (`/digest`, `/learnings`, `/correct`, `/feature`) and feedback via the `karpathy-cherny-discipline.yml` workflow + the ROP-naked-throws check in `.githooks/pre-commit`. Missing: a structured **post-merge** evaluator that grades the merged PR against the original goal. Today an agent declares "done" and that's the end of the loop.
- **Action (M):** Add a `/grade-last-pr` skill that runs `octo:review` against `git diff HEAD~1` and writes the score to `state/quality/pr-grades/<sha>.json`. Surfaces drift between intent and delivery without humans manually reviewing.

### Generator-evaluator separation

Never let the agent that wrote the code judge its own correctness.

- **Status: 🟡 Partial.** `octo:review` already dispatches 3+ specialist personas (code-reviewer, security-sentinel, kieran-typescript-reviewer) and explicitly recommends "the agent that wrote it must not grade it." But the discipline is opt-in — most session work today never invokes a separate reviewer. In multi-agent mode it's better: Claude writes, Codex reviews via `gh pr` from a separate session. The "Engineering Council" pattern from claude-codex-forge formalizes this with 5 advisors and a Codex chairman.
- **Action (M):** Adopt the claude-codex-forge `/council` model as an opt-in pre-merge gate for one-way-door PRs (schema changes, public API surfaces, OPTIC-K dims, pricing). One-way doors are already enumerated in `CLAUDE.md` so the trigger condition is known.

### Pre-completion interceptors

Block the agent from declaring done until lint, type-check, build, and E2E suites are green.

- **Status: 🟡 Partial.** `.githooks/pre-commit` runs `dotnet format` + build + ROP check + AGENTS.md sync + DESIGN.md/theme sync (post-#294). Missing: frontend type-check + a programmatic "is the public URL still serving 200?" check. Today's session shipped a port-guard fix (#289) because no one detected the displacement before merging.
- **Action (S):** Add an `npm run typecheck` step to the React project (currently `build:with-types` exists but isn't pre-commit), call it from `.githooks/pre-commit` when any `.tsx`/`.ts` file is staged.
- **Action (M):** Add a `Scripts/post-merge-smoke.ps1` invoked from a post-merge hook OR a GitHub Action that curls `https://demos.guitaralchemist.com/test` + `/chatbot/api/chatbot/status` + `/dev-data/manifest` after each push to main. Page the operator if any returns non-200. Closes today's 25-min-outage class of bugs.
  - **Shipped 2026-05-23 in [#311](https://github.com/GuitarAlchemist/ga/pull/311).** `.github/workflows/post-merge-smoke.yml` curls `/test`, `/chatbot/`, `/dev-data/manifest` after each merge to main and writes `state/quality/e2e/<timestamp>.json`. Per-URL regression: comment on the merge commit + open a tracking issue. Tunnel-down heuristic (`>=2 of 3 unreachable`) suppresses noise during local-Cloudflare outages. Paired `Scripts/post-merge-smoke.ps1` for local runs. Phase 2 to move the check earlier into the PR-check flow is noted in the workflow header.

## Workflow & environment security

### User-experience E2E verification

Run outside-in browser verification (e.g. Playwright) so the agent sees what the user sees.

- **Status: 🟡 Partial.** I (Claude) do this manually via headless Chrome screenshots in critical sessions, but it's not automated. The `chrome-devtools-mcp` server is in `.mcp.json` but not consistently invoked by agents. `agent-browser` skill exists. No automated E2E suite on the dashboard.
- **Action (M):** Wire a Playwright job to the post-merge GitHub Action that opens `/test`, `/test/manifest`, `/chatbot/#showcase` and asserts heartbeat banner color, manifest schema_version, and chatbot showcase response time < 5s. Output goes to `state/quality/e2e/` and feeds the QA tab.

### Mechanical invariant enforcement

Encode rules into tooling, not prompts. OS-level sandboxes for shell commands.

- **Status: 🟡 Partial.** Claude Code's permission classifier already blocks `Stop-Process` / `taskkill` / etc. without user authorization (witnessed in this session: the Vite restart and GaApi kill both required explicit prompts). The pre-commit hook acts as a tooling-encoded invariant for build green + format + ROP. Missing: OS-level sandbox isolation of the agent's shell — today every agent runs as the user. The PR #288 `/dev-data/*` exposure happened because the dev-only middleware leaked through the Cloudflare tunnel; the boundary lived in code, not in a sandboxing layer.
- **Action (L):** Run Vite + GaApi inside a constrained user account (no admin token) so the GaApi-21036-bin-lock problem can be resolved by a non-elevated agent kill. Or alternatively: install the `GuitarAlchemist` Windows service (`Scripts/install-ga-service.ps1`) so service-lifecycle is centralized and individual agents don't need to kill processes.
- **Action (S, partial coverage):** Already done in this session for the `.mcp.json` env-var leak (mcp_servers redaction kept in `vite.config.ts`) and the `/pr/*` SSE bus public exposure (gate in #fix/prime-radiant of PR #288). Pattern is documented in `vite.config.ts` SECURITY MODEL comment.

## Architecture-aligned techniques

### Adopt claude-codex-forge's 7-phase workflow

PRD → Research → Design → Review → Build → Verify → Ship.

- **Status: 🟡 Partial.** We have Research/Design (`docs/plans/`), Review (`octo:review`), Build, Ship via PR. Missing as an enforced step: explicit PRD ("who is in pain and what changes for them" — Karpathy rule 5 / sohaibt product-mode) and Verify (the post-merge smoke from above).
- **Action (M):** Adopt the forge's `/new-feature` skill or its equivalent so non-trivial work prompts for PRD framing before code. Already partially covered by the brainstorming skill, but not enforced.

### Adopt the `state.md` Workflow / Done / Now / Next pattern

Per-developer state file in gitignored `.claude/local/state.md`, read by hooks on demand, kept out of the auto-loaded context window.

- **Status: ❌ Missing.** Today my own session state lives in this conversation buffer; if you (Stephane) opened a new Claude window in parallel, you'd have to re-explain "I'm 6 PRs deep into dashboard work." The handoff system exists for cross-agent baton but not for cross-session same-agent continuity.
- **Action (S):** Adopt the forge's `.claude/local/state.md` pattern. Already partly implemented by the digest system; could be reframed.

### Auto-memory across compaction

The forge uses a `PreCompact` hook to rescue active context before auto-compact wipes it.

- **Status: ✅ Have.** `Scripts/precompact-digest.ps1` does exactly this. Documented in `CLAUDE.md`'s session continuity section.

### ADR / CHANGELOG / solutions/ as compounded knowledge

Architecture decisions, root causes, patterns travel with the repo so every session builds on the previous.

- **Status: ✅ Have.** `docs/solutions/`, `docs/plans/`, `docs/archive/`, `docs/architecture/`. Could be more rigorous (e.g. ADRs as a distinct format under `docs/adr/`), but the substance is there.

## What's NOT worth adopting (yet)

- **Superpowers plugin** from the forge — overlaps significantly with what `octo` already gives us; adding it would create two flavors of the same primitives. Revisit when octo plateaus.
- **Full sandbox isolation per agent** — high cost, low marginal win for a 1-developer + multi-agent setup where the developer is the trusted root. Worth it if a third-party agent gets read+write access to the repo.
- **Cross-agent inline-comment review threading** — multi-agent PR comments would be useful but require an integration layer none of us has built. The `state/handoffs/` files are 90% as good for the cost.

## Prioritized rollout

Each item is independent; pick by hand or work top-down.

| # | Item | Effort | Impact | Owner |
|---|---|---|---|---|
| 1 | "Where to find things" map in CLAUDE.md | S | M (every new agent needs it) | next session |
| 2 | Add `npm run typecheck` to pre-commit hook | S | M (catches a class of regressions) | next session |
| 3 | Per-session `.claude/local/state.md` | S | M (cross-session continuity) | next session |
| 4 | Post-merge smoke (curl + screenshot the 3 demo URLs) via GitHub Action | M | H (closes 25-min-outage class) | shipped 2026-05-23 (#311) |
| 5 | `/grade-last-pr` skill: re-run octo:review against the merged diff | M | M (closes the intent-vs-delivery loop) | week of |
| 6 | Adopt `/council` from claude-codex-forge as opt-in pre-merge gate for one-way doors | M | M (only fires on high-stakes PRs, low ongoing cost) | sprint of |
| 7 | Playwright E2E job on demos dashboard + chatbot showcase | M | H (UX verification automated) | sprint of |
| 8 | Run Vite + GaApi as a non-admin Windows service (or via `install-ga-service.ps1`) | L | H (eliminates today's elevated-process kill problem) | when the same outage recurs |

## What this plan deliberately defers

- Multi-tenant security (per-agent OAuth scoping, audit log per agent action) — over-engineering for a 1-developer setup.
- Replacing the existing `octo` system with a third-party harness (forge, deep-agents). They overlap heavily and migration cost > benefit.
- Token-quota visibility on the AI Contributors card — flagged as Operational TODO #4; needs per-provider auth (Anthropic / OpenAI / Google API) and is a separate integration per provider.

## Revisit trigger

Revisit when:
- A new outage class recurs (port displacement, elevated process lock, public data exposure) — pick the most relevant action from the table above
- `awesome-harness-engineering` or `claude-codex-forge` ships a 1.0 with a new pattern not covered here
- The session-state friction (re-explaining context to a fresh window) starts costing > 10 minutes a week

This plan is intentionally a menu, not a sprint. Pick items #1-#3 (all small, all single-session) before the next major dashboard or chatbot push.
