namespace GA.Business.Config

open FSharp.Configuration

module Config =
    type TuningsConfig = YamlConfig<"Tunings.yaml">
    let Tuning = TuningsConfig()

    


    
