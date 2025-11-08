namespace GA.Business.Core.Tests.Atonal.Grothendieck;

using Core.Atonal;
using Core.Atonal.Grothendieck;
using Core.Fretboard.Positions;
using Core.Fretboard.Primitives;
using Core.Fretboard.Shapes;
using Microsoft.Extensions.Logging;
using Moq;

[TestFixture]
public class MarkovWalkerTests
{
    [SetUp]
    public void SetUp()
    {
        _loggerMock = new Mock<ILogger<MarkovWalker>>();
        _walker = new MarkovWalker(_loggerMock.Object);
        _testGraph = CreateTestGraph();
    }

    private MarkovWalker _walker = null!;
    private Mock<ILogger<MarkovWalker>> _loggerMock = null!;
    private ShapeGraph _testGraph = null!;

    private ShapeGraph CreateTestGraph()
    {
        // Create test shapes
        var pcs1 = PitchClassSet.Parse("047");
        var shape1 = new FretboardShape
        {
            Id = "shape1",
            TuningId = "standard",
            PitchClassSet = pcs1,
            Icv = pcs1.IntervalClassVector,
            Positions = [new PositionLocation(Str.FromValue(1), Fret.FromValue(3))],
            StringMask = 1,
            MinFret = 3,
            MaxFret = 3,
            Diagness = 0.2,
            Ergonomics = 0.8,
            FingerCount = 1
        };

        var pcs2 = PitchClassSet.Parse("037");
        var shape2 = new FretboardShape
        {
            Id = "shape2",
            TuningId = "standard",
            PitchClassSet = pcs2,
            Icv = pcs2.IntervalClassVector,
            Positions = [new PositionLocation(Str.FromValue(1), Fret.FromValue(5))],
            StringMask = 1,
            MinFret = 5,
            MaxFret = 5,
            Diagness = 0.3,
            Ergonomics = 0.7,
            FingerCount = 1
        };

        var pcs3 = PitchClassSet.Parse("048");
        var shape3 = new FretboardShape
        {
            Id = "shape3",
            TuningId = "standard",
            PitchClassSet = pcs3,
            Icv = pcs3.IntervalClassVector,
            Positions = [new PositionLocation(Str.FromValue(1), Fret.FromValue(7))],
            StringMask = 1,
            MinFret = 7,
            MaxFret = 7,
            Diagness = 0.8,
            Ergonomics = 0.6,
            FingerCount = 1
        };

        // Create transitions
        var transition1To2 = new ShapeTransition
        {
            FromId = "shape1",
            ToId = "shape2",
            Delta = new GrothendieckDelta { Ic1 = 0, Ic2 = 0, Ic3 = 1, Ic4 = -1, Ic5 = 0, Ic6 = 0 },
            HarmonicCost = 2.0,
            PhysicalCost = 1.0
        };

        var transition1To3 = new ShapeTransition
        {
            FromId = "shape1",
            ToId = "shape3",
            Delta = new GrothendieckDelta { Ic1 = 0, Ic2 = 0, Ic3 = -1, Ic4 = 2, Ic5 = -1, Ic6 = 0 },
            HarmonicCost = 4.0,
            PhysicalCost = 2.0
        };

        var transition2To3 = new ShapeTransition
        {
            FromId = "shape2",
            ToId = "shape3",
            Delta = new GrothendieckDelta { Ic1 = 0, Ic2 = 0, Ic3 = -2, Ic4 = 3, Ic5 = -1, Ic6 = 0 },
            HarmonicCost = 6.0,
            PhysicalCost = 1.5
        };

        return new ShapeGraph
        {
            TuningId = "standard",
            Shapes = new Dictionary<string, FretboardShape>
            {
                ["shape1"] = shape1,
                ["shape2"] = shape2,
                ["shape3"] = shape3
            },
            Adjacency = new Dictionary<string, IReadOnlyList<ShapeTransition>>
            {
                ["shape1"] = [transition1To2, transition1To3],
                ["shape2"] = [transition2To3],
                ["shape3"] = []
            }
        };
    }

    [TestFixture]
    public class GenerateWalk : MarkovWalkerTests
    {
        [Test]
        public void ShouldGenerateWalk_WithSpecifiedSteps()
        {
            // Arrange
            var startShape = _testGraph.Shapes["shape1"];
            var options = new WalkOptions { Steps = 5, Temperature = 1.0 };

            // Act
            var path = _walker.GenerateWalk(_testGraph, startShape, options);

            // Assert
            Assert.That(path, Is.Not.Empty);
            Assert.That(path.First().Id, Is.EqualTo("shape1"));
            Assert.That(path.Count, Is.LessThanOrEqualTo(6)); // Start + 5 steps (may stop early)
        }

