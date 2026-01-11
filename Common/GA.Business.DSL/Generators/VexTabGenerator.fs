namespace GA.MusicTheory.DSL.Generators

open System
open System.Text
open GA.MusicTheory.DSL.Types.VexTabTypes

/// <summary>
/// Generator for VexTab notation from DSL types
/// Converts our music theory DSL to VexTab format for rendering with VexFlow
/// </summary>
module VexTabGenerator =

    // ============================================================================
    // PRIMITIVE FORMATTERS
    // ============================================================================

    /// Format a note letter
    let formatNoteLetter (letter: NoteLetter) =
        match letter with
        | A -> "A"
        | B -> "B"
        | C -> "C"
        | D -> "D"
        | E -> "E"
        | F -> "F"
        | G -> "G"

    /// Format an accidental
    let formatAccidental (acc: VexAccidental) =
        match acc with
        | VexAccidental.Sharp -> "#"
        | VexAccidental.DoubleSharp -> "##"
        | VexAccidental.Flat -> "b"
        | VexAccidental.DoubleFlat -> "bb"
        | VexAccidental.Natural -> "n"

    /// Format a fret
    let formatFret (fret: Fret) =
        match fret with
        | FretNumber n -> string n
        | Muted -> "X"

    // ============================================================================
    // DURATION FORMATTERS
    // ============================================================================

    /// Format a duration code
    let formatDurationCode (code: DurationCode) =
        match code with
        | DurationCode.Whole -> "w"
        | DurationCode.Half -> "h"
        | DurationCode.Quarter -> "q"
        | DurationCode.Eighth -> "8"
        | DurationCode.Sixteenth -> "16"
        | DurationCode.ThirtySecond -> "32"

    /// Format a duration
    let formatDuration (dur: Duration) =
        let code = formatDurationCode dur.Code
        let dot = if dur.Dotted then "d" else ""
        let slash = if dur.SlashNotation then "S" else ""
        $":%s{code}%s{dot}%s{slash}"

    // ============================================================================
    // TECHNIQUE FORMATTERS
    // ============================================================================

    /// Format a technique
    let formatTechnique (tech: Technique) =
        match tech with
        | HammerOn toFret -> $"h%d{toFret}"
        | PullOff toFret -> $"p%d{toFret}"
        | Slide toFret -> $"s%d{toFret}"
        | Bend (toFret, None) -> $"b%d{toFret}"
        | Bend (toFret, Some release) -> $"b%d{toFret}b%d{release}"
        | Vibrato false -> "v"
        | Vibrato true -> "V"
        | Tap -> "t"
        | Upstroke -> "u"
        | Downstroke -> "d"

    /// Format a technique chain
    let formatTechniqueChain (techs: Technique list) =
        techs |> List.map formatTechnique |> String.concat ""

    // ============================================================================
    // ARTICULATION FORMATTERS
    // ============================================================================

    /// Format an articulation type
    let formatArticulationType (artType: ArticulationType) =
        match artType with
        | Staccato -> "a."
        | Staccatissimo -> "av"
        | Accent -> "a>"
        | Tenuto -> "a-"
        | Marcato -> "a^"
        | LeftHandPizzicato -> "a+"
        | SnapPizzicato -> "ao"
        | OpenNote -> "ah"
        | UpFermata -> "a@a"
        | DownFermata -> "a@u"
        | BowUp -> "a|"
        | BowDown -> "am"

    /// Format an articulation position
    let formatArticulationPosition (pos: ArticulationPosition) =
        match pos with
        | Top -> "top"
        | Bottom -> "bottom"

    /// Format an articulation
    let formatArticulation (art: Articulation) =
        $"$.%s{formatArticulationType art.Type}/%s{formatArticulationPosition art.Position}.$"

    // ============================================================================
    // NOTE FORMATTERS
    // ============================================================================

    /// Format a standard note
    let formatStandardNote (note: StandardNote) =
        let letter = formatNoteLetter note.Letter
        let acc = note.Accidental |> Option.map formatAccidental |> Option.defaultValue ""
        let oct = string note.Octave
        let techs = formatTechniqueChain note.Techniques
        let art = note.Articulation |> Option.map formatArticulation |> Option.defaultValue ""
        $"%s{letter}%s{acc}/%s{oct}%s{techs}%s{art}"

    /// Format a tab note
    let formatTabNote (note: TabNote) =
        let fret = formatFret note.Fret
        let str = string note.String
        let techs = formatTechniqueChain note.Techniques
        let art = note.Articulation |> Option.map formatArticulation |> Option.defaultValue ""
        $"%s{fret}/%s{str}%s{techs}%s{art}"

    /// Format a chord note
    let formatChordNote (note: ChordNote) =
        match note with
        | StandardChordNote sn -> formatStandardNote sn
        | TabChordNote tn -> formatTabNote tn

    /// Format a chord
    let formatChord (chord: Chord) =
        let notes = chord.Notes |> List.map formatChordNote |> String.concat "."
        let techs = formatTechniqueChain chord.Techniques
        $"(%s{notes})%s{techs}"

    /// Format a rest
    let formatRest (rest: Rest) =
        match rest.Position with
        | Some pos -> $"#%d{pos}#"
        | None -> "##"

    /// Format a bar line
    let formatBarLine (barType: BarLineType) =
        match barType with
        | Single -> "|"
        | Double -> "=||"
        | RepeatBegin -> "=|:"
        | RepeatEnd -> "=:|"
        | DoubleRepeat -> "=::"
        | EndBar -> "=|="

    /// Format a tuplet
    let formatTuplet (tuplet: Tuplet) =
        $"^%d{tuplet.Number}^"

    /// Format a text style
    let formatTextStyle (style: TextStyle) =
        match style with
        | Preset name -> $".%s{name}."
        | Custom (face, size, style) -> $".%s{face}-%d{size}-%s{style}."

    /// Format an annotation
    let formatAnnotation (ann: Annotation) =
        let style = ann.Style |> Option.map formatTextStyle |> Option.defaultValue ""
        $"$%s{style}%s{ann.Text}$"

    /// Format a note item
    let formatNoteItem (item: NoteItem) =
        match item with
        | StandardNoteItem note -> formatStandardNote note
        | TabNoteItem note -> formatTabNote note
        | ChordItem chord -> formatChord chord
        | RestItem rest -> formatRest rest
        | BarLine barType -> formatBarLine barType
        | TupletMarker tuplet -> formatTuplet tuplet
        | AnnotationItem ann -> formatAnnotation ann

    // ============================================================================
    // STAVE CONFIGURATION FORMATTERS
    // ============================================================================

    /// Format a clef type
    let formatClefType (clef: ClefType) =
        match clef with
        | Treble -> "treble"
        | Alto -> "alto"
        | Tenor -> "tenor"
        | Bass -> "bass"
        | Percussion -> "percussion"

    /// Format a key signature
    let formatKeySignature (key: KeySignature) =
        let root = formatNoteLetter key.Root
        let acc = key.Accidental |> Option.map formatAccidental |> Option.defaultValue ""
        let mode = key.Mode |> Option.defaultValue ""
        $"%s{root}%s{acc}%s{mode}"

    /// Format a time signature
    let formatTimeSignature (time: TimeSignature) =
        match time with
        | Numeric (num, denom) -> $"%d{num}/%d{denom}"
        | CommonTime -> "C"
        | CutTime -> "C|"

    /// Format a tuning
    let formatTuning (tuning: Tuning) =
        match tuning with
        | Tuning.Standard -> "standard"
        | Tuning.DropD -> "dropd"
        | Tuning.EFlat -> "eb"
        | Tuning.Custom notes ->
            notes
            |> List.map (fun (letter, acc, oct) ->
                let l = formatNoteLetter letter
                let a = acc |> Option.map formatAccidental |> Option.defaultValue ""
                let o = string oct
                $"%s{l}%s{a}/%s{o}")
            |> String.concat ","

    /// Format tabstave options
    let formatTabstaveOptions (opts: TabstaveOptions) =
        let sb = StringBuilder("tabstave")

        opts.Notation |> Option.iter (fun v -> sb.Append $" notation=%b{v}" |> ignore)
        opts.Tablature |> Option.iter (fun v -> sb.Append $" tablature=%b{v}" |> ignore)
        opts.Clef |> Option.iter (fun v -> sb.Append $" clef=%s{formatClefType v}" |> ignore)
        opts.Key |> Option.iter (fun v -> sb.Append $" key=%s{formatKeySignature v}" |> ignore)
        opts.Time |> Option.iter (fun v -> sb.Append $" time=%s{formatTimeSignature v}" |> ignore)
        opts.Tuning |> Option.iter (fun v -> sb.Append $" tuning=%s{formatTuning v}" |> ignore)

        sb.ToString()

    // ============================================================================
    // OPTIONS FORMATTERS
    // ============================================================================

    /// Format an option value
    let formatOptionValue (value: OptionValue) =
        match value with
        | NumberValue n -> string n
        | StringValue s -> s
        | BoolValue b -> if b then "true" else "false"

    /// Format an option
    let formatOption (opt: VexOption) =
        $"%s{opt.Name}=%s{formatOptionValue opt.Value}"

    /// Format options line
    let formatOptionsLine (opts: VexOption list) =
        let optStrs = opts |> List.map formatOption |> String.concat " "
        $"options %s{optStrs}"

    // ============================================================================
    // TEXT LINE FORMATTERS
    // ============================================================================

    /// Format a symbol
    let formatSymbol (sym: Symbol) =
        match sym with
        | Coda -> "#coda"
        | Segno -> "#segno"
        | Trill -> "#tr"
        | Forte -> "#f"
        | Piano -> "#p"
        | MezzoForte -> "#mf"
        | MezzoPiano -> "#mp"
        | Fortissimo -> "#ff"
        | Pianissimo -> "#pp"

    /// Format a text item
    let formatTextItem (item: TextItem) =
        match item with
        | TextString s -> s
        | TextBarLine barType -> formatBarLine barType
        | TextSymbol sym -> formatSymbol sym
        | PositionModifier pos -> $".%d{pos}"
        | FontModifier (face, size, style) -> $".font=%s{face}-%d{size}-%s{style}"
        | NewLine -> "++"

    /// Format a text line
    let formatTextLine (dur: Duration option, items: TextItem list) =
        let durStr = dur |> Option.map formatDuration |> Option.defaultValue ""
        let itemsStr = items |> List.map formatTextItem |> String.concat ","
        $"text%s{durStr} %s{itemsStr}"

    // ============================================================================
    // LINE FORMATTERS
    // ============================================================================

    /// Format a notes line
    let formatNotesLine (dur: Duration option, items: NoteItem list) =
        let durStr = dur |> Option.map formatDuration |> Option.defaultValue ""
        let itemsStr = items |> List.map formatNoteItem |> String.concat " "
        $"notes%s{durStr} %s{itemsStr}"

    /// Format a VexTab line
    let formatLine (line: VexTabLine) =
        match line with
        | OptionsLine opts -> formatOptionsLine opts
        | TabstaveLine opts -> formatTabstaveOptions opts
        | NotesLine (dur, items) -> formatNotesLine (dur, items)
        | TextLine (dur, items) -> formatTextLine (dur, items)
        | BlankLine -> ""

    /// Format a complete VexTab document
    let formatDocument (doc: VexTabDocument) =
        doc.Lines
        |> List.map formatLine
        |> String.concat "\n"

    // ============================================================================
    // PUBLIC API
    // ============================================================================

    /// Generate VexTab from a document
    let generate (doc: VexTabDocument) : string =
        formatDocument doc

    /// Generate VexTab from note items
    let generateNotes (items: NoteItem list) : string =
        formatNotesLine (None, items)

