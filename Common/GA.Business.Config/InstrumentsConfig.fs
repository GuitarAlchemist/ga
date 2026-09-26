namespace GA.Business.Config

open YamlDotNet.Serialization
open YamlDotNet.Serialization.NamingConventions
open System.IO
open System
open System.Collections.Generic

module InstrumentsConfig =
    type InstrumentsYaml =
        { Instruments: ResizeArray<System.Collections.Generic.IDictionary<string, obj>> }

    let mutable private instrumentsData: InstrumentsYaml option = None

    let private mkDict (pairs: (string * obj) list) =
        let d = System.Collections.Generic.Dictionary<string, obj>()
        pairs |> List.iter (fun (k, v) -> d.Add(k, v))
        d :> System.Collections.Generic.IDictionary<string, obj>

    let private defaultData () =
        let defaultTunings: System.Collections.Generic.IDictionary<string, obj> list =
            [ mkDict [ "Name", box "Standard"; "Tuning", box "E2 A2 D3 G3 B3 E4" ]
              mkDict [ "Name", box "Drop D"; "Tuning", box "D2 A2 D3 G3 B3 E4" ] ]

        let defaultInstruments =
            ResizeArray [ mkDict [ "Name", box "Guitar"; "Tunings", box defaultTunings ] ]

        { Instruments = defaultInstruments }

    /// Instruments.yaml is a mapping of instruments (the key is the instrument's name), each holding
    /// DisplayName/Icon scalars and one nested mapping per tuning (the key is the tuning's name, with a
    /// `Tuning:` pitch list). It has no `Instruments:` list, so it is read as a mapping and reshaped into
    /// the { Name; Tunings } dictionaries that getAllInstruments consumes.
    let private fromInstrumentMapping (yaml: string) : InstrumentsYaml =
        let deserializer = DeserializerBuilder().Build()
        let raw = deserializer.Deserialize<Dictionary<string, Dictionary<string, obj>>>(yaml)

        let instruments =
            if isNull raw then
                ResizeArray()
            else
                raw
                |> Seq.filter (fun kv -> not (isNull kv.Value))
                |> Seq.map (fun kv ->
                    let tunings =
                        kv.Value
                        |> Seq.choose (fun t ->
                            match t.Value with
                            | :? System.Collections.IDictionary as tuning when tuning.Contains("Tuning") ->
                                Some(mkDict [ "Name", box t.Key; "Tuning", tuning["Tuning"] ])
                            | _ -> None)
                        |> Seq.toList

                    mkDict [ "Name", box kv.Key; "Tunings", box tunings ])
                |> ResizeArray

        { Instruments = instruments }

    let private loadInstrumentsData () =
        try
            match ConfigFileLocator.findFile "Instruments.yaml" with
            | Some path ->
                let data = fromInstrumentMapping (File.ReadAllText(path))

                if data.Instruments.Count = 0 then
                    // A file that yields no instrument is a broken file, not a missing one: say so
                    // instead of silently answering with the built-in guitar.
                    eprintfn $"[InstrumentsConfig] %s{path} holds no instrument; using the built-in guitar"
                    instrumentsData <- Some(defaultData ())
                    false
                else
                    instrumentsData <- Some data
                    true
            | None ->
                // No external file found: supply a minimal built-in dataset so tests and core features can work
                instrumentsData <- Some(defaultData ())
                true
        with ex ->
            // Fall back to defaults so callers keep working, but make the failure visible: a silent
            // fallback here once hid that the loader read 1 instrument out of the file's 122.
            eprintfn $"[InstrumentsConfig] Failed to load Instruments.yaml: %s{ex.Message}; using the built-in guitar"
            instrumentsData <- Some(defaultData ())
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

                    let tryGetTunings () : TuningInfo list =
                        if instrumentDict.ContainsKey("Tunings") && not (isNull instrumentDict["Tunings"]) then
                            match instrumentDict["Tunings"] with
                            | :? System.Collections.IEnumerable as enumerable ->
                                enumerable
                                |> Seq.cast<obj>
                                |> Seq.choose (fun o ->
                                    match o with
                                    | :? System.Collections.Generic.IDictionary<string, obj> as tdict ->
                                        let tryStr (k: string) =
                                            if tdict.ContainsKey(k) && not (isNull tdict[k]) then
                                                match tdict[k] with
                                                | :? string as s -> Some s
                                                | _ -> None
                                            else
                                                None

                                        match tryStr "Name", tryStr "Tuning" with
                                        | Some n, Some t -> Some { Name = n; Tuning = t }
                                        | _ -> None
                                    | _ -> None)
                                |> Seq.toList
                            | _ -> []
                        else
                            []

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
