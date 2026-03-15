# Compound Report — 2026-03-10 — feat/chatbot-orchestration-extraction

## Summary

This sprint completed the chatbot orchestration extraction by introducing a structured plugin/skill/hook architecture, a shared Ollama HTTP client, a GA scale-note service, four new MCP tools for contextual chords, structured FSI diagnostics with timing, a pure-SVG fret diagram component, and live fretboard scale overlays via the `ga:scale` AG-UI custom event.

The work is coherent and well-scoped. Five repeating patterns emerge that are strong candidates for abstraction at the F#/DSL tier or as stable MCP contracts.

---

## Patterns Found

### 1. Dual-interface skill contract (CanHandle / ExecuteAsync)

**Evidence:** `IAgentSkill` (agent-level, `AgentRequest`), `IOrchestratorSkill` (orchestrator-level, `string message`) — 2 interfaces with identical shape but different parameter types. `VoicingComfortSkill` implements `IOrchestratorSkill`; the four `KeyIdentificationSkill`, `ChordSubstitutionSkill`, `ScaleInfoSkill`, `FretSpanSkill` in `GA.Business.ML` implement `IAgentSkill`. Total: 6 implementations across 2 files, pattern repeated in every new skill.

**Opportunity:** A single generic `ISkill<TInput>` base with `CanHandle(TInput)` + `ExecuteAsync(TInput, CancellationToken)` would unify both tiers. The two concrete shapes (`AgentRequest` vs `string`) can become DU cases, removing the parallel interface hierarchy and making skill routing a single polymorphic pass.

---

### 2. MCP proxy method (create client → URI-encode args → GET/POST → ReadAsStringAsync)

**Evidence:** `ContextualChordsTool` — 4 methods, each: `httpClientFactory.CreateClient("gaapi")` → `Uri.EscapeDataString(x)` → `client.GetAsync(url, ct)` → `EnsureSuccessStatusCode()` → `ReadAsStringAsync(ct)`. `GaScriptTool.EvalGaScript` and `GaScriptTool.ListGaClosures` follow the POST variant of the same pattern. 6 occurrences across 2 files, with only the URL template differing.

