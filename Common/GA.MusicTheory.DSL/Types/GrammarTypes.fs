namespace GA.MusicTheory.DSL.Types

open System

/// <summary>
/// Core types for the Music Theory DSL grammar system
/// Adapted from TARS grammar system for music theory domain
/// </summary>
module GrammarTypes =

    // ============================================================================
    // GRAMMAR METADATA
    // ============================================================================

    /// Grammar metadata for versioning and tracking
    type GrammarMetadata =
        { Name: string
          Version: string
          Description: string
          Author: string
          Created: DateTime
          Modified: DateTime
          Tags: string list
          Hash: string option }

    /// Grammar source location
    type GrammarSource =
        | ExternalFile of path: string
        | InlineDefinition of content: string
        | EmbeddedResource of resourceName: string

    /// Grammar format
    type GrammarFormat =
        | EBNF
        | ANTLR
        | PEG
        | Custom of string

    // ============================================================================
    // PARSE RESULTS
    // ============================================================================

    // Note: We use FParsec's ParserResult and F#'s Result<'T, string> types
    // instead of custom parse result types

    /// Parse error with context
    type ParseError =
        { Message: string
          Position: int
          Line: int
          Column: int
          Context: string option }

    // ============================================================================
    // CHORD PROGRESSION AST
    // ============================================================================

    /// Note representation
    type Note =
        { Letter: char  // A-G
          Accidental: Accidental option
          Octave: int option }

    and Accidental =
        | Sharp
        | Flat
        | DoubleSharp
        | DoubleFlat
        | Natural

    /// Chord quality
    type ChordQuality =
        | Major
        | Minor
        | Diminished
        | Augmented
        | Dominant7
        | Major7
        | Minor7
        | Diminished7
        | HalfDiminished7
        | Augmented7
        | Sus2
        | Sus4
        | Add9
        | Major9
        | Minor9
        | Dominant9
        | Custom of string

    /// Chord extensions and alterations
    type ChordExtension =
        | Add of interval: int
        | Omit of interval: int
        | Alter of interval: int * alteration: Accidental
        | Slash of bassNote: Note

    /// Chord representation
    type Chord =
        { Root: Note
          Quality: ChordQuality
          Extensions: ChordExtension list
          Duration: Duration option }

    and Duration =
        | Whole
        | Half
        | Quarter
        | Eighth
        | Sixteenth
        | Dotted of Duration
        | Triplet of Duration
        | Beats of float

    /// Roman numeral chord
    type RomanNumeral =
        { Degree: int  // 1-7
          Quality: RomanQuality
          Accidental: Accidental option
          Extensions: ChordExtension list
          Duration: Duration option }

    and RomanQuality =
        | MajorRoman
        | MinorRoman
        | DiminishedRoman
        | AugmentedRoman
        | Dominant7Roman

    /// Chord progression
    type ChordProgression =
        { Chords: ProgressionChord list
          Key: Note option
          TimeSignature: TimeSignature option
          Tempo: int option
          Metadata: Map<string, string> }

    and ProgressionChord =
        | AbsoluteChord of Chord
        | RomanNumeralChord of RomanNumeral

    and TimeSignature =
        { Numerator: int
          Denominator: int }

    // ============================================================================
    // FRETBOARD NAVIGATION AST
    // ============================================================================

    /// Fretboard position
    type FretboardPosition =
        { String: int  // 1-based
          Fret: int    // 0 = open
          Finger: Finger option }

    and Finger =
        | Thumb
        | Index
        | Middle
        | Ring
        | Pinky

    /// CAGED shape
    type CAGEDShape =
        | C_Shape
        | A_Shape
        | G_Shape
        | E_Shape
        | D_Shape

    /// Navigation command
    type NavigationCommand =
        | GotoPosition of FretboardPosition
        | GotoShape of CAGEDShape * fret: int
        | Move of direction: Direction * distance: int
        | Slide of from: FretboardPosition * ``to``: FretboardPosition
        | FindNote of note: Note * constraints: SearchConstraints option
        | FindChord of chord: Chord * constraints: SearchConstraints option
        | NavigatePath of from: FretboardPosition * ``to``: FretboardPosition

    and Direction =
        | Up
        | Down
        | Left
        | Right

    and SearchConstraints =
        { MinFret: int option
          MaxFret: int option
          Strings: int list option
          PreferredShape: CAGEDShape option }

    // ============================================================================
    // SCALE TRANSFORMATION AST
    // ============================================================================

    /// Scale representation
    type Scale =
        { Root: Note
          Intervals: int list  // Semitone intervals from root
          Name: string option }

    /// Mode
    type Mode =
        | Ionian
        | Dorian
        | Phrygian
        | Lydian
        | Mixolydian
        | Aeolian
        | Locrian
        | MelodicMinor
        | HarmonicMinor
        | Custom of string

    /// Scale transformation
    type ScaleTransformation =
        | Transpose of semitones: int
        | Rotate of steps: int
        | Invert
        | Reflect of axis: Note option
        | ModalInterchange of mode: Mode
        | ParallelMode of mode: Mode
        | RelativeMode of mode: Mode
        | AlterDegree of degree: int * alteration: Accidental
        | AddInterval of interval: int
        | RemoveInterval of interval: int
        | Union of other: Scale
        | Intersection of other: Scale
        | Complement

    // ============================================================================
    // GROTHENDIECK OPERATIONS AST
    // ============================================================================

    /// Musical object (object in a category)
    type MusicalObject =
        | NoteObject of Note
        | ChordObject of Chord
        | ScaleObject of Scale
        | ProgressionObject of ChordProgression
        | VoicingObject of FretboardPosition list
        | SetClassObject of pitchClasses: int list

    /// Morphism (arrow in a category)
    type Morphism =
        | TransposeMorphism of semitones: int
        | InvertMorphism
        | RotateMorphism of steps: int
        | ReflectMorphism of axis: Note option
        | CustomMorphism of name: string

    /// Category name
    type CategoryName = string

    /// Functor name
    type FunctorName = string

    /// Natural transformation name
    type NatTransName = string

    /// Diagram name
    type DiagramName = string

    /// Sheaf name
    type SheafName = string

    /// Open set name
    type OpenSetName = string

    /// Functor definition
    type FunctorDef =
        { Name: FunctorName
          SourceCategory: CategoryName
          TargetCategory: CategoryName
          MorphismMappings: (string * MorphismExpression) list option }

    /// Morphism expression
    and MorphismExpression =
        | TransposeExpr of semitones: int
        | InvertExpr
        | RotateExpr of steps: int
        | ReflectExpr
        | CustomExpr of name: string

    /// Natural transformation component
    type NatTransComponent =
        { Object: MusicalObject
          Morphism: MorphismExpression }

    /// Natural transformation definition
    type NatTransDef =
        { Name: NatTransName
          SourceFunctor: FunctorName
          TargetFunctor: FunctorName
          Components: NatTransComponent list option }

    /// Diagram specification
    type DiagramSpec =
        | ObjectList of MusicalObject list
        | NamedDiagram of DiagramName

    /// Space specification for sheaves
    type SpaceSpec =
        | Fretboard
        | CircleOfFifths
        | Tonnetz
        | PitchClassSpace
        | CustomSpace of name: string

    /// Sheaf section
    type SheafSection =
        { OpenSet: OpenSetName
          Value: MusicalObject }

    /// Gluing rule
    type GluingRule =
        { OpenSet1: OpenSetName
          OpenSet2: OpenSetName
          Morphism: MorphismExpression }

    /// Sheaf definition
    type SheafDef =
        { Name: SheafName
          Space: SpaceSpec
          Sections: SheafSection list option }

    /// Grothendieck operation
    type GrothendieckOperation =
        // Category operations
        | TensorProduct of MusicalObject * MusicalObject
        | DirectSum of MusicalObject * MusicalObject
        | Product of MusicalObject list
        | Coproduct of MusicalObject list
        | Exponential of MusicalObject * MusicalObject
        // Functor operations
        | DefineFunctor of FunctorDef
        | ApplyFunctor of FunctorName * MusicalObject
        | ComposeFunctors of FunctorName list
        // Natural transformations
        | DefineNatTrans of NatTransDef
        | ApplyNatTrans of NatTransName * MusicalObject
        // Limit operations
        | Limit of DiagramSpec
        | Pullback of MusicalObject * MorphismExpression * MusicalObject
        | Equalizer of MorphismExpression * MorphismExpression
        // Colimit operations
        | Colimit of DiagramSpec
        | Pushout of MusicalObject * MorphismExpression * MusicalObject
        | Coequalizer of MorphismExpression * MorphismExpression
        // Topos operations
        | SubobjectClassifier of MusicalObject option
        | PowerObject of MusicalObject
        | InternalHom of MusicalObject * MusicalObject
        // Sheaf operations
        | DefineSheaf of SheafDef
        | SheafRestriction of SheafName * OpenSetName
        | SheafGluing of MusicalObject list * GluingRule list option

    // ============================================================================
    // DSL COMMAND
    // ============================================================================

    /// Top-level DSL command
    type DslCommand =
        | ChordProgressionCommand of ChordProgression
        | NavigationCommand of NavigationCommand
        | ScaleTransformCommand of Scale * ScaleTransformation list
        | GrothendieckCommand of GrothendieckOperation
        | PracticeRoutineCommand of string  // simplified for now
        | CompositeCommand of DslCommand list

    // ============================================================================
    // EXECUTION RESULT
    // ============================================================================

    /// Result of executing a DSL command
    type ExecutionResult =
        | ChordResult of Chord list
        | PositionResult of FretboardPosition list
        | ScaleResult of Scale
        | ObjectResult of MusicalObject
        | ErrorResult of error: string
        | MultiResult of ExecutionResult list

