---
title: "Security Hardening, Layer Violation Fixes, and Dead Code Removal — Chatbot Orchestration Extraction"
date: "2026-03-07"
category: "compound-reviews"
tags:
  - security
  - rate-limiting
  - prompt-injection
  - layer-violations
  - dead-code
  - rop
  - logging
  - mcp
  - refactoring
  - chatbot
  - orchestration
  - compound-engineering
branch: "feat/chatbot-orchestration-extraction"
status: "solved"
review_report: "docs/solutions/reviews/ce-review-feat-chatbot-orchestration-extraction-2026-03-07.md"
components:
  - "Apps/ga-server/GaApi/Controllers/GaEvalController.cs"
  - "Apps/ga-server/GaApi/Controllers/ChatbotController.cs"
  - "Apps/ga-server/GaApi/Services/IChatService.cs"
  - "Apps/ga-server/GaApi/Services/ChatbotSessionOrchestrator.cs"
  - "Apps/ga-server/GaApi/Program.cs"
  - "Common/GA.Business.Core.Orchestration/Models/ChatModels.cs"
  - "Common/GA.Business.Core.Orchestration/Services/GroundedPromptBuilder.cs"
  - "Common/GA.Business.Core.Orchestration/Services/OllamaGroundedNarrator.cs"
  - "Common/GA.Business.Core.Orchestration/Services/QueryUnderstandingService.cs"
  - "Common/GA.Business.DSL/Closures/BuiltinClosures/AgentClosures.fs"
  - "Common/GA.Business.DSL/Parsers/GaSurfaceSyntaxParser.fs"
  - "Common/GA.Business.DSL/LSP/GaBlockDetector.fs"
  - "Common/GA.Business.DSL/LSP/LanguageServer.fs"
  - "GaMcpServer/Tools/GaDslTool.cs"
  - ".agent/agents/codebase-documenter.md"
---

# CE Review Cycle: Security Hardening, Architecture, and Engineering Hygiene

**Branch**: `feat/chatbot-orchestration-extraction`
**Date**: 2026-03-07
**Driver**: Multi-agent `/ce:review` report — see [full report](../reviews/ce-review-feat-chatbot-orchestration-extraction-2026-03-07.md)
**Commits**:
- `76842b00` — fix(security): address /ce:review findings — security, layer violations, dead code
- `0c67d26c` — refactor(chatbot): address review findings — rename, rate limit, prompt sanitization, docs subagent

---

## Summary

A multi-agent security and architecture review of the chatbot orchestration extraction branch produced 27 findings. This cycle resolved all CRITICAL and HIGH findings plus all addressable MEDIUM and SIMPLICITY items. The changes span five categories: security hardening, layer boundary enforcement, dead code removal, engineering hygiene, and compound flywheel infrastructure.

---

## Security Fixes

### C-1: Eval Endpoint Restricted to Development

**Problem**: `POST /api/ga/eval` executed arbitrary F# scripts via FSI with no environment guard — any deployed instance exposed RCE to unauthenticated callers.

**Root cause**: The controller had no environment check. The endpoint was designed for local dev but was not protected.

**Solution**: Environment guard injected before any script execution via `IWebHostEnvironment`:

```csharp
// GaEvalController.cs
public sealed class GaEvalController(IWebHostEnvironment env) : ControllerBase
{
    [HttpPost("eval")]
    public async Task<IActionResult> Eval([FromBody] GaEvalRequest request, CancellationToken cancellationToken)
    {
        if (!env.IsDevelopment())
            return StatusCode(StatusCodes.Status403Forbidden,
                "Script evaluation is only available in development mode.");
        // ...
    }
}
```

---

### C-2: MCP Closure Allowlist — Side-Effect Category Blocking

**Problem**: `GaInvokeClosure` MCP tool allowed callers to invoke any GA DSL closure by name, including `io.*` (file I/O) and `agent.*` (outbound HTTP) — unauthenticated side-effect access.

**Root cause**: No authorization layer between the MCP surface and the flat F# closure registry.

**Solution**: Prefix-based allowlist check before dispatch:

```csharp
// GaMcpServer/Tools/GaDslTool.cs
private static bool IsPermittedForMcp(string name) =>
    !name.StartsWith("io.", StringComparison.OrdinalIgnoreCase) &&
    !name.StartsWith("agent.", StringComparison.OrdinalIgnoreCase);

public static Task<string> GaInvokeClosure(string name, string paramsJson)
{
    if (!IsPermittedForMcp(name))
        return Task.FromResult(
            $"Error: closure '{name}' is not accessible via MCP (side-effect categories are restricted).");
    return InvokeJsonAsync(name, paramsJson);
}
```

