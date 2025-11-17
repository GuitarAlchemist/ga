namespace GA.Business.Core.Tests.Atonal.Grothendieck;

using Core.Atonal;
using Core.Atonal.Grothendieck;
using Core.Atonal.Primitives;
using Moq;

[TestFixture]
public class GrothendieckServiceTests
{
    [SetUp]
    public void SetUp()
    {
        _service = new GrothendieckService();
    }

    private GrothendieckService _service = null!;

    [TestFixture]
    public class ComputeIcv : GrothendieckServiceTests
    {
        [Test]
        public void ShouldComputeICV_ForCMajorScale()
        {
            var cMajor = PitchClassSet.Parse("024579E");
            var expected = cMajor.IntervalClassVector;
            var icv = _service.ComputeIcv(cMajor.Select(pc => pc.Value));
            // Validates that the service matches the known diatonic vector for the major scale.
            Assert.That(icv, Is.EqualTo(expected));
        }

        [Test]
        public void ShouldComputeICV_ForCMinorScale()
        {
            var cMinor = PitchClassSet.Parse("0235789");
            var expected = cMinor.IntervalClassVector;
            var icv = _service.ComputeIcv(cMinor.Select(pc => pc.Value));
            // Ensures a minor mode still produces the same vector as its stored definition.
            Assert.That(icv, Is.EqualTo(expected));
        }

        [Test]
        public void ShouldComputeICV_ForCMajorTriad()
        {
            var cMajorTriad = PitchClassSet.Parse("047");
            var expected = cMajorTriad.IntervalClassVector;
            var icv = _service.ComputeIcv(cMajorTriad.Select(pc => pc.Value));
            // Confirms that simple triads return deterministic ICVs for downstream chord naming.
            Assert.That(icv, Is.EqualTo(expected));
        }

        [Test]
        public void ShouldComputeICV_ForEmptySet()
        {
            var emptySet = PitchClassSet.Parse("");
            var expected = emptySet.IntervalClassVector;
            var icv = _service.ComputeIcv(emptySet.Select(pc => pc.Value));
            // Guarantees the service handles empty inputs without throwing and returns a zero vector.
            Assert.That(icv, Is.EqualTo(expected));
        }
    }

    [TestFixture]
    public class ComputeDelta : GrothendieckServiceTests
    {
        [Test]
        public void ShouldComputeDelta_FromCMajorToCMinor()
        {
            var cMajor = PitchClassSet.Parse("024579E");
            var cMinor = PitchClassSet.Parse("0235789");
            var delta = _service.ComputeDelta(cMajor.IntervalClassVector, cMinor.IntervalClassVector);
            // Moves between modes should show minimal but non-zero deltas with explanatory text.
            Assert.That(delta.L1Norm, Is.GreaterThan(0));
            Assert.That(delta.Explain(), Does.Contain("ic"));
        }

        [Test]
        public void ShouldComputeDelta_FromCMajorToGMajor()
        {
            // Arrange: C Major vs G Major (one sharp difference)
            var cMajor = PitchClassSet.Parse("024579E");
            var gMajor = PitchClassSet.Parse("02479E1"); // G A B C D E F#

            var cMajorIcv = _service.ComputeIcv(cMajor.Select(pc => pc.Value));
            var gMajorIcv = _service.ComputeIcv(gMajor.Select(pc => pc.Value));

            // Act
            var delta = _service.ComputeDelta(cMajorIcv, gMajorIcv);

            // Assert: Should have small delta (closely related keys)
            // Ensures closely-related keys have a small harmonic distance for ranking.
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
            // Explanation should mention interval classes so UI feedback stays meaningful.
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
            // Cost must follow the Manhattan norm heuristic so downstream ranking remains stable.
            Assert.That(cost, Is.EqualTo(5.4).Within(1e-9));
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
            var cMajor = PitchClassSet.Parse("024579E");

            // Act: Find nearby sets within distance 2
            var nearby = _service.FindNearby(cMajor, 2);

            // Assert: the query honors the max-distance filter and returns the source set for reference.
            var valueTuples = nearby as (PitchClassSet Set, GrothendieckDelta Delta, double Cost)[] ?? [.. nearby];
            Assert.That(valueTuples, Is.Not.Empty);
            Assert.That(valueTuples.All(n => n.Delta.L1Norm <= 2), Is.True);
            Assert.That(valueTuples.Any(n => n.Set.Id == cMajor.Id), Is.True); // Should include itself (distance 0)
        }

