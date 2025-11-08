namespace GA.Business.Core.Tests.Fretboard.Invariants;

using Core.Atonal;

[TestFixture]
public class StorageOptimizationTests
{
    [Test]
    public void PatternBasedStorage_BasicOperations_Test()
    {
        // Arrange
        var storage = new PatternBasedChordStorage();
        var testChord = ChordInvariant.FromFrets([0, 2, 2, 1, 0, 0], Tuning.Default);

        // Act
        storage.AddChordVoicing(testChord, PitchClass.E, "E major");
        var stats = storage.GetStatistics();

        // Assert
        Assert.That(stats.TotalVoicings, Is.EqualTo(1), "Should have 1 voicing");
        Assert.That(stats.UniquePatterns, Is.EqualTo(1), "Should have 1 unique pattern");
        Assert.That(stats.CompressionRatio, Is.EqualTo(1.0), "Single chord should have ratio 1.0");
    }

    [Test]
    public void PatternBasedStorage_CompressionRatio_AccuracyTest()
    {
        // Test compression with multiple transpositions of same pattern
        var storage = new PatternBasedChordStorage();
        var basePattern = new[] { 0, 2, 2, 1, 0, 0 }; // E major shape

        // Add 12 transpositions of the same pattern
        for (var i = 0; i < 12; i++)
        {
            var transposed = basePattern.Select(f => f + i).ToArray();
            var invariant = ChordInvariant.FromFrets(transposed, Tuning.Default);
            var root = PitchClass.FromValue((4 + i) % 12); // E + i semitones
            storage.AddChordVoicing(invariant, root, $"{root} major");
        }

        // Act
        var stats = storage.GetStatistics();

        // Assert
        Assert.That(stats.TotalVoicings, Is.EqualTo(12), "Should have 12 voicings");
        Assert.That(stats.UniquePatterns, Is.EqualTo(1), "Should have 1 unique pattern");
        Assert.That(stats.CompressionRatio, Is.EqualTo(1.0 / 12.0).Within(0.01),
            "Compression ratio should be 1/12");
    }

    [Test]
    public void PatternBasedStorage_MultiplePatterns_CompressionTest()
    {
        // Test compression with multiple different patterns
        var storage = new PatternBasedChordStorage();

        var patterns = new[]
        {
            new[] { 0, 2, 2, 1, 0, 0 }, // E major shape
            new[] { 0, 3, 2, 0, 1, 0 }, // C major shape  
            new[] { 3, 5, 5, -1, -1, -1 } // Power chord shape
        };

        var chordNames = new[] { "E", "C", "G5" };
        var roots = new[] { PitchClass.E, PitchClass.C, PitchClass.G };

        // Add 5 transpositions of each pattern
        for (var patternIndex = 0; patternIndex < patterns.Length; patternIndex++)
        {
            for (var transpose = 0; transpose < 5; transpose++)
            {
                var transposed = patterns[patternIndex].Select(f => f >= 0 ? f + transpose : f).ToArray();
                var invariant = ChordInvariant.FromFrets(transposed, Tuning.Default);
                var root = PitchClass.FromValue((roots[patternIndex].Value + transpose) % 12);
                storage.AddChordVoicing(invariant, root, $"{root}{chordNames[patternIndex].Substring(1)}");
            }
        }

        // Act
        var stats = storage.GetStatistics();

        // Assert
        Assert.That(stats.TotalVoicings, Is.EqualTo(15), "Should have 15 voicings");
        Assert.That(stats.UniquePatterns, Is.EqualTo(3), "Should have 3 unique patterns");
        Assert.That(stats.CompressionRatio, Is.EqualTo(3.0 / 15.0).Within(0.01),
            "Compression ratio should be 3/15 = 0.2");
    }