Typed MCP tools (`GaParseChord`, `GaTransposeChord`, etc.) bypass the check because they hardcode safe `domain.*`/`tab.*` closure names.

---

### M-1: Prompt Injection Defense + Unicode Normalization

**Problem**: User query text and DB-sourced candidate fields (display name, shape, summary, tags) were interpolated raw into LLM prompts — full-width Unicode bypass and role token injection were both possible.

**Solution**: NFKD normalization + compiled injection regex applied to all values before prompt construction. DB-sourced fields pass through `SanitizeField` before `sb.AppendLine`:

```csharp
// GroundedPromptBuilder.cs
private static readonly Regex InjectionPattern = new(
    @"(SYSTEM\s*:|USER\s*:|ASSISTANT\s*:|\nHuman\s*:|\nAssistant\s*:|###\s*\n?|```)",
    RegexOptions.IgnoreCase | RegexOptions.Compiled);

private static string SanitizeQuery(string raw)
{
    var normalized = raw.Normalize(System.Text.NormalizationForm.FormKD);
    var sanitized  = InjectionPattern.Replace(normalized, string.Empty);
    sanitized      = Regex.Replace(sanitized, @"\s{3,}", " ").Trim();
    if (sanitized.Length > MaxQueryLength)
        sanitized = sanitized[..MaxQueryLength] + "…";
    return sanitized;
}

private static string SanitizeField(string? raw) =>
    string.IsNullOrWhiteSpace(raw) ? string.Empty
        : InjectionPattern.Replace(raw.Normalize(System.Text.NormalizationForm.FormKD), string.Empty).Trim();
```

---

### M-2: Rate Limiting Wired in Program.cs

**Problem**: All API routes were unprotected from flooding. A TODO comment for `.NET 9` rate limiting had been accumulating since the API was built.

**Solution**: Fixed-window rate limiter registered and middleware activated:

```csharp
// Program.cs
builder.Services.AddRateLimiter(options =>
{
    options.AddFixedWindowLimiter("global", o =>
    {
        o.PermitLimit = 60;
        o.Window = TimeSpan.FromMinutes(1);
        o.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
        o.QueueLimit = 5;
    });
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
});

// middleware pipeline:
app.UseRateLimiter(); // after UseCors, before MapControllers
```

---

## Architecture Fixes

### A-2: VoicingExplanationDto — Layer Boundary DTO

**Problem**: `GA.Business.Core.Orchestration` (Layer 5) directly referenced `VoicingExplanation` from `GA.Business.ML` (Layer 4), violating the five-layer dependency model's contract boundary rule: model contracts in Orchestration must not carry ML types.

**Solution**: Mirror DTO introduced in `ChatModels.cs` in the Orchestration namespace. Services that construct `CandidateVoicing` (which are in Layer 5 and may reference Layer 4) map the ML type to the DTO at the boundary:

```csharp
// Common/GA.Business.Core.Orchestration/Models/ChatModels.cs
/// Mirrors VoicingExplanation from GA.Business.ML without a direct ML dependency.
/// Map from VoicingExplanation at the Orchestration service boundary.
public sealed record VoicingExplanationDto(
    string Summary,
    IReadOnlyList<string> Tags,
    IReadOnlyList<string> Techniques,
    IReadOnlyList<string> Styles,
    double? SpectralCentroid
);
```

Mapping in `SpectralRagOrchestrator.cs`:
```csharp
private static VoicingExplanationDto ToDto(VoicingExplanation e) =>
    new(e.Summary, [..e.Tags], [..e.Techniques], [..e.Styles], e.SpectralCentroid);
```

---

### A-1 (partial): SSL Bypass Conditionalized in AgentClosures.fs

**Problem**: `AgentClosures.fs` unconditionally applied `DangerousAcceptAnyServerCertificateValidator` to its shared `HttpClient`, bypassing TLS validation in all environments.

**Solution**: Guard added — bypass only active when `DOTNET_ENVIRONMENT = "Development"`:

```fsharp
let private httpClient =
    let handler = new HttpClientHandler()
    if System.Environment.GetEnvironmentVariable "DOTNET_ENVIRONMENT" = "Development" then
        handler.ServerCertificateCustomValidationCallback <-
            HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
    let c = new HttpClient(handler)
    c.Timeout <- System.TimeSpan.FromSeconds 30.0
    c
