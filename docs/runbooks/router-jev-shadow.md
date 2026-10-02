# Router Jev shadow — runbook

`JevRoutingShadow` asks TypeSafe Jev (System One, model pinned to `jev-1.13.0`)
which intent should handle each query the `SemanticIntentRouter` scores. It logs
Jev's pick next to production's and **never changes the routing result**.

It is OFF by default. **Turning it on sends every scored user message to
api.typesafe.ai.** That is an operator decision, not a code default.

## Why

ix Stage 3 (ix#363, `state/router-spike/RESULTS.md`) compared the routers on 221
model-written prompts covering all 32 production skills:

| Router | In-scope (190) | Out-of-scope declined (31) |
|---|---|---|
| Production router (bge-large @ 0.64) | 121 | 24 |
| Jev zero-shot, given the production `IIntent.Description`s | 178 | 31 |

That set is model-written. The shadow measures the same comparison on real
traffic, which no set so far contains.

## Enable

Set these in the GaChatbot.Api process environment, then restart it:

```powershell
$env:GA_ROUTER_JEV_SHADOW = "1"
$env:TYPESAFE_API_KEY = "<key>"                    # never logged
$env:GA_ROUTER_JEV_SHADOW_BUDGET_USD = "0.05"      # optional; default 0.05
$env:GA_ROUTER_JEV_SHADOW_DIR = "C:\...\shadow"    # optional
```

Without `TYPESAFE_API_KEY` the shadow stays off even when the flag is set.
To disable it, unset `GA_ROUTER_JEV_SHADOW` and restart.

## What it does per query

- It builds the question ix Stage 3 measured. The instructions and the
  `__none__` option are copied verbatim from ix
  `state/router-spike/jev/options-full.json`. The other options are the
  descriptions of the intents production has just scored, so both pick from the
  same set.
- It skips the startup warmup query (`SemanticIntentRouter.WarmupQuery`), which
  is not user traffic.
- It runs in the background: the user's answer never waits for Jev.
- Each call has a 2.5 s timeout. At most 2 calls are in flight at once. A call is
  never retried.
- After a 401, 403 or 429 it stops calling until the process restarts.
- The response is checked fail-closed, as in ix `jev_router.rs`:
  - the model must be the pinned one;
  - the choice must be one of the options sent and carry the highest
    probability;
  - the probabilities must cover exactly the options and sum to 1 within 0.03;
  - token usage must be reported.

## Budget

Each row records `cost_usd`: the reported input and output tokens, both at the
input rate of $0.042 per 1M. The published rate card prices input only, so this
overcounts on purpose. When usage is unknown (timeout, network error), the row
records a conservative estimate of one token per request byte instead.

The cap counts every row already in the log directory, so a restart does not
reset it. To allow more spend, raise `GA_ROUTER_JEV_SHADOW_BUDGET_USD`.

ix Stage 3 averaged about 2,900 input and 400 output tokens per call, which is
$0.00014 counted this way, so $0.05 covers roughly 350 queries.

## Log

Rows go to `{repo-root}/state/telemetry/routing-jev-shadow/YYYY-MM-DD.jsonl`.
That path is git-ignored, and the rows hold real user messages, so keep them out
of commits.

| Field | Meaning |
|---|---|
| `q` | the user message |
| `prod`, `prod_conf`, `margin` | production's pick (`null` = fell through), its score, top-1 minus top-2 |
| `status` | one of `ok`, `timeout`, `http_NNN`, `invalid`, `wrong_model`, `error`, `skipped_busy`, `skipped_budget`, `skipped_stopped` |
| `detail` | why a response was rejected, or an exception type name |
| `jev`, `jev_conf` | Jev's pick (`__none__` = declined) and its confidence |
| `agree` | Jev and production agree; a fall-through agrees with `__none__` |
| `latency_ms`, `input_tokens`, `output_tokens` | for the call |
| `cost_usd`, `cost_known` | what was charged against the budget |
| `options_sha256` | hash of the question sent; do not pool rows with different hashes |

The shadow only sees queries the router scored with embeddings. It does not see
queries that hit the keyword fallback, such as when the embedder is down.

## Reading it

Agreement alone is not accuracy. The disagreements (`agree: false`) are the rows
to label by hand. They show where production and Jev differ on real traffic, and
the labelled rows become the held-out set the arena never had.
