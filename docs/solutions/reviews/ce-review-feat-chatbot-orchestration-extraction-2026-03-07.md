---
title: "/ce:review — feat/chatbot-orchestration-extraction"
date: 2026-03-07
branch: feat/chatbot-orchestration-extraction
agents: [architecture-strategist, security-sentinel, code-simplicity-reviewer]
---

# Code Review — feat/chatbot-orchestration-extraction

Three review agents ran in parallel. This document consolidates findings across all three.

---

## Risk Matrix (all agents combined)

| ID | Severity | Agent | Title |
|----|----------|-------|-------|
| C-1 | CRITICAL | Security | Unauthenticated arbitrary F# code execution via `/api/ga/eval` |
| C-2 | CRITICAL | Security | Arbitrary closure invocation with no name allowlist |
| A-1 | HIGH | Architecture | `AgentClosures.fs` in Layer 2 — belongs in Layer 5 Orchestration |
| A-2 | HIGH | Architecture | `VoicingExplanation` ML type imported into Orchestration model contract |
| H-1 | HIGH | Security | SSRF — Ollama endpoint URI accepted from config with no validation |
| H-2 | HIGH | Security | Unbounded conversation history in static in-memory dictionary |
| H-3 | HIGH | Security | SSRF — `WebScrapingToolWrapper` forwards user URL with no validation |
| A-3 | HIGH | Architecture | Unconditional SSL bypass in module-static `HttpClient` in `AgentClosures.fs` |
| M-1 | MEDIUM | Security | Prompt injection regex incomplete — Unicode bypass + DB content unsanitized |
| M-2 | MEDIUM | Security | No authentication on any public endpoint |
| M-3 | MEDIUM | Security | `EvalGaScript` MCP tool forwards unsanitized input to FSI |
| M-4 | MEDIUM | Security | `SearchKnowledge` limit parameter unbounded — DoS vector |
| A-4 | MEDIUM | Architecture | Static `HttpClient` without `IHttpClientFactory` in three closure modules |
| A-5 | MEDIUM | Architecture | `ClaudeChatService` implements `IOllamaChatService` — wrong abstraction name |
| A-6 | MEDIUM | Architecture | `ChatbotHub` conversation write non-atomic, unbounded cache |
| S-1 | MEDIUM | Simplicity | `WorkflowDecl` DU case — YAGNI, never parsed, never tested |
| S-2 | MEDIUM | Simplicity | `toBlockRelativeLine` — dead helper in `GaBlockDetector.fs` |
| S-3 | MEDIUM | Simplicity | `getDiagnostics` — exposed but returns empty list; stub with no consumers |
| S-4 | LOW | Simplicity | `MetaDecl` + `PolicyDecl` differ only by keyword — merge into `DirectiveDecl` |
| S-5 | LOW | Simplicity | `NoteNames`/`RootNames` duplication — same 12 names in two places |
| S-6 | LOW | Simplicity | `DocumentStore.GetAll()` — exposed but no callers in LSP |
| S-7 | LOW | Simplicity | `enrichParams`/`_requestId` — side-channel injection into JObject params bag |
| L-1 | LOW | Security | Anthropic API key exposure via SDK debug/trace logging |
| L-2 | LOW | Security | Silent exception swallowing in Ollama availability check |
| A-7 | LOW | Architecture | `Console.WriteLine` in library services — bypasses structured logging |
| A-8 | LOW | Architecture | Double closure registration (module-level `do` + `GaClosureBootstrap.init()`) |
| A-9 | LOW | Architecture | ROP policy not applied in `ChatbotSessionOrchestrator.GetResponseAsync` |

---

## CRITICAL — Fix Before Merge

### C-1 — Unauthenticated Arbitrary F# Code Execution

**File:** `Apps/ga-server/GaApi/Controllers/GaEvalController.cs:15`

`POST /api/ga/eval` accepts a `{ script }` JSON body and passes it verbatim to `GaFsiPool.Instance.EvalAsync`. No authentication, no authorization attribute, no allowlist. An unauthenticated network attacker can exfiltrate `ANTHROPIC_API_KEY`, execute shell commands, write files, and dump the MongoDB connection string. The FSI session retains bindings across calls.

**Fix:**
```csharp
// Option A — dev-only
if (!app.Environment.IsDevelopment())
    app.MapControllers(); // remove GaEvalController route in production

// Option B — always require authorization
[Authorize(Policy = "InternalAdmin")]
public class GaEvalController : ControllerBase { ... }
```

---

### C-2 — Arbitrary Closure Invocation Without Allowlist

**File:** `GaMcpServer/Tools/GaDslTool.cs:183`

`GaInvokeClosure` accepts a caller-supplied `name` and looks it up in the global registry with no allowlist. `io.readFile`, `io.writeFile`, `io.httpGet`, `io.httpPost` are all reachable. Any process that reaches the MCP transport can call these without going through `EvalGaScript`.

