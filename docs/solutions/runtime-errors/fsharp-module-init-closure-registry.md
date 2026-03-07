---
title: "F# Module Initializers, GaAsync Covariance, and DSL Closure Wiring"
date: 2026-03-07
category: "runtime-errors"
tags: [fsharp, module-initializers, type-mismatch, dsl-closures, gacli, async]
symptoms: "GaClosureRegistry.Global.List() returned empty list at runtime despite open directives for all builtin closure modules; FS0001 GaAsync<string> not assignable to GaAsync<obj>; MSB4025 XML comment parse error"
components:
  - Apps/GaCli/Program.fs
  - Common/GA.Business.DSL/Closures/BuiltinClosures/DomainClosures.fs
  - Common/GA.Business.DSL/Closures/BuiltinClosures/AgentClosures.fs
severity: "medium"
---

# F# Module Initializers, GaAsync Covariance, and DSL Closure Wiring

Three bugs encountered while building `Apps/GaCli` and wiring the GA Language (GAL) builtin closures to real domain services. The most critical is the F# module initializer pitfall — it fails silently and produces zero registered closures with no error message.

## Symptoms

1. `GaClosureRegistry.Global.List()` returns `[]` in `Apps/GaCli` despite all closure modules being opened
2. FS0001: "This expression was expected to have type 'obj' but here has type 'string'" in `AgentClosures.fs`
3. MSB4025: "An XML comment cannot contain '--'" when loading `Apps/GaCli/GaCli.fsproj`

## Root Causes

### 1. F# `open` does not trigger module-level `do` bindings

`open` is a compile-time namespace alias only. The CLR type initializer for an F# module (which runs `do register()`) fires only when a value from that module is **first accessed at runtime**. If no value is accessed, the side-effectful registration never runs.

This is invisible: the code compiles, the closures appear to be defined, but the registry is empty at runtime.

### 2. F# generic invariance — `GaAsync<string>` ≠ `GaAsync<obj>`

`GaAsync<'T> = Async<Result<'T, GaError>>`. F# generic types are invariant — `Async<Result<string,_>>` is not a subtype of `Async<Result<obj,_>>`. The `Exec` signature in `GaClosure` is `Map<string,obj> -> GaAsync<obj>`, so any helper returning a concrete `GaAsync<'T>` must have its value boxed before returning.

### 3. `--` in XML comments

The XML specification (§2.5) forbids `--` anywhere inside `<!-- ... -->` comment content. MSBuild's XML reader rejects the project file outright.

## Solution

### Bug 1 — Force module initializers explicitly

```fsharp
// WRONG — open does not trigger module init
open GA.Business.DSL.Closures.BuiltinClosures.DomainClosures

// CORRECT — explicit call forces CLR type initializer
do GA.Business.DSL.Closures.BuiltinClosures.DomainClosures.register   ()
do GA.Business.DSL.Closures.BuiltinClosures.PipelineClosures.register ()
do GA.Business.DSL.Closures.BuiltinClosures.AgentClosures.register    ()
do GA.Business.DSL.Closures.BuiltinClosures.IoClosures.register       ()
```

Applied in `Apps/GaCli/Program.fs`. The comment in the source explains why:

```fsharp
// Force module initializers so every `do register()` call fires.
// F# modules only initialize when a value from them is accessed;
// `open` alone is a compile-time alias and does not trigger the CLR type initializer.
```

### Bug 2 — Box the concrete result at the closure boundary

```fsharp
// WRONG — returns GaAsync<string>, not GaAsync<obj>
| Some q -> return! chatbotPost (q :?> string)

// CORRECT — explicitly box the string result
| Some q ->
    let! r = chatbotPost (q :?> string)
    return r |> Result.map box
```

**Decision tree for writing conforming `Exec` functions:**

```
Does your helper already produce GaAsync<obj>?
  YES → return directly.
  NO  →
    Have a GaAsync<'T>?  →  |> GaAsyncOps.map box
    Have a Result<'T,_>? →  |> Result.map box    (inside a CE after let!)
    At a final return?   →  return box value
```

### Bug 3 — Remove `--` from .fsproj XML comments

```xml
<!-- WRONG -->
<!-- Install: dotnet tool install --global GA.Cli -->

<!-- CORRECT -->
<!-- Install as a local dotnet tool via: dotnet pack, then dotnet tool install -->
```

### Feature: Wiring closures to real domain services

The pattern established for `DomainClosures.fs`:

```fsharp
// domain.parseChord → ChordDslService().Parse() → JSON ChordAst
Exec = fun inputs ->
    async {
        match inputs.TryFind "symbol" with
        | None -> return Error (GaError.DomainError "Missing 'symbol' input")
        | Some sym ->
            match ChordDslService().Parse(sym :?> string) with
            | Result.Error err -> return Error (GaError.ParseError ("chord", err))
            | Result.Ok ast    -> return Ok (box (serializeAst ast))
    }
```

