---
title: "Connect curated music theory YAML files to the RAG pipeline via a generic knowledge loader"
date: "2026-03-07"
problem_type: "integration_issue"
component: "GA.Business.Config / GA.Data.MongoDB"
symptom: "Approximately 20 curated music theory YAML files (GuitarTechniques, ChordProgressions, VoiceLeading, AdvancedHarmony, Counterpoint, etc.) were copied to the build output directory on every build but never loaded, parsed, or made available for semantic retrieval — effectively dead configuration weight with no consumers."
root_cause: "Only the 5 domain-typed F# loaders (Modes, Scales, Instruments, TabSources, SemanticNomenclature) were wired up in GA.Business.Config. The remaining content YAML files had no corresponding loader, no typed schema, and no ingestion path into the vector/RAG pipeline, so they accumulated without ever contributing to chatbot knowledge."
solution_summary: "Three new components bridge the gap without requiring per-file typed schemas: YamlKnowledgeLoader.fs (F#) discovers and flattens all non-config YAMLs; YamlKnowledgeDocument.cs is a RagDocumentBase record for the yaml_knowledge collection; YamlKnowledgeSyncService.cs auto-ingests them with embeddings on every sync pass."
tags: [yaml, rag, knowledge-base, fsharp, mongodb, embeddings]
difficulty: "medium"
time_to_solve: "1 session"
---

# Connect curated music theory YAML files to the RAG pipeline via a generic knowledge loader

## Problem Statement

`GA.Business.Config` contained ~20 curated music theory content files
(`GuitarTechniques.yaml`, `ChordProgressions.yaml`, `VoiceLeading.yaml`,
`AdvancedHarmony.yaml`, `Counterpoint.yaml`, `ImprovisationConcepts.yaml`, and others)
that were registered in the `.fsproj` with `CopyToOutputDirectory: PreserveNewest` —
meaning they were physically present on disk next to the DLL on every build — but no
code ever opened them.

Only 5 files had dedicated typed F# loaders:

| File | Loader |
|---|---|
| `Modes.yaml` | `ModesConfig.fs` |
| `Scales.yaml` | `ScalesConfig.fs` |
| `Instruments.yaml` | `InstrumentsConfig.fs` |
| `TabSources.yaml` | `TabSourcesConfig.fs` |
| `SemanticNomenclature.yaml` | `SemanticConfig.fs` |

The other ~20 were dead weight. Chatbot agents (`TheoryAgent`, `TabAgent`,
`TechniqueAgent`) could not retrieve grounded answers from them because the data
was never embedded or indexed.

The failure was **silent**: no errors, no warnings, no runtime indication that the
knowledge was being ignored.

---

## Solution

The approach is a generic YAML-to-RAG ingestion pipeline that treats all non-typed
content YAML files in `GA.Business.Config` as a uniform knowledge corpus. An F#
module flattens each YAML entry into a plain-text chunk; a C# sync service then
embeds those chunks and upserts them into a dedicated MongoDB collection, making
the content available to chatbot agents via semantic retrieval.

### 1. F# Generic Flattener — `YamlKnowledgeLoader.fs`

The loader discovers the config directory at runtime, skips the five files that
already have dedicated typed loaders, and recursively flattens any YAML structure
to human-readable text.

```fsharp
// Files that have dedicated typed loaders — skip these
let private configFiles =
    Set.ofList
        [ "Modes.yaml"; "Scales.yaml"; "Instruments.yaml"
          "TabSources.yaml"; "SemanticNomenclature.yaml" ]

// Recursive YAML → text flattener (handles strings, lists, nested dicts)
let rec private flattenValue (depth: int) (value: obj) : string =
    match value with
    | null -> ""
    | :? string as s -> s
    | :? List<obj> as list ->
        list |> Seq.choose (fun item ->
            let text = flattenValue (depth + 1) item
            if String.IsNullOrWhiteSpace text then None else Some text)
        |> String.concat "; "
    | :? Dictionary<obj, obj> as dict ->
        dict |> Seq.choose (fun kv ->
            let v = flattenValue (depth + 1) kv.Value
            if String.IsNullOrWhiteSpace v then None
            else Some $"{kv.Key}: {v}")
        |> String.concat ". "
    | other -> string other

// Public entry point
let LoadAllKnowledgeEntries () : KnowledgeEntry list =
    match findYamlDirectory () with
    | None -> []
    | Some dir ->
        Directory.GetFiles(dir, "*.yaml")
        |> Array.filter (fun p -> not (configFiles.Contains(Path.GetFileName p)))
        |> Array.toList
        |> List.collect loadFile
```

