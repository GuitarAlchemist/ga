---
name: "Chatbot Iterate"
description: "L2 chatbot-development loop. Picks the next chatbot-shaped item from BACKLOG.md, runs feature → plan → work → review → PR while enforcing the Demerzel tribunal gate on any change touching GA.Business.ML/Agents, MCP tooling, DSL parser, or DI composition. Use when iterating on chatbot capability, not for general feature work."
allowed-tools: Read, Write, Edit, Glob, Grep, Bash, Agent, AskUserQuestion, Skill
last_verified: 2026-05-10
---

# /chatbot-iterate

Glue skill that ties the existing pieces (`/feature`, `/work`,
`/octo:review`, the Demerzel QA tribunal, `/learnings`,
`/ce-compound`) into a single chatbot-scoped iteration loop. Emits a
clear gate plan up front so the human knows what review the change
will need before merging.

This is the **Level 2** automation in our chatbot-development ladder
(per the discussion in docs/automation/chatbot-loop.md): assistant
drives end-to-end on one item, human reviews and merges. Not Level 3
(auto-merge on multi-LLM green) or Level 4 (failure-fed dark factory).

## When to Run

- "Pick up the next chatbot thing" — moving down the chatbot backlog
  systematically.
- After shipping a chatbot feature, when the next item is queued and
  the assistant should keep the cadence.
- *Not* for general feature work — use `/feature` directly. This skill
  exists to enforce the chatbot-specific gates.

## Iron Law

```
ANY PR touching the following paths REQUIRES Demerzel tribunal verdict
+ multi-LLM review (octopus or codex) BEFORE merge:

  Common/GA.Business.ML/Agents/**
  Common/GA.Business.ML/**/Mcp/**
  Apps/ga-server/GaApi/Mcp/**
  Common/GA.Business.DSL/**
  **/IChatClientFactory*
  **/AddGuitarAlchemistAi*

The "feedback_multi_llm_review_pays_off" memory documents that
multi-LLM review caught 9 real bugs in PR #151 alone that local
tests missed. This gate is load-bearing, not bureaucratic.

NEVER mark a PR ready-for-merge on a chatbot path until both gates
have been satisfied.
```

## Process

### Step 1: Identify the next chatbot item

Sources, in priority order:

1. `BACKLOG.md` (root) — chatbot-flagged items. Filter to entries
   mentioning chatbot / TheoryAgent / TabAgent / VoicingAgent /
   intent routing / MCP tools / DSL eval / capability matrix.
2. Active `docs/plans/*-chatbot-*.md` — any plan in flight that
   has a "Next" section.
3. Open issues tagged `chatbot` (if any) on GitHub. Use
   `gh issue list --label chatbot --state open`.
4. Recent `docs/solutions/*chatbot*` entries — sometimes a learning
   surfaces a concrete fix that's worth spinning into a backlog item.

If no candidate is unblocked, **stop**. Don't manufacture work. Tell
the user and ask whether to surface a stale item.

### Step 2: Classify the gate requirements

For the chosen item, walk the implementation areas and decide which
gates are required. Output a short upfront plan, e.g.:

```
Item: "Add ga_chord_voicings(chord, tuning) MCP tool"

Touches:
  - Common/GA.Business.ML/Mcp/Tools/         → tribunal: REQUIRED
  - Common/GA.Business.Core.DSL/Voicings/    → tribunal: REQUIRED
  - GaApi/Program.cs (registration)          → tribunal: review only

Gates before merge:
  - octo:review (correctness + security)
  - Demerzel QA Architect Tribunal (music-theory verdict)
  - dotnet test AllProjects.slnx green
  - ga-react-components build + lint green
  - Manual smoke against /api/chatbot endpoint

Estimated PR size: medium (3-6 files, +200/-50 LOC)
```

This step is **explicit so the user can veto**. If they push back on
scope, restart with a smaller item.

### Step 3: Run the feature pipeline

Once gates are agreed, hand off to existing skills:

1. **Plan**: invoke `compound-engineering:ce-plan` if the item needs
   design thinking. For most well-shaped items, skip directly to
   step 2.
2. **Work**: invoke `compound-engineering:ce-work` with the scope
   limited to the touched paths. Or run `/feature` if the item
   requires the full BACKLOG → plan → implement flow.
3. **Verify**: per CLAUDE.md, `dotnet build AllProjects.slnx -c Debug`
   AND `dotnet test AllProjects.slnx` AND
   `npm run build && npm run lint` in
   `ReactComponents/ga-react-components`. Don't claim success until
   all three are green.
4. **Multi-LLM review**: run `/octo:review` (or `codex review` if
   octopus is unavailable). Resolve every finding marked validity ≥
   medium before requesting tribunal.
5. **Tribunal verdict**: invoke
   `compound-engineering:ce-doc-review` against the change list, or
   for chatbot paths specifically use the Demerzel governance
   pipeline (`Demerzel/pipelines/qa-tribunal.ixql` per the
   project memory).

### Step 4: Open the PR

Use `compound-engineering:ce-commit-push-pr` for the PR with body
including:

- The backlog item link
- Summary of approach
- Test output snippets (build + tests + lint)
- Multi-LLM review summary (passed gates)
- Tribunal verdict ID
- A short demo paragraph if the change is observable (chatbot
  question + new response example)

### Step 5: Post-merge — capture learnings

After the PR merges:

1. Run `/learnings` to capture any non-obvious thing from the
   session.
2. Run `/ce-compound` if the change unlocks a new abstraction that
   should be promoted into the F# DSL or a new MCP tool.
3. Update `BACKLOG.md` to mark the item shipped and surface any
   follow-ups.

### Step 6: Loop

If the user said "keep going" or this is part of a `/loop`, return to
Step 1 with the next item. Otherwise stop.

## Anti-Patterns

- **Skipping the gate analysis** because "this is a small change".
  The rule is path-based, not size-based. A one-line change in
  `IChatClientFactory.cs` requires the same gates as a 200-line
  change.
- **Running `/feature` against a chatbot item without invoking this
  skill first** — you lose the gate enforcement. Use this as the
  entrypoint.
- **Auto-merging on a green octopus review without the tribunal
  verdict** — the tribunal catches different things (music-theory
  correctness, cross-LLM consensus on contested decisions). Both
  gates exist for different reasons.
- **Counting `dotnet test` as the only quality gate**. PR #151
  evidence: tests passed locally; multi-LLM review caught 9 bugs;
  tribunal would have caught more. Three gates = three signals.

## Output

Each invocation should produce, in order:

1. The selected item (or a clean "no work to do" message).
2. The gate plan (paths + required gates).
3. The hand-off to the next skill (with the prompt that will be sent).
4. After the work completes: gate-by-gate pass/fail summary.
5. After the PR opens: PR URL + waiting-for state.

If the user invokes this skill in `auto` or `/loop` mode, run the
gates serially and only require human input if a gate fails or the
plan changes.
