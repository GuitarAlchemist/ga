---
id: 2026-09-25-ga-chatbot-baseline
date: 2026-09-25
status: active
domain: code
question: Which smallest public chatbot slice can demonstrate useful, correct guitar advice while exposing the current reliability gaps?
hypotheses:
  - claim: A single progression-to-arpeggio answer on the existing public stream is the first useful tracer.
    refuted_if: The public stream cannot complete that answer with the expected route and deterministic musical facts, or a smaller user-visible failure has higher priority.
tools: [gh, git, rg, Invoke-WebRequest]
artifacts: []
validators: [public GETs, source-code cross-check]
confidence: medium
supersedes: null
superseded_by: null
---

# GA chatbot: public baseline and first tracer

**Date:** 2026-09-25  
**Type:** research; [Wayfinder question #75](https://github.com/GuitarAlchemist/.github/issues/75), planning only  
**Source revision:** local `dc2e74cbd992a298f67efb928b5b3754615e1924` (`feat/589-performance-intent-tracer`); the working tree also has unrelated uncommitted edits. Live observations below are time-specific.

**Publication scope:** this is a note-only research snapshot rooted at the recorded commit. Repository links identify the tracked source at that revision; observations explicitly attributed to uncommitted work remain provisional and are not published source changes. No live POST or deployment validation is implied by publication.

## TL;DR

The public `/chatbot/` page and its read-only API calls worked at 13:10 UTC, and the public service identified itself as `ga-chatbot-api`. This does **not** establish answer correctness or uptime: the September 25 CI QA snapshot has `pass_pct: null`, and no live chat POST was made for this research. The smallest first tracer is one existing, deterministic progression-to-arpeggio answer through the public page and SSE path, with a musical oracle, route/stream assertions, and a measured latency/failure baseline. Keep host retirement and the full progression-to-voicing coach gated on separate proof.

## 1. Question and prior art

The user needs to see one correct, complete guitar answer before a larger coach flow can be trusted. [Issue #623](https://github.com/GuitarAlchemist/ga/issues/623) proposes a 2–8 chord progression, theory, suggestions, voicings, and a reviewable UI; it is a multi-slice product story, not an observed current capability. [Issue #589](https://github.com/GuitarAlchemist/ga/issues/589) proposes validated structured arpeggio advice. [ADR-0005](../adr/0005-gaapi-single-canonical-chat-host.md) already accepts GaApi as the *eventual* sole chat host, but orders GaChatbot.Api retirement last, after parity, ingress migration, and live verification. The older [chat-surface inventory](../architecture/chat-surfaces.md) records a May deployment, so it is history to recheck rather than current ingress evidence. The September [consolidation assessment](../architecture/2026-09-12-chat-api-consolidation-assessment.md) records later local parity work while still leaving live ingress and retirement unverified; its last section is uncommitted in this checkout.

## 2. Hypothesis

**Claim:** Prove one public progression-arpeggio answer before adding another model path or attempting the full coach. The existing page already calls `POST api/chatbot/chat/stream`, and the current corpus already expects `skill.improvisation` for `which arpeggio fits Am F C G` ([page source](../../Apps/GaChatbot.Api/wwwroot/index.html), lines 736–808 and 947–984; [corpus](../../Tests/Apps/GaChatbot.Api.Tests/Corpus/prompts.yaml), lines 472–496). The answer is small enough to validate chord by chord while exercising the actual visitor route.

**Refutation:** A controlled public run fails to complete, routes away from improvisation, gives a structurally wrong arpeggio/mode, or shows that another smaller user-visible failure must be repaired first. No such run was made here; the recommendation is provisional.

## 3. Method (read-only baseline; rerunnable)

From the GA checkout, inspect source at the recorded SHA and current working-tree status:

```powershell
git rev-parse HEAD
git status --short
gh issue view 75 -R GuitarAlchemist/.github --json title,body,state,url
gh issue view 623 -R GuitarAlchemist/ga --json title,body,state,url
gh issue view 589 -R GuitarAlchemist/ga --json title,body,state,url
gh issue view 560 -R GuitarAlchemist/ga --json title,body,state,url
gh issue view 703 -R GuitarAlchemist/ga --json title,body,state,url
gh issue view 328 -R GuitarAlchemist/ga --json title,body,state,url,comments
gh issue view 724 -R GuitarAlchemist/ga --json title,body,state,url,comments
gh issue view 726 -R GuitarAlchemist/ga --json title,body,state,url
rg -n 'ArpeggioIntentService|PerformanceIntent' Apps Common GaMcpServer -g '*.cs'
rg -n 'qa-summary|HttpGet\("examples"\)' Apps/GaChatbot.Api/Controllers/ChatbotController.cs Apps/ga-server/GaApi/Controllers/ChatbotController.cs
```

At the time of the live check, `Invoke-WebRequest -Method Get -TimeoutSec 10` was used for the four public `/chatbot/` URLs in the table below. `https://demos.guitaralchemist.com/dev-data/manifest` was separately requested with a 15-second timeout. These requests do not submit a chat prompt, use a model, change deployment, or reveal secrets. The [September 25 QA snapshot on `main`](https://github.com/GuitarAlchemist/ga/blob/main/state/quality/chatbot-qa/2026-09-25.json) was read through the GitHub contents API because that file is absent from this local checkout.

## 4. Evidence and limits

| Surface or claim | Observation on 2026-09-25 | Meaning |
|---|---|---|
| `GET https://demos.guitaralchemist.com/chatbot/` | HTTP 200, HTML title `GA Chatbot` | Public page reachable at this instant; no chat answer tested. |
| `GET https://demos.guitaralchemist.com/chatbot/api` | HTTP 200, `service: "ga-chatbot-api"`, version `0.1.0` | Current public `/chatbot/` ingress reaches the old host's unique service identity ([host route](../../Apps/GaChatbot.Api/Program.cs), lines 94–106), despite the GaApi target in ADR-0005. |
| `GET https://demos.guitaralchemist.com/chatbot/api/chatbot/status` | HTTP 200; reported `isAvailable`, `providerReachable`, and `orchestratorRoundTripOk` all `true`, timestamp `2026-09-25T13:10:36Z` | A reported readiness signal, not an independently validated answer. |
| `GET .../chatbot/api/chatbot/examples` and `/qa-summary` | Both HTTP 200; QA summary 12,813 bytes | Page dependencies load. The `/qa-summary` action exists in [GaChatbot.Api](../../Apps/GaChatbot.Api/Controllers/ChatbotController.cs) at line 248, not GaApi's controller. The summary contains older prompt validations, not today's measured pass rate. |
| `GET https://demos.guitaralchemist.com/dev-data/manifest` | Timed out after 15 seconds | Separate development-manifest surface is slow or unavailable under this bound. This is neither proof of a chatbot outage nor proof of a permanent manifest outage. [Issue #724](https://github.com/GuitarAlchemist/ga/issues/724) records both prior failures and recoveries. |
| Daily corpus | [2026-09-25 snapshot](https://github.com/GuitarAlchemist/ga/blob/main/state/quality/chatbot-qa/2026-09-25.json): `total_prompts: 69`, `degraded: true`, `pass_pct: null`, last-known-good `98.08` from `2026-07-19` | Current CI did not measure answer quality. The [issue #328 update](https://github.com/GuitarAlchemist/ga/issues/328#issuecomment-5831099586) reports Ollama preflight false and OPTIC-K preflight true; its generic prose saying both are absent overstates that day's evidence. |

The old chatbot-only 502 was a historical incident, not a current diagnosis. The live GETs above refute a claim that this public page was returning 502 at the observation time, but say nothing about a full POST/SSE exchange, sustained availability, or the React `/chatbot` route.

Source and ticket comparison reveals further limits:

- [Issue #560](https://github.com/GuitarAlchemist/ga/issues/560) identifies a register gap in the *routing-eval* baseline: its 10 chord-info cases were symbol-shaped. The prompt corpus now has active G7/C-major/B-minor chord-tone guards ([`prompts.yaml`](../../Tests/Apps/GaChatbot.Api.Tests/Corpus/prompts.yaml), lines 590–597 and 666–683), but the committed [`routing-eval-2026-06-17.json`](../../state/quality/routing-eval-2026-06-17.json) predates them. A green older routing ratchet does not establish the public route's present answer shape.
- [Issue #703](https://github.com/GuitarAlchemist/ga/issues/703) correctly points to a streaming hook gap at this checkout: [`AnswerStreamingAsync`](../../Common/GA.Business.Core.Orchestration/Services/ProductionOrchestrator.cs), lines 162–303, invokes `OnRequestReceived` but not `OnBeforeSkill`, `OnAfterSkill`, or `OnResponseSent`; the non-streaming branch does invoke them (lines 420–502). This blocks claims that streaming and JSON have equal hook effects, including memory/analytics behavior.
- [Issue #726](https://github.com/GuitarAlchemist/ga/issues/726) identifies a `https://localhost:7001` default in the separate React `GAChatPanelPage` route ([`main.tsx`](../../ReactComponents/ga-react-components/src/main.tsx), lines 366–376 and 483–485). This can break that React route, but the currently observed public `/chatbot/` is the GaChatbot.Api HTML page with relative `api/chatbot/...` calls; the two should not be conflated.
- The already merged `428729b5` adds [`ArpeggioIntentService`](../../Common/GA.Business.Core.Orchestration/PerformanceIntents/ArpeggioIntentService.cs), a validator, and deterministic [tests](../../Tests/Common/GA.Business.Core.Tests/Orchestration/ArpeggioPerformanceIntentTests.cs). `rg` finds the service in DI registration and tests, with **no production call to `SuggestAsync`**. The tests explicitly omit the Ollama HTTP hop. The validator checks parseability, matching roots, and whether the *chord* is diatonic; it does not validate the proposed mode text, every arpeggio tone, or `degrees[]`. Thus neither registration nor unit tests prove the proposed #589 contract is user-visible or complete.
- [Issues #567](https://github.com/GuitarAlchemist/ga/issues/567), [#614](https://github.com/GuitarAlchemist/ga/issues/614), and [#554](https://github.com/GuitarAlchemist/ga/issues/554) are closed, but closure alone is not correctness evidence. At this SHA, [`GuitaristProblemTools.cs`](../../GaMcpServer/Tools/GuitaristProblemTools.cs) still scores keys using chord roots only (lines 173–204) and still builds `arpeggio = chord + arpeggioSuffix` (line 482). The E-flat relative-minor corpus repro remains `skip: true` ([`prompts.yaml`](../../Tests/Apps/GaChatbot.Api.Tests/Corpus/prompts.yaml), lines 653–662). These are distinct from the current public `ImprovisationSkill` path and must be assessed before promising cross-surface coach correctness.

## 5. Verdict: first tracer and acceptance

**Provisional answer:** start with the *existing public SSE* and one useful prompt, `which arpeggio fits Am F C G`, using `ImprovisationSkill`'s per-chord result ([source](../../Common/GA.Business.ML/Agents/Skills/ImprovisationSkill.cs), lines 217–260). This directly advances the first musical-advice part of #623, uses a current visitor route, and avoids making an unconnected structured-output service the first production dependency. The implementation task should first record a baseline, then add only the missing end-to-end guard and any defect that guard actually finds.

Acceptance for that bounded execution task:

1. A real browser visit to `/chatbot/` loads the page and its relative status endpoint, submits the prompt, and displays a complete answer. Capture the page, request URL, timestamp, response status, elapsed time, and answer as reviewable evidence; do not infer host identity from route names alone.
2. The public SSE has a `routing` frame naming `skill.improvisation`, nonempty answer text, a terminal `[DONE]`, and no error frame. The UI shows the same answer. Measure time to first text and total completion before setting a latency threshold.
3. A deterministic oracle checks one suggestion per input chord, in order; `Am` has a minor-compatible arpeggio; no `Amm7`; suggestions preserve each written chord's quality. Add a borrowed/secondary-chord counterexample (A major in C major) that must not silently receive Aeolian/natural-C advice. Invalid or ambiguous input must visibly decline or state uncertainty, not invent certainty.
4. Add the tracer and its negative/neighboring cases to the corpus and the routing-eval **input** set, then require a real, non-degraded run with route, answer-shape, and forbidden-content assertions. Record failure rate and latency alongside the snapshot; `pass_pct: null` or a carried-forward value is not a pass.

**Rollback signal for a subsequent rollout:** restore the previous deployed version or ingress target if the public page/status loses availability, the controlled stream loses its terminal frame, a musical oracle fails, or error/latency worsens beyond the measured pre-change baseline. Do not delete the old host until the rollback path and GaApi parity have been exercised. No deployment or rollback was performed in this study.

**Confidence: medium** for the current route and code gaps, because first-party endpoints and source agree; **low** for actual answer quality, because no POST, browser run, live provider corpus, or independent model validation was performed. This study remains `active` under the [research protocol](README.md) until those checks and an independent verdict are recorded. No one-way schema/API change is authorized here.

## 6. Keep blocked / decisions to make

- Keep GaChatbot.Api retirement and cloudflared repointing blocked by ADR-0005's parity, live ingress, and rollback evidence. Current source-level GaApi REST/history and AG-UI work is useful but not a public migration proof.
- Keep the full #623 progression-to-voicing coach gated on canonical key detection and arpeggio correctness across the specific paths it will call, a held-out progression/alternate-tuning corpus, valid voicing paths, and measured public latency. Closed dependent issue labels do not override the surviving MCP code.
- Keep #589's structured-output path out of user-facing claims until it has a production caller, an actual Ollama/HTTP round trip, and a validator that checks the claims it renders. Decide whether it improves a measured failure of the simpler deterministic path before promoting it.
- Decide whether the React `/chatbot` route is intended to replace the public HTML page; if yes, fix and verify the `:7001` default as part of that route's own tracer. Decide what hook behavior is required on streams before claiming JSON/SSE parity. Choose a real CI QA environment or explicitly label daily quality as operator-only; the current null signal cannot gate a release.
