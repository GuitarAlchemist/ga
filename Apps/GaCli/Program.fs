module GA.Cli.Program

open System
open GA.Business.DSL.Closures.GaAsync
open GA.Business.DSL.Closures.GaClosureRegistry

// Force module initializers so every `do register()` call fires.
// F# modules only initialize when a value from them is accessed;
// `open` alone is a compile-time alias and does not trigger the CLR type initializer.
do GA.Business.DSL.Closures.BuiltinClosures.DomainClosures.register   ()
do GA.Business.DSL.Closures.BuiltinClosures.PipelineClosures.register ()
do GA.Business.DSL.Closures.BuiltinClosures.AgentClosures.register    ()
do GA.Business.DSL.Closures.BuiltinClosures.IoClosures.register       ()

// ── helpers ───────────────────────────────────────────────────────────────────

let private invoke name inputs =
    GaClosureRegistry.Global.Invoke(name, Map.ofList inputs)
    |> Async.RunSynchronously

let private ok label (result: Result<obj, GaError>) =
    match result with
    | Ok v    -> printfn "%s%O" label v
    | Error e -> eprintfn "Error: %O" e; exit 1

let private diatonic (chords: obj) =
    let arr = chords :?> string[]
    let labels = [| "I"; "ii"; "iii"; "IV"; "V"; "vi"; "vii°" |]
    arr |> Array.iteri (fun i c -> printf "%s=%s  " labels.[i] c)
    printfn ""

// ── usage ─────────────────────────────────────────────────────────────────────

let private usage () =
    printfn """ga — Guitar Alchemist language CLI

USAGE
  ga chord <symbol>                     Parse a chord symbol to JSON
  ga transpose <symbol> <semitones>     Transpose a chord by N semitones
  ga diatonic <root> [major|minor]      Get the 7 diatonic triads for a key
  ga progresson <sym1> <sym2> ... --by <n>
                                        Transpose every chord in a progression
  ga closures [domain|pipeline|agent|io]
                                        List registered closures
  ga ask <question>                     Ask the GA chatbot (needs server running)
  ga eval <fsharp-expression>           Evaluate an FSI expression (needs server)

EXAMPLES
  ga chord Am7
  ga transpose Cmaj9 5
  ga diatonic G major
  ga diatonic Bb minor
  ga progression Am F C G --by 2
  ga closures domain
  ga ask "What is a tritone substitution?"
"""

// ── commands ──────────────────────────────────────────────────────────────────

let private cmdChord symbol =
    invoke "domain.parseChord" [ "symbol", box symbol ] |> ok ""

let private cmdTranspose symbol semitones =
    invoke "domain.transposeChord"
        [ "symbol", box symbol; "semitones", box semitones ]
    |> ok ""

let private cmdDiatonic root scale =
    match invoke "domain.diatonicChords" [ "root", box root; "scale", box scale ] with
    | Error e -> eprintfn "Error: %O" e; exit 1
    | Ok v    ->
        printfn "Key of %s %s:" root scale
        diatonic v

let private cmdProgression (chords: string list) semitones =
    let results =
        chords |> List.map (fun sym ->
            match invoke "domain.transposeChord" [ "symbol", box sym; "semitones", box semitones ] with
            | Ok v    -> v :?> string
            | Error e -> eprintfn "  %s → Error: %O" sym e; sym)
    printfn "%s  →  %s" (String.concat " " chords) (String.concat " " results)

let private cmdClosures (category: string option) =
    let all =
        match category with
        | None   -> GaClosureRegistry.Global.List()
        | Some c ->
            let cat =
                match c.ToLowerInvariant() with
                | "domain"   -> GaClosureCategory.Domain
                | "pipeline" -> GaClosureCategory.Pipeline
                | "agent"    -> GaClosureCategory.Agent
                | _          -> GaClosureCategory.Io
            GaClosureRegistry.Global.List(?category = Some cat)
    if all.IsEmpty then
        printfn "(no closures found)"
    else
        let byCategory = all |> List.groupBy (fun c -> c.Category)
        for (cat, closures) in byCategory do
            printfn "\n[%A]" cat
            for c in closures do
                printfn "  %-35s %s" c.Name c.Description
    printfn ""

let private cmdAsk question =
    invoke "agent.theoryAgent" [ "question", box question ] |> ok ""

// ── entry point ───────────────────────────────────────────────────────────────

[<EntryPoint>]
let main argv =
    match argv |> Array.toList with
    | [] | ["help"] | ["--help"] | ["-h"] ->
        usage (); 0

    | "chord" :: symbol :: _ ->
        cmdChord symbol; 0

    | "transpose" :: symbol :: n :: _ ->
        match Int32.TryParse n with
        | true, semitones -> cmdTranspose symbol semitones; 0
        | false, _        -> eprintfn "semitones must be an integer"; 1

    | "diatonic" :: root :: scale :: _ ->
        cmdDiatonic root scale; 0

    | "diatonic" :: root :: _ ->
        cmdDiatonic root "major"; 0

    | "progression" :: rest ->
        // ga progression Am F C G --by 5
        let byIdx = rest |> List.tryFindIndex ((=) "--by")
        match byIdx with
        | None ->
            eprintfn "Usage: ga progression <chords...> --by <semitones>"; 1
        | Some idx ->
            let chords    = rest |> List.take idx
            let semStr    = rest |> List.skip (idx + 1) |> List.tryHead
            match semStr |> Option.bind (fun s -> match Int32.TryParse s with true, n -> Some n | _ -> None) with
            | None -> eprintfn "--by requires an integer"; 1
            | Some n -> cmdProgression chords n; 0

    | "closures" :: category :: _ ->
        cmdClosures (Some category); 0

    | "closures" :: _ ->
        cmdClosures None; 0

    | "ask" :: rest ->
        cmdAsk (String.concat " " rest); 0

    | cmd :: _ ->
        eprintfn "Unknown command: %s" cmd; usage (); 1
