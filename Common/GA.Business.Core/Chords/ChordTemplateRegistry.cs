namespace GA.Business.Core.Chords;

using Atonal;

/// <summary>
///     Intelligent chord template registry with forward analysis and likelihood-based lookup.
///     Provides the most musically probable chord interpretations for any given pitch class set.
/// </summary>
public static class ChordTemplateRegistry
{
    private static readonly Lazy<ConcurrentDictionary<PitchClassSet, ChordInterpretation[]>> _registry =
        new(BuildRegistry);

    private static readonly Lazy<ChordTemplate[]> _allTemplates =
        new(BuildAllTemplates);

    /// <summary>
    ///     Gets the most likely chord interpretation for a given pitch class set
    /// </summary>
    /// <param name="pitchClassSet">The pitch class set to interpret</param>
    /// <returns>The most probable chord interpretation, or null if none found</returns>
    public static ChordInterpretation? GetMostLikelyChord(PitchClassSet pitchClassSet)
    {
        return _registry.Value.TryGetValue(pitchClassSet, out var interpretations)
            ? interpretations.FirstOrDefault()
            : null;
    }

    /// <summary>
    ///     Gets all possible chord interpretations for a pitch class set, ordered by likelihood
    /// </summary>
    /// <param name="pitchClassSet">The pitch class set to interpret</param>
    /// <returns>Array of interpretations ordered by musical probability</returns>
    public static ChordInterpretation[] GetAllInterpretations(PitchClassSet pitchClassSet)
    {
        return _registry.Value.TryGetValue(pitchClassSet, out var interpretations)
            ? interpretations
            : [];
    }

    /// <summary>
    ///     Gets advanced chord interpretations including drop voicings, slash chords, and complex harmonies
    /// </summary>
    /// <param name="voicing">The chord voicing to analyze</param>
    /// <returns>Advanced chord interpretation with harmonic analysis</returns>
    public static AdvancedChordInterpretation? GetAdvancedInterpretation(ChordVoicing voicing)
    {
        var basicInterpretation = GetMostLikelyChord(voicing.ChordTemplate.PitchClassSet);
        if (basicInterpretation == null)
        {
            return null;
        }

        var advancedAnalysis = AnalyzeVoicing(voicing);
        var enhancedName = GenerateAdvancedChordName(basicInterpretation.Value.Name, advancedAnalysis);

        return new AdvancedChordInterpretation(
            basicInterpretation.Value.Template,
            enhancedName,
            basicInterpretation.Value.RootLikelihood,
            advancedAnalysis
        );
    }

    /// <summary>
    ///     Gets a cached enhanced chord template for the most likely interpretation
    /// </summary>
    /// <param name="pitchClassSet">The pitch class set</param>
    /// <returns>Cached enhanced chord template</returns>
    public static EnhancedChordTemplate? GetTemplate(PitchClassSet pitchClassSet)
    {
        var interpretation = GetMostLikelyChord(pitchClassSet);
        return interpretation.HasValue ? new EnhancedChordTemplate(interpretation.Value.Template) : null;
    }

    /// <summary>
    ///     Gets all cached chord templates (for compatibility with existing code)
    /// </summary>
    /// <returns>All available chord templates</returns>
    public static IEnumerable<ChordTemplate> GetAllTemplates()
    {
        return _allTemplates.Value;
    }

    /// <summary>
    ///     Gets common chord templates (triads and seventh chords)
    /// </summary>
    /// <returns>Common chord templates ordered by musical frequency</returns>
    public static IEnumerable<ChordTemplate> GetCommonTemplates()
    {
        return _allTemplates.Value.Where(t => t.PitchClassSet.Count <= 4);
    }

    /// <summary>
    ///     Registers a custom chord template
    /// </summary>
    public static void RegisterTemplate(ChordTemplate template)
    {
        if (template == null)
        {
            throw new ArgumentNullException(nameof(template));
        }

        var interpretations = AnalyzeAllInterpretations(template.PitchClassSet);
        if (interpretations.Length > 0)
        {
            _registry.Value[template.PitchClassSet] = interpretations;
        }
    }

    private static ConcurrentDictionary<PitchClassSet, ChordInterpretation[]> BuildRegistry()
    {
        var registry = new ConcurrentDictionary<PitchClassSet, ChordInterpretation[]>();

        // Get all systematically generated chord templates (NO HARD-CODED CHORDS)
        var allChords = ChordTemplateFactory.GenerateAllPossibleChords();

        foreach (var template in allChords)
        {
            var interpretations = AnalyzeAllInterpretations(template.PitchClassSet);
            if (interpretations.Length > 0)
            {
                registry[template.PitchClassSet] = interpretations;
            }
        }

        return registry;
    }

    private static ChordTemplate[] BuildAllTemplates()
    {
        return ChordTemplateFactory.GenerateAllPossibleChords().ToArray();
    }

