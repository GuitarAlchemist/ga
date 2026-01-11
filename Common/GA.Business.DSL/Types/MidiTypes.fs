namespace GA.MusicTheory.DSL.Types

/// MIDI-specific types for guitar tab conversion
module MidiTypes =

    // ============================================================================
    // MIDI EVENT TYPES
    // ============================================================================

    /// MIDI note number (0-127)
    type MidiNoteNumber = int

    /// MIDI velocity (0-127)
    type Velocity = int

    /// MIDI channel (0-15)
    type Channel = int

    /// Delta time in ticks
    type DeltaTime = int

    /// MIDI note event
    type MidiNoteEvent =
        { NoteNumber: MidiNoteNumber
          Velocity: Velocity
          Channel: Channel
          DeltaTime: DeltaTime }

    /// MIDI event type
    type MidiEvent =
        | NoteOn of MidiNoteEvent
        | NoteOff of MidiNoteEvent
        | ProgramChange of Channel * int
        | ControlChange of Channel * int * int
        | PitchBend of Channel * int
        | Tempo of int
        | TimeSignature of int * int
        | KeySignature of int * bool // sharps/flats, major/minor
        | Text of string
        | Copyright of string
        | TrackName of string
        | Instrument of string
        | Lyric of string
        | Marker of string
        | CuePoint of string
        | EndOfTrack

    // ============================================================================
    // MIDI TRACK
    // ============================================================================

    /// MIDI track
    type MidiTrack =
        { TrackNumber: int
          TrackName: string option
          Instrument: string option
          Events: MidiEvent list }

    // ============================================================================
    // MIDI FILE
    // ============================================================================

    /// MIDI file format (0, 1, or 2)
    type MidiFormat =
        | SingleTrack = 0
        | MultiTrack = 1
        | MultiSong = 2

    /// MIDI time division
    type TimeDivision =
        | TicksPerQuarterNote of int
        | SMPTE of int * int // fps, ticks per frame

    /// MIDI file header
    type MidiHeader =
        { Format: MidiFormat
          TrackCount: int
          TimeDivision: TimeDivision }

    /// Complete MIDI file
    type MidiFile =
        { Header: MidiHeader
          Tracks: MidiTrack list }

    // ============================================================================
    // GUITAR-SPECIFIC MIDI TYPES
    // ============================================================================

    /// Guitar string (1-6 for standard tuning, 1-7 for 7-string, etc.)
    type GuitarString = int

    /// Fret number (0 = open string)
    type Fret = int

    /// Guitar tuning (MIDI note numbers for each string)
    type GuitarTuning =
        { StringCount: int
          Tuning: MidiNoteNumber list }

    /// Standard guitar tunings
    module StandardTunings =
        /// Standard 6-string tuning (E2, A2, D3, G3, B3, E4)
        let standard6String =
            { StringCount = 6
              Tuning = [40; 45; 50; 55; 59; 64] } // E2, A2, D3, G3, B3, E4

        /// Drop D tuning (D2, A2, D3, G3, B3, E4)
        let dropD =
            { StringCount = 6
              Tuning = [38; 45; 50; 55; 59; 64] } // D2, A2, D3, G3, B3, E4

        /// 7-string tuning (B1, E2, A2, D3, G3, B3, E4)
        let standard7String =
            { StringCount = 7
              Tuning = [35; 40; 45; 50; 55; 59; 64] } // B1, E2, A2, D3, G3, B3, E4

    /// Guitar position (string and fret)
    type GuitarPosition =
        { String: GuitarString
          Fret: Fret }

    /// MIDI note with guitar position
    type GuitarMidiNote =
        { NoteNumber: MidiNoteNumber
          Velocity: Velocity
          DeltaTime: DeltaTime
          Position: GuitarPosition option }

    // ============================================================================
    // CONVERSION TYPES
    // ============================================================================

    /// Options for MIDI to Tab conversion
    type MidiToTabOptions =
        { Tuning: GuitarTuning
          PreferredString: int option
          MaxFret: int
          MinFret: int
          PreferOpenStrings: bool }

    /// Default MIDI to Tab conversion options
    module DefaultMidiToTabOptions =
        let standard =
            { Tuning = StandardTunings.standard6String
              PreferredString = None
              MaxFret = 24
              MinFret = 0
              PreferOpenStrings = true }

    /// Options for Tab to MIDI conversion
    type TabToMidiOptions =
        { Tuning: GuitarTuning
          DefaultVelocity: Velocity
          DefaultTempo: int
          DefaultTimeSignature: int * int }

    /// Default Tab to MIDI conversion options
    module DefaultTabToMidiOptions =
        let standard =
            { Tuning = StandardTunings.standard6String
              DefaultVelocity = 100
              DefaultTempo = 120
              DefaultTimeSignature = (4, 4) }

    // ============================================================================
    // PITCH TO FRET MAPPING
    // ============================================================================

    /// Find all possible guitar positions for a MIDI note
    let findPositions (tuning: GuitarTuning) (maxFret: int) (noteNumber: MidiNoteNumber) : GuitarPosition list =
        tuning.Tuning
        |> List.mapi (fun stringIndex openNote ->
            let fret = noteNumber - openNote
            if fret >= 0 && fret <= maxFret then
                Some { String = stringIndex + 1; Fret = fret }
            else
                None)
        |> List.choose id

    /// Find the best guitar position for a MIDI note
    let findBestPosition
        (tuning: GuitarTuning)
        (options: MidiToTabOptions)
        (noteNumber: MidiNoteNumber)
        : GuitarPosition option =
        
        let positions = findPositions tuning options.MaxFret noteNumber
        
        match positions with
        | [] -> None
        | _ ->
            // Prefer open strings if option is set
            let openStringPos = positions |> List.tryFind (fun p -> p.Fret = 0)
            if options.PreferOpenStrings && openStringPos.IsSome then
                openStringPos
            else
                // Prefer specified string if option is set
                match options.PreferredString with
                | Some preferredString ->
                    positions
                    |> List.tryFind (fun p -> p.String = preferredString)
                    |> Option.orElse (Some (List.head positions))
                | None ->
                    // Default: prefer lower frets on higher strings
                    positions
                    |> List.sortBy (fun p -> p.Fret * 10 + (tuning.StringCount - p.String))
                    |> List.tryHead

    /// Convert MIDI note number to guitar position
    let midiNoteToPosition
        (tuning: GuitarTuning)
        (options: MidiToTabOptions)
        (noteNumber: MidiNoteNumber)
        : GuitarPosition option =
        findBestPosition tuning options noteNumber

    /// Convert guitar position to MIDI note number
    let positionToMidiNote (tuning: GuitarTuning) (position: GuitarPosition) : MidiNoteNumber =
        let stringIndex = position.String - 1
        if stringIndex >= 0 && stringIndex < tuning.Tuning.Length then
            tuning.Tuning.[stringIndex] + position.Fret
        else
            0 // Invalid position

    // ============================================================================
    // HELPER FUNCTIONS
    // ============================================================================

    /// Get note name from MIDI note number
    let noteNumberToName (noteNumber: MidiNoteNumber) : string =
        let noteNames = [| "C"; "C#"; "D"; "D#"; "E"; "F"; "F#"; "G"; "G#"; "A"; "A#"; "B" |]
        let octave = noteNumber / 12 - 1
        let note = noteNames.[noteNumber % 12]
        $"%s{note}%d{octave}"

    /// Get MIDI note number from note name
    let noteNameToNumber (noteName: string) : MidiNoteNumber option =
        let noteMap =
            Map.ofList [
                ("C", 0); ("C#", 1); ("Db", 1)
                ("D", 2); ("D#", 3); ("Eb", 3)
                ("E", 4)
                ("F", 5); ("F#", 6); ("Gb", 6)
                ("G", 7); ("G#", 8); ("Ab", 8)
                ("A", 9); ("A#", 10); ("Bb", 10)
                ("B", 11)
            ]
        
        if noteName.Length < 2 then None
        else
            let note = noteName.Substring(0, noteName.Length - 1)
            let octaveStr = noteName.Substring(noteName.Length - 1)
            match System.Int32.TryParse(octaveStr) with
            | true, octave ->
                noteMap
                |> Map.tryFind note
                |> Option.map (fun n -> n + (octave + 1) * 12)
            | false, _ -> None

    /// Check if MIDI note number is valid (0-127)
    let isValidNoteNumber (noteNumber: MidiNoteNumber) : bool =
        noteNumber >= 0 && noteNumber <= 127

    /// Check if velocity is valid (0-127)
    let isValidVelocity (velocity: Velocity) : bool =
        velocity >= 0 && velocity <= 127

    /// Check if channel is valid (0-15)
    let isValidChannel (channel: Channel) : bool =
        channel >= 0 && channel <= 15

