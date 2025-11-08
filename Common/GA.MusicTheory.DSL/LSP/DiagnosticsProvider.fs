namespace GA.MusicTheory.DSL.LSP

open System
open GA.MusicTheory.DSL.Types.GrammarTypes
open GA.MusicTheory.DSL.Parsers
open GA.MusicTheory.DSL.LSP.LspTypes

/// <summary>
/// Diagnostics provider for Music Theory DSL
/// Provides syntax validation and error reporting
/// </summary>
module DiagnosticsProvider =

    // ============================================================================
    // DIAGNOSTIC CREATION
    // ============================================================================

    /// Create a diagnostic from a parse error
    let createDiagnostic (error: string) (line: int) (character: int) (severity: DiagnosticSeverity) : Diagnostic =
        { Range = { Start = { Line = line; Character = character };
                   End = { Line = line; Character = character + 10 } }
          Severity = severity
          Message = error
          Source = Some "music-theory-dsl" }

    /// Create an error diagnostic
    let createError (error: string) (line: int) (character: int) : Diagnostic =
        createDiagnostic error line character DiagnosticSeverity.Error

    /// Create a warning diagnostic
    let createWarning (warning: string) (line: int) (character: int) : Diagnostic =
        createDiagnostic warning line character DiagnosticSeverity.Warning

    /// Create an info diagnostic
    let createInfo (info: string) (line: int) (character: int) : Diagnostic =
        createDiagnostic info line character DiagnosticSeverity.Information

    // ============================================================================
    // VALIDATION FUNCTIONS
    // ============================================================================

    /// Validate chord progression syntax
    let validateChordProgression (text: string) : Diagnostic list =
        match ChordProgressionParser.parse text with
        | Ok _ -> []
        | Error error -> [createError error 0 0]

    /// Validate fretboard navigation syntax
    let validateFretboardNavigation (text: string) : Diagnostic list =
        match FretboardNavigationParser.parse text with
        | Ok _ -> []
        | Error error -> [createError error 0 0]

    /// Validate scale transformation syntax
    let validateScaleTransformation (text: string) : Diagnostic list =
        match ScaleTransformationParser.parse text with
        | Ok _ -> []
        | Error error -> [createError error 0 0]

    /// Validate Grothendieck operations syntax
    let validateGrothendieckOperations (text: string) : Diagnostic list =
        match GrothendieckOperationsParser.parse text with
        | Ok _ -> []
        | Error error -> [createError error 0 0]

    // ============================================================================
    // SEMANTIC VALIDATION
    // ============================================================================

    /// Check for common mistakes in chord progressions
    let checkChordProgressionSemantics (text: string) : Diagnostic list =
        let warnings = ResizeArray<Diagnostic>()

        // Check for consecutive identical chords
        if System.Text.RegularExpressions.Regex.IsMatch(text, @"(\b\w+\b)-\1") then
            warnings.Add(createWarning "Consecutive identical chords detected" 0 0)

        // Check for very long progressions (>16 chords)
        let chordCount = text.Split([| '-'; ',' |], StringSplitOptions.RemoveEmptyEntries).Length
        if chordCount > 16 then
            warnings.Add(createWarning $"Very long progression (%d{chordCount} chords)" 0 0)

        List.ofSeq warnings

    /// Check for common mistakes in scale transformations
    let checkScaleTransformationSemantics (text: string) : Diagnostic list =
        let warnings = ResizeArray<Diagnostic>()

        // Check for excessive transposition
        if System.Text.RegularExpressions.Regex.IsMatch(text, @"transpose\s+(\d{2,})") then
            warnings.Add(createWarning "Large transposition value (>12 semitones)" 0 0)

        // Check for excessive rotation
        if System.Text.RegularExpressions.Regex.IsMatch(text, @"rotate\s+([8-9]|\d{2,})") then
            warnings.Add(createWarning "Large rotation value (>7 steps)" 0 0)

        List.ofSeq warnings

    // ============================================================================
    // MAIN VALIDATION
    // ============================================================================

    /// Validate text and return diagnostics
    let validate (text: string) : Diagnostic list =
        let diagnostics = ResizeArray<Diagnostic>()

        // Try each parser and collect diagnostics
        let chordProgressionDiags = validateChordProgression text
        let navigationDiags = validateFretboardNavigation text
        let scaleTransformDiags = validateScaleTransformation text
        let grothendieckDiags = validateGrothendieckOperations text

        // If all parsers fail, report the most relevant error
        if not (List.isEmpty chordProgressionDiags) &&
           not (List.isEmpty navigationDiags) &&
           not (List.isEmpty scaleTransformDiags) &&
           not (List.isEmpty grothendieckDiags) then
            // All parsers failed - report the first error
            diagnostics.AddRange(chordProgressionDiags)
        else
            // At least one parser succeeded - add semantic warnings
            if List.isEmpty chordProgressionDiags then
                diagnostics.AddRange(checkChordProgressionSemantics text)
            if List.isEmpty scaleTransformDiags then
                diagnostics.AddRange(checkScaleTransformationSemantics text)

        List.ofSeq diagnostics

    // ============================================================================
    // QUICK FIXES
    // ============================================================================

    /// Suggest quick fixes for common errors
    let suggestQuickFixes (diagnostic: Diagnostic) : string list =
        let message = diagnostic.Message.ToLowerInvariant()

        if message.Contains("unexpected") || message.Contains("expected") then
            [ "Check syntax against grammar documentation"
              "Verify chord quality spelling (maj7, min7, etc.)"
              "Ensure proper use of separators (-, |, etc.)" ]
        else if message.Contains("invalid") then
            [ "Check note names (A-G with optional # or b)"
              "Verify roman numerals (I-VII or i-vii)"
              "Check fret/string numbers are in valid range" ]
        else
            [ "Consult DSL documentation"
              "Try a simpler expression first"
              "Check for typos" ]

