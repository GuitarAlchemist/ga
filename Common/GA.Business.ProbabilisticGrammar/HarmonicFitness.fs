namespace GA.Business.ProbabilisticGrammar

open GA.Business.DSL.Types.GrammarTypes

/// Context for voicing and harmonic fitness evaluation
type HarmonicContext =
    { KeyRoot: char
      KeyMode: string
      Style: string
      PreviousChord: Chord option }

module private NoteOps =

    let noteToSemitone (note: Note) : int =
        let base' =
            match note.Letter with
            | 'C' -> 0 | 'D' -> 2 | 'E' -> 4 | 'F' -> 5
            | 'G' -> 7 | 'A' -> 9 | 'B' -> 11 | _ -> 0
        let acc =
            match note.Accidental with
            | Some Sharp       -> 1
            | Some Flat        -> -1
            | Some DoubleSharp -> 2
            | Some DoubleFlat  -> -2
            | _                -> 0
        (base' + acc + 120) % 12

    let letterToSemitone (letter: char) : int =
        match letter with
        | 'C' -> 0 | 'D' -> 2 | 'E' -> 4 | 'F' -> 5
        | 'G' -> 7 | 'A' -> 9 | 'B' -> 11 | _ -> 0

module HarmonicFitness =

    /// Voice leading smoothness: rewards stepwise motion, penalizes large jumps.
    /// Returns 0.0 (bad) to 1.0 (good).
    let private voiceLeadingSmoothness (chords: Chord list) : float =
        if chords.Length < 2 then 1.0
        else
            let roots = chords |> List.map (fun c -> NoteOps.noteToSemitone c.Root)
            let avgJump =
                roots
                |> List.pairwise
                |> List.averageBy (fun (a, b) ->
                    let diff = abs (b - a)
                    float (min diff (12 - diff)))
            // Score: 1.0 = no movement, 0.0 = tritone jumps
            1.0 - (min avgJump 7.0) / 7.0

    /// Functional harmony score: rewards T-PD-D-T motion (V->I cadences, ii-V-I progressions).
    let private functionalHarmonyScore (chords: Chord list) (keyRootLetter: char) : float =
        let tonicPc     = NoteOps.letterToSemitone keyRootLetter
        let dominantPc  = (tonicPc + 7)  % 12
        let subdominant = (tonicPc + 5)  % 12
        let rootPcs     = chords |> List.map (fun c -> NoteOps.noteToSemitone c.Root)

        let cadences =
            rootPcs
            |> List.pairwise
            |> List.filter (fun (a, b) -> a = dominantPc && b = tonicPc)
            |> List.length

        let iiVIs =
            rootPcs
            |> List.windowed 3
            |> List.filter (function
                | [a; b; c] -> a = subdominant && b = dominantPc && c = tonicPc
                | _ -> false)
            |> List.length

        min 1.0 (0.5 + float cadences * 0.2 + float iiVIs * 0.3)

    /// Common-tone retention: rewards progressions that share notes between adjacent chords.
    let private commonToneScore (chords: Chord list) : float =
        if chords.Length < 2 then 1.0
        else
            let chordToneSets =
                chords
                |> List.map (fun c ->
                    let root = NoteOps.noteToSemitone c.Root
                    let intervals =
                        match c.Quality with
                        | Major    -> [0; 4; 7]
                        | Minor    -> [0; 3; 7]
                        | Dominant7 -> [0; 4; 7; 10]
                        | Major7   -> [0; 4; 7; 11]
                        | Minor7   -> [0; 3; 7; 10]
                        | _        -> [0; 4; 7]
                    intervals |> List.map (fun i -> (root + i) % 12) |> Set.ofList)

            let retentionRatios =
                chordToneSets
                |> List.pairwise
                |> List.map (fun (a, b) ->
                    let shared = Set.intersect a b |> Set.count
                    float shared / float (max 1 (Set.count a)))

            List.average retentionRatios

    /// Overall chord progression fitness (0.0–1.0).
    /// Factors: voice leading (40%), functional harmony (40%), common tones (20%).
    let chordProgressionFitness (progression: Chord list) (key: Note) : float =
        if progression.IsEmpty then 0.0
        else
            let smoothness = voiceLeadingSmoothness progression
            let functional = functionalHarmonyScore progression key.Letter
            let commonTone = commonToneScore progression
            smoothness * 0.4 + functional * 0.4 + commonTone * 0.2

    /// Voicing fitness based on pitch-class distances (0.0–1.0).
    /// Full fretboard-aware scoring would require Voicing/Tuning types from GA.Domain.Services.
    /// This version uses pitch-class intervals as a proxy.
    let voicingFitness (pitchClasses: int list) (context: HarmonicContext) : float =
        if pitchClasses.IsEmpty then 0.0
        else
            let sorted = List.sort pitchClasses
            let span = List.last sorted - List.head sorted

            // Penalize very wide spans (> 2 octaves = 24 semitones)
            let spanScore = max 0.0 (1.0 - float span / 24.0)

            // Reward variety: distinct interval classes between adjacent voices
            let intervalVariety =
                sorted
                |> List.pairwise
                |> List.map (fun (a, b) -> b - a)
                |> List.distinct
                |> List.length
            let varietyScore = min 1.0 (float intervalVariety / 4.0)

            // Style bonus: jazz prefers rootless voicings (no perfect 5th doublings)
            let styleBonus =
                if context.Style.Contains("jazz") then
                    let hasFifth =
                        sorted
                        |> List.pairwise
                        |> List.exists (fun (a, b) -> (b - a) % 12 = 7)
                    if hasFifth then 0.0 else 0.1
                else 0.0

            min 1.0 (spanScore * 0.5 + varietyScore * 0.4 + styleBonus * 0.1)

    /// Scale fitness for improvising over a chord.
    /// Measures how well the scale covers the chord tones plus style appropriateness.
    let scaleChoiceFitness (scaleIntervals: int list) (chordIntervals: int list) (style: string) : float =
        if scaleIntervals.IsEmpty || chordIntervals.IsEmpty then 0.0
        else
            let chordTones  = Set.ofList chordIntervals
            let scaleTones  = Set.ofList scaleIntervals
            let covered     = Set.intersect chordTones scaleTones |> Set.count
            let coverageRatio = float covered / float (max 1 chordTones.Count)

            let styleBonus =
                match style with
                | s when s.Contains("jazz") && scaleIntervals.Length >= 7 -> 0.1
                | s when s.Contains("blues") && List.contains 3 scaleIntervals && List.contains 10 scaleIntervals -> 0.15
                | s when s.Contains("classical") && scaleIntervals = [0; 2; 4; 5; 7; 9; 11] -> 0.2
                | _ -> 0.0

            min 1.0 (coverageRatio + styleBonus)
