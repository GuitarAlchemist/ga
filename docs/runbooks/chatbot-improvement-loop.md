---
title: Chatbot Improvement Loop (Cherny-style autonomous iteration)
status: living
date: 2026-05-13
related:
  - docs/plans/2026-05-07-chatbot-roadmap.md
  - docs/plans/2026-05-13-skills-domain-backed-refactor-plan.md
  - Tests/Apps/GaChatbot.Api.Tests/Corpus/prompts.yaml
  - Scripts/run-prompt-corpus.ps1
---

# Chatbot improvement loop

How to run a long-running autonomous iteration on the GA chatbot using the prompt corpus as the oracle. Each cycle picks the worst-scoring prompt, fixes the underlying skill or formatter, re-runs the corpus, commits if improved.

## Components

| Piece | Role | Where |
|---|---|---|
| Oracle | YAML corpus + invariants | `Tests/Apps/GaChatbot.Api.Tests/Corpus/prompts.yaml` |
| Runner | NUnit test exec + failure aggregation | `Tests/Apps/GaChatbot.Api.Tests/Corpus/PromptCorpusTests.cs` |
| Loop-friendly wrapper | Picks worst failures, emits JSON | `Scripts/run-prompt-corpus.ps1` |
| Target hosts | The actual chatbot under test | `GaChatbot.Api` (5252) + `GaApi` (5232 for hubs) |

## One-shot health check

```powershell
pwsh Scripts/run-prompt-corpus.ps1
```

Exits 0 if every non-skipped prompt satisfies its invariants. Prints failures grouped by category, plus latency warnings (soft budget — informational, not gating).

## Loop invocation patterns

### Pattern A — `/loop` with a self-prompting payload

In the Claude Code REPL:

```
/loop 20m pwsh Scripts/run-prompt-corpus.ps1 -Worst 1 -Json state/corpus-last.json
```

The `/loop` skill wakes every 20 minutes, runs the corpus, you (or the model) review the worst failure, apply a fix, commit. The cache-friendly cadence keeps cost bounded.

### Pattern B — Autonomous mode with the corpus as the gate

```
/loop <<autonomous-loop-dynamic>>
```

The autonomous payload is responsible for picking the worst failure, proposing a fix, applying it, and re-running. Sample instructions to wire into the autonomous prompt:

> Run `pwsh Scripts/run-prompt-corpus.ps1 -Json state/corpus-last.json`. If `totalFailures` is 0, no work to do — sleep until next cycle. Otherwise: read the first failure, locate the implicated skill in `Common/GA.Business.ML/Agents/Skills/`, propose a minimal fix that satisfies the invariant, rebuild + restart GaChatbot.Api on port 5252 (per memory `reference_dev_stack_three_services.md`), re-run the corpus. If failure count strictly decreased and no new failure appeared, commit with `fix(chatbot): improve <prompt> handling` and continue. If not, revert and try a different angle.

### Pattern C — Multi-LLM parallel improvement

For the strongest signal, run `/octo:multi` against each failing prompt:

```
/octo:multi "Looking at PromptCorpusTests failure '<failing prompt>' — propose three different fixes and rank by safety."
```

Pick the winning patch, apply, re-run corpus. Memory `feedback_multi_llm_review_pays_off` documents the empirical value of this for music-theory / DI / parser PRs.

## Safety rails

The loop is only as honest as the oracle. Before letting it run unattended:

1. **Cover the regression class first.** Every new failure mode the loop introduces should add a new invariant in `prompts.yaml`. The corpus grows monotonically.
2. **Commit one prompt-fix per iteration.** Multiple skill edits in one commit make rollback expensive when the loop overcorrects.
3. **Don't let the loop edit the corpus.** That's the gate — having the agent rewrite its own gate defeats the safety property. Lock `prompts.yaml` to human review.
4. **Hard stop at 50 failure-fixes per session.** If the corpus is still failing after 50 iterations, the architecture is wrong, not the implementation. Stop and ask.
5. **Latency warnings are not gates.** If a fix makes a prompt 2x slower but still under budget, that's acceptable for now — performance work is its own track.

## What the loop is good at

- Fixing leaked SKILL.md preambles (string substitution / loader fix).
- Adding missing aliases (e.g. "Hijaz" → Phrygian Dominant, the regional name fix).
- Refining `not_contains` lists when the chatbot regresses to old phrasing.
- Catching skill-routing regressions (wrong agent picked).

## What the loop is bad at

- Architectural changes (domain-backed refactor, new MCP tools). Those belong in `docs/plans/*.md` and a human review.
- New capabilities (extended chords, alternate tunings, capo). Those are dealbreakers from `BACKLOG.md` and need scoped feature work.
- Anything touching the OPTIC-K index or the schema. One-way doors must go through a planned change.

## Adding a new prompt

```yaml
- prompt: "<the question>"
  category: <bucket>
  routes_to: <expected agentId>             # optional
  contains: ["<must appear>"]               # optional
  contains_any: ["<one of>", "<these>"]     # optional
  not_contains: ["<must not appear>"]       # optional
  min_length: 100                            # optional, default 50
  max_elapsed_ms: 30000                      # optional latency budget
  expected_grounding: ga.dsl                # optional grounding source
  skip: false                                # default; set true with skip_reason
```

Run `dotnet test --filter PromptCorpusTests.Corpus` to verify the new entry loads. Then run the full corpus once to confirm the live backend already satisfies the invariants (otherwise you're adding a known-failing test).

## When to stop the loop

- Failures = 0 for three consecutive runs.
- Or: the next failure requires a one-way door (schema, OPTIC-K, public API).
- Or: latency warnings start growing (the loop is trading correctness for speed).
- Or: 50-iteration hard stop (see safety rail #4).
