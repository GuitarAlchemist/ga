namespace GA.Business.Core.Tests.Fretboard.Shapes.Applications;

using Core.Atonal;
using Core.Atonal.Grothendieck;
using Core.Fretboard.Shapes;
using Microsoft.Extensions.Logging;
using Moq;

[TestFixture]
public class HarmonicAnalysisEngineTests
{
    [SetUp]
    public void Setup()
    {
        _loggerFactory = LoggerFactory.Create(builder => builder.SetMinimumLevel(LogLevel.Information));

        var grothendieckServiceMock = new Mock<IGrothendieckService>();
        grothendieckServiceMock
            .Setup(x => x.ComputeDelta(It.IsAny<IntervalClassVector>(), It.IsAny<IntervalClassVector>()))
            .Returns((IntervalClassVector a, IntervalClassVector b) =>
            {
                // Simple mock delta - just return a delta with L1Norm = 1
                return new GrothendieckDelta { Ic1 = 1, Ic2 = 0, Ic3 = 0, Ic4 = 0, Ic5 = 0, Ic6 = 0 };
            });

        grothendieckServiceMock
            .Setup(x => x.ComputeHarmonicCost(It.IsAny<GrothendieckDelta>()))
            .Returns((GrothendieckDelta delta) => delta.L1Norm);

        _builder = new ShapeGraphBuilder(
            grothendieckServiceMock.Object,
            _loggerFactory.CreateLogger<ShapeGraphBuilder>()
        );

        _standardTuning = Tuning.Default;
    }

    [TearDown]
    public void TearDown()
    {
        _loggerFactory?.Dispose();
    }

    private ILoggerFactory _loggerFactory = null!;
    private ShapeGraphBuilder _builder = null!;
    private Tuning _standardTuning = null!;

    [Test]
    public async Task ShouldPerformComprehensiveAnalysis()
    {
        // Arrange
        var pitchClassSets = new[]
        {
            PitchClassSet.Parse("047"), // C major
            PitchClassSet.Parse("037"), // C minor
            PitchClassSet.Parse("0258"), // C7
            PitchClassSet.Parse("0479"), // G major
            PitchClassSet.Parse("0369") // Cdim7
        };

        var options = new ShapeGraphBuildOptions
        {
            MaxFret = 12,
            MinErgonomics = 0.5,
            MaxSpan = 5
        };

        var graph = await _builder.BuildGraphAsync(_standardTuning, pitchClassSets, options);
        var engine = new HarmonicAnalysisEngine(_loggerFactory);

        // Act
        var report = await engine.AnalyzeAsync(graph, new HarmonicAnalysisOptions
        {
            IncludeSpectralAnalysis = true,
            IncludeDynamicalAnalysis = true,
            IncludeTopologicalAnalysis = true,
            ClusterCount = 3,
            TopCentralShapes = 5,
            TopBottlenecks = 3
        });

        // Assert
        Assert.That(report, Is.Not.Null);
        Assert.That(report.GraphSize, Is.EqualTo(graph.ShapeCount));

        // Spectral analysis
        Assert.That(report.Spectral, Is.Not.Null);
        Assert.That(report.Spectral!.AlgebraicConnectivity, Is.GreaterThan(0));

        // Chord families
        Assert.That(report.ChordFamilies, Is.Not.Empty);
        Assert.That(report.ChordFamilies.Count, Is.LessThanOrEqualTo(3));

        // Central shapes
        Assert.That(report.CentralShapes, Is.Not.Empty);
        Assert.That(report.CentralShapes.Count, Is.LessThanOrEqualTo(5));

        // Dynamics
        Assert.That(report.Dynamics, Is.Not.Null);
        Assert.That(report.Dynamics!.ShapeCount, Is.EqualTo(graph.ShapeCount));

        // Topology
        Assert.That(report.Topology, Is.Not.Null);

        TestContext.WriteLine($"Graph size: {report.GraphSize}");
        TestContext.WriteLine($"Algebraic connectivity: {report.Spectral.AlgebraicConnectivity:F4}");
        TestContext.WriteLine($"Chord families: {report.ChordFamilies.Count}");
        TestContext.WriteLine($"Central shapes: {report.CentralShapes.Count}");
        TestContext.WriteLine($"Attractors: {report.Dynamics.Attractors.Count}");
        TestContext.WriteLine($"Fixed points: {report.Dynamics.FixedPoints.Count}");
        TestContext.WriteLine($"Limit cycles: {report.Dynamics.LimitCycles.Count}");
        TestContext.WriteLine($"Lyapunov exponent: {report.Dynamics.LyapunovExponent:F4}");
    }

