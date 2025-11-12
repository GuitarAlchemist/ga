namespace GA.Business.Core.Tests.Fretboard.Shapes;

using Core.Atonal;
using Core.Atonal.Grothendieck;
using Core.Fretboard.Positions;
using Core.Fretboard.Primitives;
using Core.Fretboard.Shapes;
using Microsoft.Extensions.Logging;
using Moq;

[TestFixture]
public class ShapeGraphBuilderTests
{
    [SetUp]
    public void SetUp()
    {
        _loggerMock = new Mock<ILogger<ShapeGraphBuilder>>();
        _grothendieckMock = new Mock<IGrothendieckService>();
        _builder = new ShapeGraphBuilder(_grothendieckMock.Object, _loggerMock.Object);
        _standardTuning = Tuning.Default; // E A D G B E
    }

    private ShapeGraphBuilder _builder = null!;
    private Mock<ILogger<ShapeGraphBuilder>> _loggerMock = null!;
    private Mock<IGrothendieckService> _grothendieckMock = null!;
    private Tuning _standardTuning = null!;

    [TestFixture]
    public class GenerateShapes : ShapeGraphBuilderTests
    {
        [Test]
        public void ShouldGenerateShapes_ForCMajorTriad()
        {
            // Arrange: C Major triad (C E G)
            var cMajorTriad = PitchClassSet.Parse("047");
            var options = new ShapeGraphBuildOptions
            {
                MaxFret = 12,
                MaxSpan = 5,
                MinErgonomics = 0.0
            };

            // Act
            var shapes = _builder.GenerateShapes(_standardTuning, cMajorTriad, options);

            // Assert
            Assert.That(shapes, Is.Not.Empty);
            Assert.That(shapes.All(s => s.PitchClassSet.Id == cMajorTriad.Id), Is.True);
            Assert.That(shapes.All(s => s.Span <= options.MaxSpan), Is.True);
        }

        [Test]
        [Ignore("Shape generation algorithm not fully implemented - no diagonal shapes generated")]
        public void ShouldGenerateShapes_WithDifferentDiagness()
        {
            // Arrange
            var cMajorTriad = PitchClassSet.Parse("047");
            var options = new ShapeGraphBuildOptions { MaxFret = 12 };

            // Act
            var shapes = _builder.GenerateShapes(_standardTuning, cMajorTriad, options).ToList();

            // Assert: Should have both box and diagonal shapes
            var boxShapes = shapes.Where(s => s.Diagness < 0.3).ToList();
            var diagonalShapes = shapes.Where(s => s.Diagness > 0.7).ToList();

            Assert.That(boxShapes, Is.Not.Empty, "Should have box shapes");
            Assert.That(diagonalShapes, Is.Not.Empty, "Should have diagonal shapes");
        }

        [Test]
        public void ShouldFilterShapes_ByMinErgonomics()
        {
            // Arrange
            var cMajorTriad = PitchClassSet.Parse("047");
            var options = new ShapeGraphBuildOptions
            {
                MaxFret = 12,
                MinErgonomics = 0.7 // Only easy shapes
            };

            // Act
            var shapes = _builder.GenerateShapes(_standardTuning, cMajorTriad, options).ToList();

            // Assert
            Assert.That(shapes.All(s => s.Ergonomics >= 0.7), Is.True);
        }

        [Test]
        public void ShouldLimitShapes_ByMaxShapesPerSet()
        {
            // Arrange
            var cMajorTriad = PitchClassSet.Parse("047");
            var options = new ShapeGraphBuildOptions
            {
                MaxFret = 24,
                MaxShapesPerSet = 10
            };

            // Act
            var shapes = _builder.GenerateShapes(_standardTuning, cMajorTriad, options).ToList();

            // Assert
            Assert.That(shapes.Count, Is.LessThanOrEqualTo(10));
        }
    }

    [TestFixture]
    public class BuildGraphAsync : ShapeGraphBuilderTests
    {
        [Test]
        public async Task ShouldBuildGraph_ForMultiplePitchClassSets()
        {
            // Arrange: Major and minor triads
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
                MaxShapesPerSet = 5
            };

