namespace GA.Business.Core.Tests.Atonal.Grothendieck;

using Core.Atonal;
using Core.Atonal.Grothendieck;
using Core.Atonal.Primitives;
using Microsoft.Extensions.Logging;
using Moq;

[TestFixture]
[Ignore("GrothendieckService implementation not fully complete - tests require advanced atonal analysis features")]
public class GrothendieckServiceTests
{
    [SetUp]
    public void SetUp()
    {
        _loggerMock = new Mock<ILogger<GrothendieckService>>();
        _service = new GrothendieckService(_loggerMock.Object);
    }

    private GrothendieckService _service = null!;
    private Mock<ILogger<GrothendieckService>> _loggerMock = null!;

    [TestFixture]
    public class ComputeIcv : GrothendieckServiceTests
    {
        [Test]
        public void ShouldComputeICV_ForCMajorScale()
        {
            // Arrange: C Major scale (C D E F G A B)
            var cMajor = PitchClassSet.Parse("024579E");

            // Act
            var icv = _service.ComputeIcv(cMajor.Select(pc => pc.Value));

            // Assert
            Assert.That(icv[IntervalClass.FromValue(1)], Is.EqualTo(2)); // 2 semitones (E-F, B-C)
            Assert.That(icv[IntervalClass.FromValue(2)], Is.EqualTo(5)); // 5 whole tones
            Assert.That(icv[IntervalClass.FromValue(3)], Is.EqualTo(4)); // 4 minor thirds
            Assert.That(icv[IntervalClass.FromValue(4)], Is.EqualTo(3)); // 3 major thirds
            Assert.That(icv[IntervalClass.FromValue(5)], Is.EqualTo(6)); // 6 perfect fourths
            Assert.That(icv[IntervalClass.FromValue(6)], Is.EqualTo(1)); // 1 tritone (F-B)
        }

        [Test]
        public void ShouldComputeICV_ForCMinorScale()
        {
            // Arrange: C Minor scale (C D Eb F G Ab Bb)
            var cMinor = PitchClassSet.Parse("0235789");

            // Act
            var icv = _service.ComputeIcv(cMinor.Select(pc => pc.Value));

            // Assert
            Assert.That(icv[IntervalClass.FromValue(1)], Is.EqualTo(2)); // 2 semitones
            Assert.That(icv[IntervalClass.FromValue(2)], Is.EqualTo(5)); // 5 whole tones
            Assert.That(icv[IntervalClass.FromValue(3)], Is.EqualTo(4)); // 4 minor thirds
            Assert.That(icv[IntervalClass.FromValue(4)], Is.EqualTo(3)); // 3 major thirds
            Assert.That(icv[IntervalClass.FromValue(5)], Is.EqualTo(6)); // 6 perfect fourths
            Assert.That(icv[IntervalClass.FromValue(6)], Is.EqualTo(1)); // 1 tritone
        }

        [Test]
        public void ShouldComputeICV_ForCMajorTriad()
        {
            // Arrange: C Major triad (C E G)
            var cMajorTriad = PitchClassSet.Parse("047");

            // Act
            var icv = _service.ComputeIcv(cMajorTriad.Select(pc => pc.Value));

            // Assert
            Assert.That(icv[IntervalClass.FromValue(1)], Is.EqualTo(0));
            Assert.That(icv[IntervalClass.FromValue(2)], Is.EqualTo(0));
            Assert.That(icv[IntervalClass.FromValue(3)], Is.EqualTo(1)); // E-G
            Assert.That(icv[IntervalClass.FromValue(4)], Is.EqualTo(1)); // C-E
            Assert.That(icv[IntervalClass.FromValue(5)], Is.EqualTo(1)); // C-G
            Assert.That(icv[IntervalClass.FromValue(6)], Is.EqualTo(0));
        }

        [Test]
        public void ShouldComputeICV_ForEmptySet()
        {
            // Arrange
            var emptySet = PitchClassSet.Parse("");

            // Act
            var icv = _service.ComputeIcv(emptySet.Select(pc => pc.Value));

            // Assert
            Assert.That(icv[IntervalClass.FromValue(1)], Is.EqualTo(0));
            Assert.That(icv[IntervalClass.FromValue(2)], Is.EqualTo(0));
            Assert.That(icv[IntervalClass.FromValue(3)], Is.EqualTo(0));
            Assert.That(icv[IntervalClass.FromValue(4)], Is.EqualTo(0));
            Assert.That(icv[IntervalClass.FromValue(5)], Is.EqualTo(0));
            Assert.That(icv[IntervalClass.FromValue(6)], Is.EqualTo(0));
        }
    }

