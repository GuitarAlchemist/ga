namespace GA.Business.Core.Tests.Fretboard.Shapes.Applications;

using Core.Atonal;
using Core.Atonal.Grothendieck;
using Microsoft.Extensions.Logging;
using Moq;

[TestFixture]
public class ProgressionOptimizerTests
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
    public async Task ShouldGeneratePracticeProgression_MaximizeInformationGain()
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
        var optimizer = new ProgressionOptimizer(_loggerFactory);

        var constraints = new ProgressionConstraints
        {
            TargetLength = 8,
            Strategy = OptimizationStrategy.MaximizeInformationGain,
            AllowRandomness = false
        };

        // Act
        var result = optimizer.GeneratePracticeProgression(graph, constraints);

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result.ShapeIds, Is.Not.Empty);
        Assert.That(result.ShapeIds.Count, Is.LessThanOrEqualTo(8));
        Assert.That(result.Strategy, Is.EqualTo(OptimizationStrategy.MaximizeInformationGain));
        Assert.That(result.Entropy, Is.GreaterThan(0));
        Assert.That(result.Quality, Is.InRange(0.0, 1.0));

        TestContext.WriteLine($"Generated progression: {string.Join(" -> ", result.ShapeIds.Take(5))}");
        TestContext.WriteLine($"Length: {result.ShapeIds.Count}");
        TestContext.WriteLine($"Entropy: {result.Entropy:F2}");
        TestContext.WriteLine($"Complexity: {result.Complexity:F2}");
        TestContext.WriteLine($"Diversity: {result.Diversity:F2}");
        TestContext.WriteLine($"Quality: {result.Quality:F2}");
    }

    [Test]
    public async Task ShouldGeneratePracticeProgression_MinimizeVoiceLeading()
    {
        // Arrange
        var pitchClassSets = new[]
        {
            PitchClassSet.Parse("047"), // C major
            PitchClassSet.Parse("0479"), // G major
            PitchClassSet.Parse("0258") // C7
        };

        var graph = await _builder.BuildGraphAsync(_standardTuning, pitchClassSets, new ShapeGraphBuildOptions());
        var optimizer = new ProgressionOptimizer(_loggerFactory);

        var constraints = new ProgressionConstraints
        {
            TargetLength = 6,
            Strategy = OptimizationStrategy.MinimizeVoiceLeading
        };

        // Act
        var result = optimizer.GeneratePracticeProgression(graph, constraints);

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result.ShapeIds, Is.Not.Empty);
        Assert.That(result.Strategy, Is.EqualTo(OptimizationStrategy.MinimizeVoiceLeading));

        TestContext.WriteLine($"Voice leading optimized: {string.Join(" -> ", result.ShapeIds.Take(5))}");
        TestContext.WriteLine($"Complexity: {result.Complexity:F2}");
    }

    [Test]
    public async Task ShouldGeneratePracticeProgression_ExploreFamilies()
    {
        // Arrange
        var pitchClassSets = new[]
        {
            PitchClassSet.Parse("047"), // C major
            PitchClassSet.Parse("037"), // C minor
            PitchClassSet.Parse("0479"), // G major
            PitchClassSet.Parse("0369"), // Cdim7
            PitchClassSet.Parse("0258") // C7
        };

        var graph = await _builder.BuildGraphAsync(_standardTuning, pitchClassSets, new ShapeGraphBuildOptions());
        var optimizer = new ProgressionOptimizer(_loggerFactory);

        var constraints = new ProgressionConstraints
        {
            TargetLength = 10,
            Strategy = OptimizationStrategy.ExploreFamilies
        };

        // Act
        var result = optimizer.GeneratePracticeProgression(graph, constraints);

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result.ShapeIds, Is.Not.Empty);
        Assert.That(result.Strategy, Is.EqualTo(OptimizationStrategy.ExploreFamilies));
        Assert.That(result.Diversity, Is.GreaterThan(0));

        TestContext.WriteLine($"Family exploration: {string.Join(" -> ", result.ShapeIds.Take(5))}");
        TestContext.WriteLine($"Diversity: {result.Diversity:F2}");
    }

    [Test]
    public async Task ShouldGeneratePracticeProgression_FollowAttractors()
    {
        // Arrange
        var pitchClassSets = new[]
        {
            PitchClassSet.Parse("047"), // C major
            PitchClassSet.Parse("0479"), // G major
            PitchClassSet.Parse("0258") // C7
        };

        var graph = await _builder.BuildGraphAsync(_standardTuning, pitchClassSets, new ShapeGraphBuildOptions());
        var optimizer = new ProgressionOptimizer(_loggerFactory);

        var constraints = new ProgressionConstraints
        {
            TargetLength = 6,
            Strategy = OptimizationStrategy.FollowAttractors,
            PreferAttractors = true
        };

        // Act
        var result = optimizer.GeneratePracticeProgression(graph, constraints);

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result.ShapeIds, Is.Not.Empty);
        Assert.That(result.Strategy, Is.EqualTo(OptimizationStrategy.FollowAttractors));

        TestContext.WriteLine($"Attractor following: {string.Join(" -> ", result.ShapeIds.Take(5))}");
        TestContext.WriteLine($"Predictability: {result.Predictability:F2}");
    }

    [Test]
    public async Task ShouldGeneratePracticeProgression_Balanced()
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
        var optimizer = new ProgressionOptimizer(_loggerFactory);

        var constraints = new ProgressionConstraints
        {
            TargetLength = 8,
            Strategy = OptimizationStrategy.Balanced,
            AllowRandomness = true
        };

        // Act
        var result = optimizer.GeneratePracticeProgression(graph, constraints);

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result.ShapeIds, Is.Not.Empty);
        Assert.That(result.Strategy, Is.EqualTo(OptimizationStrategy.Balanced));
        Assert.That(result.Quality, Is.GreaterThan(0));

        TestContext.WriteLine($"Balanced progression: {string.Join(" -> ", result.ShapeIds.Take(5))}");
        TestContext.WriteLine($"Quality: {result.Quality:F2}");
        TestContext.WriteLine($"Entropy: {result.Entropy:F2}");
        TestContext.WriteLine($"Diversity: {result.Diversity:F2}");
    }

    [Test]
    public async Task ShouldImproveProgression_ReduceComplexity()
    {
        // Arrange
        var pitchClassSets = new[]
        {
            PitchClassSet.Parse("047"),
            PitchClassSet.Parse("0479"),
            PitchClassSet.Parse("0258")
        };

        var graph = await _builder.BuildGraphAsync(_standardTuning, pitchClassSets, new ShapeGraphBuildOptions());
        var optimizer = new ProgressionOptimizer(_loggerFactory);

        var existingProgression = graph.Shapes.Keys.Take(4).ToList();

        // Act
        var result = optimizer.ImproveProgression(graph, existingProgression, ImprovementGoal.ReduceComplexity);

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result.ShapeIds, Is.Not.Empty);

        TestContext.WriteLine("Original complexity: (baseline)");
        TestContext.WriteLine($"Improved complexity: {result.Complexity:F2}");
    }

    [Test]
    public async Task ShouldRespectConstraints()
    {
        // Arrange
        var pitchClassSets = new[]
        {
            PitchClassSet.Parse("047"),
            PitchClassSet.Parse("0479")
        };

        var graph = await _builder.BuildGraphAsync(_standardTuning, pitchClassSets, new ShapeGraphBuildOptions());
        var optimizer = new ProgressionOptimizer(_loggerFactory);

        var startShape = graph.Shapes.Keys.First();

        var constraints = new ProgressionConstraints
        {
            TargetLength = 5,
            StartShapeId = startShape,
            Strategy = OptimizationStrategy.MaximizeInformationGain
        };

        // Act
        var result = optimizer.GeneratePracticeProgression(graph, constraints);

        // Assert
        Assert.That(result.ShapeIds[0], Is.EqualTo(startShape));
        Assert.That(result.ShapeIds.Count, Is.LessThanOrEqualTo(5));
    }

    [Test]
    public async Task ShouldHandleSmallGraph()
    {
        // Arrange
        var pitchClassSets = new[] { PitchClassSet.Parse("047") };
        var graph = await _builder.BuildGraphAsync(_standardTuning, pitchClassSets, new ShapeGraphBuildOptions());
        var optimizer = new ProgressionOptimizer(_loggerFactory);

        var constraints = new ProgressionConstraints { TargetLength = 10 };

        // Act
        var result = optimizer.GeneratePracticeProgression(graph, constraints);

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result.ShapeIds, Is.Not.Empty);
        // May be shorter than target if graph is small
    }

    [Test]
    public async Task ShouldProduceDifferentResults_WithRandomness()
    {
        // Arrange
        var pitchClassSets = new[]
        {
            PitchClassSet.Parse("047"),
            PitchClassSet.Parse("0479"),
            PitchClassSet.Parse("0258"),
            PitchClassSet.Parse("037")
        };

        var graph = await _builder.BuildGraphAsync(_standardTuning, pitchClassSets, new ShapeGraphBuildOptions());
        var optimizer = new ProgressionOptimizer(_loggerFactory);

        var constraints = new ProgressionConstraints
        {
            TargetLength = 6,
            Strategy = OptimizationStrategy.Balanced,
            AllowRandomness = true
        };

        // Act - Generate multiple progressions
        var results = Enumerable.Range(0, 3)
            .Select(_ => optimizer.GeneratePracticeProgression(graph, constraints))
            .ToList();

        // Assert - At least some should be different (with high probability)
        var allSame = results.All(r => r.ShapeIds.SequenceEqual(results[0].ShapeIds));
        Assert.That(allSame, Is.False, "Expected some variation with randomness enabled");
    }
}