```

**Deferred (A-1 full)**: Full migration of `AgentClosures.fs` from `GA.Business.DSL` (Layer 2) to `GA.Business.Core.Orchestration` (Layer 5) with `IHttpClientFactory`. Breaking change — requires DI wiring across the F#/C# boundary.

---

### A-5: Interface Renamed IOllamaChatService → IChatService

**Problem**: The GaApi chat abstraction was named after its first implementation (Ollama), making it harder to reason about substitutability and leaking infrastructure detail into the interface contract.

**Solution**: Rename across 7 files — interface, 2 implementations (`OllamaChatService`, `ClaudeChatService`), adapter, controller, extensions, orchestrator. Git detected as rename at 93% similarity.

```csharp
// Apps/ga-server/GaApi/Services/IChatService.cs
public interface IChatService
{
    IAsyncEnumerable<string> ChatStreamAsync(string userMessage,
        List<ChatMessage>? conversationHistory = null,
        string? systemPrompt = null,
        CancellationToken cancellationToken = default);

    Task<string> ChatAsync(string userMessage,
        List<ChatMessage>? conversationHistory = null,
        string? systemPrompt = null,
        CancellationToken cancellationToken = default);

    Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default);
}
```

---

## Dead Code Removal

| Item | File | What was removed |
|---|---|---|
| S-1 `WorkflowDecl` | `GaSurfaceSyntaxParser.fs` | Unused DU case, parser combinator, desugar match arm |
| S-2 `toBlockRelativeLine` | `GaBlockDetector.fs` | Helper function with zero callers |
| S-3 `getDiagnostics` | `LanguageServer.fs` | Hollow stub with zero callers (only a commented reference in Library.fs) |

All three were YAGNI — scaffolded for a future feature that never arrived.

---

## Engineering Hygiene

### A-7: Console.WriteLine → ILogger

`Console.WriteLine` calls in two orchestration services were replaced with structured `ILogger<T>` warnings. Both services received the logger via primary constructor:

- `Common/GA.Business.Core.Orchestration/Services/OllamaGroundedNarrator.cs`
- `Common/GA.Business.Core.Orchestration/Services/QueryUnderstandingService.cs`

---

### A-8: Double Closure Registration Eliminated

Each of the 5 `BuiltinClosures/*.fs` files had both a module-level `do register ()` AND was called from `GaClosureBootstrap.init()` in `Library.fs`. The `do` bindings were removed from all 5 files. `GaClosureBootstrap.init()` is now the sole driver of registration.

**Files**: `AgentClosures.fs`, `DomainClosures.fs`, `IoClosures.fs`, `PipelineClosures.fs`, `TabClosures.fs`

---

### A-9: ROP Fix — ChatbotSessionOrchestrator

`ArgumentNullException.ThrowIfNull(request)` in `StreamResponseAsync` violated the project ROP policy (service methods must not throw). Replaced with null-safe early return:

```csharp
// Before (violates ROP policy):
ArgumentNullException.ThrowIfNull(request);

// After:
if (request is null || string.IsNullOrWhiteSpace(request.Message))
    return Task.FromResult(string.Empty);
```

---

## Compound Flywheel Infrastructure

### Step 7 — codebase-documenter Agent Added

The compound flywheel was extended from 6 to 7 steps. A new `codebase-documenter` agent runs after `grammar-governor` to produce:

- `docs/snapshots/YYYY-MM-DD-system-snapshot.md` — point-in-time system state
- `docs/architecture/service-inventory.md` — living service register (overwritten each cycle)
- `docs/compound/YYYY-MM-DD-<feature>-cycle-summary.md` — narrative for future developers

The pipeline is now:
```
Work → Reflect → Compound → Promote → Encode → Govern → Document
```

Agent file: `.agent/agents/codebase-documenter.md`

---

## Prevention Strategies

### Unauthenticated Internal Endpoints
- **Guardrail**: Eval/exec/REPL routes must check `IWebHostEnvironment.IsDevelopment()` and return 403 otherwise. Consider a reusable `[EnvironmentGate("Development")]` filter attribute.
- **Checklist**: Every new endpoint whose route template contains `eval`, `exec`, or `script` must have an environment guard and a corresponding integration test asserting 403 in non-dev.
- **Test idea**: Enumerate all `ControllerActionDescriptor` instances matching those patterns and assert each returns 403 when requested in a non-Development test host.

### MCP Side-Effect Leakage
- **Guardrail**: Closures should declare a `ClosureKind` (Pure | SideEffecting | Restricted) at registration. Only `Pure` closures eligible for MCP without explicit override. Maintain an allowlist config file.
- **Checklist**: Any new closure with `io.*` or `agent.*` prefix must not be added to the MCP allowlist without a documented security review comment.
- **Test idea**: Assert `GaInvokeClosure("io.write", ...)` and `GaInvokeClosure("agent.route", ...)` return error results, never execute.

### Layer Boundary Violations
- **Guardrail**: Add an `NetArchTest.Rules` architecture test project asserting `GA.Business.Core.Orchestration` has zero type-level references to `GA.Business.ML`.
- **Checklist**: No `using GA.Business.ML` may appear in any file under `Common/GA.Business.Core.Orchestration/` — verify in PR diff.
- **Test idea**: `Types().That().ResideInNamespace("GA.Business.Core.Orchestration").Should().NotDependOnAny("GA.Business.ML")` — fails the build on violation.

### Prompt Injection via DB Content
- **Guardrail**: All strings from DB entities or user requests concatenated into prompts must pass through `PromptSanitizer.Sanitize()`. Direct interpolation of raw entity fields is a blocking PR comment.
- **Checklist**: No string interpolation of `entity.*` or `request.*` fields directly into prompt `StringBuilder` without sanitization wrapper.
- **Test idea**: Pass known injection strings through every prompt-builder method and assert the returned prompt does not contain the raw injection payload.

### Double Registration (F# modules)
- **Guardrail**: F# module `do` bindings must not call registration or mutation functions. Registration belongs exclusively in the explicit bootstrap entry point. Bootstrap should be idempotent (Lazy or guard flag).
- **Checklist**: Search PR diff for top-level `do` blocks in non-test F# modules calling anything containing `register`, `add`, `bind`.
- **Test idea**: Call bootstrap twice, assert registry count is identical after both calls.

### Console.WriteLine in Services
- **Guardrail**: Enable `S2228` (SonarAnalyzer) or a custom Roslyn rule flagging `Console.Write*` in all projects except `*.Tests*` and `*Program.cs`. Zero-warnings policy promotes this to a build error.
- **Checklist**: No `Console.Write*` in `Common/` or `Apps/` outside of `Program.cs`.

### throw in Service Methods (ROP)
- **Guardrail**: Custom `DiagnosticAnalyzer` (`GA0001`) that reports `throw` statements inside methods returning `Result<`, `Try<`, `Option<`, or `Validation<`. `ArgumentNullException.ThrowIfNull` and `ArgumentException.ThrowIf*` also flagged.
- **Checklist**: No `throw` or `ThrowIfNull`/`ThrowIf*` in service methods — replace with early-return or `Option.FromNullable`.

---

## Suggested Project-Wide Linting Rules

| Rule | Mechanism | What it catches |
|---|---|---|
| Banned `Console` in library code | `SonarAnalyzer.CSharp` rule `S2228` as error in `Directory.Build.props` | `Console.WriteLine` in services |
| Layer boundary enforcement | `NetArchTest.Rules` in `Tests/GA.ArchitectureTests` project | ML types in Orchestration contracts |
| throw-in-Result analyzer | Custom `DiagnosticAnalyzer` `GA0001` in `GA.Analyzers` project | `throw` / `ThrowIfNull` in ROP service methods |

---

## Deferred Items

| ID | Item | Reason |
|---|---|---|
| A-1 full | Full migration of `AgentClosures.fs` from Layer 2 (`GA.Business.DSL`) to Layer 5 | Breaking — requires `IHttpClientFactory` DI wiring across F#/C# boundary |
| S-4 | Merge `MetaDecl`/`PolicyDecl` into `DirectiveDecl` | Low urgency, parsers stable |
| S-5 | Consolidate `NoteNames`/`RootNames` duplicate arrays | Low risk as-is |
| S-6 | Remove `DocumentStore.GetAll()` dead API | Needs caller audit first |
| S-7 | Thread `_requestId` through function signature (not JObject side-channel) | Refactor scope, not security-critical |
| A-4 | Fix per-invocation `HttpClient` in `IoClosures.fs`/`TabClosures.fs` with `SocketsHttpHandler` | Performance improvement, not blocking |
| L-1 | Anthropic API key exposure via SDK debug/trace logging | Needs SDK-level investigation |
| L-2 | Silent exception swallowing in Ollama availability check | Observability improvement |

---

## Related Docs

- [Full review report](../reviews/ce-review-feat-chatbot-orchestration-extraction-2026-03-07.md) — original 27-finding risk matrix
- [F# module init + closure registry](../runtime-errors/fsharp-module-init-closure-registry.md) — explains why the double-registration happened (A-8 is a direct sequel)
- [Compound engineering flywheel](../tooling/compound-engineering-flywheel-2026-03-07.md) — describes the 7-step loop this cycle extended
- [Chatbot Technical Roadmap](../../../Common/GA.Business.ML/Documentation/Architecture/Chatbot_Technical_Roadmap.md) — Phase 5 is the architectural home for `ChatbotSessionOrchestrator` and `GroundedNarrator`
- [Five-layer model](../../../CLAUDE.md) — canonical layer authority for A-1/A-2 findings