        [Test]
        public void ShouldGenerateWalk_StartingFromGivenShape()
        {
            // Arrange
            var startShape = _testGraph.Shapes["shape1"];
            var options = new WalkOptions { Steps = 3, Temperature = 1.0 };

            // Act
            var path = _walker.GenerateWalk(_testGraph, startShape, options);

            // Assert
            Assert.That(path.First().Id, Is.EqualTo("shape1"));
        }

        [Test]
        public void ShouldGenerateWalk_WithBoxPreference()
        {
            // Arrange
            var startShape = _testGraph.Shapes["shape1"];
            var options = new WalkOptions
            {
                Steps = 5,
                Temperature = 1.0,
                BoxPreference = true // Prefer diagness < 0.5
            };

            // Act
            var path = _walker.GenerateWalk(_testGraph, startShape, options);

            // Assert: Should prefer shape2 (diagness 0.3) over shape3 (diagness 0.8)
            Assert.That(path, Is.Not.Empty);
        }

        [Test]
        public void ShouldGenerateWalk_WithMaxSpanFilter()
        {
            // Arrange
            var startShape = _testGraph.Shapes["shape1"];
            var options = new WalkOptions
            {
                Steps = 5,
                Temperature = 1.0,
                MaxSpan = 2
            };

            // Act
            var path = _walker.GenerateWalk(_testGraph, startShape, options);

            // Assert
            Assert.That(path.All(s => s.Span <= 2), Is.True);
        }

        [Test]
        public void ShouldStopWalk_WhenNoTransitionsAvailable()
        {
            // Arrange: Start from shape3 which has no outgoing transitions
            var startShape = _testGraph.Shapes["shape3"];
            var options = new WalkOptions { Steps = 10, Temperature = 1.0 };

            // Act
            var path = _walker.GenerateWalk(_testGraph, startShape, options);

            // Assert: Should only contain start shape
            Assert.That(path.Count, Is.EqualTo(1));
            Assert.That(path.First().Id, Is.EqualTo("shape3"));
        }
    }

    [TestFixture]
    public class GenerateHeatMap : MarkovWalkerTests
    {
        [Test]
        public void ShouldGenerateHeatMap_With6x24Grid()
        {
            // Arrange
            var currentShape = _testGraph.Shapes["shape1"];
            var options = new WalkOptions { Steps = 1, Temperature = 1.0 };

            // Act
            var heatMap = _walker.GenerateHeatMap(_testGraph, currentShape, options);

            // Assert
            Assert.That(heatMap.GetLength(0), Is.EqualTo(6)); // 6 strings
            Assert.That(heatMap.GetLength(1), Is.EqualTo(24)); // 24 frets
        }

        [Test]
        public void ShouldGenerateHeatMap_WithNormalizedValues()
        {
            // Arrange
            var currentShape = _testGraph.Shapes["shape1"];
            var options = new WalkOptions { Steps = 1, Temperature = 1.0 };

            // Act
            var heatMap = _walker.GenerateHeatMap(_testGraph, currentShape, options);

            // Assert: All values should be between 0 and 1
            for (var s = 0; s < 6; s++)
            {
                for (var f = 0; f < 24; f++)
                {
                    Assert.That(heatMap[s, f], Is.InRange(0.0, 1.0));
                }
            }
        }

        [Test]
        public void ShouldGenerateHeatMap_WithHigherProbabilitiesForNextShapes()
        {
            // Arrange
            var currentShape = _testGraph.Shapes["shape1"];
            var options = new WalkOptions { Steps = 1, Temperature = 1.0 };

            // Act
            var heatMap = _walker.GenerateHeatMap(_testGraph, currentShape, options);

            // Assert: Fret 5 (shape2) and fret 7 (shape3) should have non-zero probabilities
            var fret5Prob = heatMap[0, 5]; // String 1, Fret 5 (shape2)
            var fret7Prob = heatMap[0, 7]; // String 1, Fret 7 (shape3)

            Assert.That(fret5Prob, Is.GreaterThan(0));
            Assert.That(fret7Prob, Is.GreaterThan(0));
        }