    [Test]
    public async Task ShouldAnalyzeProgression()
    {
        // Arrange
        var pitchClassSets = new[]
        {
            PitchClassSet.Parse("047"), // C major
            PitchClassSet.Parse("0479"), // G major
            PitchClassSet.Parse("0258"), // C7
            PitchClassSet.Parse("0479") // G major
        };

        var graph = await _builder.BuildGraphAsync(_standardTuning, pitchClassSets, new ShapeGraphBuildOptions());
        var engine = new HarmonicAnalysisEngine(_loggerFactory);

        // Get some shapes for the progression
        var progression = graph.Shapes.Keys.Take(4).ToList();

        // Act
        var report = engine.AnalyzeProgression(graph, progression);

        // Assert
        Assert.That(report, Is.Not.Null);
        Assert.That(report.Length, Is.EqualTo(4));
        Assert.That(report.Entropy, Is.GreaterThan(0));
        Assert.That(report.Complexity, Is.GreaterThanOrEqualTo(0));
        Assert.That(report.Predictability, Is.InRange(0.0, 1.0));
        Assert.That(report.Diversity, Is.InRange(0.0, 1.0));
        Assert.That(report.SuggestedNextShapes, Is.Not.Empty);

        TestContext.WriteLine($"Progression length: {report.Length}");
        TestContext.WriteLine($"Unique shapes: {report.UniqueShapes}");
        TestContext.WriteLine($"Entropy: {report.Entropy:F2} bits");
        TestContext.WriteLine($"Complexity: {report.Complexity:F2}");
        TestContext.WriteLine($"Predictability: {report.Predictability:F2}");
        TestContext.WriteLine($"Diversity: {report.Diversity:F2}");
        TestContext.WriteLine($"Avg voice leading cost: {report.AverageVoiceLeadingCost:F2}");
        TestContext.WriteLine($"Suggested next shapes: {report.SuggestedNextShapes.Count}");
    }

    [Test]
    public async Task ShouldCompareProgressions()
    {
        // Arrange
        var pitchClassSets = new[]
        {
            PitchClassSet.Parse("047"), // C major
            PitchClassSet.Parse("0479"), // G major
            PitchClassSet.Parse("0258"), // C7
            PitchClassSet.Parse("037") // C minor
        };

        var graph = await _builder.BuildGraphAsync(_standardTuning, pitchClassSets, new ShapeGraphBuildOptions());
        var engine = new HarmonicAnalysisEngine(_loggerFactory);

        var shapes = graph.Shapes.Keys.ToList();
        var prog1 = shapes.Take(3).ToList();
        var prog2 = shapes.Skip(1).Take(3).ToList();

        // Act
        var report = engine.CompareProgressions(graph, prog1, prog2);

        // Assert
        Assert.That(report, Is.Not.Null);
        Assert.That(report.JensenShannonDistance, Is.GreaterThanOrEqualTo(0));
        Assert.That(report.WassersteinDistance, Is.GreaterThanOrEqualTo(0));
        Assert.That(report.Similarity, Is.InRange(0.0, 1.0));

        TestContext.WriteLine($"Jensen-Shannon distance: {report.JensenShannonDistance:F4}");
        TestContext.WriteLine($"Wasserstein distance: {report.WassersteinDistance:F4}");
        TestContext.WriteLine($"Entropy difference: {report.EntropyDifference:F4}");
        TestContext.WriteLine($"Complexity difference: {report.ComplexityDifference:F4}");
        TestContext.WriteLine($"Similarity: {report.Similarity:F4}");
    }

