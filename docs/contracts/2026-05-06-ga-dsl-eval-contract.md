# `ga_dsl_eval` MCP Tool — Contract

**Version:** 0.1.0 (draft, NOT frozen)
**Schema version:** 1
**Status:** Draft (Phase 0 of `2026-05-06-skills-orchestration-architecture` plan)
**Producers:** `Common/GA.Business.ML/Agents/Mcp/DslEvalMcpTools.cs` (chatbot in-process MCP tool)
**Consumers:** Any chatbot SKILL.md skill that wants to invoke a domain closure without a dedicated keyhole MCP tool. First consumer (Phase 2): `skills/voicing-search/SKILL.md`.
**Companion projects:** `Common/GA.Business.DSL/Closures/GaClosureRegistry.fs` (F# closure registry — single source of truth), `Common/GA.Business.DSL/Closures/BuiltinClosures/DomainClosures.fs` (the closures the chatbot sees).

---

## 1. Why This Contract Exists

The chatbot's in-process MCP registry exposes 7 keyhole tools. The F# `GaClosureRegistry` carries ~40 closures (chord parse, transpose, diatonic chords, voice leading, voicing search, polychord analysis, …). A chatbot SKILL.md wanting to answer *"find me a mellow Cm9 voicing"* must currently either:

- have a dedicated MCP tool wrapping that one operation (the keyhole pattern — 33 missing tools), or
- be unable to answer (the current state).

This contract is the **bridge**: one MCP tool — `ga_dsl_eval` — that lets a SKILL.md author refer to *any* visible closure by name, with the LLM emitting the closure's `name` plus a flat `args` map. The closure registry is already self-describing (each closure carries `Name`, `Description`, `InputSchema`, `OutputType`); this tool surfaces that contract through the MCP wire.

The contract is **not yet frozen**. The Phase 3 measurement gate decides whether it survives v0.1, gets bumped to v0.2 (richer arg shapes), or rolls back in favour of more keyhole tools. Treat additions as additive and optional until v2.

---

## 2. Tool Surface

The tool is registered as `ga_dsl_eval` in the MCP namespace and exposes three methods. Each method is invoked by the LLM via Microsoft.Extensions.AI's `AIFunction` plumbing (the same plumbing all other GA MCP tools use).

### 2.1 `ga_dsl_list_closures` — discovery

Returns the curated list of closures the chatbot is allowed to invoke.

**Input:** none.

**Output (`ClosureListResult`):**
```json
{
  "Closures": [
    {
      "Name": "domain.parseChord",
      "Description": "Parse a chord symbol into its interval structure.",
      "Category": "Domain",
      "Tags": ["chord", "parse", "music-theory"]
    },
    ...
  ],
  "Error": null
}
```

The result is filtered to **`GaClosureCategory.Domain` only** for v0.1. Pipeline / Io / Agent closures are NOT exposed — they touch network, filesystem, and other agents (destructive surface). Adding categories requires a contract version bump.

### 2.2 `ga_dsl_get_closure_schema` — contract inspection

Returns a single closure's input/output contract so the LLM can construct a valid `EvalClosure` call.

**Input:**
- `closureName` — string. Case-insensitive (the registry uses `OrdinalIgnoreCase`).

**Output (`ClosureSchemaResult`):**
```json
{
  "Name": "domain.transposeChord",
  "Description": "Transpose a chord symbol by N semitones.",
  "Category": "Domain",
  "Tags": ["chord", "transpose", "music-theory"],
  "InputSchema": {
    "symbol": "string",
    "semitones": "int"
  },
  "OutputType": "string (transposed chord symbol)",
  "Error": null
}
```

If the closure exists but isn't visible to the chatbot (wrong category), `Error` is `"closure '<name>' is not exposed to the chatbot (category: <Category>)"`.

If the closure doesn't exist at all, `Error` is `"closure '<sanitized-name>' not found; call ga_dsl_list_closures to see available names"`. The echoed name is sanitized via `McpEchoSanitizer` — same pattern used by the existing 6 MCP tools.

### 2.3 `ga_dsl_eval` — invocation

Runs a closure with the supplied args.

**Input:**
- `closureName` — string. Case-insensitive.
- `args` — flat key-value map: `Dictionary<string, string>`. **Every argument value is a string** at the wire level; type coercion to the closure's declared input types happens server-side per the rules in §3.

**Output (`DslEvalResult`):**
```json
{
  "ClosureName": "domain.transposeChord",
  "Result": "Eb",
  "ResultJson": "\"Eb\"",
  "ElapsedMs": 4,
  "Error": null
}
```

- `Result` — the closure's output as a string. For simple types (`string`, `int`, etc.) this is the canonical string form. For complex types (records, arrays), this is `null`; callers should read `ResultJson`.
- `ResultJson` — the closure's output serialised as JSON via `System.Text.Json` with default options. Always populated on success.
- `ElapsedMs` — wall-clock duration of the closure execution (excluding registry lookup).
- `Error` — non-null when the closure failed. See §4 for shape.

**Invariant:** when `Error` is non-null, `Result` and `ResultJson` are `null` and `ElapsedMs` reflects the elapsed time up to the failure point. Callers branch on `Error` first.

---

## 3. Argument Format and Coercion (v0.1)

For v0.1, the LLM sends `args` as a flat `Dictionary<string, string>`. The MCP tool coerces each value to the closure's declared input type per the closure's `InputSchema`.

### Coercion rules

| Declared type (substring match in `InputSchema` value) | Coercion | Example |
|---|---|---|
| starts with `"string"` | leave as string | `"symbol": "Cmaj7"` → `"Cmaj7"` |
| starts with `"int"` | `int.Parse(InvariantCulture)` | `"semitones": "3"` → `3` |
| starts with `"bool"` | `bool.Parse` | `"useFlat": "true"` → `true` |
| starts with `"double"` or `"float"` | `double.Parse(InvariantCulture)` | `"threshold": "0.85"` → `0.85` |
| anything else (incl. `"string[]"`, `"JSON"`, `"...record"`) | leave as string; closure handles parsing | `"chordList": "C G Am F"` → `"C G Am F"` |

Coercion failure (e.g. `"semitones": "high"`) returns an `Error` with code `arg-coerce-failed` and a message naming the offending argument.

Missing required args (per `InputSchema.Keys`) return an `Error` with code `missing-required-arg`.

Extra args not in `InputSchema` are passed through (the closure decides whether to ignore or error).

### What v0.1 explicitly does NOT support

- **Nested objects** (e.g. `args.upper = { Name: "Cmaj7" }`). Closures that need structured input must accept a JSON-string and parse internally. Phase 3 measurement decides whether v0.2 adds nested-object support.
- **Arrays as args** (e.g. `args.chords = ["C", "G", "Am"]`). Same — pass as a single string with the closure parsing it.
- **Streaming output**. The closure runs synchronously (`RunSynchronously` from F#'s `Async`); large result sets must be paginated by the closure itself (e.g. `topK` parameter) rather than streamed.

---

## 4. Error Shape

```json
{
  "ClosureName": "domain.transposeChord",
  "Result": null,
  "ResultJson": null,
  "ElapsedMs": 1,
  "Error": {
    "Code": "arg-coerce-failed",
    "Message": "argument 'semitones' could not be parsed as int (got 'high')",
    "ClosureCategory": "Domain"
  }
}
```

### Defined error codes

| Code | When |
|---|---|
| `closure-not-found` | No closure with that name in the registry |
| `closure-not-exposed` | Closure exists but its `Category` isn't in the exposed set |
| `missing-required-arg` | An argument named in `InputSchema` is absent from `args` |
| `arg-coerce-failed` | An argument couldn't be coerced from string to the declared type |
| `closure-runtime-error` | Closure executed and returned `Error` (`GaError.DomainError` / `ParseError` / `IoError` / `AgentError` / `Cancelled`); the `Message` contains `GaError.ToString()` |
| `closure-timeout` | Closure exceeded the per-invocation timeout (default: 30 s; see §5) |
| `closure-exception` | Unexpected .NET exception escaped the closure |

Echo fields (closure name, arg values) are sanitized via `McpEchoSanitizer` — same as the other 6 MCP tools. No raw user input flows back through `Message` without sanitization.

---

## 5. Operational Constraints

- **Closure invocation timeout:** 30 seconds default. Configurable via `MCP_DSL_EVAL_TIMEOUT_SECONDS` env var. Closures expected to take longer (anything in Pipeline / Io / Agent categories) are NOT in the exposed set, so 30 s is an upper bound for Domain operations.
- **Concurrency:** the registry is `ConcurrentDictionary`-backed; multiple in-flight `EvalClosure` calls are safe. Each closure's `Exec` body is responsible for its own thread-safety (the F# closures generally are pure-functional, so this is rarely an issue).
- **Caching:** v0.1 does not cache results. A closure that's deterministic and expensive (e.g. voicing search at high `topK`) will recompute each call. Phase 3 measurement decides whether result caching lands in v0.2.
- **Logging:** every `EvalClosure` invocation logs at `Information` level: `closureName`, `argCount`, `outcome` (`success` / `error-<code>`), `elapsedMs`. PII / user-input echo is sanitized.

---

## 6. Versioning and Compatibility

- **Adding a closure** to `BuiltinClosures.DomainClosures` is **non-breaking** (additive). No schema bump.
- **Renaming a closure** is **breaking**. Requires:
  - Schema version bump to v0.2
  - A migration entry in this contract under §8 with `links.supersedes`
  - Coordinated update of any SKILL.md that references the old name
  - At least one release cycle of the old + new name being aliased
- **Changing a closure's `InputSchema`** (renaming a key, changing a type) is **breaking**. Same migration rules as renaming.
- **Adding a new method to this MCP tool** (e.g. `ga_dsl_explain` for natural-language closure summaries) is **additive**. No schema bump.
- **Changing the arg format** from flat `Dictionary<string,string>` to nested JSON is a **major bump** (v1.0). Phase 3 measurement decides timing.

---

## 7. Sign-off Defaults (the 4 open questions from the plan)

1. **Arg format** = flat `Dictionary<string, string>` for v0.1. Decided.
2. **Visible closure set** = Domain category only. Pipeline / Io / Agent excluded.
3. **Phase 3 success threshold** = 80% LLM invocation success + 75% latency parity vs keyhole baseline.
4. **LSP pre-validation** = deferred to Phase 4 (reactive). Phase 1 ships without LSP coupling.

These can change before Phase 3 lands without bumping the contract version (the contract documents the surface, not the success criteria).

---

## 8. Migrations

(empty for v0.1)

When the first migration lands, follow the format:

```
### v0.1 → v0.2 (YYYY-MM-DD)

**Changes:**
- ...

**`links.supersedes`:** v0.1 closures `<name>` map to v0.2 `<new-name>`.
```
