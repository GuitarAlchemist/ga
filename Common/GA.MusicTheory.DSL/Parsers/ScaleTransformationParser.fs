namespace GA.MusicTheory.DSL.Parsers

open System
open FParsec
open GA.MusicTheory.DSL.Types.GrammarTypes

/// <summary>
/// Parser for Scale Transformation DSL using FParsec
/// Implements the ScaleTransformation.ebnf grammar
/// </summary>
module ScaleTransformationParser =

    // ============================================================================
    // BASIC PARSERS
    // ============================================================================

    let ws = spaces
    let ws1 = spaces1
    let str s = pstring s .>> ws
    let ch c = pchar c .>> ws

    // ============================================================================
    // NOTE AND SCALE PARSERS (Reuse from ChordProgressionParser)
    // ============================================================================

    let noteLetter : Parser<char, unit> =
        anyOf ['A'; 'B'; 'C'; 'D'; 'E'; 'F'; 'G'] .>> ws

    let accidental : Parser<Accidental, unit> =
        choice [
            str "##" >>% DoubleSharp
            str "bb" >>% DoubleFlat
            ch '#' >>% Sharp
            ch 'b' >>% Flat
        ]

    let note : Parser<Note, unit> =
        pipe2 noteLetter (opt accidental) (fun letter acc ->
            { Letter = letter; Accidental = acc; Octave = None })

    // ============================================================================
    // SCALE PARSERS
    // ============================================================================

    /// Parse a scale (simplified for now)
    let scale : Parser<Scale, unit> =
        pipe2 note (str "major" <|> str "minor" <|> str "dorian" <|> str "phrygian")
              (fun root scaleType ->
                  let intervals =
                      match scaleType with
                      | "major" -> [0; 2; 4; 5; 7; 9; 11]
                      | "minor" -> [0; 2; 3; 5; 7; 8; 10]
                      | "dorian" -> [0; 2; 3; 5; 7; 9; 10]
                      | "phrygian" -> [0; 1; 3; 5; 7; 8; 10]
                      | _ -> [0; 2; 4; 5; 7; 9; 11]
                  { Root = root; Intervals = intervals; Name = Some scaleType })

    // ============================================================================
    // TRANSFORMATION PARSERS
    // ============================================================================

    /// Parse a transpose transformation
    let transposeTransform : Parser<ScaleTransformation, unit> =
        str "transpose" >>. pint32 |>> Transpose

    /// Parse a rotate transformation
    let rotateTransform : Parser<ScaleTransformation, unit> =
        str "rotate" >>. pint32 |>> Rotate

    /// Parse an invert transformation
    let invertTransform : Parser<ScaleTransformation, unit> =
        str "invert" >>% Invert

    /// Parse a transformation
    let transformation : Parser<ScaleTransformation, unit> =
        choice [
            transposeTransform
            rotateTransform
            invertTransform
        ]

    /// Parse a transformation chain
    let transformationChain : Parser<ScaleTransformation list, unit> =
        sepBy1 transformation (str "->" <|> str "|>" <|> str "then")

    // ============================================================================
    // TOP-LEVEL PARSER
    // ============================================================================

    /// Parse a scale transformation command
    let scaleTransformCommand : Parser<Scale * ScaleTransformation list, unit> =
        pipe2 scale transformationChain (fun s transforms -> (s, transforms))

    // ============================================================================
    // PUBLIC API
    // ============================================================================

    /// Parse a scale transformation string
    let parse input =
        match run (ws >>. scaleTransformCommand .>> eof) input with
        | Success (result, _, _) -> Result.Ok result
        | Failure (errorMsg, _, _) -> Result.Error errorMsg

    /// Parse a scale transformation string and return a DslCommand
    let parseCommand input =
        parse input
        |> Result.map (fun (scale, transforms) -> ScaleTransformCommand (scale, transforms))

    /// Try to parse a scale transformation string
    let tryParse input =
        match parse input with
        | Result.Ok result -> Some result
        | Result.Error _ -> None

