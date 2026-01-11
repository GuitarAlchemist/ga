namespace GA.MusicTheory.DSL.Types

/// <summary>
/// Type definitions for ASCII Tab notation
/// </summary>
module AsciiTabTypes =

    // ============================================================================
    // BASIC TYPES
    // ============================================================================

    /// String name (E, B, G, D, A, E for standard tuning)
    type StringName =
        | HighE
        | B
        | G
        | D
        | A
        | LowE
        | LowB  // 7-string
        | LowFSharp  // 8-string

    /// Fret number or muted
    type Fret =
        | FretNumber of int
        | Muted
        | Open

    // ============================================================================
    // TECHNIQUES
    // ============================================================================

    /// Guitar techniques
    type Technique =
        | HammerOn of fromFret: Fret * toFret: Fret
        | PullOff of fromFret: Fret * toFret: Fret
        | SlideUp of fromFret: Fret * toFret: Fret
        | SlideDown of fromFret: Fret * toFret: Fret
        | Bend of fret: Fret * toFret: Fret option
        | BendRelease of fret: Fret * bendTo: Fret * releaseTo: Fret
        | Vibrato of fret: Fret
        | Harmonic of fret: Fret
        | ArtificialHarmonic of fret: Fret
        | PinchHarmonic of fret: Fret
        | Tap of fret: Fret
        | Trill of fret1: Fret * fret2: Fret
        | PreBend of fret: Fret
        | GhostNote of fret: Fret
        | DeadNote
        | Rake
        | Slap
        | Pop
        | DiveBomb of fret: Fret
        | Whammy of fret: Fret
        | PickScrape
        | Feedback

    /// Strum direction
    type StrumDirection =
        | Down
        | Up

    // ============================================================================
    // NOTE ELEMENTS
    // ============================================================================

    /// Note element (single note or technique)
    type NoteElement =
        | SimpleFret of Fret
        | TechniqueNote of Technique
        | Spacing of char

    /// Bar line type
    type BarLineType =
        | Single
        | Double
        | RepeatBegin
        | RepeatEnd
        | DoubleRepeat

    // ============================================================================
    // STRING LINE
    // ============================================================================

    /// String line content item
    type StringContentItem =
        | Note of NoteElement
        | BarLine of BarLineType
        | Space

    /// String line
    type StringLine = {
        StringName: StringName
        Content: StringContentItem list
    }

    // ============================================================================
    // ANNOTATIONS
    // ============================================================================

    /// Chord quality
    type ChordQuality =
        | Major
        | Minor
        | Diminished
        | Augmented
        | Suspended

    /// Chord name
    type ChordName = {
        Root: string
        Accidental: string option
        Quality: ChordQuality option
        Extension: int option
    }

    /// Time signature
    type TimeSignature = {
        Numerator: int
        Denominator: int
    }

    /// Tuning
    type Tuning = {
        Strings: StringName list
    }

    /// Section type
    type SectionType =
        | Intro
        | Verse
        | Chorus
        | Bridge
        | Solo
        | Outro
        | Custom of string

    /// Annotation
    type Annotation =
        | Chord of ChordName
        | Tempo of int
        | TimeSignature of TimeSignature
        | Capo of int
        | Tuning of Tuning
        | Section of SectionType
        | Repeat of int
        | PalmMute
        | LetRing
        | Tremolo
        | StrumPattern of StrumDirection

    // ============================================================================
    // RHYTHM NOTATION
    // ============================================================================

    /// Note duration
    type Duration =
        | Whole
        | Half
        | Quarter
        | Eighth
        | Sixteenth
        | ThirtySecond

    /// Rhythm modifier
    type RhythmModifier =
        | Triplet
        | Dotted
        | Rest

    // ============================================================================
    // STAFF STRUCTURE
    // ============================================================================

    /// Staff (collection of string lines)
    type Staff = {
        Lines: StringLine list
        StringCount: int
    }

    /// Measure (staff with optional annotations)
    type Measure = {
        Annotations: Annotation list
        Staff: Staff
    }

    // ============================================================================
    // DOCUMENT STRUCTURE
    // ============================================================================

    /// Header information
    type Header = {
        Title: string option
        Artist: string option
        Album: string option
        Author: string option
        Lines: string list
    }

    /// ASCII Tab document
    type AsciiTabDocument = {
        Header: Header option
        Measures: Measure list
    }

    // ============================================================================
    // CHORD DIAGRAM
    // ============================================================================

    /// Chord diagram
    type ChordDiagram = {
        Name: ChordName
        Frets: Fret list  // One per string
    }

    // ============================================================================
    // HELPER FUNCTIONS
    // ============================================================================

    /// Convert string name to string
    let stringNameToString = function
        | HighE -> "E"
        | B -> "B"
        | G -> "G"
        | D -> "D"
        | A -> "A"
        | LowE -> "E"
        | LowB -> "B"
        | LowFSharp -> "F#"

    /// Convert fret to string
    let fretToString = function
        | FretNumber n -> string n
        | Muted -> "x"
        | Open -> "0"

    /// Convert bar line type to string
    let barLineToString = function
        | Single -> "|"
        | Double -> "||"
        | RepeatBegin -> "|:"
        | RepeatEnd -> ":|"
        | DoubleRepeat -> ":||:"

    /// Get standard tuning
    let standardTuning = {
        Strings = [HighE; B; G; D; A; LowE]
    }

    /// Get 7-string tuning
    let sevenStringTuning = {
        Strings = [HighE; B; G; D; A; LowE; LowB]
    }

    /// Get 8-string tuning
    let eightStringTuning = {
        Strings = [HighE; B; G; D; A; LowE; LowB; LowFSharp]
    }

    /// Create empty staff
    let createEmptyStaff stringCount =
        let strings = 
            match stringCount with
            | 6 -> standardTuning.Strings
            | 7 -> sevenStringTuning.Strings
            | 8 -> eightStringTuning.Strings
            | _ -> standardTuning.Strings
        
        {
            Lines = strings |> List.map (fun name -> { StringName = name; Content = [] })
            StringCount = stringCount
        }

    /// Create empty measure
    let createEmptyMeasure stringCount = {
        Annotations = []
        Staff = createEmptyStaff stringCount
    }

    /// Create empty document
    let createEmptyDocument () = {
        Header = None
        Measures = []
    }

