module GA.Cli.Program

open System
open System.IO
open GA.Business.DSL.Closures.GaAsync
open GA.Business.DSL.Closures.GaClosureRegistry

// Force module initializers so every `do register()` call fires.
// F# modules only initialize when a value from them is accessed;
// `open` alone is a compile-time alias and does not trigger the CLR type initializer.
do GA.Business.DSL.Closures.BuiltinClosures.DomainClosures.register   ()
do GA.Business.DSL.Closures.BuiltinClosures.PipelineClosures.register ()
do GA.Business.DSL.Closures.BuiltinClosures.AgentClosures.register    ()
do GA.Business.DSL.Closures.BuiltinClosures.IoClosures.register       ()
do GA.Business.DSL.Closures.BuiltinClosures.TabClosures.register      ()

// ── helpers ───────────────────────────────────────────────────────────────────

let private invoke name inputs =
    GaClosureRegistry.Global.Invoke(name, Map.ofList inputs)
    |> Async.RunSynchronously

/// Read one line from stdin when the input is redirected (piped). Returns "" if nothing.
let private fromStdin () =
    if Console.IsInputRedirected then
        match Console.ReadLine() with null -> "" | s -> s.Trim()
    else ""

let private ok label (result: Result<obj, GaError>) =
    match result with
    | Ok v    -> printfn "%s%O" label v
    | Error e -> eprintfn "Error: %O" e; exit 1

let private diatonic (chords: obj) =
    let arr = chords :?> string[]
    let labels = [| "I"; "ii"; "iii"; "IV"; "V"; "vi"; "vii°" |]
    arr |> Array.iteri (fun i c -> printf "%s=%s  " labels.[i] c)
    printfn ""

// ── skill scaffold helpers ────────────────────────────────────────────────────

/// Convert a space/dash/underscore-delimited name to PascalCase.
/// "GA Chords" → "GaChords", "fret-span" → "FretSpan"
let private toPascalCase (s: string) =
    s.Split([|' '; '-'; '_'|], StringSplitOptions.RemoveEmptyEntries)
    |> Array.map (fun p ->
        if p.Length = 0 then p
        else Char.ToUpper(p.[0]).ToString() + p.[1..].ToLower())
    |> String.concat ""

/// Strip surrounding YAML quotes from a value string.
let private stripYamlQuotes (s: string) =
    let s = s.Trim()
    if s.Length >= 2 &&
       ((s.[0] = '"'  && s.[s.Length-1] = '"') ||
        (s.[0] = '\'' && s.[s.Length-1] = '\'')) then
        s.[1..s.Length-2]
    else s

/// Parse frontmatter from a SKILL.md file.
/// Returns Some (name, description, triggers) or None.
let private parseSkillMdFrontmatter (filePath: string) =
    let lines = File.ReadAllLines filePath
    if lines.Length < 2 || lines.[0].Trim() <> "---" then None
    else
        let rest   = lines |> Array.skip 1
        let endIdx = rest  |> Array.tryFindIndex (fun l -> l.Trim() = "---")
        match endIdx with
        | None -> None
        | Some idx ->
            let frontLines = rest.[0..idx - 1]
            let mutable name        = ""
            let mutable description = ""
            let triggers            = ResizeArray<string>()
            let mutable inTriggers  = false
            for line in frontLines do
                let tl = line.TrimStart()
                let afterColon (s: string) =
                    stripYamlQuotes (s.Substring(s.IndexOf(':') + 1).Trim())
                if   tl.StartsWith("name:",        StringComparison.OrdinalIgnoreCase) then
                    name        <- afterColon tl; inTriggers <- false
                elif tl.StartsWith("description:", StringComparison.OrdinalIgnoreCase) then
                    description <- afterColon tl; inTriggers <- false
                elif tl.StartsWith("triggers:",    StringComparison.OrdinalIgnoreCase) then
                    inTriggers <- true
                elif inTriggers && tl.StartsWith("- ") then
                    triggers.Add(stripYamlQuotes tl.[2..])
                elif tl <> "" && not (line.StartsWith(" ") || line.StartsWith("\t")) then
                    inTriggers <- false
            if name = "" then None
            else Some (name, description, triggers |> Seq.toList)

