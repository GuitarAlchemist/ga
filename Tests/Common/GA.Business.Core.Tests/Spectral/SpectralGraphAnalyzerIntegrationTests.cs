namespace GA.Business.Core.Tests.Spectral;

using System.Collections.Immutable;
using GA.Domain.Core.Instruments.Positions;
using GA.Domain.Core.Instruments.Primitives;
using GA.Domain.Core.Theory.Atonal;
using GA.Domain.Services.Fretboard.Shapes;
using GA.Domain.Services.Fretboard.Shapes.Spectral;
using GA.Domain.Services.Atonal.Grothendieck;
using NUnit.Framework;

[TestFixture]
public class SpectralGraphAnalyzerIntegrationTests
{
    [Test]
    public void Analyze_ReturnsOrderedEigenvaluesAndMetrics()
    {
        var graph = BuildSimpleGraph();
        var analyzer = new SpectralGraphAnalyzer();
        var metrics = analyzer.Analyze(graph);
        Assert.That(metrics.Eigenvalues.Length, Is.EqualTo(graph.ShapeCount));
        Assert.That(metrics.Eigenvalues[0], Is.EqualTo(0).Within(1e-9));
        Assert.That(metrics.Eigenvalues.Max(), Is.GreaterThan(0));
    }
    [Test]
    public void Cluster_ReturnsFamiliesForConnectedGraph()
    {
        var graph = BuildSimpleGraph();
        var analyzer = new SpectralGraphAnalyzer();
        var families = analyzer.Cluster(graph, 2);
        Assert.That(families.Count, Is.GreaterThan(0));
        Assert.That(families.All(f => f.ShapeIds.Count > 0), Is.True);
    }
    private static ShapeGraph BuildSimpleGraph()
    {
        var shapes = new Dictionary<string, FretboardShape>
        {
            ["shape-1"] = CreateShape("shape-1", [0, 4, 7]),
            ["shape-2"] = CreateShape("shape-2", [0, 3, 7]),
            ["shape-3"] = CreateShape("shape-3", [0, 4, 9])
        };
        var adjacency = new Dictionary<string, ImmutableList<ShapeTransition>>
        {
            ["shape-1"] = [new ShapeTransition
            {
                FromId = "shape-1",
                ToId = "shape-2",
                Delta = GrothendieckDelta.FromIcVs(shapes["shape-1"].Icv, shapes["shape-2"].Icv),
                HarmonicCost = 1,
                PhysicalCost = 0.5
            }],
            ["shape-2"] = [new ShapeTransition
            {
                FromId = "shape-2",
                ToId = "shape-3",
                Delta = GrothendieckDelta.FromIcVs(shapes["shape-2"].Icv, shapes["shape-3"].Icv),
                HarmonicCost = 0.5,
                PhysicalCost = 0.4
            }],
            ["shape-3"] = []
        };
        return new ShapeGraph
        {
            TuningId = "standard-6-string",
            Shapes = shapes.ToImmutableDictionary(),
            Adjacency = adjacency.ToImmutableDictionary()
        };
    }
    private static FretboardShape CreateShape(string id, int[] pitchClasses)
    {
        var pcs = pitchClasses.Select(PitchClass.FromValue).ToList();
        var pitchClassSet = new PitchClassSet(pcs);
        var positions = new[]
        {
            new PositionLocation(Str.FromValue(1), Fret.Open),
            new PositionLocation(Str.FromValue(2), Fret.FromValue(pitchClasses[1] % 12)),
            new PositionLocation(Str.FromValue(3), Fret.FromValue(pitchClasses[^1] % 12))
        };
        return new FretboardShape
        {
            Id = id,
            TuningId = "standard-6-string",
            PitchClassSet = pitchClassSet,
            Icv = pitchClassSet.IntervalClassVector,
            Positions = positions,
            StringMask = FretboardShape.ComputeStringMask(positions),
            MinFret = 0,
            MaxFret = 5,
            Diagness = 0.2,
            Ergonomics = 0.8,
            FingerCount = 3
        };
    }
}