namespace GaApi.GraphQL.Queries;

using GA.Business.Core.Chords;
using GA.Business.Core.Atonal;
using GA.Business.Core.Intervals;
using GA.Business.Core.Intervals.Primitives;
using Microsoft.Extensions.Logging;
using HotChocolate;
using HotChocolate.Types;

[ExtendObjectType(OperationTypeNames.Query)]
[GraphQLDescription("GraphQL query endpoints for chord naming using the unified naming façade.")]
public sealed class ChordNamingQuery
{
    private readonly IChordNamingService _naming;
    private readonly ILogger<ChordNamingQuery> _logger;

    public ChordNamingQuery(IChordNamingService naming, ILogger<ChordNamingQuery> logger)
    {
        _naming = naming;
        _logger = logger;
    }

    [GraphQLDescription("Returns the best chord name for the given root and a list of semitone intervals above the root. Example: root=C(0), intervals=[4,7,10] → 'C7' (depending on policies). Root and bass are pitch classes (0-11). Intervals are semitone offsets above the root.")]
    public string ChordBestName(
        string formulaName,
        int root,
        IEnumerable<int> intervals,
        int? bass = null)
    {
        ValidateInputs(formulaName, root, intervals, bass);
        var pcs = PitchClass.FromValue(NormalizePc(root));
        var bassPc = bass.HasValue ? PitchClass.FromValue(NormalizePc(bass.Value)) : (PitchClass?)null;
        var chordIntervals = ToChordFormulaIntervals(intervals);

        var name = _naming.GetBestChordName(chordIntervals, formulaName, pcs, bassPc);
        return name;
    }

    [GraphQLDescription("Returns all reasonable naming options for the chord defined by the given intervals. Root and bass are pitch classes (0-11). Intervals are semitone offsets above the root.")]
    public IEnumerable<string> ChordAllNames(
        string formulaName,
        int root,
        IEnumerable<int> intervals,
        int? bass = null)
    {
        ValidateInputs(formulaName, root, intervals, bass);
        var pcs = PitchClass.FromValue(NormalizePc(root));
        var bassPc = bass.HasValue ? PitchClass.FromValue(NormalizePc(bass.Value)) : (PitchClass?)null;
        var chordIntervals = ToChordFormulaIntervals(intervals);

        return _naming.GetAllNamingOptions(chordIntervals, formulaName, pcs, bassPc);
    }

    [GraphQLDescription("Returns a comprehensive naming object (primary name, alternates, enharmonic, quartal, key-aware, etc.). Root and bass are pitch classes (0-11). Intervals are semitone offsets above the root.")]
    public ChordTemplateNamingService.ComprehensiveChordName ChordComprehensiveNames(
        string formulaName,
        int root,
        IEnumerable<int> intervals,
        int? bass = null)
    {
        ValidateInputs(formulaName, root, intervals, bass);
        var pcs = PitchClass.FromValue(NormalizePc(root));
        var bassPc = bass.HasValue ? PitchClass.FromValue(NormalizePc(bass.Value)) : (PitchClass?)null;
        var chordIntervals = ToChordFormulaIntervals(intervals);

        return _naming.GenerateComprehensiveNames(chordIntervals, formulaName, pcs, bassPc);
    }

    private static int NormalizePc(int x)
        => ((x % 12) + 12) % 12;

    private static IReadOnlyCollection<ChordFormulaInterval> ToChordFormulaIntervals(IEnumerable<int> semitoneValues)
    {
        var list = new List<ChordFormulaInterval>();
        foreach (var s in semitoneValues)
        {
            var semis = ((s % 24) + 24) % 24; // support 0..23 defensively
            var interval = new Interval.Chromatic(Semitones.FromValue(semis));
            var function = MapFunction(semis);
            list.Add(new ChordFormulaInterval(interval, function));
        }
        return list;
    }

    // Heuristic mapping consistent with core logic used elsewhere.
    private static ChordFunction MapFunction(int semitones)
        => semitones switch
        {
            2 or 14 => ChordFunction.Ninth,
            3 or 4 => ChordFunction.Third,
            5 or 17 => ChordFunction.Eleventh,
            7 => ChordFunction.Fifth,
            9 or 21 => ChordFunction.Thirteenth,
            10 or 11 => ChordFunction.Seventh,
            _ => ChordFunction.Root
        };

    private void ValidateInputs(string formulaName, int root, IEnumerable<int> intervals, int? bass)
    {
        if (string.IsNullOrWhiteSpace(formulaName))
        {
            _logger.LogWarning("Chord naming called with empty formulaName");
            throw new GraphQLException(ErrorBuilder.New()
                .SetMessage("formulaName must be a non-empty string.")
                .Build());
        }

        if (intervals is null)
        {
            throw new GraphQLException(ErrorBuilder.New()
                .SetMessage("intervals must not be null.")
                .Build());
        }

        var list = intervals.ToList();
        if (list.Count == 0)
        {
            throw new GraphQLException(ErrorBuilder.New()
                .SetMessage("intervals must contain at least one semitone value.")
                .Build());
        }

        bool InRange(int v) => v >= 0 && v <= 11;
        if (!InRange(root))
        {
            throw new GraphQLException(ErrorBuilder.New()
                .SetMessage("root must be a pitch class in the range 0..11.")
                .Build());
        }

        if (bass.HasValue && !InRange(bass.Value))
        {
            throw new GraphQLException(ErrorBuilder.New()
                .SetMessage("bass must be a pitch class in the range 0..11 when provided.")
                .Build());
        }
    }
}
