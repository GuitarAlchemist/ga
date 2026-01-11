namespace GA.MusicTheory.DSL.Generators

open System
open System.Text
open GA.MusicTheory.DSL.Types.GrammarTypes

/// <summary>
/// Generator for Fretboard Navigation DSL from AST types
/// Converts navigation command AST back to text format
/// </summary>
module FretboardNavigationGenerator =

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

    /// Format a chord
    let formatChord (chord: Chord) =
        let root = formatNote chord.Root
        let quality =
            match chord.Quality with
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
        if quality = "" then root else $"%s{root}%s{quality}"

    /// Format a fretboard position
    let formatPosition (pos: FretboardPosition) =
        $"%d{pos.String}:%d{pos.Fret}"

    /// Format a CAGED shape
    let formatCAGEDShape (shape: CAGEDShape) (fret: int) =
        let shapeName =
            match shape with
            | C_Shape -> "C"
            | A_Shape -> "A"
            | G_Shape -> "G"
            | E_Shape -> "E"
            | D_Shape -> "D"

        $"CAGED shape %s{shapeName} at fret %d{fret}"

    /// Format a direction
    let formatDirection (dir: Direction) =
        match dir with
        | Up -> "up"
        | Down -> "down"
        | Left -> "left"
        | Right -> "right"

    // ============================================================================
    // NAVIGATION COMMAND FORMATTERS
    // ============================================================================

    /// Generate navigation command DSL from AST
    let rec generate (command: NavigationCommand) : string =
        match command with
        | GotoPosition pos ->
            formatPosition pos

        | GotoShape (shape, fret) ->
            formatCAGEDShape shape fret

        | Move (direction, distance) ->
            $"move %s{formatDirection direction} %d{distance}"

        | Slide (from, ``to``) ->
            $"slide from %s{formatPosition from} to %s{formatPosition ``to``}"

        | FindNote (note, _) ->
            $"find note %s{formatNote note}"

        | FindChord (chord, _) ->
            $"find chord %s{formatChord chord}"

        | NavigatePath (from, ``to``) ->
            $"navigate from %s{formatPosition from} to %s{formatPosition ``to``}"


    // ============================================================================
    // PUBLIC API
    // ============================================================================

    // Round-trip test would require importing the parser module
    // For now, just provide the generate function

