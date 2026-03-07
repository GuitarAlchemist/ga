---
status: pending
priority: p3
issue_id: "021"
tags: [security, secrets, code-review]
dependencies: []
---

# 021 — OpenAI ApiKey Field Present in Committed appsettings.json

## Problem Statement
`Apps/ga-server/GaApi/appsettings.json:13` contains an `"OpenAI": { "ApiKey": "", "Model": "..." }` section. The field is empty now, but its presence in committed config creates a risk: a developer may populate it locally and accidentally commit a real secret.

## Findings
- `appsettings.json` is tracked by git and therefore part of every clone and PR diff.
- An empty `ApiKey` field is a standing invitation to fill it in locally.
- Run `git log -S "sk-"` to confirm no real key was ever committed (should be done before closing this issue).

## Proposed Solutions
1. Remove the `OpenAI.ApiKey` field from `appsettings.json` entirely.
2. Source the key exclusively from an environment variable (e.g. `OPENAI__APIKEY`) or .NET user secrets (`dotnet user-secrets set "OpenAI:ApiKey" "sk-..."`).
3. Add a comment in `appsettings.json` noting that secrets must come from environment variables or user secrets, never from this file.
4. Optionally add a `.gitignore` rule or pre-commit hook grep for `sk-` prefixes.

## Recommended Action

## Technical Details
- **Affected files:**
  - `Apps/ga-server/GaApi/appsettings.json` (line 13)

## Acceptance Criteria
- [ ] `appsettings.json` contains no `ApiKey` field (empty or otherwise) for any external service.
- [ ] `git log -S "sk-"` returns no results confirming no key was ever committed.
- [ ] Developer setup docs (or a comment in `appsettings.json`) explain how to supply the key via user secrets or environment variable.
- [ ] CI pipeline has no hardcoded key.

## Work Log
- 2026-03-07 — Identified in /ce:review of PR #2

## Resources
- PR: https://github.com/GuitarAlchemist/ga/pull/2
