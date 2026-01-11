namespace GA.MusicTheory.DSL.Services

open System
open System.IO
open System.Net.Http
open System.Threading.Tasks
open GA.MusicTheory.DSL.Types.PracticeRoutineTypes

/// <summary>
/// Service for loading musical content from internet sources
/// Supports tablatures, MIDI files, and various music repositories
/// </summary>
module WebContentLoader =

    // ============================================================================
    // HTTP CLIENT MANAGEMENT
    // ============================================================================

    /// Shared HTTP client for web requests
    let private httpClient = new HttpClient()

    /// Set user agent for responsible web scraping
    do httpClient.DefaultRequestHeaders.Add("User-Agent", 
        "GuitarAlchemist/1.0 (Educational Music Software)")

    // ============================================================================
    // CONTENT VALIDATION
    // ============================================================================

    /// Validate URL format and safety
    let validateUrl (url: string) =
        try
            let uri = Uri(url)
            match uri.Scheme.ToLower() with
            | "http" | "https" -> Ok uri
            | _ -> Error "Only HTTP and HTTPS URLs are supported"
        with
        | ex -> Error $"Invalid URL format: {ex.Message}"

    /// Check if content type is supported
    let isSupportedContentType (contentType: string) =
        let supportedTypes = [
            "text/plain"           // ASCII tabs
            "application/octet-stream"  // Guitar Pro files
            "audio/midi"           // MIDI files
            "application/xml"      // MusicXML
            "text/xml"            // MusicXML
            "application/json"     // JSON-based formats
        ]
        supportedTypes |> List.exists (fun t -> contentType.Contains(t, StringComparison.OrdinalIgnoreCase))

    // ============================================================================
    // DIRECT URL LOADING
    // ============================================================================

    /// Load content from a direct URL
    let loadFromUrl (urlSpec: UrlSpec) : Task<Result<byte[], string>> =
        task {
            let url = 
                match urlSpec with
                | DirectUrl u | TabUrl u | MidiUrl u | GuitarProUrl u | MusicXmlUrl u -> u

            match validateUrl url with
            | Error err -> return Error err
            | Ok uri ->
                try
                    let! response = httpClient.GetAsync(uri)
                    
                    if response.IsSuccessStatusCode then
                        let contentType =
                            match response.Content.Headers.ContentType with
                            | null -> ""
                            | ct when ct.MediaType <> null -> ct.MediaType
                            | _ -> ""
                        
                        if isSupportedContentType contentType then
                            let! content = response.Content.ReadAsByteArrayAsync()
                            return Ok content
                        else
                            return Error $"Unsupported content type: {contentType}"
                    else
                        return Error $"HTTP {response.StatusCode}: {response.ReasonPhrase}"
                        
                with
                | ex -> return Error $"Network error: {ex.Message}"
        }

    // ============================================================================
    // REPOSITORY-SPECIFIC LOADERS
    // ============================================================================

    /// Load content from Ultimate Guitar
    let loadFromUltimateGuitar (searchCriteria: SearchCriteria option) : Task<Result<string, string>> =
        task {
            // Note: This is a simplified implementation
            // In production, would use Ultimate Guitar's API or web scraping
            let searchQuery = 
                match searchCriteria with
                | Some criteria ->
                    let parts = [
                        criteria.Artist |> Option.map (fun a -> $"artist:{a}")
                        criteria.Title |> Option.map (fun t -> $"title:{t}")
                        criteria.Difficulty |> Option.map (fun d -> $"difficulty:{d}")
                    ]
                    parts |> List.choose id |> String.concat " "
                | None -> "popular tabs"

            // Simulate API response
            return Ok $"Ultimate Guitar search results for: {searchQuery}"
        }

    /// Load content from Songsterr
    let loadFromSongsterr (searchCriteria: SearchCriteria option) : Task<Result<string, string>> =
        task {
            let searchQuery = 
                match searchCriteria with
                | Some criteria ->
                    [criteria.Artist; criteria.Title] 
                    |> List.choose id 
                    |> String.concat " "
                | None -> "guitar tabs"

            // Simulate Songsterr API
            return Ok $"Songsterr tabs for: {searchQuery}"
        }

    /// Load content from MuseScore
    let loadFromMuseScore (searchCriteria: SearchCriteria option) : Task<Result<string, string>> =
        task {
            let searchQuery = 
                match searchCriteria with
                | Some criteria ->
                    [criteria.Title; criteria.Genre] 
                    |> List.choose id 
                    |> String.concat " "
                | None -> "sheet music"

            return Ok $"MuseScore content for: {searchQuery}"
        }

    /// Load content from IMSLP (International Music Score Library Project)
    let loadFromIMSLP (searchCriteria: SearchCriteria option) : Task<Result<string, string>> =
        task {
            let searchQuery = 
                match searchCriteria with
                | Some criteria ->
                    [criteria.Title; criteria.Artist] 
                    |> List.choose id 
                    |> String.concat " "
                | None -> "classical music"

            return Ok $"IMSLP public domain scores for: {searchQuery}"
        }

    /// Load content from FreeMidi
    let loadFromFreeMidi (searchCriteria: SearchCriteria option) : Task<Result<string, string>> =
        task {
            let searchQuery = 
                match searchCriteria with
                | Some criteria ->
                    [criteria.Title; criteria.Genre] 
                    |> List.choose id 
                    |> String.concat " "
                | None -> "midi files"

            return Ok $"FreeMidi files for: {searchQuery}"
        }

    // ============================================================================
    // MAIN CONTENT LOADING INTERFACE
    // ============================================================================

    /// Load content from any supported source
    let loadContent (source: ContentSource) : Task<Result<string, string>> =
        task {
            match source with
            | UrlSource urlSpec ->
                let! result = loadFromUrl urlSpec
                match result with
                | Ok bytes -> 
                    let content = System.Text.Encoding.UTF8.GetString(bytes)
                    return Ok content
                | Error err -> return Error err

            | RepositorySource (repo, criteria) ->
                match repo with
                | UltimateGuitar -> return! loadFromUltimateGuitar criteria
                | Songsterr -> return! loadFromSongsterr criteria
                | MuseScore -> return! loadFromMuseScore criteria
                | IMSLP -> return! loadFromIMSLP criteria
                | FreeMidi -> return! loadFromFreeMidi criteria
                | _ -> return Error $"Repository {repo} not yet implemented"

            | SearchSource (criteria, repoOpt) ->
                let repo = repoOpt |> Option.defaultValue UltimateGuitar
                match repo with
                | UltimateGuitar -> return! loadFromUltimateGuitar (Some criteria)
                | Songsterr -> return! loadFromSongsterr (Some criteria)
                | MuseScore -> return! loadFromMuseScore (Some criteria)
                | IMSLP -> return! loadFromIMSLP (Some criteria)
                | FreeMidi -> return! loadFromFreeMidi (Some criteria)
                | _ -> return Error $"Repository {repo} not yet implemented"
        }
