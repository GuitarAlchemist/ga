namespace GA.Business.Core.Tests.Performance;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
// using GA.Business.Core.Services; // Namespace does not exist

[TestFixture]
[Category("Performance")]
public class InvariantPerformanceBenchmarks
{
    [SetUp]
    public void SetUp()
    {
        var services = new ServiceCollection();
        services.AddLogging(builder =>
            builder.SetMinimumLevel(LogLevel.Warning)); // Reduce logging for performance tests
        services.AddCachedInvariantValidation();

        _serviceProvider = services.BuildServiceProvider();
        _validationService = _serviceProvider.GetRequiredService<InvariantValidationService>();
        _cachedValidationService = _serviceProvider.GetRequiredService<CachedInvariantValidationService>();
    }

    [TearDown]
    public void TearDown()
    {
        _serviceProvider?.Dispose();
    }

    private ServiceProvider _serviceProvider;
    private InvariantValidationService _validationService;
    private CachedInvariantValidationService _cachedValidationService;

    [Test]
    [TestCase(100)]
    [TestCase(1000)]
    [TestCase(5000)]
    public void BenchmarkIconicChordValidation_VariousVolumes_ShouldMeetPerformanceTargets(int validationCount)
    {
        // Arrange
        var chord = CreateValidIconicChord();
        var stopwatch = new Stopwatch();

        // Warm up
        for (var i = 0; i < 10; i++)
        {
            _validationService.ValidateIconicChord(chord);
        }

        // Act
        stopwatch.Start();
        for (var i = 0; i < validationCount; i++)
        {
            var result = _validationService.ValidateIconicChord(chord);
            Assert.That(result.IsValid, Is.True);
        }

        stopwatch.Stop();

        // Assert performance targets
        var totalMs = stopwatch.ElapsedMilliseconds;
        var avgMs = (double)totalMs / validationCount;

        Console.WriteLine($"Validated {validationCount} chords in {totalMs}ms (avg: {avgMs:F2}ms per validation)");

        // Performance targets:
        // - Average validation should be under 5ms
        // - Total time should scale linearly
        Assert.That(avgMs, Is.LessThan(5.0), $"Average validation time {avgMs:F2}ms exceeds 5ms target");

        if (validationCount >= 1000)
        {
            Assert.That(totalMs, Is.LessThan(validationCount * 2), $"Total time {totalMs}ms suggests poor scaling");
        }
    }

    [Test]
    public void BenchmarkCachedVsUncached_SameChord_ShouldShowCacheImprovement()
    {
        // Arrange
        var chord = CreateValidIconicChord();
        var iterations = 1000;

        // Benchmark uncached
        var uncachedStopwatch = Stopwatch.StartNew();
        for (var i = 0; i < iterations; i++)
        {
            _validationService.ValidateIconicChord(chord);
        }

        uncachedStopwatch.Stop();

        // Benchmark cached (first call will populate cache)
        _cachedValidationService.ValidateIconicChord(chord);

        var cachedStopwatch = Stopwatch.StartNew();
        for (var i = 0; i < iterations; i++)
        {
            _cachedValidationService.ValidateIconicChord(chord);
        }

        cachedStopwatch.Stop();

        // Assert cache provides significant improvement
        var uncachedMs = uncachedStopwatch.ElapsedMilliseconds;
        var cachedMs = cachedStopwatch.ElapsedMilliseconds;
        var improvement = (double)(uncachedMs - cachedMs) / uncachedMs;

        Console.WriteLine($"Uncached: {uncachedMs}ms, Cached: {cachedMs}ms, Improvement: {improvement:P}");

        // Cache should provide at least 50% improvement for repeated validations
        Assert.That(improvement, Is.GreaterThan(0.5),
            $"Cache improvement {improvement:P} is less than expected 50%");
    }

    [Test]
    public void BenchmarkAllConceptTypes_ShouldValidatePerformanceConsistency()
    {
        // Arrange
        var iconicChord = CreateValidIconicChord();
        var chordProgression = CreateValidChordProgression();
        var guitarTechnique = CreateValidGuitarTechnique();
        var specializedTuning = CreateValidSpecializedTuning();

        var iterations = 100;
        var results = new Dictionary<string, long>();

        // Benchmark each concept type
        var conceptTypes = new[]
        {
            ("IconicChord", (Func<object>)(() => _validationService.ValidateIconicChord(iconicChord))),
            ("ChordProgression", (Func<object>)(() => _validationService.ValidateChordProgression(chordProgression))),
            ("GuitarTechnique", (Func<object>)(() => _validationService.ValidateGuitarTechnique(guitarTechnique))),
            ("SpecializedTuning", (Func<object>)(() => _validationService.ValidateSpecializedTuning(specializedTuning)))
        };

        foreach (var (name, validator) in conceptTypes)
        {
            // Warm up
            for (var i = 0; i < 5; i++)
            {
                validator();
            }

            // Benchmark
            var stopwatch = Stopwatch.StartNew();
            for (var i = 0; i < iterations; i++)
            {
                validator();
            }

            stopwatch.Stop();

            results[name] = stopwatch.ElapsedMilliseconds;
            Console.WriteLine($"{name}: {stopwatch.ElapsedMilliseconds}ms for {iterations} validations");
        }

        // Assert all concept types perform reasonably
        foreach (var (conceptType, totalMs) in results)
        {
            var avgMs = (double)totalMs / iterations;
            Assert.That(avgMs, Is.LessThan(10.0),
                $"{conceptType} average validation time {avgMs:F2}ms exceeds 10ms target");
        }

        // Assert performance consistency (no concept type should be more than 3x slower than the fastest)
        var fastest = results.Values.Min();
        var slowest = results.Values.Max();
        var ratio = (double)slowest / fastest;

        Assert.That(ratio, Is.LessThan(3.0),
            $"Performance inconsistency: slowest ({slowest}ms) is {ratio:F1}x slower than fastest ({fastest}ms)");
    }

