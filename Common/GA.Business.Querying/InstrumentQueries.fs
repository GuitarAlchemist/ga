namespace GA.Business.Querying

open GA.Business.Core.Notes
open GA.Business.Core.Fretboard
open GA.Business.Config

(* http://dungpa.github.io/fsharp-cheatsheet/ *)

[<AutoOpen>]
module InstrumentQueries =
    let guitarTuning() =
        // Get guitar instrument from the new API
        match InstrumentsConfig.tryGetInstrument "Guitar" with
        | Some guitar ->
            // Find standard tuning
            match guitar.Tunings |> List.tryFind (fun t -> t.Name.Contains("Standard")) with
            | Some tuning -> tuning.Tuning |> parse<PitchCollection>
            | None -> None
        | None -> None

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
            | Guitar ->
                match InstrumentsConfig.tryGetInstrument "Guitar" with
                | Some guitar ->
                    match guitar.Tunings |> List.tryFind (fun t -> t.Name.Contains("Standard")) with
                    | Some tuning -> tuning.Tuning
                    | None -> null
                | None -> null
            | Other name ->
                match InstrumentsConfig.tryGetInstrument name with
                | Some instrument ->
                    match instrument.Tunings |> List.tryHead with
                    | Some tuning -> tuning.Tuning
                    | None -> null
                | None -> null
        sTuning |> parseTuning

    let fretboard() = Tuning.Default // Return default tuning instead of fretboard
