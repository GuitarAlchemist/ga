module GA.Business.DSL.Closures.BuiltinClosures.TabClosures

open System
open System.Net.Http
open System.Text
open GA.Business.DSL.Closures.GaAsync
open GA.Business.DSL.Closures.GaClosureRegistry
open GA.Business.DSL.Parsers
open GA.Business.DSL.Generators

// ── Shared HTTP client ────────────────────────────────────────────────────────

let private http =
    let h = new HttpClient()
    h.DefaultRequestHeaders.Add("User-Agent", "GuitarAlchemist/1.0 (Educational; +https://github.com/guitar-alchemist)")
    h.Timeout <- TimeSpan.FromSeconds 15.0
    h

let private get (url: string) =
    async {
        try
            let! resp = http.GetStringAsync(url) |> Async.AwaitTask
            return Ok resp
        with ex ->
            return Error (GaError.AgentError ("http", ex.Message))
    }

// ── tab.parseAscii ────────────────────────────────────────────────────────────

/// Parse ASCII tab text and return a JSON summary.
let parseAscii : GaClosure =
    { Name        = "tab.parseAscii"
      Category    = GaClosureCategory.Domain
      Description = "Parse ASCII tab notation and return a structured summary."
      Tags        = [ "tab"; "ascii"; "parse" ]
      InputSchema = Map.ofList [ "text", "string — ASCII tab text" ]
      OutputType  = "string (JSON summary)"
      Exec        = fun inputs ->
          async {
              match inputs.TryFind "text" with
              | None -> return Error (GaError.DomainError "Missing 'text' input")
              | Some t ->
                  match AsciiTabParser.parse (t :?> string) with
                  | Result.Error err -> return Error (GaError.ParseError ("ascii-tab", err))
                  | Result.Ok doc ->
                      let measures = doc.Measures.Length
                      let strings  = doc.Measures |> List.tryHead |> Option.map (fun m -> m.Staff.StringCount) |> Option.defaultValue 0
                      let title    = doc.Header |> Option.bind (fun h -> h.Title)   |> Option.defaultValue "unknown"
                      let artist   = doc.Header |> Option.bind (fun h -> h.Artist)  |> Option.defaultValue "unknown"
                      let json     =
                          sprintf """{"title":"%s","artist":"%s","measures":%d,"strings":%d}"""
                              title artist measures strings
                      return Ok (box json)
          } }

// ── tab.parseVexTab ───────────────────────────────────────────────────────────

/// Parse VexTab text, validate it, and re-emit canonical VexTab.
let parseVexTab : GaClosure =
    { Name        = "tab.parseVexTab"
      Category    = GaClosureCategory.Domain
      Description = "Parse VexTab notation, validate it, and re-emit canonical VexTab."
      Tags        = [ "tab"; "vextab"; "parse"; "validate" ]
      InputSchema = Map.ofList [ "text", "string — VexTab source" ]
      OutputType  = "string (canonical VexTab)"
      Exec        = fun inputs ->
          async {
              match inputs.TryFind "text" with
              | None -> return Error (GaError.DomainError "Missing 'text' input")
              | Some t ->
                  match VexTabParser.parse (t :?> string) with
                  | Result.Error err -> return Error (GaError.ParseError ("vextab", err))
                  | Result.Ok doc    -> return Ok (box (VexTabGenerator.generate doc))
          } }

// ── tab.generateChord ─────────────────────────────────────────────────────────

