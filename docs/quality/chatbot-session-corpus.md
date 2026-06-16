# Chatbot session corpus — the multi-turn axis of the improvement loop

**Added:** 2026-06-16 · **Type:** quality oracle (mechanical axis)

## Why

The single-turn corpus (`prompts.yaml`, 56 prompts) asserts invariants on one-shot
answers. It is structurally blind to **conversation** bugs: a follow-up that falls
back, an answer that forgets "those chords" two turns later, a journey that works
prompt-by-prompt but not as a session. Real guitarists don't ask one question —
they have a conversation. This corpus measures that.

## What

`Tests/Apps/GaChatbot.Api.Tests/Corpus/sessions.yaml` defines full multi-turn
conversations across four guitarist journeys (beginner onboarding, songwriting &
progressions, improv scales/modes, jazz & advanced harmony).
`SessionCorpusTests.EverySession_SatisfiesItsInvariants` replays each one
turn-by-turn against `GaChatbot.Api`, passing the **accumulating
`ConversationHistory`** on every request — the exact path the live `/chatbot` UI
uses — so context-dependent turns actually carry state.

`Scripts/run-session-corpus.ps1 -Snapshot` emits a trend snapshot to
`state/quality/chatbot-qa-sessions/YYYY-MM-DD.json` with `session_pass_pct`.

### The context-retention invariant

The one invariant single-turn corpora cannot express: a turn's `references:` are
substrings the answer must carry **because a prior turn established them**. If turn
2 ("give me a progression using *those* chords") drops every chord turn 1 taught,
that's a `lost conversation context` failure — caught here, invisible elsewhere.

All invariants are **mechanical** (routing, grounding presence, no-fallback,
context retention, banned phrases, latency). That is deliberate: the mechanical
axis is deterministically verifiable and therefore safe for the autonomous loop to
optimise against.

## How the loop/goal consumes it

`state/quality/chatbot-qa-sessions/baseline.json` is the setpoint contract — the
same shape as the single-turn `chatbot-qa/baseline.json` the `/auto-optimize` loop
already reads. The loop's session-axis cycle:

1. **Measure** — `run-session-corpus.ps1 -Snapshot` → `session_pass_pct` + the
   failing-turn list.
2. **Target** — pick the worst failing turn (a stable miss like "tritone sub →
   Db7", or a lost-context turn) and fix the responsible skill/agent within
   `scope_boundary.allow_edit`.
3. **Re-measure → ratchet** — commit only if `session_pass_pct` did not regress
   (`reject_on_regression`, threshold 0.06 — wider than single-turn because a
   16-turn corpus is coarse and multi-turn LLM variance is higher).
4. **Grow** — the loop may **add** new sessions/turns (gear/tuning, technique,
   ear-training — see `baseline.json:corpus_growth`). New journeys start as failing
   targets; lifting them is the work. The loop must **not weaken** existing
   invariants to pass them.

## Boundary held

Multi-turn answer **helpfulness/tone** is fuzzy and stays human/Demerzel-tribunal-
gated. This corpus never encodes it, and an LLM-judge of session quality must never
drive the inner loop — the same line the whole nested-loop design holds.

## Seed baseline (2026-06-16)

`session_pass_pct ≈ 0.69–0.81` across runs (11–13 / 16 turns). Stable target:
jazz tritone-substitution turn. Flaky targets: the two jazz context-retention
turns (Cmaj7) and two borderline `min_length` turns — first loop work items.
