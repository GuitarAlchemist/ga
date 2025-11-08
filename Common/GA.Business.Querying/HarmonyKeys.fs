namespace GA.Business.Querying

open System
open GA.Business.Core.Notes
open GA.Business.Core.Atonal
open GA.Business.Core.Chords
open GA.Business.Core.Tonal

/// <summary>
/// Functional harmony analysis module using F# discriminated unions and pattern matching
/// </summary>
[<AutoOpen>]
module HarmonyKeys =
    
    /// Represents different types of harmonic functions
    type HarmonicFunction =
        | Tonic of PitchClass
        | Subdominant of PitchClass
        | Dominant of PitchClass
        | Mediant of PitchClass
        | Submediant of PitchClass
        | Supertonic of PitchClass
        | LeadingTone of PitchClass
        | Chromatic of PitchClass
        
    /// Represents chord quality in functional terms
    type ChordQuality =
        | Major
        | Minor
        | Diminished
        | Augmented
        | Dominant7
        | Major7
        | Minor7
        | HalfDiminished7
        | FullyDiminished7
        | Extended of int * ChordQuality // degree and base quality
        
    /// Represents a functional chord with context
    type FunctionalChord = {
        Function: HarmonicFunction
        Quality: ChordQuality
        Root: PitchClass
        PitchClassSet: PitchClassSet
        Inversions: int option
        Extensions: int list
    }
    
    /// Represents tonal context for analysis
    type TonalContext = {
        KeyCenter: PitchClass
        Mode: TonalityType
        Scale: PitchClassSet
        DiatonicChords: FunctionalChord list
    }
    
    /// Represents harmonic progression analysis
    type ProgressionAnalysis = {
        Context: TonalContext
        Chords: FunctionalChord list
        Modulations: (int * TonalContext) list // position and new context
        TonalMovement: string
        Coherence: float
    }
    
    /// Determines harmonic function based on scale degree
    let getHarmonicFunction (keyCenter: PitchClass) (chordRoot: PitchClass) : HarmonicFunction =
        let interval = (int chordRoot - int keyCenter + 12) % 12
        match interval with
        | 0 -> Tonic chordRoot
        | 2 -> Supertonic chordRoot
        | 4 -> Mediant chordRoot
        | 5 -> Subdominant chordRoot
        | 7 -> Dominant chordRoot
        | 9 -> Submediant chordRoot
        | 10 -> LeadingTone chordRoot
        | 11 -> LeadingTone chordRoot
        | _ -> Chromatic chordRoot
    
    /// Analyzes chord quality from pitch class set
    let analyzeChordQuality (pitchClassSet: PitchClassSet) : ChordQuality =
        let intervals = pitchClassSet.IntervalClassVector
        match pitchClassSet.Cardinality.Value with
        | 3 -> // Triads
            if intervals.[3] > 0 && intervals.[4] > 0 then Major
            elif intervals.[3] > 0 && intervals.[2] > 0 then Minor
            elif intervals.[2] > 0 && intervals.[1] > 0 then Diminished
            elif intervals.[4] > 0 && intervals.[5] > 0 then Augmented
            else Major // default
        | 4 -> // Seventh chords
            if intervals.[3] > 0 && intervals.[4] > 0 && intervals.[5] > 0 then Major7
            elif intervals.[3] > 0 && intervals.[2] > 0 && intervals.[5] > 0 then Minor7
            elif intervals.[3] > 0 && intervals.[4] > 0 && intervals.[4] > 1 then Dominant7
            elif intervals.[2] > 0 && intervals.[1] > 0 && intervals.[4] > 0 then HalfDiminished7
            elif intervals.[2] > 0 && intervals.[1] > 0 && intervals.[1] > 1 then FullyDiminished7
            else Dominant7 // default
        | n when n > 4 -> Extended(n, Major) // simplified for extended chords
        | _ -> Major
    
    /// Creates a functional chord from basic components
    let createFunctionalChord (keyCenter: PitchClass) (chordTemplate: ChordTemplate) (root: PitchClass) : FunctionalChord =
        {
            Function = getHarmonicFunction keyCenter root
            Quality = analyzeChordQuality chordTemplate.PitchClassSet
            Root = root
            PitchClassSet = chordTemplate.PitchClassSet
            Inversions = None
            Extensions = []
        }
    
    /// Creates tonal context for a given key
    let createTonalContext (keyCenter: PitchClass) (mode: TonalityType) : TonalContext =
        let scale = match mode with
                   | TonalityType.Major -> 
                       new PitchClassSet([0; 2; 4; 5; 7; 9; 11] |> List.map (fun i -> PitchClass.FromValue((int keyCenter + i) % 12)))
                   | TonalityType.Minor ->
                       new PitchClassSet([0; 2; 3; 5; 7; 8; 10] |> List.map (fun i -> PitchClass.FromValue((int keyCenter + i) % 12)))
                   | _ -> 
                       new PitchClassSet([0; 1; 2; 3; 4; 5; 6; 7; 8; 9; 10; 11] |> List.map PitchClass.FromValue) // chromatic
        
        // Generate diatonic chords (simplified)
        let diatonicChords = 
            scale 
            |> Seq.take 7
            |> Seq.mapi (fun i pc -> 
                let triadPitches = [pc; PitchClass.FromValue((int pc + 2) % 12); PitchClass.FromValue((int pc + 4) % 12)]
                let triadSet = new PitchClassSet(triadPitches)
                {
                    Function = getHarmonicFunction keyCenter pc
                    Quality = analyzeChordQuality triadSet
                    Root = pc
                    PitchClassSet = triadSet
                    Inversions = None
                    Extensions = []
                })
            |> Seq.toList
        
        {
            KeyCenter = keyCenter
            Mode = mode
            Scale = scale
            DiatonicChords = diatonicChords
        }
    
    /// Analyzes a chord progression functionally
    let analyzeProgression (progression: (ChordTemplate * PitchClass) list) (initialContext: TonalContext option) : ProgressionAnalysis =
        // Determine initial context if not provided
        let context = match initialContext with
                     | Some ctx -> ctx
                     | None -> 
                         // Simple key detection based on first and last chords
                         let firstRoot = snd progression.Head
                         createTonalContext firstRoot TonalityType.Major
        
        // Analyze each chord in context
        let functionalChords = 
            progression
            |> List.map (fun (template, root) -> createFunctionalChord context.KeyCenter template root)
        
        // Detect modulations (simplified)
        let modulations = 
            functionalChords
            |> List.mapi (fun i chord -> (i, chord))
            |> List.choose (fun (i, chord) ->
                match chord.Function with
                | Chromatic _ -> Some (i, createTonalContext chord.Root TonalityType.Major)
                | _ -> None)
        
        // Calculate tonal movement
        let tonalMovement = 
            functionalChords
            |> List.pairwise
            |> List.map (fun (prev, curr) ->
                match prev.Function, curr.Function with
                | Tonic _, Dominant _ -> "Strong"
                | Dominant _, Tonic _ -> "Resolution"
                | Subdominant _, Dominant _ -> "Classical"
                | _ -> "Weak")
            |> List.distinct
            |> String.concat ", "
        
        // Calculate coherence based on diatonic content
        let diatonicChords = functionalChords |> List.filter (fun c -> 
            match c.Function with
            | Chromatic _ -> false
            | _ -> true)
        let coherence = float diatonicChords.Length / float functionalChords.Length
        
        {
            Context = context
            Chords = functionalChords
            Modulations = modulations
            TonalMovement = tonalMovement
            Coherence = coherence
        }
    
    /// Pattern matching helpers for harmonic analysis
    module Patterns =
        
        /// Active pattern for common chord progressions
        let (|CommonProgression|_|) (functions: HarmonicFunction list) =
            match functions with
            | [Tonic _; Subdominant _; Dominant _; Tonic _] -> Some "I-IV-V-I"
            | [Tonic _; Submediant _; Subdominant _; Dominant _] -> Some "I-vi-IV-V"
            | [Submediant _; Subdominant _; Tonic _; Dominant _] -> Some "vi-IV-I-V"
            | [Tonic _; Dominant _; Submediant _; Subdominant _] -> Some "I-V-vi-IV"
            | _ -> None
        
        /// Active pattern for cadences
        let (|Cadence|_|) (functions: HarmonicFunction list) =
            match functions |> List.rev |> List.take 2 |> List.rev with
            | [Dominant _; Tonic _] -> Some "Authentic"
            | [Subdominant _; Tonic _] -> Some "Plagal"
            | [Dominant _; Submediant _] -> Some "Deceptive"
            | _ -> None
        
        /// Active pattern for secondary dominants
        let (|SecondaryDominant|_|) (chord: FunctionalChord) =
            match chord.Quality, chord.Function with
            | Dominant7, Chromatic root -> Some ("V7/" + root.ToString())
            | _ -> None
    
    /// Utility functions for harmonic analysis
    module HarmonyUtils =
        
        /// Gets the Roman numeral representation of a functional chord
        let toRomanNumeral (chord: FunctionalChord) : string =
            let numeral = match chord.Function with
                         | Tonic _ -> "I"
                         | Supertonic _ -> "ii"
                         | Mediant _ -> "iii"
                         | Subdominant _ -> "IV"
                         | Dominant _ -> "V"
                         | Submediant _ -> "vi"
                         | LeadingTone _ -> "vii°"
                         | Chromatic _ -> "?"
            
            match chord.Quality with
            | Minor | HalfDiminished7 | FullyDiminished7 -> numeral.ToLower()
            | Diminished -> numeral.ToLower() + "°"
            | Dominant7 -> numeral + "7"
            | Major7 -> numeral + "maj7"
            | Minor7 -> numeral.ToLower() + "7"
            | _ -> numeral
        
        /// Calculates harmonic rhythm (chord changes per measure)
        let calculateHarmonicRhythm (chords: FunctionalChord list) (beatsPerMeasure: int) : float =
            float chords.Length / float beatsPerMeasure
        
        /// Finds the most common progression patterns
        let findProgressionPatterns (analysis: ProgressionAnalysis) : string list =
            let functions = analysis.Chords |> List.map (fun c -> c.Function)
            [
                match functions with
                | Patterns.CommonProgression pattern -> yield pattern
                | _ -> ()
                
                match functions with
                | Patterns.Cadence cadence -> yield cadence + " Cadence"
                | _ -> ()
            ]
        
        /// Analyzes voice leading between chords
        let analyzeVoiceLeading (chord1: FunctionalChord) (chord2: FunctionalChord) : string =
            let set1 = chord1.PitchClassSet |> Set.ofSeq
            let set2 = chord2.PitchClassSet |> Set.ofSeq
            let common = Set.intersect set1 set2
            let commonCount = Set.count common
            
            match commonCount with
            | n when n >= 2 -> "Smooth"
            | 1 -> "Moderate"
            | _ -> "Disjunct"
