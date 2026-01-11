namespace GA.MusicTheory.DSL.Parsers

/// <summary>
/// FParsec-based parser for Practice Routine DSL
/// Parses practice session definitions with exercises, timing, and skill progression
/// </summary>
module PracticeRoutineParser =

    open FParsec
    open GA.MusicTheory.DSL.Types.PracticeRoutineTypes

    // ============================================================================
    // BASIC PARSERS
    // ============================================================================

    /// Parse whitespace
    let ws = spaces

    /// Parse whitespace1 (at least one whitespace character)
    let ws1 = spaces1

    /// Parse integer
    let integer : Parser<int, unit> = pint32 .>> ws

    /// Parse identifier
    let identifier : Parser<string, unit> =
        let isIdentifierFirstChar c = isLetter c || c = '_'
        let isIdentifierChar c = isLetter c || isDigit c || c = '_'
        many1Satisfy2L isIdentifierFirstChar isIdentifierChar "identifier" .>> ws

    /// Parse quoted string
    let quotedString : Parser<string, unit> =
        between (pchar '"') (pchar '"') (manySatisfy (fun c -> c <> '"')) .>> ws

    /// Parse string literal (quoted or unquoted identifier)
    let stringLiteral : Parser<string, unit> =
        quotedString <|> identifier

    // ============================================================================
    // DURATION PARSERS
    // ============================================================================

    /// Parse duration
    let duration : Parser<Duration, unit> =
        pipe2 integer (choice [
            pstring "minutes" >>% Minutes
            pstring "min" >>% Minutes
            pstring "hours" >>% Hours
            pstring "hr" >>% Hours
            pstring "seconds" >>% Seconds
            pstring "sec" >>% Seconds
            pstring "%" >>% Percentage
        ]) (fun value unit -> unit value)

    // ============================================================================
    // SKILL LEVEL PARSERS
    // ============================================================================

    /// Parse skill level
    let skillLevel : Parser<SkillLevel, unit> =
        choice [
            pstring "beginner" >>% Beginner
            pstring "intermediate" >>% Intermediate
            pstring "advanced" >>% Advanced
            pstring "expert" >>% Expert
        ] .>> ws

    /// Parse difficulty level
    let difficultyLevel : Parser<DifficultyLevel, unit> =
        choice [
            pstring "very_easy" >>% VeryEasy
            pstring "easy" >>% Easy
            pstring "medium" >>% Medium
            pstring "hard" >>% Hard
            pstring "very_hard" >>% VeryHard
            pipe2 integer (pchar '%') (fun value _ -> PercentageDifficulty value)
        ] .>> ws

    // ============================================================================
    // MUSICAL ELEMENT PARSERS (simplified)
    // ============================================================================

    // ============================================================================
    // INTERNET CONTENT PARSERS
    // ============================================================================

    /// Parse URL specification
    let urlSpec : Parser<UrlSpec, unit> =
        choice [
            pstring "url" >>. quotedString |>> DirectUrl
            pstring "tab" >>. quotedString |>> TabUrl
            pstring "midi" >>. quotedString |>> MidiUrl
            pstring "gp" >>. quotedString |>> GuitarProUrl
            pstring "musicxml" >>. quotedString |>> MusicXmlUrl
        ] .>> ws

    /// Parse repository name
    let repositoryName : Parser<RepositoryName, unit> =
        choice [
            pstring "ultimate_guitar" >>% UltimateGuitar
            pstring "ug" >>% UltimateGuitar
            pstring "songsterr" >>% Songsterr
            pstring "musescore" >>% MuseScore
            pstring "imslp" >>% IMSLP
            pstring "freemidi" >>% FreeMidi
            pstring "github" >>% GitHub
            pstring "archive_org" >>% ArchiveOrg
            pstring "public_domain" >>% PublicDomainRepo
        ] .>> ws

    /// Parse tuning specification
    let tuningSpec : Parser<TuningSpec, unit> =
        choice [
            pstring "standard" >>% Standard
            pstring "drop_d" >>% DropD
            pstring "dadgad" >>% DADGAD
            pstring "open_g" >>% OpenG
            pstring "open_d" >>% OpenD
        ] .>> ws

    /// Parse license type
    let licenseType : Parser<LicenseType, unit> =
        choice [
            pstring "public_domain" >>% PublicDomain
            pstring "creative_commons" >>% CreativeCommons
            pstring "free" >>% Free
            pstring "educational_use" >>% EducationalUse
        ] .>> ws

    /// Parse search criteria (simplified)
    let searchCriteria : Parser<SearchCriteria, unit> =
        pchar '{' >>. ws >>.
        (many (choice [
            pstring "artist:" >>. quotedString .>> ws
            pstring "title:" >>. quotedString .>> ws
            pstring "genre:" >>. quotedString .>> ws
        ])) .>>
        pchar '}' .>> ws
        |>> (fun _ -> {
            Artist = None; Title = None; Genre = None; Difficulty = None
            Tuning = None; Capo = None; Tempo = None; Key = None
            Tags = []; License = None
        })

    /// Parse content source
    let contentSource : Parser<ContentSource, unit> =
        choice [
            pstring "from" >>. urlSpec |>> UrlSource
            pstring "from" >>. repositoryName .>>. opt searchCriteria |>> RepositorySource
            pstring "search" >>. searchCriteria .>>. opt (pstring "in" >>. repositoryName) |>> SearchSource
        ] .>> ws

    /// Parse musical content with internet support
    let musicalContent : Parser<MusicalContent, unit> =
        choice [
            pstring "chord:" >>. quotedString |>> ChordProgressionContent
            pstring "scale:" >>. quotedString |>> ScaleContent
            pstring "technique:" >>. quotedString |>> TechniqueContent
            pstring "song:" >>. quotedString .>>. opt contentSource |>> SongContent
            quotedString |>> TechniqueContent  // default to technique
        ]

    // ============================================================================
    // EXERCISE TYPE PARSERS
    // ============================================================================

    /// Parse exercise type
    let exerciseType : Parser<ExerciseType, unit> =
        choice [
            pstring "warmup" >>% Warmup; pstring "warm_up" >>% Warmup; pstring "warm-up" >>% Warmup
            pstring "technique" >>% Technique; pstring "tech" >>% Technique
            pstring "scales" >>% Scales; pstring "scale_practice" >>% Scales
            pstring "chords" >>% Chords; pstring "chord_practice" >>% Chords
            pstring "arpeggios" >>% Arpeggios; pstring "arp" >>% Arpeggios
            pstring "songs" >>% Songs; pstring "repertoire" >>% Songs; pstring "pieces" >>% Songs
            pstring "improvisation" >>% Improvisation; pstring "improv" >>% Improvisation; pstring "jam" >>% Improvisation
            pstring "ear_training" >>% EarTraining; pstring "ear" >>% EarTraining
            pstring "rhythm" >>% Rhythm; pstring "timing" >>% Rhythm
            pstring "reading" >>% Reading; pstring "sight_reading" >>% Reading
            pstring "theory" >>% Theory; pstring "music_theory" >>% Theory
            pstring "cooldown" >>% Cooldown; pstring "cool_down" >>% Cooldown; pstring "cool-down" >>% Cooldown
            pstring "stretching" >>% Stretching; pstring "flexibility" >>% Stretching
        ] .>> ws

    // ============================================================================
    // TEMPO AND TIMING PARSERS
    // ============================================================================

    /// Parse BPM specification
    let bpmSpec : Parser<TempoSpec, unit> =
        pipe2 integer (pstring "bpm") (fun bpm _ -> BPM bpm) .>> ws

    /// Parse tempo range
    let tempoRange : Parser<TempoSpec, unit> =
        pipe4 integer (pchar '-') integer (pstring "bpm")
              (fun start _ endBpm _ -> TempoRange (start, endBpm)) .>> ws

    /// Parse tempo progression
    let tempoProgression : Parser<TempoSpec, unit> =
        pipe4 (pstring "start" >>. integer) (pstring "increase_to") integer (pstring "bpm")
              (fun start _ endBpm _ -> TempoProgression (start, endBpm)) .>> ws

    /// Parse tempo specification
    let tempoSpec : Parser<TempoSpec, unit> =
        choice [
            attempt tempoProgression
            attempt tempoRange
            bpmSpec
        ]

    /// Parse time signature (simplified)
    let timeSignature : Parser<string, unit> =
        pipe4 (pstring "in") integer (pchar '/') integer
              (fun _ num _ den -> $"%d{num}/%d{den}") .>> ws

    /// Parse feel type
    let feelType : Parser<FeelType, unit> =
        choice [
            pstring "straight" >>% Straight; pstring "swing" >>% Swing
            pstring "shuffle" >>% Shuffle; pstring "latin" >>% Latin
            pstring "rock" >>% Rock; pstring "jazz" >>% Jazz
        ] .>> ws

    /// Parse feel specification
    let feelSpec : Parser<FeelType, unit> =
        pstring "with" >>. feelType

    /// Parse timing specification
    let timingSpec : Parser<TimingSpec, unit> =
        pipe3 (pstring "at" >>. tempoSpec) (opt timeSignature) (opt feelSpec)
              (fun tempo timeSig feel ->
                  { Tempo = tempo; TimeSignature = timeSig; Feel = feel })

    // ============================================================================
    // GOALS AND TRACKING PARSERS
    // ============================================================================

    /// Parse accuracy goal
    let accuracyGoal : Parser<Goal, unit> =
        pipe2 (pstring "accuracy") (integer .>> pchar '%')
              (fun _ percentage -> AccuracyGoal percentage) .>> ws

    /// Parse tempo goal
    let tempoGoal : Parser<Goal, unit> =
        pipe2 (pstring "tempo") (integer .>> pstring "bpm")
              (fun _ bpm -> TempoGoal bpm) .>> ws

    /// Parse consistency goal
    let consistencyGoal : Parser<Goal, unit> =
        pipe2 (pstring "consistency") (integer .>> pstring "reps")
              (fun _ reps -> ConsistencyGoal reps) .>> ws

    /// Parse technique goal
    let techniqueGoal : Parser<Goal, unit> =
        pipe2 (pstring "clean") identifier
              (fun _ technique -> TechniqueGoal technique) .>> ws

    /// Parse goal
    let goal : Parser<Goal, unit> =
        choice [
            accuracyGoal; tempoGoal; consistencyGoal; techniqueGoal
        ]

    /// Parse goals specification
    let goalsSpec : Parser<GoalsSpec, unit> =
        pstring "goals" >>. between (pchar '{') (pchar '}')
            (sepBy goal (pchar ',' .>> ws))
        |>> (fun goals -> { Goals = goals }) .>> ws

    /// Parse tracking item
    let trackingItem : Parser<TrackingItem, unit> =
        choice [
            pstring "accuracy" >>% Accuracy
            pstring "tempo" >>% Tempo
            pstring "consistency" >>% Consistency
            pstring "mistakes" >>% Mistakes
            pstring "improvement_rate" >>% ImprovementRate
            pstring "session_completion" >>% SessionCompletion
        ] .>> ws

    /// Parse tracking specification
    let trackingSpec : Parser<TrackingSpec, unit> =
        pstring "track" >>. between (pchar '{') (pchar '}')
            (sepBy trackingItem (pchar ',' .>> ws))
        |>> (fun items -> { Items = items }) .>> ws

    // ============================================================================
    // EXERCISE PARSERS
    // ============================================================================

    /// Parse difficulty specification
    let difficultySpec : Parser<DifficultyLevel, unit> =
        pstring "difficulty" >>. difficultyLevel

    /// Parse exercise
    let exercise : Parser<Exercise, unit> =
        pipe5 exerciseType duration (pchar ':' >>. quotedString)
              (opt timingSpec) (opt difficultySpec)
              (fun exType dur desc timing diff ->
                  { Type = exType; Duration = dur; Description = desc
                    Timing = timing; Difficulty = diff; Goals = None; Content = None
                    ContentSource = None; PracticeOptions = None })

    /// Parse exercise item (for now, just regular exercises)
    let exerciseItem : Parser<ExerciseItem, unit> =
        exercise |>> RegularExercise

    /// Parse exercise list
    let exerciseList : Parser<ExerciseItem list, unit> =
        many exerciseItem

    // ============================================================================
    // SESSION PARSERS
    // ============================================================================

    /// Parse session definition
    let sessionDefinition : Parser<PracticeSession, unit> =
        pipe5 (pstring "session" >>. stringLiteral) duration skillLevel
              (opt trackingSpec) (between (pchar '{') (pchar '}') exerciseList)
              (fun name dur skill tracking exercises ->
                  { Name = name; Duration = dur; SkillLevel = skill
                    Exercises = exercises; Tracking = tracking; Metadata = None })

    /// Parse practice routine
    let practiceRoutine : Parser<PracticeRoutine, unit> =
        sessionDefinition |>> Session

    // ============================================================================
    // PUBLIC API
    // ============================================================================

    /// Parse a practice routine string
    let parse input =
        match run (ws >>. practiceRoutine .>> eof) input with
        | Success (result, _, _) -> Result.Ok result
        | Failure (errorMsg, _, _) -> Result.Error errorMsg

    /// Parse a practice routine string and return a DslCommand
    let parseCommand input =
        parse input
        |> Result.map (fun routine -> GA.MusicTheory.DSL.Types.GrammarTypes.PracticeRoutineCommand $"%A{routine}")

    /// Try to parse a practice routine string
    let tryParse input =
        match parse input with
        | Result.Ok routine -> Some routine
        | Result.Error _ -> None

    /// Parse multiple practice routines from a string
    let parseMultiple input =
        match run (ws >>. many (practiceRoutine .>> ws) .>> eof) input with
        | Success (results, _, _) -> Result.Ok results
        | Failure (errorMsg, _, _) -> Result.Error errorMsg

    /// Validate practice routine syntax without full parsing
    let validateSyntax input =
        match parse input with
        | Result.Ok _ -> true
        | Result.Error _ -> false

    /// Get detailed parse error information
    let getParseError input =
        match run (ws >>. practiceRoutine .>> eof) input with
        | Success _ -> None
        | Failure (errorMsg, error, _) ->
            Some (errorMsg, error.Position.Line, error.Position.Column)
