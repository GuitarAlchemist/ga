namespace GA.Business.Core.Chords;

using Atonal;

/// <summary>
///     Main service that coordinates all chord naming services and provides a unified API
/// </summary>
public static class ChordTemplateNamingService
{
    /// <summary>
    ///     Generates comprehensive chord names for a chord template with specific root
    /// </summary>
    public static ComprehensiveChordName GenerateComprehensiveNames(
        ChordTemplate template,
        PitchClass root,
        PitchClass? bassNote = null)
    {
        var primary = GeneratePrimaryName(template, root, bassNote);
        var slashChord = GenerateSlashChordName(template, root, bassNote);
        var quartal = GenerateQuartalName(template, root);
        var withAlterations = GenerateNameWithAlterations(template, root);
        var enharmonicEquivalent = GenerateEnharmonicEquivalent(template, root);
        var atonalName = GenerateAtonalName(template, root);
        var (keyAwareName, mostProbableKey) = GenerateKeyAwareName(template, root);
        var (iconicName, iconicDescription) = GenerateIconicName(template, root);
        var alternates = GenerateAlternateNames(template, root, bassNote);

        return new ComprehensiveChordName(primary, slashChord, quartal, withAlterations, enharmonicEquivalent,
            atonalName, keyAwareName, mostProbableKey, iconicName, iconicDescription, alternates);
    }

    /// <summary>
    ///     Gets the best chord name based on context using hybrid analysis
    /// </summary>
    public static string GetBestChordName(ChordTemplate template, PitchClass root, PitchClass? bassNote = null)
    {
        // Use hybrid analysis for intelligent tonal/atonal selection
        return HybridChordNamingService.GetBestChordName(template, root, bassNote);
    }

    /// <summary>
    ///     Gets all naming options for the same chord
    /// </summary>
    public static IEnumerable<string> GetAllNamingOptions(
        ChordTemplate template,
        PitchClass root,
        PitchClass? bassNote = null)
    {
        var comprehensive = GenerateComprehensiveNames(template, root, bassNote);
        var names = new List<string> { comprehensive.Primary };

        if (!string.IsNullOrEmpty(comprehensive.SlashChord))
        {
            names.Add(comprehensive.SlashChord);
        }

        if (!string.IsNullOrEmpty(comprehensive.Quartal))
        {
            names.Add(comprehensive.Quartal);
        }

        names.AddRange(comprehensive.Alternates);

        return names.Distinct();
    }

    /// <summary>
    ///     Generates the primary chord name
    /// </summary>
    private static string GeneratePrimaryName(ChordTemplate template, PitchClass root, PitchClass? bassNote)
    {
        var rootName = GetNoteName(root);
        var suffix = BasicChordExtensionsService.GetExtensionNotation(template.Extension, template.Quality);

        var primaryName = $"{rootName}{suffix}";

        // Add bass note if it's a slash chord
        if (bassNote.HasValue && !bassNote.Value.Equals(root))
        {
            var bassName = GetNoteName(bassNote.Value);
            primaryName += $"/{bassName}";
        }

        return primaryName;
    }

    /// <summary>
    ///     Generates slash chord name if applicable
    /// </summary>
    private static string? GenerateSlashChordName(ChordTemplate template, PitchClass root, PitchClass? bassNote)
    {
        if (!bassNote.HasValue || bassNote.Value.Equals(root))
        {
            return null;
        }

        // Simple slash chord notation for now
        var rootName = GetNoteName(root);
        var suffix = BasicChordExtensionsService.GetExtensionNotation(template.Extension, template.Quality);
        var bassName = GetNoteName(bassNote.Value);
        return $"{rootName}{suffix}/{bassName}";
    }

    /// <summary>
    ///     Generates quartal chord name if applicable
    /// </summary>
    private static string? GenerateQuartalName(ChordTemplate template, PitchClass root)
    {
        if (!QuartalChordNamingService.IsQuartalHarmony(template))
        {
            return null;
        }

        // Use enhanced quartal analysis for better naming
        var analysis = QuartalChordNamingService.AnalyzeQuartalChord(template, root);
        return analysis.Type != QuartalChordNamingService.QuartalChordType.NotQuartal
            ? analysis.PrimaryName
            : null;
    }

    /// <summary>
    ///     Generates chord name with alterations if applicable
    /// </summary>
    private static string? GenerateNameWithAlterations(ChordTemplate template, PitchClass root)
    {
        var analysis = ChordAlterationService.AnalyzeAlterations(template);
        if (!analysis.Alterations.Any())
        {
            return null;
        }

        return ChordAlterationService.GenerateNameWithAlterations(root, template);
    }

