namespace GA.Business.Config

open FSharp.Configuration

module Instruments =
    type Config = YamlConfig<"Instruments.yaml">
    let Instrument = Config()
