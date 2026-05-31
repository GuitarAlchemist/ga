---
title: VoicingsController High-Value Test Plan
target: Apps/ga-server/GaApi/Controllers/VoicingsController.cs
status: draft
generated_at: 2026-05-24T19:30:00Z
generator: claude-opus-4-7
business_value_confidence: 0.90
effort_tshirt: S
---

# VoicingsController High-Value Test Plan

`VoicingsController` is the **REST surface for voicings** —
`POST /api/voicings/retrieve` wraps `ISemanticKnowledgeSource` so MCP tools,
test harnesses, and observability hit the 313k-voicing OPTIC-K index without
the chatbot. Annotated `@ai:business-value conf=0.90`.

## Coverage gap summary

`Tests/Apps/GaApi.Tests/Controllers/VoicingsControllerTests.cs` is decent for
a 78-line controller — already asserts `BadRequest` on empty query and limit
clamping. Gaps:

- No assertion that the response **`SchemaVersion`** field is locked at `"v1"`
  (a one-way door for MCP consumers).
- No coverage of **`Rank` ordering** (must match service-returned order, 0-based).
- No assertion that `ISemanticKnowledgeSource` is called with the **clamped**
  limit, not the raw input.
- No `CancellationToken` plumbing test — controller passes a token through and
  must honor it.
- No `WebApplicationFactory` integration test that the route is actually
  registered at `/api/voicings/retrieve` (every other controller has one).
- No log-line shape assertion (`voicing-retrieve query=... limit=... returned=... latency_ms=...`)
  — observability surface for the live retrieval-quality dashboard.

## Test cases (6 proposed)

| # | Name | Type | What it covers | Fixtures / mocks | Overlaps |
|---|---|---|---|---|---|
| 1 | `Retrieve_ResponseSchemaVersion_IsV1` | unit | `VoicingRetrieveResponse.SchemaVersion == "v1"`; pin against accidental bump that would break MCP callers. | extends existing `StubKnowledge` from the current test class. | none. |
| 2 | `Retrieve_RankPreservesServiceOrder` | unit | Returned `Results[i].Rank == i`; first item is the highest-scored snippet from the stub. | `StubKnowledge` returning 3 ordered results. | partial: `Retrieve_ClampsLimitAndEnvelopesResults` checks count, not order. |
| 3 | `Retrieve_ClampedLimit_FlowsToService` | unit | `request.Limit = 999` calls the service with `limit=50`, not `999`. | extend `StubKnowledge` to record observed `limit`. | partial: existing test asserts the clamp in the envelope but not the inbound limit. |
| 4 | `Retrieve_HonorsCancellationToken` | unit | Cancelling the token before the await throws `OperationCanceledException` (not silently swallowed). | `TaskCompletionSource`-backed knowledge source. | none. |
| 5 | `Retrieve_RouteIsRegistered` | integration | `POST /api/voicings/retrieve` returns 200 from `WebApplicationFactory<Program>` with the real DI graph (catches route-misregistration). | `WebApplicationFactory<Program>` with `ISemanticKnowledgeSource` replaced by a fake in `ConfigureTestServices`. | none. |
| 6 | `Retrieve_StructuredLog_HasQueryLimitCountLatency` | unit | Logger receives exactly one info entry with `query`, `limit`, `returned`, `latency_ms` template tokens — the four fields the dashboard parses. | `TestLoggerProvider`-captured `LogValues`. | none. |

## Suggested file locations

- Extend the existing `Tests/Apps/GaApi.Tests/Controllers/VoicingsControllerTests.cs`
  for cases #1–#4 and #6 (unit).
- New file `Tests/Apps/GaApi.Tests/Controllers/VoicingsControllerIntegrationTests.cs`
  for case #5.

## Effort estimate

**S** (small). The controller is tiny, an existing stub pattern is in place, and
five of the six cases extend that file directly. Estimate 0.5–1 dev-day.

## Rubric

This file is **easy** to fully cover — its small surface and pure-passthrough
design make it the highest coverage-bang-per-buck of the 8 high-value files.
Worth doing first as a quick win.
