# Runbook: chatbot-qa producer revival

**Producer:** `.github/workflows/chatbot-qa-snapshot.yml` → `Scripts/run-prompt-corpus.ps1 -Snapshot`
**Output:** `state/quality/chatbot-qa/YYYY-MM-DD.json` (one per day)
**Consumers:** `ix-quality-trend` (`ix/crates/ix-quality-trend/src/snapshot.rs`, `ChatbotQaSnapshot`), the quality dashboard, the routing-eval gate, and hari issues #13/#20.
**Freshness sensor:** `.github/workflows/chatbot-qa-freshness.yml` (fails loudly when the newest snapshot is stale).

## What died, and why

The daily producer has landed **nothing since 2026-06-19**. The `2026-07-19.json`
on `main` is not a producer run — its timestamp carries a local `-04:00` offset,
i.e. it was hand-run and committed via #553. The scheduled producer itself is dead.

Root cause: **every scheduled run since 2026-06-21 is `cancelled` at the 60-minute
job timeout**, during the `Run corpus + emit snapshot` step.

Evidence:

```
$ gh run list --workflow chatbot-qa-snapshot.yml --repo GuitarAlchemist/ga
completed  cancelled  ... main  schedule  1h0m21s  2026-07-20T08:34Z
completed  cancelled  ... main  schedule  1h0m22s  2026-07-19T07:54Z
...every scheduled run, 1h0mXX each...
$ gh run view <id>
ANNOTATIONS
  X The job has exceeded the maximum execution time of 1h0m0s
  X The operation was canceled.        (step: Run corpus + emit snapshot)
```

The trigger was **PR #411 (2026-06-16, "in-runner Ollama")** + **#409 (2026-06-20,
CF Access headers)**. #411 made the hosted runner install Ollama, pull
`nomic-embed-text` + `llama3.2:3b`, and run ~50 **CPU** inference prompts. That does
not fit in `timeout-minutes: 60` on a 2-core `ubuntu-latest` runner, so GitHub
cancels the whole job at the wall — **before** the `Commit snapshot if produced`
step ever runs. No snapshot is committed; no degraded snapshot either.

Why it stayed silent for a month:

- The job shows as **`cancelled`**, not `failed` — no red X on a PR, no default alert.
- The producer's own degradation guards (algedonic signal + `[meta] Chatbot QA
  degradation` issue) only fire when the run *reaches* them. A run killed at the
  timeout never reaches any guard.
- Nobody watches this scheduled workflow's tab.

This is the same green-but-dead shape that silently broke `readme-drift-sensor.yml`
for 5+ weekly runs (see its header comment) and the `feedback_green_but_dead` rule.

Note: **#553 and #564 do not fix this.** #553 (merged) corrected a *different* bug —
a fabricated `pass_pct: 7.69, degraded: false` for a degraded environment — via the
`GA_CORPUS_ENV_DEGRADED` plumbing; it does not touch the timeout. #564 (open,
`feat/chatbot-llm-judge-invariants`) adds an LLM-as-judge to `run-prompt-corpus.ps1`
and the tests; it does not touch the workflow and, being more work per prompt, can
only make the timeout worse.

## Immediate unblock (manual, free, ~2 min of your time)

Run the corpus locally against your warm Ollama and commit the snapshot:

```pwsh
pwsh Scripts/run-prompt-corpus.ps1 -Snapshot
# writes state/quality/chatbot-qa/<today>.json (+ last.json, loop-history.jsonl)
git add state/quality/chatbot-qa/<today>.json
git commit -m "chore(quality): chatbot-qa snapshot <today> [skip ci]"
```

That is exactly how the `2026-07-19` snapshot got there. It clears the freshness
sensor but does **not** revive the automation — do one of the durable fixes below.

## Durable fix — this is an OWNER DECISION (pick one)

All three restore a daily snapshot; they differ in cost and in whether CI measures
a *real* `pass_pct` or an honest *degraded* one. I did **not** apply any of these,
because each changes the producer's declared behaviour (`#411`'s "measure real
pass_pct in-runner" intent) or spends money/infra — an owner call, not a silent one.

1. **Gate the in-runner Ollama off by default → free daily *degraded* snapshots.**
   Make the `Install + warm in-runner Ollama (default backend)` step opt-in (e.g.
   `if: vars.CHATBOT_QA_INRUNNER_OLLAMA == 'true'`). With it off, the corpus runs
   fast, the chatbot degrades gracefully, and — thanks to #553 — the snapshot is
   written honestly (`pass_pct: null, degraded: true, carryforward`) and committed
   within budget. **Smallest revival; restores freshness at zero cost; loses real
   measurement until option 2/3.** Recommended as the immediate automation fix.

2. **Provision the remote backend → real `pass_pct`.** Follow
   [`chatbot-qa-ollama-ci-endpoint.md`](chatbot-qa-ollama-ci-endpoint.md): stand up
   the Cloudflare-Access-fronted Ollama and set `OLLAMA_BASE_URL` +
   `OLLAMA_CF_ACCESS_CLIENT_ID/SECRET` + `OPTICK_INDEX_URL`. The workflow already
   routes to it when those secrets are set. Costs the box + tunnel; gives real data.

3. **Move the job to a self-hosted runner** with Ollama + the OPTIC-K index warm,
   and raise/remove `timeout-minutes`. Real measurement, but ties the daily signal
   to that runner's uptime.

Recommendation: ship **(1)** now to stop the bleeding (free, restores the daily
artifact the four readers need), then do **(2)** when someone wants live `pass_pct`
back. Whichever you pick, the freshness sensor stays as the tripwire so this can't
silently rot again.

## Verify after fixing

```pwsh
gh workflow run chatbot-qa-snapshot.yml --repo GuitarAlchemist/ga   # dispatch the producer
gh workflow run chatbot-qa-freshness.yml --repo GuitarAlchemist/ga  # then the sensor
```

Green sensor + a `state/quality/chatbot-qa/<today>.json` committed by
`github-actions[bot]` = revived.
