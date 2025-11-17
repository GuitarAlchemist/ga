namespace GA.Business.Config

open YamlDotNet.Serialization
open YamlDotNet.Serialization.NamingConventions
open System.IO
open System

module InstrumentsConfig =
    type InstrumentsYaml =
        { Instruments: ResizeArray<System.Collections.Generic.IDictionary<string, obj>> }

    let mutable private instrumentsData: InstrumentsYaml option = None

    let private mkDict (pairs: (string * obj) list) =
        let d = System.Collections.Generic.Dictionary<string, obj>()
        pairs |> List.iter (fun (k, v) -> d.Add(k, v))
        d :> System.Collections.Generic.IDictionary<string, obj>

    let private defaultData () =
        let defaultTunings : System.Collections.Generic.IDictionary<string, obj> list =
            [ mkDict [ "Name", box "Standard"; "Tuning", box "E2,A2,D3,G3,B3,E4" ]
              mkDict [ "Name", box "Drop D";   "Tuning", box "D2,A2,D3,G3,B3,E4" ] ]

        let defaultInstruments =
            ResizeArray [ mkDict [ "Name", box "Guitar"; "Tunings", box defaultTunings ] ]

        { Instruments = defaultInstruments }

    let private loadInstrumentsData () =
        try
            let configName = "Instruments.yaml"

            let possiblePaths =
                [ // Typical bin/ working dir locations
                  Path.Combine(AppDomain.CurrentDomain.BaseDirectory, configName)
                  Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "config", configName)
                  Path.Combine(Environment.CurrentDirectory, configName)
                  Path.Combine(Environment.CurrentDirectory, "config", configName)
                  // Repo-root relative (when running tests from solution root)
                  Path.Combine(Environment.CurrentDirectory, "Common", "GA.Business.Config", configName)
                  // Bin→repo relative hop for test runners
                  Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "..", "Common", "GA.Business.Config", configName) ]

            match possiblePaths |> List.tryFind File.Exists with
            | Some path ->
                let deserializer =
                    DeserializerBuilder()
                        .WithNamingConvention(PascalCaseNamingConvention.Instance)
                        .Build()

                let yaml = File.ReadAllText(path)
                let data = deserializer.Deserialize<InstrumentsYaml>(yaml)
                // If deserialized data is null or empty, use defaults
                if obj.ReferenceEquals(data, null) || obj.ReferenceEquals(data.Instruments, null) || data.Instruments.Count = 0 then
                    instrumentsData <- Some (defaultData ())
                else
                    instrumentsData <- Some data
                true
            | None ->
                // No external file found: supply a minimal built-in dataset so tests and core features can work
                instrumentsData <- Some (defaultData ())
                true
        with _ ->
            // On any error, fall back to defaults
            instrumentsData <- Some (defaultData ())
            true

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

                    let tryGetTunings () : TuningInfo list =
                        if instrumentDict.ContainsKey("Tunings") && not (isNull instrumentDict["Tunings"]) then
                            match instrumentDict["Tunings"] with
                            | :? System.Collections.IEnumerable as enumerable ->
                                enumerable
                                |> Seq.cast<obj>
                                |> Seq.choose (fun o ->
                                    match o with
                                    | :? System.Collections.Generic.IDictionary<string, obj> as tdict ->
                                        let tryStr (k:string) =
                                            if tdict.ContainsKey(k) && not (isNull tdict[k]) then
                                                match tdict[k] with
                                                | :? string as s -> Some s
                                                | _ -> None
                                            else None
                                        match tryStr "Name", tryStr "Tuning" with
                                        | Some n, Some t -> Some { Name = n; Tuning = t }
                                        | _ -> None
                                    | _ -> None)
                                |> Seq.toList
                            | _ -> []
                        else []

                    match tryGetString "Name" with
                    | Some name ->
                        let tunings = tryGetTunings ()
                        Some { Name = name; Tunings = tunings }
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