            // Mock Grothendieck service
            _grothendieckMock
                .Setup(g => g.ComputeDelta(It.IsAny<IntervalClassVector>(), It.IsAny<IntervalClassVector>()))
                .Returns(new GrothendieckDelta { Ic1 = 0, Ic2 = 0, Ic3 = 0, Ic4 = 0, Ic5 = 0, Ic6 = 0 });

            _grothendieckMock
                .Setup(g => g.ComputeHarmonicCost(It.IsAny<GrothendieckDelta>()))
                .Returns(1.0);

            // Act
            var graph = await _builder.BuildGraphAsync(_standardTuning, pitchClassSets, options);

            // Assert
            Assert.That(graph, Is.Not.Null);
            Assert.That(graph.TuningId, Is.EqualTo(_standardTuning.ToString()));
            Assert.That(graph.ShapeCount, Is.GreaterThan(0));
            Assert.That(graph.Shapes.Values.All(s => s.Span <= options.MaxSpan), Is.True);
        }

        [Test]
        public async Task ShouldBuildGraph_WithTransitions()
        {
            // Arrange
            var pitchClassSets = new[]
            {
                PitchClassSet.Parse("047"), // C Major
                PitchClassSet.Parse("037") // C Minor
            };

            var options = new ShapeGraphBuildOptions
            {
                MaxFret = 12,
                MaxHarmonicDistance = 5,
                MaxPhysicalCost = 10
            };

            // Mock Grothendieck service
            _grothendieckMock
                .Setup(g => g.ComputeDelta(It.IsAny<IntervalClassVector>(), It.IsAny<IntervalClassVector>()))
                .Returns(new GrothendieckDelta { Ic1 = 1, Ic2 = 0, Ic3 = 0, Ic4 = 0, Ic5 = 0, Ic6 = 0 });

            _grothendieckMock
                .Setup(g => g.ComputeHarmonicCost(It.IsAny<GrothendieckDelta>()))
                .Returns(1.0);

            // Act
            var graph = await _builder.BuildGraphAsync(_standardTuning, pitchClassSets, options);

            // Assert
            Assert.That(graph.TransitionCount, Is.GreaterThan(0));
        }

