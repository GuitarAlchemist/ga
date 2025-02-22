namespace GA.Business.Querying

open GA.Business.Core.Notes
open GA.Business.Core.Fretboard
open GA.Business.Config

(* http://dungpa.github.io/fsharp-cheatsheet/ *)
          
[<AutoOpen>]
module InstrumentQueries =
    let guitarTuning() = 
        InstrumentsConfig.Instruments.Guitar.Standard.Tuning 
        |> parse<PitchCollection>

    let parseTuning(sTuning : string): Tuning option =
        match sTuning |> parse<PitchCollection> with
        | Some pitchCollection -> new Tuning(pitchCollection) |> Some
        | _ -> None

    type Instrument =
        | Guitar
        | Other of name: string

    let tuning(inst: Instrument): Tuning option =
        let sTuning = 
            match inst with
            | Guitar -> InstrumentsConfig.Instruments.Guitar.Standard.Tuning
            | Other name ->
                // You might want to implement a lookup for other instruments here
                // For now, we'll return null for simplicity
                null
        sTuning |> parseTuning

    let fretboard() = Fretboard.Default