    [Test]
    public void PatternBasedStorage_QueryPerformance_Test()
    {
        // Test query performance with larger dataset
        var storage = PatternBasedStorageFactory.CreateWithCommonPatterns();
        var stopwatch = new Stopwatch();

        // Add many chord voicings
        var random = new Random(42);
        var addedChords = new List<(ChordInvariant invariant, PitchClass root, string name)>();

        stopwatch.Start();
        for (var i = 0; i < 1000; i++)
        {
            var frets = new int[6];
            for (var j = 0; j < 6; j++)
            {
                frets[j] = random.Next(-1, 13); // Frets 0-12, or muted
            }

            try
            {
                var invariant = ChordInvariant.FromFrets(frets, Tuning.Default);
                var root = PitchClass.FromValue(random.Next(12));
                var name = $"{root}";

                storage.AddChordVoicing(invariant, root, name);
                addedChords.Add((invariant, root, name));
            }
            catch (ArgumentException)
            {
                // Skip invalid patterns
            }
        }

        stopwatch.Stop();
        var addTime = stopwatch.ElapsedMilliseconds;

        // Test query performance
        stopwatch.Restart();
        var queryResults = new List<PatternBasedChordStorage.PatternGroup>();

        for (var i = 0; i < Math.Min(100, addedChords.Count); i++)
        {
            var (invariant, _, _) = addedChords[i];
            var group = storage.GetPatternGroup(invariant.PatternId);
            if (group != null)
            {
                queryResults.Add(group);
            }
        }

        stopwatch.Stop();
        var queryTime = stopwatch.ElapsedMilliseconds;

        // Performance assertions
        Assert.That(addTime, Is.LessThan(10000), "Adding 1000 chords should be fast");
        Assert.That(queryTime, Is.LessThan(100), "100 queries should be very fast");
        Assert.That(queryResults.Count, Is.GreaterThan(50), "Should find most queried patterns");

        var stats = storage.GetStatistics();
        Console.WriteLine($"Added {stats.TotalVoicings} voicings in {addTime}ms");
        Console.WriteLine($"Queried 100 patterns in {queryTime}ms");
        Console.WriteLine($"Compression ratio: {stats.CompressionRatio:P2}");
    }

    [Test]
    public void PatternBasedStorage_OptimizeStorage_EfficiencyTest()
    {
        // Test storage optimization removes duplicates
        var storage = new PatternBasedChordStorage();
        var testPattern = ChordInvariant.FromFrets([0, 2, 2, 1, 0, 0], Tuning.Default);

        // Add the same chord multiple times (simulating duplicates)
        for (var i = 0; i < 5; i++)
        {
            storage.AddChordVoicing(testPattern, PitchClass.E, "E major");
        }

        var statsBefore = storage.GetStatistics();

        // Act
        storage.OptimizeStorage();
        var statsAfter = storage.GetStatistics();

        // Assert
        Assert.That(statsBefore.TotalVoicings, Is.EqualTo(5), "Should have 5 voicings before optimization");
        Assert.That(statsAfter.TotalVoicings, Is.EqualTo(1), "Should have 1 voicing after optimization");
        Assert.That(statsAfter.UniquePatterns, Is.EqualTo(1), "Should still have 1 unique pattern");
    }

    [Test]
    public void PatternBasedStorage_ExportImport_DataIntegrityTest()
    {
        // Test data export/import maintains integrity
        var originalStorage = new PatternBasedChordStorage();

        // Add test data
        var testChords = new[]
        {
            ([0, 2, 2, 1, 0, 0], PitchClass.E, "E major"),
            (new[] { 1, 3, 3, 2, 1, 1 }, PitchClass.F, "F major"),
            (new[] { 3, 5, 5, -1, -1, -1 }, PitchClass.G, "G5")
        };

        foreach (var (frets, root, name) in testChords)
        {
            var invariant = ChordInvariant.FromFrets(frets, Tuning.Default);
            originalStorage.AddChordVoicing(invariant, root, name);
        }

        var originalStats = originalStorage.GetStatistics();

        // Act - Export and import
        var exportedData = originalStorage.ExportData();
        var newStorage = new PatternBasedChordStorage();
        newStorage.ImportData(exportedData);
        var importedStats = newStorage.GetStatistics();

        // Assert
        Assert.That(importedStats.TotalVoicings, Is.EqualTo(originalStats.TotalVoicings),
            "Total voicings should be preserved");
        Assert.That(importedStats.UniquePatterns, Is.EqualTo(originalStats.UniquePatterns),
            "Unique patterns should be preserved");
        Assert.That(importedStats.CompressionRatio, Is.EqualTo(originalStats.CompressionRatio).Within(0.01),
            "Compression ratio should be preserved");
    }

