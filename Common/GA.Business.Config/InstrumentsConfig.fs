namespace GA.Business.Config

open FSharp.Configuration
open System

module InstrumentsConfig =
    type Config = YamlConfig<"./Instruments.yaml">
    let mutable private instrumentConfig = Config()

    let Instruments = instrumentConfig

    type TuningInfo = { Name: string; Tuning: string }
    type InstrumentInfo = { Name: string; Tunings: TuningInfo list }

    let configChanged = Event<unit>()

    let reloadConfig() =
        try
            instrumentConfig <- Config()
            configChanged.Trigger()
            Ok()
        with
        | ex -> Error ex.Message
        
    let getAllInstruments() =
        instrumentConfig.GetType().GetProperties()
        |> Array.choose (fun prop ->
            let instrumentValue = prop.GetValue(instrumentConfig)
            if isNull instrumentValue then None
            else
                let displayNameProp = instrumentValue.GetType().GetProperty("DisplayName")
                let displayName = 
                    if isNull displayNameProp then prop.Name
                    else displayNameProp.GetValue(instrumentValue) :?> string
                
                let tunings =
                    instrumentValue.GetType().GetProperties()
                    |> Array.filter (fun p -> p.Name <> "DisplayName")
                    |> Array.choose (fun tuningProp ->
                        let tuningInstance = tuningProp.GetValue(instrumentValue)
                        if isNull tuningInstance then None
                        else
                            let tuningType = tuningInstance.GetType()
                            let tuningDisplayNameProp = tuningType.GetProperty("DisplayName")
                            let tuningTuningProp = tuningType.GetProperty("Tuning")
                            if isNull tuningTuningProp then None
                            else
                                let tuningDisplayName = 
                                    if isNull tuningDisplayNameProp then tuningProp.Name
                                    else tuningDisplayNameProp.GetValue(tuningInstance) :?> string
                                let tuning = tuningTuningProp.GetValue(tuningInstance) :?> string
                                Some { Name = tuningDisplayName; Tuning = tuning }
                    )
                    |> Array.toList
                
                Some { Name = displayName; Tunings = tunings }
        )
        |> Array.toList

    let listAllInstrumentNames() =
        getAllInstruments()
        |> List.map (fun i -> i.Name)

    let listAllInstrumentTunings() =
        getAllInstruments()
        |> List.collect (fun i -> 
            i.Tunings 
            |> List.map (fun t -> sprintf "%s - %s" i.Name t.Name)
        )

    let findInstrumentsByName (searchTerm: string) =
        getAllInstruments()
        |> List.filter (fun i -> 
            i.Name.IndexOf(searchTerm, StringComparison.OrdinalIgnoreCase) >= 0
        )

    let tryGetInstrument name =
        getAllInstruments()
        |> List.tryFind (fun i -> i.Name.Equals(name, StringComparison.OrdinalIgnoreCase))