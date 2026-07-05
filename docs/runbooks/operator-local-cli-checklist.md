# Operator local-CLI checklist — the privileged actions

**Purpose.** The handful of actions that *must* run on the operator's own
machine (secrets, the full .NET + duckdb toolchain, the live backend,
local-only source) — i.e. exactly what a cloud / web Claude Code session
**cannot** do. Paste these into your local Claude Code CLI, ordered by
leverage. Companion: [cloud-dev-onboarding.md](cloud-dev-onboarding.md).

Legend: 👤 only you can do it (secret / local-only / live backend) ·
🤝 you *or* a local Claude Code session on your machine.

## 1. 👤 Re-arm Jules — unblocks #517 / #518 / #519 in one shot

```bash
gh secret set GEMINI_API_KEY --repo GuitarAlchemist/ga
# paste your Gemini key when prompted — it is YOUR secret; a cloud session
# neither has it nor should hold it.
```

The `gemini_dispatch` triage job fails with *"Please set an Auth method …
GEMINI_API_KEY"* (verified in the failed run logs); every
`ready-for-agent`+`jules` issue reports "unable to process" until the secret
exists. One command re-opens the whole delegation lane.

## 2. 🤝 Commit the local-only MCP sources — kills the desktop SPOF

`.mcp.json` points `hari`/`sentrux` at built binaries that live only on this
machine, and **`hari-mcp` has no source in the hari repo**. Commit + push those
sources so any host (cloud, CI, a teammate) can build them — then the portable
`.mcp.json` migration in cloud-dev-onboarding.md §2 becomes possible. Until
then, no cloud host can reach those peers.

## 3. 🤝 Feed the G2 reliability model with real data

```bash
cd <your ga checkout>
/grade-last-pr        # fill the additive `agent` field (the PR author) each run
```

`state/quality/pr-grades/` is **empty today**, so `hari-core reliability`
returns an empty report. Each graded merge gives hari's per-agent × task-class
model real input. Run it on recent merges and going forward — that is what
turns "which agent for which task" from assumed into measured (Giskard G2).

## 4. 👤 Real verification — cloud sessions have no dotnet here

```bash
# on branch claude/jarvis-track-scoping-9431c6:
dotnet test AllProjects.slnx      # the .NET side a cloud session can't build
cargo test --all                  # hari/ix verified in-session already; re-confirm in your env
```

## 5. 👤 Drive the .NET-bound work end to end

- **#519 discovery harness** — you have dotnet + duckdb + the LLM in one place,
  so you can build the harness *and* run the first discovery generation loop
  locally (plan: `docs/plans/2026-07-04-research-math-discovery-engine-plan.md`),
  without waiting on Jules.
- **ga#493** — restart the live backend + Cloudflare tunnel (fixes the dead
  chatbot-qa feed and the red ix nightly).

## What's me vs you

| Cloud session (me) | Local CLI (you) |
|---|---|
| Rust (hari / ix), plans & docs, sub-agent fan-out, bounded research, GitHub review/merge | Secrets, .NET/duckdb builds, the live backend, local-only source, `/grade-last-pr` |

Highest leverage right now: **#1** (re-arms Jules) and **#3** (`/grade-last-pr`
populates G2) — two minutes each, and they unblock the rest.
