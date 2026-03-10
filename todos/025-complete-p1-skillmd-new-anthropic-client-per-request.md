---
status: complete
priority: p1
issue_id: "025"
tags: [performance, architecture, anthropic, http-client, code-review]
dependencies: []
---

# 025 — `SkillMdDrivenSkill` Creates a New `AnthropicClient` Per Request

## Problem Statement

`SkillMdDrivenSkill.ExecuteAsync` instantiates a new `AnthropicClient` and constructs a full `IChatClient` middleware chain (including `UseFunctionInvocation()`) on **every single call**. HTTP clients are expensive to create — they do not share connection pools, TLS handshakes are repeated, and .NET's socket exhaustion risk is real under concurrent load.

## Findings

`Common/GA.Business.ML/Agents/Skills/SkillMdDrivenSkill.cs` lines 75–84:
```csharp
var anthropicClient = new AnthropicClient { ApiKey = apiKey };
chatClient = anthropicClient
    .AsIChatClient(model)
    .AsBuilder()
    .UseFunctionInvocation()
    .Build();
```

This is inside `ExecuteAsync` which is called on every matching chatbot request. Three parallel requests = three `AnthropicClient` instances, three TLS handshakes, three connection pool allocations.

The existing `GuitarAlchemistAgentBase` correctly accepts `IChatClient` via DI (singleton per agent) — `SkillMdDrivenSkill` regresses this pattern by constructing its own client.

## Proposed Solutions

### Option A — Inject `IChatClient` via DI (Recommended)
Register a named or typed `IChatClient` in `GaPlugin` / `SkillMdPlugin` that wraps `AnthropicClient` + `UseFunctionInvocation`. Inject it into `SkillMdDrivenSkill` via primary constructor. Matches the existing agent pattern.

- **Pros:** Consistent with all other agents; connection pool shared; easy to test.
- **Cons:** All SKILL.md skills share the same client configuration (same model). Use `IConfiguration` to keep model configurable.
- **Effort:** Small
- **Risk:** Low

### Option B — Lazy singleton per skill via `Lazy<IChatClient>`
Cache the built client in a `Lazy<IChatClient>` field on `SkillMdDrivenSkill` (which is itself registered as singleton).

- **Pros:** No DI registration change; client built once per skill instance.
- **Cons:** `ApiKey` is read at construction time, not request time — fine for env-var approach.
- **Effort:** Small
- **Risk:** Low

### Option C — `IHttpClientFactory`-backed `AnthropicClient`
Register `AnthropicClient` via `IHttpClientFactory` so connection pooling is handled by the factory.

- **Pros:** Matches .NET best practices for HTTP client lifetime.
- **Cons:** Requires NuGet support from the Anthropic SDK — may not be available.
- **Effort:** Medium
- **Risk:** Medium (SDK dependency)

## Recommended Action
Option A or B. Option A is cleanest and consistent with the existing codebase pattern.

## Technical Details

**Affected files:**
- `Common/GA.Business.ML/Agents/Skills/SkillMdDrivenSkill.cs` (lines 60–85)
- `Common/GA.Business.Core.Orchestration/Plugins/GaPlugin.cs` (DI registration)
- `Common/GA.Business.ML/Agents/Plugins/SkillMdPlugin.cs` (registration)

**Components:** `SkillMdDrivenSkill`, `AnthropicClient`, `IChatClient`, `IMcpToolsProvider`

## Acceptance Criteria

- [ ] `SkillMdDrivenSkill` does not create a new `AnthropicClient` in `ExecuteAsync`
- [ ] The built `IChatClient` is created at most once per skill instance (singleton lifetime)
- [ ] `ForTesting()` still works — test seam not broken
- [ ] 3 concurrent calls do not create 3 HTTP connections

## Work Log

- 2026-03-10: Identified during `/ce:review` of PR #8 (`feat/chatbot-orchestration-extraction`)