    [Test]
    public async Task ShouldFindOptimalPracticePath()
    {
        // Arrange
        var pitchClassSets = new[]
        {
            PitchClassSet.Parse("047"), // C major
            PitchClassSet.Parse("0479"), // G major
            PitchClassSet.Parse("0258"), // C7
            PitchClassSet.Parse("037"), // C minor
            PitchClassSet.Parse("0369") // Cdim7
        };

        var graph = await _builder.BuildGraphAsync(_standardTuning, pitchClassSets, new ShapeGraphBuildOptions());
        var engine = new HarmonicAnalysisEngine(_loggerFactory);

        var startShape = graph.Shapes.Keys.First();

        // Act - Test different goals
        var pathInfoGain = engine.FindOptimalPracticePath(
            graph, startShape, pathLength: 6, PracticeGoal.MaximizeInformationGain);

        var pathVoiceLeading = engine.FindOptimalPracticePath(
            graph, startShape, pathLength: 6, PracticeGoal.MinimizeVoiceLeading);

        var pathDiversity = engine.FindOptimalPracticePath(
            graph, startShape, pathLength: 6, PracticeGoal.ExploreDiversity);

        // Assert
        Assert.That(pathInfoGain, Is.Not.Empty);
        Assert.That(pathVoiceLeading, Is.Not.Empty);
        Assert.That(pathDiversity, Is.Not.Empty);

        Assert.That(pathInfoGain[0], Is.EqualTo(startShape));
        Assert.That(pathVoiceLeading[0], Is.EqualTo(startShape));
        Assert.That(pathDiversity[0], Is.EqualTo(startShape));

        TestContext.WriteLine($"Info gain path: {string.Join(" -> ", pathInfoGain.Take(5))}");
        TestContext.WriteLine($"Voice leading path: {string.Join(" -> ", pathVoiceLeading.Take(5))}");
        TestContext.WriteLine($"Diversity path: {string.Join(" -> ", pathDiversity.Take(5))}");
    }

    [Test]
    public async Task ShouldHandleEmptyGraph()
    {
        // Arrange
        var emptyGraph = new ShapeGraph
        {
            TuningId = "E2 A2 D3 G3 B3 E4",
            Shapes = new Dictionary<string, FretboardShape>(),
            Adjacency = new Dictionary<string, IReadOnlyList<ShapeTransition>>()
        };
        var engine = new HarmonicAnalysisEngine(_loggerFactory);

        // Act & Assert
        Assert.DoesNotThrowAsync(async () =>
        {
            var report = await engine.AnalyzeAsync(emptyGraph, new HarmonicAnalysisOptions
            {
                IncludeSpectralAnalysis = false,
                IncludeDynamicalAnalysis = false,
                IncludeTopologicalAnalysis = false
            });

            Assert.That(report.GraphSize, Is.EqualTo(0));
        });
    }

    [Test]
    public async Task ShouldRespectAnalysisOptions()
    {
        // Arrange
        var pitchClassSets = new[] { PitchClassSet.Parse("047") };
        var graph = await _builder.BuildGraphAsync(_standardTuning, pitchClassSets, new ShapeGraphBuildOptions());
        var engine = new HarmonicAnalysisEngine(_loggerFactory);

        // Act - Disable all analyses
        var report = await engine.AnalyzeAsync(graph, new HarmonicAnalysisOptions
        {
            IncludeSpectralAnalysis = false,
            IncludeDynamicalAnalysis = false,
            IncludeTopologicalAnalysis = false
        });

        // Assert
        Assert.That(report.Spectral, Is.Null);
        Assert.That(report.Dynamics, Is.Null);
        Assert.That(report.Topology, Is.Null);
        Assert.That(report.ChordFamilies, Is.Empty);
    }
}