**Opportunity:** An `IMcpProxyClient` helper (or a thin F# `mcpGet`/`mcpPost` computation) that encodes args, calls the endpoint, and returns `string` — eliminating 5–8 lines per method. The URL template could itself be a GAL `io` closure, making proxy endpoints registerable without new C# code.

---

### 3. Pitch-class arithmetic duplicated across layers

**Evidence:** `ScaleNoteService` (C#, app layer) hard-codes the chromatic pitch-class table, root-PC lookup dictionary, and `(root + offset) % 12` modular arithmetic. The same semitone offsets (`[0,2,4,5,7,9,11]` for major; `[0,2,3,5,7,8,10]` for minor) exist in `GA.Business.Core` domain types and in the F# `GaPrelude` scale definitions. The `VoicingComfortSkill.ChordSymbol` regex and `NormalizeChord` helper also duplicate the accidental-parsing logic already in `GA.Business.Core.Harmony`. Minimum 3 independent re-implementations.

**Opportunity:** A single canonical F# `PitchClass` module exported through `GaClosureRegistry` as `domain.scaleNotes` and `domain.parseChord` (the latter already exists). `ScaleNoteService` becomes a thin C# wrapper calling into that closure via the FSI pool or via the domain layer directly — removing the parallel chromatic table entirely.

---

### 4. GA custom event payload type (name × typed value)

**Evidence:** AG-UI CUSTOM events `ga:diatonic`, `ga:scale`, `ga:candidates`, `ga:progression` are emitted in `AgUiChatController` (4 `WriteCustomAsync` calls) and consumed in `useGAAgent.ts` `onCustomEvent` (string switch over `name`). `ScaleNote` and `ChordInContext` each have a parallel C# record and TypeScript interface that must stay manually in sync. 4 event types, 2 language boundaries, 0 contract enforcement.

**Opportunity:** A discriminated union `GaCustomEvent` on the C# side, serialised to a typed JSON envelope `{ "name": "ga:scale", "value": [...] }`. The TypeScript side gains a discriminated union generated from the C# source (via NSwag or a simple T4). Contract drift becomes a build error instead of a runtime mismatch.

---

### 5. Dev-only environment guard (if (!env.IsDevelopment()) return 403)

**Evidence:** `GaEvalController.Eval`, `GaEvalController.ListClosures`, `GaEvalController.GetClosure` — identical three-line guard repeated 3 times in the same file. A fourth occurrence exists in the Aspire host configuration for the Eval endpoints.

**Opportunity:** A `[DevOnly]` action filter attribute (Tier 0 — inline helper) registered once in DI, turning the repetition into a single declarative annotation per method. Too simple for F# promotion; straightforward ASP.NET Core middleware concern.

---

## Proposed Promotions

### 1. Unified Skill contract → Tier 2: F# discriminated union + shared record

The two skill interfaces are structurally identical; the only variance is the input type.

```fsharp
// GA.Business.ML / SkillContracts.fs

/// Unified input for both orchestrator-level and agent-level skills.
[<Struct>]
type SkillInput =
    | RawMessage of message: string
    | AgentReq   of request: AgentRequest

/// Result of a skill execution.
type SkillOutput =
    { AgentId    : string
      Result     : string
      Confidence : float32
      Evidence   : string[]
      Assumptions: string[] }

/// Generic skill contract — replaces IAgentSkill and IOrchestratorSkill.
type IGaSkill =
    abstract Name        : string
    abstract Description : string
    abstract CanHandle   : SkillInput -> bool
    abstract ExecuteAsync: SkillInput * CancellationToken -> Task<SkillOutput>
```

C# wrappers (`IAgentSkill`, `IOrchestratorSkill`) become thin adapters projecting `AgentRequest → SkillInput.AgentReq` and `string → SkillInput.RawMessage`. Routing becomes a single `skills |> Seq.tryFind (fun s -> s.CanHandle input)`.

**Effort:** M  **Risk:** Low — pure rename/adapter refactor; existing DI registrations unchanged.

---

### 2. MCP proxy closure → Tier 5: MCP tool generated from GAL closure descriptors

The proxy methods in `ContextualChordsTool` and `GaScriptTool` are mechanical translations of `GaClosure` descriptors into HTTP calls. The `io.httpGet` closure already exists in the registry.

```fsharp
// GaClosureRegistry — new category: McpProxy

/// Auto-generated MCP tool binding for a named GA closure exposed over HTTP.
type McpProxyBinding =
    { ClosureName : string          // e.g. "contextual-chords.diatonic"
      HttpMethod  : string          // "GET" | "POST"
      UrlTemplate : string          // "/api/contextual-chords/keys/{key}"
      Params      : string list }   // ordered URL-segment param names

/// Registers a proxy binding; McpProxyToolFactory emits [McpServerTool] methods at startup.
val registerMcpProxy : McpProxyBinding -> unit
```

At startup, `McpProxyToolFactory` iterates registered bindings and synthesises `[McpServerTool]`-decorated delegates, replacing hand-written proxy classes with a data-driven registry. New chord or scale endpoints require only a new `registerMcpProxy` call — no C# class.

**Effort:** L  **Risk:** Med — requires reflection-based tool generation; MCP SDK must support dynamic registration (validate first).

---

### 3. Canonical pitch-class module → Tier 2: F# record + Tier 5: `domain.scaleNotes` closure

The chromatic arithmetic belongs in one place.

```fsharp
// GA.Business.Core / PitchClass.fs  (already exists conceptually; make it the canonical source)

[<Struct>]
type PitchClass = PitchClass of int   // 0–11

type ScaleMode = Major | NaturalMinor | Dorian | Phrygian // | ...

type ScaleNoteDegree =
    { Degree     : int
      Note       : string     // e.g. "F#"
      PitchClass : PitchClass }

/// Returns the 7 (or n) degrees of a scale from a root PC and mode.
val scaleNotes : root: PitchClass -> mode: ScaleMode -> ScaleNoteDegree[]

/// Registered as "domain.scaleNotes" in GaClosureRegistry.
/// Input schema: { "root": "string", "mode": "string" }
/// Output type:  "ScaleNoteDegree[]"
```

`ScaleNoteService` in `GaApi` is replaced by a one-line call to `GaClosureRegistry.Global.Invoke("domain.scaleNotes", params)`, or directly to the F# function through a thin C# bridge. The `VoicingComfortSkill` accidental regex becomes a call to `domain.parseChord`.

**Effort:** M  **Risk:** Low — additive; existing code continues to work until the call sites are migrated.

---

### 4. Typed AG-UI custom event contract → Tier 2: F# discriminated union + TypeScript codegen

```fsharp
// GA.Business.Core.Orchestration / AgUiEvents.fs

/// Typed discriminated union for all domain-specific AG-UI CUSTOM events.
[<JsonConverter(typeof<GaCustomEventConverter>)>]
type GaCustomEvent =
    | GaDiatonic    of chords:     ChordInContext[]
    | GaScale       of notes:      ScaleNoteDegree[]
    | GaCandidates  of candidates: CandidateVoicing[]
    | GaProgression of steps:      ProgressionStep[]

/// Emits the event as { "name": "ga:<tag>", "value": <payload> }
val toAgUiEnvelope : GaCustomEvent -> {| name: string; value: obj |}
```

`AgUiChatController` changes from four ad-hoc `WriteCustomAsync` calls to:

```fsharp
let event = GaScale scaleNotes
await writer.WriteCustomAsync(toAgUiEnvelope event, ct)
```

TypeScript side: NSwag or a T4 template generates `GaCustomEvent` as a TypeScript discriminated union; the `onCustomEvent` switch becomes exhaustive and type-safe.

**Effort:** M  **Risk:** Low (C#) / Med (TypeScript codegen pipeline setup).

---

### 5. Dev-only guard → Tier 0: inline `[DevOnly]` action filter

```csharp
// GaApi/Filters/DevOnlyAttribute.cs
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class)]
public sealed class DevOnlyAttribute : ActionFilterAttribute
{
    public override void OnActionExecuting(ActionExecutingContext ctx)
    {
        var env = ctx.HttpContext.RequestServices.GetRequiredService<IWebHostEnvironment>();
        if (!env.IsDevelopment())
            ctx.Result = new StatusCodeResult(StatusCodes.Status403Forbidden);
    }
}
```

All three `GaEvalController` methods get `[DevOnly]` in place of the guard block.

**Effort:** S  **Risk:** Low — pure mechanical refactor.

---

## Grammar Audit

**Verdict: CLEAR PROMOTION (with one DEFER)**

**Rationale:**

*Stability* — The FSI session pool now carries structured `GaScriptDiagnostic` with Code/Message/Severity/Line/Column and Stopwatch timing. This is a sound, additive change that does not alter session semantics. The `EvalAsync` / `EvalWithResultAsync` duplication in `GaFsiSessionPool` (the gate + acquire + clear + stopwatch block is copy-pasted between the two members) is the one internal bloat risk — it should be extracted into a private `runUnderGate` helper before further session-level features are added, or the risk of session-state bugs on diverging copy-paste grows.

*Bloat* — `ScaleNoteService` is the clearest bloat signal: it re-implements pitch-class arithmetic that already exists in the domain layer, at the application layer, as a static class. This is a second independent chromatic table in the codebase (the third if we count the F# prelude). Allowing a third adds technical debt faster than the feature delivers value.

*GAL surface syntax* — No new `pipeline { }` or `ga { }` CE clauses were introduced. The `TranspileGaScript` MCP tool is purely read-side (parse + desugar, no evaluation). The `GaClosureRegistry` gained no new categories. Grammar is stable.

*MCP contract* — `ContextualChordsTool` adds 4 tools and `GaScriptTool` adds 4 tools (8 total new MCP surface area in one sprint). This is at the upper limit of what the grammar can absorb without a meta-tool (the `McpProxy` pattern from Promotion 2). If the next sprint adds another tool class without the proxy abstraction, we risk MCP surface explosion that will be expensive to reverse.

**DEFER: Promotion 2 (McpProxy codegen)** — validate MCP SDK support for dynamic tool registration before committing to it. Do not add more hand-written proxy tool classes in the next sprint; instead, defer any new proxy tools until the binding data model is confirmed.

---

## Recommended Actions

| Priority | Action | Effort | Target |
|---|---|---|---|
| P0 | Extract `runUnderGate` helper in `GaFsiSessionPool.fs` to eliminate the copy-pasted gate/acquire/stopwatch block | S | `GA.Business.DSL` |
| P0 | Add `[DevOnly]` action filter; remove 3 inline env-guards from `GaEvalController` | S | `GaApi` |
| P1 | Consolidate `ScaleNoteService` by delegating to `domain.scaleNotes` closure (or direct F# bridge) | M | `GaApi` → `GA.Business.DSL` |
| P1 | Define `GaCustomEvent` DU in `GA.Business.Core.Orchestration`; replace 4 ad-hoc `WriteCustomAsync` calls | M | `GA.Business.Core.Orchestration` |
| P2 | Introduce unified `IGaSkill<TInput>` contract; make `IAgentSkill` / `IOrchestratorSkill` thin adapters | M | `GA.Business.ML` |
| P3 | Validate MCP SDK dynamic tool registration; prototype `McpProxyBinding` registry | L | `GaMcpServer` |
| P3 | TypeScript codegen for `GaCustomEvent` (NSwag or T4) — unblocked after P1 typed DU lands | M | `ReactComponents` |

---

## Deferred

- **McpProxy codegen (Promotion 2):** Blocked on MCP SDK dynamic registration validation. Do not add new hand-written proxy tool classes until unblocked; track in BACKLOG.md.
- **Full `IGaSkill<TInput>` migration (Promotion 1):** Defer until after the typed event DU (P1) lands, to reduce change set size per PR.
- **TypeScript discriminated union codegen:** Dependent on P1 `GaCustomEvent` DU; defer to the sprint after the C# contract is stable.
- **`GaScriptDiagnostic` as a shared NuGet type:** Currently defined in F# (`GaFsiSessionPool.fs`) and mirrored as a C# record DTO in `GaEvalController` and again in `GaScriptTool`. Worth consolidating into a shared `GA.Business.DSL.Diagnostics` module, but not blocking.
