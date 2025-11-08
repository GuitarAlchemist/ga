namespace GaApi.Services;

using GA.Business.Core.Atonal;
using GA.Business.Core.Chords;
using GA.Business.Core.Tonal;
using GA.Business.Core.Tonal.Modes.Diatonic;
using GA.Business.Core.Tonal.Primitives.Diatonic;
using Models;
using ChordExtension = GA.Business.Core.Chords.ChordExtension;
using ChordStackingType = GA.Business.Core.Chords.ChordStackingType;

/// <summary>
///     Service for analyzing and suggesting modulations between keys
/// </summary>
public interface IModulationService
{
    /// <summary>
    ///     Get modulation suggestions from source key to target key
    /// </summary>
    Task<ModulationSuggestion> GetModulationSuggestionAsync(Key sourceKey, Key targetKey);

    /// <summary>
    ///     Get all common modulation targets from a source key
    /// </summary>
    Task<IEnumerable<ModulationSuggestion>> GetCommonModulationsAsync(Key sourceKey);

    /// <summary>
    ///     Find pivot chords between two keys
    /// </summary>
    IEnumerable<PivotChord> FindPivotChords(Key sourceKey, Key targetKey);
}

/// <summary>
///     Modulation suggestion with pivot chords and progression
/// </summary>
public record ModulationSuggestion
{
    public required Key SourceKey { get; init; }
    public required Key TargetKey { get; init; }
    public required ModulationType Type { get; init; }
    public required IReadOnlyList<PivotChord> PivotChords { get; init; }
    public required string Description { get; init; }
    public double Difficulty { get; init; } // 0.0 = easy, 1.0 = difficult
    public IReadOnlyList<string>? SuggestedProgression { get; init; }
}

/// <summary>
///     A chord that exists in both source and target keys
/// </summary>
public record PivotChord
{
    public required ChordTemplate Template { get; init; }
    public required PitchClass Root { get; init; }
    public required string ChordName { get; init; }
    public required int DegreeInSourceKey { get; init; }
    public required int DegreeInTargetKey { get; init; }
    public required string RomanNumeralInSourceKey { get; init; }
    public required string RomanNumeralInTargetKey { get; init; }
    public required string Function { get; init; }
}