/// Generate a minimal VexTab stave showing a named chord.
/// Produces a one-bar tabstave with the chord symbol as a text annotation
/// and a whole-note chord marker — a useful scaffold for rendering with VexFlow.
let generateChord : GaClosure =
    { Name        = "tab.generateChord"
      Category    = GaClosureCategory.Domain
      Description = "Generate VexTab notation scaffold for a chord symbol."
      Tags        = [ "tab"; "vextab"; "chord"; "generate" ]
      InputSchema = Map.ofList [ "symbol", "string — chord symbol (e.g. Am7, Cmaj9)" ]
      OutputType  = "string (VexTab source)"
      Exec        = fun inputs ->
          async {
              match inputs.TryFind "symbol" with
              | None -> return Error (GaError.DomainError "Missing 'symbol' input")
              | Some sym ->
                  let s = (sym :?> string).Trim()
                  // Generate a minimal valid VexTab stave
                  let vextab =
                      String.concat "\n"
                          [ "options scale=1.0 space=20"
                            "tabstave notation=true"
                            sprintf "notes :w ## | =|="
                            sprintf "text :w %s" s ]
                  return Ok (box vextab)
          } }

// ── tab.sources ───────────────────────────────────────────────────────────────

/// Locate TabSources.yaml relative to this assembly / CWD.
let private findTabSourcesYaml () =
    let name = "TabSources.yaml"
    [ IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, name)
      IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "config", name)
      IO.Path.Combine(Environment.CurrentDirectory, name)
      IO.Path.Combine(Environment.CurrentDirectory, "Common", "GA.Business.Config", name) ]
    |> List.tryFind IO.File.Exists

/// Parse the minimal YAML structure we need (id, name, url, license, description).
/// Uses line-by-line parsing to avoid a YamlDotNet dependency here.
let private parseTabSources (yaml: string) =
    let lines = yaml.Split([|'\n'; '\r'|], StringSplitOptions.RemoveEmptyEntries)
    let getValue (prefix: string) (line: string) =
        if line.TrimStart().StartsWith(prefix) then
            Some (line.TrimStart().[prefix.Length..].Trim().Trim('"'))
        else None
    let mutable results = []
    let mutable cur = Map.empty<string,string>
    for line in lines do
        if line.TrimStart().StartsWith("- id:") then
            if cur.ContainsKey "name" then results <- cur :: results
            cur <- Map.ofList [ "id", line.TrimStart().[5..].Trim().Trim('"') ]
        else
            [ "name"; "url"; "license"; "description" ]
            |> List.iter (fun key ->
                match getValue (key + ":") line with
                | Some v -> cur <- cur |> Map.add key v
                | None   -> ())
    if cur.ContainsKey "name" then results <- cur :: results
    List.rev results

/// List the free tab sources configured in TabSources.yaml.
let tabSources : GaClosure =
    { Name        = "tab.sources"
      Category    = GaClosureCategory.Domain
      Description = "List configured free/open tab data sources."
      Tags        = [ "tab"; "sources"; "datasets" ]
      InputSchema = Map.ofList []
      OutputType  = "string (formatted source list)"
      Exec        = fun _ ->
          async {
              let sources =
                  match findTabSourcesYaml () with
                  | None -> []
                  | Some path ->
                      try parseTabSources (IO.File.ReadAllText path)
                      with _ -> []
              if sources.IsEmpty then
                  return Ok (box "(no tab sources found — check TabSources.yaml)")
              else
                  let g = fun (k: string) (m: Map<string,string>) -> m |> Map.tryFind k |> Option.defaultValue ""
                  let lines =
                      sources |> List.map (fun s ->
                          sprintf "%-28s  %-20s  %s\n    %s"
                              (g "name" s) (g "license" s) (g "description" s) (g "url" s))
                  return Ok (box (String.concat "\n\n" lines))
          } }

// ── tab.fetch ─────────────────────────────────────────────────────────────────

