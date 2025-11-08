namespace GA.Business.Config

open YamlDotNet.Serialization
open YamlDotNet.Serialization.NamingConventions
open System.IO
open System
open System.Collections.Generic

module ScalesConfig =
    type ScalesYaml =
        { Scales: ResizeArray<IDictionary<string, obj>> }

    let mutable private scalesData: ScalesYaml option = None

    let private loadScalesData () =
        try
            let configName = "Scales.yaml"

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
                let data = deserializer.Deserialize<ScalesYaml>(yaml)
                scalesData <- Some data
                true
            | None -> false
        with _ ->
            false

    let Scale =
        if scalesData.IsNone then
            loadScalesData () |> ignore

        scalesData

    type ScaleInfo =
        { Name: string
          Notes: string
          Description: string option }

    let mutable private version = Guid.NewGuid()

    let GetVersion () = version

    let ReloadConfig () =
        try
            if loadScalesData () then
                version <- Guid.NewGuid()
                true
            else
                false
        with _ ->
            false

    let GetAllScales () : IEnumerable<ScaleInfo> =
        if scalesData.IsNone then
            loadScalesData () |> ignore

        match scalesData with
        | None -> Seq.empty
        | Some data ->
            data.Scales
            |> Seq.choose (fun scaleDict ->
                if isNull scaleDict then
                    None
                else
                    let tryGetString key =
                        if scaleDict.ContainsKey(key) && not (isNull scaleDict[key]) then
                            match scaleDict[key] with
                            | :? string as s -> Some s
                            | _ -> None
                        else
                            None

                    match tryGetString "Name", tryGetString "Notes" with
                    | Some name, Some notes ->
                        Some
                            { Name = name
                              Notes = notes
                              Description = tryGetString "Description" }
                    | _ -> None)
