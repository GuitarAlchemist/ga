namespace GA.Business.Core.Tests.Fretboard.Shapes.Spectral;

using Core.Atonal;
using Core.Atonal.Grothendieck;
using Microsoft.Extensions.Logging;
using Moq;

[TestFixture]
public class SpectralGraphAnalyzerTests
{
    [SetUp]
    public void SetUp()
    {
        _analyzerLoggerMock = new Mock<ILogger<SpectralGraphAnalyzer>>();
        _clusteringLoggerMock = new Mock<ILogger<SpectralClustering>>();
        _builderLoggerMock = new Mock<ILogger<ShapeGraphBuilder>>();
        _grothendieckMock = new Mock<IGrothendieckService>();

        _analyzer = new SpectralGraphAnalyzer(_analyzerLoggerMock.Object);
        _clustering = new SpectralClustering(_clusteringLoggerMock.Object, seed: 42);
        _builder = new ShapeGraphBuilder(_grothendieckMock.Object, _builderLoggerMock.Object);
        _standardTuning = Tuning.Default;
    }

    private SpectralGraphAnalyzer _analyzer = null!;
    private SpectralClustering _clustering = null!;
    private Mock<ILogger<SpectralGraphAnalyzer>> _analyzerLoggerMock = null!;
    private Mock<ILogger<SpectralClustering>> _clusteringLoggerMock = null!;
    private Mock<ILogger<ShapeGraphBuilder>> _builderLoggerMock = null!;
    private Mock<IGrothendieckService> _grothendieckMock = null!;
    private ShapeGraphBuilder _builder = null!;
    private Tuning _standardTuning = null!;

    [TestFixture]
    public class AnalyzeMethod : SpectralGraphAnalyzerTests
    {
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
            var metrics = _analyzer.Analyze(graph, useWeights: true, normalized: true);

            // Assert
            Assert.That(metrics, Is.Not.Null);
            Assert.That(metrics.NodeCount, Is.EqualTo(graph.ShapeCount));
            Assert.That(metrics.Eigenvalues, Is.Not.Empty);
            Assert.That(metrics.Eigenvalues.Length, Is.EqualTo(graph.ShapeCount));

            // First eigenvalue should be ~0 for connected graph
            Assert.That(metrics.Lambda1, Is.LessThan(1e-6));

            // Algebraic connectivity should be positive for connected graph
            Assert.That(metrics.AlgebraicConnectivity, Is.GreaterThan(0));
        }

        [Test]
        public async Task ShouldDetectConnectedGraph()
        {
            // Arrange
            var pitchClassSets = new[]
            {
                PitchClassSet.Parse("047"),
                PitchClassSet.Parse("037")
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
            var metrics = _analyzer.Analyze(graph);

            // Assert
            Assert.That(metrics.IsConnected, Is.True);
            Assert.That(metrics.EstimatedComponentCount, Is.EqualTo(1));
        }

        [Test]
        public async Task ShouldComputeFiedlerVector()
        {
            // Arrange
            var pitchClassSets = new[]
            {
                PitchClassSet.Parse("047"),
                PitchClassSet.Parse("037"),
                PitchClassSet.Parse("048")
            };

            var options = new ShapeGraphBuildOptions { MaxFret = 12, MaxShapesPerSet = 3 };

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
            Assert.That(metrics.FiedlerVector, Is.Not.Null);
            Assert.That(metrics.FiedlerVector!.Count, Is.EqualTo(graph.ShapeCount));
        }
    }

    [TestFixture]
    public class CentralityMethods : SpectralGraphAnalyzerTests
    {
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
            Assert.That(centralShapes.All(x => x.Centrality >= 0), Is.True);

            // Should be sorted by centrality (descending)
            for (var i = 1; i < centralShapes.Count; i++)
            {
                Assert.That(centralShapes[i].Centrality, Is.LessThanOrEqualTo(centralShapes[i - 1].Centrality));
            }
        }