Each YAML mapping that has a `Name` field becomes a `KnowledgeEntry` record with
`Name`, `Content` (all other fields flattened), `Category`, `SourceFile`, and `Tags`.

### 2. MongoDB Document Model — `YamlKnowledgeDocument.cs`

```csharp
public record YamlKnowledgeDocument : RagDocumentBase
{
    public required string EntryName  { get; init; }
    public required string Content    { get; init; }
    public required string Category   { get; init; }
    public required string SourceFile { get; init; }
    public List<string> Tags { get; init; } = [];

    public override string ToEmbeddingString() =>
        string.Join(" | ", new[] { EntryName, Category, Content }
            .Where(s => !string.IsNullOrWhiteSpace(s)));
}
```

### 3. Sync Service — `YamlKnowledgeSyncService.cs`

Calls the F# loader, generates text embeddings (with graceful per-entry fallback),
replaces the entire `yaml_knowledge` collection:

```csharp
public async Task<bool> SyncAsync()
{
    var entries = YamlKnowledgeLoader.LoadAllKnowledgeEntries().ToList();

    var documents = new List<YamlKnowledgeDocument>(entries.Count);
    foreach (var entry in entries)
    {
        var doc = new YamlKnowledgeDocument
        {
            EntryName  = entry.Name,
            Content    = entry.Content,
            Category   = entry.Category,
            SourceFile = entry.SourceFile,
            Tags       = [.. entry.Tags],
            CreatedAt  = now,
            UpdatedAt  = now
        };
        doc.GenerateSearchText();

        try   { doc.Embedding = [.. await embeddingService.GenerateEmbeddingAsync(doc.SearchText)]; }
        catch { /* stored without embedding — logged as warning */ }

        documents.Add(doc);
    }

    await collection.DeleteManyAsync(Builders<YamlKnowledgeDocument>.Filter.Empty);
    await collection.InsertManyAsync(documents);
    return true;
}
```

### 4. MongoDB Collection Property — `MongoDbService.cs`

```csharp
public IMongoCollection<YamlKnowledgeDocument> YamlKnowledge =>
    _database.GetCollection<YamlKnowledgeDocument>("yaml_knowledge");
```

### 5. F# Project — compile order in `GA.Business.Config.fsproj`

`YamlKnowledgeLoader.fs` is added last among the config modules:

```xml
<Compile Include="TabSourcesConfig.fs"/>
<Compile Include="YamlKnowledgeLoader.fs"/>
```

### How the pieces connect

`YamlKnowledgeLoader.fs` owns all file-discovery and flattening logic and exposes a
single public function `LoadAllKnowledgeEntries` — no YAML schema knowledge escapes
this module. On the C# side, `YamlKnowledgeSyncService` calls that function during the
data-sync pass (the same pass that indexes tabs, voicings, and set classes), maps each
`KnowledgeEntry` to a `YamlKnowledgeDocument`, generates a text embedding via the
shared `IEmbeddingService`, and bulk-replaces the `yaml_knowledge` MongoDB collection.
`YamlKnowledgeSyncService` is auto-discovered by the reflection-based `AddSyncServices()`
— no manual DI registration required.

---

## Prevention & Best Practices

### How to add new content YAMLs going forward

1. **Create the YAML file** in `Common/GA.Business.Config/` with a top-level mapping
   key whose value is a list of objects, each with at minimum a `Name` and `Category`
   field:
   ```yaml
   MyNewTopic:
     - Name: "Concept Name"
       Category: "Jazz"
       Description: "..."
   ```

2. **Register it in the `.fsproj`** as `<Content>` with `CopyToOutputDirectory:
   PreserveNewest`. Do **not** use `<None>` — that is the signal for config YAMLs
   with dedicated typed loaders.

3. **No code changes required.** `YamlKnowledgeLoader` auto-discovers all `*.yaml`
   files in the output directory that are not in the skip list.

4. **Trigger a sync.** `YamlKnowledgeSyncService` will pick up the new entries on
   the next sync pass.

