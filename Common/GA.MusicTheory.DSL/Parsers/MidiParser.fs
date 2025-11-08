namespace GA.MusicTheory.DSL.Parsers

open GA.MusicTheory.DSL.Types.MidiTypes
open System
open System.IO

/// MIDI file parser using Melanchall.DryWetMidi library
module MidiParser =

    // ============================================================================
    // BINARY PARSING HELPERS
    // ============================================================================

    /// Read big-endian 16-bit integer
    let readInt16BE (reader: BinaryReader) : int =
        let b1 = reader.ReadByte() |> int
        let b2 = reader.ReadByte() |> int
        (b1 <<< 8) ||| b2

    /// Read big-endian 32-bit integer
    let readInt32BE (reader: BinaryReader) : int =
        let b1 = reader.ReadByte() |> int
        let b2 = reader.ReadByte() |> int
        let b3 = reader.ReadByte() |> int
        let b4 = reader.ReadByte() |> int
        (b1 <<< 24) ||| (b2 <<< 16) ||| (b3 <<< 8) ||| b4

    /// Read variable-length quantity (VLQ)
    let readVLQ (reader: BinaryReader) : int =
        let rec loop acc =
            let b = reader.ReadByte() |> int
            let value = (acc <<< 7) ||| (b &&& 0x7F)
            if (b &&& 0x80) = 0 then
                value
            else
                loop value
        loop 0

    /// Read MIDI string
    let readString (reader: BinaryReader) (length: int) : string =
        let bytes = reader.ReadBytes(length)
        System.Text.Encoding.ASCII.GetString(bytes)

    // ============================================================================
    // MIDI HEADER PARSING
    // ============================================================================

    /// Parse MIDI header chunk
    let parseHeader (reader: BinaryReader) : Result<MidiHeader, string> =
        try
            // Read "MThd" chunk type
            let chunkType = readString reader 4
            if chunkType <> "MThd" then
                Result.Error $"Invalid MIDI file: expected 'MThd', got '%s{chunkType}'"
            else
                // Read chunk length (should be 6)
                let chunkLength = readInt32BE reader
                if chunkLength <> 6 then
                    Result.Error $"Invalid header chunk length: %d{chunkLength}"
                else
                    // Read format (0, 1, or 2)
                    let format = readInt16BE reader
                    let midiFormat =
                        match format with
                        | 0 -> MidiFormat.SingleTrack
                        | 1 -> MidiFormat.MultiTrack
                        | 2 -> MidiFormat.MultiSong
                        | _ -> MidiFormat.MultiTrack // Default to multi-track

                    // Read track count
                    let trackCount = readInt16BE reader

                    // Read time division
                    let division = readInt16BE reader
                    let timeDivision =
                        if (division &&& 0x8000) = 0 then
                            TicksPerQuarterNote division
                        else
                            let fps = -(division >>> 8)
                            let ticksPerFrame = division &&& 0xFF
                            SMPTE (fps, ticksPerFrame)

                    Result.Ok {
                        Format = midiFormat
                        TrackCount = trackCount
                        TimeDivision = timeDivision
                    }
        with ex ->
            Result.Error $"Error parsing MIDI header: %s{ex.Message}"

    // ============================================================================
    // MIDI EVENT PARSING
    // ============================================================================

    /// Parse MIDI event
    let parseEvent (reader: BinaryReader) (runningStatus: byte ref) : Result<MidiEvent option, string> =
        try
            let deltaTime = readVLQ reader
            let statusByte = reader.ReadByte()
            
            // Handle running status
            let actualStatus =
                if (statusByte &&& 0x80uy) = 0uy then
                    reader.BaseStream.Seek(-1L, SeekOrigin.Current) |> ignore
                    !runningStatus
                else
                    runningStatus := statusByte
                    statusByte

            let eventType = actualStatus &&& 0xF0uy
            let channel = (actualStatus &&& 0x0Fuy) |> int

            match eventType with
            | 0x80uy -> // Note Off
                let note = reader.ReadByte() |> int
                let velocity = reader.ReadByte() |> int
                Result.Ok (Some (NoteOff { NoteNumber = note; Velocity = velocity; Channel = channel; DeltaTime = deltaTime }))
            
            | 0x90uy -> // Note On
                let note = reader.ReadByte() |> int
                let velocity = reader.ReadByte() |> int
                if velocity = 0 then
                    // Note On with velocity 0 is Note Off
                    Result.Ok (Some (NoteOff { NoteNumber = note; Velocity = 0; Channel = channel; DeltaTime = deltaTime }))
                else
                    Result.Ok (Some (NoteOn { NoteNumber = note; Velocity = velocity; Channel = channel; DeltaTime = deltaTime }))
            
            | 0xC0uy -> // Program Change
                let program = reader.ReadByte() |> int
                Result.Ok (Some (ProgramChange (channel, program)))
            
            | 0xB0uy -> // Control Change
                let controller = reader.ReadByte() |> int
                let value = reader.ReadByte() |> int
                Result.Ok (Some (ControlChange (channel, controller, value)))
            
            | 0xE0uy -> // Pitch Bend
                let lsb = reader.ReadByte() |> int
                let msb = reader.ReadByte() |> int
                let value = (msb <<< 7) ||| lsb
                Result.Ok (Some (PitchBend (channel, value)))
            
            | 0xFFuy -> // Meta Event
                let metaType = reader.ReadByte()
                let length = readVLQ reader
                
                match metaType with
                | 0x01uy -> // Text
                    let text = readString reader length
                    Result.Ok (Some (Text text))
                | 0x02uy -> // Copyright
                    let text = readString reader length
                    Result.Ok (Some (Copyright text))
                | 0x03uy -> // Track Name
                    let text = readString reader length
                    Result.Ok (Some (TrackName text))
                | 0x04uy -> // Instrument
                    let text = readString reader length
                    Result.Ok (Some (Instrument text))
                | 0x05uy -> // Lyric
                    let text = readString reader length
                    Result.Ok (Some (Lyric text))
                | 0x06uy -> // Marker
                    let text = readString reader length
                    Result.Ok (Some (Marker text))
                | 0x07uy -> // Cue Point
                    let text = readString reader length
                    Result.Ok (Some (CuePoint text))
                | 0x2Fuy -> // End of Track
                    Result.Ok (Some EndOfTrack)
                | 0x51uy -> // Tempo
                    if length = 3 then
                        let b1 = reader.ReadByte() |> int
                        let b2 = reader.ReadByte() |> int
                        let b3 = reader.ReadByte() |> int
                        let microsecondsPerQuarter = (b1 <<< 16) ||| (b2 <<< 8) ||| b3
                        let bpm = 60000000 / microsecondsPerQuarter
                        Result.Ok (Some (Tempo bpm))
                    else
                        reader.ReadBytes(length) |> ignore
                        Result.Ok None
                | 0x58uy -> // Time Signature
                    if length = 4 then
                        let numerator = reader.ReadByte() |> int
                        let denominatorPower = reader.ReadByte() |> int
                        let denominator = pown 2 denominatorPower
                        reader.ReadBytes(2) |> ignore // Skip clocks and 32nds
                        Result.Ok (Some (TimeSignature (numerator, denominator)))
                    else
                        reader.ReadBytes(length) |> ignore
                        Result.Ok None
                | 0x59uy -> // Key Signature
                    if length = 2 then
                        let sharpsFlats = reader.ReadByte() |> int8 |> int
                        let majorMinor = reader.ReadByte() = 0uy
                        Result.Ok (Some (KeySignature (sharpsFlats, majorMinor)))
                    else
                        reader.ReadBytes(length) |> ignore
                        Result.Ok None
                | _ ->
                    // Unknown meta event - skip
                    reader.ReadBytes(length) |> ignore
                    Result.Ok None
            
            | _ ->
                // Unknown event type - skip
                Result.Ok None

        with ex ->
            Result.Error $"Error parsing MIDI event: %s{ex.Message}"

    // ============================================================================
    // MIDI TRACK PARSING
    // ============================================================================

    /// Parse MIDI track chunk
    let parseTrack (reader: BinaryReader) (trackNumber: int) : Result<MidiTrack, string> =
        try
            // Read "MTrk" chunk type
            let chunkType = readString reader 4
            if chunkType <> "MTrk" then
                Result.Error $"Invalid track chunk: expected 'MTrk', got '%s{chunkType}'"
            else
                // Read chunk length
                let chunkLength = readInt32BE reader
                let endPosition = reader.BaseStream.Position + int64 chunkLength

                // Parse events
                let runningStatus = ref 0uy
                let rec parseEvents acc =
                    if reader.BaseStream.Position >= endPosition then
                        Result.Ok (List.rev acc)
                    else
                        match parseEvent reader runningStatus with
                        | Result.Ok (Some event) ->
                            if event = EndOfTrack then
                                Result.Ok (List.rev (event :: acc))
                            else
                                parseEvents (event :: acc)
                        | Result.Ok None ->
                            parseEvents acc
                        | Result.Error err ->
                            Result.Error err

                match parseEvents [] with
                | Result.Ok events ->
                    // Extract track name and instrument from events
                    let trackName =
                        events
                        |> List.tryPick (function TrackName name -> Some name | _ -> None)
                    
                    let instrument =
                        events
                        |> List.tryPick (function Instrument name -> Some name | _ -> None)

                    Result.Ok {
                        TrackNumber = trackNumber
                        TrackName = trackName
                        Instrument = instrument
                        Events = events
                    }
                | Result.Error err ->
                    Result.Error err
        with ex ->
            Result.Error $"Error parsing MIDI track: %s{ex.Message}"

    // ============================================================================
    // PUBLIC API
    // ============================================================================

    /// Parse a MIDI file from bytes
    let parseBytes (bytes: byte[]) : Result<MidiFile, string> =
        use stream = new MemoryStream(bytes)
        use reader = new BinaryReader(stream)

        match parseHeader reader with
        | Result.Error err -> Result.Error err
        | Result.Ok header ->
            // Parse all tracks
            let rec parseTracks trackNum acc =
                if trackNum >= header.TrackCount then
                    Result.Ok (List.rev acc)
                else
                    match parseTrack reader trackNum with
                    | Result.Ok track ->
                        parseTracks (trackNum + 1) (track :: acc)
                    | Result.Error err ->
                        Result.Error err

            match parseTracks 0 [] with
            | Result.Ok tracks ->
                Result.Ok { Header = header; Tracks = tracks }
            | Result.Error err ->
                Result.Error err

    /// Parse a MIDI file from file path
    let parseFile (filePath: string) : Result<MidiFile, string> =
        try
            let bytes = File.ReadAllBytes(filePath)
            parseBytes bytes
        with ex ->
            Result.Error $"Error reading MIDI file: %s{ex.Message}"

    /// Try to parse MIDI bytes
    let tryParseBytes bytes =
        match parseBytes bytes with
        | Result.Ok midiFile -> Some midiFile
        | Result.Error _ -> None

    /// Try to parse MIDI file
    let tryParseFile filePath =
        match parseFile filePath with
        | Result.Ok midiFile -> Some midiFile
        | Result.Error _ -> None