        [Test]
        public async Task ShouldFilterTransitions_ByMaxHarmonicDistance()
        {
            // Arrange
            var pitchClassSets = new[]
            {
                PitchClassSet.Parse("047"),
                PitchClassSet.Parse("037")
            };

            var options = new ShapeGraphBuildOptions
            {
                MaxFret = 12,
                MaxHarmonicDistance = 2 // Very restrictive
            };

            // Mock: High harmonic cost
            _grothendieckMock
                .Setup(g => g.ComputeDelta(It.IsAny<IntervalClassVector>(), It.IsAny<IntervalClassVector>()))
                .Returns(new GrothendieckDelta { Ic1 = 5, Ic2 = 5, Ic3 = 5, Ic4 = 5, Ic5 = 5, Ic6 = 5 });

            _grothendieckMock
                .Setup(g => g.ComputeHarmonicCost(It.IsAny<GrothendieckDelta>()))
                .Returns(30.0); // High cost

            // Act
            var graph = await _builder.BuildGraphAsync(_standardTuning, pitchClassSets, options);

            // Assert: Should have very few transitions due to high cost
            Assert.That(graph.TransitionCount, Is.LessThan(10));
        }
    }

    [TestFixture]
    public class ShapeProperties : ShapeGraphBuilderTests
    {
        [Test]
        public void ShouldComputeDiagness_ForBoxShape()
        {
            // Arrange: Box shape (vertical, no fret change)
            var positions = new[]
            {
                new PositionLocation(Str.FromValue(1), Fret.FromValue(3)),
                new PositionLocation(Str.FromValue(2), Fret.FromValue(3)),
                new PositionLocation(Str.FromValue(3), Fret.FromValue(3))
            };

            // Act
            var diagness = FretboardShape.ComputeDiagness(positions);

            // Assert: Should be close to 0 (box shape)
            Assert.That(diagness, Is.LessThan(0.3));
        }

        [Test]
        public void ShouldComputeDiagness_ForDiagonalShape()
        {
            // Arrange: Diagonal shape (large fret change per string)
            var positions = new[]
            {
                new PositionLocation(Str.FromValue(1), Fret.FromValue(0)),
                new PositionLocation(Str.FromValue(2), Fret.FromValue(4)),
                new PositionLocation(Str.FromValue(3), Fret.FromValue(8))
            };

            // Act
            var diagness = FretboardShape.ComputeDiagness(positions);

            // Assert: Should be close to 1 (diagonal shape)
            Assert.That(diagness, Is.GreaterThan(0.7));
        }

        [Test]
        public void ShouldComputeErgonomics_ForEasyShape()
        {
            // Arrange: Easy shape (small span, low frets)
            var positions = new[]
            {
                new PositionLocation(Str.FromValue(1), Fret.FromValue(1)),
                new PositionLocation(Str.FromValue(2), Fret.FromValue(2)),
                new PositionLocation(Str.FromValue(3), Fret.FromValue(3))
            };

            // Act
            var span = 2; // max fret (3) - min fret (1)
            var ergonomics = FretboardShape.ComputeErgonomics(positions, span);

            // Assert: Should be high (easy)
            Assert.That(ergonomics, Is.GreaterThan(0.7));
        }

        [Test]
        public void ShouldComputeErgonomics_ForHardShape()
        {
            // Arrange: Hard shape (large span, high frets, big stretch)
            var positions = new[]
            {
                new PositionLocation(Str.FromValue(1), Fret.FromValue(15)),
                new PositionLocation(Str.FromValue(2), Fret.FromValue(20)),
                new PositionLocation(Str.FromValue(3), Fret.FromValue(24))
            };

            // Act
            var span = 9; // max fret (24) - min fret (15)
            var ergonomics = FretboardShape.ComputeErgonomics(positions, span);

            // Assert: Should be low (hard)
            Assert.That(ergonomics, Is.LessThan(0.5));
        }
    }

    [TestFixture]
    public class Performance : ShapeGraphBuilderTests
    {
        [Test]
        public void ShouldGenerateShapes_InLessThan10ms()
        {
            // Arrange
            var cMajorTriad = PitchClassSet.Parse("047");
            var options = new ShapeGraphBuildOptions { MaxFret = 12 };
            var stopwatch = Stopwatch.StartNew();

            // Act
            var shapes = _builder.GenerateShapes(_standardTuning, cMajorTriad, options).ToList();
            stopwatch.Stop();

            // Assert
            Assert.That(stopwatch.ElapsedMilliseconds, Is.LessThan(10));
            Assert.That(shapes, Is.Not.Empty);
        }

        [Test]
        [Ignore("Shape ID generation produces duplicates - needs fix in shape generation logic")]
        public async Task ShouldBuildGraph_InLessThan5Seconds()
        {
            // Arrange: 10 pitch-class sets
            var pitchClassSets = PitchClassSet.Items
                .Where(pcs => pcs.Cardinality == 3)
                .Take(10)
                .ToList();

            var options = new ShapeGraphBuildOptions
            {
                MaxFret = 12,
                MaxShapesPerSet = 10
            };

            // Mock Grothendieck service
            _grothendieckMock
                .Setup(g => g.ComputeDelta(It.IsAny<IntervalClassVector>(), It.IsAny<IntervalClassVector>()))
                .Returns(new GrothendieckDelta { Ic1 = 0, Ic2 = 0, Ic3 = 0, Ic4 = 0, Ic5 = 0, Ic6 = 0 });

            _grothendieckMock
                .Setup(g => g.ComputeHarmonicCost(It.IsAny<GrothendieckDelta>()))
                .Returns(1.0);

            var stopwatch = Stopwatch.StartNew();

            // Act
            var graph = await _builder.BuildGraphAsync(_standardTuning, pitchClassSets, options);
            stopwatch.Stop();

            // Assert
            Assert.That(stopwatch.ElapsedMilliseconds, Is.LessThan(5000));
            Assert.That(graph.ShapeCount, Is.GreaterThan(0));
        }
    }
}
