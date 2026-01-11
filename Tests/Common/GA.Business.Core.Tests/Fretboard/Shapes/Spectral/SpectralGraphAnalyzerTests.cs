namespace GA.Business.Core.Tests.Fretboard.Shapes.Spectral;

using GA.Business.Core.Atonal;
using GA.Business.Core.Atonal.Grothendieck;
using GA.Business.Core.Fretboard.Shapes;
using GA.Business.Core.Fretboard.Shapes.Spectral;
using GA.Business.Core.Atonal.Primitives; // For PitchClassSet
using Moq;
using NUnit.Framework;
using System.Linq;
using System.Threading.Tasks;

[TestFixture]
public class SpectralGraphAnalyzerTests
{
    private SpectralGraphAnalyzer _analyzer;
    private Mock<IGrothendieckService> _grothendieckMock;
    private ShapeGraphBuilder _builder;
    private Tuning _standardTuning;

    [SetUp]
    public void SetUp()
    {
        _grothendieckMock = new Mock<IGrothendieckService>();
        _analyzer = new SpectralGraphAnalyzer();
        _builder = new ShapeGraphBuilder(_grothendieckMock.Object);
        _standardTuning = Tuning.Default;
    }

    [Test]
    public async Task ShouldComputeSpectralMetrics_ForSimpleGraph()
    {
        // Arrange: Build a small graph
        var pitchClassSets = new[]
        {
            PitchClassSet.Parse("047"), // C Major
            PitchClassSet.Parse("037"), // C Minor
            PitchClassSet.Parse("048") // C Augmented
        };

        var options = new ShapeGraphBuildOptions
        {
            MaxFret = 12,
            MaxSpan = 5,
            MaxShapesPerSet = 3
        };

        // Mock Grothendieck service
        _grothendieckMock
            .Setup(g => g.ComputeDelta(It.IsAny<IntervalClassVector>(), It.IsAny<IntervalClassVector>()))
            .Returns(new GrothendieckDelta { Ic1 = 1, Ic2 = 0, Ic3 = 0, Ic4 = 0, Ic5 = 0, Ic6 = 0 });

        _grothendieckMock
            .Setup(g => g.ComputeHarmonicCost(It.IsAny<GrothendieckDelta>()))
            .Returns(1.0);

        var graph = await _builder.BuildGraphAsync(_standardTuning, pitchClassSets, options);

        // Act
        var metrics = _analyzer.Analyze(graph);

        // Assert
        Assert.That(metrics, Is.Not.Null);
        Assert.That(metrics.NodeCount, Is.EqualTo(graph.ShapeCount));
        Assert.That(metrics.Eigenvalues, Is.Not.Empty);
        Assert.That(metrics.Eigenvalues.Length, Is.EqualTo(graph.ShapeCount));

        // First eigenvalue should be ~0 for connected graph (if calculated correctly)
        // With mocked simple graph, it might not be fully connected if transitions fail?
        // But we mocked cost to 1.0, so valid transitions should exist if positions match.
        // Assuming ShapeGraphBuilder creates edges.
        Assert.That(metrics.Lambda1, Is.LessThan(1e-6));
    }

    [Test]
    public async Task ShouldFindCentralShapes()
    {
        // Arrange
        var pitchClassSets = new[]
        {
            PitchClassSet.Parse("047"),
            PitchClassSet.Parse("037"),
            PitchClassSet.Parse("048")
        };

        var options = new ShapeGraphBuildOptions { MaxFret = 12, MaxShapesPerSet = 5 };

        _grothendieckMock
            .Setup(g => g.ComputeDelta(It.IsAny<IntervalClassVector>(), It.IsAny<IntervalClassVector>()))
            .Returns(new GrothendieckDelta { Ic1 = 0, Ic2 = 0, Ic3 = 0, Ic4 = 0, Ic5 = 0, Ic6 = 0 });

        _grothendieckMock
            .Setup(g => g.ComputeHarmonicCost(It.IsAny<GrothendieckDelta>()))
            .Returns(1.0);

        var graph = await _builder.BuildGraphAsync(_standardTuning, pitchClassSets, options);

        // Act
        var centralShapes = _analyzer.FindCentralShapes(graph, topK: 5);

        // Assert
        Assert.That(centralShapes, Is.Not.Empty);
        Assert.That(centralShapes.Count, Is.LessThanOrEqualTo(5));
        
        // Should be sorted by centrality (descending)
        for (var i = 1; i < centralShapes.Count; i++)
        {
            Assert.That(centralShapes[i].Score, Is.LessThanOrEqualTo(centralShapes[i - 1].Score));
        }
    }

    [Test]
    public async Task ShouldClusterShapes()
    {
        // Arrange
        // We need enough shapes for clustering
        var pitchClassSets = new[]
        {
            PitchClassSet.Parse("047"), // set 1
            PitchClassSet.Parse("037"), // set 2
            PitchClassSet.Parse("048"), // set 3
            PitchClassSet.Parse("027")  // set 4
        };

        var options = new ShapeGraphBuildOptions { MaxFret = 12, MaxShapesPerSet = 5 };

        _grothendieckMock
            .Setup(g => g.ComputeDelta(It.IsAny<IntervalClassVector>(), It.IsAny<IntervalClassVector>()))
            .Returns(new GrothendieckDelta { Ic1 = 1, Ic2 = 0, Ic3 = 0, Ic4 = 0, Ic5 = 0, Ic6 = 0 });

        _grothendieckMock
            .Setup(g => g.ComputeHarmonicCost(It.IsAny<GrothendieckDelta>()))
            .Returns(1.0);

        var graph = await _builder.BuildGraphAsync(_standardTuning, pitchClassSets, options);

        // Act
        var families = _analyzer.Cluster(graph, k: 2);

        // Assert
        Assert.That(families, Is.Not.Empty);
        Assert.That(families.Count, Is.LessThanOrEqualTo(2));
        
        // Check families structure
        foreach(var family in families) 
        {
            Assert.That(family.ShapeIds, Is.Not.Empty);
            Assert.That(family.Size, Is.GreaterThan(0));
            Assert.That(family.AverageErgonomics, Is.EqualTo(0).Or.GreaterThan(0)); // Non-negative
        }

        // Ideally verify total shapes clustered matches graph size
        var totalClustered = families.Sum(f => f.ShapeIds.Count);
        Assert.That(totalClustered, Is.EqualTo(graph.ShapeCount));
    }
}
