namespace GA.MusicTheory.DSL.Parsers

open GA.MusicTheory.DSL.Types.GuitarProTypes
open System
open System.IO
open System.Text

/// <summary>
/// Parser for Guitar Pro binary file formats (.gp3, .gp4, .gp5, .gpx, .gp)
/// This is a simplified parser that extracts basic tablature information
/// Full Guitar Pro format is complex and proprietary
/// </summary>
module GuitarProParser =

    // ============================================================================
    // BINARY READING HELPERS
    // ============================================================================

    /// Read a byte from the stream
    let readByte (reader: BinaryReader) =
        reader.ReadByte()

    /// Read an integer (4 bytes, little-endian)
    let readInt (reader: BinaryReader) =
        reader.ReadInt32()

    /// Read a short (2 bytes, little-endian)
    let readShort (reader: BinaryReader) =
        reader.ReadInt16() |> int

    /// Read a boolean (1 byte)
    let readBool (reader: BinaryReader) =
        reader.ReadByte() <> 0uy

    /// Read a string with length prefix
    let readString (reader: BinaryReader) =
        try
            let length = readInt reader
            if length > 0 && length < 1000 then
                let bytes = reader.ReadBytes(length)
                Encoding.UTF8.GetString(bytes)
            else
                ""
        with
        | _ -> ""

    /// Read a byte-prefixed string
    let readByteString (reader: BinaryReader) =
        try
            let length = readByte reader |> int
            if length > 0 then
                let bytes = reader.ReadBytes(length)
                Encoding.UTF8.GetString(bytes)
            else
                ""
        with
        | _ -> ""

    // ============================================================================
    // VERSION DETECTION
    // ============================================================================

    /// Detect Guitar Pro version from file header
    let detectVersion (reader: BinaryReader) : GuitarProVersion option =
        try
            reader.BaseStream.Position <- 0L
            let versionString = readByteString reader

            match versionString with
            | s when s.Contains("FICHIER GUITAR PRO v3") -> Some GP3
            | s when s.Contains("FICHIER GUITAR PRO v4") -> Some GP4
            | s when s.Contains("FICHIER GUITAR PRO v5") -> Some GP5
            | s when s.Contains("FICHIER GUITAR PRO v6") -> Some GPX
            | s when s.Contains("FICHIER GUITAR PRO v7") -> Some GP
            | _ -> None
        with
        | _ -> None

    // ============================================================================
    // METADATA PARSING
    // ============================================================================

    /// Parse song information
    let parseSongInfo (reader: BinaryReader) : SongInfo =
        try
            { Title = Some (readString reader)
              Subtitle = Some (readString reader)
              Artist = Some (readString reader)
              Album = Some (readString reader)
              Author = Some (readString reader)
              Copyright = Some (readString reader)
              Writer = Some (readString reader)
              Instructions = Some (readString reader)
              Comments = [] }
        with
        | _ ->
            { Title = None
              Subtitle = None
              Artist = None
              Album = None
              Author = None
              Copyright = None
              Writer = None
              Instructions = None
              Comments = [] }

    // ============================================================================
    // NOTE/BEAT/MEASURE PARSING
    // ============================================================================

    /// Parse a note effect
    let parseNoteEffect (reader: BinaryReader) : NoteEffect option =
        try
            let effectType = readByte reader |> int
            match effectType with
            | 1 -> Some Vibrato
            | 2 -> Some (Slide "up")
            | 3 -> Some (Bend 2)  // Default 2 semitone bend
            | 4 -> Some Hammer
            | 5 -> Some Pull
            | 6 -> Some PalmMute
            | 7 -> Some LetRing
            | 8 -> Some (Harmonic "natural")
            | 9 ->
                let fret = readByte reader |> int
                Some (Grace fret)
            | _ -> None
        with
        | _ -> None

    /// Parse a beat effect
    let parseBeatEffect (reader: BinaryReader) : BeatEffect option =
        try
            let effectType = readByte reader |> int
            match effectType with
            | 1 -> Some Accent
            | 2 -> Some HeavyAccent
            | 3 -> Some GhostNote
            | 4 -> Some Staccato
            | 5 -> Some (Arpeggio "up")
            | 6 -> Some (Stroke "down")
            | 7 -> Some (PickStroke "down")
            | 8 -> Some Rasgueado
            | 9 -> Some Slap
            | 10 -> Some Pop
            | _ -> None
        with
        | _ -> None

    /// Parse a single note
    let parseNote (reader: BinaryReader) (stringNum: int) : GuitarProNote option =
        try
            let flags = readByte reader |> int
            if flags &&& 0x20 <> 0 then  // Note is present
                let fret = readByte reader |> int
                let velocity = if flags &&& 0x10 <> 0 then readByte reader |> int else 95

                // Parse note effects
                let effectCount = if flags &&& 0x08 <> 0 then readByte reader |> int else 0
                let effects =
                    [ for _ in 1..effectCount do
                        match parseNoteEffect reader with
                        | Some effect -> yield effect
                        | None -> () ]

                Some {
                    String = stringNum
                    Fret = fret
                    Duration = NoteDuration.Quarter  // Will be overridden by beat duration
                    IsTied = flags &&& 0x02 <> 0
                    IsDead = fret = 255
                    IsGhost = flags &&& 0x04 <> 0
                    Velocity = velocity
                    Effects = effects
                }
            else
                None
        with
        | _ -> None

    /// Parse a beat
    let parseBeat (reader: BinaryReader) (numStrings: int) : Beat option =
        try
            let flags = readByte reader |> int

            // Parse duration
            let durationValue = readByte reader |> int
            let duration =
                match durationValue with
                | 0 -> NoteDuration.Whole
                | 1 -> NoteDuration.Half
                | 2 -> NoteDuration.Quarter
                | 3 -> NoteDuration.Eighth
                | 4 -> NoteDuration.Sixteenth
                | 5 -> NoteDuration.ThirtySecond
                | 6 -> NoteDuration.SixtyFourth
                | _ -> NoteDuration.Quarter

            let isDotted = flags &&& 0x01 <> 0
            let isTuplet = flags &&& 0x20 <> 0
            let tupletRatio = if isTuplet then Some (3, 2) else None

            // Parse notes for each string
            let notes =
                [ for stringNum in 1..numStrings do
                    match parseNote reader stringNum with
                    | Some note -> yield { note with Duration = duration }
                    | None -> () ]

            // Parse beat effects
            let effectCount = if flags &&& 0x08 <> 0 then readByte reader |> int else 0
            let effects =
                [ for _ in 1..effectCount do
                    match parseBeatEffect reader with
                    | Some effect -> yield effect
                    | None -> () ]

            // Parse text/chord
            let text = if flags &&& 0x04 <> 0 then Some (readByteString reader) else None

            Some {
                Notes = notes
                Duration = duration
                IsDotted = isDotted
                IsTuplet = isTuplet
                TupletRatio = tupletRatio
                Effects = effects
                Text = text
                Chord = None  // Simplified - not parsing chord diagrams yet
            }
        with
        | _ -> None

    /// Parse a measure
    let parseMeasure (reader: BinaryReader) (numStrings: int) : Measure option =
        try
            let flags = readByte reader |> int

            // Parse time signature
            let timeSignature =
                if flags &&& 0x01 <> 0 then
                    let numerator = readByte reader |> int
                    let denominator = readByte reader |> int
                    Some (numerator, denominator)
                else
                    None

            // Parse key signature
            let keySignature = if flags &&& 0x02 <> 0 then Some (readByte reader |> int) else None

            // Parse tempo
            let tempo = if flags &&& 0x04 <> 0 then Some (readInt reader) else None

            // Parse marker
            let marker = if flags &&& 0x08 <> 0 then Some (readByteString reader) else None

            // Parse repeat info
            let repeatOpen = flags &&& 0x10 <> 0
            let repeatClose = if flags &&& 0x20 <> 0 then Some (readByte reader |> int) else None
            let alternateEnding = if flags &&& 0x40 <> 0 then Some (readByte reader |> int) else None

            // Parse beats
            let beatCount = readByte reader |> int
            let beats =
                [ for _ in 1..beatCount do
                    match parseBeat reader numStrings with
                    | Some beat -> yield beat
                    | None -> () ]

            Some {
                Beats = beats
                TimeSignature = timeSignature
                KeySignature = keySignature
                Tempo = tempo
                Marker = marker
                RepeatOpen = repeatOpen
                RepeatClose = repeatClose
                AlternateEnding = alternateEnding
            }
        with
        | _ -> None

    // ============================================================================
    // TRACK PARSING
    // ============================================================================

    /// Parse a track with full measure/beat/note structure
    let parseTrack (reader: BinaryReader) (trackNumber: int) (measureCount: int) : Track =
        try
            // Parse track header
            let flags = readByte reader |> int
            let name = if flags &&& 0x01 <> 0 then readByteString reader else $"Track %d{trackNumber}"
            let strings = readByte reader |> int

            // Parse tuning
            let tuning =
                if flags &&& 0x02 <> 0 then
                    [ for _ in 1..strings do yield readByte reader |> int ]
                else
                    standardGuitarTuning

            // Parse other properties
            let instrument = if flags &&& 0x04 <> 0 then readByte reader |> int else 25
            let capo = if flags &&& 0x08 <> 0 then readByte reader |> int else 0
            let color =
                if flags &&& 0x10 <> 0 then
                    let r = readByte reader |> int
                    let g = readByte reader |> int
                    let b = readByte reader |> int
                    Some (r, g, b)
                else
                    None

            let isMuted = flags &&& 0x20 <> 0
            let isSolo = flags &&& 0x40 <> 0
            let volume = if flags &&& 0x80 <> 0 then readByte reader |> int else 100
            let pan = readByte reader |> int

            // Parse measures
            let measures =
                [ for _ in 1..measureCount do
                    match parseMeasure reader strings with
                    | Some measure -> yield measure
                    | None -> () ]

            { Name = name
              Instrument = instrument
              Strings = strings
              Tuning = tuning
              Capo = capo
              Color = color
              Measures = measures
              IsMuted = isMuted
              IsSolo = isSolo
              Volume = volume
              Pan = pan
              Channel = trackNumber }
        with
        | ex ->
            // Fallback to simplified track
            { Name = $"Track %d{trackNumber}"
              Instrument = 25
              Strings = 6
              Tuning = standardGuitarTuning
              Capo = 0
              Color = None
              Measures = []
              IsMuted = false
              IsSolo = false
              Volume = 100
              Pan = 64
              Channel = trackNumber }

    // ============================================================================
    // MAIN PARSING FUNCTION
    // ============================================================================

    /// Parse a Guitar Pro file from a byte array
    let parseBytes (bytes: byte[]) : GuitarProParseResult =
        try
            use stream = new MemoryStream(bytes)
            use reader = new BinaryReader(stream)

            // Detect version
            match detectVersion reader with
            | None -> Error "Unable to detect Guitar Pro version"
            | Some version ->
                // Reset stream position
                reader.BaseStream.Position <- 0L

                // Skip version string
                let _ = readByteString reader

                // Parse song info
                let info = parseSongInfo reader

                // Parse global settings
                let masterVolume = try readByte reader |> int with | _ -> 100
                let tempo = try readInt reader with | _ -> 120
                let key = try readByte reader |> int with | _ -> 0
                let octave = try readByte reader |> int with | _ -> 0

                // Parse track count and measure count
                let trackCount = try readByte reader |> int with | _ -> 1
                let measureCount = try readByte reader |> int with | _ -> 0

                // Parse tracks with full measure/beat/note structure
                let tracks =
                    [ for trackNum in 1..trackCount do
                        yield parseTrack reader trackNum measureCount ]

                let doc =
                    { Version = version
                      Info = info
                      Tracks = tracks
                      MasterVolume = masterVolume
                      Tempo = tempo
                      Key = key
                      Octave = octave }

                Success doc
        with
        | ex -> Error $"Error parsing Guitar Pro file: %s{ex.Message}"

    /// Parse a Guitar Pro file from a file path
    let parseFile (filePath: string) : GuitarProParseResult =
        try
            let bytes = File.ReadAllBytes(filePath)
            parseBytes bytes
        with
        | ex -> Error $"Error reading file: %s{ex.Message}"

    /// Parse a Guitar Pro file from a string (base64 encoded)
    let parse (content: string) : GuitarProParseResult =
        try
            let bytes = Convert.FromBase64String(content)
            parseBytes bytes
        with
        | ex -> Error $"Error decoding Guitar Pro content: %s{ex.Message}"

    // ============================================================================
    // CONVERSION TO ASCII TAB
    // ============================================================================

    /// Convert a beat to ASCII tab representation
    let beatToAscii (beat: Beat) (numStrings: int) : string list =
        // Create a list of fret positions for each string
        let frets = Array.create numStrings "-"

        // Fill in the frets from the notes
        for note in beat.Notes do
            if note.String >= 1 && note.String <= numStrings then
                let fretStr =
                    if note.IsDead then "x"
                    elif note.IsGhost then $"(%d{note.Fret})"
                    else string note.Fret
                frets.[note.String - 1] <- fretStr

        Array.toList frets

    /// Convert Guitar Pro document to ASCII tab
    let toAsciiTab (doc: GuitarProDocument) : string =
        let sb = StringBuilder()

        // Add title
        match doc.Info.Title with
        | Some title -> sb.AppendLine $"Title: %s{title}" |> ignore
        | None -> ()

        // Add artist
        match doc.Info.Artist with
        | Some artist -> sb.AppendLine $"Artist: %s{artist}" |> ignore
        | None -> ()

        // Add tempo
        sb.AppendLine $"Tempo: %d{doc.Tempo} BPM" |> ignore
        sb.AppendLine() |> ignore

        // Add track information
        for track in doc.Tracks do
            sb.AppendLine $"Track: %s{track.Name}" |> ignore
            sb.AppendLine $"Tuning: %s{tuningToString track.Tuning}" |> ignore
            if track.Capo > 0 then
                sb.AppendLine $"Capo: %d{track.Capo}" |> ignore
            sb.AppendLine() |> ignore

            // Convert measures to ASCII tab
            for measureIdx, measure in track.Measures |> List.indexed do
                // Add measure marker if present
                match measure.Marker with
                | Some marker -> sb.AppendLine $"[%s{marker}]" |> ignore
                | None -> ()

                // Add time signature if present
                match measure.TimeSignature with
                | Some (num, denom) -> sb.AppendLine $"Time: %d{num}/%d{denom}" |> ignore
                | None -> ()

                // Add tempo if present
                match measure.Tempo with
                | Some tempo -> sb.AppendLine $"Tempo: %d{tempo}" |> ignore
                | None -> ()

                if measure.Beats.Length > 0 then
                    // Create tab lines for each string
                    let stringNames =
                        match track.Strings with
                        | 6 -> ["e"; "B"; "G"; "D"; "A"; "E"]
                        | 7 -> ["e"; "B"; "G"; "D"; "A"; "E"; "B"]
                        | 4 -> ["G"; "D"; "A"; "E"]
                        | _ -> List.init track.Strings (fun i -> $"S%d{i + 1}")

                    // Build tab lines
                    let tabLines = Array.create track.Strings (StringBuilder())

                    for beat in measure.Beats do
                        let frets = beatToAscii beat track.Strings
                        for stringIdx in 0..(track.Strings - 1) do
                            let fret = frets.[stringIdx]
                            let padded = fret.PadRight(3, '-')
                            tabLines.[stringIdx].Append(padded) |> ignore

                    // Output tab lines
                    for stringIdx in 0..(track.Strings - 1) do
                        sb.AppendLine $"%s{stringNames.[stringIdx]}|%s{tabLines.[stringIdx].ToString()}|" |> ignore

                    sb.AppendLine() |> ignore

        sb.ToString()

