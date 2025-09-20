namespace GA.Business.Config

open FSharp.Configuration
open System.Collections.Immutable
open System.IO
open System
open System.Collections.Generic

module ModesConfig =
    type Config = YamlConfig<"Modes.yaml">
    type ModeInfo = 
        { 
            Name: string
            IntervalClassVector: string
            Notes: string
            Description: string option
            AlternateNames: IReadOnlyList<string> option
            FamilyName: string option
        }
        
    let getConfigPath() =
        let configName = "Modes.yaml"
        let possiblePaths = [
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, configName)
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "config", configName)
            Path.Combine(Environment.CurrentDirectory, configName)
            Path.Combine(Environment.CurrentDirectory, "config", configName)
            Path.Combine(__SOURCE_DIRECTORY__, configName)
        ]
        possiblePaths |> List.tryFind File.Exists
        
    let mutable private configPath = 
            match getConfigPath() with
            | Some path -> path
            | None -> failwith "Modes.yaml configuration file not found"
            
    let mutable private modeConfig = Config()
    let Mode = modeConfig
    let mutable private version = Guid.NewGuid()
    
    // Add memoization caches
    let private modeByNameCache = System.Collections.Concurrent.ConcurrentDictionary<string, ModeInfo>()
    let private modeByVectorCache = System.Collections.Concurrent.ConcurrentDictionary<string, ModeInfo>()
    
    let GetVersion() = version
    let ReloadConfig() =
        try
            configPath <- 
                match getConfigPath() with
                | Some path -> path
                | None -> failwith "Modes.yaml configuration file not found"
            modeConfig.Load(configPath)
            // Clear caches when config is reloaded
            modeByNameCache.Clear()
            modeByVectorCache.Clear()
            version <- Guid.NewGuid()
            true
        with
        | _ -> false
    
    // Helper function to extract a string value from a dictionary
    let private tryGetString (dict: IDictionary<string, obj>) key =
        if dict.ContainsKey(key) && not (isNull dict[key]) then
            match dict[key] with
            | :? string as s -> Some s
            | _ -> None
        else None
    
    // Helper function to extract a list of strings from a dictionary
    let private tryGetStringList (dict: IDictionary<string, obj>) key =
        if dict.ContainsKey(key) && not (isNull dict[key]) then
            try
                match dict[key] with
                | :? List<string> as list -> Some (list :> IReadOnlyList<string>)
                | :? IReadOnlyList<string> as list -> Some list
                | :? Array as arr -> 
                    let stringList = arr |> Seq.cast<string> |> Seq.toList
                    Some (stringList :> IReadOnlyList<string>)
                | _ -> None
            with
            | _ -> None
        else None        
    
    // Recursive function to extract modes from the hierarchical YAML structure
    let rec private extractModes (yamlObj: obj) (parentFamilyName: string option) (parentICV: string option) : ImmutableList<ModeInfo> =
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
                
                builder.Add({ 
                    Name = name
                    IntervalClassVector = intervalVector
                    Notes = notes
                    Description = description
                    AlternateNames = alternateNames
                    FamilyName = familyName
                })
            
            // If this is a top-level entry in the old format
            elif dict.Keys |> Seq.exists (fun k -> k <> "ModalFamilies" && not (k.StartsWith("#"))) then
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
                            
                            builder.Add({ 
                                Name = key
                                IntervalClassVector = intervalVector
                                Notes = notes
                                Description = description
                                AlternateNames = alternateNames
                                FamilyName = None
                            })
                        | _ -> () // Ignore non-dictionary values
        
        | _ -> () // Ignore other types
        
        builder.ToImmutable()
    
    let GetAllModes() : ImmutableList<ModeInfo> =
        try
            // Check if we have the new format with ModalFamilies
            if modeConfig.GetType().GetProperty("ModalFamilies") <> null then
                let modalFamilies = modeConfig.ModalFamilies
                extractModes modalFamilies None None
            else
                // Fall back to the old format
                modeConfig.GetType().GetProperties()
                |> Seq.choose (fun prop ->
                    let modeValue = prop.GetValue(modeConfig)
                    if isNull modeValue then None
                    else
                        let modeType = modeValue.GetType()
                        let intervalClassVectorProp = modeType.GetProperty("IntervalClassVector")
                        let notesProp = modeType.GetProperty("Notes")
                        let descriptionProp = modeType.GetProperty("Description")
                        let alternateNamesProp = modeType.GetProperty("AlternateNames")
                        if isNull intervalClassVectorProp || isNull notesProp then None
                        else
                            let intervalClassVector = intervalClassVectorProp.GetValue(modeValue) :?> string
                            let notes = notesProp.GetValue(modeValue) :?> string
                            let description = 
                                if isNull descriptionProp then None
                                else Some (descriptionProp.GetValue(modeValue) :?> string)
                            let alternateNames =
                                if isNull alternateNamesProp then None
                                else 
                                    let namesObj = alternateNamesProp.GetValue(modeValue)
                                    if isNull namesObj then None
                                    else
                                        match namesObj with
                                        | :? List<string> as list -> Some (list :> IReadOnlyList<string>)
                                        | _ -> failwithf $"Unexpected type for AlternateNames: %A{namesObj.GetType()}"
                            Some { 
                                Name = prop.Name
                                IntervalClassVector = intervalClassVector
                                Notes = notes
                                Description = description
                                AlternateNames = alternateNames
                                FamilyName = None
                            }
                )
                |> ImmutableList.CreateRange
        with ex ->
            printfn "Error parsing Modes.yaml: %s" ex.Message
            ImmutableList.Create()

    let TryGetModeByName (name: string) : ModeInfo option =
        let normalizedName = name.ToUpperInvariant()
        match modeByNameCache.TryGetValue(normalizedName) with
        | true, mode -> Some mode
        | false, _ ->
            let result = 
                GetAllModes()
                |> Seq.tryFind (fun mode -> 
                    String.Equals(mode.Name, name, StringComparison.OrdinalIgnoreCase) ||
                    match mode.AlternateNames with
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