### Distinguishing config YAMLs from content YAMLs

| Signal | Config YAML | Content YAML |
|---|---|---|
| Has a dedicated `*Config.fs` loader module | ✅ | ❌ |
| Registered as `<None>` in `.fsproj` | ✅ | ❌ |
| Registered as `<Content>` in `.fsproj` | ❌ | ✅ |
| Listed in `YamlKnowledgeLoader.configFiles` | ✅ | ❌ |
| Consumed by typed F# records | ✅ | ❌ |
| Ingested by the RAG embedding pipeline | ❌ | ✅ |

**Rule:** if the YAML does **not** need a typed F# schema consumed by application code,
it is a content YAML and must follow the content path.

### The skip list and when to update it

The `configFiles` set in `YamlKnowledgeLoader.fs` (lines 25–31) is the explicit
exclusion list. **Only add filenames here when you add a brand-new config YAML with a
dedicated typed F# loader.** Never add content YAMLs to this set — doing so silently
re-introduces the original bug.

### Warning signs that a YAML is being ignored

- File is registered as `<None>` in the `.fsproj` but has no `*Config.fs` loader
- Filename appears in `configFiles` but has no corresponding loader module
- Chatbot returns hallucinated answers on a topic covered by a YAML file
- `YamlKnowledgeLoader.LoadAllKnowledgeEntries()` returns zero entries for the file
  (check stderr — the loader logs `[YamlKnowledgeLoader] Skipping <file>: <reason>`)
- YAML entries lack a `Name` field (`entryFromDict` silently returns `None` for them)

### Suggested test

```csharp
[Test]
public void LoadAllKnowledgeEntries_ShouldIncludeEntriesFromEveryContentYaml()
{
    var entries = YamlKnowledgeLoader.LoadAllKnowledgeEntries();
    var entrySourceFiles = entries.Select(e => e.SourceFile).ToHashSet();

    var skipList = new[] { "Modes", "Scales", "Instruments", "TabSources", "SemanticNomenclature" };
    var yamlDir = AppDomain.CurrentDomain.BaseDirectory;
    var contentYamls = Directory.GetFiles(yamlDir, "*.yaml")
        .Select(p => Path.GetFileNameWithoutExtension(p))
        .Where(name => !skipList.Contains(name))
        .ToList();

    foreach (var expectedSource in contentYamls)
        Assert.That(entrySourceFiles, Does.Contain(expectedSource),
            $"No entries loaded from {expectedSource}.yaml — check Name field and skip list.");
}
```

---

## Related

### Existing Solution Docs

No prior solution doc covers RAG pipeline setup, YAML-to-MongoDB ingestion, or the
embedding sync pattern. The closest related docs:

- `docs/solutions/reviews/ce-review-feat-chatbot-orchestration-extraction-2026-03-07.md`
  — references the `SearchKnowledge` endpoint that queries the kind of data this
  feature now ingests
- `docs/solutions/runtime-errors/fsharp-module-init-closure-registry.md`
  — F# module initialization patterns; reference for F# config/loader pitfalls

### Related Plans

- `docs/plans/2026-03-02-feat-functional-chatbot-agentic-routing-plan.md`
  — chatbot RAG routing architecture that this knowledge ingestion feeds into

### PRs and Commits

- PR #5 — `feat/yaml-knowledge-rag` (commit `7df4933b`)
- PR #4 — `feat/scale-ian-ring-nomenclature` — preceded this; fixed broken
  `ScalesConfig` YAML loader (same root-cause class: config YAML with no working consumer)
- PR #2 — `feat/chatbot-orchestration-extraction` — established the orchestration
  layer that the RAG pipeline sits beneath

### Structural Analogues in the Codebase

| File | Relationship |
|---|---|
| `ScaleSyncService.cs` | Direct structural parallel to `YamlKnowledgeSyncService` — best reference for the sync pattern |
| `MusicTheoryRagService.cs` | RAG query service over theory documents; query-side consumer of this feature's output |
| `MusicTheoryRagDocument.cs` | Sibling RAG document model to `YamlKnowledgeDocument` |
| `EnhancedChordRagService.cs` | Another RAG retrieval service; same collection/retrieval pattern |
| `ModesConfig.fs` | Best reference for a correctly implemented typed F# config loader |
