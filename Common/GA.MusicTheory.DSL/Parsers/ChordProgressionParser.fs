namespace GA.MusicTheory.DSL.Parsers

open System
open FParsec
open GA.MusicTheory.DSL.Types.GrammarTypes

/// <summary>
/// Parser for Chord Progression DSL using FParsec
/// Implements the ChordProgression.ebnf grammar
/// </summary>
module ChordProgressionParser =

    // ============================================================================
    // BASIC PARSERS
    // ============================================================================

    /// Parse whitespace
    let ws = spaces

    /// Parse optional whitespace
    let ws1 = spaces1

    /// Parse a specific string with optional trailing whitespace
    let str s = pstring s .>> ws

    /// Parse a specific character with optional trailing whitespace
    let ch c = pchar c .>> ws

    // ============================================================================
    // NOTE PARSERS
    // ============================================================================

    /// Parse a note letter (A-G)
    let noteLetter : Parser<char, unit> =
        anyOf ['A'; 'B'; 'C'; 'D'; 'E'; 'F'; 'G'] .>> ws

    /// Parse an accidental
    let accidental : Parser<Accidental, unit> =
        choice [
            str "##" >>% DoubleSharp
            str "bb" >>% DoubleFlat
            str "x" >>% DoubleSharp
            ch '#' >>% Sharp
            ch 'b' >>% Flat
            ch '♯' >>% Sharp
            ch '♭' >>% Flat
            ch '♮' >>% Natural
        ]

    /// Parse an optional accidental
    let optAccidental = opt accidental

    /// Parse an octave number
    let octave : Parser<int, unit> =
        pint32 .>> ws

    /// Parse an optional octave
    let optOctave = opt octave

    /// Parse a complete note
    let note : Parser<Note, unit> =
        pipe3 noteLetter optAccidental optOctave (fun letter acc oct ->
            { Letter = letter
              Accidental = acc
              Octave = oct })

    // ============================================================================
    // CHORD QUALITY PARSERS
    // ============================================================================

    /// Parse chord quality
    let chordQuality : Parser<ChordQuality, unit> =
        choice [
            // 7th chords (must come before basic triads)
            str "maj7" >>% Major7
            str "min7" >>% Minor7
            str "m7" >>% Minor7
            str "dom7" >>% Dominant7
            str "dim7" >>% Diminished7
            str "ø7" >>% HalfDiminished7
            str "aug7" >>% Augmented7
            str "7" >>% Dominant7

            // 9th chords
            str "maj9" >>% Major9
            str "min9" >>% Minor9
            str "m9" >>% Minor9
            str "9" >>% Dominant9

            // Sus chords
            str "sus4" >>% Sus4
            str "sus2" >>% Sus2

            // Add chords
            str "add9" >>% Add9

            // Basic triads
            str "maj" >>% Major
            str "min" >>% Minor
            str "m" >>% Minor
            str "dim" >>% Diminished
            str "aug" >>% Augmented
            str "+" >>% Augmented
            str "°" >>% Diminished

            // Default to major if no quality specified
            preturn Major
        ]

    // ============================================================================
    // CHORD EXTENSION PARSERS
    // ============================================================================

    /// Parse an interval number
    let intervalNumber : Parser<int, unit> =
        choice [
            str "b2" >>% 1
            str "2" >>% 2
            str "b3" >>% 3
            str "3" >>% 4
            str "4" >>% 5
            str "#4" >>% 6
            str "b5" >>% 6
            str "5" >>% 7
            str "#5" >>% 8
            str "b6" >>% 8
            str "6" >>% 9
            str "b7" >>% 10
            str "7" >>% 11
            str "b9" >>% 13
            str "9" >>% 14
            str "#9" >>% 15
            str "11" >>% 17
            str "#11" >>% 18
            str "b13" >>% 20
            str "13" >>% 21
            pint32 .>> ws
        ]

    /// Parse a chord extension
    let chordExtension : Parser<ChordExtension, unit> =
        choice [
            str "add" >>. intervalNumber |>> Add
            str "omit" >>. intervalNumber |>> Omit
            ch '/' >>. note |>> Slash
        ]

    /// Parse optional chord extensions
    let chordExtensions = many chordExtension

    // ============================================================================
    // DURATION PARSERS
    // ============================================================================

    /// Parse a duration
    let duration : Parser<Duration, unit> =
        choice [
            str "whole" >>% Whole
            str "half" >>% Half
            str "quarter" >>% Quarter
            str "eighth" >>% Eighth
            str "sixteenth" >>% Sixteenth
            pfloat .>> ws |>> Beats
        ]

    /// Parse optional duration
    let optDuration = opt (ch ':' >>. duration)

    // ============================================================================
    // CHORD PARSERS
    // ============================================================================

    /// Parse an absolute chord
    let absoluteChord : Parser<Chord, unit> =
        pipe4 note chordQuality chordExtensions optDuration (fun root quality exts dur ->
            { Root = root
              Quality = quality
              Extensions = exts
              Duration = dur })

    // ============================================================================
    // ROMAN NUMERAL PARSERS
    // ============================================================================

    /// Parse a roman numeral degree
    let romanDegree : Parser<int * RomanQuality, unit> =
        choice [
            // Major (uppercase)
            str "VII" >>% (7, MajorRoman)
            str "VI" >>% (6, MajorRoman)
            str "V" >>% (5, MajorRoman)
            str "IV" >>% (4, MajorRoman)
            str "III" >>% (3, MajorRoman)
            str "II" >>% (2, MajorRoman)
            str "I" >>% (1, MajorRoman)

            // Minor (lowercase)
            str "vii" >>% (7, MinorRoman)
            str "vi" >>% (6, MinorRoman)
            str "v" >>% (5, MinorRoman)
            str "iv" >>% (4, MinorRoman)
            str "iii" >>% (3, MinorRoman)
            str "ii" >>% (2, MinorRoman)
            str "i" >>% (1, MinorRoman)
        ]

    /// Parse roman numeral quality modifiers
    let romanQualityModifier : Parser<RomanQuality, unit> =
        choice [
            str "°" >>% DiminishedRoman
            str "dim" >>% DiminishedRoman
            str "ø" >>% DiminishedRoman
            str "+" >>% AugmentedRoman
            str "aug" >>% AugmentedRoman
            str "7" >>% Dominant7Roman
            preturn MajorRoman
        ]

    /// Parse a roman numeral chord
    let romanNumeralChord : Parser<RomanNumeral, unit> =
        pipe4 (opt accidental) romanDegree chordExtensions optDuration (fun acc (degree, quality) exts dur ->
            { Degree = degree
              Quality = quality
              Accidental = acc
              Extensions = exts
              Duration = dur })

    // ============================================================================
    // PROGRESSION PARSERS
    // ============================================================================

    /// Parse a progression chord (either absolute or roman numeral)
    let progressionChord : Parser<ProgressionChord, unit> =
        choice [
            attempt (romanNumeralChord |>> RomanNumeralChord)
            absoluteChord |>> AbsoluteChord
        ]

    /// Parse a chord separator (accepts -, ->, ,, |, or whitespace)
    let chordSeparator =
        choice [
            attempt (str "->" >>% ' ')  // Arrow separator
            ch '-' >>% ' '               // Dash separator
            ch ',' >>% ' '               // Comma separator
            ch '|' >>% ' '               // Bar separator
            ws1 >>% ' '                  // Whitespace separator
        ]

    /// Parse a list of chords
    let chordList : Parser<ProgressionChord list, unit> =
        sepBy1 progressionChord (ws >>. chordSeparator .>> ws)

    // ============================================================================
    // KEY AND TIME SIGNATURE PARSERS
    // ============================================================================

    /// Parse a key specification
    let keySpec : Parser<Note, unit> =
        str "in" >>. ws >>. note

    /// Parse optional key
    let optKey = opt keySpec

    /// Parse a time signature
    let timeSignature : Parser<TimeSignature, unit> =
        pipe3 pint32 (ch '/') pint32 (fun num _ denom ->
            { Numerator = num
              Denominator = denom })

    /// Parse optional time signature
    let optTimeSignature = opt (str "time" >>. ws >>. timeSignature)

    /// Parse tempo
    let tempo : Parser<int, unit> =
        str "tempo" >>. ws >>. pint32 .>> ws

    /// Parse optional tempo
    let optTempo = opt tempo

    // ============================================================================
    // TOP-LEVEL PROGRESSION PARSER
    // ============================================================================

    /// Parse a complete chord progression
    let chordProgression : Parser<ChordProgression, unit> =
        pipe4 chordList optKey optTimeSignature optTempo (fun chords key timeSig tempo ->
            { Chords = chords
              Key = key
              TimeSignature = timeSig
              Tempo = tempo
              Metadata = Map.empty })

    // ============================================================================
    // PUBLIC API
    // ============================================================================

    /// Parse a chord progression string
    let parse input =
        match run (ws >>. chordProgression .>> eof) input with
        | Success (result, _, _) -> Result.Ok result
        | Failure (errorMsg, _, _) -> Result.Error errorMsg

    /// Parse a chord progression string and return a DslCommand
    let parseCommand input =
        parse input
        |> Result.map ChordProgressionCommand

    /// Try to parse a chord progression string
    let tryParse input =
        match parse input with
        | Result.Ok prog -> Some prog
        | Result.Error _ -> None

