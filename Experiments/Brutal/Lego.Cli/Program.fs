module Lego.Cli.Program

open System
open System.Globalization
open Lego

let private usage =
    """usage: Lego.Cli test
       Lego.Cli blocks
       Lego.Cli describe <block[@version]> [--seed N] [--arg name=value]... [--max-depth N] [--max-instances N] [--max-boxes N]
describe prints lego-scene/1 and exits 0, or prints lego-errors/1 and exits 2."""

let rec private options (seed: uint64, args: string list, limits: Limits) rest =
    let number (text: string) =
        match Int32.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture) with
        | true, n -> Some n
        | _ -> None

    match rest with
    | [] -> Some(seed, args, limits)
    | "--seed" :: n :: tail ->
        match UInt64.TryParse(n, NumberStyles.None, CultureInfo.InvariantCulture) with
        | true, s -> options (s, args, limits) tail
        | _ -> None
    | "--arg" :: a :: tail -> options (seed, args @ [ a ], limits) tail
    | "--max-depth" :: n :: tail -> number n |> Option.bind (fun n -> options (seed, args, { limits with MaxDepth = n }) tail)
    | "--max-instances" :: n :: tail -> number n |> Option.bind (fun n -> options (seed, args, { limits with MaxInstances = n }) tail)
    | "--max-boxes" :: n :: tail -> number n |> Option.bind (fun n -> options (seed, args, { limits with MaxBoxes = n }) tail)
    | _ -> None

[<EntryPoint>]
let main argv =
    match List.ofArray argv with
    | [ "test" ] -> Tests.run ()
    | [ "blocks" ] ->
        Console.Out.WriteLine(Describe.library Api.Library)
        0
    | "describe" :: spec :: rest ->
        match options (0UL, [], Limits.Standard) rest with
        | Some(seed, args, limits) ->
            let d = Api.Describe(Api.Library, spec, seed, args, limits)
            Console.Out.WriteLine d.Json
            if d.Valid then 0 else 2
        | None ->
            eprintfn "%s" usage
            1
    | _ ->
        eprintfn "%s" usage
        1
