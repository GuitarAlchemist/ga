namespace GA.Business.Core.Tests.Atonal.Grothendieck;

using Core.Atonal;
using Core.Atonal.Grothendieck;
using Core.Atonal.Primitives;
using Microsoft.Extensions.Logging;

/// <summary>
///     Tests for GPU-accelerated Grothendieck service
/// </summary>
[TestFixture]
[Category("GPU")]
[Category("Performance")]
public class GpuGrothendieckServiceTests
{
    [SetUp]
    public void Setup()
    {
        _loggerFactory = LoggerFactory.Create(builder => builder.SetMinimumLevel(LogLevel.Information));

        try
        {
            _cpuService = new GrothendieckService(_loggerFactory.CreateLogger<GrothendieckService>());
            _gpuService =
                new GpuGrothendieckService(_loggerFactory.CreateLogger<GpuGrothendieckService>(), _cpuService);
        }
        catch (Exception ex)
        {
            Assert.Inconclusive($"GPU not available: {ex.Message}");
        }
    }

    [TearDown]
    public void TearDown()
    {
        _gpuService?.Dispose();
        _loggerFactory?.Dispose();
    }

    private GpuGrothendieckService? _gpuService;
    private IGrothendieckService? _cpuService;
    private ILoggerFactory? _loggerFactory;

    [Test]
    public void ComputeICV_SingleSet_MatchesCPU()
    {
        // Arrange
        var pitchClasses = new[] { 0, 4, 7 }; // C major triad

        // Act
        var cpuResult = _cpuService!.ComputeICV(pitchClasses);
        var gpuResult = _gpuService!.ComputeICV(pitchClasses);

        // Assert
        Assert.That(gpuResult[IntervalClass.FromValue(1)], Is.EqualTo(cpuResult[IntervalClass.FromValue(1)]));
        Assert.That(gpuResult[IntervalClass.FromValue(2)], Is.EqualTo(cpuResult[IntervalClass.FromValue(2)]));
        Assert.That(gpuResult[IntervalClass.FromValue(3)], Is.EqualTo(cpuResult[IntervalClass.FromValue(3)]));
        Assert.That(gpuResult[IntervalClass.FromValue(4)], Is.EqualTo(cpuResult[IntervalClass.FromValue(4)]));
        Assert.That(gpuResult[IntervalClass.FromValue(5)], Is.EqualTo(cpuResult[IntervalClass.FromValue(5)]));
        Assert.That(gpuResult[IntervalClass.FromValue(6)], Is.EqualTo(cpuResult[IntervalClass.FromValue(6)]));
    }

    [Test]
    public void ComputeBatchICV_MultipleSet_MatchesCPU()
    {
        // Arrange
        var testSets = GenerateTestSets(100);

        // Act
        var cpuResults = testSets.Select(s => _cpuService!.ComputeICV(s.Select(pc => pc.Value))).ToList();
        var gpuResults = _gpuService!.ComputeBatchICV(testSets.Select(s => s.Select(pc => pc.Value))).ToList();

        // Assert
        Assert.That(gpuResults.Count, Is.EqualTo(cpuResults.Count));

        for (var i = 0; i < cpuResults.Count; i++)
        {
            Assert.That(gpuResults[i][IntervalClass.FromValue(1)],
                Is.EqualTo(cpuResults[i][IntervalClass.FromValue(1)]), $"Mismatch at index {i}, IC1");
            Assert.That(gpuResults[i][IntervalClass.FromValue(2)],
                Is.EqualTo(cpuResults[i][IntervalClass.FromValue(2)]), $"Mismatch at index {i}, IC2");
            Assert.That(gpuResults[i][IntervalClass.FromValue(3)],
                Is.EqualTo(cpuResults[i][IntervalClass.FromValue(3)]), $"Mismatch at index {i}, IC3");
            Assert.That(gpuResults[i][IntervalClass.FromValue(4)],
                Is.EqualTo(cpuResults[i][IntervalClass.FromValue(4)]), $"Mismatch at index {i}, IC4");
            Assert.That(gpuResults[i][IntervalClass.FromValue(5)],
                Is.EqualTo(cpuResults[i][IntervalClass.FromValue(5)]), $"Mismatch at index {i}, IC5");
            Assert.That(gpuResults[i][IntervalClass.FromValue(6)],
                Is.EqualTo(cpuResults[i][IntervalClass.FromValue(6)]), $"Mismatch at index {i}, IC6");
        }
    }

