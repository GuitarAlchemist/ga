namespace GA.Business.Querying

open GA.Business.Core.Notes
open GA.Business.Core.Fretboard
open GA.Business.Config.Instruments

(* http://dungpa.github.io/fsharp-cheatsheet/ *)
          
[<AutoOpen>]
module InstrumentQueries =
    let guitarTuning() = Instrument.Guitar.Standard.Tuning |> parse<PitchCollection>

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
            | Guitar -> Instrument.Guitar.Standard.Tuning
            | _ -> null
        sTuning |> parseTuning

    let freboard() = Fretboard.Default
