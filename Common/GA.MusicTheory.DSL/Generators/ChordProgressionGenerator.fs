namespace GA.MusicTheory.DSL.Generators

open System
open System.Text
open GA.MusicTheory.DSL.Types.GrammarTypes

/// <summary>
/// Generator for Chord Progression DSL from AST types
/// Converts chord progression AST back to text format
/// </summary>
module ChordProgressionGenerator =

    // ============================================================================
    // PRIMITIVE FORMATTERS
    // ============================================================================

    /// Format a note
    let formatNote (note: Note) =
        let letter = string note.Letter
        let accidental =
            match note.Accidental with
            | Some Sharp -> "#"
            | Some Flat -> "b"
            | Some DoubleSharp -> "##"
            | Some DoubleFlat -> "bb"
            | Some Natural -> ""
            | None -> ""

        $"%s{letter}%s{accidental}"

    /// Format a chord quality
    let formatChordQuality (quality: ChordQuality) =
        match quality with
        | Major -> ""
        | Minor -> "m"
        | Diminished -> "dim"
        | Augmented -> "aug"
        | Dominant7 -> "7"
        | Major7 -> "maj7"
        | Minor7 -> "m7"
        | Diminished7 -> "dim7"
        | HalfDiminished7 -> "m7b5"
        | Augmented7 -> "aug7"
        | Sus2 -> "sus2"
        | Sus4 -> "sus4"
        | Add9 -> "add9"
        | Major9 -> "maj9"
        | Minor9 -> "m9"
        | Dominant9 -> "9"
        | ChordQuality.Custom s -> s

    /// Format a chord
    let formatChord (chord: Chord) =
        let root = formatNote chord.Root
        let quality = formatChordQuality chord.Quality
        if quality = "" then root else $"%s{root}%s{quality}"

    /// Format a Roman numeral degree
    let formatRomanNumeral (degree: int) (isMinor: bool) =
        let numeral =
            match degree with
            | 1 -> "I"
            | 2 -> "II"
            | 3 -> "III"
            | 4 -> "IV"
            | 5 -> "V"
            | 6 -> "VI"
            | 7 -> "VII"
            | _ -> $"%d{degree}"

        if isMinor then numeral.ToLower() else numeral

    // ============================================================================
    // PROGRESSION CHORD FORMATTERS
    // ============================================================================

    /// Format a progression chord
    let formatProgressionChord (chord: ProgressionChord) =
        match chord with
        | AbsoluteChord c -> formatChord c
        | RomanNumeralChord rn ->
            let isMinor =
                match rn.Quality with
                | MinorRoman | DiminishedRoman -> true
                | _ -> false
            let numeral = formatRomanNumeral rn.Degree isMinor
            numeral

    // ============================================================================
    // METADATA FORMATTERS
    // ============================================================================

    /// Format time signature
    let formatTimeSignature (ts: TimeSignature option) =
        match ts with
        | Some { Numerator = n; Denominator = d } -> $"%d{n}/%d{d}"
        | None -> ""

    /// Format key
    let formatKey (key: Note option) =
        match key with
        | Some note -> formatNote note
        | None -> ""

    /// Format tempo
    let formatTempo (tempo: int option) =
        match tempo with
        | Some t -> $"%d{t}"
        | None -> ""

    // ============================================================================
    // MAIN GENERATOR
    // ============================================================================

    /// Generate chord progression DSL from AST
    let generate (progression: ChordProgression) : string =
        let sb = StringBuilder()

        // Format chords
        let chords =
            progression.Chords
            |> List.map formatProgressionChord
            |> String.concat " "
        sb.Append(chords) |> ignore

        // Format metadata if present
        let hasMetadata =
            progression.Key.IsSome ||
            progression.TimeSignature.IsSome ||
            progression.Tempo.IsSome

        if hasMetadata then
            sb.Append(" |") |> ignore

            let mutable first = true

            if progression.Key.IsSome then
                if not first then sb.Append(",") |> ignore
                sb.Append $" key: %s{formatKey progression.Key}" |> ignore
                first <- false

            if progression.TimeSignature.IsSome then
                if not first then sb.Append(",") |> ignore
                sb.Append $" time: %s{formatTimeSignature progression.TimeSignature}" |> ignore
                first <- false

            if progression.Tempo.IsSome then
                if not first then sb.Append(",") |> ignore
                sb.Append $" tempo: %s{formatTempo progression.Tempo}" |> ignore

        sb.ToString()

    // ============================================================================
    // PUBLIC API
    // ============================================================================

    // Round-trip test would require importing the parser module
    // For now, just provide the generate function

