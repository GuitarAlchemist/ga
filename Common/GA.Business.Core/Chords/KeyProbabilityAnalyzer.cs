namespace GA.Business.Core.Chords;

using Atonal;
using Tonal;

/// <summary>
///     Advanced key probability analysis based on chord progressions, voice leading, and harmonic context
/// </summary>
public static class KeyProbabilityAnalyzer
{
    /// <summary>
    ///     Analyzes key probability for a sequence of chords
    /// </summary>
    public static ProgressionAnalysis AnalyzeProgression(IEnumerable<(ChordTemplate template, PitchClass root)> chords)
    {
        var chordList = chords.ToList();
        var keyProbabilities = new List<KeyProbabilityResult>();

        // Analyze each possible key
        foreach (var key in Key.Items)
        {
            var result = AnalyzeKeyProbability(chordList, key);
            keyProbabilities.Add(result);
        }

        // Sort by probability
        var sortedResults = keyProbabilities.OrderByDescending(r => r.Probability).ToList().AsReadOnly();
        var mostProbable = sortedResults.First();

        // Detect common progressions
        var detectedProgressions = DetectCommonProgressions(chordList, mostProbable.Key);

        // Calculate overall tonal strength
        var tonalStrength = CalculateOverallTonalStrength(chordList, mostProbable);

        return new ProgressionAnalysis(sortedResults, mostProbable, detectedProgressions, tonalStrength);
    }

    /// <summary>
    ///     Analyzes probability of a specific key for a chord sequence
    /// </summary>
    private static KeyProbabilityResult AnalyzeKeyProbability(IList<(ChordTemplate template, PitchClass root)> chords,
        Key key)
    {
        var diatonicScore = CalculateDiatonicScore(chords, key);
        var functionalScore = CalculateFunctionalScore(chords, key);
        var progressionScore = CalculateProgressionScore(chords, key);
        var voiceLeadingScore = CalculateVoiceLeadingScore(chords, key);

        // Weighted combination of scores
        var probability = diatonicScore * 0.3 + functionalScore * 0.3 +
                          progressionScore * 0.25 + voiceLeadingScore * 0.15;

        var supportingEvidence = GenerateSupportingEvidence(chords, key, diatonicScore, functionalScore);
        var conflictingEvidence = GenerateConflictingEvidence(chords, key);

        return new KeyProbabilityResult(
            key, probability, diatonicScore, functionalScore, progressionScore, voiceLeadingScore,
            supportingEvidence, conflictingEvidence);
    }

    /// <summary>
    ///     Calculates how well chords fit diatonically in the key
    /// </summary>
    private static double CalculateDiatonicScore(IList<(ChordTemplate template, PitchClass root)> chords, Key key)
    {
        if (!chords.Any())
        {
            return 0.0;
        }

        var keyPitchClasses = key.PitchClassSet;
        var totalScore = 0.0;

        foreach (var (template, root) in chords)
        {
            var chordPitchClasses = GetChordPitchClasses(template, root).ToList();
            var diatonicNotes = chordPitchClasses.Count(pc => keyPitchClasses.Contains(pc));
            var chordScore = (double)diatonicNotes / chordPitchClasses.Count;
            totalScore += chordScore;
        }

        return totalScore / chords.Count;
    }

    /// <summary>
    ///     Calculates functional harmony score
    /// </summary>
    private static double CalculateFunctionalScore(IList<(ChordTemplate template, PitchClass root)> chords, Key key)
    {
        if (!chords.Any())
        {
            return 0.0;
        }

        var totalScore = 0.0;

        foreach (var (template, root) in chords)
        {
            var scaleDegree = GetScaleDegree(root, key);
            var function = DetermineChordFunction(scaleDegree, template, key);

            var functionScore = function switch
            {
                KeyAwareChordNamingService.ChordFunction.Tonic => 1.0,
                KeyAwareChordNamingService.ChordFunction.Dominant => 0.9,
                KeyAwareChordNamingService.ChordFunction.Subdominant => 0.8,
                KeyAwareChordNamingService.ChordFunction.Supertonic => 0.7,
                KeyAwareChordNamingService.ChordFunction.Submediant => 0.7,
                KeyAwareChordNamingService.ChordFunction.Mediant => 0.6,
                KeyAwareChordNamingService.ChordFunction.LeadingTone => 0.6,
                KeyAwareChordNamingService.ChordFunction.Secondary => 0.4,
                _ => 0.2
            };

            totalScore += functionScore;
        }

        return totalScore / chords.Count;
    }

