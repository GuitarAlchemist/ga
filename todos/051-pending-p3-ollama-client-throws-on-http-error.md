---
status: pending
priority: p3
issue_id: "051"
tags: [code-review, quality, rop, error-handling, ollama]
---

# OllamaGenerateClient Throws on HTTP Error — ROP Violation

## Problem Statement
`OllamaGenerateClient.GenerateAsync` calls `EnsureSuccessStatusCode()`, which throws `HttpRequestException` on non-2xx responses. This violates the project's Railway-Oriented Programming (ROP) policy: service methods must never throw and must return `Result<T, TError>` instead.

## Proposed Solution
- Replace `EnsureSuccessStatusCode()` with a manual status check (`response.IsSuccessStatusCode`)
- Return `Result<string, OllamaError>` where `OllamaError` captures the HTTP status code and response body
- Define `OllamaError` as a discriminated union or record covering: `HttpError(statusCode, body)`, `NetworkError(message)`, `TimeoutError`
- Update all callers to handle `Result` rather than catching exceptions

**File:** `Common/GA.Business.Core.Orchestration/Clients/OllamaGenerateClient.cs`
*(Note: this file will move to `GA.Business.ML` per todo 048 — coordinate changes)*

## Acceptance Criteria
- [ ] `GenerateAsync` returns `Result<string, OllamaError>`, never throws
- [ ] HTTP 4xx/5xx responses map to `OllamaError.HttpError`
- [ ] Network/socket failures map to `OllamaError.NetworkError`
- [ ] Timeout maps to `OllamaError.TimeoutError`
- [ ] All callers updated to use `Result` pattern (no catch blocks at call sites)
- [ ] Unit tests cover: success, 500 error, network failure, timeout