**Fix:**
```csharp
private static readonly HashSet<string> _allowedClosures = new(StringComparer.OrdinalIgnoreCase)
{
    "domain.parseChord", "domain.chordIntervals", "domain.transposeChord",
    "domain.diatonicChords", "domain.relativeKey",
    // add only domain.* and analysis.* — exclude all io.* and agent.*
};

if (!_allowedClosures.Contains(closureName))
    return McpError("Closure not permitted via MCP: " + closureName);
```

---

## HIGH — Fix Before Staging

### A-1 — `AgentClosures.fs` in Layer 2

**File:** `Common/GA.Business.DSL/Closures/BuiltinClosures/AgentClosures.fs`

Makes outbound HTTP calls to the GA chatbot API. Layer 2 (Domain) must not perform AI/orchestration calls. `PipelineClosures.fs` in the same layer has a `pipeline.embedOpticK` stub that explicitly intends to call `GA.Business.ML` — a direct upward dependency that will cause a circular reference when wired for real.

**Fix:** Move `AgentClosures.fs` to `GA.Business.Core.Orchestration/Closures/`. Register via `ChatbotOrchestrationExtensions`. Remove from `GaClosureBootstrap.init()` in `GA.Business.DSL`. Keep `pipeline.embedOpticK` as a stub in DSL; require the real implementation to be injected as a delegate from Orchestration.

---

### A-2 — ML Type in Orchestration Model Contract

**File:** `Common/GA.Business.Core.Orchestration/Models/ChatModels.cs:3`

```csharp
using GA.Business.ML.Musical.Explanation;
// ...
public VoicingExplanation ExplanationFacts { get; init; }
```

Couples the model contract to ML internals. Any consumer of `ChatModels.cs` transitively depends on `GA.Business.ML`.

**Fix:** Introduce `VoicingExplanationDto` in `GA.Business.Core.Orchestration.Models`. Map from ML type at the Orchestration service boundary.

---

### A-3 — Unconditional SSL Bypass

**File:** `Common/GA.Business.DSL/Closures/BuiltinClosures/AgentClosures.fs:13`

```fsharp
handler.ServerCertificateCustomValidationCallback <-
    HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
```

Unconditional, module-static — affects any deployment where `GA_API_BASE_URL` points to a remote endpoint.

**Fix:**
```fsharp
if System.Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT") = "Development" then
    handler.ServerCertificateCustomValidationCallback <-
        HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
```

Remove entirely once the module moves to Orchestration and `IHttpClientFactory` is wired.

---

### H-1 — SSRF via Ollama Endpoint Config

**File:** `Common/GA.Business.Core.Orchestration/Services/OllamaGroundedNarrator.cs:21`

`Ollama:Endpoint` read from config with no URI validation. In containerized or cloud environments, a poisoned config source can point the narrator at `http://169.254.169.254/latest/meta-data/`.

**Fix:** Validate at startup: require `http`/`https` scheme, reject RFC-1918 and loopback ranges.

---

### H-2 / A-6 — Unbounded Conversation History

**File:** `Apps/ga-server/GaApi/Hubs/ChatbotHub.cs:20`

Static `ConcurrentDictionary` grows without bound. Abnormal disconnects leave ghost entries. Write at line 76 is a plain indexer assignment (non-atomic race possible).

**Fix:** Replace with `MemoryCache<string, List<ChatMessage>>` with 30-minute sliding expiration. Use `AddOrUpdate` for the write path.

---

### H-3 — SSRF in WebScrapingToolWrapper

**File:** `GaMcpServer/Tools/WebScrapingToolWrapper.cs:12`

All three tool methods accept a bare `url` parameter forwarded directly to `WebScrapingService`. No scheme check, no hostname/IP range filter.

**Fix:** Require `https` scheme; resolve hostname; reject private/loopback IP ranges before issuing the HTTP request.

---

## MEDIUM — Fix in Follow-up

### S-1 — `WorkflowDecl` DU Case (YAGNI)

**File:** `Common/GA.Business.DSL/Parsers/GaSurfaceSyntaxParser.fs`

`WorkflowDecl` is a DU case that is never parsed from real syntax, never covered by a test, and never desugared. It sits in the AST as intent, not as an implemented feature.

**Fix:** Remove the case. Add it back in the sprint where workflow-level syntax is actually designed.

---

### S-2 — `toBlockRelativeLine` (dead helper)

**File:** `Common/GA.Business.DSL/LSP/GaBlockDetector.fs`

Defined but never called by any LSP message handler.

**Fix:** Delete.

---

### S-3 — `getDiagnostics` (hollow stub)

**File:** `Common/GA.Business.DSL/LSP/LanguageServer.fs`

Exposed as a public function, returns an empty list. No callers.

**Fix:** Delete until diagnostics are actually implemented.

---

### M-1 — Incomplete Prompt Injection Sanitization

**File:** `Common/GA.Business.Core.Orchestration/Services/GroundedPromptBuilder.cs:15`

