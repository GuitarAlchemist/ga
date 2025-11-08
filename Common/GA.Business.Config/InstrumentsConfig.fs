namespace GA.Business.Config

open YamlDotNet.Serialization
open YamlDotNet.Serialization.NamingConventions
open System.IO
open System

module InstrumentsConfig =
    type InstrumentsYaml =
        { Instruments: ResizeArray<System.Collections.Generic.IDictionary<string, obj>> }

    let mutable private instrumentsData: InstrumentsYaml option = None

    let private loadInstrumentsData () =
        try
            let configName = "Instruments.yaml"

            let possiblePaths =
                [ Path.Combine(AppDomain.CurrentDomain.BaseDirectory, configName)
                  Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "config", configName)
                  Path.Combine(Environment.CurrentDirectory, configName)
                  Path.Combine(Environment.CurrentDirectory, "config", configName) ]

            match possiblePaths |> List.tryFind File.Exists with
            | Some path ->
                let deserializer =
                    DeserializerBuilder()
                        .WithNamingConvention(PascalCaseNamingConvention.Instance)
                        .Build()

                let yaml = File.ReadAllText(path)
                let data = deserializer.Deserialize<InstrumentsYaml>(yaml)
                instrumentsData <- Some data
                true
            | None -> false
        with _ ->
            false

    let Instruments =
        if instrumentsData.IsNone then
            loadInstrumentsData () |> ignore

        instrumentsData

    type TuningInfo = { Name: string; Tuning: string }

    type InstrumentInfo =
        { Name: string
          Tunings: TuningInfo list }

    let configChanged = Event<unit>()

    let reloadConfig () =
        try
            if loadInstrumentsData () then
                configChanged.Trigger()
                Ok()
            else
                Error "Failed to load instruments data"
        with ex ->
            Error ex.Message

    let getAllInstruments () =
        if instrumentsData.IsNone then
            loadInstrumentsData () |> ignore

        match instrumentsData with
        | None -> []
        | Some data ->
            data.Instruments
            |> Seq.choose (fun instrumentDict ->
                if isNull instrumentDict then
                    None
                else
                    let tryGetString key =
                        if instrumentDict.ContainsKey(key) && not (isNull instrumentDict[key]) then
                            match instrumentDict[key] with
                            | :? string as s -> Some s
                            | _ -> None
                        else
                            None

                    match tryGetString "Name" with
                    | Some name ->
                        // For now, return a simple instrument with no tunings
                        // Full tuning support would require more complex YAML parsing
                        Some { Name = name; Tunings = [] }
                    | None -> None)
            |> Seq.toList

    let listAllInstrumentNames () =
        getAllInstruments () |> List.map (fun i -> i.Name)

    let listAllInstrumentTunings () =
        getAllInstruments ()
        |> List.collect (fun i -> i.Tunings |> List.map (fun t -> $"%s{i.Name} - %s{t.Name}"))

    let findInstrumentsByName (searchTerm: string) =
        getAllInstruments ()
        |> List.filter (fun i -> i.Name.IndexOf(searchTerm, StringComparison.OrdinalIgnoreCase) >= 0)

    let tryGetInstrument name =
        getAllInstruments ()
        |> List.tryFind (fun i -> i.Name.Equals(name, StringComparison.OrdinalIgnoreCase))
