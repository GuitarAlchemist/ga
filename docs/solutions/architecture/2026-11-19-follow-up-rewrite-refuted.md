---
module: GA.Business.Core.Orchestration
tags: [chatbot, routing, follow-up, conversation, eval, refuted]
problem_type: decision
decision: Do not rewrite follow-ups into standalone questions before routing (for now)
rejected: [LLM standalone-question rewrite before routing and skills, the same rewrite for routing only]
reason: The rewrite injects the previous turn's entity, so deterministic skills claim the question and answer the entity instead of the question; A/B gave 6 regressions for 1 improvement
date_decided: 2026-11-19
---

# Follow-up rewrite before routing: refuted

## Hypothesis

Routing and the deterministic skills read only the current message, so follow-ups such as
"And a C shape?" (after a capo question) or "Show me an easy way to play it" (after D7) are
misrouted. Rewriting the follow-up into a standalone question with Haiku 5.5, using the last
turns, and feeding that question to every stage should fix them. It would also replace the
prefix regex of `RoutingContextEnricher`.

Falsifier set before the run: no gain on the conversation cases, or a regression elsewhere.

## Method

Prototype `FollowUpRewriter` (branch `feat/followup-rewrite`, commit 71940f3e) behind `Chatbot:FollowUpRewrite`, wired after the request hooks of
`ProductionOrchestrator` (tabs never rewritten, a `followup.rewrite` trace step). Same GaApi binary
(main at 73593de4 plus the prototype), production configuration (Anthropic chat, Ollama
embedders), setting off then on. Both runs used the 51 cases of `Tools/chatbot-eval` (convo and
the Haiku API judge), compared with `evalgate`.

## Evidence

| | off | on |
|---|---|---|
| deterministic checks passed | 48/51 | 46/51 |
| judged fail | 2 | 7 |
| good | 48/51 | 43/51 |
| slowest turn per case, median | 11.4 s | 7.5 s |

One improvement (`conv-capo-followup`), six regressions. The rewrites themselves were mostly
faithful. What they changed was the route:

| case | follow-up | rewrite | routed to | answer |
|---|---|---|---|---|
| c2-scale-then-mode | Now start it from D. What mode is that? | Spell the C major scale starting from D. What mode is that? | skill.scaleinfo | the C major spelling again, no mode |
| conv-mode-then-song | Which chord does it fit best? | Which chord does the Mixolydian mode fit best? | skill.modes | the Mixolydian definition again |
| conv-practice-then-detail | Can you explain the first exercise in more detail? | … the Warm-up (5 min) exercise in the Jazz comping routine … | skill.practiceroutine | the whole routine again |
| c2-name-memory | What is the lowest note I can play on it? | … on the seven-string guitar tuned B E A D G B E? | voicing | B voicings, not the lowest note |

Without the rewrite, the bare follow-up matched no skill and reached an LLM agent that has the
history and answered correctly.

## Verdict

Refuted in this form. Routing on the rewrite is what sends these questions to skills, so the
"routing only" variant has the same failure.

The real defect is upstream: **deterministic skills claim entity-rich questions they cannot
answer** (scaleinfo for a mode question, beginnerchords for "an easy D7"). That precision problem
is what to fix before a rewrite can help. A skill should decline what it does not answer (see
skill declines in dc2e74cb). Revisit the rewrite once the skills decline those questions; rerun
the same A/B.
