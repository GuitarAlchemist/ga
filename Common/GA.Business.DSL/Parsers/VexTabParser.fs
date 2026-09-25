namespace GA.Business.DSL.Parsers

open System
open FParsec
open GA.Business.DSL.Types.VexTabTypes

/// <summary>
/// Parser for VexTab notation using FParsec.
/// Follows VexTab's own grammar (https://github.com/0xfe/vextab/blob/master/src/vextab.jison)
/// and tutorial (https://vexflow.com/vextab/tutorial.html); Grammars/VexTab.ebnf describes
/// the subset GA models.
/// </summary>
/// <remarks>
/// Whitespace policy: token parsers never consume the whitespace after them. Separators are
/// explicit: <c>hws</c> / <c>hws1</c> (spaces and tabs) inside a line, a newline between lines.
/// A token parser that eats the whitespace after it also eats the space that starts the next
/// tabstave option, and the newline that ends its line.
/// </remarks>
module VexTabParser =

    // ============================================================================
    // BASIC PARSERS
    // ============================================================================

    /// Any whitespace, newlines included
    let ws = spaces

    /// At least one whitespace character, newlines included
    let ws1 = spaces1

    /// Horizontal whitespace: spaces and tabs, never a newline
    let hws: Parser<unit, unit> = skipManySatisfy (fun c -> c = ' ' || c = '\t')

    /// At least one space or tab, never a newline
    let hws1: Parser<unit, unit> =
        skipMany1SatisfyL (fun c -> c = ' ' || c = '\t') "space"

    /// A literal string; consumes nothing after it
    let str s = pstring s

    /// A literal character; consumes nothing after it
    let ch c = pchar c

    /// An unsigned integer: digits only, no sign, nothing consumed after it
    let pint: Parser<int, unit> =
        many1SatisfyL isDigit "number"
        >>= fun digits ->
            match Int32.TryParse digits with
            | true, value -> preturn value
            | _ -> fail $"number too large: %s{digits}"

    /// Parse a boolean
    let pbool: Parser<bool, unit> =
        choice [ str "true" >>% true; str "false" >>% false ]

    /// Parse an identifier (letters, digits, '-' and '_', starting with a letter)
    let identifier: Parser<string, unit> =
        many1Satisfy2L isLetter (fun c -> isLetter c || isDigit c || c = '-' || c = '_') "identifier"

    /// A space or tab followed by a letter: the start of the next option on a line.
    /// Trailing spaces are left to the end of the line.
    let private optionSeparator: Parser<unit, unit> =
        attempt (hws1 .>> followedBy (satisfy isLetter))

    // ============================================================================
    // NOTE LETTER PARSERS
    // ============================================================================

    /// Parse a note letter (A-G)
    let noteLetter: Parser<NoteLetter, unit> =
        choice
            [ ch 'A' >>% A
              ch 'B' >>% B
              ch 'C' >>% C
              ch 'D' >>% D
              ch 'E' >>% E
              ch 'F' >>% F
              ch 'G' >>% G ]

    /// Accidental of a note, as VexTab writes it: # ## @ @@ n (VexTab's '@' is the flat;
    /// a 'b' after a note name is not an accidental there, it starts a bend)
    let accidental: Parser<VexAccidental, unit> =
        choice
            [ str "##" >>% DoubleSharp
              ch '#' >>% Sharp
              str "@@" >>% DoubleFlat
              ch '@' >>% Flat
              ch 'n' >>% Natural ]

    /// Parse an optional note accidental
    let optAccidental: Parser<VexAccidental option, unit> = opt accidental

    /// Accidental of a key signature (key=Bb, key=F#m), as VexFlow's key specs write it
    let keyAccidental: Parser<VexAccidental, unit> =
        choice [ ch '#' >>% Sharp; ch 'b' >>% Flat ]

    /// Accidental of a tuning note (tuning=Eb/5,...), as VexFlow's note names write it
    let tuningAccidental: Parser<VexAccidental, unit> =
        choice
            [ str "##" >>% DoubleSharp
              ch '#' >>% Sharp
              str "bb" >>% DoubleFlat
              ch 'b' >>% Flat
              ch 'n' >>% Natural ]

    /// Parse an octave
    let octave: Parser<int, unit> = digit |>> (fun c -> int c - int '0')

    // ============================================================================
    // FRET AND STRING PARSERS
    // ============================================================================

    /// Parse a fret (number or X for muted)
    let fret: Parser<Fret, unit> = choice [ ch 'X' >>% Muted; pint |>> FretNumber ]

    /// Parse a string number
    let stringNumber: Parser<int, unit> = pint

    // ============================================================================
    // DURATION PARSERS
    // ============================================================================

    /// Parse a duration code
    let durationCode: Parser<DurationCode, unit> =
        choice
            [ ch 'w' >>% Whole
              ch 'h' >>% Half
              ch 'q' >>% Quarter
              str "32" >>% ThirtySecond
              str "16" >>% Sixteenth
              ch '8' >>% Eighth ]

    /// Parse a duration: ':' code, then 'S' for slash notation, then 'd' for a dot (VexTab's order)
    let duration: Parser<Duration, unit> =
        ch ':'
        >>. pipe3 durationCode (opt (ch 'S')) (opt (ch 'd')) (fun code slash dotted ->
            { Code = code
              Dotted = Option.isSome dotted
              SlashNotation = Option.isSome slash })

    /// Parse optional duration
    let optDuration: Parser<Duration option, unit> = opt duration

    // ============================================================================
    // TECHNIQUE PARSERS
    // ============================================================================

    /// A decorator: written right after a fret, before the next technique or the '/'
    let decorator: Parser<Technique, unit> =
        choice
            [ ch 'v' >>% Vibrato false // normal
              ch 'V' >>% Vibrato true // harsh
              ch 'u' >>% Upstroke
              ch 'd' >>% Downstroke ]

    /// One note of a run, with its techniques in reverse order while the run is being read
    type private Segment<'Head> =
        { Head: 'Head
          Techniques: Technique list }

    let private addTechnique technique (segments: Segment<'Head> list) =
        match segments with
        | current :: earlier ->
            { current with
                Techniques = technique :: current.Techniques }
            :: earlier
        | [] -> segments

    /// `7b9b7`: the second 'b' is the release of the first bend
    let private addBend toFret (segments: Segment<'Head> list) =
        match segments with
        | current :: earlier ->
            match current.Techniques with
            | Bend(first, None) :: before ->
                { current with
                    Techniques = Bend(first, Some toFret) :: before }
                :: earlier
            | techniques ->
                { current with
                    Techniques = Bend(toFret, None) :: techniques }
                :: earlier
        | [] -> segments

    /// A VexTab "line": frets (or note names) joined by techniques, then '/' and the string
    /// (or octave) they all share: `5h7/3`, `7b9b7/3`, `4-5-6/3`, `t12p7p5h7/4`, `C-D-E/4`.
    /// Techniques sit between frets, as VexTab writes them. '-' starts the next note of a run
    /// with no technique, 't' the next note with a tap.
    let private runOf (head: Parser<'Head, unit>) (allowTapPrefix: bool) =
        let first =
            if allowTapPrefix then
                pipe2 (opt (ch 't')) head (fun tap h ->
                    [ { Head = h
                        Techniques = if tap.IsSome then [ Tap ] else [] } ])
            else
                head |>> (fun h -> [ { Head = h; Techniques = [] } ])

        let step: Parser<Segment<'Head> list -> Segment<'Head> list, unit> =
            choice
                [ decorator |>> addTechnique
                  ch 'h' >>. pint |>> (fun n -> addTechnique (HammerOn n))
                  ch 'p' >>. pint |>> (fun n -> addTechnique (PullOff n))
                  ch 's' >>. pint |>> (fun n -> addTechnique (Slide n))
                  ch 'b' >>. pint |>> addBend
                  ch '-' >>. head
                  |>> (fun h segments -> { Head = h; Techniques = [] } :: segments)
                  ch 't' >>. head
                  |>> (fun h segments -> { Head = h; Techniques = [ Tap ] } :: segments) ]

        pipe3 first (many step) (ch '/' >>. pint) (fun start steps position ->
            let segments = List.fold (fun acc apply -> apply acc) start steps

            let ordered =
                segments
                |> List.rev
                |> List.map (fun s ->
                    { s with
                        Techniques = List.rev s.Techniques })

            ordered, position)

    let private standardHead: Parser<NoteLetter * VexAccidental option, unit> =
        noteLetter .>>. optAccidental

    // ============================================================================
    // ARTICULATION PARSERS
    // ============================================================================

    /// Parse articulation type
    let articulationType: Parser<ArticulationType, unit> =
        choice
            [ str "a@a" >>% UpFermata
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
              str "am" >>% BowDown ]

    /// Parse articulation position
    let articulationPosition: Parser<ArticulationPosition, unit> =
        choice [ str "top" >>% Top; str "bottom" >>% Bottom ]

    /// The text of an articulation annotation, between the '$': `.a./top.`
    let private articulationText: Parser<Articulation, unit> =
        between (ch '.') (ch '.') (pipe2 articulationType (ch '/' >>. articulationPosition) (fun artType pos ->
            { Type = artType; Position = pos }))

    /// Parse an articulation: `$.a./top.$`
    let articulation: Parser<Articulation, unit> =
        between (ch '$') (ch '$') articulationText

    /// Parse optional articulation
    let optArticulation: Parser<Articulation option, unit> = opt (attempt articulation)

    // ============================================================================
    // ANNOTATION PARSERS
    // ============================================================================

    /// Parse text style: `.big.` or `.Arial-14-bold.`
    let textStyle: Parser<TextStyle, unit> =
        let face =
            many1Satisfy2L isLetter (fun c -> isLetter c || isDigit c || c = '_') "font face"

        between
            (ch '.')
            (ch '.')
            (choice
                [ attempt (pipe3 face (ch '-' >>. pint) (ch '-' >>. identifier) (fun f size style -> Custom(f, size, style)))
                  identifier |>> Preset ])

    /// VexTab annotations are `$...$` after a note. `$.a./top.$` is an articulation of the
    /// note before it; anything else is text, with an optional style.
    let private annotationContent (raw: string) : Choice<Articulation, Annotation> =
        match run (articulationText .>> eof) raw with
        | Success(art, _, _) -> Choice1Of2 art
        | Failure _ ->
            match run (opt (attempt textStyle) .>>. manySatisfy (fun _ -> true) .>> eof) raw with
            | Success((style, text), _, _) -> Choice2Of2 { Style = style; Text = text }
            | Failure _ -> Choice2Of2 { Style = None; Text = raw }

    let private annotationToken: Parser<string, unit> =
        between (ch '$') (ch '$') (manySatisfy (fun c -> c <> '$' && c <> '\n' && c <> '\r'))

    /// Parse annotation
    let annotationParser: Parser<Annotation, unit> =
        annotationToken
        >>= fun raw ->
            match annotationContent raw with
            | Choice2Of2 ann -> preturn ann
            | Choice1Of2 _ -> preturn { Style = None; Text = raw }

    // ============================================================================
    // NOTE PARSERS
    // ============================================================================

    /// Standard notes of one line: `C/4`, `C#-D-E@/4`
    let standardNotes: Parser<StandardNote list, unit> =
        runOf standardHead false
        |>> (fun (segments, oct) ->
            segments
            |> List.map (fun s ->
                let letter, acc = s.Head

                { Letter = letter
                  Accidental = acc
                  Octave = oct
                  Techniques = s.Techniques
                  Articulation = None }))

    /// Parse a single standard notation note
    let standardNote: Parser<StandardNote, unit> =
        standardNotes
        >>= function
            | [ note ] -> preturn note
            | _ -> fail "a single note"

    /// Tab notes of one line: `5/3`, `5h7/3`, `4-5-6/3`, `t12p7/4`
    let tabNotes: Parser<TabNote list, unit> =
        runOf fret true
        |>> (fun (segments, str) ->
            segments
            |> List.map (fun s ->
                { Fret = s.Head
                  String = str
                  Techniques = s.Techniques
                  Articulation = None }))

    /// Parse a single tablature note
    let tabNote: Parser<TabNote, unit> =
        tabNotes
        >>= function
            | [ note ] -> preturn note
            | _ -> fail "a single note"

    /// Parse the notes of one chord line (standard or tab)
    let private chordLine: Parser<ChordNote list, unit> =
        choice
            [ standardNotes |>> List.map StandardChordNote
              tabNotes |>> List.map TabChordNote ]

    /// Parse a chord: `(0/1.1/2.0/3)`, optionally followed by decorators
    let chord: Parser<Chord, unit> =
        between (ch '(' >>. hws) (ch ')') (sepBy1 (chordLine .>> hws) (ch '.' >>. hws))
        .>>. many decorator
        |>> (fun (lines, decorators) ->
            { Notes = List.concat lines
              Techniques = decorators })

    /// Parse a rest: `##`, `#5#`, `#-3#`
    let rest: Parser<Rest, unit> =
        between (ch '#') (ch '#') (opt (pipe2 (opt (ch '-')) pint (fun minus n -> if minus.IsSome then -n else n)))
        |>> (fun pos -> { Position = pos })

    /// Parse a bar line type
    let barLineType: Parser<BarLineType, unit> =
        choice
            [ str "=||" >>% Double
              str "=|:" >>% RepeatBegin
              str "=:|" >>% RepeatEnd
              str "=::" >>% DoubleRepeat
              str "=|=" >>% EndBar
              ch '|' >>% Single ]

    /// Parse a tuplet
    let tuplet: Parser<Tuplet, unit> =
        between (ch '^') (ch '^') pint |>> (fun num -> { Number = num })

    /// What one token of a notes line gives: items, or an articulation for the note before it
    type private NotePiece =
        | Items of NoteItem list
        | ArticulationOf of Articulation * raw: string

    let private notePiece: Parser<NotePiece, unit> =
        choice
            [ chord |>> (fun c -> Items [ ChordItem c ])
              standardNotes |>> (fun notes -> Items(notes |> List.map StandardNoteItem))
              tabNotes |>> (fun notes -> Items(notes |> List.map TabNoteItem))
              rest |>> (fun r -> Items [ RestItem r ])
              barLineType |>> (fun b -> Items [ BarLine b ])
              tuplet |>> (fun t -> Items [ TupletMarker t ])
              duration |>> (fun d -> Items [ DurationItem d ])
              annotationToken
              |>> (fun raw ->
                  match annotationContent raw with
                  | Choice1Of2 art -> ArticulationOf(art, raw)
                  | Choice2Of2 ann -> Items [ AnnotationItem ann ]) ]

    /// Attach each articulation to the note before it, or keep it as an annotation
    let private assemble (pieces: NotePiece list) : NoteItem list =
        pieces
        |> List.fold
            (fun (acc: NoteItem list) piece ->
                match piece, acc with
                | Items items, _ -> List.rev items @ acc
                | ArticulationOf(art, _), StandardNoteItem note :: earlier when note.Articulation.IsNone ->
                    StandardNoteItem { note with Articulation = Some art } :: earlier
                | ArticulationOf(art, _), TabNoteItem note :: earlier when note.Articulation.IsNone ->
                    TabNoteItem { note with Articulation = Some art } :: earlier
                | ArticulationOf(_, raw), _ -> AnnotationItem { Style = None; Text = raw } :: acc)
            []
        |> List.rev

    /// Parse the items of one token of a notes line
    let noteItems: Parser<NoteItem list, unit> = notePiece |>> (fun piece -> assemble [ piece ])

    // ============================================================================
    // STAVE CONFIGURATION PARSERS
    // ============================================================================

    /// Parse clef type
    let clefType: Parser<ClefType, unit> =
        choice
            [ str "treble" >>% Treble
              str "alto" >>% Alto
              str "tenor" >>% Tenor
              str "bass" >>% Bass
              str "percussion" >>% Percussion ]

    /// Parse key signature: `C`, `Bb`, `F#`, `Am`, `C#m` (VexFlow's key specs); `Cmajor` and
    /// `Aminor` are read too
    let keySignature: Parser<KeySignature, unit> =
        let mode =
            choice [ str "minor" >>% "minor"; str "major" >>% "major"; ch 'm' >>% "minor" ]

        pipe3 noteLetter (opt keyAccidental) (opt mode) (fun root acc mode ->
            { Root = root
              Accidental = acc
              Mode = mode })

    /// Parse time signature
    let timeSignature: Parser<TimeSignature, unit> =
        choice
            [ str "C|" >>% CutTime
              ch 'C' >>% CommonTime
              pipe2 pint (ch '/' >>. pint) (fun num denom -> Numeric(num, denom)) ]

    /// Parse a single tuning note (e.g., "E/5")
    let tuningNote: Parser<NoteLetter * VexAccidental option * int, unit> =
        pipe3 noteLetter (opt tuningAccidental) (ch '/' >>. octave) (fun letter acc oct -> (letter, acc, oct))

    /// Parse tuning
    let tuning: Parser<Tuning, unit> =
        choice
            [ str "standard" >>% Tuning.Standard
              str "dropd" >>% Tuning.DropD
              str "eb" >>% Tuning.EFlat
              // Custom tuning: E/5,B/4,G/4,D/4,A/3,E/3
              sepBy1 tuningNote (ch ',') |>> Tuning.Custom ]

    /// Parse a single tabstave option (without the space before it)
    let tabstaveOptionParser: Parser<TabstaveOptions -> TabstaveOptions, unit> =
        choice
            [ str "notation=" >>. pbool
              |>> (fun v opts -> { opts with Notation = Some v })
              str "tablature=" >>. pbool
              |>> (fun v opts -> { opts with Tablature = Some v })
              str "clef=" >>. clefType |>> (fun v opts -> { opts with Clef = Some v })
              str "key=" >>. keySignature |>> (fun v opts -> { opts with Key = Some v })
              str "time=" >>. timeSignature
              |>> (fun v opts -> { opts with Time = Some v })
              str "tuning=" >>. tuning |>> (fun v opts -> { opts with Tuning = Some v }) ]

    /// Parse a tabstave line: "tabstave notation=true tablature=false key=C time=4/4".
    /// Options not written stay unset.
    let tabstaveLine: Parser<TabstaveOptions, unit> =
        str "tabstave" >>. many (optionSeparator >>. tabstaveOptionParser)
        |>> (fun updates -> List.fold (fun opts update -> update opts) emptyTabstave updates)

    // ============================================================================
    // OPTIONS PARSERS
    // ============================================================================

    /// Parse option value: a boolean, an integer, or any other word (`0.8`, `bold`)
    let optionValue: Parser<OptionValue, unit> =
        many1SatisfyL (fun c -> not (Char.IsWhiteSpace c)) "option value"
        |>> (fun word ->
            match word, Int32.TryParse word with
            | "true", _ -> BoolValue true
            | "false", _ -> BoolValue false
            | _, (true, n) when word |> Seq.forall isDigit -> NumberValue n
            | _ -> StringValue word)

    /// Parse option
    let vexOption: Parser<VexOption, unit> =
        pipe2 identifier (ch '=' >>. optionValue) (fun name value -> { Name = name; Value = value })

    /// Parse options line
    let optionsLine: Parser<VexOption list, unit> =
        str "options" >>. many (optionSeparator >>. vexOption)

    // ============================================================================
    // TEXT LINE PARSERS
    // ============================================================================

    let private textBarLines =
        [ "=||", Double
          "=|:", RepeatBegin
          "=:|", RepeatEnd
          "=::", DoubleRepeat
          "=|=", EndBar
          "|", Single ]
        |> Map.ofList

    let private textSymbols =
        [ "#coda", Coda
          "#segno", Segno
          "#tr", Trill
          "#ff", Fortissimo
          "#pp", Pianissimo
          "#mf", MezzoForte
          "#mp", MezzoPiano
          "#f", Forte
          "#p", Piano ]
        |> Map.ofList

    let private fontSpec: Parser<string * int * string, unit> =
        pipe3
            (many1Satisfy2L isLetter (fun c -> isLetter c || isDigit c || c = '_') "font face")
            (ch '-' >>. pint)
            (ch '-' >>. identifier)
            (fun face size style -> (face, size, style))

    /// A text item is everything up to the next comma; VexTab reads it as text unless it is
    /// one of the special forms
    let private classifyTextItem (raw: string) : TextItem =
        let text = raw.Trim()

        match text with
        | "++" -> NewLine
        | _ when textBarLines.ContainsKey text -> TextBarLine textBarLines[text]
        | _ when textSymbols.ContainsKey text -> TextSymbol textSymbols[text]
        | _ when text.StartsWith(".font=", StringComparison.Ordinal) ->
            match run (fontSpec .>> eof) (text.Substring 6) with
            | Success((face, size, style), _, _) -> FontModifier(face, size, style)
            | Failure _ -> TextString text
        | _ when text.Length > 1 && text[0] = '.' && text.Substring 1 |> Seq.forall isDigit ->
            match Int32.TryParse(text.Substring 1) with
            | true, position -> PositionModifier position
            | _ -> TextString text
        | _ -> TextString text

    /// Parse text item
    let textItem: Parser<TextItem, unit> =
        manySatisfy (fun c -> c <> ',' && c <> '\n' && c <> '\r') |>> classifyTextItem

    /// Parse text line: `text :h,G,C` or `text G,C`
    let textLine: Parser<Duration option * TextItem list, unit> =
        str "text"
        >>. hws
        >>. pipe2 (opt (duration .>> hws .>> optional (ch ','))) (sepBy textItem (ch ',')) (fun dur items -> (dur, items))

    // ============================================================================
    // LINE PARSERS
    // ============================================================================

    /// Parse notes line: `notes :q 5/3 :8 7/3 5h7/3 | (0/1.1/2) $Am$`
    let notesLine: Parser<Duration option * NoteItem list, unit> =
        str "notes"
        >>. hws
        >>. pipe2 (opt (duration .>> hws)) (many (notePiece .>> hws)) (fun dur pieces -> (dur, assemble pieces))

    /// Parse a VexTab line. Each keyword fails without consuming input on another line's
    /// keyword, so an error inside a line is reported where it is.
    let vexTabLine: Parser<VexTabLine, unit> =
        hws
        >>. (choice
                [ optionsLine |>> OptionsLine
                  tabstaveLine |>> TabstaveLine
                  notesLine |>> NotesLine
                  textLine |>> TextLine ]
             .>> hws
             <|>% BlankLine)

    /// Parse a complete VexTab document
    let vexTabDocument: Parser<VexTabDocument, unit> =
        sepBy vexTabLine skipNewline
        |>> (fun lines ->
            let trimmed =
                lines
                |> List.rev
                |> List.skipWhile ((=) BlankLine)
                |> List.rev

            { Lines = trimmed })

    // ============================================================================
    // PUBLIC API
    // ============================================================================

    /// Parse a complete VexTab document
    let parse input : Result<VexTabDocument, string> =
        match run (ws >>. vexTabDocument .>> ws .>> eof) input with
        | Success(result, _, _) -> Result.Ok result
        | Failure(errorMsg, _, _) -> Result.Error errorMsg

    /// Try to parse a VexTab document
    let tryParse input =
        match parse input with
        | Result.Ok doc -> Some doc
        | Result.Error _ -> None

    /// Parse a VexTab note sequence (the content of a notes line, over one or more lines)
    let parseNotes input : Result<NoteItem list, string> =
        match run (ws >>. many (notePiece .>> ws) .>> eof |>> assemble) input with
        | Success(result, _, _) -> Result.Ok result
        | Failure(errorMsg, _, _) -> Result.Error errorMsg

    /// Try to parse VexTab notes
    let tryParseNotes input =
        match parseNotes input with
        | Result.Ok items -> Some items
        | Result.Error _ -> None
