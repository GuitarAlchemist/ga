namespace GA.Business.Core.Fretboard.Voicings.Filtering;

using Analysis;
using Core;
using Primitives;
using Tonal;
using Generation;

/// <summary>
/// Extension methods for filtering voicings by musical key context
/// </summary>
/// <remarks>
/// These filters operate on the generated voicing stream, allowing key-aware
/// filtering without the architectural problems of key-first generation.
///
/// This approach maintains:
/// - Clean separation of concerns (generation vs. analysis)
/// - Complete voicing coverage (no missed chromatic/atonal voicings)
/// - Computational efficiency (filter after generation, not during)
/// - Multi-key awareness (voicings can match multiple keys)
/// </remarks>
public static class VoicingKeyFilters
{
    /// <summary>
    /// Filters voicings to only those that contain notes from the specified key
    /// </summary>
    /// <param name="voicings">Stream of voicings to filter</param>
    /// <param name="key">The musical key to filter by</param>
    /// <param name="strictDiatonic">If true, ALL notes must be in the key. If false, at least one note must be in the key.</param>
    /// <returns>Filtered stream of voicings</returns>
    public static async IAsyncEnumerable<Voicing> FilterByKey(
        this IAsyncEnumerable<Voicing> voicings,
        Key key,
        bool strictDiatonic = false)
    {
        var keyPitchClasses = key.PitchClassSet.ToHashSet();

        await foreach (var voicing in voicings)
        {
            var voicingPitchClasses = voicing.Notes
                .Select(n => n.PitchClass)
                .ToHashSet();

            var matches = strictDiatonic
                ? voicingPitchClasses.IsSubsetOf(keyPitchClasses)  // All notes in key
                : voicingPitchClasses.Overlaps(keyPitchClasses);   // At least one note in key

            if (matches)
            {
                yield return voicing;
            }
        }
    }

    /// <summary>
    /// Filters voicings to only those that are strictly diatonic to the specified key
    /// (all notes must be in the key's scale)
    /// </summary>
    public static IAsyncEnumerable<Voicing> FilterByKeyStrictDiatonic(
        this IAsyncEnumerable<Voicing> voicings,
        Key key)
    {
        return voicings.FilterByKey(key, strictDiatonic: true);
    }

    /// <summary>
    /// Filters voicings to only those that contain chromatic alterations relative to the key
    /// (at least one note outside the key's scale)
    /// </summary>
    public static async IAsyncEnumerable<Voicing> FilterByKeyChromatic(
        this IAsyncEnumerable<Voicing> voicings,
        Key key)
    {
        var keyPitchClasses = key.PitchClassSet.ToHashSet();

        await foreach (var voicing in voicings)
        {
            var voicingPitchClasses = voicing.Notes
                .Select(n => n.PitchClass)
                .ToHashSet();

            // Has at least one note outside the key
            var hasChromaticNote = voicingPitchClasses.Any(pc => !keyPitchClasses.Contains(pc));

            if (hasChromaticNote)
            {
                yield return voicing;
            }
        }
    }

    /// <summary>
    /// Groups voicings by their primary (closest matching) key
    /// </summary>
    /// <param name="voicings">Stream of voicings to group</param>
    /// <returns>Dictionary mapping each key to its matching voicings</returns>
    public static async Task<Dictionary<Key, List<Voicing>>> GroupByPrimaryKey(
        this IAsyncEnumerable<Voicing> voicings)
    {
        var grouped = new Dictionary<Key, List<Voicing>>();

        await foreach (var voicing in voicings)
        {
            var analysis = VoicingAnalyzer.Analyze(voicing);
            var primaryKey = analysis.ChordId.ClosestKey;

            if (primaryKey != null)
            {
                if (!grouped.ContainsKey(primaryKey))
                {
                    grouped[primaryKey] = new List<Voicing>();
                }
                grouped[primaryKey].Add(voicing);
            }
        }

        return grouped;
    }

    /// <summary>
    /// Finds all keys that contain the voicing (diatonic or chromatic)
    /// </summary>
    /// <param name="voicing">The voicing to analyze</param>
    /// <param name="allowChromatic">If true, includes keys where voicing has chromatic notes</param>
    /// <returns>List of matching keys with their match quality</returns>
    public static List<KeyMatch> FindMatchingKeys(
        Voicing voicing,
        bool allowChromatic = true)
    {
        var voicingPitchClasses = voicing.Notes
            .Select(n => n.PitchClass)
            .ToHashSet();

        var matches = new List<KeyMatch>();

        foreach (var key in Key.Items)
        {
            var keyPitchClasses = key.PitchClassSet.ToHashSet();

            var commonNotes = voicingPitchClasses.Intersect(keyPitchClasses).Count();
            var totalNotes = voicingPitchClasses.Count;
            var chromaticNotes = voicingPitchClasses.Except(keyPitchClasses).Count();

            var isDiatonic = chromaticNotes == 0;
            var matchQuality = (double)commonNotes / totalNotes;

            if (isDiatonic || (allowChromatic && commonNotes > 0))
            {
                matches.Add(new KeyMatch(
                    key,
                    isDiatonic,
                    matchQuality,
                    commonNotes,
                    chromaticNotes
                ));
            }
        }

        // Sort by match quality (best matches first)
        return [.. matches.OrderByDescending(m => m.MatchQuality).ThenBy(m => m.ChromaticNotes)];
    }

    /// <summary>
    /// Convenience method: Generate all voicings for a specific key
    /// </summary>
    /// <param name="fretboard">The fretboard to generate voicings on</param>
    /// <param name="key">The musical key to filter by</param>
    /// <param name="strictDiatonic">If true, only strictly diatonic voicings. If false, allows chromatic notes.</param>
    /// <param name="windowSize">Size of the sliding window in frets</param>
    /// <param name="minPlayedNotes">Minimum number of played notes</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Async enumerable stream of voicings in the specified key</returns>
    public static IAsyncEnumerable<Voicing> GenerateForKey(
        Fretboard fretboard,
        Key key,
        bool strictDiatonic = false,
        int windowSize = 4,
        int minPlayedNotes = 2,
        CancellationToken cancellationToken = default)
    {
        return VoicingGenerator.GenerateAllVoicingsAsync(
                fretboard,
                windowSize,
                minPlayedNotes,
                parallel: true,
                cancellationToken)
            .FilterByKey(key, strictDiatonic);
    }
}

/// <summary>
/// Represents a match between a voicing and a musical key
/// </summary>
/// <param name="Key">The matching key</param>
/// <param name="IsDiatonic">True if all notes are in the key (no chromatic alterations)</param>
/// <param name="MatchQuality">Percentage of voicing notes that are in the key (0.0 to 1.0)</param>
/// <param name="CommonNotes">Number of notes in both the voicing and the key</param>
/// <param name="ChromaticNotes">Number of notes in the voicing but not in the key</param>
public record KeyMatch(
    Key Key,
    bool IsDiatonic,
    double MatchQuality,
    int CommonNotes,
    int ChromaticNotes)
{
    public override string ToString()
    {
        var diatonicStr = IsDiatonic ? "diatonic" : $"chromatic ({ChromaticNotes} altered)";
        return $"{Key} - {MatchQuality:P0} match ({diatonicStr})";
    }
}