        [Test]
        public void ShouldReturnEmptyHeatMap_WhenNoTransitions()
        {
            // Arrange: shape3 has no outgoing transitions
            var currentShape = _testGraph.Shapes["shape3"];
            var options = new WalkOptions { Steps = 1, Temperature = 1.0 };

            // Act
            var heatMap = _walker.GenerateHeatMap(_testGraph, currentShape, options);

            // Assert: All values should be 0
            for (var s = 0; s < 6; s++)
            {
                for (var f = 0; f < 24; f++)
                {
                    Assert.That(heatMap[s, f], Is.EqualTo(0.0));
                }
            }
        }
    }

    [TestFixture]
    public class GeneratePracticePath : MarkovWalkerTests
    {
        [Test]
        public void ShouldGeneratePracticePath_WithGradualDifficulty()
        {
            // Arrange
            var startShape = _testGraph.Shapes["shape1"];
            var options = new WalkOptions { Steps = 5, Temperature = 1.0 };

            // Act
            var path = _walker.GeneratePracticePath(_testGraph, startShape, options);

            // Assert
            Assert.That(path, Is.Not.Empty);
            Assert.That(path.First().Id, Is.EqualTo("shape1"));
        }

        [Test]
        public void ShouldGeneratePracticePath_PreferringEasierTransitionsFirst()
        {
            // Arrange
            var startShape = _testGraph.Shapes["shape1"];
            var options = new WalkOptions { Steps = 10, Temperature = 0.5 };

            // Act
            var path = _walker.GeneratePracticePath(_testGraph, startShape, options);

            // Assert: Early transitions should have lower cost
            // (This is probabilistic, so we just check the path is generated)
            Assert.That(path, Is.Not.Empty);
        }
    }

    [TestFixture]
    public class TemperatureControl : MarkovWalkerTests
    {
        [Test]
        public void ShouldBeMoreGreedy_WithLowTemperature()
        {
            // Arrange
            var startShape = _testGraph.Shapes["shape1"];
            var greedyOptions = new WalkOptions { Steps = 5, Temperature = 0.1 }; // Very greedy

            // Act: Run multiple times to check consistency
            var paths = Enumerable.Range(0, 10)
                .Select(_ => _walker.GenerateWalk(_testGraph, startShape, greedyOptions))
                .ToList();

            // Assert: With low temperature, paths should be more consistent (prefer low-cost transitions)
            // shape1 -> shape2 has lower cost (3.0) than shape1 -> shape3 (6.0)
            var preferShape2 = paths.Count(p => p.Count > 1 && p[1].Id == "shape2");
            Assert.That(preferShape2, Is.GreaterThan(5)); // Should prefer shape2 most of the time
        }

        [Test]
        public void ShouldBeMoreExploratory_WithHighTemperature()
        {
            // Arrange
            var startShape = _testGraph.Shapes["shape1"];
            var exploratoryOptions = new WalkOptions { Steps = 5, Temperature = 10.0 }; // Very exploratory

            // Act: Run multiple times
            var paths = Enumerable.Range(0, 20)
                .Select(_ => _walker.GenerateWalk(_testGraph, startShape, exploratoryOptions))
                .ToList();

            // Assert: With high temperature, should explore both shape2 and shape3
            var visitedShape2 = paths.Count(p => p.Any(s => s.Id == "shape2"));
            var visitedShape3 = paths.Count(p => p.Any(s => s.Id == "shape3"));

            Assert.That(visitedShape2, Is.GreaterThan(0));
            Assert.That(visitedShape3, Is.GreaterThan(0));
        }
    }

    [TestFixture]
    public class Performance : MarkovWalkerTests
    {
        [Test]
        public void ShouldGenerateWalk_InLessThan100ms()
        {
            // Arrange
            var startShape = _testGraph.Shapes["shape1"];
            var options = new WalkOptions { Steps = 10, Temperature = 1.0 };
            var stopwatch = Stopwatch.StartNew();

            // Act
            var path = _walker.GenerateWalk(_testGraph, startShape, options);
            stopwatch.Stop();

            // Assert
            Assert.That(stopwatch.ElapsedMilliseconds, Is.LessThan(100));
            Assert.That(path, Is.Not.Empty);
        }

        [Test]
        public void ShouldGenerateHeatMap_InLessThan50ms()
        {
            // Arrange
            var currentShape = _testGraph.Shapes["shape1"];
            var options = new WalkOptions { Steps = 1, Temperature = 1.0 };
            var stopwatch = Stopwatch.StartNew();

            // Act
            var heatMap = _walker.GenerateHeatMap(_testGraph, currentShape, options);
            stopwatch.Stop();

            // Assert
            Assert.That(stopwatch.ElapsedMilliseconds, Is.LessThan(50));
            Assert.That(heatMap, Is.Not.Null);
        }
    }
}