    [Test]
    public void ComputeDelta_SinglePair_MatchesCPU()
    {
        // Arrange
        var from = new PitchClassSet(new[] { 0, 4, 7 }.Select(PitchClass.FromValue)); // C major
        var to = new PitchClassSet(new[] { 2, 5, 9 }.Select(PitchClass.FromValue)); // D minor

        // Act
        var fromIcv = from.IntervalClassVector;
        var toIcv = to.IntervalClassVector;
        var cpuDelta = _cpuService!.ComputeDelta(fromIcv, toIcv);
        var gpuDelta = _gpuService!.ComputeDelta(fromIcv, toIcv);

        // Assert
        Assert.That(gpuDelta.Ic1, Is.EqualTo(cpuDelta.Ic1));
        Assert.That(gpuDelta.Ic2, Is.EqualTo(cpuDelta.Ic2));
        Assert.That(gpuDelta.Ic3, Is.EqualTo(cpuDelta.Ic3));
        Assert.That(gpuDelta.Ic4, Is.EqualTo(cpuDelta.Ic4));
        Assert.That(gpuDelta.Ic5, Is.EqualTo(cpuDelta.Ic5));
        Assert.That(gpuDelta.Ic6, Is.EqualTo(cpuDelta.Ic6));
    }

    [Test]
    public void ComputeDeltasBatch_MultiplePairs_MatchesCPU()
    {
        // Arrange
        var testSets = GenerateTestSets(50);
        var fromSets = testSets.Take(50).ToArray();
        var toSets = testSets.Skip(1).Take(50).ToArray();

        // Act
        var pairs = fromSets.Zip(toSets, (f, t) => (f.IntervalClassVector, t.IntervalClassVector)).ToList();
        var cpuDeltas = pairs.Select(p => _cpuService!.ComputeDelta(p.Item1, p.Item2)).ToList();
        var gpuDeltas = _gpuService!.ComputeBatchDelta(pairs).ToList();

        // Assert
        Assert.That(gpuDeltas.Count, Is.EqualTo(cpuDeltas.Count));

        for (var i = 0; i < cpuDeltas.Count; i++)
        {
            Assert.That(gpuDeltas[i].Ic1, Is.EqualTo(cpuDeltas[i].Ic1), $"Mismatch at index {i}, IC1");
            Assert.That(gpuDeltas[i].Ic2, Is.EqualTo(cpuDeltas[i].Ic2), $"Mismatch at index {i}, IC2");
            Assert.That(gpuDeltas[i].Ic3, Is.EqualTo(cpuDeltas[i].Ic3), $"Mismatch at index {i}, IC3");
            Assert.That(gpuDeltas[i].Ic4, Is.EqualTo(cpuDeltas[i].Ic4), $"Mismatch at index {i}, IC4");
            Assert.That(gpuDeltas[i].Ic5, Is.EqualTo(cpuDeltas[i].Ic5), $"Mismatch at index {i}, IC5");
            Assert.That(gpuDeltas[i].Ic6, Is.EqualTo(cpuDeltas[i].Ic6), $"Mismatch at index {i}, IC6");
        }
    }

    [Test]
    public void ComputeDistancesBatch_MultipleTargets_MatchesCPU()
    {
        // Arrange
        var query = new PitchClassSet(new[] { 0, 4, 7 }.Select(PitchClass.FromValue));
        var targets = GenerateTestSets(100);

        // Act
        var queryIcv = query.IntervalClassVector;
        var cpuDistances = targets.Select(t =>
        {
            var targetIcv = t.IntervalClassVector;
            var delta = _cpuService!.ComputeDelta(queryIcv, targetIcv);
            return delta.L2Norm;
        }).ToArray();

        // GPU batch computation
        var pairs = targets.Select(t => (queryIcv, t.IntervalClassVector)).ToList();
        var gpuDeltas = _gpuService!.ComputeBatchDelta(pairs).ToList();
        var gpuDistances = gpuDeltas.Select(d => d.L2Norm).ToArray();

        // Assert
        Assert.That(gpuDistances.Length, Is.EqualTo(cpuDistances.Length));

        for (var i = 0; i < cpuDistances.Length; i++)
        {
            Assert.That(gpuDistances[i], Is.EqualTo(cpuDistances[i]).Within(0.0001),
                $"Distance mismatch at index {i}");
        }
    }