    /// <summary>
    ///     Calculates score based on common chord progressions
    /// </summary>
    private static double CalculateProgressionScore(IList<(ChordTemplate template, PitchClass root)> chords, Key key)
    {
        if (chords.Count < 2)
        {
            return 0.5; // Neutral score for single chords
        }

        var scaleDegrees = chords.Select(c => GetScaleDegree(c.root, key)).ToList();
        var progressionScore = 0.0;
        var progressionCount = 0;

        // Check for common progressions
        var commonProgressions = GetCommonProgressionPatterns();

        foreach (var progression in commonProgressions)
        {
            var matches = FindProgressionMatches(scaleDegrees, progression.ScaleDegrees);
            progressionScore += matches * progression.Strength;
            progressionCount += matches;
        }

        // Normalize by number of possible progressions
        var maxPossibleProgressions = chords.Count - 1;
        return progressionCount > 0 ? progressionScore / maxPossibleProgressions : 0.3;
    }

    /// <summary>
    ///     Calculates voice leading score
    /// </summary>
    private static double CalculateVoiceLeadingScore(IList<(ChordTemplate template, PitchClass root)> chords, Key key)
    {
        if (chords.Count < 2)
        {
            return 0.5;
        }

        var totalScore = 0.0;
        var transitionCount = 0;

        for (var i = 1; i < chords.Count; i++)
        {
            var prevRoot = chords[i - 1].root;
            var currentRoot = chords[i].root;
            var interval = (currentRoot.Value - prevRoot.Value + 12) % 12;

            // Score based on common voice leading intervals
            var voiceLeadingScore = interval switch
            {
                0 => 0.3, // Same root
                1 => 0.4, // Semitone
                2 => 0.6, // Whole tone
                3 => 0.5, // Minor third
                4 => 0.5, // Major third
                5 => 0.8, // Perfect fourth (strong)
                7 => 0.9, // Perfect fifth (very strong)
                _ => 0.3 // Other intervals
            };

            totalScore += voiceLeadingScore;
            transitionCount++;
        }

        return transitionCount > 0 ? totalScore / transitionCount : 0.5;
    }

    /// <summary>
    ///     Gets common progression patterns
    /// </summary>
    private static IEnumerable<(IReadOnlyList<int> ScaleDegrees, double Strength, string Name)>
        GetCommonProgressionPatterns()
    {
        return
        [
            (new[] { 1, 5, 6, 4 }.ToList().AsReadOnly(), 1.0, "I-V-vi-IV"),
            (new[] { 6, 4, 1, 5 }.ToList().AsReadOnly(), 1.0, "vi-IV-I-V"),
            (new[] { 2, 5, 1 }.ToList().AsReadOnly(), 0.9, "ii-V-I"),
            (new[] { 1, 6, 4, 5 }.ToList().AsReadOnly(), 0.8, "I-vi-IV-V"),
            (new[] { 1, 4, 5, 1 }.ToList().AsReadOnly(), 0.8, "I-IV-V-I"),
            (new[] { 6, 2, 5, 1 }.ToList().AsReadOnly(), 0.7, "vi-ii-V-I"),
            (new[] { 1, 3, 6, 4 }.ToList().AsReadOnly(), 0.6, "I-iii-vi-IV"),
            (new[] { 4, 5, 1 }.ToList().AsReadOnly(), 0.7, "IV-V-I"),
            (new[] { 5, 1 }.ToList().AsReadOnly(), 0.8, "V-I"),
            (new[] { 4, 1 }.ToList().AsReadOnly(), 0.6, "IV-I")
        ];
    }

    /// <summary>
    ///     Finds matches for a progression pattern
    /// </summary>
    private static int FindProgressionMatches(IList<int> scaleDegrees, IReadOnlyList<int> pattern)
    {
        var matches = 0;

        for (var i = 0; i <= scaleDegrees.Count - pattern.Count; i++)
        {
            var isMatch = true;
            for (var j = 0; j < pattern.Count; j++)
            {
                if (scaleDegrees[i + j] != pattern[j])
                {
                    isMatch = false;
                    break;
                }
            }

            if (isMatch)
            {
                matches++;
            }
        }

        return matches;
    }

    /// <summary>
    ///     Detects common progressions in the chord sequence
    /// </summary>
    private static IReadOnlyList<CommonProgression> DetectCommonProgressions(
        IList<(ChordTemplate template, PitchClass root)> chords, Key key)
    {
        var scaleDegrees = chords.Select(c => GetScaleDegree(c.root, key)).ToList();
        var detectedProgressions = new List<CommonProgression>();

        foreach (var (pattern, strength, name) in GetCommonProgressionPatterns())
        {
            var matches = FindProgressionMatches(scaleDegrees, pattern);
            if (matches > 0)
            {
                detectedProgressions.Add(new CommonProgression(name, pattern, strength, key));
            }
        }

        return detectedProgressions.AsReadOnly();
    }

