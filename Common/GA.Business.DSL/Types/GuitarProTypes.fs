namespace GA.MusicTheory.DSL.Types

/// <summary>
/// Type definitions for Guitar Pro file format (.gp3, .gp4, .gp5, .gpx, .gp)
/// Guitar Pro is a popular tablature editor with binary file formats
/// </summary>
module GuitarProTypes =

    // ============================================================================
    // CORE TYPES
    // ============================================================================

    /// Guitar Pro file version
    type GuitarProVersion =
        | GP3
        | GP4
        | GP5
        | GPX  // Guitar Pro 6+
        | GP   // Guitar Pro 7+

    /// Note duration
    type NoteDuration =
        | Whole
        | Half
        | Quarter
        | Eighth
        | Sixteenth
        | ThirtySecond
        | SixtyFourth

    /// Note effect
    type NoteEffect =
        | Bend of int  // Bend semitones
        | Slide of slideType: string
        | Vibrato
        | Hammer
        | Pull
        | Trill of fret: int
        | Tremolo
        | PalmMute
        | LetRing
        | Harmonic of harmonicType: string
        | Grace of fret: int

    /// Beat effect
    type BeatEffect =
        | Accent
        | HeavyAccent
        | GhostNote
        | Staccato
        | Arpeggio of direction: string
        | Stroke of direction: string
        | PickStroke of direction: string
        | Rasgueado
        | Slap
        | Pop

    /// Note on a string
    type GuitarProNote =
        { String: int  // 1-based string number
          Fret: int
          Duration: NoteDuration
          IsTied: bool
          IsDead: bool
          IsGhost: bool
          Velocity: int  // 0-127
          Effects: NoteEffect list }

    /// Beat (vertical slice of notes)
    type Beat =
        { Notes: GuitarProNote list
          Duration: NoteDuration
          IsDotted: bool
          IsTuplet: bool
          TupletRatio: (int * int) option  // e.g., (3, 2) for triplet
          Effects: BeatEffect list
          Text: string option
          Chord: ChordDiagram option }

    /// Chord diagram
    and ChordDiagram =
        { Name: string
          Frets: int list  // -1 for muted, 0 for open
          Fingers: int list option  // 0-4 (0=thumb, 1-4=fingers)
          Barres: int list }

    /// Measure (bar)
    type Measure =
        { Beats: Beat list
          TimeSignature: (int * int) option  // (numerator, denominator)
          KeySignature: int option  // -7 to 7 (flats to sharps)
          Tempo: int option
          Marker: string option
          RepeatOpen: bool
          RepeatClose: int option  // Number of repeats
          AlternateEnding: int option }

    /// Track (instrument/voice)
    type Track =
        { Name: string
          Instrument: int  // MIDI instrument number
          Strings: int  // Number of strings
          Tuning: int list  // MIDI note numbers for each string
          Capo: int
          Color: (int * int * int) option  // RGB
          Measures: Measure list
          IsMuted: bool
          IsSolo: bool
          Volume: int  // 0-127
          Pan: int  // 0-127 (64=center)
          Channel: int }  // MIDI channel

    /// Song metadata
    type SongInfo =
        { Title: string option
          Subtitle: string option
          Artist: string option
          Album: string option
          Author: string option  // Tab author
          Copyright: string option
          Writer: string option  // Music writer
          Instructions: string option
          Comments: string list }

    /// Complete Guitar Pro document
    type GuitarProDocument =
        { Version: GuitarProVersion
          Info: SongInfo
          Tracks: Track list
          MasterVolume: int
          Tempo: int
          Key: int  // -7 to 7
          Octave: int }

    // ============================================================================
    // PARSING RESULT
    // ============================================================================

    /// Result of parsing a Guitar Pro file
    type GuitarProParseResult =
        | Success of GuitarProDocument
        | Error of string

    // ============================================================================
    // HELPER FUNCTIONS
    // ============================================================================

    /// Convert version enum to string
    let versionToString = function
        | GP3 -> "Guitar Pro 3"
        | GP4 -> "Guitar Pro 4"
        | GP5 -> "Guitar Pro 5"
        | GPX -> "Guitar Pro 6"
        | GP -> "Guitar Pro 7+"

    /// Convert duration to string
    let durationToString = function
        | Whole -> "1"
        | Half -> "1/2"
        | Quarter -> "1/4"
        | Eighth -> "1/8"
        | Sixteenth -> "1/16"
        | ThirtySecond -> "1/32"
        | SixtyFourth -> "1/64"

    /// Get MIDI note name from number
    let midiNoteToName (midiNote: int) =
        let noteNames = [| "C"; "C#"; "D"; "D#"; "E"; "F"; "F#"; "G"; "G#"; "A"; "A#"; "B" |]
        let octave = (midiNote / 12) - 1
        let note = noteNames.[midiNote % 12]
        $"%s{note}%d{octave}"

    /// Convert tuning to string
    let tuningToString (tuning: int list) =
        tuning
        |> List.map midiNoteToName
        |> String.concat ", "

    /// Standard guitar tuning (E2, A2, D3, G3, B3, E4)
    let standardGuitarTuning = [40; 45; 50; 55; 59; 64]

    /// Standard bass tuning (E1, A1, D2, G2)
    let standardBassTuning = [28; 33; 38; 43]

