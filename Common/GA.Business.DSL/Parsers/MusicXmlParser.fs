namespace GA.MusicTheory.DSL.Parsers

open GA.MusicTheory.DSL.Types.MusicXmlTypes
open System
open System.Xml
open System.Xml.Linq

/// MusicXML parser using System.Xml.Linq
module MusicXmlParser =

    // ============================================================================
    // XML HELPER FUNCTIONS
    // ============================================================================

    let xn name = XName.Get(name)

    let tryGetElement (name: string) (element: XElement) =
        element.Element(xn name) |> Option.ofObj

    let tryGetAttribute (name: string) (element: XElement) =
        element.Attribute(xn name) |> Option.ofObj |> Option.map (fun a -> a.Value)

    let tryGetValue (element: XElement) =
        if element = null then None
        else Some element.Value

    let tryParseInt (s: string) =
        match Int32.TryParse(s) with
        | true, i -> Some i
        | false, _ -> None

    // ============================================================================
    // PITCH PARSING
    // ============================================================================

    /// Parse pitch element
    let parsePitch (pitchElement: XElement) : Pitch option =
        try
            let stepElement = pitchElement.Element(xn "step")
            let alterElement = pitchElement.Element(xn "alter")
            let octaveElement = pitchElement.Element(xn "octave")

            match stepElement, octaveElement with
            | null, _ | _, null -> None
            | _ ->
                let step = stringToStep stepElement.Value
                let alter = alterElement |> Option.ofObj |> Option.bind (fun e -> tryParseInt e.Value)
                let octave = tryParseInt octaveElement.Value

                match step, octave with
                | Some s, Some o -> Some { Step = s; Alter = alter; Octave = o }
                | _ -> None
        with _ -> None

    // ============================================================================
    // TECHNICAL PARSING
    // ============================================================================

    /// Parse technical element
    let parseTechnical (technicalElement: XElement) : Technical list =
        try
            let elements = technicalElement.Elements()
            elements
            |> Seq.choose (fun e ->
                match e.Name.LocalName with
                | "string" -> tryParseInt e.Value |> Option.map Technical.String
                | "fret" -> tryParseInt e.Value |> Option.map Technical.Fret
                | "hammer-on" -> Some Technical.HammerOn
                | "pull-off" -> Some Technical.PullOff
                | "bend" -> Some Technical.Bend
                | "slide" -> Some Technical.Slide
                | "vibrato" -> Some Technical.Vibrato
                | "harmonic" -> Some Technical.Harmonic
                | "palm-mute" -> Some Technical.PalmMute
                | "let-ring" -> Some Technical.LetRing
                | "tap" -> Some Technical.Tapping
                | _ -> None)
            |> List.ofSeq
        with _ -> []

    // ============================================================================
    // NOTE PARSING
    // ============================================================================

    /// Parse note element
    let parseNote (noteElement: XElement) : Note option =
        try
            let pitchElement = noteElement.Element(xn "pitch")
            let restElement = noteElement.Element(xn "rest")
            let durationElement = noteElement.Element(xn "duration")
            let typeElement = noteElement.Element(xn "type")
            let chordElement = noteElement.Element(xn "chord")
            let technicalElement = noteElement.Element(xn "notations") |> Option.ofObj
                                   |> Option.bind (fun n -> n.Element(xn "technical") |> Option.ofObj)

            let pitch = if pitchElement <> null then parsePitch pitchElement else None
            let duration = durationElement |> Option.ofObj |> Option.bind (fun e -> tryParseInt e.Value)
            let noteType = typeElement |> Option.ofObj |> Option.bind (fun e -> stringToNoteType e.Value)
            let isChord = chordElement <> null
            let technical = technicalElement |> Option.map parseTechnical |> Option.defaultValue []

            match duration, noteType with
            | Some d, Some t ->
                Some {
                    Pitch = pitch
                    Duration = d
                    Type = t
                    Dots = 0 // TODO: Parse dots
                    Voice = None // TODO: Parse voice
                    Staff = None // TODO: Parse staff
                    Chord = isChord
                    Technical = technical
                    Articulations = [] // TODO: Parse articulations
                    Tie = None // TODO: Parse ties
                }
            | _ -> None
        with _ -> None

    // ============================================================================
    // ATTRIBUTES PARSING
    // ============================================================================

    /// Parse time signature
    let parseTimeSignature (timeElement: XElement) : TimeSignature option =
        try
            let beatsElement = timeElement.Element(xn "beats")
            let beatTypeElement = timeElement.Element(xn "beat-type")

            match beatsElement, beatTypeElement with
            | null, _ | _, null -> None
            | _ ->
                let beats = tryParseInt beatsElement.Value
                let beatType = tryParseInt beatTypeElement.Value

                match beats, beatType with
                | Some b, Some bt -> Some { Beats = b; BeatType = bt }
                | _ -> None
        with _ -> None

    /// Parse key signature
    let parseKeySignature (keyElement: XElement) : KeySignature option =
        try
            let fifthsElement = keyElement.Element(xn "fifths")
            let modeElement = keyElement.Element(xn "mode")

            match fifthsElement with
            | null -> None
            | _ ->
                let fifths = tryParseInt fifthsElement.Value
                let mode = modeElement |> Option.ofObj |> Option.map (fun e -> e.Value)

                match fifths with
                | Some f -> Some { Fifths = f; Mode = mode }
                | _ -> None
        with _ -> None

    /// Parse clef
    let parseClef (clefElement: XElement) : Clef option =
        try
            let signElement = clefElement.Element(xn "sign")
            match signElement with
            | null -> None
            | _ -> stringToClef signElement.Value
        with _ -> None

    /// Parse attributes element
    let parseAttributes (attributesElement: XElement) : Attributes =
        try
            let divisionsElement = attributesElement.Element(xn "divisions")
            let keyElement = attributesElement.Element(xn "key")
            let timeElement = attributesElement.Element(xn "time")
            let stavesElement = attributesElement.Element(xn "staves")
            let clefElement = attributesElement.Element(xn "clef")

            {
                Divisions = divisionsElement |> Option.ofObj |> Option.bind (fun e -> tryParseInt e.Value)
                Key = keyElement |> Option.ofObj |> Option.bind parseKeySignature
                Time = timeElement |> Option.ofObj |> Option.bind parseTimeSignature
                Staves = stavesElement |> Option.ofObj |> Option.bind (fun e -> tryParseInt e.Value)
                Clef = clefElement |> Option.ofObj |> Option.bind parseClef
            }
        with _ ->
            { Divisions = None; Key = None; Time = None; Staves = None; Clef = None }

    // ============================================================================
    // MEASURE PARSING
    // ============================================================================

    /// Parse measure element
    let parseMeasure (measureElement: XElement) : Measure option =
        try
            let number = measureElement.Attribute(xn "number") |> Option.ofObj |> Option.map (fun a -> a.Value)

            match number with
            | None -> None
            | Some n ->
                let elements =
                    measureElement.Elements()
                    |> Seq.choose (fun e ->
                        match e.Name.LocalName with
                        | "note" -> parseNote e |> Option.map NoteElement
                        | "attributes" -> Some (AttributesElement (parseAttributes e))
                        | "barline" -> Some (Barline "barline")
                        | "backup" ->
                            e.Element(xn "duration")
                            |> Option.ofObj
                            |> Option.bind (fun d -> tryParseInt d.Value)
                            |> Option.map Backup
                        | "forward" ->
                            e.Element(xn "duration")
                            |> Option.ofObj
                            |> Option.bind (fun d -> tryParseInt d.Value)
                            |> Option.map Forward
                        | _ -> None)
                    |> List.ofSeq

                Some { Number = n; Elements = elements }
        with _ -> None

    // ============================================================================
    // PART PARSING
    // ============================================================================

    /// Parse part element
    let parsePart (partElement: XElement) (partInfo: PartInfo) : Part option =
        try
            let measures =
                partElement.Elements(xn "measure")
                |> Seq.choose parseMeasure
                |> List.ofSeq

            Some { Info = partInfo; Measures = measures }
        with _ -> None

    /// Parse part-list to get part info
    let parsePartInfo (partListElement: XElement) : PartInfo list =
        try
            partListElement.Elements(xn "score-part")
            |> Seq.map (fun sp ->
                let id = sp.Attribute(xn "id") |> Option.ofObj |> Option.map (fun a -> a.Value) |> Option.defaultValue ""
                let name = sp.Element(xn "part-name") |> Option.ofObj |> Option.map (fun e -> e.Value)
                let abbr = sp.Element(xn "part-abbreviation") |> Option.ofObj |> Option.map (fun e -> e.Value)
                let inst = sp.Element(xn "score-instrument") |> Option.ofObj
                           |> Option.bind (fun i -> i.Element(xn "instrument-name") |> Option.ofObj)
                           |> Option.map (fun e -> e.Value)

                { Id = id; Name = name; Abbreviation = abbr; Instrument = inst })
            |> List.ofSeq
        with _ -> []

    // ============================================================================
    // SCORE PARSING
    // ============================================================================

    /// Parse work element
    let parseWork (workElement: XElement option) : Work option =
        try
            match workElement with
            | None -> None
            | Some w ->
                let title = w.Element(xn "work-title") |> Option.ofObj |> Option.map (fun e -> e.Value)
                Some { Title = title; Composer = None; Lyricist = None; Copyright = None }
        with _ -> None

    /// Parse score
    let parseScore (doc: XDocument) : Result<Score, string> =
        try
            let root = doc.Root
            if root = null then
                Result.Error "Invalid MusicXML: no root element"
            else
                let workElement = root.Element(xn "work") |> Option.ofObj
                let partListElement = root.Element(xn "part-list")

                if partListElement = null then
                    Result.Error "Invalid MusicXML: no part-list element"
                else
                    let partInfos = parsePartInfo partListElement

                    let parts =
                        root.Elements(xn "part")
                        |> Seq.choose (fun partElement ->
                            let id = partElement.Attribute(xn "id") |> Option.ofObj |> Option.map (fun a -> a.Value)
                            match id with
                            | None -> None
                            | Some partId ->
                                partInfos
                                |> List.tryFind (fun pi -> pi.Id = partId)
                                |> Option.bind (fun pi -> parsePart partElement pi))
                        |> List.ofSeq

                    Result.Ok {
                        Work = parseWork workElement
                        Parts = parts
                    }
        with ex ->
            Result.Error $"Error parsing MusicXML: %s{ex.Message}"

    // ============================================================================
    // PUBLIC API
    // ============================================================================

    /// Parse MusicXML from string
    let parse (xml: string) : Result<Score, string> =
        try
            let doc = XDocument.Parse(xml)
            parseScore doc
        with ex ->
            Result.Error $"Error parsing MusicXML: %s{ex.Message}"

    /// Parse MusicXML from file
    let parseFile (filePath: string) : Result<Score, string> =
        try
            let doc = XDocument.Load(filePath)
            parseScore doc
        with ex ->
            Result.Error $"Error reading MusicXML file: %s{ex.Message}"

    /// Try to parse MusicXML
    let tryParse xml =
        match parse xml with
        | Result.Ok score -> Some score
        | Result.Error _ -> None

    /// Try to parse MusicXML file
    let tryParseFile filePath =
        match parseFile filePath with
        | Result.Ok score -> Some score
        | Result.Error _ -> None

