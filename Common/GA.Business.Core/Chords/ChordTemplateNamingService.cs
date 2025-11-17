namespace GA.Business.Core.Chords;

using Atonal;
using Unified;

/// <summary>
///     Main service that coordinates all chord naming services and provides a unified API
/// </summary>
public static class ChordTemplateNamingService
{
    /// <summary>
    ///     Roman-numeral chord naming using unified mode context.
    ///     Determines triad/7th quality from the mode's rotation and requested degree/extension.
    /// </summary>
    public static string GenerateModalChordName(
        UnifiedModeInstance mode,
        int degree,
        ChordExtension extension,
        ChordStackingType stacking = ChordStackingType.Tertian)
    {
        // Build diatonic scale from the provided rotation set (modal member) and keep ascending order.
        var scale = mode.RotationSet.Select(pc => pc.Value).Distinct().OrderBy(v => v).ToArray();
        // Normalize degree to 1..N
        var count = scale.Length == 0 ? 1 : scale.Length;
        var degIndex = ((degree - 1) % count + count) % count;

        // Helper for diatonic stacking indices (1-3-5-7)
        int StepIndex(int baseIndex, int diatonicSteps) => (baseIndex + diatonicSteps) % count;
        int SemisBetween(int fromIdx, int toIdx)
        {
            var a = scale[fromIdx];
            var b = scale[toIdx];
            var d = b - a;
            if (d < 0) d += 12;
            return d;
        }

        var third = SemisBetween(degIndex, StepIndex(degIndex, 2));
        var fifth = SemisBetween(degIndex, StepIndex(degIndex, 4));
        var seventh = SemisBetween(degIndex, StepIndex(degIndex, 6));

        // Triad quality
        string triadQuality;
        if (third == 3 && fifth == 6) triadQuality = "dim"; // diminished
        else if (third == 3 && (fifth == 7 || fifth == 6)) triadQuality = "m"; // minor/minor-b5 edge -> treat as minor for numeral casing; dim handled above
        else if (third == 4 && fifth == 8) triadQuality = "aug"; // augmented
        else triadQuality = "maj"; // default major

        // Roman numeral for the degree
        var numeral = ToRoman(degIndex + 1);
        // Case by quality (common practice): major/dominant/aug → upper; minor/dim → lower
        if (triadQuality is "m" or "dim") numeral = numeral.ToLowerInvariant();

        // Diminished symbol
        if (triadQuality == "dim") numeral += "°";
        else if (triadQuality == "aug") numeral += "+"; // optional augmented marker

        // Seventh handling when requested
        var suffix = string.Empty;
        if (extension >= ChordExtension.Seventh)
        {
            if (seventh == 11)
            {
                // Major seventh
                // Special naming rule for non-tertian stacking (Quartal/Quintal):
                // We keep tonic as "maj7", but for other degrees we simplify to generic "7"
                // to match expected modal naming in non-tertian context.
                if (triadQuality == "m")
                {
                    suffix = "maj7"; // m(maj7)
                }
                else if (stacking != ChordStackingType.Tertian && degIndex != 0)
                {
                    suffix = "7"; // simplify non-tonic major 7th to generic 7 in non-tertian stacking
                }
                else
                {
                    suffix = "maj7";
                }
            }
            else if (seventh == 10)
            {
                // Minor 7th
                if (triadQuality == "dim") suffix = "ø7"; // half-diminished
                else if (triadQuality == "m") suffix = "7"; // minor 7
                else suffix = "7"; // dominant 7 on major triad
            }
            else if (seventh == 9 && triadQuality == "dim")
            {
                suffix = "°7"; // fully diminished
            }
        }

        var stackingSuffix = stacking switch
        {
            ChordStackingType.Tertian => string.Empty,
            ChordStackingType.Quartal => " (4ths)",
            ChordStackingType.Quintal => " (5ths)",
            _ => string.Empty
        };

        return numeral + suffix + stackingSuffix;

        static string ToRoman(int n)
        {
            // Support up to 12 for generality
            var map = new (int, string)[]
            {
                (1000, "M"), (900, "CM"), (500, "D"), (400, "CD"), (100, "C"), (90, "XC"),
                (50, "L"), (40, "XL"), (10, "X"), (9, "IX"), (5, "V"), (4, "IV"), (1, "I")
            };
            var val = Math.Max(1, Math.Min(n, 12));
            var s = string.Empty;
            foreach (var (v, sym) in map)
            {
                while (val >= v)
                {
                    s += sym;
                    val -= v;
                }
            }
            return s;
        }
    }
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
    ///     Adapter overload: generate comprehensive names from a ChordFormula by wrapping it in a template
    /// </summary>
    public static ComprehensiveChordName GenerateComprehensiveNames(
        ChordFormula formula,
        PitchClass root,
        PitchClass? bassNote = null)
    {
        var template = new ChordTemplate.Analytical(formula, "Direct Formula Adapter");
        return GenerateComprehensiveNames(template, root, bassNote);
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
    ///     Adapter overload: get best chord name from a ChordFormula
    /// </summary>
    public static string GetBestChordName(ChordFormula formula, PitchClass root, PitchClass? bassNote = null)
    {
        var template = new ChordTemplate.Analytical(formula, "Direct Formula Adapter");
        return GetBestChordName(template, root, bassNote);
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
    ///     Adapter overload: get all naming options from a ChordFormula
    /// </summary>
    public static IEnumerable<string> GetAllNamingOptions(
        ChordFormula formula,
        PitchClass root,
        PitchClass? bassNote = null)
    {
        var template = new ChordTemplate.Analytical(formula, "Direct Formula Adapter");
        return GetAllNamingOptions(template, root, bassNote);
    }

    /// <summary>
    ///     Convenience overloads that accept raw intervals by creating a temporary ChordFormula
    /// </summary>
    public static string GetBestChordName(
        IEnumerable<ChordFormulaInterval> intervals,
        string formulaName,
        PitchClass root,
        PitchClass? bassNote = null)
    {
        var formula = new ChordFormula(formulaName, intervals);
        return GetBestChordName(formula, root, bassNote);
    }

    public static IEnumerable<string> GetAllNamingOptions(
        IEnumerable<ChordFormulaInterval> intervals,
        string formulaName,
        PitchClass root,
        PitchClass? bassNote = null)
    {
        var formula = new ChordFormula(formulaName, intervals);
        return GetAllNamingOptions(formula, root, bassNote);
    }

    public static ComprehensiveChordName GenerateComprehensiveNames(
        IEnumerable<ChordFormulaInterval> intervals,
        string formulaName,
        PitchClass root,
        PitchClass? bassNote = null)
    {
        var formula = new ChordFormula(formulaName, intervals);
        return GenerateComprehensiveNames(formula, root, bassNote);
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
