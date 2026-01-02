namespace GA.Business.Core.Fretboard.Shapes;

using System.Runtime.CompilerServices;
using Atonal;
using Atonal.Grothendieck;
using Positions;
using Primitives;
using GA.Business.Core.Atonal;
using GA.Business.Core.Atonal.Primitives;
using GA.Business.Core.Fretboard.Shapes.Geometry;

/// <summary>
/// Builds fretboard shape graphs with transitions
/// </summary>
public class ShapeGraphBuilder(
    IGrothendieckService grothendieckService)
    : IShapeGraphBuilder
{
    /// <inheritdoc />
    public IEnumerable<FretboardShape> GenerateShapes(
        Tuning tuning,
        PitchClassSet pitchClassSet,
        ShapeGraphBuildOptions options)
    {
        var shapes = new List<FretboardShape>();
        var fretboard = new Fretboard(tuning, options.MaxFret);

        // Generate all possible fingerings for this pitch-class set
        for (var baseFret = 0; baseFret <= options.MaxFret - options.MaxSpan; baseFret++)
        {
            var positions = GeneratePositionsForPitchClassSet(
                fretboard,
                pitchClassSet,
                baseFret,
                options);

            if (positions.Any())
            {
                var shape = CreateShape(pitchClassSet, positions, tuning);
                if (shape.Ergonomics >= options.MinErgonomics && shape.Span <= options.MaxSpan)
                {
                    shapes.Add(shape);
                }
            }
        }

        // Limit shapes per set
        return shapes
            .OrderByDescending(s => s.Ergonomics)
            .Take(options.MaxShapesPerSet);
    }

    /// <inheritdoc />
    public async IAsyncEnumerable<FretboardShape> GenerateShapesStreamAsync(
        Tuning tuning,
        PitchClassSet pitchClassSet,
        ShapeGraphBuildOptions options,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await Task.Yield(); // Make it truly async

        foreach (var shape in GenerateShapes(tuning, pitchClassSet, options))
        {
            cancellationToken.ThrowIfCancellationRequested();
            yield return shape;
        }
    }

    /// <inheritdoc />
    public Task<ShapeGraph> BuildGraphAsync(
        Tuning tuning,
        IEnumerable<PitchClassSet> pitchClassSets,
        ShapeGraphBuildOptions options)
    {
        // Generate all shapes
        var allShapes = new List<FretboardShape>();
        foreach (var pcs in pitchClassSets)
        {
            var shapes = GenerateShapes(tuning, pcs, options);
            allShapes.AddRange(shapes);
        }

        // Build shape dictionary
        var shapesDict = allShapes.ToImmutableDictionary(s => s.Id);

        // Build adjacency list with transitions
        var adjacency = new Dictionary<string, ImmutableList<ShapeTransition>>();
        foreach (var fromShape in allShapes)
        {
            var transitions = new List<ShapeTransition>();

            foreach (var toShape in allShapes)
            {
                if (fromShape.Id == toShape.Id) continue;

                var transition = CreateTransition(fromShape, toShape, options);
                if (transition != null)
                {
                    transitions.Add(transition);
                }
            }

            adjacency[fromShape.Id] = [.. transitions];
        }

        var result = new ShapeGraph
        {
            TuningId = tuning.ToString(),
            Shapes = shapesDict,
            Adjacency = adjacency.ToImmutableDictionary()
        };

        return Task.FromResult(result);
    }

    private List<PositionLocation> GeneratePositionsForPitchClassSet(
        Fretboard fretboard,
        PitchClassSet pitchClassSet,
        int baseFret,
        ShapeGraphBuildOptions options)
    {
        var positions = new List<PositionLocation>();

        // Simple implementation: try to find one note per string
        for (var stringIndex = 0; stringIndex < fretboard.StringCount; stringIndex++)
        {
            for (var fret = baseFret; fret <= Math.Min(baseFret + options.MaxSpan, options.MaxFret); fret++)
            {
                var note = fretboard.GetNote(stringIndex, fret);

                if (pitchClassSet.Contains(note.PitchClass))
                {
                    var position = new PositionLocation(Str.FromValue(stringIndex + 1), Fret.FromValue(fret));
                    positions.Add(position);
                    break; // One note per string
                }
            }
        }

        return positions;
    }

    private FretboardShape CreateShape(
        PitchClassSet pitchClassSet,
        List<PositionLocation> positions,
        Tuning tuning)
    {
        var tuningId = tuning.ToString();
        var positionsList = positions.ToImmutableList();

        var frettedPositions = positions.Where(p => !p.IsOpen).ToList();
        var minFret = frettedPositions.Any() ? frettedPositions.Min(p => p.Fret.Value) : 0;
        var maxFret = positions.Any() ? positions.Max(p => p.Fret.Value) : 0;

        var diagness = FretboardShape.ComputeDiagness(positions);
        var ergonomics = ComputeErgonomics(positions, maxFret - minFret);
        var fingerCount = EstimateFingerCount(positions);

        return new FretboardShape
        {
            Id = FretboardShape.GenerateId(tuningId, positionsList),
            TuningId = tuningId,
            PitchClassSet = pitchClassSet,
            Icv = pitchClassSet.IntervalClassVector,
            Positions = positionsList,
            StringMask = FretboardShape.ComputeStringMask(positionsList),
            MinFret = minFret,
            MaxFret = maxFret,
            Diagness = diagness,
            Ergonomics = ergonomics,
            FingerCount = fingerCount
        };
    }

    private double ComputeErgonomics(List<PositionLocation> positions, int span)
    {
        if (!positions.Any()) return 0.0;

        // Simple ergonomics: prefer smaller spans and lower frets
        var avgFret = positions.Average(p => p.Fret.Value);
        var spanPenalty = span / 5.0; // Normalize to 0-1
        var fretPenalty = avgFret / 12.0; // Normalize to 0-1

        return Math.Max(0.0, 1.0 - (spanPenalty + fretPenalty) / 2.0);
    }

    private static int EstimateFingerCount(List<PositionLocation> positions)
    {
        // Simple heuristic: count unique frets (excluding open strings)
        var uniqueFrets = positions.Where(p => !p.IsOpen).Select(p => p.Fret.Value).Distinct().Count();
        return Math.Min(uniqueFrets, 4); // Max 4 fingers
    }

    private ShapeTransition? CreateTransition(
        FretboardShape fromShape,
        FretboardShape toShape,
        ShapeGraphBuildOptions options)
    {
        // Compute harmonic cost using Grothendieck service
        var delta = grothendieckService.ComputeDelta(
            fromShape.PitchClassSet.IntervalClassVector,
            toShape.PitchClassSet.IntervalClassVector);

        var harmonicCost = grothendieckService.ComputeHarmonicCost(delta);

        if (harmonicCost > options.MaxHarmonicDistance)
        {
            return null;
        }

        // Compute physical cost (position shift)
        var physicalCost = ShapeTransition.ComputePhysicalCost(fromShape, toShape);

        if (physicalCost > options.MaxPhysicalCost)
        {
            return null;
        }

        // Optional: Compute OPTIC voice-leading cost (Tymoczko) between set classes
        // Only influences extended score/weight if VoiceLeadingWeight > 0.
        double vlCost = 0.0;
        double vlWeightUsed = 0.0;
        if (options.VoiceLeadingWeight > 0)
        {
            try
            {
                var vlOptions = new VoiceLeadingOptions
                {
                    OctaveEquivalence = options.VlOctaveEquivalence,
                    PermutationEquivalence = options.VlPermutationEquivalence,
                    TranspositionEquivalence = options.VlTranspositionEquivalence,
                    InversionEquivalence = options.VlInversionEquivalence
                };

                var scFrom = new SetClass(new PitchClassSet(fromShape.PitchClassSet));
                var scTo = new SetClass(new PitchClassSet(toShape.PitchClassSet));
                vlCost = SetClassOpticIndex.Distance(scFrom, scTo, vlOptions);
                vlWeightUsed = options.VoiceLeadingWeight;
            }
            catch
            {
                // Fallback silently if anything goes wrong
                vlCost = 0.0;
                vlWeightUsed = 0.0;
            }
        }

        return new ShapeTransition
        {
            FromId = fromShape.Id,
            ToId = toShape.Id,
            Delta = delta,
            HarmonicCost = harmonicCost,
            PhysicalCost = physicalCost,
            VoiceLeadingCost = vlCost,
            VoiceLeadingWeightUsed = vlWeightUsed
        };
    }
}
