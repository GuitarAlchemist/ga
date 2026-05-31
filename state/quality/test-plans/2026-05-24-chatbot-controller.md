---
title: ChatbotController High-Value Test Plan
target: Apps/ga-server/GaApi/Controllers/ChatbotController.cs
status: draft
generated_at: 2026-05-24T19:30:00Z
generator: claude-opus-4-7
business_value_confidence: 0.95
effort_tshirt: M
---

# ChatbotController High-Value Test Plan

`ChatbotController` is the **SSE chatbot ingress** for the whole product —
every browser-side chat call and every MCP-tool `chat` call passes through
its three endpoints (`POST /chat/stream`, `POST /chat`, `GET /status`,
`GET /examples`, `GET /demo`). Annotated `@ai:business-value conf=0.95`
in PR #358.

## Coverage gap summary

Existing tests (`Tests/Apps/GaApi.Tests/Controllers/ChatbotControllerTests.cs`,
`ChatbotShowcaseSmokeTests.cs`, `Tests/Apps/GaChatbot.Api.Tests/Controllers/*`)
cover the happy-path GET surface and an SSE contract-only round trip — but
do **not** cover:

- The **SSE wire-protocol contract** (routing frame shape, sentence chunking,
  `[DONE]` terminator) end-to-end.
- The **concurrency-gate 503/200-SSE-error** split per the `VULN-004` trade-off
  comment (busy gate must surface as SSE error frame on `/chat/stream`, but
  as `503` JSON on `/chat`).
- The **`HttpChatSessionCookie.GetOrIssue` ordering invariant** that landed in
  Phase C P1 (must run *before* `Response.StartAsync` — caught a real bug 2026-05-07).
- **Cookie issuance discipline**: never minted on shape-invalid (400) requests,
  never minted on busy (`/chat` 503) requests.
- The **trace + grounding contract** on the SSE routing frame (codex-CLI 2026-05-08
  risk-list item 3 — "SSE parity is easy to miss").

## Test cases (8 proposed)

| # | Name | Type | What it covers | Fixtures / mocks | Overlaps |
|---|---|---|---|---|---|
| 1 | `ChatStream_RoutingFrame_HasRequiredKeys` | integration | First SSE event is `data: {type:"routing", agentId, confidence, routingMethod, grounding, trace}`; missing any key fails. | `WebApplicationFactory<Program>` + stub `IChatApplicationService` returning canned `ChatResponse`. | partial: `ChatbotControllerTests.GetStatus_*` (no SSE assertion). |
| 2 | `ChatStream_TerminatorIsDataDone` | integration | Stream terminates with `data: [DONE]\n\n` after sentence chunks; client can rely on it. | same as #1. | none. |
| 3 | `ChatStream_EmptyMessage_Returns400_NoCookie` | unit | `BadRequest` + SSE error written **and** no `ga-chat-session` cookie set (matches Phase C P1 invariant). | `Microsoft.AspNetCore.TestHost` minimal pipeline. | none. |
| 4 | `ChatStream_BusyGate_WritesSseError_NotStatus503` | integration | When `ILlmConcurrencyGate.TryEnterAsync` returns false on `/chat/stream`, response is HTTP 200 + an SSE error frame (not 503 — headers already started). | stub `ILlmConcurrencyGate` rigged to deny. | none. |
| 5 | `Chat_BusyGate_Returns503Json` | integration | Symmetric: same gate denial on `POST /chat` returns `503` + JSON `{error}` (no SSE). | same stub. | none. |
| 6 | `Chat_SessionCookie_IssuedExactlyOnce_Then_Reused` | integration | First `/chat/stream` call mints `ga-chat-session`; second call with the cookie set reuses the same `sessionId` value into `IChatApplicationService.ChatAsync`. | capture-by-stub `IChatApplicationService` that records `ChatRequest.SessionId`. | none. |
| 7 | `Chat_CookieNotIssued_OnShapeInvalidOr503` | unit | No `ga-chat-session` cookie set when message is empty or gate returns busy on `POST /chat`. | controller + stub deps; assert `HttpContext.Response.Cookies` is empty. | none. |
| 8 | `GetDemo_Returns12Prompts_AcrossKnownCategories` | unit | `GET /demo` returns version `"1.1"`, exactly 12 prompts, with the five category IDs the React showcase expects (`theory`, `scales-keys`, `progressions`, `operations`, `getting-started`). | direct `new ChatbotController(...)`. | `ChatbotShowcaseSmokeTests` covers semantics but not category-ID stability. |

## Suggested file locations

- `Tests/Apps/GaApi.Tests/Controllers/ChatbotControllerSseTests.cs` (cases #1, #2, #4, #6, #8).
- `Tests/Apps/GaApi.Tests/Controllers/ChatbotControllerCookieTests.cs` (cases #3, #5, #7).

## Effort estimate

**M** (medium). Most cases reuse the existing `TestWebApplicationFactory<Program>`
harness; cookie + SSE-frame assertions need a small `HttpResponseMessage` SSE
parser helper (≈30 LOC, reusable). Estimate 1–2 dev-days.

## Rubric

These tests focus on **contract invariants** (SSE shape, cookie ordering,
gate-denial code path) over implementation coverage. Each one corresponds to
a known one-way door already documented in code comments — failing the test
means a documented contract regressed.
