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
               
    let GetAllModes() : ImmutableList<ModeInfo> =
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
                            let names = alternateNamesProp.GetValue(modeValue) :?> string[]
                            Some (names :> IReadOnlyList<string>)
                    Some { 
                        Name = prop.Name
                        IntervalClassVector = intervalClassVector
                        Notes = notes
                        Description = description
                        AlternateNames = alternateNames
                    }
        )
        |> ImmutableList.CreateRange

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