/// Find the repo root by crawling up from the binary directory.
let private findRepoRoot () =
    let mutable dir : DirectoryInfo = DirectoryInfo(AppContext.BaseDirectory)
    let mutable result = ""
    while dir <> null && result = "" do
        if Directory.Exists(Path.Combine(dir.FullName, ".git")) ||
           File.Exists(Path.Combine(dir.FullName, ".git")) then
            result <- dir.FullName
        else
            dir <- dir.Parent
    result

/// Emit a C# IOrchestratorSkill skeleton following the FretSpanSkill pattern.
let private cmdSkillScaffold (path: string) =
    if not (File.Exists path) then
        eprintfn "Error: file not found: %s" path; exit 1
    match parseSkillMdFrontmatter path with
    | None ->
        eprintfn "Error: no valid frontmatter (or missing 'name:') in %s" path; exit 1
    | Some (name, description, triggers) ->
        let className = toPascalCase name + "Skill"
        let fileName  = path |> Path.GetFileName

        // ── Build triggers section ───────────────────────────────────────────
        let triggersField, canHandleBody =
            if triggers.IsEmpty then
                "\n    // No triggers defined — add to SKILL.md frontmatter and re-scaffold.\n",
                "        return false; // Add triggers to SKILL.md frontmatter and re-scaffold."
            else
                let items =
                    triggers
                    |> List.map (fun t -> sprintf "        \"%s\"" t)
                    |> String.concat ",\n"
                sprintf "\n    private static readonly IReadOnlyList<string> _triggers =\n    [\n%s,\n    ];\n" items,
                "        var lower = message.ToLowerInvariant();\n        return _triggers.Any(t => lower.Contains(t));"

        // ── Assemble .cs file using a template string ────────────────────────
        let cs =
            "namespace GA.Business.ML.Agents.Skills;\n\n" +
            "using GA.Business.Core.Orchestration.Abstractions;\n\n" +
            sprintf "/// <summary>\n/// %s\n/// </summary>\n" description +
            sprintf "/// <remarks>\n/// Generated by <c>ga skill scaffold</c> from <c>%s</c>.\n" fileName +
            "/// Implement domain logic here; remove SKILL.md triggers once validated.\n/// </remarks>\n" +
            sprintf "public sealed class %s(ILogger<%s> logger) : IOrchestratorSkill\n{\n" className className +
            triggersField + "\n" +
            sprintf "    public string Name        => \"%s\";\n" name +
            sprintf "    public string Description => \"%s\";\n\n" description +
            "    public bool CanHandle(string message)\n    {\n" +
            canHandleBody + "\n    }\n\n" +
            "    public Task<AgentResponse> ExecuteAsync(string message, CancellationToken ct = default)\n    {\n" +
            "        logger.LogInformation(\"{Skill} handling: {Message}\", Name, message);\n\n" +
            "        // TODO: implement domain logic (pure C# — no LLM required)\n" +
            sprintf "        throw new NotImplementedException(\n            $\"Implement %s domain logic in ExecuteAsync. \" +\n" name +
            "            \"Once working, remove triggers from SKILL.md.\");\n" +
            "    }\n}\n"

        // ── Write output ──────────────────────────────────────────────────────
        let repoRoot = findRepoRoot ()
        let outputPath =
            let rel = Path.Combine("Common", "GA.Business.ML", "Agents", "Skills", $"{className}.cs")
            if repoRoot = "" then rel else Path.Combine(repoRoot, rel)

        File.WriteAllText(outputPath, cs)
        printfn "Scaffolded: %s" outputPath
        printfn "  class:  %s" className
        printfn "  skill:  %s" name
        if triggers.IsEmpty then
            printfn "  WARNING: no triggers found — add to SKILL.md and re-scaffold"
        else
            printfn "  triggers (%d): %s" triggers.Length (String.concat ", " triggers)

let private cmdSkill (args: string list) =
    match args with
    | "scaffold" :: path :: _ -> cmdSkillScaffold path
    | "scaffold" :: [] ->
        eprintfn "Usage: ga skill scaffold <SKILL.md path>"; exit 1
    | sub :: _ ->
        eprintfn "Unknown skill subcommand: %s. Available: scaffold" sub; exit 1
    | [] ->
        printfn "ga skill — skill management commands"
        printfn ""
        printfn "  ga skill scaffold <path>   Generate a C# IOrchestratorSkill skeleton from a SKILL.md"

// ── usage ─────────────────────────────────────────────────────────────────────

