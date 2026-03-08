namespace GA.Business.Config

open YamlDotNet.Serialization
open System.IO
open System
open System.Collections.Generic

/// Generic loader that reads content YAML files and converts them into
/// flat knowledge entries suitable for embedding and RAG retrieval.
module YamlKnowledgeLoader =

    type KnowledgeEntry =
        { /// Name of the concept (e.g. "Sweep Picking", "ii-V-I").
          Name: string
          /// Flattened, human-readable description of the entry.
          Content: string
          /// Category field from the YAML, e.g. "Jazz", "Lead Guitar".
          Category: string
          /// Source filename without extension, e.g. "GuitarTechniques".
          SourceFile: string
          /// Tags derived from Category and SourceFile for filtering.
          Tags: string list }

    // ── Files that have dedicated typed loaders — skip these ─────────────────
    let private configFiles =
        Set.ofList
            [ "Modes.yaml"
              "Scales.yaml"
              "Instruments.yaml"
              "TabSources.yaml"
              "SemanticNomenclature.yaml" ]

    // ── Generic YAML → text flattener ─────────────────────────────────────────

    let rec private flattenValue (depth: int) (value: obj) : string =
        match value with
        | null -> ""
        | :? string as s -> s
        | :? bool as b -> if b then "yes" else "no"
        | :? int as i -> string i
        | :? float as f -> string f
        | :? List<obj> as list ->
            list
            |> Seq.choose (fun item ->
                let text = flattenValue (depth + 1) item
                if String.IsNullOrWhiteSpace text then None else Some text)
            |> String.concat "; "
        | :? Dictionary<obj, obj> as dict ->
            dict
            |> Seq.choose (fun kv ->
                let v = flattenValue (depth + 1) kv.Value
                if String.IsNullOrWhiteSpace v then None
                else Some $"{kv.Key}: {v}")
            |> String.concat ". "
        | other -> string other

    let private tryGetStr (dict: Dictionary<obj, obj>) (key: string) : string option =
        match dict.TryGetValue(box key) with
        | true, v when not (isNull v) ->
            let s = flattenValue 0 v
            if String.IsNullOrWhiteSpace s then None else Some s
        | _ -> None

    /// Convert one YAML mapping entry to a KnowledgeEntry.
    let private entryFromDict (sourceFile: string) (dict: Dictionary<obj, obj>) : KnowledgeEntry option =
        let name = tryGetStr dict "Name" |> Option.defaultValue ""
        if String.IsNullOrWhiteSpace name then None
        else
            let category = tryGetStr dict "Category" |> Option.defaultValue ""

            // Build content: all fields except Name, formatted as "Key: Value"
            let lines =
                dict
                |> Seq.choose (fun kv ->
                    let key = string kv.Key
                    if key = "Name" then None
                    else
                        let v = flattenValue 0 kv.Value
                        if String.IsNullOrWhiteSpace v then None
                        else Some $"{key}: {v}")
                |> Seq.toList

            let content =
                $"{name}"
                + (if lines.IsEmpty then "" else "\n" + String.concat "\n" lines)

            let tags =
                [ if not (String.IsNullOrWhiteSpace category) then yield category
                  yield sourceFile ]

            Some { Name = name; Content = content; Category = category; SourceFile = sourceFile; Tags = tags }

    // ── Per-file loading ──────────────────────────────────────────────────────

    let private loadFile (path: string) : KnowledgeEntry list =
        try
            let deserializer = DeserializerBuilder().Build()
            let yaml = File.ReadAllText(path)
            let root = deserializer.Deserialize<obj>(yaml)
            let sourceFile = Path.GetFileNameWithoutExtension(path)

            match root with
            | :? Dictionary<obj, obj> as rootDict ->
                [ for kv in rootDict do
                      match kv.Value with
                      | :? List<obj> as items ->
                          for item in items do
                              match item with
                              | :? Dictionary<obj, obj> as entryDict ->
                                  match entryFromDict sourceFile entryDict with
                                  | Some e -> yield e
                                  | None   -> ()
                              | _ -> ()
                      | _ -> () ]
            | _ -> []
        with ex ->
            eprintfn $"[YamlKnowledgeLoader] Skipping %s{Path.GetFileName path}: %s{ex.Message}"
            []

    // ── Directory discovery ───────────────────────────────────────────────────

    let private findYamlDirectory () : string option =
        let sentinel = "Scales.yaml" // always present after build
        [ AppDomain.CurrentDomain.BaseDirectory
          Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "config")
          Environment.CurrentDirectory
          Path.Combine(Environment.CurrentDirectory, "config") ]
        |> List.tryFind (fun dir -> File.Exists(Path.Combine(dir, sentinel)))

    // ── Public API ────────────────────────────────────────────────────────────

    /// Load all knowledge entries from content YAML files.
    /// Files with dedicated typed loaders (Modes, Scales, Instruments, etc.) are skipped.
    /// Returns an empty list if the config directory cannot be found.
    let LoadAllKnowledgeEntries () : KnowledgeEntry list =
        match findYamlDirectory () with
        | None ->
            eprintfn "[YamlKnowledgeLoader] Could not find YAML directory."
            []
        | Some dir ->
            Directory.GetFiles(dir, "*.yaml")
            |> Array.filter (fun p -> not (configFiles.Contains(Path.GetFileName p)))
            |> Array.toList
            |> List.collect loadFile