The pattern established for `AgentClosures.fs` (HTTP to chatbot):

```fsharp
// agent.theoryAgent → POST /api/chatbot/chat → NaturalLanguageAnswer
let private chatbotPost (message: string) : GaAsync<string> =
    async {
        try
            let msgJson = JsonSerializer.Serialize message   // handles escaping
            let body    = sprintf """{"message":%s,"useSemanticSearch":true}""" msgJson
            let content = new StringContent(body, Encoding.UTF8, "application/json")
            let! resp   = httpClient.PostAsync(url, content) |> Async.AwaitTask
            let! json   = resp.Content.ReadAsStringAsync()   |> Async.AwaitTask
            // ...extract naturalLanguageAnswer via JsonDocument...
            return Ok answer
        with ex ->
            return Error (GaError.AgentError ("chatbot", ex.Message))
    }

// In Exec — box the string result before returning as GaAsync<obj>
| Some q ->
    let! r = chatbotPost (q :?> string)
    return r |> Result.map box
```

## Prevention & Best Practices

### 1. F# Module Initializers: `open` vs explicit touch

**Rule of thumb:**
- `open Module` → safe for type aliases, function imports, operator definitions, and any module with no side-effectful `do` bindings
- `open Module` → **NOT safe** when the module's `do` block performs side effects that must happen before other code runs

**Pattern for entry points:** Collect all registration calls in a single `Startup.registerAll()` function and call it once at the top of `main`:

```fsharp
let registerAll () =
    DomainClosures.register   ()
    PipelineClosures.register ()
    AgentClosures.register    ()
    IoClosures.register       ()
```

**Mark side-effectful modules in source:** Add a doc comment `/// SIDE-EFFECTFUL INITIALIZER — must be explicitly referenced at startup.` to any module whose `do` block mutates shared state.

### 2. Systematic `GaAsync<obj>` pattern for closures

Any closure `Exec` whose internal helper returns `GaAsync<'T>` for a concrete `T` must end with one of:

```fsharp
// Option A — pipe the full GaAsync<'T>
|> GaAsyncOps.map box

// Option B — box at the final return site inside a CE
return box myValue

// Option C — box after let! inside a CE
let! r = helperAsync ()
return r |> Result.map box
```

**Anti-pattern:** `box` applied to the entire `GaAsync<'T>` wrapper produces `GaAsync<obj>` where `obj` is a boxed wrapper — a wrapped wrapper that will fail at call time.

### 3. XML comments in `.fsproj` files

Never paste raw CLI flags into XML comments. Replace `--flag-name` with prose descriptions in `.fsproj`/`.csproj` files, or document CLI commands in adjacent `.md` files instead.

### 4. Closure registry completeness test

Add a startup test that catches missing registrations before they surface in production:

```fsharp
[<Test>]
let ``All closures are registered after registerAll`` () =
    Startup.registerAll ()
    let registry = GaClosureRegistry.Global
    let expected = [
        "domain.parseChord"; "domain.transposeChord"; "domain.diatonicChords"
        "agent.theoryAgent"; "agent.tabAgent"; "agent.criticAgent"; "agent.fanOut"
        "io.readFile"; "io.writeFile"; "io.httpGet"; "io.httpPost"
        "pipeline.pullBspRooms"; "pipeline.embedOpticK"; "pipeline.storeQdrant"; "pipeline.reportFailures"
    ]
    for key in expected do
        Assert.IsTrue(registry.TryGet(key).IsSome, sprintf "Closure '%s' not registered" key)
```

## Related Documentation

| Topic | Location |
|-------|----------|
| F# coding standards and C#/F# interop | `.agent/skills/fsharp-csharp-bridge/SKILL.md` |
| GA closure registry usage patterns | `.agent/skills/ga-chords/SKILL.md` §5 |
| GA eval CLI and REST endpoint | `.agent/skills/ga-eval/SKILL.md` |
| Async bridge (`FSharpAsync.StartAsTask`) | `CLAUDE.md` — F# Standards section |

`docs/solutions/` had no prior entries — this is the first.

## Files Changed

| File | Change |
|------|--------|
| `Apps/GaCli/Program.fs` | Created — in-process closure CLI; explicit `do register()` calls |
| `Apps/GaCli/GaCli.fsproj` | Created — F# console app; XML comment without `--` |
| `Common/GA.Business.DSL/Closures/BuiltinClosures/DomainClosures.fs` | Wired to `ChordDslService`, semitone arithmetic, scale patterns |
| `Common/GA.Business.DSL/Closures/BuiltinClosures/AgentClosures.fs` | Wired to HTTP POST `/api/chatbot/chat`; `Result.map box` fix |
| `.agent/skills/ga-eval/SKILL.md` | Created — GA Language Evaluator skill |
| `.agent/skills/ga-chords/SKILL.md` | Created — Chord Theory Helper skill |
| `.agent/skills/ga-chatbot-probe/SKILL.md` | Created — Chatbot Probe skill |
