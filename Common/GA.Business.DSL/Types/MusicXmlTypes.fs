namespace GA.MusicTheory.DSL.Types

/// MusicXML-specific types for guitar tab conversion
module MusicXmlTypes =

    // ============================================================================
    // BASIC MUSIC TYPES
    // ============================================================================

    /// Note step (C, D, E, F, G, A, B)
    type Step =
        | C | D | E | F | G | A | B

    /// Accidental
    type Accidental =
        | Sharp
        | Flat
        | Natural
        | DoubleSharp
        | DoubleFlat

    /// Octave (0-9)
    type Octave = int

    /// Pitch
    type Pitch =
        { Step: Step
          Alter: int option // -2 to +2 (semitones)
          Octave: Octave }

    /// Note duration (in divisions)
    type Duration = int

    /// Note type (whole, half, quarter, eighth, etc.)
    type NoteType =
        | Whole
        | Half
        | Quarter
        | Eighth
        | Sixteenth
        | ThirtySecond
        | SixtyFourth
        | OneHundredTwentyEighth

    // ============================================================================
    // GUITAR-SPECIFIC TYPES
    // ============================================================================

    /// Guitar string number (1-6 for standard guitar)
    type StringNumber = int

    /// Fret number (0 = open string)
    type FretNumber = int

    /// Technical notation for guitar
    type Technical =
        | String of StringNumber
        | Fret of FretNumber
        | HammerOn
        | PullOff
        | Bend
        | Slide
        | Vibrato
        | Harmonic
        | PalmMute
        | LetRing
        | Tapping

    /// Articulation
    type Articulation =
        | Accent
        | StrongAccent
        | Staccato
        | Tenuto
        | DetachedLegato
        | Staccatissimo
        | Spiccato
        | Scoop
        | Plop
        | Doit
        | Falloff

    // ============================================================================
    // NOTE TYPES
    // ============================================================================

    /// Note element
    type Note =
        { Pitch: Pitch option // None for rest
          Duration: Duration
          Type: NoteType
          Dots: int
          Voice: int option
          Staff: int option
          Chord: bool // True if part of a chord
          Technical: Technical list
          Articulations: Articulation list
          Tie: TieType option }

    and TieType =
        | Start
        | Stop
        | Continue

    // ============================================================================
    // MEASURE TYPES
    // ============================================================================

    /// Time signature
    type TimeSignature =
        { Beats: int
          BeatType: int }

    /// Key signature
    type KeySignature =
        { Fifths: int // -7 to +7 (flats to sharps)
          Mode: string option } // "major" or "minor"

    /// Clef
    type Clef =
        | Treble
        | Bass
        | Alto
        | Tenor
        | Percussion
        | TAB

    /// Attributes (time, key, clef, etc.)
    type Attributes =
        { Divisions: int option
          Key: KeySignature option
          Time: TimeSignature option
          Staves: int option
          Clef: Clef option }

    /// Direction (tempo, dynamics, etc.)
    type Direction =
        | Tempo of int
        | Dynamic of string // "p", "f", "mf", etc.
        | Wedge of string // "crescendo", "diminuendo"
        | Words of string

    /// Measure element
    type MeasureElement =
        | NoteElement of Note
        | AttributesElement of Attributes
        | DirectionElement of Direction
        | Barline of string
        | Backup of Duration
        | Forward of Duration

    /// Measure
    type Measure =
        { Number: string
          Elements: MeasureElement list }

    // ============================================================================
    // PART TYPES
    // ============================================================================

    /// Part information
    type PartInfo =
        { Id: string
          Name: string option
          Abbreviation: string option
          Instrument: string option }

    /// Part
    type Part =
        { Info: PartInfo
          Measures: Measure list }

    // ============================================================================
    // SCORE TYPES
    // ============================================================================

    /// Work information
    type Work =
        { Title: string option
          Composer: string option
          Lyricist: string option
          Copyright: string option }

    /// Score
    type Score =
        { Work: Work option
          Parts: Part list }

    // ============================================================================
    // HELPER FUNCTIONS
    // ============================================================================

    /// Convert step to string
    let stepToString = function
        | C -> "C"
        | D -> "D"
        | E -> "E"
        | F -> "F"
        | G -> "G"
        | A -> "A"
        | B -> "B"

    /// Convert string to step
    let stringToStep = function
        | "C" -> Some C
        | "D" -> Some D
        | "E" -> Some E
        | "F" -> Some F
        | "G" -> Some G
        | "A" -> Some A
        | "B" -> Some B
        | _ -> None

    /// Convert accidental to string
    let accidentalToString = function
        | Sharp -> "sharp"
        | Flat -> "flat"
        | Natural -> "natural"
        | DoubleSharp -> "double-sharp"
        | DoubleFlat -> "double-flat"

    /// Convert string to accidental
    let stringToAccidental = function
        | "sharp" -> Some Sharp
        | "flat" -> Some Flat
        | "natural" -> Some Natural
        | "double-sharp" -> Some DoubleSharp
        | "double-flat" -> Some DoubleFlat
        | _ -> None

    /// Convert note type to string
    let noteTypeToString = function
        | Whole -> "whole"
        | Half -> "half"
        | Quarter -> "quarter"
        | Eighth -> "eighth"
        | Sixteenth -> "16th"
        | ThirtySecond -> "32nd"
        | SixtyFourth -> "64th"
        | OneHundredTwentyEighth -> "128th"

    /// Convert string to note type
    let stringToNoteType = function
        | "whole" -> Some Whole
        | "half" -> Some Half
        | "quarter" -> Some Quarter
        | "eighth" -> Some Eighth
        | "16th" -> Some Sixteenth
        | "32nd" -> Some ThirtySecond
        | "64th" -> Some SixtyFourth
        | "128th" -> Some OneHundredTwentyEighth
        | _ -> None

    /// Convert clef to string
    let clefToString = function
        | Treble -> "G"
        | Bass -> "F"
        | Alto -> "C"
        | Tenor -> "C"
        | Percussion -> "percussion"
        | TAB -> "TAB"

    /// Convert string to clef
    let stringToClef = function
        | "G" -> Some Treble
        | "F" -> Some Bass
        | "C" -> Some Alto
        | "percussion" -> Some Percussion
        | "TAB" -> Some TAB
        | _ -> None

    /// Get MIDI note number from pitch
    let pitchToMidiNote (pitch: Pitch) : int =
        let stepValue = match pitch.Step with
                        | C -> 0
                        | D -> 2
                        | E -> 4
                        | F -> 5
                        | G -> 7
                        | A -> 9
                        | B -> 11
        let alter = pitch.Alter |> Option.defaultValue 0
        (pitch.Octave + 1) * 12 + stepValue + alter

    /// Get pitch from MIDI note number
    let midiNoteToPitch (midiNote: int) : Pitch =
        let octave = midiNote / 12 - 1
        let pitchClass = midiNote % 12
        let (step, alter) = match pitchClass with
                            | 0 -> (C, 0)
                            | 1 -> (C, 1)
                            | 2 -> (D, 0)
                            | 3 -> (D, 1)
                            | 4 -> (E, 0)
                            | 5 -> (F, 0)
                            | 6 -> (F, 1)
                            | 7 -> (G, 0)
                            | 8 -> (G, 1)
                            | 9 -> (A, 0)
                            | 10 -> (A, 1)
                            | 11 -> (B, 0)
                            | _ -> (C, 0)
        { Step = step; Alter = Some alter; Octave = octave }

