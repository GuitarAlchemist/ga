namespace GaApi.GraphQL.Queries;

using System.Collections.Immutable;
using GA.Business.Core.Fretboard.Positions;
using GA.Business.Core.Fretboard.Primitives;
using GA.Business.Core.Notes.Primitives;
using Types;

/// <summary>
///     GraphQL queries for fretboard analysis
/// </summary>
[ExtendObjectType("Query")]
public class FretboardQuery
{
    /// <summary>
    ///     Analyze all chord positions within a 5-fret span
    /// </summary>
    public FretSpanAnalysisResult AnalyzeFretSpan(FretSpanInput input)
    {
        var fretboard = Fretboard.Default;
        var startFret = Math.Max(0, input.StartFret);
        var endFret = Math.Min(24, input.EndFret);
        var maxFret = Math.Max(endFret, startFret + 5);

        // Generate all chord analyses
        var allChords = GenerateAllFiveFretSpanChords(
                fretboard,
                maxFret,
                input.IncludeBiomechanicalAnalysis)
            .Where(c => c.LowestFret >= startFret && c.HighestFret <= endFret);

        // Apply difficulty filter if specified
        if (!string.IsNullOrEmpty(input.DifficultyFilter) &&
            Enum.TryParse<ChordDifficulty>(input.DifficultyFilter, out var difficulty))
        {
            allChords = allChords.Where(c => c.Difficulty == difficulty);
        }

        // Apply max results limit
        if (input.MaxResults.HasValue)
        {
            allChords = allChords.Take(input.MaxResults.Value);
        }

        var chordsList = allChords.ToList();
        var chordTypes = chordsList
            .Select(c => FretboardChordAnalysisType.FromAnalysis(c, input.IncludePhysicalAnalysis)).ToList();

        // Calculate distributions
        var difficultyDistribution = chordsList
            .GroupBy(c => c.Difficulty.ToString())
            .ToDictionary(g => g.Key, g => g.Count());

        var cagedDistribution = chordsList
            .Where(c => c.CagedAnalysis?.ClosestShape != null)
            .GroupBy(c => c.CagedAnalysis!.ClosestShape!.ToString())
            .ToDictionary(g => g.Key, g => g.Count());

        return new FretSpanAnalysisResult
        {
            StartFret = startFret,
            EndFret = endFret,
            TotalChords = chordsList.Count,
            Chords = chordTypes,
            DifficultyDistribution = difficultyDistribution,
            CagedShapeDistribution = cagedDistribution
        };
    }

    /// <summary>
    ///     Get chord position by specific fret pattern
    /// </summary>
    public FretboardChordAnalysisType? GetChordByPattern(
        [GraphQLDescription("Fret pattern as array of 6 integers (-1 for muted, 0+ for fret number)")]
        List<int> fretPattern,
        bool includePhysicalAnalysis = false)
    {
        if (fretPattern.Count != 6)
        {
            throw new ArgumentException("Fret pattern must have exactly 6 values (one per string)");
        }

        var fretboard = Fretboard.Default;
        var positions = ConvertFretPatternToPositions(fretPattern);

        try
        {
            var analysis = AnalyzeChordVoicing(
                positions,
                fretboard);

            return FretboardChordAnalysisType.FromAnalysis(analysis, includePhysicalAnalysis);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    ///     Search chords by name
    /// </summary>
    public List<FretboardChordAnalysisType> SearchChordsByName(
        string searchTerm,
        int maxResults = 50,
        bool includePhysicalAnalysis = false)
    {
        var fretboard = Fretboard.Default;

        // Generate chords from first 12 frets (most common positions)
        var allChords = GenerateAllFiveFretSpanChords(fretboard, 12)
            .Where(c => c.ChordName.Contains(searchTerm, StringComparison.OrdinalIgnoreCase) ||
                        (c.IconicName?.Contains(searchTerm, StringComparison.OrdinalIgnoreCase) ?? false))
            .Take(maxResults)
            .Select(c => FretboardChordAnalysisType.FromAnalysis(c, includePhysicalAnalysis))
            .ToList();

        return allChords;
    }

    /// <summary>
    ///     Get chord equivalence groups for a fret span
    /// </summary>
    public List<ChordEquivalenceGroup> GetEquivalenceGroups(
        int startFret = 0,
        int endFret = 5,
        int maxGroups = 20,
        bool includePhysicalAnalysis = false)
    {
        var fretboard = Fretboard.Default;

        var chords = GenerateAllFiveFretSpanChords(fretboard, endFret)
            .Where(c => c.LowestFret >= startFret && c.HighestFret <= endFret)
            .ToList();

        // Group by pattern ID (equivalence)
        var groups = chords
            .GroupBy(c => c.Invariant.PatternId.ToPatternString())
            .OrderByDescending(g => g.Count())
            .Take(maxGroups)
            .Select(g => new ChordEquivalenceGroup
            {
                PatternId = g.Key,
                ChordCount = g.Count(),
                RepresentativeChord = FretboardChordAnalysisType.FromAnalysis(g.First(), includePhysicalAnalysis),
                Variations = g.Select(c => FretboardChordAnalysisType.FromAnalysis(c, includePhysicalAnalysis)).ToList()
            })
            .ToList();

        return groups;
    }

    private static ImmutableList<Position> ConvertFretPatternToPositions(List<int> fretPattern)
    {
        var positions = new List<Position>();
        var fretboard = Fretboard.Default;

        for (var i = 0; i < fretPattern.Count; i++)
        {
            var fretValue = fretPattern[i];
            var str = Str.FromValue(i + 1);

            if (fretValue < 0)
            {
                positions.Add(new Position.Muted(str));
            }
            else
            {
                var fret = Fret.FromValue(fretValue);
                var location = new PositionLocation(str, fret);
                var midiNote = fretboard.Tuning[str].MidiNote + fretValue;
                positions.Add(new Position.Played(location, MidiNote.FromValue(midiNote)));
            }
        }

        return positions.ToImmutableList();
    }
}

/// <summary>
///     Chord equivalence group result
/// </summary>
public class ChordEquivalenceGroup
{
    public string PatternId { get; set; } = "";
    public int ChordCount { get; set; }
    public FretboardChordAnalysisType RepresentativeChord { get; set; } = new();
    public List<FretboardChordAnalysisType> Variations { get; set; } = [];
}
