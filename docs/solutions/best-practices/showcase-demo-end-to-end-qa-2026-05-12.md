---
module: GA.Business.ML / GaApi
date: 2026-05-12
problem_type: best_practice
component: chatbot
symptoms:
  - "Demo / showcase / example UI ships with broken advertised paths"
  - "Tests pass green but live feature fails on the first click"
  - "Catalog skill returns its LLM-priming preamble verbatim to the user"
  - "Voicing prompts return 'no matches' because the index isn't loaded"
root_cause: tested_scaffolding_not_experience
resolution_type: process_plus_test_gate
severity: high
tags:
  - chatbot
  - showcase
  - qa
  - catalog-skills
  - skill-md
  - smoke-test
  - end-to-end
  - ui-shipping
  - compound-engineering
---

# Showcase / Demo Features — End-to-End QA Required

When a feature advertises specific paths (a Showcase Panel, a Demo Mode, an Examples list), **the advertised content is the QA target**. Tests that verify only the JSON shape of the demo script and the rendering of the modal are decorative — they will not catch the failures a user finds on the first click.

## What went wrong (PR #210, 2026-05-12)

The chatbot Showcase Panel shipped with "3 integration tests + 5 unit tests, all green." Within 10 seconds of live use the user found:

| Prompt | Failure |
|---|---|
| "Explain the circle of fifths" | Leaked SKILL.md directive: *"Reproduce the catalog below verbatim when a user asks…"* |
| "How do I play a barre chord?" | Misrouted to practice-routine skill, leaked **that** directive too |
| "Show me chord voicings for Cmaj7" | "The OPTIC-K index returned no matches" — startup log line 5 already said the index wasn't loaded |
| "Analyze the progression Cmaj7 Am7 Dm7 G7" | Chord-symbol parser failed on the format |
| "Compute voice-leading from Cmaj7 to Fmaj7" | Parser failed |
| "How do I improve my fingerpicking?" | Misrouted to memory skill |

8 of 15 advertised prompts were broken. The "tests" never exercised the prompts; they checked shape and render.

## Root causes (two)

### 1. Tests validated scaffolding, not experience

Backend tests asserted `GET /demo` returned shape-correct JSON. Frontend tests asserted the modal renders. Neither asserted that any prompt produced a useful response. The path *advertised by the feature* was never traversed by automation.

### 2. Catalog skills returned LLM-priming preamble to users

`CatalogSkillMdLoader.LoadBodyOrFallback` returned `skillMd.Body` verbatim. The SKILL.md authoring style places a "model-directive preamble" between the H1 heading and the first H2 section — instructions written for an LLM that will use the body as context. **Pure-catalog skills make no LLM call**, so the preamble goes straight to the user and reads as a leaked system prompt.

## Fix

### Code

- `Common/GA.Business.ML/Agents/Skills/CatalogSkillMdLoader.cs` — added `StripModelDirectivePreamble` that walks the H1→first-H2 zone and drops paragraphs containing known directive markers ("reproduce the catalog below", "pure pedagogy", "use when a learner asks", etc.). H1 heading and all H2+ content preserved.
- `Apps/ga-server/GaApi/Controllers/ChatbotController.cs` — bumped `GetDemo()` script to v1.1, removed Voicings category entirely (restore when OPTIC-K index ships), replaced broken progression / voice-leading / fingerpicking prompts with hand-verified working ones. 12 prompts, all live-tested.

### Test gate (compound)

- `Tests/Apps/GaApi.Tests/Controllers/ChatbotShowcaseSmokeTests.cs::EveryShowcasePrompt_ProducesAUsefulAnswer` — `[Explicit]` integration test that fetches `/api/chatbot/demo`, POSTs every prompt to `/api/chatbot/chat`, and asserts the response is non-empty (>50 chars), doesn't contain known SKILL.md-directive markers, and doesn't contain "no matches" / "could not parse" / "didn't find anything to remember" backend-fail markers. Tagged `[Explicit]` because it walks the full agentic pipeline (~minutes); operator runs before changing the demo script.

## Rule for the future

For UI / demo / showcase / example features:

1. **Click every advertised path** in a browser or via curl before declaring done. If untested, say "untested — please verify" instead of claiming success.
2. **The advertised content IS the QA target.** Shape tests on the demo script are necessary but never sufficient.
3. **Startup-log warnings on the running service are part of the test surface.** "OPTK index not found" at boot meant every voicing prompt was guaranteed to fail before a single test ran.
4. **Pair every advertised collection with an executable smoke test** that exercises each item against the live backend. The smoke test in PR #210 is the gate for the chatbot showcase; copy this pattern for any future "examples" / "demos" / "showcase" collections.

## Related

- CLAUDE.md "Doing tasks" section: *"For UI or frontend changes, start the dev server and use the feature in a browser before reporting the task as complete."*
- Memory: `feedback_ui_click_through_before_done.md`.