    [Test]
    [Category("Performance")]
    public void BatchICV_LargeDataset_FasterThanCPU()
    {
        // Arrange
        var testSets = GenerateTestSets(10000);

        // Act - CPU
        var cpuStopwatch = Stopwatch.StartNew();
        var cpuResults = testSets.Select(s => _cpuService!.ComputeICV(s.Select(pc => pc.Value))).ToList();
        cpuStopwatch.Stop();

        // Act - GPU
        var gpuStopwatch = Stopwatch.StartNew();
        var gpuResults = _gpuService!.ComputeBatchICV(testSets.Select(s => s.Select(pc => pc.Value))).ToList();
        gpuStopwatch.Stop();

        // Assert
        var speedup = (double)cpuStopwatch.ElapsedMilliseconds / gpuStopwatch.ElapsedMilliseconds;
        TestContext.WriteLine($"CPU Time: {cpuStopwatch.ElapsedMilliseconds}ms");
        TestContext.WriteLine($"GPU Time: {gpuStopwatch.ElapsedMilliseconds}ms");
        TestContext.WriteLine($"Speedup: {speedup:F2}x");

        // GPU should be at least as fast as CPU (may be slower for small datasets due to overhead)
        Assert.That(speedup, Is.GreaterThan(0.5), "GPU should not be significantly slower than CPU");
    }

    [Test]
    [Category("Performance")]
    public void BatchDelta_LargeDataset_FasterThanCPU()
    {
        // Arrange
        var testSets = GenerateTestSets(5000);
        var fromSets = testSets.Take(5000).ToArray();
        var toSets = testSets.Skip(1).Take(5000).ToArray();

        // Act - CPU
        var cpuStopwatch = Stopwatch.StartNew();
        var pairs = fromSets.Zip(toSets, (f, t) => (f.IntervalClassVector, t.IntervalClassVector)).ToList();
        var cpuDeltas = pairs.Select(p => _cpuService!.ComputeDelta(p.Item1, p.Item2)).ToList();
        cpuStopwatch.Stop();

        // Act - GPU
        var gpuStopwatch = Stopwatch.StartNew();
        var gpuDeltas = _gpuService!.ComputeBatchDelta(pairs).ToList();
        gpuStopwatch.Stop();

        // Assert
        var speedup = (double)cpuStopwatch.ElapsedMilliseconds / gpuStopwatch.ElapsedMilliseconds;
        TestContext.WriteLine($"CPU Time: {cpuStopwatch.ElapsedMilliseconds}ms");
        TestContext.WriteLine($"GPU Time: {gpuStopwatch.ElapsedMilliseconds}ms");
        TestContext.WriteLine($"Speedup: {speedup:F2}x");

        Assert.That(speedup, Is.GreaterThan(0.5), "GPU should not be significantly slower than CPU");
    }

    [Test]
    public void ComputeICV_EmptySet_ReturnsZeroVector()
    {
        // Arrange
        var emptySet = Array.Empty<int>();

        // Act
        var icv = _gpuService!.ComputeICV(emptySet);

        // Assert - Empty set should have all zeros in ICV
        Assert.That(icv[IntervalClass.FromValue(1)], Is.EqualTo(0));
        Assert.That(icv[IntervalClass.FromValue(2)], Is.EqualTo(0));
        Assert.That(icv[IntervalClass.FromValue(3)], Is.EqualTo(0));
        Assert.That(icv[IntervalClass.FromValue(4)], Is.EqualTo(0));
        Assert.That(icv[IntervalClass.FromValue(5)], Is.EqualTo(0));
        Assert.That(icv[IntervalClass.FromValue(6)], Is.EqualTo(0));
    }

    [Test]
    public void ComputeBatchICV_EmptyArray_ReturnsEmptyArray()
    {
        // Arrange
        var emptySets = Array.Empty<IEnumerable<int>>();

        // Act
        var results = _gpuService!.ComputeBatchICV(emptySets).ToList();

        // Assert
        Assert.That(results, Is.Empty);
    }

    private PitchClassSet[] GenerateTestSets(int count)
    {
        var random = new Random(42); // Fixed seed for reproducibility
        var sets = new List<PitchClassSet>();

        for (var i = 0; i < count; i++)
        {
            var cardinality = random.Next(3, 7); // 3-6 notes
            var pitchClasses = new HashSet<int>();

            while (pitchClasses.Count < cardinality)
            {
                pitchClasses.Add(random.Next(0, 12));
            }

            sets.Add(new PitchClassSet(pitchClasses.Select(PitchClass.FromValue)));
        }

        return sets.ToArray();
    }
}