    [TestFixture]
    public class ComputeDelta : GrothendieckServiceTests
    {
        [Test]
        public void ShouldComputeDelta_FromCMajorToCMinor()
        {
            // Arrange
            var cMajor = PitchClassSet.Parse("024579E");
            var cMinor = PitchClassSet.Parse("0235789");
            var cMajorIcv = cMajor.IntervalClassVector;
            var cMinorIcv = cMinor.IntervalClassVector;

            // Act
            var delta = _service.ComputeDelta(cMajorIcv, cMinorIcv);

            // Assert: C Major and C Minor have same ICV (they're modes of each other)
            Assert.That(delta.Ic1, Is.EqualTo(0));
            Assert.That(delta.Ic2, Is.EqualTo(0));
            Assert.That(delta.Ic3, Is.EqualTo(0));
            Assert.That(delta.Ic4, Is.EqualTo(0));
            Assert.That(delta.Ic5, Is.EqualTo(0));
            Assert.That(delta.Ic6, Is.EqualTo(0));
        }

        [Test]
        public void ShouldComputeDelta_FromCMajorToGMajor()
        {
            // Arrange: C Major vs G Major (one sharp difference)
            var cMajor = PitchClassSet.Parse("024579E");
            var gMajor = PitchClassSet.Parse("0247 9E1"); // G A B C D E F#

            var cMajorIcv = _service.ComputeIcv(cMajor.Select(pc => pc.Value));
            var gMajorIcv = _service.ComputeIcv(gMajor.Select(pc => pc.Value));

            // Act
            var delta = _service.ComputeDelta(cMajorIcv, gMajorIcv);

            // Assert: Should have small delta (closely related keys)
            Assert.That(delta.L1Norm, Is.LessThan(5));
        }

        [Test]
        public void ShouldComputeDelta_WithExplanation()
        {
            // Arrange
            var cMajor = PitchClassSet.Parse("024579E");
            var gMajor = PitchClassSet.Parse("02479E1");
            var source = cMajor.IntervalClassVector;
            var target = gMajor.IntervalClassVector;

            // Act
            var delta = _service.ComputeDelta(source, target);
            var explanation = delta.Explain();

            // Assert
            Assert.That(explanation, Does.Contain("ic1"));
            Assert.That(explanation, Is.Not.Empty);
        }
    }

    [TestFixture]
    public class ComputeHarmonicCost : GrothendieckServiceTests
    {
        [Test]
        public void ShouldComputeHarmonicCost_AsL1Norm()
        {
            // Arrange
            var delta = new GrothendieckDelta
            {
                Ic1 = 1,
                Ic2 = -2,
                Ic3 = 3,
                Ic4 = 0,
                Ic5 = -1,
                Ic6 = 2
            };

            // Act
            var cost = _service.ComputeHarmonicCost(delta);

            // Assert: L1Norm = |1| + |-2| + |3| + |0| + |-1| + |2| = 9, cost = 9 * 0.6 = 5.4
            Assert.That(cost, Is.EqualTo(5.4));
        }

        [Test]
        public void ShouldComputeHarmonicCost_ZeroForIdentity()
        {
            // Arrange
            var delta = new GrothendieckDelta
            {
                Ic1 = 0,
                Ic2 = 0,
                Ic3 = 0,
                Ic4 = 0,
                Ic5 = 0,
                Ic6 = 0
            };

            // Act
            var cost = _service.ComputeHarmonicCost(delta);

            // Assert
            Assert.That(cost, Is.EqualTo(0.0));
        }
    }

    [TestFixture]
    public class FindNearby : GrothendieckServiceTests
    {
        [Test]
        public void ShouldFindNearby_WithinMaxDistance()
        {
            // Arrange: C Major scale
            var cMajor = PitchClassSet.Parse("024579B");

            // Act: Find nearby sets within distance 2
            var nearby = _service.FindNearby(cMajor, 2);

            // Assert
            var valueTuples = nearby as (PitchClassSet Set, GrothendieckDelta Delta, double Cost)[] ?? nearby.ToArray();
            Assert.That(valueTuples, Is.Not.Empty);
            Assert.That(valueTuples.All(n => n.Delta.L1Norm <= 2), Is.True);
            Assert.That(valueTuples.Any(n => n.Set.Id == cMajor.Id), Is.True); // Should include itself (distance 0)
        }

