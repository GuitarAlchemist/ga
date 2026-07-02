---
module: afk-delegation
tags: [afk-router, jules, claude-code-action, github-events-api, cloudflare-530, post-merge-smoke, agent-feed]
problem_type: silent-failure-chain
---

# AFK delegation chain: four silent failure modes found and fixed (2026-07-01/02)

One evening of bottleneck-hunting surfaced four independent, silent failure
modes in the cross-repo agent delegation chain. All are fixed; recorded here
because each one *looked* like "the agent ignored us" while being mechanical.

## 1. Router hard-failed: the `jules` label never existed in ga

`gh issue edit --add-label jules` fails with `'jules' not found` if the label
was never created in the repo (gh resolves labels via GraphQL IDs; it does NOT
auto-create). tars had the label, ga didn't → ga's router could never delegate.

**Fix (ga#494, mirrored tars#173):** `gh label create jules --force` before
every apply — idempotent create + repairs color/description.

**Rule:** event-triggered automations must create their own resources
idempotently; never assume a sibling repo's setup happened here too.

## 2. Router dead-letter: `labeled` events are not re-emitted

An issue labeled `ready-for-agent` *before* the router workflow existed will
never produce the event again — it strands silently (ga#328 sat for 5+ weeks).

**Fix (same PRs):** daily `schedule` + `workflow_dispatch` sweep job that
re-routes open `ready-for-agent` issues lacking the `jules` label. Jules lane
only — comment-triggered lanes (`worker:codex`/`worker:claude`) are skipped to
avoid duplicate @-mentions; re-apply the label to re-fire those.

## 3. `@claude` lane: two stacked causes, read the logs in order

- `anthropics/claude-code-action@v1` requires `actions/checkout` first; without
  it, branch setup dies with `fatal: not a git repository` (tars run
  28550132148). ga had the checkout, tars didn't.
- After that fix, env validation still fails if neither
  `CLAUDE_CODE_OAUTH_TOKEN` (subscription, $0/call) nor `ANTHROPIC_API_KEY`
  (pay-per-use) is set. The org secret was empty — visible as
  `CLAUDE_CODE_OAUTH_TOKEN:` (blank) in the action's env dump.

**Cost doctrine (tars#176):** API-key fallback ONLY on the mention-triggered
lane (human-initiated, bounded). Per-PR auto-review (`claude-code-review.yml`)
stays subscription-only and skips green when the token is absent — never wire
pay-per-use into anything that fires per-PR.

## 4. Observability quirks found while building the agent feed

- GitHub **Events API** does not reliably populate
  `payload.pull_request` on `PullRequestEvent` — build PR URLs from
  `repo + payload.number` (always present) and treat title as optional
  (.github#35).
- A down cloudflared tunnel surfaces as **HTTP 530** (Cloudflare error 1033),
  not connection-refused — post-merge-smoke's infra-down suppression only knew
  502/503/504, so every merge during an outage opened a duplicate regression
  issue (#493/#495/#496). Classify 52x/530 as infra-down (ga#497).
- raw.githubusercontent.com caches ~5 min; verify fresh publishes via the
  contents API on the data branch, not the raw URL.

## Where the feed lives

`https://raw.githubusercontent.com/GuitarAlchemist/.github/agent-feed-data/feed.json`
— 15-min cron (`.github` repo, `agent-feed.yml`), signal-only, consumed by
ChatGPT Tasks for cross-vendor observation. The first real read of this feed is
what surfaced failure mode #3.
