---
title: "replay-and-diff CI check fails on every PR (repo-wide red)"
category: integration-issues
component: semantic-regression-chatbot.yml
date: 2026-05-31
tags: [ci, github-actions, ollama, openai, chatbot, false-failure, infra]
status: solved
mode: compact-safe
---

## Problem

The `replay-and-diff` job (`.github/workflows/semantic-regression-chatbot.yml`)
failed on **every PR**, regardless of content — repo-wide red noise that masked
real check signal. The visible error was:

```
::error::OPENAI_API_KEY secret is not configured. Cannot embed answers.
```

## Root cause (two layers)

1. **Surface:** the job's first step guards on the `OPENAI_API_KEY` secret (used
   to embed answer pairs with `text-embedding-3-small` for the semantic diff). The
   secret was never configured → guard failed.
2. **Deeper (the real blocker):** this is a **live-model integration test** — it
   boots `GaChatbot.Api` and replays golden prompts, which needs **Ollama**
   (`llama3.2:3b` + `nomic-embed`) for the chatbot to generate answers.
   GitHub-hosted runners have **no Ollama**, so even with the key set the chatbot
   never becomes ready: `SemanticIntentRouter` warmup throws
   `Connection refused (localhost:11434)` and `Wait for chatbot ready` times out.
   The `OPENAI_API_KEY` powers only the *diff* step, NOT the chatbot's own runtime.

## Investigation

- Set the secret from the local env (`gh secret set OPENAI_API_KEY --body "$OPENAI_API_KEY"`).
- Triggered a `workflow_dispatch` run (sample_size=3) → the `Guard — OPENAI_API_KEY`
  step now **passed**, but `Wait for chatbot ready` **failed**.
- `gh run download` → `artifacts/chatbot.log` showed `Connection refused
  (localhost:11434)` in `SemanticIntentRouter.EnsureExamplesEmbeddedAsync` → confirmed
  no Ollama in CI. So the key was necessary but not sufficient.

## Solution

1. **Set the `OPENAI_API_KEY` GitHub Actions secret** (fixes the diff step).
2. **Make the workflow `workflow_dispatch`-only** — drop the `pull_request` trigger
   (PR #388). It stops reddening every PR while staying runnable on demand where a
   model runner exists (a dev machine or self-hosted runner with Ollama).
3. **Do NOT point the chatbot at OpenAI in CI** — the golden traces were generated
   with `llama3.2:3b`; OpenAI-generated answers would show false "regressions."

## Prevention

- **Live-model integration tests don't belong as blocking PR checks on model-less
  hosted runners.** Gate them to `workflow_dispatch` / a self-hosted runner that
  provisions the model. Re-add `pull_request` only once CI has a model runner.
- **When a CI check fails identically on *every* PR (including content-unrelated
  ones), suspect infra/env, not the diff.** Verify by dispatching a run and reading
  the job log/artifacts before assuming the change is at fault.
- A guard that hard-fails on a missing secret can **mask a deeper missing
  dependency** — clear the guard, then re-verify the downstream actually works.

## Cross-references

- Memory: `reference_openai_key_locations` (where the key lives + rotation).
- Memory: `project_ga_chatbot_oos_path` (why the chatbot needs Ollama at runtime).
- PRs: #388 (dispatch-only); secret set 2026-05-31.
