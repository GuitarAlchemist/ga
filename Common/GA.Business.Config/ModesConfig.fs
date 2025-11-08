namespace GA.Business.Config

open YamlDotNet.Serialization
open YamlDotNet.Serialization.NamingConventions
open System.Collections.Immutable
open System.IO
open System
open System.Collections.Generic

module ModesConfig =
    type ModeInfo =
        { Name: string
          IntervalClassVector: string
          Notes: string
          Description: string option
          AlternateNames: IReadOnlyList<string> option
          FamilyName: string option }

    type ModalFamily =
        { Name: string
          IntervalClassVector: string
          Modes: ResizeArray<IDictionary<string, obj>> }

    type ModesYaml =
        { ModalFamilies: ResizeArray<ModalFamily> }

    let getConfigPath () =
        let configName = "Modes_Simple.yaml"

        let possiblePaths =
            [ Path.Combine(AppDomain.CurrentDomain.BaseDirectory, configName)
              Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "config", configName)
              Path.Combine(Environment.CurrentDirectory, configName)
              Path.Combine(Environment.CurrentDirectory, "config", configName)
              Path.Combine(__SOURCE_DIRECTORY__, configName) ]

        possiblePaths |> List.tryFind File.Exists

    let mutable private configPath =
        match getConfigPath () with
        | Some path -> path
        | None -> failwith "Modes_Simple.yaml configuration file not found"

    let mutable private modesData: ModesYaml option = None
    let mutable private version = Guid.NewGuid()

    // Add memoization caches
    let private modeByNameCache =
        System.Collections.Concurrent.ConcurrentDictionary<string, ModeInfo>()

    let private modeByVectorCache =
        System.Collections.Concurrent.ConcurrentDictionary<string, ModeInfo>()

    let private loadModesData () =
        try
            let deserializer =
                DeserializerBuilder()
                    .WithNamingConvention(PascalCaseNamingConvention.Instance)
                    .Build()

            let yaml = File.ReadAllText(configPath)
            let data = deserializer.Deserialize<ModesYaml>(yaml)
            modesData <- Some data
            true
        with ex ->
            printfn $"Failed to load Modes_Simple.yaml: %s{ex.Message}"
            false

    let GetVersion () = version

    let ReloadConfig () =
        try
            configPath <-
                match getConfigPath () with
                | Some path -> path
                | None -> failwith "Modes_Simple.yaml configuration file not found"

            if loadModesData () then
                // Clear caches when config is reloaded
                modeByNameCache.Clear()
                modeByVectorCache.Clear()
                version <- Guid.NewGuid()
                true
            else
                false
        with _ ->
            false

    // Initialize on first access
    let private ensureLoaded () =
        if modesData.IsNone then
            loadModesData () |> ignore

    // Helper function to extract a string value from a dictionary
    let private tryGetString (dict: IDictionary<string, obj>) key =
        if dict.ContainsKey(key) && not (isNull dict[key]) then
            match dict[key] with
            | :? string as s -> Some s
            | _ -> None
        else
            None

    // Helper function to extract a list of strings from a dictionary
    let private tryGetStringList (dict: IDictionary<string, obj>) key =
        if dict.ContainsKey(key) && not (isNull dict[key]) then
            try
                match dict[key] with
                | :? List<string> as list -> Some(list :> IReadOnlyList<string>)
                | :? IReadOnlyList<string> as list -> Some list
                | :? Array as arr ->
                    let stringList = arr |> Seq.cast<string> |> Seq.toList
                    Some(stringList :> IReadOnlyList<string>)
                | _ -> None
            with _ ->
                None
        else
            None

    // Recursive function to extract modes from the hierarchical YAML structure
    let rec private extractModes
        (yamlObj: obj)
        (parentFamilyName: string option)
        (parentICV: string option)
        : ImmutableList<ModeInfo> =
        let builder = ImmutableList.CreateBuilder<ModeInfo>()

        match yamlObj with
        | :? List<obj> as list ->
            // Process a list of items (e.g., list of modal families or modes)
            for item in list do
                let modes = extractModes item parentFamilyName parentICV
                builder.AddRange(modes)

        | :? IDictionary<string, obj> as dict ->
            // Check if this is a modal family
            let familyName =
                match tryGetString dict "Name" with
                | Some name -> Some name
                | None -> parentFamilyName

            // Get the interval class vector for this level
            let icv =
                match tryGetString dict "IntervalClassVector" with
                | Some vector -> Some vector
                | None -> parentICV

            // Check if this dictionary has modes
            if dict.ContainsKey("Modes") && not (isNull dict["Modes"]) then
                // Process the modes in this family
                let modes = extractModes dict["Modes"] familyName icv
                builder.AddRange(modes)

            // Check if this dictionary has subfamilies
            elif dict.ContainsKey("SubFamilies") && not (isNull dict["SubFamilies"]) then
                // Process the subfamilies
                let subfamilies = extractModes dict["SubFamilies"] familyName icv
                builder.AddRange(subfamilies)

            // Check if this is a mode entry
            elif dict.ContainsKey("Notes") && not (isNull dict["Notes"]) then
                // This is a mode entry
                let name =
                    match tryGetString dict "Name" with
                    | Some n -> n
                    | None -> "Unknown Mode"

                let notes =
                    match tryGetString dict "Notes" with
                    | Some n -> n
                    | None -> ""

                let intervalVector =
                    match icv with
                    | Some v -> v
                    | None -> "<0 0 0 0 0 0>" // Default empty vector

                let description = tryGetString dict "Description"
                let alternateNames = tryGetStringList dict "AlternateNames"

                builder.Add(
                    { Name = name
                      IntervalClassVector = intervalVector
                      Notes = notes
                      Description = description
                      AlternateNames = alternateNames
                      FamilyName = familyName }
                )

            // If this is a top-level entry in the old format
            elif
                dict.Keys
                |> Seq.exists (fun k -> k <> "ModalFamilies" && not (k.StartsWith("#")))
            then
                // Process each key as a potential mode in the old format
                for KeyValue(key, value) in dict do
                    if not (key.StartsWith("#")) && key <> "ModalFamilies" then
                        match value with
                        | :? IDictionary<string, obj> as modeDict ->
                            let notes =
                                match tryGetString modeDict "Notes" with
                                | Some n -> n
                                | None -> ""

                            let intervalVector =
                                match tryGetString modeDict "IntervalClassVector" with
                                | Some v -> v
                                | None -> "<0 0 0 0 0 0>" // Default empty vector

                            let description = tryGetString modeDict "Description"
                            let alternateNames = tryGetStringList modeDict "AlternateNames"

                            builder.Add(
                                { Name = key
                                  IntervalClassVector = intervalVector
                                  Notes = notes
                                  Description = description
                                  AlternateNames = alternateNames
                                  FamilyName = None }
                            )
                        | _ -> () // Ignore non-dictionary values

        | _ -> () // Ignore other types

        builder.ToImmutable()

    let GetAllModes () : ImmutableList<ModeInfo> =
        try
            ensureLoaded ()

            match modesData with
            | None -> ImmutableList.Create()
            | Some data ->
                let builder = ImmutableList.CreateBuilder<ModeInfo>()

                for family in data.ModalFamilies do
                    for modeDict in family.Modes do
                        let name =
                            match tryGetString modeDict "Name" with
                            | Some n -> n
                            | None -> ""

                        if not (String.IsNullOrWhiteSpace(name)) then
                            let notes =
                                match tryGetString modeDict "Notes" with
                                | Some n -> n
                                | None -> ""

                            let intervalVector =
                                match tryGetString modeDict "IntervalClassVector" with
                                | Some v -> v
                                | None -> family.IntervalClassVector

                            let description = tryGetString modeDict "Description"
                            let alternateNames = tryGetStringList modeDict "AlternateNames"

                            builder.Add(
                                { Name = name
                                  IntervalClassVector = intervalVector
                                  Notes = notes
                                  Description = description
                                  AlternateNames = alternateNames
                                  FamilyName = Some family.Name }
                            )

                builder.ToImmutable()
        with ex ->
            printfn $"Error in GetAllModes: %s{ex.Message}"
            ImmutableList.Create()

    // Fallback manual parser (kept for reference but not used)
    let private parseManual () : ImmutableList<ModeInfo> =
        try
            let path = configPath

            if String.IsNullOrWhiteSpace(path) || not (File.Exists(path)) then
                ImmutableList.Create()
            else
                let lines = File.ReadAllLines(path)

                let rxICV =
                    System.Text.RegularExpressions.Regex("^\s*IntervalClassVector:\s*\"([^\"]+)\"")

                let rxFamilyName =
                    System.Text.RegularExpressions.Regex("^\s*-\s*Name:\s*\"([^\"]+)\"")

                let rxModes = System.Text.RegularExpressions.Regex("^\s*Modes:\s*$")

                let rxModeName =
                    System.Text.RegularExpressions.Regex("^\s*-\s*Name:\s*\"([^\"]+)\"")

                let rxNotes = System.Text.RegularExpressions.Regex("^\s*Notes:\s*\"(.*)\"\s*$")

                let rxAlt =
                    System.Text.RegularExpressions.Regex("^\s*AlternateNames:\s*\[(.*)\]\s*$")

                let rxDesc = System.Text.RegularExpressions.Regex("^\s*Description:\s*\"(.*)\"\s*$")

                let builder = ImmutableList.CreateBuilder<ModeInfo>()
                let mutable currentICV: string option = None
                let mutable currentFamily: string option = None
                let mutable inModes = false
                let mutable currentModeName: string option = None
                let mutable currentDesc: string option = None
                let mutable currentAlts: IReadOnlyList<string> option = None

                for raw in lines do
                    let line = raw
                    // Detect start of a family name at indent 2 ("  - Name:")
                    if not inModes then
                        let mFam = rxFamilyName.Match(line)

                        if mFam.Success && line.StartsWith("  - ") then
                            currentFamily <- Some mFam.Groups[1].Value
                            currentModeName <- None
                            currentDesc <- None
                            currentAlts <- None
                    // Interval class vector at family scope
                    let mIcv = rxICV.Match(line)

                    if mIcv.Success then
                        currentICV <- Some mIcv.Groups[1].Value
                    // Enter/exit modes block
                    if rxModes.IsMatch(line) then
                        inModes <- true
                    // Mode entries appear with deeper indent ("      - Name:") when inModes
                    if inModes then
                        let mMode = rxModeName.Match(line)

                        if mMode.Success && line.StartsWith("      - ") then
                            currentModeName <- Some mMode.Groups[1].Value
                            currentDesc <- None
                            currentAlts <- None
                        else
                            let mNotes = rxNotes.Match(line)

                            if mNotes.Success then
                                match currentModeName, currentICV with
                                | Some nm, Some icv ->
                                    let notes = mNotes.Groups[1].Value

                                    let info: ModeInfo =
                                        { Name = nm
                                          IntervalClassVector = icv
                                          Notes = notes
                                          Description = currentDesc
                                          AlternateNames = currentAlts
                                          FamilyName = currentFamily }

                                    builder.Add(info)
                                    currentModeName <- None
                                    currentDesc <- None
                                    currentAlts <- None
                                | _ -> ()
                            else
                                let mAlt = rxAlt.Match(line)

                                if mAlt.Success then
                                    let content = mAlt.Groups[1].Value

                                    let parts =
                                        content.Split(',', StringSplitOptions.RemoveEmptyEntries)
                                        |> Array.map (fun s -> s.Trim().Trim('"'))
                                        |> Array.toList

                                    currentAlts <- Some(parts :> IReadOnlyList<string>)
                                else
                                    let mD = rxDesc.Match(line)

                                    if mD.Success then
                                        currentDesc <- Some mD.Groups[1].Value
                    // Detect next family (outdent)
                    if
                        (not (line.StartsWith("      ")))
                        && line.StartsWith("  - ")
                        && not (line.Contains("Notes:") || line.Contains("Name:\""))
                    then
                        inModes <- false

                builder.ToImmutable()
        with _ ->
            ImmutableList.Create()

    let TryGetModeByName (name: string) : ModeInfo option =
        let normalizedName = name.ToUpperInvariant()

        match modeByNameCache.TryGetValue(normalizedName) with
        | true, mode -> Some mode
        | false, _ ->
            let result =
                GetAllModes()
                |> Seq.tryFind (fun mode ->
                    String.Equals(mode.Name, name, StringComparison.OrdinalIgnoreCase)
                    || match mode.AlternateNames with
                       | Some alternateNames ->
                           alternateNames
                           |> Seq.exists (fun altName ->
                               String.Equals(altName, name, StringComparison.OrdinalIgnoreCase))
                       | None -> false)

            match result with
            | Some mode ->
                modeByNameCache.TryAdd(normalizedName, mode) |> ignore
                // Also cache alternate names
                match mode.AlternateNames with
                | Some alternateNames ->
                    for altName in alternateNames do
                        modeByNameCache.TryAdd(altName.ToUpperInvariant(), mode) |> ignore
                | None -> ()

                Some mode
            | None -> None

    let TryGetModeByIntervalClassVector (intervalClassVector: string) : ModeInfo option =
        let normalizedVector = intervalClassVector.ToUpperInvariant()

        match modeByVectorCache.TryGetValue(normalizedVector) with
        | true, mode -> Some mode
        | false, _ ->
            let result =
                GetAllModes()
                |> Seq.tryFind (fun mode ->
                    String.Equals(mode.IntervalClassVector, intervalClassVector, StringComparison.OrdinalIgnoreCase))

            match result with
            | Some mode ->
                modeByVectorCache.TryAdd(normalizedVector, mode) |> ignore
                Some mode
            | None -> None
