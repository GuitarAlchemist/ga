namespace GA.MusicTheory.DSL.Services

open System
open System.IO
open System.Text.RegularExpressions
open GA.MusicTheory.DSL.Types.PracticeRoutineTypes

/// <summary>
/// Content validation and safety service for internet-sourced musical content
/// Ensures content is safe, appropriate, and properly formatted
/// </summary>
module ContentValidator =

    // ============================================================================
    // SAFETY VALIDATION
    // ============================================================================

    /// List of trusted domains for content loading
    let private trustedDomains = [
        "ultimate-guitar.com"
        "songsterr.com"
        "musescore.com"
        "imslp.org"
        "freemidi.org"
        "github.com"
        "archive.org"
        "wikipedia.org"
        "wikimedia.org"
    ]

    /// Check if a URL is from a trusted domain
    let isTrustedDomain (url: string) =
        try
            let uri = Uri(url)
            let host = uri.Host.ToLower()
            trustedDomains |> List.exists (fun domain ->
                host = domain || host.EndsWith($".{domain}"))
        with
        | _ -> false

    /// Validate URL safety
    let validateUrlSafety (url: string) =
        if isTrustedDomain url then
            Ok url
        else
            Error $"URL not from trusted domain: {url}"

    // ============================================================================
    // CONTENT FORMAT VALIDATION
    // ============================================================================

    /// Validate ASCII tablature format
    let validateAsciiTab (content: string) =
        let lines = content.Split([|'\n'; '\r'|], StringSplitOptions.RemoveEmptyEntries)

        // Look for typical tab patterns
        let hasTabLines = lines |> Array.exists (fun line ->
            Regex.IsMatch(line, @"^[eEbBgGdDaA]\|.*\|") ||  // Standard tab notation
            Regex.IsMatch(line, @"^[1-6]\|.*\|") ||         // Numbered strings
            Regex.IsMatch(line, @".*[-0-9]+.*"))           // Fret numbers

        if hasTabLines then
            Ok content
        else
            Error "Content does not appear to be valid ASCII tablature"

    /// Validate MIDI file format
    let validateMidiFormat (bytes: byte[]) =
        if bytes.Length >= 4 &&
           bytes.[0] = 0x4Duy && bytes.[1] = 0x54uy &&
           bytes.[2] = 0x68uy && bytes.[3] = 0x64uy then
            Ok bytes
        else
            Error "Content does not appear to be a valid MIDI file"

    /// Validate Guitar Pro file format
    let validateGuitarProFormat (bytes: byte[]) =
        // Guitar Pro files typically start with "FICHIER GUITAR PRO" or version info
        let header = System.Text.Encoding.ASCII.GetString(bytes.[0..Math.Min(19, bytes.Length-1)])

        if header.Contains("GUITAR PRO") || header.Contains("FICHIER") then
            Ok bytes
        else
            Error "Content does not appear to be a valid Guitar Pro file"

    /// Validate MusicXML format
    let validateMusicXmlFormat (content: string) =
        if content.TrimStart().StartsWith("<?xml") &&
           (content.Contains("<score-partwise") || content.Contains("<score-timewise")) then
            Ok content
        else
            Error "Content does not appear to be valid MusicXML"

    // ============================================================================
    // CONTENT SIZE VALIDATION
    // ============================================================================

    /// Maximum allowed file sizes (in bytes)
    let private maxFileSizes = Map [
        "tab", 1024L * 1024L      // 1MB for text tabs
        "midi", 10L * 1024L * 1024L  // 10MB for MIDI
        "gp", 50L * 1024L * 1024L    // 50MB for Guitar Pro
        "xml", 5L * 1024L * 1024L    // 5MB for MusicXML
    ]

    /// Validate content size
    let validateContentSize (contentType: string) (size: int64) =
        let maxSize =
            maxFileSizes
            |> Map.tryFind contentType
            |> Option.defaultValue (1024L * 1024L)  // Default 1MB

        if size <= maxSize then
            Ok size
        else
            Error $"Content size ({size} bytes) exceeds maximum allowed ({maxSize} bytes) for {contentType}"

    // ============================================================================
    // LICENSE VALIDATION
    // ============================================================================

    /// Check if content has appropriate license for educational use
    let validateLicense (licenseType: LicenseType option) =
        match licenseType with
        | Some PublicDomain | Some CreativeCommons | Some Free | Some EducationalUse ->
            Ok "Content has appropriate license for educational use"
        | None ->
            Ok "No license specified - proceed with caution"

    // ============================================================================
    // COMPREHENSIVE VALIDATION
    // ============================================================================

    /// Validate content based on its type and source
    let validateContent (urlSpec: UrlSpec) (content: byte[]) =
        let contentSize = int64 content.Length

        match urlSpec with
        | TabUrl url ->
            validateUrlSafety url
            |> Result.bind (fun _ -> validateContentSize "tab" contentSize)
            |> Result.bind (fun _ ->
                let textContent = System.Text.Encoding.UTF8.GetString(content)
                validateAsciiTab textContent)
            |> Result.map (fun _ -> content)

        | MidiUrl url ->
            validateUrlSafety url
            |> Result.bind (fun _ -> validateContentSize "midi" contentSize)
            |> Result.bind (fun _ -> validateMidiFormat content)

        | GuitarProUrl url ->
            validateUrlSafety url
            |> Result.bind (fun _ -> validateContentSize "gp" contentSize)
            |> Result.bind (fun _ -> validateGuitarProFormat content)

        | MusicXmlUrl url ->
            validateUrlSafety url
            |> Result.bind (fun _ -> validateContentSize "xml" contentSize)
            |> Result.bind (fun _ ->
                let textContent = System.Text.Encoding.UTF8.GetString(content)
                validateMusicXmlFormat textContent)
            |> Result.map (fun _ -> content)

        | DirectUrl url ->
            validateUrlSafety url
            |> Result.bind (fun _ -> validateContentSize "general" contentSize)
            |> Result.map (fun _ -> content)

    /// Validate search criteria for safety
    let validateSearchCriteria (criteria: SearchCriteria) =
        // Check for potentially malicious search terms
        let suspiciousPatterns = [
            @"<script"
            @"javascript:"
            @"data:"
            @"file://"
            @"\.\./.*\.\."  // Path traversal
        ]

        let searchText =
            [criteria.Artist; criteria.Title; criteria.Genre]
            |> List.choose id
            |> String.concat " "

        let hasSuspiciousContent =
            suspiciousPatterns
            |> List.exists (fun pattern ->
                Regex.IsMatch(searchText, pattern, RegexOptions.IgnoreCase))

        if hasSuspiciousContent then
            Error "Search criteria contains potentially unsafe content"
        else
            Ok criteria