    /// <summary>
    ///     Calculates overall tonal strength
    /// </summary>
    private static double CalculateOverallTonalStrength(
        IList<(ChordTemplate template, PitchClass root)> chords, KeyProbabilityResult mostProbable)
    {
        // High probability difference suggests strong tonal center
        var probabilitySpread = mostProbable.Probability -
                                (chords.Any() ? mostProbable.Probability * 0.5 : 0.5);

        // Strong functional harmony suggests tonality
        var functionalStrength = mostProbable.FunctionalScore;

        // Common progressions suggest tonality
        var progressionStrength = mostProbable.ProgressionScore;

        return (probabilitySpread + functionalStrength + progressionStrength) / 3.0;
    }

    /// <summary>
    ///     Generates supporting evidence for key choice
    /// </summary>
    private static IReadOnlyList<string> GenerateSupportingEvidence(
        IList<(ChordTemplate template, PitchClass root)> chords, Key key, double diatonicScore, double functionalScore)
    {
        var evidence = new List<string>();

        if (diatonicScore > 0.8)
        {
            evidence.Add($"High diatonic content ({diatonicScore:P0})");
        }

        if (functionalScore > 0.7)
        {
            evidence.Add($"Strong functional harmony ({functionalScore:P0})");
        }

        var tonicChords = chords.Count(c => GetScaleDegree(c.root, key) == 1);
        if (tonicChords > 0)
        {
            evidence.Add($"Contains {tonicChords} tonic chord(s)");
        }

        var dominantChords = chords.Count(c => GetScaleDegree(c.root, key) == 5);
        if (dominantChords > 0)
        {
            evidence.Add($"Contains {dominantChords} dominant chord(s)");
        }

        return evidence.AsReadOnly();
    }

    /// <summary>
    ///     Generates conflicting evidence against key choice
    /// </summary>
    private static IReadOnlyList<string> GenerateConflictingEvidence(
        IList<(ChordTemplate template, PitchClass root)> chords, Key key)
    {
        var conflicts = new List<string>();

        var chromaticChords = chords.Count(c => !IsNaturallyOccurringInKey(c.template, c.root, key));
        if (chromaticChords > chords.Count / 2)
        {
            conflicts.Add($"{chromaticChords} chromatic chord(s)");
        }

        var keyPitchClasses = key.PitchClassSet;
        var foreignNotes = chords.SelectMany(c => GetChordPitchClasses(c.template, c.root))
            .Count(pc => !keyPitchClasses.Contains(pc));

        if (foreignNotes > chords.Count)
        {
            conflicts.Add($"{foreignNotes} foreign note(s)");
        }

        return conflicts.AsReadOnly();
    }

    // Helper methods (reuse from KeyAwareChordNamingService)
    private static int GetScaleDegree(PitchClass pitchClass, Key key)
    {
        return KeyAwareChordNamingService.GetScaleDegree(pitchClass, key);
    }

    private static KeyAwareChordNamingService.ChordFunction DetermineChordFunction(int scaleDegree,
        ChordTemplate template, Key key)
    {
        return KeyAwareChordNamingService.DetermineChordFunction(scaleDegree, template, key);
    }

    private static IEnumerable<PitchClass> GetChordPitchClasses(ChordTemplate template, PitchClass root)
    {
        return KeyAwareChordNamingService.GetChordPitchClasses(template, root);
    }

    private static bool IsNaturallyOccurringInKey(ChordTemplate template, PitchClass root, Key key)
    {
        return KeyAwareChordNamingService.IsNaturallyOccurringInKey(template, root, key);
    }

    /// <summary>
    ///     Chord progression analysis result
    /// </summary>
    public record ProgressionAnalysis(
        IReadOnlyList<KeyProbabilityResult> KeyProbabilities,
        KeyProbabilityResult MostProbableKey,
        IReadOnlyList<CommonProgression> DetectedProgressions,
        double OverallTonalStrength);

    /// <summary>
    ///     Key probability result with detailed analysis
    /// </summary>
    public record KeyProbabilityResult(
        Key Key,
        double Probability,
        double DiatonicScore,
        double FunctionalScore,
        double ProgressionScore,
        double VoiceLeadingScore,
        IReadOnlyList<string> SupportingEvidence,
        IReadOnlyList<string> ConflictingEvidence);

    /// <summary>
    ///     Common chord progression patterns
    /// </summary>
    public record CommonProgression(
        string Name,
        IReadOnlyList<int> ScaleDegrees,
        double Strength,
        Key Key);
}