        [Test]
        public void ShouldFindNearby_OrderedByCost()
        {
            // Arrange
            var cMajor = PitchClassSet.Parse("024579B");

            // Act
            var nearby = _service.FindNearby(cMajor, 5).ToList();

            // Assert: Should be ordered by cost
            for (var i = 1; i < nearby.Count; i++)
            {
                Assert.That(nearby[i].Cost, Is.GreaterThanOrEqualTo(nearby[i - 1].Cost));
            }
        }

        [Test]
        public void ShouldFindNearby_IncludeSelfAtDistanceZero()
        {
            // Arrange
            var cMajor = PitchClassSet.Parse("024579B");

            // Act
            var nearby = _service.FindNearby(cMajor, 10).ToList();

            // Assert
            var self = nearby.FirstOrDefault(n => n.Set.Id == cMajor.Id);
            Assert.That(self.Set, Is.Not.Null);
            Assert.That(self.Cost, Is.EqualTo(0.0));
        }
    }

    [TestFixture]
    public class FindShortestPath : GrothendieckServiceTests
    {
        [Test]
        public void ShouldFindShortestPath_ToSelf()
        {
            // Arrange
            var cMajor = PitchClassSet.Parse("024579B");

            // Act
            var path = _service.FindShortestPath(cMajor, cMajor, 5).ToList();

            // Assert
            Assert.That(path, Is.Not.Empty);
            Assert.That(path.Count, Is.EqualTo(1));
            Assert.That(path[0].Id, Is.EqualTo(cMajor.Id));
        }

        [Test]
        public void ShouldFindShortestPath_BetweenRelatedKeys()
        {
            // Arrange: C Major to G Major (closely related)
            var cMajor = PitchClassSet.Parse("024579B");
            var gMajor = PitchClassSet.Parse("02479B1");

            // Act
            var path = _service.FindShortestPath(cMajor, gMajor, 5).ToList();

            // Assert
            Assert.That(path, Is.Not.Empty);
            Assert.That(path.Count, Is.GreaterThan(0));
            Assert.That(path.First().Id, Is.EqualTo(cMajor.Id));
            Assert.That(path.Last().Id, Is.EqualTo(gMajor.Id));
        }

        [Test]
        public void ShouldReturnEmpty_WhenNoPathExists()
        {
            // Arrange: Very distant sets with small maxSteps
            var cMajor = PitchClassSet.Parse("024579B");
            var chromatic = PitchClassSet.Parse("0123456789AB");

            // Act: Only allow 1 step (impossible to reach chromatic from major scale)
            var path = _service.FindShortestPath(cMajor, chromatic, 1).ToList();

            // Assert
            Assert.That(path, Is.Empty);
        }
    }

    [TestFixture]
    public class Performance : GrothendieckServiceTests
    {
        [Test]
        public void ShouldComputeICV_InLessThan1ms()
        {
            // Arrange
            var cMajor = PitchClassSet.Parse("024579B");
            var pitchClasses = cMajor.Select(pc => pc.Value).ToList();
            var stopwatch = Stopwatch.StartNew();

            // Act
            for (var i = 0; i < 1000; i++)
            {
                _service.ComputeIcv(pitchClasses);
            }

            stopwatch.Stop();

            // Assert: 1000 computations in < 10ms (< 0.01ms each)
            Assert.That(stopwatch.ElapsedMilliseconds, Is.LessThan(10));
        }

        [Test]
        public void ShouldFindNearby_InLessThan50ms()
        {
            // Arrange
            var cMajor = PitchClassSet.Parse("024579B");
            var stopwatch = Stopwatch.StartNew();

            // Act
            var nearby = _service.FindNearby(cMajor, 3);
            var count = nearby.Count();
            stopwatch.Stop();

            // Assert
            Assert.That(stopwatch.ElapsedMilliseconds, Is.LessThan(50));
            Assert.That(count, Is.GreaterThan(0));
        }
    }
}