    [Test]
    public void BenchmarkMemoryUsage_LargeVolume_ShouldNotLeakMemory()
    {
        // Arrange
        var chord = CreateValidIconicChord();
        var iterations = 10000;

        // Measure initial memory
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        var initialMemory = GC.GetTotalMemory(false);

        // Act - Perform many validations
        for (var i = 0; i < iterations; i++)
        {
            var result = _validationService.ValidateIconicChord(chord);

            // Occasionally force garbage collection to prevent memory buildup during test
            if (i % 1000 == 0)
            {
                GC.Collect();
            }
        }

        // Measure final memory
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        var finalMemory = GC.GetTotalMemory(false);

        var memoryIncrease = finalMemory - initialMemory;
        var memoryIncreaseKb = memoryIncrease / 1024.0;

        Console.WriteLine($"Memory increase after {iterations} validations: {memoryIncreaseKb:F1} KB");

        // Assert memory usage is reasonable (less than 1MB increase for 10k validations)
        Assert.That(memoryIncrease, Is.LessThan(1024 * 1024),
            $"Memory increase of {memoryIncreaseKb:F1} KB suggests memory leak");
    }

    [Test]
    public void BenchmarkConcurrentValidation_MultipleThreads_ShouldScaleWell()
    {
        // Arrange
        var chord = CreateValidIconicChord();
        var validationsPerThread = 100;
        var threadCounts = new[] { 1, 2, 4, 8 };
        var results = new Dictionary<int, long>();

        foreach (var threadCount in threadCounts)
        {
            var tasks = new List<Task>();
            var stopwatch = Stopwatch.StartNew();

            // Act - Run validations concurrently
            for (var t = 0; t < threadCount; t++)
            {
                tasks.Add(Task.Run(() =>
                {
                    for (var i = 0; i < validationsPerThread; i++)
                    {
                        _validationService.ValidateIconicChord(chord);
                    }
                }));
            }

            Task.WaitAll(tasks.ToArray());
            stopwatch.Stop();

            results[threadCount] = stopwatch.ElapsedMilliseconds;
            Console.WriteLine(
                $"{threadCount} threads: {stopwatch.ElapsedMilliseconds}ms for {threadCount * validationsPerThread} total validations");
        }

        // Assert scaling efficiency
        var singleThreadTime = results[1];
        var maxThreadTime = results[threadCounts.Max()];
        var maxThreads = threadCounts.Max();

        // With perfect scaling, max threads should be ~maxThreads times faster
        // We'll accept if it's at least 50% of theoretical maximum
        var theoreticalSpeedup = maxThreads;
        var actualSpeedup = (double)singleThreadTime / maxThreadTime;
        var efficiency = actualSpeedup / theoreticalSpeedup;

        Console.WriteLine(
            $"Scaling efficiency: {efficiency:P} (actual speedup: {actualSpeedup:F1}x, theoretical: {theoreticalSpeedup}x)");

        Assert.That(efficiency, Is.GreaterThan(0.3),
            $"Concurrent scaling efficiency {efficiency:P} is too low");
    }

    private static IconicChordDefinition CreateValidIconicChord()
    {
        return new IconicChordDefinition
        {
            Name = "C Major",
            TheoreticalName = "Cmaj",
            PitchClasses = [0, 4, 7],
            GuitarVoicing = [-1, 3, 2, 0, 1, 0],
            Artist = "Various",
            Song = "Many Songs",
            Genre = "Classical",
            Era = "Classical Period"
        };
    }

    private static ChordProgressionDefinition CreateValidChordProgression()
    {
        return new ChordProgressionDefinition
        {
            Name = "ii-V-I",
            RomanNumerals = ["ii", "V", "I"],
            Category = "Jazz",
            Difficulty = "Intermediate",
            Function = ["Predominant", "Dominant", "Tonic"],
            InKey = "C major",
            Chords = ["Dm7", "G7", "Cmaj7"],
            UsedBy = ["Many Jazz Standards"]
        };
    }

    private static GuitarTechniqueDefinition CreateValidGuitarTechnique()
    {
        return new GuitarTechniqueDefinition
        {
            Name = "Alternate Picking",
            Category = "Picking",
            Difficulty = "Intermediate",
            Description = "A fundamental picking technique that alternates between downstrokes and upstrokes",
            Concept = "Efficient picking motion for speed and accuracy",
            Theory = "Based on mechanical efficiency and string attack consistency",
            Technique = "Start with slow, deliberate motions. Focus on consistent pick angle and depth",
            Artists = ["Paul Gilbert", "John Petrucci"],
            Songs = ["Technical Difficulties", "Glasgow Kiss"],
            Benefits = ["Increased speed", "Better accuracy", "Reduced fatigue"]
        };
    }

    private static SpecializedTuningDefinition CreateValidSpecializedTuning()
    {
        return new SpecializedTuningDefinition
        {
            Name = "Drop D",
            Category = "Drop Tunings",
            PitchClasses = [2, 9, 2, 7, 11, 4],
            TuningPattern = "D-A-D-G-B-E",
            Interval = "Perfect fourth intervals with dropped sixth string",
            Description = "A popular alternate tuning that lowers the sixth string by a whole step",
            TonalCharacteristics = ["Darker tone", "Easier power chords", "Open string resonance"],
            Applications = ["Rock music", "Metal", "Power chord progressions"],
            Artists = ["Foo Fighters", "Soundgarden", "Tool"]
        };
    }
}
