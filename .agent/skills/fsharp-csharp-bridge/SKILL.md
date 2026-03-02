---
name: "F# & Configuration Architecture"
description: "Comprehensive guide for F# development, YAML configuration standards, and C# interoperability patterns."
---

# F# & Configuration Architecture

This skill governs the development of the F# components (`GA.MusicTheory.DSL`, `GA.Business.Config`) and the YAML configuration architecture they support.

## 1. F# Coding Standards

### 1.1 Style & Layout
- **Indent**: 4 spaces.
- **Naming**: `camelCase` for values/functions, `PascalCase` for types/modules.
- **Modules**: Use `module MyModule =` for grouping helper functions.
- **Pipelines**: Prefer `|>` for data transformations.

### 1.2 Type Design
- **Immutability**: Use immutable records and DUs (Discriminated Unions) by default.
- **CLIMutable**: Add `[<CLIMutable>]` to records used in YAML deserialization or C# interop.
- **Options**: Use `Option<'T>` for nullability.

```fsharp
type TuningInfo = { Name: string; Tuning: string }

module TuningHelpers =
    let formatTuning t = $"%s{t.Name}: %s{t.Tuning}"
```

## 2. Configuration Architecture (YAML)

All static domain data (Scales, Instruments, Voicings) is stored in YAML in `GA.Business.Config`.

### 2.1 YAML Schema Rules
- **Naming**: Keys must be **PascalCase** (e.g., `IntervalClassVector`, `ModalFamilies`).
- **Structure**: Hierarchical data preferred.
- **Serialization**: Handled by **YamlDotNet**.

### 2.2 Loading Pattern
Use the standard loader pattern found in `InstrumentsConfig.fs` or `ModesConfig.fs`:
1.  **Define DTOs**: Create `CLIMutable` records matching the YAML structure.
2.  **Locate File**: Check multiple locations (Bin, Repo Root, Config subfolder) to support Test vs Run environments.
3.  **Deserialize**: Use `DeserializerBuilder().WithNamingConvention(PascalCaseNamingConvention.Instance)`.
4.  **Fallback**: Always provide a minimal hardcoded fall back if the file is missing to prevent crash-loops.

```fsharp
let private loadData () =
    try
        let yaml = File.ReadAllText(path)
        deserializer.Deserialize<MyConfig>(yaml)
    with _ ->
        defaultData // Fallback
```

## 3. C# Interop (The Bridge)

### 3.1 Handling Results
Convert F# `Result<'T, 'Error>` to C# logic:
```csharp
var result = service.DoWork();
if (result.IsOk) { ... result.ResultValue ... }
```

### 3.2 Discriminated Unions
Use C# switch expressions with type patterns:
```csharp
var text = unionValue match {
    CaseA a => a.Value,
    CaseB b => b.Id.ToString(),
    _ => "Unknown"
};
```

### 3.3 Async vs Task
- **F#**: `async { ... }` produces `Async<'T>`.
- **C#**: Wants `Task<'T>`.
- **Bridge**: Use `Async.StartAsTask` (F# side) or `FSharpAsync.StartAsTask` (C# side).

## 4. Metadata Conventions
- **Namespace Alignment**: `GA.Business.DSL` (F#) should map to `GA.Domain.*` (C#) via Adapters.
- **Null Safety**: F# code assumes non-nullity. C# *must* sanitize inputs before calling F#.