Regex misses full-width Unicode variants (`ＳＹＳＴＥＭ:`), `\nHuman:` patterns, and `###\n` (content on next line). Also: `candidates` data (`DisplayName`, `Summary`, `Tags`) from the database is injected into the prompt without sanitization — indirect injection vector.

**Fix:** Apply `String.Normalize(NormalizationForm.FormKD)` before regex. Extend pattern. Sanitize all database-sourced fields before prompt interpolation.

---

### M-2 — No Auth on Any Endpoint

**File:** `Apps/ga-server/GaApi/Program.cs:148`

No global `RequireAuthorization()`, no `[Authorize]` on any controller or hub method. All CRITICAL and HIGH findings above are exploitable without credentials.

**Fix:** Add authorization policy at minimum for `GaEvalController` (C-1). Wire global rate limiting (the `TODO` at line 149 is overdue).

---

### M-4 — Unbounded `limit` in `SearchKnowledge`

**File:** `Apps/ga-server/GaApi/Hubs/ChatbotHub.cs:111`

`limit` forwarded directly to vector search. Pass `int.MaxValue` → memory exhaustion.

**Fix:** `limit = Math.Clamp(limit, 1, 50)`.

---

### A-4 — Static `HttpClient` Without `IHttpClientFactory`

Three modules create static or per-invocation `HttpClient`s directly. `IoClosures.fs` creates per-invocation clients (socket exhaustion risk). `TabClosures.fs` and `AgentClosures.fs` are module-static (cannot respect DNS TTL, no retry policies).

**Fix:** After moving `AgentClosures` to Orchestration, wire `IHttpClientFactory`. For `IoClosures`/`TabClosures`, use a single module-level `SocketsHttpHandler`-backed instance with `PooledConnectionLifetime = TimeSpan.FromMinutes(5)`.

---

### A-5 — `ClaudeChatService` Implements `IOllamaChatService`

**File:** `Apps/ga-server/GaApi/Services/ClaudeChatService.cs:12`

Interface named for a specific provider.

**Fix:** Rename `IOllamaChatService` → `IChatService` or `ILlmChatService`. Update all DI registrations.

---

### S-4 — `MetaDecl`/`PolicyDecl` Merge Opportunity

Both DU cases hold the same `name * GaExpr list` payload and differ only in which keyword triggered them. Callers switch on the case to emit a comment header.

**Fix:** Merge into `DirectiveDecl of kind: string * body: GaExpr list`. Kind carries `"meta"` or `"policy"` as a string tag — or a typed `DirectiveKind` DU if more cases are expected.

---

## LOW

### S-5 — `NoteNames`/`RootNames` Duplication
Two arrays with the same 12 chromatic note names in different files. Consolidate into one in `GA.Core` or `GA.Business.Config`.

### S-6 — `DocumentStore.GetAll()` Dead API
No LSP code path reads all documents. Remove until needed.

### S-7 / A-9 — `_requestId` LSP Side Channel / ROP Violation
- Thread request ID through function signature, not params JObject injection.
- `ChatbotSessionOrchestrator.GetResponseAsync` should return `Result<ChatResponse, OrchestratorError>` instead of throwing `ArgumentNullException`.

### A-7 — `Console.WriteLine` in Library Services
`QueryUnderstandingService` and `OllamaGroundedNarrator` use `Console.WriteLine`. Replace with `ILogger<T>`.

### A-8 — Double Closure Registration
Module-level `do register ()` bindings in each builtin module fire at load time; `GaClosureBootstrap.init()` then calls the same `register ()` again. Remove the `do register ()` bindings; let `init()` be the sole driver.

---

## Recommended Merge Checklist

**Block merge on:**
- [ ] C-1: Auth on `/api/ga/eval`
- [ ] C-2: Closure name allowlist in `GaDslTool`
- [ ] A-1: Move `AgentClosures.fs` to Layer 5
- [ ] A-2: Replace `VoicingExplanation` with DTO in `ChatModels.cs`
- [ ] A-3: Conditionalize SSL bypass

**Before staging:**
- [ ] H-1: Validate Ollama endpoint URI at startup
- [ ] H-2: Bounded sliding-expiry cache for `_conversations`
- [ ] H-3: URL validation in `WebScrapingToolWrapper`
- [ ] A-4: Fix per-invocation `HttpClient` in `IoClosures.fs`
- [ ] M-2: Global rate limiting wired
- [ ] M-4: Clamp `SearchKnowledge` limit

**Follow-up sprint:**
- [ ] S-1: Remove `WorkflowDecl`
- [ ] S-2/S-3: Remove dead helpers (`toBlockRelativeLine`, `getDiagnostics`)
- [ ] S-4: Merge `MetaDecl`/`PolicyDecl` → `DirectiveDecl`
- [ ] A-5: Rename `IOllamaChatService`
- [ ] M-1: Prompt injection Unicode normalization + DB field sanitization
- [ ] A-7/A-8/A-9: Console.WriteLine, double registration, ROP