let private usage () =
    printfn """ga — Guitar Alchemist language CLI

USAGE
  ga chord <symbol>                     Parse a chord symbol to JSON
  ga intervals <symbol>                 Show interval names (P1, m3, P5…)
  ga transpose <symbol> <semitones>     Transpose a chord by N semitones
  ga diatonic <root> [major|minor]      Get the 7 diatonic triads for a key
  ga relative <root> [major|minor]      Get the relative major/minor key
  ga analyze <chord1> <chord2> ...      Infer key + Roman numeral analysis
  ga query --key <root> --scale <s>     Filter diatonic chords by quality/interval
    [--quality major|minor|dim|aug]
    [--has-interval <name>]
    [--degree <roman>]
  ga project <symbol> <field> ...       Project chord fields (root quality intervals bass pc)
  ga join <chord1> <chord2>             Common-tone / voice-leading analysis
  ga progression <sym1> <sym2> ... --by <n>
                                        Transpose every chord in a progression
  ga pipe <symbol> <op[:arg]> ...       Chain operations on a chord
  ga closures [domain|pipeline|agent|io]
                                        List registered closures
  ga ask <question>                     Ask the GA chatbot (needs server running)
  ga skill scaffold <SKILL.md path>    Generate a C# IOrchestratorSkill skeleton

PIPE OPERATIONS
  transpose:<n>   Transpose by N semitones (transforms the current chord)
  intervals       Show interval names       (display only)
  diatonic[:<scale>]  Show diatonic triads  (display only)
  relative[:<scale>]  Show relative key     (display only)
  chord           Show parsed JSON          (display only)

EXAMPLES
  ga chord Am7
  ga intervals Cmaj9
  ga transpose Bb minor 3
  ga diatonic Bb minor
  ga relative A minor
  ga analyze Am F C G
  ga progression Am F C G --by 2
  ga pipe Am7 transpose:7 intervals diatonic:major
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

let private cmdIntervals symbol =
    match invoke "domain.chordIntervals" [ "symbol", box symbol ] with
    | Error e -> eprintfn "Error: %O" e; exit 1
    | Ok v    ->
        let arr = v :?> string[]
        printfn "%s:  %s" symbol (String.concat "  " arr)

let private cmdRelative root scale =
    match invoke "domain.relativeKey" [ "root", box root; "scale", box scale ] with
    | Error e -> eprintfn "Error: %O" e; exit 1
    | Ok v    -> printfn "%s %s → relative: %O" root scale v

let private getOpt flag (args: string list) =
    args |> List.tryFindIndex ((=) flag)
    |> Option.bind (fun i -> args |> List.skip (i + 1) |> List.tryHead)

let private cmdQuery (args: string list) =
    let key   = getOpt "--key"   args
    let scale = getOpt "--scale" args |> Option.defaultValue "major"
    let qual  = getOpt "--quality" args
    let iv    = getOpt "--has-interval" args
    let deg   = getOpt "--degree" args
    match key with
    | None -> eprintfn "Usage: ga query --key <root> [--scale major|minor] [--quality major|minor|diminished] [--has-interval <name>] [--degree <roman>]"; exit 1
    | Some k ->
        let inputs =
            [ yield "key",   box k
              yield "scale", box scale
              match qual with Some q -> yield "quality",     box q | None -> ()
              match iv   with Some v -> yield "hasInterval", box v | None -> ()
              match deg  with Some d -> yield "degree",      box d | None -> () ]
        match invoke "domain.queryChords" inputs with
        | Error e -> eprintfn "Error: %O" e; exit 1
        | Ok v    ->
            let arr = v :?> string[]
            if arr.Length = 0 then printfn "(no matching chords)"
            else
                printfn "Key: %s %s" k scale
                arr |> Array.iter (printfn "  %s")

let private cmdProject symbol (fields: string list) =
    match invoke "domain.projectChord" [ "symbol", box symbol; "fields", box (String.concat " " fields) ] with
    | Error e -> eprintfn "Error: %O" e; exit 1
    | Ok v    -> printfn "%O" v

let private cmdJoin chord1 chord2 =
    match invoke "domain.commonTones" [ "chord1", box chord1; "chord2", box chord2 ] with
    | Error e -> eprintfn "Error: %O" e; exit 1
    | Ok v    -> printfn "%O" v

let private cmdAnalyze (chords: string list) =
    match invoke "domain.analyzeProgression" [ "chords", box (String.concat " " chords) ] with
    | Error e -> eprintfn "Error: %O" e; exit 1
    | Ok v    -> printfn "%O" v

/// Extract the root note string from a parsed chord JSON, e.g. {"root":"Eb",...} -> "Eb".
let private chordRoot chordSymbol =
    match invoke "domain.parseChord" [ "symbol", box chordSymbol ] with
    | Error _ -> chordSymbol   // fallback: treat the whole symbol as root
    | Ok v ->
        let json = v :?> string
        let m = System.Text.RegularExpressions.Regex.Match(json, "\"root\":\"([^\"]+)\"")
        if m.Success then m.Groups.[1].Value else chordSymbol

let private cmdPipe (args: string list) =
    match args with
    | [] -> eprintfn "Usage: ga pipe <symbol> [op[:arg]]..."; exit 1
    | symbol :: ops ->
        let mutable current = symbol
        printf "%s" current
        for op in ops do
            let parts  = op.Split([|':'|], 2)
            let opName = parts.[0].ToLowerInvariant()
            let opArg  = if parts.Length > 1 then Some parts.[1] else None
            match opName with
            | "transpose" ->
                match opArg |> Option.bind (fun s ->
                    match Int32.TryParse s with true, n -> Some n | _ -> None) with
                | None -> eprintfn "transpose requires an integer arg, e.g. transpose:5"; exit 1
                | Some n ->
                    match invoke "domain.transposeChord" [ "symbol", box current; "semitones", box n ] with
                    | Error e -> eprintfn "Error: %O" e; exit 1
                    | Ok v ->
                        current <- v :?> string
                        printf " | transpose:%d | %s" n current
            | "intervals" ->
                match invoke "domain.chordIntervals" [ "symbol", box current ] with
                | Error e -> eprintfn "Error: %O" e; exit 1
                | Ok v ->
                    let arr = v :?> string[]
                    printf " | %s" (String.concat " " arr)
            | "diatonic" ->
                let scale = defaultArg opArg "major"
                let root  = chordRoot current
                match invoke "domain.diatonicChords" [ "root", box root; "scale", box scale ] with
                | Error e -> eprintfn "Error: %O" e; exit 1
                | Ok v ->
                    let arr    = v :?> string[]
                    let labels = [| "I"; "ii"; "iii"; "IV"; "V"; "vi"; "vii°" |]
                    let row    = Array.map2 (sprintf "%s=%s") labels arr |> String.concat "  "
                    printf " | %s(%s): %s" root scale row
            | "relative" ->
                let scale = defaultArg opArg "major"
                let root  = chordRoot current
                match invoke "domain.relativeKey" [ "root", box root; "scale", box scale ] with
                | Error e -> eprintfn "Error: %O" e; exit 1
                | Ok v    -> printf " | relative: %O" v
            | "chord" ->
                match invoke "domain.parseChord" [ "symbol", box current ] with
                | Error e -> eprintfn "Error: %O" e; exit 1
                | Ok v    -> printf " | %O" v
            | "analyze" ->
                cmdAnalyze [current]
            | unknown ->
                eprintfn "Unknown pipe operation: %s" unknown; exit 1
        printfn ""

let private cmdTab (args: string list) =
    match args with
    | "parse" :: rest ->
        let text = match rest with t :: _ -> t | [] -> fromStdin ()
        if text = "" then eprintfn "Usage: ga tab parse <ascii-text>"; exit 1
        match invoke "tab.parseAscii" [ "text", box text ] with
        | Error e -> eprintfn "Error: %O" e; exit 1
        | Ok v    -> printfn "%O" v

    | "vextab" :: rest ->
        let text = match rest with t :: _ -> t | [] -> fromStdin ()
        if text = "" then eprintfn "Usage: ga tab vextab <vextab-text>"; exit 1
        match invoke "tab.parseVexTab" [ "text", box text ] with
        | Error e -> eprintfn "Error: %O" e; exit 1
        | Ok v    -> printfn "%O" v

    | "generate" :: symbol :: _ ->
        match invoke "tab.generateChord" [ "symbol", box symbol ] with
        | Error e -> eprintfn "Error: %O" e; exit 1
        | Ok v    -> printfn "%O" v

    | "sources" :: _ ->
        invoke "tab.sources" [] |> ok ""

    | "fetch" :: rest ->
        if rest.IsEmpty then eprintfn "Usage: ga tab fetch <query>"; exit 1
        let query = String.concat " " rest
        match invoke "tab.fetch" [ "query", box query ] with
        | Error e -> eprintfn "Error: %O" e; exit 1
        | Ok v    -> printfn "%O" v

    | "url" :: url :: _ ->
        match invoke "tab.fetchUrl" [ "url", box url ] with
        | Error e -> eprintfn "Error: %O" e; exit 1
        | Ok v    -> printfn "%O" v

    | sub :: _ -> eprintfn "Unknown tab subcommand: %s" sub; exit 1
    | [] ->
        printfn """ga tab — tab parsing, generation, and fetching

  ga tab parse <ascii-text>     Parse ASCII tab notation
  ga tab vextab <vextab-text>   Validate and re-emit VexTab
  ga tab generate <symbol>      Generate VexTab scaffold for a chord
  ga tab sources                List free/open tab data sources
  ga tab fetch <query>          Search Archive.org + GitHub for free tabs
  ga tab url <url>              Fetch raw tab text from a direct URL"""

let private cmdAsk question =
    invoke "agent.theoryAgent" [ "question", box question ] |> ok ""

// ── entry point ───────────────────────────────────────────────────────────────

[<EntryPoint>]
let main argv =
    Console.OutputEncoding <- System.Text.Encoding.UTF8
    match argv |> Array.toList with
    | [] | ["help"] | ["--help"] | ["-h"] ->
        usage (); 0

    | "chord" :: rest ->
        let sym = match rest with s :: _ -> s | [] -> fromStdin ()
        if sym = "" then eprintfn "Usage: ga chord <symbol>"; 1 else cmdChord sym; 0

    | "intervals" :: rest ->
        let sym = match rest with s :: _ -> s | [] -> fromStdin ()
        if sym = "" then eprintfn "Usage: ga intervals <symbol>"; 1 else cmdIntervals sym; 0

    | "transpose" :: sym :: n :: _ ->
        match Int32.TryParse n with
        | true, semitones -> cmdTranspose sym semitones; 0
        | false, _        -> eprintfn "semitones must be an integer"; 1

    // echo "Am7" | ga transpose 7
    | "transpose" :: [n] when Console.IsInputRedirected ->
        let sym = fromStdin ()
        match Int32.TryParse n with
        | true, semitones -> cmdTranspose sym semitones; 0
        | false, _        -> eprintfn "semitones must be an integer"; 1

    | "transpose" :: _ ->
        eprintfn "Usage: ga transpose <symbol> <semitones>"; 1

    | "diatonic" :: root :: scale :: _ ->
        cmdDiatonic root scale; 0

    | "diatonic" :: [scale] when Console.IsInputRedirected ->
        cmdDiatonic (fromStdin ()) scale; 0

    | "diatonic" :: root :: _ ->
        cmdDiatonic root "major"; 0

    | "diatonic" :: _ when Console.IsInputRedirected ->
        cmdDiatonic (fromStdin ()) "major"; 0

    | "relative" :: root :: scale :: _ ->
        cmdRelative root scale; 0

    | "relative" :: [scale] when Console.IsInputRedirected ->
        cmdRelative (fromStdin ()) scale; 0

    | "relative" :: root :: _ ->
        cmdRelative root "major"; 0

    | "relative" :: _ when Console.IsInputRedirected ->
        cmdRelative (fromStdin ()) "major"; 0

    | "analyze" :: rest ->
        if rest.IsEmpty then eprintfn "Usage: ga analyze <chord1> <chord2> ..."; 1
        else cmdAnalyze rest; 0

    | "query" :: rest ->
        cmdQuery rest; 0

    | "project" :: symbol :: fields ->
        if fields.IsEmpty then eprintfn "Usage: ga project <symbol> <field1> [field2 ...]"; 1
        else cmdProject symbol fields; 0

    | "join" :: c1 :: c2 :: _ ->
        cmdJoin c1 c2; 0

    | "join" :: _ ->
        eprintfn "Usage: ga join <chord1> <chord2>"; 1

    | "pipe" :: rest ->
        cmdPipe rest; 0

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

    | "tab" :: rest ->
        cmdTab rest; 0

    | "ask" :: rest ->
        cmdAsk (String.concat " " rest); 0

    | "skill" :: rest ->
        cmdSkill rest; 0

    | cmd :: _ ->
        eprintfn "Unknown command: %s" cmd; usage (); 1
