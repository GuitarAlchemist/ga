namespace GA.Business.Config

namespace GA.Business.Config

open FSharp.Configuration
open System
open System.Collections.Generic

module ScalesConfig =
    type Config = YamlConfig<"Scales.yaml">
    let mutable private scaleConfig = Config()

    let Scale = scaleConfig

    type ScaleInfo = 
        { 
            Name: string
            Notes: string
            Description: string option
        }

    let mutable private version = Guid.NewGuid()

    let GetVersion() = version

    let ReloadConfig() =
        try
            scaleConfig <- Config()
            version <- Guid.NewGuid()
            true
        with
        | _ -> false

    let GetAllScales() : IEnumerable<ScaleInfo> =
        scaleConfig.GetType().GetProperties()
        |> Seq.choose (fun prop ->
            let scaleValue = prop.GetValue(scaleConfig)
            if isNull scaleValue then None
            else
                let scaleType = scaleValue.GetType()
                let notesProp = scaleType.GetProperty("Notes")
                let descriptionProp = scaleType.GetProperty("Description")
                if isNull notesProp then None
                else
                    let notes = notesProp.GetValue(scaleValue) :?> string
                    let description = 
                        if isNull descriptionProp then None
                        else Some (descriptionProp.GetValue(scaleValue) :?> string)
                    Some { 
                        Name = prop.Name
                        Notes = notes
                        Description = description
                    }
        )
