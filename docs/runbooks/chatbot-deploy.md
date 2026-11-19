---
title: Chatbot deploy runbook (retired)
status: retired
date: 2026-11-18
related:
  - docs/runbooks/gaapi-deploy.md
  - docs/adr/0005-gaapi-single-canonical-chat-host.md
---

# Chatbot deploy runbook (retired)

GaChatbot.Api, the host this runbook deployed on `:5252`, was retired on 2026-11-18. The public chat routes (`/chatbot/*`, `/api/chatbot/*`, `/api/a2a/*`) are served by the production GaApi on `:5262` ([ADR-0005](../adr/0005-gaapi-single-canonical-chat-host.md)).

- Deploy, configuration, operator settings and common failures: [gaapi-deploy.md](gaapi-deploy.md).
- `Scripts/deploy-chatbot.ps1`, the scheduled task `GA-Chatbot-5252` and the `Apps/GaChatbot.Api` project are gone. The worktree `..\ga-deploy-chatbot` still holds the last release. To bring the old host back, revert the retirement commit, run the restored `Scripts/deploy-chatbot.ps1` (it recreates the task) and point the three cloudflared chat rules back at `:5252`.