        [Test]
        public async Task ShouldComputePageRank()
        {
            // Arrange
            var pitchClassSets = new[]
            {
                PitchClassSet.Parse("047"),
                PitchClassSet.Parse("037")
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
            var pageRank = _analyzer.ComputePageRank(graph);

            // Assert
            Assert.That(pageRank, Is.Not.Empty);
            Assert.That(pageRank.Count, Is.EqualTo(graph.ShapeCount));
            Assert.That(pageRank.Values.All(score => score >= 0 && score <= 1), Is.True);

            // Sum should be ~1 (normalized)
            var sum = pageRank.Values.Sum();
            Assert.That(sum, Is.EqualTo(1.0).Within(0.01));
        }

        [Test]
        public async Task ShouldFindBottlenecks()
        {
            // Arrange
            var pitchClassSets = new[]
            {
                PitchClassSet.Parse("047"),
                PitchClassSet.Parse("037"),
                PitchClassSet.Parse("048")
            };

            var options = new ShapeGraphBuildOptions { MaxFret = 12, MaxShapesPerSet = 3 };

            _grothendieckMock
                .Setup(g => g.ComputeDelta(It.IsAny<IntervalClassVector>(), It.IsAny<IntervalClassVector>()))
                .Returns(new GrothendieckDelta { Ic1 = 1, Ic2 = 0, Ic3 = 0, Ic4 = 0, Ic5 = 0, Ic6 = 0 });

            _grothendieckMock
                .Setup(g => g.ComputeHarmonicCost(It.IsAny<GrothendieckDelta>()))
                .Returns(1.0);

            var graph = await _builder.BuildGraphAsync(_standardTuning, pitchClassSets, options);

            // Act
            var bottlenecks = _analyzer.FindBottlenecks(graph, topK: 3);

            // Assert
            Assert.That(bottlenecks, Is.Not.Null);
            Assert.That(bottlenecks.Count, Is.LessThanOrEqualTo(3));
        }
    }

    [TestFixture]
    public class SpectralClusteringTests : SpectralGraphAnalyzerTests
    {
        [Test]
        public async Task ShouldClusterShapes()
        {
            // Arrange
            var pitchClassSets = PitchClassSet.Items
                .Where(pcs => pcs.Cardinality == 3)
                .Take(5)
                .ToList();

            var options = new ShapeGraphBuildOptions { MaxFret = 12, MaxShapesPerSet = 3 };

            _grothendieckMock
                .Setup(g => g.ComputeDelta(It.IsAny<IntervalClassVector>(), It.IsAny<IntervalClassVector>()))
                .Returns(new GrothendieckDelta { Ic1 = 1, Ic2 = 0, Ic3 = 0, Ic4 = 0, Ic5 = 0, Ic6 = 0 });

            _grothendieckMock
                .Setup(g => g.ComputeHarmonicCost(It.IsAny<GrothendieckDelta>()))
                .Returns(1.0);

            var graph = await _builder.BuildGraphAsync(_standardTuning, pitchClassSets, options);

            // Act
            var clusters = _clustering.Cluster(graph, k: 3);

            // Assert
            Assert.That(clusters, Is.Not.Empty);
            Assert.That(clusters.Count, Is.EqualTo(graph.ShapeCount));

            // All cluster IDs should be in range [0, k)
            Assert.That(clusters.Values.All(c => c >= 0 && c < 3), Is.True);

            // Should have at least 2 different clusters
            var uniqueClusters = clusters.Values.Distinct().Count();
            Assert.That(uniqueClusters, Is.GreaterThanOrEqualTo(2));
        }

        [Test]
        public async Task ShouldComputeClusterStats()
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
                .Returns(new GrothendieckDelta { Ic1 = 1, Ic2 = 0, Ic3 = 0, Ic4 = 0, Ic5 = 0, Ic6 = 0 });

            _grothendieckMock
                .Setup(g => g.ComputeHarmonicCost(It.IsAny<GrothendieckDelta>()))
                .Returns(1.0);

            var graph = await _builder.BuildGraphAsync(_standardTuning, pitchClassSets, options);
            var clusters = _clustering.Cluster(graph, k: 2);

            // Act
            var stats = _clustering.GetClusterStats(graph, clusters);

            // Assert
            Assert.That(stats, Is.Not.Empty);
            Assert.That(stats.Count, Is.LessThanOrEqualTo(2));

            foreach (var stat in stats.Values)
            {
                Assert.That(stat.Size, Is.GreaterThan(0));
                Assert.That(stat.AvgErgonomics, Is.GreaterThanOrEqualTo(0).And.LessThanOrEqualTo(1));
                Assert.That(stat.AvgDiagness, Is.GreaterThanOrEqualTo(0).And.LessThanOrEqualTo(1));
            }
        }
    }
}
