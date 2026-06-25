namespace GA.Business.Config

open YamlDotNet.Serialization
open YamlDotNet.Serialization.NamingConventions
open System.IO
open System

module TabSourcesConfig =

    [<CLIMutable>]
    type TabSource =
        { Id: string
          Name: string
          Url: string
          Format: string
          Description: string
          License: string }

    [<CLIMutable>]
    type TabSourcesYaml = { Datasets: ResizeArray<TabSource> }

    let private findConfigPath () = ConfigFileLocator.findFile "TabSources.yaml"

    let Load () : TabSource list =
        match findConfigPath () with
        | Some path ->
            try
                let yaml = File.ReadAllText(path)

                let deserializer =
                    DeserializerBuilder()
                        .WithNamingConvention(CamelCaseNamingConvention.Instance)
                        .IgnoreUnmatchedProperties()
                        .Build()

                let data = deserializer.Deserialize<TabSourcesYaml>(yaml)

                if isNull (box data) || isNull (box data.Datasets) then
                    []
                else
                    data.Datasets |> Seq.toList
            with _ ->
                []
        | None -> []
