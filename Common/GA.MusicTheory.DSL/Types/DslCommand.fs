namespace GA.MusicTheory.DSL.Types

open System

/// <summary>
/// Helper functions for DSL commands
/// </summary>
module DslCommand =

    open GrammarTypes
    open PracticeRoutineTypes

    // ============================================================================
    // COMMAND CREATION
    // ============================================================================

    /// Create a chord progression command
    let chordProgression chords key timeSignature tempo =
        ChordProgressionCommand
            { Chords = chords
              Key = key
              TimeSignature = timeSignature
              Tempo = tempo
              Metadata = Map.empty }

    /// Create a navigation command
    let navigation cmd =
        NavigationCommand cmd

    /// Create a scale transformation command
    let scaleTransform scale transformations =
        ScaleTransformCommand (scale, transformations)

    /// Create a Grothendieck operation command
    let grothendieck operation =
        GrothendieckCommand operation

    /// Create a practice routine command
    let practiceRoutine routine =
        PracticeRoutineCommand routine

    /// Create a composite command
    let composite commands =
        CompositeCommand commands

    // ============================================================================
    // COMMAND INSPECTION
    // ============================================================================

    /// Get the type of a command as a string
    let getCommandType command =
        match command with
        | ChordProgressionCommand _ -> "ChordProgression"
        | NavigationCommand _ -> "Navigation"
        | ScaleTransformCommand _ -> "ScaleTransform"
        | GrothendieckCommand _ -> "Grothendieck"
        | PracticeRoutineCommand _ -> "PracticeRoutine"
        | CompositeCommand _ -> "Composite"

    /// Check if a command is a chord progression command
    let isChordProgression command =
        match command with
        | ChordProgressionCommand _ -> true
        | _ -> false

    /// Check if a command is a navigation command
    let isNavigation command =
        match command with
        | NavigationCommand _ -> true
        | _ -> false

    /// Check if a command is a scale transformation command
    let isScaleTransform command =
        match command with
        | ScaleTransformCommand _ -> true
        | _ -> false

    /// Check if a command is a Grothendieck operation command
    let isGrothendieck command =
        match command with
        | GrothendieckCommand _ -> true
        | _ -> false

    /// Check if a command is a composite command
    let isComposite command =
        match command with
        | CompositeCommand _ -> true
        | _ -> false

    // ============================================================================
    // COMMAND TRANSFORMATION
    // ============================================================================

    /// Map a function over all chord progression commands
    let rec mapChordProgressions f command =
        match command with
        | ChordProgressionCommand prog -> ChordProgressionCommand (f prog)
        | CompositeCommand commands ->
            CompositeCommand (List.map (mapChordProgressions f) commands)
        | other -> other

    /// Map a function over all navigation commands
    let rec mapNavigations f command =
        match command with
        | NavigationCommand nav -> NavigationCommand (f nav)
        | CompositeCommand commands ->
            CompositeCommand (List.map (mapNavigations f) commands)
        | other -> other

    /// Map a function over all scale transformation commands
    let rec mapScaleTransforms f command =
        match command with
        | ScaleTransformCommand (scale, transforms) ->
            let (newScale, newTransforms) = f (scale, transforms)
            ScaleTransformCommand (newScale, newTransforms)
        | CompositeCommand commands ->
            CompositeCommand (List.map (mapScaleTransforms f) commands)
        | other -> other

    /// Flatten a composite command into a list of simple commands
    let rec flatten command =
        match command with
        | CompositeCommand commands ->
            commands |> List.collect flatten
        | other -> [other]

    // ============================================================================
    // COMMAND VALIDATION
    // ============================================================================

    /// Validate a note
    let validateNote (note: Note) =
        let validLetters = ['A'; 'B'; 'C'; 'D'; 'E'; 'F'; 'G']
        if not (List.contains note.Letter validLetters) then
            Error $"Invalid note letter: %c{note.Letter}"
        else
            Ok note

    /// Validate a chord
    let validateChord (chord: Chord) =
        match validateNote chord.Root with
        | Error e -> Error e
        | Ok _ -> Ok chord

    /// Validate a scale
    let validateScale (scale: Scale) =
        match validateNote scale.Root with
        | Error e -> Error e
        | Ok _ ->
            if List.isEmpty scale.Intervals then
                Error "Scale must have at least one interval"
            else if List.exists (fun i -> i < 0 || i > 12) scale.Intervals then
                Error "Scale intervals must be between 0 and 12"
            else
                Ok scale

    /// Validate a fretboard position
    let validatePosition (pos: FretboardPosition) =
        if pos.String < 1 || pos.String > 12 then
            Error $"Invalid string number: %d{pos.String} (must be 1-12)"
        else if pos.Fret < 0 || pos.Fret > 24 then
            Error $"Invalid fret number: %d{pos.Fret} (must be 0-24)"
        else
            Ok pos

    /// Validate a command
    let rec validate command =
        match command with
        | ChordProgressionCommand prog ->
            let chordResults =
                prog.Chords
                |> List.map (function
                    | AbsoluteChord chord -> validateChord chord |> Result.map (fun _ -> ())
                    | RomanNumeralChord _ -> Ok ())

            match List.tryFind Result.isError chordResults with
            | Some (Error e) -> Error e
            | _ -> Ok command

        | NavigationCommand nav ->
            match nav with
            | GotoPosition pos -> validatePosition pos |> Result.map (fun _ -> command)
            | Slide (from, ``to``) ->
                match validatePosition from, validatePosition ``to`` with
                | Ok _, Ok _ -> Ok command
                | Error e, _ | _, Error e -> Error e
            | NavigatePath (from, ``to``) ->
                match validatePosition from, validatePosition ``to`` with
                | Ok _, Ok _ -> Ok command
                | Error e, _ | _, Error e -> Error e
            | _ -> Ok command

        | ScaleTransformCommand (scale, _) ->
            validateScale scale |> Result.map (fun _ -> command)

        | GrothendieckCommand _ ->
            Ok command  // TODO: Add validation for Grothendieck operations

        | PracticeRoutineCommand _ ->
            Ok command  // Practice routines are always valid for now

        | CompositeCommand commands ->
            let results = List.map validate commands
            match List.tryFind Result.isError results with
            | Some (Error e) -> Error e
            | _ -> Ok command

    // ============================================================================
    // COMMAND FORMATTING
    // ============================================================================

    /// Format a note as a string
    let formatNote (note: Note) =
        let accidentalStr =
            match note.Accidental with
            | Some Sharp -> "#"
            | Some Flat -> "b"
            | Some DoubleSharp -> "##"
            | Some DoubleFlat -> "bb"
            | Some Natural -> "♮"
            | None -> ""

        let octaveStr =
            match note.Octave with
            | Some o -> string o
            | None -> ""

        $"%c{note.Letter}%s{accidentalStr}%s{octaveStr}"

    /// Format a chord quality as a string
    let formatChordQuality (quality: ChordQuality) =
        match quality with
        | ChordQuality.Major -> ""
        | ChordQuality.Minor -> "m"
        | ChordQuality.Diminished -> "dim"
        | ChordQuality.Augmented -> "aug"
        | ChordQuality.Dominant7 -> "7"
        | ChordQuality.Major7 -> "maj7"
        | ChordQuality.Minor7 -> "m7"
        | ChordQuality.Diminished7 -> "dim7"
        | ChordQuality.HalfDiminished7 -> "ø7"
        | ChordQuality.Augmented7 -> "aug7"
        | ChordQuality.Sus2 -> "sus2"
        | ChordQuality.Sus4 -> "sus4"
        | ChordQuality.Add9 -> "add9"
        | ChordQuality.Major9 -> "maj9"
        | ChordQuality.Minor9 -> "m9"
        | ChordQuality.Dominant9 -> "9"
        | ChordQuality.Custom s -> s

    /// Format a chord as a string
    let formatChord (chord: Chord) =
        let root = formatNote chord.Root
        let quality = formatChordQuality chord.Quality
        $"%s{root}%s{quality}"

    /// Format a command as a string (for display/debugging)
    let rec format command =
        match command with
        | ChordProgressionCommand prog ->
            let chords =
                prog.Chords
                |> List.map (function
                    | AbsoluteChord chord -> formatChord chord
                    | RomanNumeralChord rn -> $"%d{rn.Degree}")
                |> String.concat "-"

            let key =
                match prog.Key with
                | Some k -> $" in %s{formatNote k}"
                | None -> ""

            $"Progression: %s{chords}%s{key}"

        | NavigationCommand nav ->
            $"Navigation: %A{nav}"

        | ScaleTransformCommand (scale, transforms) ->
            $"Scale Transform: %s{formatNote scale.Root} %A{transforms}"

        | GrothendieckCommand op ->
            $"Grothendieck: %A{op}"

        | PracticeRoutineCommand routine ->
            $"Practice Routine: %s{routine}"

        | CompositeCommand commands ->
            commands
            |> List.map format
            |> String.concat " -> "
            |> sprintf "Composite: %s"