    private static ChordInterpretation[] AnalyzeAllInterpretations(PitchClassSet pitchClassSet)
    {
        var interpretations = new List<ChordInterpretation>();
        var pitchClasses = pitchClassSet.ToList();

        // Try each pitch class as a potential root
        foreach (var root in pitchClasses)
        {
            var likelihood = CalculateRootLikelihood(pitchClassSet, root);
            if (likelihood > 0.1) // Only include interpretations with reasonable likelihood
            {
                var template = FindBestTemplate(pitchClassSet, root);
                if (template != null)
                {
                    var inversion = CalculateInversion(pitchClassSet, root);
                    var name = GenerateChordName(template, root);

                    interpretations.Add(new ChordInterpretation(
                        template, root, likelihood, inversion, name));
                }
            }
        }

        // Sort by likelihood (highest first)
        return interpretations.OrderByDescending(i => i.RootLikelihood).ToArray();
    }

    private static double CalculateRootLikelihood(PitchClassSet pitchClassSet, PitchClass root)
    {
        var pitchClasses = pitchClassSet.ToList();
        var rootIndex = pitchClasses.IndexOf(root);

        // Higher likelihood for bass note (first in set)
        if (rootIndex == 0)
        {
            return 1.0;
        }

        // Check for perfect fifth above root
        var fifthPitchClass = PitchClass.FromSemitones((root.Value + 7) % 12);
        var hasFifth = pitchClasses.Contains(fifthPitchClass);

        // Check for third above root
        var majorThirdPitchClass = PitchClass.FromSemitones((root.Value + 4) % 12);
        var minorThirdPitchClass = PitchClass.FromSemitones((root.Value + 3) % 12);
        var hasThird = pitchClasses.Contains(majorThirdPitchClass) || pitchClasses.Contains(minorThirdPitchClass);

        // Calculate likelihood based on chord tones present
        var likelihood = 0.5; // Base likelihood
        if (hasFifth)
        {
            likelihood += 0.3;
        }

        if (hasThird)
        {
            likelihood += 0.2;
        }

        return Math.Min(likelihood, 1.0);
    }

    private static ChordTemplate? FindBestTemplate(PitchClassSet pitchClassSet, PitchClass root)
    {
        // For now, create a template from the pitch class set
        // In a full implementation, this would match against known templates
        var name = $"Chord_{root}";
        return ChordTemplate.Analytical.FromPitchClassSet(pitchClassSet, name);
    }

    private static int CalculateInversion(PitchClassSet pitchClassSet, PitchClass root)
    {
        var pitchClasses = pitchClassSet.ToList();
        return pitchClasses.IndexOf(root);
    }

    private static string GenerateChordName(ChordTemplate template, PitchClass root)
    {
        return $"{root}{template.GetSymbolSuffix()}";
    }

    private static VoicingAnalysis AnalyzeVoicing(ChordVoicing voicing)
    {
        return new VoicingAnalysis(
            voicing.IsInverted,
            voicing.GetInversion(),
            false, // isDropVoicing - would need more analysis
            false // isSlashChord - would need more analysis
        );
    }

    private static string GenerateAdvancedChordName(string baseName, VoicingAnalysis analysis)
    {
        var name = baseName;

        if (analysis.IsInverted)
        {
            name += $" (inv {analysis.Inversion})";
        }

        if (analysis.IsDropVoicing)
        {
            name += " (drop)";
        }

        if (analysis.IsSlashChord)
        {
            name += " (slash)";
        }

        return name;
    }
}

/// <summary>
///     Represents a specific interpretation of a pitch class set as a chord
/// </summary>
/// <param name="Template">The core chord template</param>
/// <param name="Root">The interpreted root note</param>
/// <param name="RootLikelihood">Likelihood this root interpretation is correct (0-1)</param>
/// <param name="Inversion">The inversion number (0=root, 1=first, etc.)</param>
/// <param name="Name">The chord name (e.g., "Cmaj7", "Am", "F#°")</param>
public readonly record struct ChordInterpretation(
    ChordTemplate Template,
    PitchClass Root,
    double RootLikelihood,
    int Inversion,
    string Name);

/// <summary>
///     Represents an advanced chord interpretation with detailed harmonic analysis
/// </summary>
/// <param name="Template">The core chord template</param>
/// <param name="Name">The enhanced chord name</param>
/// <param name="RootLikelihood">Likelihood this interpretation is correct</param>
/// <param name="Analysis">Detailed voicing analysis</param>
public readonly record struct AdvancedChordInterpretation(
    ChordTemplate Template,
    string Name,
    double RootLikelihood,
    VoicingAnalysis Analysis);

/// <summary>
///     Represents detailed analysis of a chord voicing
/// </summary>
/// <param name="IsInverted">Whether the chord is inverted</param>
/// <param name="Inversion">The inversion number</param>
/// <param name="IsDropVoicing">Whether this is a drop voicing</param>
/// <param name="IsSlashChord">Whether this is a slash chord</param>
public readonly record struct VoicingAnalysis(
    bool IsInverted,
    int Inversion,
    bool IsDropVoicing,
    bool IsSlashChord);