    [Test]
    public void PatternBasedStorage_CagedIntegration_Test()
    {
        // Test integration with CAGED system
        var storage = PatternBasedStorageFactory.CreateWithCommonPatterns();
        var stats = storage.GetStatistics();

        // Should have CAGED shapes
        Assert.That(stats.CagedShapeCount, Is.EqualTo(5), "Should have all 5 CAGED shapes");

        // Test CAGED pattern retrieval
        var cagedPatterns = storage.GetCagedPatterns().ToList();
        Assert.That(cagedPatterns.Count, Is.EqualTo(5), "Should retrieve all CAGED patterns");

        foreach (var (shape, group) in cagedPatterns)
        {
            Assert.That(group.CagedShape, Is.EqualTo(shape),
                $"Group should be associated with {shape} shape");
            Assert.That(group.Instances.Count, Is.GreaterThan(0),
                $"{shape} should have instances");
        }
    }

    [Test]
    public void PatternBasedStorage_SimilarPatterns_SearchTest()
    {
        // Test similar pattern search functionality
        var storage = new PatternBasedChordStorage();

        // Add base pattern and similar patterns
        var basePattern = ChordInvariant.FromFrets([0, 2, 2, 1, 0, 0], Tuning.Default);
        var similarPattern = ChordInvariant.FromFrets([0, 2, 2, 2, 0, 0], Tuning.Default); // One finger different
        var differentPattern = ChordInvariant.FromFrets([3, 5, 5, -1, -1, -1], Tuning.Default); // Power chord

        storage.AddChordVoicing(basePattern, PitchClass.E, "E major");
        storage.AddChordVoicing(similarPattern, PitchClass.E, "E major variant");
        storage.AddChordVoicing(differentPattern, PitchClass.G, "G5");

        // Act
        var similarPatterns = storage.FindSimilarPatterns(basePattern.PatternId, 0.7).ToList();

        // Assert
        Assert.That(similarPatterns.Count, Is.GreaterThan(0), "Should find similar patterns");

        // Should find the similar pattern but not the very different one
        var similarities = similarPatterns.Select(sp => sp.similarity).ToArray();
        Assert.That(similarities.Any(s => s > 0.8), Is.True, "Should find highly similar pattern");
        Assert.That(similarities.All(s => s >= 0.7), Is.True, "All results should meet minimum similarity");
    }

