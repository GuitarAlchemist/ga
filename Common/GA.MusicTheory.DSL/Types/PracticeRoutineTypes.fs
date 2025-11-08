namespace GA.MusicTheory.DSL.Types

open GA.MusicTheory.DSL.Types.GrammarTypes

/// <summary>
/// Type definitions for Practice Routine DSL
/// Supports structured practice sessions with exercises, timing, and skill progression
/// </summary>
module PracticeRoutineTypes =

    // ============================================================================
    // BASIC TYPES
    // ============================================================================

    /// Duration specification
    type Duration =
        | Minutes of int
        | Hours of int
        | Seconds of int
        | Percentage of int

    /// Skill level enumeration
    type SkillLevel =
        | Beginner
        | Intermediate
        | Advanced
        | Expert

    /// Difficulty level
    type DifficultyLevel =
        | VeryEasy
        | Easy
        | Medium
        | Hard
        | VeryHard
        | PercentageDifficulty of int

    // ============================================================================
    // EXERCISE TYPES
    // ============================================================================

    /// Exercise type classification
    type ExerciseType =
        | Warmup | Technique | Scales | Chords | Arpeggios
        | Songs | Improvisation | EarTraining | Rhythm
        | Reading | Theory | Cooldown | Stretching

    /// Tempo specification
    type TempoSpec =
        | BPM of int
        | TempoRange of int * int
        | TempoProgression of startBpm: int * endBpm: int

    /// Feel type for rhythm
    type FeelType =
        | Straight | Swing | Shuffle | Latin | Rock | Jazz

    /// Timing specification
    type TimingSpec = {
        Tempo: TempoSpec
        TimeSignature: string option  // simplified as string
        Feel: FeelType option
    }

    // ============================================================================
    // GOALS AND TRACKING
    // ============================================================================

    /// Practice goal types
    type Goal =
        | AccuracyGoal of int  // percentage
        | TempoGoal of int     // BPM
        | ConsistencyGoal of int // repetitions
        | TechniqueGoal of string // technique name

    /// Performance tracking items
    type TrackingItem =
        | Accuracy | Tempo | Consistency | Mistakes 
        | ImprovementRate | SessionCompletion

    /// Goals specification
    type GoalsSpec = {
        Goals: Goal list
    }

    /// Tracking specification  
    type TrackingSpec = {
        Items: TrackingItem list
    }

    // ============================================================================
    // INTERNET CONTENT LOADING
    // ============================================================================

    /// Content source specification
    type ContentSource =
        | UrlSource of UrlSpec
        | RepositorySource of RepositoryName * SearchCriteria option
        | SearchSource of SearchCriteria * RepositoryName option

    /// URL specification for different content types
    and UrlSpec =
        | DirectUrl of string
        | TabUrl of string
        | MidiUrl of string
        | GuitarProUrl of string
        | MusicXmlUrl of string

    /// Supported music repositories
    and RepositoryName =
        | UltimateGuitar | Songsterr | MuseScore | IMSLP
        | FreeMidi | GitHub | ArchiveOrg | PublicDomainRepo

    /// Search criteria for content discovery
    and SearchCriteria = {
        Artist: string option
        Title: string option
        Genre: string option
        Difficulty: DifficultyLevel option
        Tuning: TuningSpec option
        Capo: int option
        Tempo: int option
        Key: string option
        Tags: string list
        License: LicenseType option
    }

    /// Guitar tuning specifications
    and TuningSpec =
        | Standard | DropD | DADGAD | OpenG | OpenD
        | Custom of string list

    /// Content licensing types
    and LicenseType =
        | PublicDomain | CreativeCommons | Free | EducationalUse

    /// Content definition for exercises
    type ContentDefinition =
        | LocalContent of string              // local file path
        | RemoteContent of ContentSource      // internet source
        | GeneratedContent of GenerationSpec  // AI-generated content

    /// AI content generation specification
    and GenerationSpec = {
        Style: string option
        Progression: string option
        Scale: string option
        Length: int option  // in bars
        Complexity: DifficultyLevel option
        AiModel: string option
    }

    /// Practice options for enhanced control
    type PracticeOptions = {
        Loop: int option                    // number of repetitions
        SlowDown: int option               // percentage of original tempo
        Transpose: int option              // semitones to transpose
        Metronome: bool option
        BackingTrack: bool option
        VisualFeedback: bool option
        RecordPerformance: bool option
    }

    // ============================================================================
    // ENHANCED MUSICAL CONTENT
    // ============================================================================

    /// Enhanced musical content with internet capabilities
    type MusicalContent =
        | ChordProgressionContent of string
        | ScaleContent of string
        | TechniqueContent of string
        | SongContent of string * ContentSource option  // song with optional source
        | InternetContent of ContentDefinition          // internet-sourced content

    // ============================================================================
    // EXERCISE DEFINITION
    // ============================================================================

    /// Enhanced exercise definition with internet content support
    type Exercise = {
        Type: ExerciseType
        Duration: Duration
        Description: string
        Timing: TimingSpec option
        Difficulty: DifficultyLevel option
        Goals: GoalsSpec option
        Content: MusicalContent option
        ContentSource: ContentSource option      // internet content source
        PracticeOptions: PracticeOptions option  // enhanced practice controls
    }

    // ============================================================================
    // ADVANCED FEATURES
    // ============================================================================

    /// Comparison operator
    type ComparisonOp =
        | GreaterThan | LessThan | GreaterEqual | LessEqual | Equal | NotEqual

    /// Condition types for conditional exercises
    type Condition =
        | SkillCondition of SkillLevel * ComparisonOp
        | ProgressCondition of int * ComparisonOp  // percentage
        | TimeCondition of int * ComparisonOp      // minutes

    /// Conditional exercise
    type ConditionalExercise = {
        Condition: Condition
        ThenExercise: Exercise
        ElseExercise: Exercise option
    }

    /// Adjustment specification for adaptive exercises
    type AdjustmentSpec =
        | TempoAdjustment of int  // ±BPM
        | DifficultyAdjustment of int  // ±percentage
        | DurationAdjustment of int    // ±minutes

    /// Adaptive exercise
    type AdaptiveExercise = {
        Type: ExerciseType
        Duration: Duration
        Description: string
        StartDifficulty: DifficultyLevel
        Adjustment: AdjustmentSpec
    }

    /// Practice loop
    type PracticeLoop = {
        Repetitions: int
        Exercises: ExerciseItem list
    }

    /// Practice cycle duration
    and CycleDuration =
        | Days of int
        | Weeks of int
        | Months of int

    /// Practice cycle
    and PracticeCycle = {
        Duration: CycleDuration
        Exercises: ExerciseItem list
    }

    /// Exercise item (can be regular, conditional, adaptive, loop, or cycle)
    and ExerciseItem =
        | RegularExercise of Exercise
        | ConditionalExercise of ConditionalExercise
        | AdaptiveExercise of AdaptiveExercise
        | PracticeLoop of PracticeLoop
        | PracticeCycle of PracticeCycle

    // ============================================================================
    // SESSION DEFINITION
    // ============================================================================

    /// Metadata for practice sessions
    type Metadata = {
        Author: string option
        Created: string option
        Version: string option
        Tags: string list
        Description: string option
    }

    /// Practice session definition
    type PracticeSession = {
        Name: string
        Duration: Duration
        SkillLevel: SkillLevel
        Exercises: ExerciseItem list
        Tracking: TrackingSpec option
        Metadata: Metadata option
    }

    /// Top-level practice routine
    type PracticeRoutine =
        | Session of PracticeSession
        | Cycle of PracticeCycle

    // ============================================================================
    // RESULT TYPES
    // ============================================================================

    /// Parse result for practice routines
    type ParseResult<'T> = Result<'T, string>

    /// Practice routine parse result
    type PracticeRoutineResult = ParseResult<PracticeRoutine>
