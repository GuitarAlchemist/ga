namespace GA.MusicTheory.DSL.Parsers

open FParsec
open GA.MusicTheory.DSL.Types.AsciiTabTypes

/// <summary>
/// Parser for ASCII Tab notation
/// </summary>
module AsciiTabParser =

    // ============================================================================
    // BASIC PARSERS
    // ============================================================================

    /// Parse whitespace
    let ws = spaces

    /// Parse whitespace (at least one)
    let ws1 = spaces1

    /// Parse optional whitespace
    let optWs() = opt spaces >>% ()

    /// Parse newline (use skipNewline instead of pstring for newline chars)
    let newline = skipNewline

    /// Parse digit
    let digit = satisfy isDigit

    /// Parse integer
    let pint = many1Chars digit |>> int

    /// Parse letter
    let letter = satisfy isLetter

    /// Parse identifier
    let identifier = many1Chars (letter <|> digit <|> pchar '_')

    // ============================================================================
    // STRING NAME PARSERS
    // ============================================================================

    /// Parse string name
    let stringName : Parser<StringName, unit> =
        choice [
            (pchar 'E' <|> pchar 'e') >>% HighE
            (pchar 'B' <|> pchar 'b') >>% B
            (pchar 'G' <|> pchar 'g') >>% G
            (pchar 'D' <|> pchar 'd') >>% D
            (pchar 'A' <|> pchar 'a') >>% A
        ]

    // ============================================================================
    // FRET PARSERS
    // ============================================================================

    /// Parse fret number
    let fretNumber : Parser<Fret, unit> =
        pint |>> FretNumber

    /// Parse muted string
    let muted : Parser<Fret, unit> =
        (pchar 'x' <|> pchar 'X') >>% Muted

    /// Parse open string
    let openString : Parser<Fret, unit> =
        pchar '0' >>% Open

    /// Parse fret
    let fret : Parser<Fret, unit> =
        choice [
            attempt muted
            attempt openString
            fretNumber
        ]

    // ============================================================================
    // TECHNIQUE PARSERS
    // ============================================================================

    /// Parse hammer-on
    let hammerOn : Parser<Technique, unit> =
        pipe3
            fret
            (pchar 'h' <|> pchar 'H')
            fret
            (fun from _ to' -> HammerOn (from, to'))

    /// Parse pull-off
    let pullOff : Parser<Technique, unit> =
        pipe3
            fret
            (pchar 'p' <|> pchar 'P')
            fret
            (fun from _ to' -> PullOff (from, to'))

    /// Parse slide up
    let slideUp : Parser<Technique, unit> =
        pipe3
            fret
            (pchar '/' <|> pchar 's' <|> pchar 'S')
            fret
            (fun from _ to' -> SlideUp (from, to'))

    /// Parse slide down
    let slideDown : Parser<Technique, unit> =
        pipe3
            fret
            (pchar '\\' <|> pchar 's' <|> pchar 'S')
            fret
            (fun from _ to' -> SlideDown (from, to'))

    /// Parse bend
    let bend : Parser<Technique, unit> =
        pipe2
            fret
            ((pchar 'b' <|> pchar '^') >>. opt fret)
            (fun from to' -> Bend (from, to'))

    /// Parse bend and release
    let bendRelease : Parser<Technique, unit> =
        pipe4
            fret
            (pchar 'b')
            fret
            (pchar 'r' >>. fret)
            (fun from _ bendTo releaseTo -> BendRelease (from, bendTo, releaseTo))

    /// Parse vibrato
    let vibrato : Parser<Technique, unit> =
        pipe2
            fret
            (pchar '~' <|> pchar 'v' <|> pchar 'V')
            (fun f _ -> Vibrato f)

    /// Parse harmonic
    let harmonic : Parser<Technique, unit> =
        between (pchar '<') (pchar '>') fret
        <|> between (pchar '(') (pchar ')') fret
        |>> Harmonic

    /// Parse artificial harmonic
    let artificialHarmonic : Parser<Technique, unit> =
        between (pchar '[') (pchar ']') fret
        |>> ArtificialHarmonic

    /// Parse pinch harmonic
    let pinchHarmonic : Parser<Technique, unit> =
        (pstring "PH" <|> pstring "ph") >>. fret
        |>> PinchHarmonic

    /// Parse tap
    let tap : Parser<Technique, unit> =
        (pchar 't' <|> pchar 'T') >>. fret
        |>> Tap

    /// Parse trill
    let trill : Parser<Technique, unit> =
        pipe3
            fret
            (pstring "tr" <|> pstring "TR")
            fret
            (fun f1 _ f2 -> Trill (f1, f2))

    /// Parse pre-bend
    let preBend : Parser<Technique, unit> =
        between (pchar '(') (pchar ')') (fret .>> pchar 'b')
        |>> PreBend

    /// Parse ghost note
    let ghostNote : Parser<Technique, unit> =
        between (pchar '(') (pchar ')') fret
        |>> GhostNote

    /// Parse dead note
    let deadNote : Parser<Technique, unit> =
        (pchar 'x' <|> pchar 'X') >>% DeadNote

    /// Parse technique
    let technique : Parser<Technique, unit> =
        choice [
            attempt bendRelease
            attempt hammerOn
            attempt pullOff
            attempt slideUp
            attempt slideDown
            attempt bend
            attempt vibrato
            attempt artificialHarmonic
            attempt harmonic
            attempt pinchHarmonic
            attempt tap
            attempt trill
            attempt preBend
            attempt ghostNote
            deadNote
        ]

    // ============================================================================
    // BAR LINE PARSERS
    // ============================================================================

    /// Parse bar line
    let barLine : Parser<BarLineType, unit> =
        choice [
            pstring ":||:" >>% DoubleRepeat
            pstring "||" >>% Double
            pstring "|:" >>% RepeatBegin
            pstring ":|" >>% RepeatEnd
            pchar '|' >>% Single
        ]

    // ============================================================================
    // NOTE ELEMENT PARSERS
    // ============================================================================

    /// Parse spacing character
    let spacing : Parser<NoteElement, unit> =
        (pchar '-' <|> pchar ' ' <|> pchar '.') |>> Spacing

    /// Parse note element
    let noteElement : Parser<NoteElement, unit> =
        choice [
            attempt (technique |>> TechniqueNote)
            attempt (fret |>> SimpleFret)
            spacing
        ]

    // ============================================================================
    // STRING LINE PARSERS
    // ============================================================================

    /// Parse string content item
    let stringContentItem : Parser<StringContentItem, unit> =
        choice [
            attempt (barLine |>> BarLine)
            attempt (noteElement |>> Note)
            pchar ' ' >>% Space
        ]

    /// Parse string line
    let stringLine : Parser<StringLine, unit> =
        pipe3
            stringName
            (pchar '|')
            (many stringContentItem)
            (fun name _ content -> { StringName = name; Content = content })

    // ============================================================================
    // ANNOTATION PARSERS
    // ============================================================================

    /// Parse chord quality
    let chordQuality : Parser<ChordQuality, unit> =
        choice [
            pstring "maj" >>% Major
            pstring "min" >>% Minor
            pchar 'm' >>% Minor
            pstring "dim" >>% Diminished
            pstring "aug" >>% Augmented
            pstring "sus" >>% Suspended
        ]

    /// Parse chord name
    let chordName : Parser<ChordName, unit> =
        pipe4
            (satisfy isUpper |>> string)
            (opt (pchar '#' <|> pchar 'b') |>> Option.map string)
            (opt chordQuality)
            (opt pint)
            (fun root acc qual ext -> { Root = root; Accidental = acc; Quality = qual; Extension = ext })

    /// Parse tempo
    let tempo : Parser<Annotation, unit> =
        pstring "Tempo:" >>. ws >>. pint |>> Tempo

    /// Parse time signature
    let timeSignature : Parser<Annotation, unit> =
        pipe3
            pint
            (pchar '/')
            pint
            (fun num _ denom -> TimeSignature { Numerator = num; Denominator = denom })

    /// Parse capo
    let capo : Parser<Annotation, unit> =
        pstring "Capo:" >>. ws >>. pint |>> Capo

    /// Parse section
    let section : Parser<Annotation, unit> =
        between (pchar '[') (pchar ']') identifier
        |>> (fun s ->
            match s.ToLower() with
            | "intro" -> Section Intro
            | "verse" -> Section Verse
            | "chorus" -> Section Chorus
            | "bridge" -> Section Bridge
            | "solo" -> Section Solo
            | "outro" -> Section Outro
            | _ -> Section (Custom s))

    /// Parse repeat
    let repeat : Parser<Annotation, unit> =
        pchar 'x' >>. pint |>> Repeat

    /// Parse annotation
    let annotation : Parser<Annotation, unit> =
        choice [
            attempt tempo
            attempt timeSignature
            attempt capo
            attempt section
            attempt repeat
            attempt (chordName |>> Chord)
            pstring "PM" >>% PalmMute
            pstring "LR" >>% LetRing
            pstring "TR" >>% Tremolo
        ]

    // ============================================================================
    // PUBLIC API
    // ============================================================================

    /// Parse a complete ASCII tab document
    let parse input : Result<AsciiTabDocument, string> =
        // Simplified parser - just parse string lines for now
        let doc = many (stringLine .>> opt newline)
        match run (ws >>. doc .>> eof) input with
        | Success (lines, _, _) ->
            let measures = [{ Annotations = []; Staff = { Lines = lines; StringCount = lines.Length } }]
            Result.Ok { Header = None; Measures = measures }
        | Failure (errorMsg, _, _) -> Result.Error errorMsg

    /// Try to parse ASCII tab
    let tryParse input =
        match parse input with
        | Result.Ok doc -> Some doc
        | Result.Error _ -> None