public class ModulationService(
    IContextualChordService chordService,
    ICachingService cache,
    ILogger<ModulationService> logger) : IModulationService
{
    public Task<ModulationSuggestion> GetModulationSuggestionAsync(Key sourceKey, Key targetKey)
    {
        var cacheKey = $"modulation_{sourceKey}_{targetKey}";

        return cache.GetOrCreateRegularAsync(cacheKey, () =>
        {
            logger.LogInformation("Analyzing modulation from {Source} to {Target}", sourceKey, targetKey);

            // Determine modulation type
            var modulationType = DetermineModulationType(sourceKey, targetKey);

            // Find pivot chords
            var pivotChords = FindPivotChords(sourceKey, targetKey).ToList();

            // Calculate difficulty
            var difficulty = CalculateDifficulty(modulationType, pivotChords.Count);

            // Generate description
            var description = GenerateDescription(sourceKey, targetKey, modulationType, pivotChords);

            // Suggest progression
            var progression = SuggestProgression(sourceKey, targetKey, modulationType, pivotChords);

            var suggestion = new ModulationSuggestion
            {
                SourceKey = sourceKey,
                TargetKey = targetKey,
                Type = modulationType,
                PivotChords = pivotChords,
                Description = description,
                Difficulty = difficulty,
                SuggestedProgression = progression
            };

            return Task.FromResult(suggestion);
        });
    }

    public async Task<IEnumerable<ModulationSuggestion>> GetCommonModulationsAsync(Key sourceKey)
    {
        var cacheKey = $"common_modulations_{sourceKey}";

        return await cache.GetOrCreateRegularAsync(cacheKey, async () =>
        {
            logger.LogInformation("Finding common modulations from {Source}", sourceKey);

            var suggestions = new List<ModulationSuggestion>();

            // Relative key (most common)
            var relativeKey = GetRelativeKey(sourceKey);
            if (relativeKey != null)
            {
                suggestions.Add(await GetModulationSuggestionAsync(sourceKey, relativeKey));
            }

            // Parallel key
            var parallelKey = GetParallelKey(sourceKey);
            if (parallelKey != null)
            {
                suggestions.Add(await GetModulationSuggestionAsync(sourceKey, parallelKey));
            }

            // Dominant key
            var dominantKey = GetDominantKey(sourceKey);
            if (dominantKey != null)
            {
                suggestions.Add(await GetModulationSuggestionAsync(sourceKey, dominantKey));
            }

            // Subdominant key
            var subdominantKey = GetSubdominantKey(sourceKey);
            if (subdominantKey != null)
            {
                suggestions.Add(await GetModulationSuggestionAsync(sourceKey, subdominantKey));
            }

            return suggestions.OrderBy(s => s.Difficulty).ToList();
        });
    }

    public IEnumerable<PivotChord> FindPivotChords(Key sourceKey, Key targetKey)
    {
        // Get diatonic chords for both keys
        var sourceChords = GetDiatonicChordsForKey(sourceKey);
        var targetChords = GetDiatonicChordsForKey(targetKey);

        // Find chords that appear in both keys
        var pivotChords = new List<PivotChord>();

        foreach (var sourceChord in sourceChords)
        {
            foreach (var targetChord in targetChords)
            {
                // Check if same chord (same root and quality)
                if (AreSameChord(sourceChord.Root, sourceChord.Template, targetChord.Root, targetChord.Template))
                {
                    pivotChords.Add(new PivotChord
                    {
                        Template = sourceChord.Template,
                        Root = sourceChord.Root,
                        ChordName = sourceChord.ContextualName,
                        DegreeInSourceKey = sourceChord.ScaleDegree ?? 0,
                        DegreeInTargetKey = targetChord.ScaleDegree ?? 0,
                        RomanNumeralInSourceKey = sourceChord.RomanNumeral ?? "",
                        RomanNumeralInTargetKey = targetChord.RomanNumeral ?? "",
                        Function = $"{sourceChord.Function} in {sourceKey}, {targetChord.Function} in {targetKey}"
                    });
                }
            }
        }

        return pivotChords;
    }

    #region Private Helper Methods

    private ModulationType DetermineModulationType(Key sourceKey, Key targetKey)
    {
        // Check if parallel FIRST (e.g., C major to C minor)
        // This must come before relative check because parallel keys
        // also share the same key signature in some cases
        if (IsParallelKey(sourceKey, targetKey))
        {
            return ModulationType.Parallel;
        }

        // Check if relative (e.g., C major to A minor)
        if (IsRelativeKey(sourceKey, targetKey))
        {
            return ModulationType.Relative;
        }

        // Check if dominant (e.g., C major to G major)
        if (IsDominantKey(sourceKey, targetKey))
        {
            return ModulationType.Dominant;
        }

        // Check if subdominant (e.g., C major to F major)
        if (IsSubdominantKey(sourceKey, targetKey))
        {
            return ModulationType.Subdominant;
        }

        // Check if supertonic (e.g., C major to D minor)
        if (IsSupertonicKey(sourceKey, targetKey))
        {
            return ModulationType.Supertonic;
        }

        // Check if mediant (e.g., C major to E minor)
        if (IsMediantKey(sourceKey, targetKey))
        {
            return ModulationType.Mediant;
        }

        // Check if submediant (same as relative for major keys)
        if (IsSubmediantKey(sourceKey, targetKey))
        {
            return ModulationType.Submediant;
        }

        // Otherwise chromatic
        return ModulationType.Chromatic;
    }

    private bool IsRelativeKey(Key source, Key target)
    {
        // Relative keys share the same key signature but different tonics
        // C major (0 sharps/flats) <-> A minor (0 sharps/flats)
        return source.KeySignature.Value == target.KeySignature.Value &&
               source.KeyMode != target.KeyMode;
    }

    private bool IsParallelKey(Key source, Key target)
    {
        // Parallel keys have the same tonic but different modes
        // C major <-> C minor
        return source.Root.PitchClass == target.Root.PitchClass &&
               source.KeyMode != target.KeyMode;
    }

    private bool IsDominantKey(Key source, Key target)
    {
        // Dominant key is a perfect fifth above (7 semitones)
        var interval = (target.Root.PitchClass.Value - source.Root.PitchClass.Value + 12) % 12;
        return interval == 7 && source.KeyMode == target.KeyMode;
    }

    private bool IsSubdominantKey(Key source, Key target)
    {
        // Subdominant key is a perfect fifth below (5 semitones up = 7 down)
        var interval = (target.Root.PitchClass.Value - source.Root.PitchClass.Value + 12) % 12;
        return interval == 5 && source.KeyMode == target.KeyMode;
    }

    private bool IsSupertonicKey(Key source, Key target)
    {
        // Supertonic is 2 semitones above
        var interval = (target.Root.PitchClass.Value - source.Root.PitchClass.Value + 12) % 12;
        return interval == 2;
    }

    private bool IsMediantKey(Key source, Key target)
    {
        // Mediant is 4 semitones above (major third)
        var interval = (target.Root.PitchClass.Value - source.Root.PitchClass.Value + 12) % 12;
        return interval == 4;
    }

    private bool IsSubmediantKey(Key source, Key target)
    {
        // Submediant is 9 semitones above (major sixth)
        var interval = (target.Root.PitchClass.Value - source.Root.PitchClass.Value + 12) % 12;
        return interval == 9;
    }

    private Key? GetRelativeKey(Key key)
    {
        // For major keys, relative minor is 3 semitones down
        // For minor keys, relative major is 3 semitones up
        if (key is Key.Major major)
        {
            var minorRoot = PitchClass.FromValue((major.Root.PitchClass.Value - 3 + 12) % 12);
            return Key.Minor.MinorItems.FirstOrDefault(k => k.Root.PitchClass == minorRoot);
        }

        if (key is Key.Minor minor)
        {
            var majorRoot = PitchClass.FromValue((minor.Root.PitchClass.Value + 3) % 12);
            return Key.Major.MajorItems.FirstOrDefault(k => k.Root.PitchClass == majorRoot);
        }

        return null;
    }

    private Key? GetParallelKey(Key key)
    {
        // Same root, different mode
        if (key is Key.Major major)
        {
            return Key.Minor.MinorItems.FirstOrDefault(k => k.Root.PitchClass == major.Root.PitchClass);
        }

        if (key is Key.Minor minor)
        {
            return Key.Major.MajorItems.FirstOrDefault(k => k.Root.PitchClass == minor.Root.PitchClass);
        }

        return null;
    }

    private Key? GetDominantKey(Key key)
    {
        // Perfect fifth above (7 semitones)
        var dominantRoot = PitchClass.FromValue((key.Root.PitchClass.Value + 7) % 12);

        if (key is Key.Major)
        {
            return Key.Major.MajorItems.FirstOrDefault(k => k.Root.PitchClass == dominantRoot);
        }

        return Key.Minor.MinorItems.FirstOrDefault(k => k.Root.PitchClass == dominantRoot);
    }

    private Key? GetSubdominantKey(Key key)
    {
        // Perfect fifth below (5 semitones up)
        var subdominantRoot = PitchClass.FromValue((key.Root.PitchClass.Value + 5) % 12);

        if (key is Key.Major)
        {
            return Key.Major.MajorItems.FirstOrDefault(k => k.Root.PitchClass == subdominantRoot);
        }

        return Key.Minor.MinorItems.FirstOrDefault(k => k.Root.PitchClass == subdominantRoot);
    }

    private double CalculateDifficulty(ModulationType type, int pivotChordCount)
    {
        // Base difficulty by modulation type
        var baseDifficulty = type switch
        {
            ModulationType.Relative => 0.1, // Easiest - shares all notes
            ModulationType.Dominant => 0.2, // Very common
            ModulationType.Subdominant => 0.2, // Very common
            ModulationType.Parallel => 0.3, // Common but more dramatic
            ModulationType.Supertonic => 0.5, // Less common
            ModulationType.Mediant => 0.6, // Less common
            ModulationType.Submediant => 0.4, // Moderate
            ModulationType.Chromatic => 0.9, // Most difficult
            _ => 0.5
        };

        // More pivot chords = easier modulation
        var pivotBonus = Math.Min(pivotChordCount * 0.05, 0.3);

        return Math.Max(0.0, Math.Min(1.0, baseDifficulty - pivotBonus));
    }

    private string GenerateDescription(Key source, Key target, ModulationType type, List<PivotChord> pivotChords)
    {
        var pivotCount = pivotChords.Count;
        var pivotText = pivotCount switch
        {
            0 => "No common chords - use chromatic or direct modulation",
            1 => $"1 pivot chord available: {pivotChords[0].ChordName}",
            _ => $"{pivotCount} pivot chords available"
        };

        return type switch
        {
            ModulationType.Relative =>
                $"Relative modulation from {source} to {target}. Very smooth - shares the same notes. {pivotText}.",
            ModulationType.Parallel =>
                $"Parallel modulation from {source} to {target}. Dramatic mood change while keeping the same tonic. {pivotText}.",
            ModulationType.Dominant =>
                $"Dominant modulation from {source} to {target}. Very common in classical music. {pivotText}.",
            ModulationType.Subdominant =>
                $"Subdominant modulation from {source} to {target}. Common and smooth. {pivotText}.",
            ModulationType.Chromatic =>
                $"Chromatic modulation from {source} to {target}. Distant keys - requires careful voice leading. {pivotText}.",
            _ =>
                $"Modulation from {source} to {target}. {pivotText}."
        };
    }

    private List<string> SuggestProgression(Key source, Key target, ModulationType type, List<PivotChord> pivotChords)
    {
        var progression = new List<string>();

        // Start in source key
        progression.Add($"I in {source}");

        if (pivotChords.Any())
        {
            // Use pivot chord
            var pivot = pivotChords.First();
            progression.Add(
                $"{pivot.ChordName} ({pivot.RomanNumeralInSourceKey} in {source} = {pivot.RomanNumeralInTargetKey} in {target})");

            // Establish target key
            progression.Add($"V in {target}");
            progression.Add($"I in {target}");
        }
        else
        {
            // Direct modulation without pivot
            progression.Add("Direct modulation");
            progression.Add($"I in {target}");
        }

        return progression;
    }

    private List<ChordInContext> GetDiatonicChordsForKey(Key key)
    {
        // Generate diatonic chords synchronously
        var scale = key.KeyMode == KeyMode.Major
            ? MajorScaleMode.Get(MajorScaleDegree.Ionian)
            : MajorScaleMode.Get(MajorScaleDegree.Aeolian);

        var extension = ChordExtension.Seventh;
        var stackingType = ChordStackingType.Tertian;

        var chordTemplates = ChordTemplateFactory.CreateModalChords(scale, extension, stackingType);
        var scaleNotes = key.Notes.ToList();

        var chords = new List<ChordInContext>();

        for (var degree = 1; degree <= scaleNotes.Count; degree++)
        {
            var root = scaleNotes[degree - 1].PitchClass;
            var template = chordTemplates.ElementAt(degree - 1);

            // Analyze in key context
            var keyContext = KeyAwareChordNamingService.AnalyzeInKey(template, root, key);

            chords.Add(new ChordInContext
            {
                Template = template,
                Root = root,
                ContextualName = keyContext.ChordName,
                ScaleDegree = degree,
                Function = keyContext.Function,
                Commonality = keyContext.Probability,
                IsNaturallyOccurring = true,
                AlternateNames = [keyContext.RomanNumeral],
                RomanNumeral = keyContext.RomanNumeral,
                FunctionalDescription = keyContext.FunctionalDescription,
                Context = new MusicalContext
                {
                    Level = ContextLevel.Key,
                    Key = key
                }
            });
        }

        return chords;
    }

    private bool AreSameChord(PitchClass root1, ChordTemplate template1, PitchClass root2, ChordTemplate template2)
    {
        // Same root
        if (root1 != root2)
        {
            return false;
        }

        // Same intervals (chord quality)
        var intervals1 = template1.Intervals.Select(i => i.Interval.Semitones.Value).OrderBy(x => x).ToList();
        var intervals2 = template2.Intervals.Select(i => i.Interval.Semitones.Value).OrderBy(x => x).ToList();

        return intervals1.SequenceEqual(intervals2);
    }

    #endregion
}
