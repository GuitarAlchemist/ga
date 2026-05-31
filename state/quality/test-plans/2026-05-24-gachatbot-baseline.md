---
title: GA Chatbot Baseline Eval
target: chatbot
status: reviewed
generated_at: 2026-05-24T00:00:00Z
generator: hand-authored
---

# GA Chatbot Baseline Eval

Hand-authored seed plan that gives the QA tab's `TestPlansCard` something
to render on first paint. The five steps below mirror the canonical
chatbot evaluation pipeline used by the
[`/chatbot-iterate`](../../.claude/skills/chatbot-iterate/SKILL.md) and
[`/auto-optimize`](../../.claude/skills/auto-optimize/SKILL.md) loops, and
are tracked end-to-end by `Tests/Apps/GaChatbot.Api.Tests/Corpus/PromptCorpusTests.cs`
against the golden traces in `state/quality/chatbot-qa/golden-traces/`.

Replace this seed once `/test-plan` writes its first proposal here. The
`status: reviewed` chip on this plan is what tells the UI it isn't a
draft that should block the eye.

## Unit tests (5 proposed)

- [x] **Intent parser** — every prompt routes to exactly one `IRouter` decision; record the routing confidence floor (rationale: routing-eval baseline lives at `state/quality/routing-eval-2026-05-12.json`).
- [x] **Skill dispatch** — each `IChordVoicingsSkill` / `IImprovisationSkill` invocation matches the golden `_signature.json` shape in `state/quality/chatbot-qa/golden-traces/<prompt>/`.
- [x] **Response generation** — generated text contains the `contains` invariants and avoids the `not_contains` strings declared in `Tests/Apps/GaChatbot.Api.Tests/Corpus/prompts.yaml`.
- [x] **Citation validator** — every emitted citation resolves to a real MCP tool name in `.mcp.json` (no hallucinated `ga_*`/`ix_*` names).
- [x] **Golden trace scorer** — `pass_pct` aggregator matches the value baked into `state/quality/chatbot-qa/baseline.json` (currently 0.94, last known good 2026-05-16).

## Integration tests (3 proposed)

- [ ] **Round-trip skill validate** — `/chatbot-qa-roundtrip-validate` reproduces the snapshot diff with `reject_on_loss=true` and `reject_on_regression=true` (baseline threshold 0.02).
- [ ] **OPTIC-K index parity** — the chatbot answer set matches the v1.8 (`optk-v4-pp-r`) index already mmapped by `GaApi`; mismatch surfaces as `degraded_reason="index_mismatch"` in `state/quality/chatbot-qa/last.json`.
- [ ] **Cascade fallback** — when local Ollama returns HTTP 5xx the Mistral cascade activates within the same request; degraded marker stays out of `last.json` when the fallback succeeds.

## E2E tests (Playwright) (2 proposed)

- [ ] **/chatbot showcase** — at least one prompt's answer renders without console errors (covered today by `tests/dashboard/chatbot-showcase.spec.ts`).
- [ ] **/test#dev/qa** — `TestPlansCard` renders this seed plan plus the chatbot eval footer (covered by the spec in this PR).

## Chatbot prompts (1 proposed)

- [x] "What are the diatonic chords in G major?" — already covered under `state/quality/chatbot-qa/golden-traces/diatonic-chords-in-g-major/`. Add a paired prompt with an enharmonic spelling once the cascade is verified green.

## Coverage gaps surfaced

- **No test covers** the degraded-mode rollback path (`degraded=true` + `last_known_good_pass_pct` propagation) end-to-end — only the projection in the dashboard footer reads it.

## Rubric

This plan was hand-authored as a seed so `TestPlansCard` has a real row to
render on first paint. Replace with a `/test-plan`-generated proposal as
soon as one lands. The five-step structure mirrors the canonical chatbot
loop (parse intent → route → generate → validate citations → score) so
future agents can diff their proposals against a known shape.
