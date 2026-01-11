namespace GA.MusicTheory.DSL.Types

/// <summary>
/// Type definitions for VexTab AST
/// Represents the parsed structure of VexTab notation
/// </summary>
module VexTabTypes =

    // ============================================================================
    // PRIMITIVES
    // ============================================================================

    /// Note letter (A-G)
    type NoteLetter = A | B | C | D | E | F | G

    /// Accidental
    type VexAccidental =
        | Sharp
        | DoubleSharp
        | Flat
        | DoubleFlat
        | Natural

    /// Octave number (0-9)
    type Octave = int

    /// Fret number or muted
    type Fret =
        | FretNumber of int
        | Muted

    /// String number (1-6 for standard guitar)
    type StringNumber = int

    // ============================================================================
    // STAVE CONFIGURATION
    // ============================================================================

    /// Clef type
    type ClefType =
        | Treble
        | Alto
        | Tenor
        | Bass
        | Percussion

    /// Key signature
    type KeySignature = {
        Root: NoteLetter
        Accidental: VexAccidental option
        Mode: string option  // "major" or "minor"
    }

    /// Time signature
    type TimeSignature =
        | Numeric of numerator: int * denominator: int
        | CommonTime
        | CutTime

    /// Tuning
    type Tuning =
        | Standard
        | DropD
        | EFlat
        | Custom of (NoteLetter * VexAccidental option * Octave) list

    /// Tabstave options
    type TabstaveOptions = {
        Notation: bool option
        Tablature: bool option
        Clef: ClefType option
        Key: KeySignature option
        Time: TimeSignature option
        Tuning: Tuning option
    }

    // ============================================================================
    // DURATIONS
    // ============================================================================

    /// Note duration
    type DurationCode =
        | Whole
        | Half
        | Quarter
        | Eighth
        | Sixteenth
        | ThirtySecond

    /// Duration with modifiers
    type Duration = {
        Code: DurationCode
        Dotted: bool
        SlashNotation: bool
    }

    // ============================================================================
    // TECHNIQUES
    // ============================================================================

    /// Guitar technique
    type Technique =
        | HammerOn of toFret: int
        | PullOff of toFret: int
        | Slide of toFret: int
        | Bend of toFret: int * release: int option
        | Vibrato of harsh: bool
        | Tap
        | Upstroke
        | Downstroke

    // ============================================================================
    // ARTICULATIONS
    // ============================================================================

    /// Articulation type
    type ArticulationType =
        | Staccato
        | Staccatissimo
        | Accent
        | Tenuto
        | Marcato
        | LeftHandPizzicato
        | SnapPizzicato
        | OpenNote
        | UpFermata
        | DownFermata
        | BowUp
        | BowDown

    /// Articulation position
    type ArticulationPosition = Top | Bottom

    /// Articulation
    type Articulation = {
        Type: ArticulationType
        Position: ArticulationPosition
    }

    // ============================================================================
    // NOTES
    // ============================================================================

    /// Standard notation note
    type StandardNote = {
        Letter: NoteLetter
        Accidental: VexAccidental option
        Octave: Octave
        Techniques: Technique list
        Articulation: Articulation option
    }

    /// Tablature note
    type TabNote = {
        Fret: Fret
        String: StringNumber
        Techniques: Technique list
        Articulation: Articulation option
    }

    /// Chord note (can be standard or tab)
    type ChordNote =
        | StandardChordNote of StandardNote
        | TabChordNote of TabNote

    /// Chord
    type Chord = {
        Notes: ChordNote list
        Techniques: Technique list
    }

    /// Rest
    type Rest = {
        Position: int option  // 0-9, bottom to top
    }

    /// Bar line type
    type BarLineType =
        | Single
        | Double
        | RepeatBegin
        | RepeatEnd
        | DoubleRepeat
        | EndBar

    /// Tuplet
    type Tuplet = {
        Number: int  // 3 for triplet, 5 for quintuplet, etc.
    }

    // ============================================================================
    // ANNOTATIONS
    // ============================================================================

    /// Text style
    type TextStyle =
        | Preset of string  // "big", "medium", "italic"
        | Custom of face: string * size: int * style: string

    /// Annotation
    type Annotation = {
        Style: TextStyle option
        Text: string
    }

    // ============================================================================
    // NOTE ITEMS
    // ============================================================================

    /// Note item (element in a note sequence)
    type NoteItem =
        | StandardNoteItem of StandardNote
        | TabNoteItem of TabNote
        | ChordItem of Chord
        | RestItem of Rest
        | BarLine of BarLineType
        | TupletMarker of Tuplet
        | AnnotationItem of Annotation

    // ============================================================================
    // TEXT LINES
    // ============================================================================

    /// Musical symbol
    type Symbol =
        | Coda
        | Segno
        | Trill
        | Forte
        | Piano
        | MezzoForte
        | MezzoPiano
        | Fortissimo
        | Pianissimo

    /// Text item
    type TextItem =
        | TextString of string
        | TextBarLine of BarLineType
        | TextSymbol of Symbol
        | PositionModifier of int
        | FontModifier of face: string * size: int * style: string
        | NewLine

    // ============================================================================
    // OPTIONS
    // ============================================================================

    /// Option value
    type OptionValue =
        | NumberValue of int
        | StringValue of string
        | BoolValue of bool

    /// Option
    type VexOption = {
        Name: string
        Value: OptionValue
    }

    // ============================================================================
    // LINES
    // ============================================================================

    /// VexTab line
    type VexTabLine =
        | OptionsLine of VexOption list
        | TabstaveLine of TabstaveOptions
        | NotesLine of Duration option * NoteItem list
        | TextLine of Duration option * TextItem list
        | BlankLine

    // ============================================================================
    // DOCUMENT
    // ============================================================================

    /// Complete VexTab document
    type VexTabDocument = {
        Lines: VexTabLine list
    }

    // ============================================================================
    // HELPER FUNCTIONS
    // ============================================================================

    /// Create a default tabstave with common settings
    let defaultTabstave = {
        Notation = Some true
        Tablature = Some true
        Clef = Some Treble
        Key = None
        Time = None
        Tuning = Some Standard
    }

    /// Create a default duration (quarter note)
    let defaultDuration = {
        Code = Quarter
        Dotted = false
        SlashNotation = false
    }

    /// Create a standard note
    let createStandardNote letter octave =
        {
            Letter = letter
            Accidental = None
            Octave = octave
            Techniques = []
            Articulation = None
        }

    /// Create a tab note
    let createTabNote fret string =
        {
            Fret = FretNumber fret
            String = string
            Techniques = []
            Articulation = None
        }

    /// Create a chord from notes
    let createChord notes =
        {
            Notes = notes
            Techniques = []
        }

    /// Create a rest
    let createRest () =
        {
            Position = None
        }

    /// Create an annotation
    let createAnnotation text =
        {
            Style = None
            Text = text
        }