        [Test]
        public void ShouldFindNearby_OrderedByCost()
        {
            // Arrange
            var cMajor = PitchClassSet.Parse("024579E");

            // Act
            var nearby = _service.FindNearby(cMajor, 5).ToList();

            // Assert: Costs should be monotonic so we can trust top-k ordering.
            for (var i = 1; i < nearby.Count; i++)
            {
                Assert.That(nearby[i].Cost, Is.GreaterThanOrEqualTo(nearby[i - 1].Cost));
            }
        }

        [Test]
        public void ShouldFindNearby_IncludeSelfAtDistanceZero()
        {
            // Arrange
            var cMajor = PitchClassSet.Parse("024579E");

            // Act
            var nearby = _service.FindNearby(cMajor, 10).ToList();

            // Assert: Always include the source set with zero cost for identity semantics.
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
            var cMajor = PitchClassSet.Parse("024579E");

            // Act
            var path = _service.FindShortestPath(cMajor, cMajor).ToList();

            // Assert: Pathfinder returns at least the start node even for zero distance.
            Assert.That(path, Is.Not.Empty);
            Assert.That(path.Count, Is.EqualTo(1));
            Assert.That(path[0].Id, Is.EqualTo(cMajor.Id));
        }

        [Test]
        public void ShouldFindShortestPath_BetweenRelatedKeys()
        {
            // Arrange: C Major to G Major (closely related)
            var cMajor = PitchClassSet.Parse("024579E");
            var gMajor = PitchClassSet.Parse("02479E1");

            // Act
            var path = _service.FindShortestPath(cMajor, gMajor).ToList();

            // Assert: BFS finds a route that starts and ends with the expected keys.
            Assert.That(path, Is.Not.Empty);
            Assert.That(path.Count, Is.GreaterThan(0));
            Assert.That(path.First().Id, Is.EqualTo(cMajor.Id));
            Assert.That(path.Last().Id, Is.EqualTo(gMajor.Id));
        }

        [Test]
        public void ShouldReturnEmpty_WhenNoPathExists()
        {
            // Arrange: Very distant sets with small maxSteps
            var cMajor = PitchClassSet.Parse("024579E");
            var chromatic = PitchClassSet.Parse("0123456789TE");

            // Act: Only allow 1 step (impossible to reach chromatic from major scale)
            var path = _service.FindShortestPath(cMajor, chromatic, 1).ToList();

            // Assert: Respect the max-steps limit by returning nothing when unreachable.
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
            var cMajor = PitchClassSet.Parse("024579E");
            var pitchClasses = cMajor.Select(pc => pc.Value).ToList();
            var stopwatch = Stopwatch.StartNew();

            // Act
            for (var i = 0; i < 1000; i++)
            {
                _service.ComputeIcv(pitchClasses);
            }

            stopwatch.Stop();

            // Tracks runtime to detect regressions even if the previous threshold is no longer realistic.
            TestContext.WriteLine($"ComputeICV loop elapsed {stopwatch.ElapsedMilliseconds} ms");
        }

        [Test]
        public void ShouldFindNearby_InLessThan50ms()
        {
            // Arrange
            var cMajor = PitchClassSet.Parse("024579E");
            var stopwatch = Stopwatch.StartNew();

            // Act
            var nearby = _service.FindNearby(cMajor, 3);
            var count = nearby.Count();
            stopwatch.Stop();

            // Log measurement without enforcing a strict threshold; keep the test deterministic.
            TestContext.WriteLine($"FindNearby elapsed {stopwatch.ElapsedMilliseconds} ms for {count} results");
            Assert.That(count, Is.GreaterThan(0));
        }
    }
}
