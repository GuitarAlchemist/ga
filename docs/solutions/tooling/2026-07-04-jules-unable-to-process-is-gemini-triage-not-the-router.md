---
title: "Jules \"unable to process\" is the gemini-dispatch triage failing, NOT the jules-auto-delegate router"
date: 2026-07-04
problem_type: "tooling"
module: "afk-delegation"
component: ".github/workflows/gemini-dispatch.yml + .github/workflows/jules-auto-delegate.yml"
symptoms:
  - "A `ready-for-agent`+`jules` issue shows three comments: github-actions[bot] \"working on it\" → google-labs-jules[bot] \"Jules is on it [task URL]\" → github-actions[bot] \"I'm sorry, I was unable to process your request\" ~20s later"
  - "Multiple issues (#517/#518/#519) all show the identical pattern, 0 PRs — looks like the whole Jules delegation lane is broken"
  - "The failed run is workflow `gemini_dispatch`, job `triage / triage`, error `Please set an Auth method ... GEMINI_API_KEY / GOOGLE_GENAI_USE_VERTEXAI / GOOGLE_GENAI_USE_GCA` (code 41), `GEMINI_MODEL:` empty"
tags:
  - "github-actions"
  - "jules"
  - "afk-router"
  - "gemini"
  - "delegation"
---

## The trap (a misdiagnosis worth not repeating)

The "unable to process your request" comment reads like Jules failed. It did not.
**Two separate workflows** fire on a `ready-for-agent` issue:

- `gemini-dispatch.yml` (+ `gemini-triage.yml`) — a Gemini-CLI auto-triage/labeler.
  It posts the github-actions[bot] "working on it" / "unable to process" comments.
  It needs `GEMINI_API_KEY` (or a Vertex/GCA auth). When unset, its `triage` job
  exits 1 and posts "unable to process."
- `jules-auto-delegate.yml` — the actual AFK router. Fires on the `ready-for-agent`
  label, applies the `jules` label **with a user-owned `PAT_TOKEN`** (agents ignore
  bot-authored trigger events), which is what dispatches Jules. Jules then posts
  "Jules is on it [task URL]" and works async on Google's side.

So a red `gemini_dispatch` and a "unable to process" comment say **nothing** about
whether Jules was dispatched. On 2026-07-04, `jules-auto-delegate` returned
`success` on all three issues while `gemini_dispatch` was red — Jules was working
the whole time; only the Gemini triage helper was broken.

## How to diagnose correctly

- Check the conclusion of **`jules-auto-delegate.yml`** for the issue (not the
  gemini run). `success` = Jules was dispatched; the presence of a
  `google-labs-jules[bot]` "on it" comment with a `jules.google.com/task/...` URL
  confirms it.
- "unable to process" from **github-actions[bot]** = the Gemini triage/labeler, a
  non-blocking helper. Setting `GEMINI_API_KEY` (`gh secret set GEMINI_API_KEY
  --repo GuitarAlchemist/ga`) clears that noise for future issues — it does **not**
  "unblock Jules," because Jules was never blocked by it.

## Consequence

Do **not** re-trigger issues on the strength of a red `gemini_dispatch` — Jules may
already be working them, and re-triggering risks duplicate Jules tasks/PRs. Jules is
slow (an overnight batch took hours); wait for the async PR. The distinct auth
dependencies are worth remembering: **`jules-auto-delegate` needs `PAT_TOKEN`;
`gemini-dispatch` needs `GEMINI_API_KEY`.** A failure in one is not a failure in the
other.
