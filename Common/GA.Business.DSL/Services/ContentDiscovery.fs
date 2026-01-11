namespace GA.MusicTheory.DSL.Services

open System
open System.Threading.Tasks
open GA.MusicTheory.DSL.Types.PracticeRoutineTypes

/// <summary>
/// Content discovery service for finding musical content across various repositories
/// Provides unified search interface for tabs, MIDI, and sheet music
/// </summary>
module ContentDiscovery =

    // ============================================================================
    // CONTENT METADATA
    // ============================================================================

    /// Metadata for discovered content
    type ContentMetadata = {
        Id: string
        Title: string
        Artist: string option
        Repository: RepositoryName
        Url: string
        ContentType: string
        Difficulty: DifficultyLevel option
        Rating: float option
        Downloads: int option
        License: LicenseType option
        Tags: string list
        PreviewUrl: string option
    }

    /// Search result with relevance scoring
    type SearchResult = {
        Content: ContentMetadata
        RelevanceScore: float
        MatchedCriteria: string list
    }

    // ============================================================================
    // REPOSITORY-SPECIFIC DISCOVERY
    // ============================================================================

    /// Discover content from Ultimate Guitar
    let discoverFromUltimateGuitar (criteria: SearchCriteria) : Task<Result<SearchResult list, string>> =
        task {
            try
                // Simulate Ultimate Guitar API search
                let mockResults = [
                    {
                        Content = {
                            Id = "ug_wonderwall_123"
                            Title = "Wonderwall"
                            Artist = Some "Oasis"
                            Repository = UltimateGuitar
                            Url = "https://tabs.ultimate-guitar.com/tab/oasis/wonderwall-chords-123"
                            ContentType = "chord_tab"
                            Difficulty = Some Easy
                            Rating = Some 4.8
                            Downloads = Some 150000
                            License = Some Free
                            Tags = ["rock"; "90s"; "acoustic"; "beginner"]
                            PreviewUrl = Some "https://tabs.ultimate-guitar.com/preview/123"
                        }
                        RelevanceScore = 0.95
                        MatchedCriteria = ["artist"; "title"]
                    }
                    {
                        Content = {
                            Id = "ug_blackbird_456"
                            Title = "Blackbird"
                            Artist = Some "The Beatles"
                            Repository = UltimateGuitar
                            Url = "https://tabs.ultimate-guitar.com/tab/beatles/blackbird-tabs-456"
                            ContentType = "guitar_tab"
                            Difficulty = Some Medium
                            Rating = Some 4.9
                            Downloads = Some 200000
                            License = Some Free
                            Tags = ["classic rock"; "fingerpicking"; "acoustic"]
                            PreviewUrl = Some "https://tabs.ultimate-guitar.com/preview/456"
                        }
                        RelevanceScore = 0.85
                        MatchedCriteria = ["genre"; "difficulty"]
                    }
                ]

                // Filter based on criteria
                let filteredResults = 
                    mockResults
                    |> List.filter (fun result ->
                        match criteria.Artist with
                        | Some artist -> 
                            result.Content.Artist 
                            |> Option.exists (fun a -> a.Contains(artist, StringComparison.OrdinalIgnoreCase))
                        | None -> true)
                    |> List.filter (fun result ->
                        match criteria.Difficulty with
                        | Some diff -> result.Content.Difficulty = Some diff
                        | None -> true)

                return Ok filteredResults
            with
            | ex -> return Error $"Ultimate Guitar discovery failed: {ex.Message}"
        }

    /// Discover content from Songsterr
    let discoverFromSongsterr (criteria: SearchCriteria) : Task<Result<SearchResult list, string>> =
        task {
            try
                let mockResults = [
                    {
                        Content = {
                            Id = "songsterr_master_789"
                            Title = "Master of Puppets"
                            Artist = Some "Metallica"
                            Repository = Songsterr
                            Url = "https://www.songsterr.com/a/wsa/metallica-master-of-puppets-tab-s789"
                            ContentType = "guitar_pro"
                            Difficulty = Some VeryHard
                            Rating = Some 4.7
                            Downloads = Some 75000
                            License = Some Free
                            Tags = ["metal"; "thrash"; "advanced"; "electric"]
                            PreviewUrl = Some "https://www.songsterr.com/preview/789"
                        }
                        RelevanceScore = 0.92
                        MatchedCriteria = ["artist"; "genre"]
                    }
                ]

                return Ok mockResults
            with
            | ex -> return Error $"Songsterr discovery failed: {ex.Message}"
        }

    /// Discover content from MuseScore
    let discoverFromMuseScore (criteria: SearchCriteria) : Task<Result<SearchResult list, string>> =
        task {
            try
                let mockResults = [
                    {
                        Content = {
                            Id = "musescore_canon_101"
                            Title = "Canon in D"
                            Artist = Some "Johann Pachelbel"
                            Repository = MuseScore
                            Url = "https://musescore.com/score/canon-in-d-guitar-101"
                            ContentType = "musicxml"
                            Difficulty = Some Medium
                            Rating = Some 4.6
                            Downloads = Some 50000
                            License = Some PublicDomain
                            Tags = ["classical"; "baroque"; "wedding"; "fingerstyle"]
                            PreviewUrl = Some "https://musescore.com/preview/101"
                        }
                        RelevanceScore = 0.88
                        MatchedCriteria = ["genre"; "license"]
                    }
                ]

                return Ok mockResults
            with
            | ex -> return Error $"MuseScore discovery failed: {ex.Message}"
        }

    /// Discover content from IMSLP
    let discoverFromIMSLP (criteria: SearchCriteria) : Task<Result<SearchResult list, string>> =
        task {
            try
                let mockResults = [
                    {
                        Content = {
                            Id = "imslp_bach_bwv999"
                            Title = "Prelude in C minor, BWV 999"
                            Artist = Some "Johann Sebastian Bach"
                            Repository = IMSLP
                            Url = "https://imslp.org/wiki/Prelude_in_C_minor,_BWV_999_(Bach,_Johann_Sebastian)"
                            ContentType = "pdf_score"
                            Difficulty = Some Hard
                            Rating = Some 4.9
                            Downloads = Some 25000
                            License = Some PublicDomain
                            Tags = ["classical"; "baroque"; "lute"; "guitar_arrangement"]
                            PreviewUrl = Some "https://imslp.org/preview/bwv999"
                        }
                        RelevanceScore = 0.94
                        MatchedCriteria = ["artist"; "genre"; "license"]
                    }
                ]

                return Ok mockResults
            with
            | ex -> return Error $"IMSLP discovery failed: {ex.Message}"
        }

    // ============================================================================
    // UNIFIED DISCOVERY INTERFACE
    // ============================================================================

    /// Discover content from a specific repository
    let discoverFromRepository (repository: RepositoryName) (criteria: SearchCriteria) : Task<Result<SearchResult list, string>> =
        match repository with
        | UltimateGuitar -> discoverFromUltimateGuitar criteria
        | Songsterr -> discoverFromSongsterr criteria
        | MuseScore -> discoverFromMuseScore criteria
        | IMSLP -> discoverFromIMSLP criteria
        | _ -> Task.FromResult(Error $"Repository {repository} not yet implemented for discovery")

    /// Discover content across all repositories
    let discoverFromAllRepositories (criteria: SearchCriteria) : Task<Result<SearchResult list, string>> =
        task {
            let repositories = [UltimateGuitar; Songsterr; MuseScore; IMSLP]
            let! results = 
                repositories
                |> List.map (fun repo -> discoverFromRepository repo criteria)
                |> Task.WhenAll

            let allResults =
                results
                |> Array.choose (function Ok r -> Some r | Error _ -> None)
                |> Array.toList
                |> List.concat
                |> List.sortByDescending (fun r -> r.RelevanceScore)

            return Ok allResults
        }

    /// Smart content recommendation based on user preferences
    let recommendContent (userPreferences: SearchCriteria) (limit: int) : Task<Result<SearchResult list, string>> =
        task {
            let! allResults = discoverFromAllRepositories userPreferences
            
            match allResults with
            | Ok results ->
                let topResults = 
                    results
                    |> List.take (min limit results.Length)
                    |> List.map (fun r -> 
                        // Boost relevance for educational content
                        let boostedScore = 
                            if r.Content.License = Some PublicDomain || r.Content.License = Some EducationalUse then
                                r.RelevanceScore * 1.1
                            else
                                r.RelevanceScore
                        { r with RelevanceScore = boostedScore })
                    |> List.sortByDescending (fun r -> r.RelevanceScore)

                return Ok topResults
            | Error err -> return Error err
        }
