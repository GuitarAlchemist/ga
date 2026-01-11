namespace GA.MusicTheory.DSL.Parsers

open System
open FParsec
open GA.MusicTheory.DSL.Types.VexTabTypes

/// <summary>
/// Parser for VexTab notation using FParsec
/// Implements the VexTab.ebnf grammar
/// </summary>
module VexTabParser =

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

    /// Parse an integer
    let pint : Parser<int, unit> =
        pint32 .>> ws

    /// Parse a boolean
    let pbool : Parser<bool, unit> =
        choice [
            str "true" >>% true
            str "false" >>% false
        ]

    /// Parse an identifier
    let identifier : Parser<string, unit> =
        let isIdentifierFirstChar c = isLetter c
        let isIdentifierChar c = isLetter c || isDigit c || c = '-' || c = '_'
        many1Satisfy2L isIdentifierFirstChar isIdentifierChar "identifier" .>> ws

    /// Parse horizontal whitespace (spaces and tabs, but not newlines)
    let hws = skipManySatisfy (fun c -> c = ' ' || c = '\t')

    /// Parse a specific string with optional trailing horizontal whitespace (no newlines)
    let strNoNewline s = pstring s .>> hws

    /// Parse a specific character with optional trailing horizontal whitespace (no newlines)
    let chNoNewline c = pchar c .>> hws

    /// Parse a boolean without consuming any whitespace
    let pboolNoNewline : Parser<bool, unit> =
        choice [
            pstring "true" >>% true
            pstring "false" >>% false
        ]

    /// Parse an integer without consuming newlines
    let pintNoNewline : Parser<int, unit> =
        pint32 .>> hws

    /// Parse horizontal whitespace (at least one space/tab, no newlines)
    let hws1 = skipMany1 (pchar ' ' <|> pchar '\t')

    // ============================================================================
    // NOTE LETTER PARSERS
    // ============================================================================

    /// Parse a note letter (A-G)
    let noteLetter : Parser<NoteLetter, unit> =
        choice [
            ch 'A' >>% A
            ch 'B' >>% B
            ch 'C' >>% C
            ch 'D' >>% D
            ch 'E' >>% E
            ch 'F' >>% F
            ch 'G' >>% G
        ]

    /// Parse an accidental
    let accidental : Parser<VexAccidental, unit> =
        choice [
            str "##" >>% DoubleSharp
            str "bb" >>% DoubleFlat
            str "x" >>% DoubleSharp
            ch '#' >>% Sharp
            ch 'b' >>% Flat
            ch '♯' >>% Sharp
            ch '♭' >>% Flat
            ch 'n' >>% Natural
            ch '♮' >>% Natural
        ]

    /// Parse an optional accidental
    let optAccidental : Parser<VexAccidental option, unit> =
        opt accidental

    /// Parse an octave
    let octave : Parser<int, unit> =
        digit |>> (fun c -> int c - int '0') .>> ws

    // ============================================================================
    // FRET AND STRING PARSERS
    // ============================================================================

    /// Parse a fret (number or X for muted)
    let fret : Parser<Fret, unit> =
        choice [
            ch 'X' >>% Muted
            pint |>> FretNumber
        ]

    /// Parse a string number
    let stringNumber : Parser<int, unit> =
        digit |>> (fun c -> int c - int '0') .>> ws

    /// Parse a fret without consuming newlines
    let fretNoNewline : Parser<Fret, unit> =
        choice [
            pchar 'X' >>% Muted
            pintNoNewline |>> FretNumber
        ]

    /// Parse a string number without consuming newlines
    let stringNumberNoNewline : Parser<int, unit> =
        digit |>> (fun c -> int c - int '0')

    // ============================================================================
    // DURATION PARSERS
    // ============================================================================

    /// Parse a duration code
    let durationCode : Parser<DurationCode, unit> =
        choice [
            str "w" >>% Whole
            str "h" >>% Half
            str "q" >>% Quarter
            str "32" >>% ThirtySecond
            str "16" >>% Sixteenth
            str "8" >>% Eighth
        ]

    /// Parse a duration
    let duration : Parser<Duration, unit> =
        ch ':' >>. pipe3
            durationCode
            (opt (ch 'd'))
            (opt (ch 'S'))
            (fun code dotted slash ->
                {
                    Code = code
                    Dotted = Option.isSome dotted
                    SlashNotation = Option.isSome slash
                })

    /// Parse optional duration
    let optDuration : Parser<Duration option, unit> =
        opt duration

    // ============================================================================
    // TECHNIQUE PARSERS
    // ============================================================================

    /// Parse a hammer-on
    let hammerOn : Parser<Technique, unit> =
        ch 'h' >>. pint |>> HammerOn

    /// Parse a pull-off
    let pullOff : Parser<Technique, unit> =
        ch 'p' >>. pint |>> PullOff

    /// Parse a slide
    let slide : Parser<Technique, unit> =
        ch 's' >>. pint |>> Slide

    /// Parse a bend
    let bend : Parser<Technique, unit> =
        ch 'b' >>. pipe2
            pint
            (opt (ch 'b' >>. pint))
            (fun toFret release -> Bend (toFret, release))

    /// Parse vibrato
    let vibrato : Parser<Technique, unit> =
        choice [
            ch 'V' >>% Vibrato true   // harsh
            ch 'v' >>% Vibrato false  // normal
        ]

    /// Parse tap
    let tap : Parser<Technique, unit> =
        ch 't' >>% Tap

    /// Parse stroke
    let stroke : Parser<Technique, unit> =
        choice [
            ch 'u' >>% Upstroke
            ch 'd' >>% Downstroke
        ]

    /// Parse a single technique
    let technique : Parser<Technique, unit> =
        choice [
            attempt hammerOn
            attempt pullOff
            attempt slide
            attempt bend
            vibrato
            tap
            stroke
        ]

    /// Parse a chain of techniques
    let techniqueChain : Parser<Technique list, unit> =
        many technique

    // ============================================================================
    // ARTICULATION PARSERS
    // ============================================================================

    /// Parse articulation type
    let articulationType : Parser<ArticulationType, unit> =
        choice [
            str "a@a" >>% UpFermata
            str "a@u" >>% DownFermata
            str "av" >>% Staccatissimo
            str "a." >>% Staccato
            str "a>" >>% Accent
            str "a-" >>% Tenuto
            str "a^" >>% Marcato
            str "a+" >>% LeftHandPizzicato
            str "ao" >>% SnapPizzicato
            str "ah" >>% OpenNote
            str "a|" >>% BowUp
            str "am" >>% BowDown
        ]

    /// Parse articulation position
    let articulationPosition : Parser<ArticulationPosition, unit> =
        choice [
            str "top" >>% Top
            str "bottom" >>% Bottom
        ]

    /// Parse articulation
    let articulation : Parser<Articulation, unit> =
        between (str "$") (str ".$")
            (pipe2
                articulationType
                (ch '/' >>. articulationPosition)
                (fun artType pos -> { Type = artType; Position = pos }))

    /// Parse optional articulation
    let optArticulation : Parser<Articulation option, unit> =
        opt articulation

    // ============================================================================
    // NOTE PARSERS
    // ============================================================================

    /// Parse a standard notation note
    let standardNote : Parser<StandardNote, unit> =
        pipe5
            noteLetter
            optAccidental
            (ch '/' >>. octave)
            techniqueChain
            optArticulation
            (fun letter acc oct techs art ->
                {
                    Letter = letter
                    Accidental = acc
                    Octave = oct
                    Techniques = techs
                    Articulation = art
                })

    /// Parse a tablature note
    let tabNote : Parser<TabNote, unit> =
        pipe5
            fret
            (ch '/' >>. stringNumber)
            techniqueChain
            optArticulation
            (preturn ())
            (fun fretVal str techs art _ ->
                {
                    Fret = fretVal
                    String = str
                    Techniques = techs
                    Articulation = art
                })

    /// Parse a tablature note without consuming newlines
    let tabNoteNoNewline : Parser<TabNote, unit> =
        pipe5
            fretNoNewline
            (pchar '/' >>. stringNumberNoNewline)
            techniqueChain
            optArticulation
            (preturn ())
            (fun fretVal str techs art _ ->
                {
                    Fret = fretVal
                    String = str
                    Techniques = techs
                    Articulation = art
                })

    /// Parse a chord note (standard or tab)
    let chordNote : Parser<ChordNote, unit> =
        choice [
            attempt (standardNote |>> StandardChordNote)
            tabNote |>> TabChordNote
        ]

    /// Parse a chord
    let chord : Parser<Chord, unit> =
        between (ch '(') (ch ')')
            (sepBy1 chordNote (ch '.'))
        .>>. techniqueChain
        |>> (fun (notes, techs) -> { Notes = notes; Techniques = techs })

    /// Parse a rest
    let rest : Parser<Rest, unit> =
        between (ch '#') (ch '#')
            (opt (digit |>> (fun c -> int c - int '0')))
        |>> (fun pos -> { Position = pos })

    /// Parse a bar line type
    let barLineType : Parser<BarLineType, unit> =
        choice [
            str "=||" >>% Double
            str "=|:" >>% RepeatBegin
            str "=:|" >>% RepeatEnd
            str "=::" >>% DoubleRepeat
            str "=|=" >>% EndBar
            ch '|' >>% Single
        ]

    /// Parse a tuplet
    let tuplet : Parser<Tuplet, unit> =
        between (ch '^') (ch '^')
            pint
        |>> (fun num -> { Number = num })

    // ============================================================================
    // STAVE CONFIGURATION PARSERS
    // ============================================================================

    /// Parse clef type
    let clefType : Parser<ClefType, unit> =
        choice [
            str "treble" >>% Treble
            str "alto" >>% Alto
            str "tenor" >>% Tenor
            str "bass" >>% Bass
            str "percussion" >>% Percussion
        ]

    /// Parse key signature
    let keySignature : Parser<KeySignature, unit> =
        pipe3
            noteLetter
            optAccidental
            (opt (choice [str "major"; str "minor"]))
            (fun root acc mode ->
                {
                    Root = root
                    Accidental = acc
                    Mode = mode
                })

    /// Parse time signature
    let timeSignature : Parser<TimeSignature, unit> =
        choice [
            str "C|" >>% CutTime
            str "C" >>% CommonTime
            pipe2
                pint
                (ch '/' >>. pint)
                (fun num denom -> Numeric (num, denom))
        ]

    /// Parse a single tuning note (e.g., "E/5")
    let tuningNote : Parser<NoteLetter * VexAccidental option * int, unit> =
        pipe3
            noteLetter
            optAccidental
            (ch '/' >>. octave)
            (fun letter acc oct -> (letter, acc, oct))

    /// Parse tuning
    let tuning : Parser<Tuning, unit> =
        choice [
            str "standard" >>% Tuning.Standard
            str "dropd" >>% Tuning.DropD
            str "eb" >>% Tuning.EFlat
            // Custom tuning: E/5,B/4,G/4,D/4,A/3,E/3
            attempt (sepBy1 tuningNote (ch ',') |>> Tuning.Custom)
        ]

    /// Parse a single tabstave option (requires leading space/tab)
    let tabstaveOptionParser : Parser<TabstaveOptions -> TabstaveOptions, unit> =
        skipMany1 (pchar ' ' <|> pchar '\t') >>. choice [
            pstring "notation=" >>. pboolNoNewline |>> (fun v opts -> { opts with Notation = Some v })
            pstring "tablature=" >>. pboolNoNewline |>> (fun v opts -> { opts with Tablature = Some v })
            pstring "clef=" >>. clefType |>> (fun v opts -> { opts with Clef = Some v })
            pstring "key=" >>. keySignature |>> (fun v opts -> { opts with Key = Some v })
            pstring "time=" >>. timeSignature |>> (fun v opts -> { opts with Time = Some v })
            pstring "tuning=" >>. tuning |>> (fun v opts -> { opts with Tuning = Some v })
        ]

    /// Parse tabstave line using a line-based approach
    /// Format: "tabstave notation=true tablature=false key=C time=4/4"
    let tabstaveLine : Parser<TabstaveOptions, unit> =
        pstring "tabstave" >>. many (attempt tabstaveOptionParser)
        |>> (fun updates ->
            List.fold (fun opts update -> update opts) defaultTabstave updates)

    // ============================================================================
    // OPTIONS PARSERS
    // ============================================================================

    /// Parse option value
    let optionValue : Parser<OptionValue, unit> =
        choice [
            attempt (pbool |>> BoolValue)
            attempt (pint |>> NumberValue)
            identifier |>> StringValue
        ]

    /// Parse option
    let vexOption : Parser<VexOption, unit> =
        pipe2
            identifier
            (ch '=' >>. optionValue)
            (fun name value -> { Name = name; Value = value })

    /// Parse options line
    let optionsLine : Parser<VexOption list, unit> =
        str "options" >>. many vexOption

    // ============================================================================
    // TEXT LINE PARSERS
    // ============================================================================

    /// Parse symbol
    let symbol : Parser<Symbol, unit> =
        ch '#' >>. choice [
            str "coda" >>% Coda
            str "segno" >>% Segno
            str "tr" >>% Trill
            str "ff" >>% Fortissimo
            str "pp" >>% Pianissimo
            str "mf" >>% MezzoForte
            str "mp" >>% MezzoPiano
            str "f" >>% Forte
            str "p" >>% Piano
        ]

    /// Parse position modifier
    let positionModifier : Parser<int, unit> =
        ch '.' >>. pint

    /// Parse font modifier
    let fontModifier : Parser<string * int * string, unit> =
        str ".font=" >>. pipe3
            identifier
            (ch '-' >>. pint)
            (ch '-' >>. identifier)
            (fun face size style -> (face, size, style))

    /// Parse text item
    let textItem : Parser<TextItem, unit> =
        choice [
            str "++" >>% NewLine
            attempt (barLineType |>> TextBarLine)
            attempt (symbol |>> TextSymbol)
            attempt (positionModifier |>> PositionModifier)
            attempt (fontModifier |>> (fun (f, s, st) -> FontModifier (f, s, st)))
            manySatisfy (fun c -> c <> ',' && c <> '|' && c <> '#' && c <> '+') |>> TextString
        ]

    /// Parse text line
    let textLine : Parser<Duration option * TextItem list, unit> =
        str "text" >>. pipe2
            optDuration
            (sepBy textItem (ch ','))
            (fun dur items -> (dur, items))

    // ============================================================================
    // LINE PARSERS
    // ============================================================================

    /// Parse a note item (standard note, tab note, chord, rest, bar line, tuplet)
    let noteItem : Parser<NoteItem, unit> =
        choice [
            attempt (chord |>> ChordItem)
            attempt (standardNote |>> StandardNoteItem)
            attempt (tabNote |>> TabNoteItem)
            attempt (rest |>> RestItem)
            attempt (barLineType |>> BarLine)
            tuplet |>> TupletMarker
        ]

    /// Parse a note item without consuming newlines
    let noteItemNoNewline : Parser<NoteItem, unit> =
        choice [
            attempt (chord |>> ChordItem)
            attempt (standardNote |>> StandardNoteItem)
            attempt (tabNoteNoNewline |>> TabNoteItem)
            attempt (rest |>> RestItem)
            attempt (barLineType |>> BarLine)
            tuplet |>> TupletMarker
        ]

    /// Parse notes line
    let notesLine : Parser<Duration option * NoteItem list, unit> =
        pstring "notes" >>. hws >>. pipe2
            optDuration
            (sepBy noteItemNoNewline (choice [pchar '-' >>% (); hws1]))
            (fun dur items -> (dur, items))

    /// Parse a VexTab line
    let vexTabLine : Parser<VexTabLine, unit> =
        hws >>. choice [
            attempt (optionsLine |>> OptionsLine)
            attempt (tabstaveLine |>> TabstaveLine)
            attempt (notesLine |>> (fun (dur, items) -> NotesLine (dur, items)))
            attempt (textLine |>> (fun (dur, items) -> TextLine (dur, items)))
            skipNewline >>% BlankLine
        ]

    /// Parse a complete VexTab document
    let vexTabDocument : Parser<VexTabDocument, unit> =
        sepEndBy vexTabLine skipNewline .>> ws |>> (fun lines -> { Lines = lines })

    // ============================================================================
    // PUBLIC API - COMPLETE DOCUMENT PARSING
    // ============================================================================

    /// Parse a complete VexTab document
    let parse input : Result<VexTabDocument, string> =
        match run (ws >>. vexTabDocument .>> eof) input with
        | Success (result, _, _) -> Result.Ok result
        | Failure (errorMsg, _, _) -> Result.Error errorMsg

    /// Try to parse a VexTab document
    let tryParse input =
        match parse input with
        | Result.Ok doc -> Some doc
        | Result.Error _ -> None

    /// Parse text style
    let textStyle : Parser<TextStyle, unit> =
        between (ch '.') (ch '.')
            (choice [
                attempt (pipe3
                    identifier
                    (ch '-' >>. pint)
                    (ch '-' >>. identifier)
                    (fun face size style -> Custom (face, size, style)))
                identifier |>> Preset
            ])

    /// Parse annotation
    let annotationParser : Parser<Annotation, unit> =
        between (ch '$') (ch '$')
            (pipe2
                (opt textStyle)
                (manySatisfy (fun c -> c <> '$'))
                (fun style text -> { Style = style; Text = text }))

    // ============================================================================
    // PUBLIC API
    // ============================================================================

    /// Parse a VexTab note sequence
    let parseNotes input : Result<NoteItem list, string> =
        let noteSequence = sepBy noteItem (choice [ch '-' >>% (); ws1])
        match run (ws >>. noteSequence .>> eof) input with
        | Success (result, _, _) -> Result.Ok result
        | Failure (errorMsg, _, _) -> Result.Error errorMsg

    /// Try to parse VexTab notes
    let tryParseNotes input =
        match parseNotes input with
        | Result.Ok items -> Some items
        | Result.Error _ -> None

