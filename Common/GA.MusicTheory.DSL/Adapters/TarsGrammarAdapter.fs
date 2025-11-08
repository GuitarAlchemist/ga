namespace GA.MusicTheory.DSL.Adapters

open System
open System.IO
open GA.MusicTheory.DSL.Types.GrammarTypes

/// <summary>
/// Adapter for TARS grammar system functionality
/// Provides grammar loading, metadata management, and versioning
/// Adapted from TARS.Engine.Grammar
/// </summary>
module TarsGrammarAdapter =

    // ============================================================================
    // GRAMMAR LOADING
    // ============================================================================

    /// Load a grammar from an external file
    let loadFromFile (path: string) : Result<string * GrammarMetadata, string> =
        try
            if not (File.Exists path) then
                Error $"Grammar file not found: %s{path}"
            else
                let content = File.ReadAllText path
                let fileName = Path.GetFileNameWithoutExtension path
                let metadata =
                    { Name = fileName
                      Version = "1.0.0"
                      Description = $"Grammar loaded from %s{path}"
                      Author = "Guitar Alchemist"
                      Created = DateTime.UtcNow
                      Modified = File.GetLastWriteTimeUtc path
                      Tags = []
                      Hash = Some (content.GetHashCode().ToString("X")) }
                Ok (content, metadata)
        with ex ->
            Error $"Failed to load grammar: %s{ex.Message}"

    /// Load a grammar from an embedded resource
    let loadFromResource (resourceName: string) : Result<string * GrammarMetadata, string> =
        try
            let assembly = System.Reflection.Assembly.GetExecutingAssembly()
            use stream = assembly.GetManifestResourceStream(resourceName)
            if stream = null then
                Error $"Embedded resource not found: %s{resourceName}"
            else
                use reader = new StreamReader(stream)
                let content = reader.ReadToEnd()
                let metadata =
                    { Name = resourceName
                      Version = "1.0.0"
                      Description = $"Grammar loaded from embedded resource %s{resourceName}"
                      Author = "Guitar Alchemist"
                      Created = DateTime.UtcNow
                      Modified = DateTime.UtcNow
                      Tags = []
                      Hash = Some (content.GetHashCode().ToString("X")) }
                Ok (content, metadata)
        with ex ->
            Error $"Failed to load embedded resource: %s{ex.Message}"

    /// Load a grammar from inline definition
    let loadFromInline (content: string) (name: string) : string * GrammarMetadata =
        let metadata =
            { Name = name
              Version = "1.0.0"
              Description = "Inline grammar definition"
              Author = "Guitar Alchemist"
              Created = DateTime.UtcNow
              Modified = DateTime.UtcNow
              Tags = []
              Hash = Some (content.GetHashCode().ToString("X")) }
        (content, metadata)

    /// Load a grammar from a GrammarSource
    let loadGrammar (source: GrammarSource) : Result<string * GrammarMetadata, string> =
        match source with
        | ExternalFile path -> loadFromFile path
        | InlineDefinition content -> Ok (loadFromInline content "inline")
        | EmbeddedResource resourceName -> loadFromResource resourceName

    // ============================================================================
    // GRAMMAR METADATA MANAGEMENT
    // ============================================================================

    /// Update grammar metadata
    let updateMetadata (metadata: GrammarMetadata) (updates: Map<string, obj>) : GrammarMetadata =
        let getValue key defaultValue =
            match Map.tryFind key updates with
            | Some value -> value :?> _
            | None -> defaultValue

        { metadata with
            Name = getValue "name" metadata.Name
            Version = getValue "version" metadata.Version
            Description = getValue "description" metadata.Description
            Author = getValue "author" metadata.Author
            Tags = getValue "tags" metadata.Tags
            Modified = DateTime.UtcNow }

    /// Add tags to grammar metadata
    let addTags (metadata: GrammarMetadata) (tags: string list) : GrammarMetadata =
        { metadata with
            Tags = List.distinct (metadata.Tags @ tags)
            Modified = DateTime.UtcNow }

    /// Remove tags from grammar metadata
    let removeTags (metadata: GrammarMetadata) (tags: string list) : GrammarMetadata =
        { metadata with
            Tags = List.filter (fun t -> not (List.contains t tags)) metadata.Tags
            Modified = DateTime.UtcNow }

    /// Update grammar version
    let updateVersion (metadata: GrammarMetadata) (newVersion: string) : GrammarMetadata =
        { metadata with
            Version = newVersion
            Modified = DateTime.UtcNow }

    // ============================================================================
    // GRAMMAR HASHING AND VERSIONING
    // ============================================================================

    /// Calculate hash for grammar content
    let calculateHash (content: string) : string =
        use sha256 = System.Security.Cryptography.SHA256.Create()
        let bytes = System.Text.Encoding.UTF8.GetBytes(content)
        let hash = sha256.ComputeHash(bytes)
        BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant()

    /// Check if grammar has changed based on hash
    let hasChanged (content: string) (metadata: GrammarMetadata) : bool =
        match metadata.Hash with
        | Some hash -> calculateHash content <> hash
        | None -> true

    /// Update hash in metadata
    let updateHash (content: string) (metadata: GrammarMetadata) : GrammarMetadata =
        { metadata with
            Hash = Some (calculateHash content)
            Modified = DateTime.UtcNow }

    // ============================================================================
    // GRAMMAR RESOLUTION
    // ============================================================================

    /// Resolve a grammar by name from a directory
    let resolveGrammar (grammarDir: string) (grammarName: string) : Result<string * GrammarMetadata, string> =
        let extensions = [".ebnf"; ".grammar"; ".tars"]
        let tryLoadWithExtension ext =
            let path = Path.Combine(grammarDir, grammarName + ext)
            if File.Exists path then
                Some (loadFromFile path)
            else
                None

        match List.tryPick tryLoadWithExtension extensions with
        | Some result -> result
        | None -> Error $"Grammar not found: %s{grammarName} (tried extensions: %A{extensions})"

    /// List all grammars in a directory
    let listGrammars (grammarDir: string) : string list =
        if not (Directory.Exists grammarDir) then
            []
        else
            let extensions = [".ebnf"; ".grammar"; ".tars"]
            extensions
            |> List.collect (fun ext ->
                Directory.GetFiles(grammarDir, "*" + ext)
                |> Array.map Path.GetFileNameWithoutExtension
                |> Array.toList)
            |> List.distinct

    // ============================================================================
    // GRAMMAR INDEXING
    // ============================================================================

    /// Grammar index entry
    type GrammarIndexEntry =
        { Name: string
          Path: string
          Metadata: GrammarMetadata
          LastIndexed: DateTime }

    /// Build an index of all grammars in a directory
    let buildIndex (grammarDir: string) : GrammarIndexEntry list =
        if not (Directory.Exists grammarDir) then
            []
        else
            let extensions = [".ebnf"; ".grammar"; ".tars"]
            extensions
            |> List.collect (fun ext ->
                Directory.GetFiles(grammarDir, "*" + ext)
                |> Array.toList)
            |> List.choose (fun path ->
                match loadFromFile path with
                | Ok (_, metadata) ->
                    Some { Name = Path.GetFileNameWithoutExtension path
                           Path = path
                           Metadata = metadata
                           LastIndexed = DateTime.UtcNow }
                | Error _ -> None)

    /// Save grammar index to JSON file
    let saveIndex (indexPath: string) (index: GrammarIndexEntry list) : Result<unit, string> =
        try
            let json = Newtonsoft.Json.JsonConvert.SerializeObject(index, Newtonsoft.Json.Formatting.Indented)
            File.WriteAllText(indexPath, json)
            Ok ()
        with ex ->
            Error $"Failed to save index: %s{ex.Message}"

    /// Load grammar index from JSON file
    let loadIndex (indexPath: string) : Result<GrammarIndexEntry list, string> =
        try
            if not (File.Exists indexPath) then
                Ok []
            else
                let json = File.ReadAllText(indexPath)
                let index = Newtonsoft.Json.JsonConvert.DeserializeObject<GrammarIndexEntry list>(json)
                Ok index
        with ex ->
            Error $"Failed to load index: %s{ex.Message}"

    // ============================================================================
    // GRAMMAR VALIDATION
    // ============================================================================

    /// Validate EBNF grammar syntax (basic validation)
    let validateEbnf (content: string) : Result<unit, string list> =
        let errors = ResizeArray<string>()
        
        // Check for balanced parentheses
        let mutable parenCount = 0
        let mutable braceCount = 0
        let mutable bracketCount = 0
        
        for c in content do
            match c with
            | '(' -> parenCount <- parenCount + 1
            | ')' -> parenCount <- parenCount - 1
            | '{' -> braceCount <- braceCount + 1
            | '}' -> braceCount <- braceCount - 1
            | '[' -> bracketCount <- bracketCount + 1
            | ']' -> bracketCount <- bracketCount - 1
            | _ -> ()
        
        if parenCount <> 0 then errors.Add("Unbalanced parentheses")
        if braceCount <> 0 then errors.Add("Unbalanced braces")
        if bracketCount <> 0 then errors.Add("Unbalanced brackets")
        
        if errors.Count = 0 then
            Ok ()
        else
            Error (List.ofSeq errors)

