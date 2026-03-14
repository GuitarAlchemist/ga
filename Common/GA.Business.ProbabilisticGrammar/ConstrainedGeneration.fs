namespace GA.Business.ProbabilisticGrammar

open System
open GA.Business.DSL.Types.GrammarTypes

/// Configuration for probabilistic progression generation
type GenerationConfig =
    { Style: string
      KeyRoot: char
      KeyMode: string
      MaxLength: int
      Temperature: float
      Seed: int option }

/// Structured intent types for constrained LLM output (IR layer)
type PerformanceIntent =
    { Style: string
      Tempo: int
      DynamicRange: string
      ArticulationHints: string list }

type HarmonyConstraintSpec =
    { Key: string
      Mode: string
      AvoidNotes: string list
      PreferredIntervals: string list
      VoiceLeadingMaxDistance: int }

type TechniquePlan =
    { Technique: string
      PositionRange: int * int
      StringSet: int list
      Difficulty: float }

module ConstrainedGeneration =

    let private defaultRng = Random()

    // -----------------------------------------------------------------------
    // Internal helpers
    // -----------------------------------------------------------------------

    let private letterToSemitone (c: char) =
        match c with
        | 'C' -> 0 | 'D' -> 2 | 'E' -> 4 | 'F' -> 5
        | 'G' -> 7 | 'A' -> 9 | 'B' -> 11 | _ -> 0

    let private semitoneToLetter (pc: int) =
        match ((pc % 12) + 12) % 12 with
        | 0 -> 'C' | 2 -> 'D' | 4 -> 'E' | 5 -> 'F'
        | 7 -> 'G' | 9 -> 'A' | 11 -> 'B'
        | _ -> 'C'  // enharmonic approximation

    let private buildChord (rootLetter: char) (quality: ChordQuality) : Chord =
        { Root       = { Letter = rootLetter; Accidental = None; Octave = None }
          Quality    = quality
          Extensions = []
          Duration   = None }

    // Diatonic qualities for major and minor key degrees
    let private majorQualities = [| Major; Minor; Minor; Major; Major7; Minor; Diminished |]
    let private minorQualities = [| Minor; Diminished; Major; Minor; Minor; Major; Major7  |]
    let private majorSemitones = [| 0; 2; 4; 5; 7; 9; 11 |]

    let private diatonicChords (keyRoot: char) (mode: string) : Chord array =
        let qualities = if mode = "minor" then minorQualities else majorQualities
        majorSemitones
        |> Array.mapi (fun i semitones ->
            let rootLetter = semitoneToLetter (letterToSemitone keyRoot + semitones)
            buildChord rootLetter qualities.[i])

    // -----------------------------------------------------------------------
    // Public generation functions
    // -----------------------------------------------------------------------

    /// Generate a chord progression guided by weighted grammar rules.
    /// Runs 20 random candidates and returns the one with the highest harmonic fitness score.
    let generateProgression
            (grammar: WeightedMusicRule list)
            (config: GenerationConfig)
            : Chord list =
        let rng =
            match config.Seed with
            | Some s -> Random(s)
            | None   -> defaultRng

        let chords  = diatonicChords config.KeyRoot config.KeyMode
        let keyNote = { Letter = config.KeyRoot; Accidental = None; Octave = None }

        let mutable best      = []
        let mutable bestScore = -1.0

        for _ in 1..20 do
            let length = max 2 (min config.MaxLength 8)
            let candidate =
                [ for _ in 1..length do
                    let idx =
                        match WeightedMusicRule.selectWeighted grammar rng with
                        | Some rule -> abs (rule.RuleId.GetHashCode()) % chords.Length
                        | None      -> rng.Next(chords.Length)
                    yield chords.[idx] ]
            let score = HarmonicFitness.chordProgressionFitness candidate keyNote
            if score > bestScore then
                bestScore <- score
                best <- candidate

        if best.IsEmpty then
            // Fallback: I-IV-V-I
            [ chords.[0]; chords.[3]; chords.[4]; chords.[0] ]
        else
            best

    /// Generate scale choices for improvising over a sequence of chord changes.
    /// Each chord gets the scale with the best coverage + style fitness.
    let generateScaleChoices
            (grammar: WeightedMusicRule list)
            (changes: (Chord * int) list)
            : Scale list =
        let scaleRules = grammar |> List.filter (fun r -> r.Source = ScaleGrammar)
        let style =
            scaleRules
            |> List.tryHead
            |> Option.bind (fun r -> r.MusicalContext)
            |> Option.defaultValue "jazz"

        let candidates =
            [ [0; 2; 4; 5; 7; 9; 11],  "major"
              [0; 2; 3; 5; 7; 8; 10],  "natural minor"
              [0; 2; 4; 7; 9],          "major pentatonic"
              [0; 3; 5; 7; 10],         "minor pentatonic"
              [0; 2; 4; 6; 7; 9; 11],  "lydian"
              [0; 2; 4; 5; 7; 9; 10],  "mixolydian"
              [0; 2; 3; 5; 7; 9; 10],  "dorian"
              [0; 1; 3; 5; 6; 8; 10],  "whole-half diminished" ]

        changes
        |> List.map (fun (chord, _beats) ->
            let chordIntervals =
                match chord.Quality with
                | Major     -> [0; 4; 7]
                | Minor     -> [0; 3; 7]
                | Dominant7 -> [0; 4; 7; 10]
                | Major7    -> [0; 4; 7; 11]
                | Minor7    -> [0; 3; 7; 10]
                | _         -> [0; 4; 7]

            let bestIntervals, bestName =
                candidates
                |> List.maxBy (fun (intervals, _) ->
                    HarmonicFitness.scaleChoiceFitness intervals chordIntervals style)

            { Root      = chord.Root
              Intervals = bestIntervals
              Name      = Some bestName })

    /// Search for ranked voicing pitch-class sets for a chord using grammar weights.
    /// Returns top-5 candidates scored by voicing fitness.
    let searchVoicings
            (grammar: WeightedMusicRule list)
            (chord: Chord)
            (maxFret: int)
            : int list list =
        let rootPc =
            match chord.Root.Letter with
            | 'C' -> 0 | 'D' -> 2 | 'E' -> 4 | 'F' -> 5
            | 'G' -> 7 | 'A' -> 9 | 'B' -> 11 | _ -> 0

        let chordIntervals =
            match chord.Quality with
            | Major     -> [0; 4; 7]
            | Minor     -> [0; 3; 7]
            | Dominant7 -> [0; 4; 7; 10]
            | Major7    -> [0; 4; 7; 11]
            | Minor7    -> [0; 3; 7; 10]
            | _         -> [0; 4; 7]

        let style =
            grammar
            |> List.filter (fun r -> r.Source = VoicingGrammar)
            |> List.tryHead
            |> Option.bind (fun r -> r.MusicalContext)
            |> Option.defaultValue "jazz"

        let ctx =
            { KeyRoot        = chord.Root.Letter
              KeyMode        = "major"
              Style          = style
              PreviousChord  = None }

        // Generate candidate voicings: stack chord tones in various register arrangements
        let voicings =
            [ for spread in 1..5 do
                for inversion in 0..(chordIntervals.Length - 1) do
                    let rotated =
                        let arr = chordIntervals |> Array.ofList
                        let len = arr.Length
                        [ for i in 0..(len - 1) -> arr.[(i + inversion) % len] ]
                    let voiced =
                        rotated
                        |> List.mapi (fun i interval ->
                            (rootPc + interval + i * spread * 2) % 12)
                    yield voiced ]

        voicings
        |> List.sortByDescending (fun v -> HarmonicFitness.voicingFitness v ctx)
        |> List.truncate 5
