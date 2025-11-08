namespace GA.MusicTheory.DSL.Parsers

open System
open FParsec
open GA.MusicTheory.DSL.Types.GrammarTypes

/// <summary>
/// Parser for Fretboard Navigation DSL using FParsec
/// Implements the FretboardNavigation.ebnf grammar
/// </summary>
module FretboardNavigationParser =

    // ============================================================================
    // BASIC PARSERS
    // ============================================================================

    let ws = spaces
    let ws1 = spaces1
    let str s = pstring s .>> ws
    let ch c = pchar c .>> ws

    // ============================================================================
    // POSITION PARSERS
    // ============================================================================

    /// Parse a string number (1-12)
    let stringNumber : Parser<int, unit> =
        pint32 .>> ws
        >>= fun n ->
            if n >= 1 && n <= 12 then preturn n
            else fail $"Invalid string number: %d{n} (must be 1-12)"

    /// Parse a fret number (0-24)
    let fretNumber : Parser<int, unit> =
        pint32 .>> ws
        >>= fun n ->
            if n >= 0 && n <= 24 then preturn n
            else fail $"Invalid fret number: %d{n} (must be 0-24)"

    /// Parse a finger
    let finger : Parser<Finger, unit> =
        choice [
            str "thumb" >>% Thumb
            str "T" >>% Thumb
            str "index" >>% Index
            str "1" >>% Index
            str "middle" >>% Middle
            str "2" >>% Middle
            str "ring" >>% Ring
            str "3" >>% Ring
            str "pinky" >>% Pinky
            str "4" >>% Pinky
        ]

    /// Parse a fretboard position
    let fretboardPosition : Parser<FretboardPosition, unit> =
        choice [
            // Format: "string 3 fret 5"
            pipe3 (str "string" >>. stringNumber) (str "fret" >>. fretNumber) (opt finger)
                (fun s f fing -> { String = s; Fret = f; Finger = fing })

            // Format: "3:5" (string:fret)
            pipe3 stringNumber (ch ':') fretNumber
                (fun s _ f -> { String = s; Fret = f; Finger = None })

            // Format: "position 5 on string 3"
            pipe2 (str "position" >>. fretNumber) (str "on" >>. str "string" >>. stringNumber)
                (fun f s -> { String = s; Fret = f; Finger = None })
        ]

    // ============================================================================
    // CAGED SHAPE PARSERS
    // ============================================================================

    /// Parse a CAGED letter
    let cagedLetter : Parser<CAGEDShape, unit> =
        choice [
            ch 'C' >>% C_Shape
            ch 'A' >>% A_Shape
            ch 'G' >>% G_Shape
            ch 'E' >>% E_Shape
            ch 'D' >>% D_Shape
        ]

    /// Parse a CAGED shape specification
    let cagedShape : Parser<CAGEDShape * int, unit> =
        pipe2 (str "CAGED" >>. str "shape" >>. cagedLetter)
              (opt (str "at" >>. str "fret" >>. fretNumber))
              (fun shape fret -> (shape, defaultArg fret 0))

    // ============================================================================
    // DIRECTION PARSERS
    // ============================================================================

    /// Parse a direction
    let direction : Parser<Direction, unit> =
        choice [
            str "up" >>% Up
            str "down" >>% Down
            str "left" >>% Left
            str "right" >>% Right
            str "higher" >>% Up
            str "lower" >>% Down
        ]

    // ============================================================================
    // NAVIGATION COMMAND PARSERS
    // ============================================================================

    /// Parse a goto position command
    let gotoPosition : Parser<NavigationCommand, unit> =
        fretboardPosition |>> GotoPosition

    /// Parse a goto shape command
    let gotoShape : Parser<NavigationCommand, unit> =
        cagedShape |>> fun (shape, fret) -> GotoShape (shape, fret)

    /// Parse a move command
    let moveCommand : Parser<NavigationCommand, unit> =
        pipe2 (str "move" >>. direction) (opt pint32)
              (fun dir dist -> Move (dir, defaultArg dist 1))

    /// Parse a slide command
    let slideCommand : Parser<NavigationCommand, unit> =
        pipe3 (str "slide" >>. str "from" >>. fretboardPosition)
              (str "to")
              fretboardPosition
              (fun from _ ``to`` -> Slide (from, ``to``))

    /// Parse a navigation command
    let navigationCommand : Parser<NavigationCommand, unit> =
        choice [
            attempt slideCommand
            attempt gotoShape
            attempt moveCommand
            gotoPosition
        ]

    // ============================================================================
    // PUBLIC API
    // ============================================================================

    /// Parse a navigation command string
    let parse input =
        match run (ws >>. navigationCommand .>> eof) input with
        | Success (result, _, _) -> Result.Ok result
        | Failure (errorMsg, _, _) -> Result.Error errorMsg

    /// Parse a navigation command string and return a DslCommand
    let parseCommand input =
        parse input
        |> Result.map NavigationCommand

    /// Try to parse a navigation command string
    let tryParse input =
        match parse input with
        | Result.Ok cmd -> Some cmd
        | Result.Error _ -> None