    [Test]
    public void PatternBasedStorage_LargeDataset_ScalabilityTest()
    {
        // Test scalability with large dataset (simulating 427k+ chords)
        var storage = new PatternBasedChordStorage();
        var stopwatch = new Stopwatch();
        var random = new Random(42);

        // Generate patterns that will create realistic compression
        var basePatterns = new[]
        {
            new[] { 0, 2, 2, 1, 0, 0 }, // E major family
            new[] { 0, 3, 2, 0, 1, 0 }, // C major family
            new[] { 3, 5, 5, -1, -1, -1 }, // Power chord family
            new[] { 0, 2, 0, 1, 0, 0 }, // 7th chord family
            new[] { 0, 2, 2, 0, 0, 0 } // Minor chord family
        };

        stopwatch.Start();
        var addedCount = 0;

        // Add many transpositions and variations
        foreach (var basePattern in basePatterns)
        {
            // Add 20 transpositions of each base pattern
            for (var transpose = 0; transpose < 20; transpose++)
            {
                var transposed = basePattern.Select(f => f >= 0 ? f + transpose : f).ToArray();

                // Add some variations
                for (var variation = 0; variation < 5; variation++)
                {
                    var varied = transposed.ToArray();

                    // Randomly modify one position slightly
                    if (random.NextDouble() < 0.3) // 30% chance of variation
                    {
                        var pos = random.Next(6);
                        if (varied[pos] > 0)
                        {
                            varied[pos] += random.Next(-1, 2); // ±1 fret
                            varied[pos] = Math.Max(0, varied[pos]);
                        }
                    }

                    try
                    {
                        var invariant = ChordInvariant.FromFrets(varied, Tuning.Default);
                        var root = PitchClass.FromValue(random.Next(12));
                        storage.AddChordVoicing(invariant, root, $"{root}");
                        addedCount++;
                    }
                    catch (ArgumentException)
                    {
                        // Skip invalid patterns
                    }
                }
            }
        }

        stopwatch.Stop();
        var addTime = stopwatch.ElapsedMilliseconds;

        // Test optimization
        stopwatch.Restart();
        storage.OptimizeStorage();
        stopwatch.Stop();
        var optimizeTime = stopwatch.ElapsedMilliseconds;

        var stats = storage.GetStatistics();

        // Performance and compression assertions
        Assert.That(addedCount, Is.GreaterThan(400), "Should add substantial number of chords");
        Assert.That(addTime, Is.LessThan(30000), "Adding large dataset should complete in reasonable time");
        Assert.That(optimizeTime, Is.LessThan(5000), "Optimization should be fast");
        Assert.That(stats.CompressionRatio, Is.LessThan(0.5), "Should achieve significant compression");
        Assert.That(stats.UniquePatterns, Is.LessThan(stats.TotalVoicings / 2),
            "Should have fewer unique patterns than total voicings");

        Console.WriteLine($"Added {addedCount} chords in {addTime}ms");
        Console.WriteLine($"Optimized in {optimizeTime}ms");
        Console.WriteLine($"Final stats: {stats.TotalVoicings} voicings, {stats.UniquePatterns} patterns");
        Console.WriteLine($"Compression ratio: {stats.CompressionRatio:P2}");
    }

    [Test]
    public void PatternBasedStorage_StatisticsAccuracy_Test()
    {
        // Test statistics calculation accuracy
        var storage = new PatternBasedChordStorage();

        // Add known patterns with known characteristics
        var testData = new[]
        {
            // (frets, expectedType, expectedDifficulty)
            ([0, 2, 2, 1, 0, 0], ChordPatternType.Open, ChordDifficulty.Beginner),
            ([1, 3, 3, 2, 1, 1], ChordPatternType.Barre, ChordDifficulty.Intermediate),
            (new[] { 3, 5, 5, -1, -1, -1 }, ChordPatternType.Power, ChordDifficulty.Beginner),
            (new[] { 0, 2, 0, 1, 0, 0 }, ChordPatternType.Open, ChordDifficulty.Intermediate)
        };

        foreach (var (frets, expectedType, expectedDifficulty) in testData)
        {
            var invariant = ChordInvariant.FromFrets(frets, Tuning.Default);
            storage.AddChordVoicing(invariant, PitchClass.C, "C");
        }

        // Act
        var stats = storage.GetStatistics();

        // Assert
        Assert.That(stats.TotalVoicings, Is.EqualTo(4), "Should count all voicings");
        Assert.That(stats.UniquePatterns, Is.EqualTo(4), "Should count all unique patterns");

        // Check pattern type distribution
        Assert.That(stats.PatternsByType.ContainsKey(ChordPatternType.Open), Is.True,
            "Should track open chords");
        Assert.That(stats.PatternsByType.ContainsKey(ChordPatternType.Barre), Is.True,
            "Should track barre chords");
        Assert.That(stats.PatternsByType.ContainsKey(ChordPatternType.Power), Is.True,
            "Should track power chords");

        // Check difficulty distribution
        Assert.That(stats.PatternsByDifficulty.ContainsKey(ChordDifficulty.Beginner), Is.True,
            "Should track beginner chords");
        Assert.That(stats.PatternsByDifficulty.ContainsKey(ChordDifficulty.Intermediate), Is.True,
            "Should track intermediate chords");
    }
}