/// Search Archive.org and GitHub for free guitar tabs matching a query.
/// Archive.org: fully open, no auth.
/// GitHub: rate-limited but no auth required for basic search.
let tabFetch : GaClosure =
    { Name        = "tab.fetch"
      Category    = GaClosureCategory.Domain
      Description = "Search free tab sources (Archive.org, GitHub) for a song or artist."
      Tags        = [ "tab"; "fetch"; "search"; "internet"; "free" ]
      InputSchema = Map.ofList [ "query", "string — search terms (artist, song title, or both)" ]
      OutputType  = "string (search results with URLs)"
      Exec        = fun inputs ->
          async {
              match inputs.TryFind "query" with
              | None -> return Error (GaError.DomainError "Missing 'query' input")
              | Some q ->
                  let query = q :?> string
                  let encoded = Uri.EscapeDataString query

                  // ── Archive.org full-text search ──────────────────────────
                  // Uses the public Solr API; no mediatype filter (causes 400).
                  // Best results come from searching guitar+tab books and song titles.
                  let archiveUrl =
                      sprintf "https://archive.org/advancedsearch.php?q=subject%%3Aguitar+tab+%%22%s%%22&fl=identifier,title&rows=6&output=json"
                          encoded

                  let! archiveResult = get archiveUrl
                  let archiveLines =
                      match archiveResult with
                      | Error _ -> []
                      | Ok json ->
                          // Extract from "docs":[{"identifier":"…","title":"…"},…]
                          let docsMatch = System.Text.RegularExpressions.Regex.Match(json, "\"docs\":\[([^\]]*)\]")
                          if not docsMatch.Success then []
                          else
                              let items =
                                  System.Text.RegularExpressions.Regex.Matches(
                                      docsMatch.Groups.[1].Value,
                                      "\"identifier\":\"([^\"]+)\"[^{]*?\"title\":\"([^\"]+)\"")
                              [ for m in items ->
                                  sprintf "  [archive.org]  %s\n    https://archive.org/details/%s"
                                      m.Groups.[2].Value m.Groups.[1].Value ]

                  // ── GitHub repository search ───────────────────────────────
                  // Repository search works without auth; code search now requires it.
                  let githubUrl =
                      sprintf "https://api.github.com/search/repositories?q=%s+guitar+tab&sort=stars&per_page=5"
                          encoded

                  let! githubResult = get githubUrl
                  let githubLines =
                      match githubResult with
                      | Error _ -> []
                      | Ok json when json.Contains("rate limit") || json.Contains("requires authentication") -> []
                      | Ok json ->
                          let items =
                              System.Text.RegularExpressions.Regex.Matches(
                                  json, "\"full_name\":\"([^\"]+)\".*?\"html_url\":\"([^\"]+)\"")
                          [ for m in items ->
                              sprintf "  [github repo]  %s\n    %s"
                                  m.Groups.[1].Value m.Groups.[2].Value ]

                  let allLines = archiveLines @ githubLines
                  if allLines.IsEmpty then
                      return Ok (box (sprintf "No results found for: %s" query))
                  else
                      let header = sprintf "Tab search: \"%s\"\n" query
                      return Ok (box (header + String.concat "\n\n" allLines))
          } }

// ── tab.fetchUrl ──────────────────────────────────────────────────────────────

/// Fetch the raw text content of a tab from a direct URL (ASCII, VexTab, or text).
let tabFetchUrl : GaClosure =
    { Name        = "tab.fetchUrl"
      Category    = GaClosureCategory.Domain
      Description = "Fetch raw tab content from a direct URL (must be text/plain)."
      Tags        = [ "tab"; "fetch"; "url"; "internet" ]
      InputSchema = Map.ofList [ "url", "string — direct URL to a tab text file" ]
      OutputType  = "string (raw tab text)"
      Exec        = fun inputs ->
          async {
              match inputs.TryFind "url" with
              | None -> return Error (GaError.DomainError "Missing 'url' input")
              | Some u ->
                  let! result = get (u :?> string)
                  match result with
                  | Error e  -> return Error e
                  | Ok text  ->
                      // Trim to reasonable size (first 4 KB)
                      let trimmed = if text.Length > 4096 then text.[..4095] + "\n...(truncated)" else text
                      return Ok (box trimmed)
          } }

// ── Registration ──────────────────────────────────────────────────────────────

let register () =
    GaClosureRegistry.Global.RegisterAll
        [ parseAscii
          parseVexTab
          generateChord
          tabSources
          tabFetch
          tabFetchUrl ]

do register ()
