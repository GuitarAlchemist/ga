namespace GA.Business.Config

open YamlDotNet.Serialization
open YamlDotNet.Serialization.NamingConventions
open System.Collections.Immutable
open System.IO
open System
open System.Collections.Generic

module SemanticConfig =
    [<CLIMutable>]
    type SemanticTag =
        { Id: string
          Name: string
          Description: string
          MusicalClues: string
          NarrativeFragment: string
          TitleTemplate: string
          Priority: int
          SuggestedContexts: ResizeArray<string> }

    [<CLIMutable>]
    type SemanticCategory =
        { Name: string
          Priority: int
          Tags: ResizeArray<SemanticTag> }

    [<CLIMutable>]
    type SemanticYaml =
        { Categories: ResizeArray<SemanticCategory> }

    let getConfigPath () =
        let configName = "SemanticNomenclature.yaml"
        let possiblePaths =
            [ Path.Combine(AppDomain.CurrentDomain.BaseDirectory, configName)
              Path.Combine(Environment.CurrentDirectory, configName)
              Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "bin", "Debug", "net10.0", configName)
              Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "bin", "Release", "net10.0", configName)
              Path.Combine(__SOURCE_DIRECTORY__, configName) ]
        possiblePaths |> List.tryFind File.Exists

    let mutable private configData: SemanticYaml option = None

    let private loadData () =
        try
            match getConfigPath() with
            | Some path ->
                let deserializer =
                    DeserializerBuilder()
                        .WithNamingConvention(PascalCaseNamingConvention.Instance)
                        .Build()
                let yaml = File.ReadAllText(path)
                configData <- Some (deserializer.Deserialize<SemanticYaml>(yaml))
                true
            | None -> 
                false
        with _ -> 
            false

    let private ensureLoaded () =
        if configData.IsNone then loadData() |> ignore

    let GetAllTags () =
        ensureLoaded()
        match configData with
        | Some data -> 
            data.Categories 
            |> Seq.collect (fun c -> c.Tags)
            |> Seq.toList
        | None -> []

    let GetAllCategories () =
        ensureLoaded()
        match configData with
        | Some data -> data.Categories |> Seq.toList
        | None -> []

    let TryGetTagById (id: string) =
        GetAllTags() |> List.tryFind (fun t -> String.Equals(t.Id, id, StringComparison.OrdinalIgnoreCase))

    let TryGetTagByIdManaged (id: string) =
        match TryGetTagById id with
        | Some t -> t
        | None -> Unchecked.defaultof<SemanticTag>

    let TryGetCategoryByTagId (id: string) =
        ensureLoaded()
        match configData with
        | Some data ->
            data.Categories 
            |> Seq.tryFind (fun c -> c.Tags |> Seq.exists (fun t -> String.Equals(t.Id, id, StringComparison.OrdinalIgnoreCase)))
        | None -> None

    let TryGetCategoryByTagIdManaged (id: string) =
        match TryGetCategoryByTagId id with
        | Some c -> c.Name
        | None -> null