    /// <summary>
    ///     Generates enharmonic equivalent name
    /// </summary>
    private static string? GenerateEnharmonicEquivalent(ChordTemplate template, PitchClass root)
    {
        var enharmonics = EnharmonicNamingService.GetEnharmonicEquivalents(root, template);
        var primary = GeneratePrimaryName(template, root, null);

        return enharmonics.FirstOrDefault(name => name != primary);
    }

    /// <summary>
    ///     Generates atonal name if applicable
    /// </summary>
    private static string? GenerateAtonalName(ChordTemplate template, PitchClass root)
    {
        if (!AtonalChordAnalysisService.RequiresAtonalAnalysis(template))
        {
            return null;
        }

        return AtonalChordAnalysisService.GenerateAtonalChordName(template, root);
    }

    /// <summary>
    ///     Generates key-aware name if applicable
    /// </summary>
    private static (string? keyAwareName, string? mostProbableKey) GenerateKeyAwareName(ChordTemplate template,
        PitchClass root)
    {
        try
        {
            var analysis = KeyAwareChordNamingService.AnalyzeInAllKeys(template, root);
            return (analysis.RecommendedName, analysis.MostProbableKey.Key.ToString());
        }
        catch
        {
            return (null, null);
        }
    }

    /// <summary>
    ///     Generates iconic chord name if this chord matches a famous voicing
    /// </summary>
    private static (string? iconicName, string? description) GenerateIconicName(ChordTemplate template, PitchClass root)
    {
        // Create pitch class set from template and root
        var pitchClasses = new List<PitchClass> { root };
        foreach (var interval in template.Intervals)
        {
            var pc = PitchClass.FromValue((root.Value + interval.Interval.Semitones.Value) % 12);
            if (!pitchClasses.Contains(pc))
            {
                pitchClasses.Add(pc);
            }
        }

        var pitchClassSet = new PitchClassSet(pitchClasses);
        // TODO: Implement IconicChordRegistry
        // var iconicMatches = IconicChordRegistry.FindIconicMatches(pitchClassSet);
        var iconicMatches = Enumerable.Empty<object>(); // Temporary stub

        // TODO: Implement iconic chord matching
        // var bestMatch = iconicMatches.FirstOrDefault();
        // if (bestMatch != null)
        // {
        //     return ($"{bestMatch.IconicName} ({bestMatch.TheoreticalName})",
        //            $"{bestMatch.Description} - {bestMatch.Artist}");
        // }

        return (null, null);
    }

    /// <summary>
    ///     Generates alternate chord names
    /// </summary>
    private static IReadOnlyList<string> GenerateAlternateNames(
        ChordTemplate template,
        PitchClass root,
        PitchClass? bassNote)
    {
        var alternates = new List<string>();

        // Add iconic names as alternates
        var (iconicName, _) = GenerateIconicName(template, root);
        if (iconicName != null)
        {
            alternates.Add(iconicName);
        }

        // Enharmonic equivalents
        var enharmonicRoot = GetEnharmonicEquivalent(root);
        if (enharmonicRoot != null)
        {
            var enharmonicName = GeneratePrimaryName(template, enharmonicRoot.Value, bassNote);
            alternates.Add(enharmonicName);
        }

        // Functional equivalents (C6 = Am7)
        if (template.Extension == ChordExtension.Sixth && template.Quality == ChordQuality.Major)
        {
            var relativeMinor = PitchClass.FromValue((root.Value + 9) % 12);
            alternates.Add($"{GetNoteName(relativeMinor)}m7");
        }

        return alternates.AsReadOnly();
    }

    /// <summary>
    ///     Helper methods
    /// </summary>
    private static string GetNoteName(PitchClass pitchClass)
    {
        return pitchClass.Value switch
        {
            0 => "C", 1 => "C#", 2 => "D", 3 => "D#", 4 => "E", 5 => "F",
            6 => "F#", 7 => "G", 8 => "G#", 9 => "A", 10 => "A#", 11 => "B",
            _ => "?"
        };
    }

    private static PitchClass? GetEnharmonicEquivalent(PitchClass pitchClass)
    {
        return pitchClass.Value switch
        {
            1 => PitchClass.FromValue(1), 3 => PitchClass.FromValue(3),
            6 => PitchClass.FromValue(6), 8 => PitchClass.FromValue(8),
            10 => PitchClass.FromValue(10), _ => null
        };
    }

    /// <summary>
    ///     Comprehensive chord naming result
    /// </summary>
    public record ComprehensiveChordName(
        string Primary,
        string? SlashChord,
        string? Quartal,
        string? WithAlterations,
        string? EnharmonicEquivalent,
        string? AtonalName,
        string? KeyAwareName,
        string? MostProbableKey,
        string? IconicName,
        string? IconicDescription,
        IReadOnlyList<string> Alternates);
}
