namespace GA.Business.Core.Tests.Fretboard.Invariants;

[TestFixture]
public class ChordPatternEquivalenceTests
{
    [Test]
    public void ChordPatternEquivalences_ShouldCreateTranslationEquivalences()
    {
        // Arrange & Act
        var equivalences = ChordPatternEquivalenceFactory.CreateGuitarChordEquivalences();

        // Assert
        Assert.That(equivalences, Is.Not.Null, "Should create equivalences");
        Assert.That(equivalences.Equivalences.Count, Is.GreaterThan(0), "Should have equivalences");
    }

    [Test]
    public void ChordPatternEquivalences_ShouldFindEquivalentPatterns()
    {
        // Arrange
        var equivalences = ChordPatternEquivalenceFactory.CreateGuitarChordEquivalences();
        var pattern1 = PatternId.FromPattern([1, 3, 3, 2, 1, 1]);
        var pattern2 = PatternId.FromPattern([0, 2, 2, 1, 0, 0]); // Same pattern, normalized

        // Act
        var equivalentPatterns1 = equivalences.FindEquivalentPatterns(pattern1).ToList();
        var equivalentPatterns2 = equivalences.FindEquivalentPatterns(pattern2).ToList();

        // Assert
        Assert.That(equivalentPatterns1.Count, Is.GreaterThan(0), "Should find equivalent patterns");
        Assert.That(equivalentPatterns2.Count, Is.GreaterThan(0), "Should find equivalent patterns");
    }

    [Test]
    public void ChordPatternEquivalences_ShouldGetPrimeForm()
    {
        // Arrange
        var equivalences = ChordPatternEquivalenceFactory.CreateGuitarChordEquivalences();
        var translatedPattern = PatternId.FromPattern([2, 4, 4, 3, 2, 2]);

        // Act
        var primeForm = equivalences.GetPrimeForm(translatedPattern);

        // Assert
        Assert.That(primeForm, Is.Not.Null, "Should find prime form");
        if (primeForm.HasValue)
        {
            var primePattern = primeForm.Value.ToPattern();
            Assert.That(primePattern[0], Is.EqualTo(0), "Prime form should start with 0");
        }
    }

    [Test]
    public void ChordPatternEquivalences_ShouldDetectEquivalence()
    {
        // Arrange
        var equivalences = ChordPatternEquivalenceFactory.CreateGuitarChordEquivalences();
        var pattern1 = PatternId.FromPattern([1, 3, 3, 2, 1, 1]);
        var pattern2 = PatternId.FromPattern([3, 5, 5, 4, 3, 3]); // Same pattern, different position

        // Act
        var areEquivalent = equivalences.AreEquivalent(pattern1, pattern2);

        // Assert
        Assert.That(areEquivalent, Is.True, "Patterns should be equivalent");
    }

    [Test]
    public void ChordPatternVariations_ShouldCreateFromFretArray()
    {
        // Arrange
        var variations = new ChordPatternVariations();
        var frets = new[] { 1, 3, 3, 2, 1, 1 };

        // Act
        var variation = variations.FromFretArray(frets);

        // Assert
        Assert.That(variation, Is.Not.Null, "Should create variation");
        Assert.That(variation.Count, Is.EqualTo(6), "Should have 6 elements");
    }

    [Test]
    public void ChordPatternVariations_ShouldConvertToPatternId()
    {
        // Arrange
        var variations = new ChordPatternVariations();
        var frets = new[] { 0, 2, 2, 1, 0, 0 };
        var variation = variations.FromFretArray(frets);

        // Act
        var patternId = variation.ToPatternId();

        // Assert
        Assert.That(patternId.ToPattern(), Is.EqualTo(frets), "Should convert correctly");
    }

    [Test]
    public void ChordPatternVariations_ShouldConvertToChordInvariant()
    {
        // Arrange
        var variations = new ChordPatternVariations();
        var frets = new[] { 1, 3, 3, 2, 1, 1 };
        var variation = variations.FromFretArray(frets);

        // Act
        var invariant = variation.ToChordInvariant();

        // Assert
        Assert.That(invariant.BaseFret, Is.EqualTo(1), "Should have correct base fret");
        Assert.That(invariant.PatternId.ToPattern(), Is.EqualTo(new[] { 0, 2, 2, 1, 0, 0 }),
            "Should normalize pattern");
    }

    [Test]
    public void ChordPatternEquivalenceFactory_ShouldAnalyzeChordDatabase()
    {
        // Arrange
        var chordDatabase = new List<ChordInvariant>
        {
            ChordInvariant.FromFrets([1, 3, 3, 2, 1, 1], Tuning.Default), // F major
            ChordInvariant.FromFrets([3, 5, 5, 4, 3, 3], Tuning.Default), // G major (same pattern)
            ChordInvariant.FromFrets([5, 7, 7, 6, 5, 5], Tuning.Default), // A major (same pattern)
            ChordInvariant.FromFrets([-1, 3, 2, 0, 1, 0], Tuning.Default) // C major (different pattern)
        };

        // Act
        var analysis = ChordPatternEquivalenceFactory.AnalyzeChordDatabase(chordDatabase);

        // Assert
        Assert.That(analysis.TotalChords, Is.EqualTo(4), "Should count all chords");
        Assert.That(analysis.UniquePatterns, Is.EqualTo(2), "Should identify 2 unique patterns");
        Assert.That(analysis.CompressionRatio, Is.EqualTo(0.5), "Should calculate correct compression ratio");
        Assert.That(analysis.AverageTranspositions, Is.EqualTo(2.0), "Should calculate correct average transpositions");
    }

    [Test]
    public void ChordPatternEquivalence_ShouldIdentifyPrimeForm()
    {
        // Arrange
        var primeEquivalence = new ChordPatternEquivalence(
            PatternId.FromPattern([0, 2, 2, 1, 0, 0]),
            PatternId.FromPattern([0, 2, 2, 1, 0, 0]),
            0, // No translation
            BigInteger.Zero,
            BigInteger.Zero);

        var translatedEquivalence = new ChordPatternEquivalence(
            PatternId.FromPattern([2, 4, 4, 3, 2, 2]),
            PatternId.FromPattern([0, 2, 2, 1, 0, 0]),
            2, // Translated by 2 frets
            BigInteger.One,
            BigInteger.Zero);

        // Act & Assert
        Assert.That(primeEquivalence.IsPrimeForm, Is.True, "Should identify prime form");
        Assert.That(translatedEquivalence.IsPrimeForm, Is.False, "Should identify non-prime form");
        Assert.That(translatedEquivalence.FretOffset, Is.EqualTo(2), "Should have correct fret offset");
    }

    [Test]
    public void ChordPatternEquivalence_ShouldGenerateCorrectString()
    {
        // Arrange
        var primeEquivalence = new ChordPatternEquivalence(
            PatternId.FromPattern([0, 2, 2, 1, 0, 0]),
            PatternId.FromPattern([0, 2, 2, 1, 0, 0]),
            0,
            BigInteger.Zero,
            BigInteger.Zero);

        var translatedEquivalence = new ChordPatternEquivalence(
            PatternId.FromPattern([2, 4, 4, 3, 2, 2]),
            PatternId.FromPattern([0, 2, 2, 1, 0, 0]),
            2,
            BigInteger.One,
            BigInteger.Zero);

        // Act
        var primeString = primeEquivalence.ToString();
        var translatedString = translatedEquivalence.ToString();

        // Assert
        Assert.That(primeString, Does.Contain("Prime"), "Prime form should mention 'Prime'");
        Assert.That(translatedString, Does.Contain("+2"), "Translated form should show offset");
        Assert.That(translatedString, Does.Contain("=>"), "Translated form should show transformation");
    }

    [Test]
    public void ChordPatternAnalysisResult_ShouldGenerateCorrectString()
    {
        // Arrange
        var result = new ChordPatternAnalysisResult(
            100, // Total chords
            25, // Unique patterns
            0.25, // Compression ratio
            4.0, // Average transpositions
            ImmutableDictionary<PatternId, ImmutableList<ChordInvariant>>.Empty);

        // Act
        var resultString = result.ToString();

        // Assert
        Assert.That(resultString, Does.Contain("100"), "Should contain total chords");
        Assert.That(resultString, Does.Contain("25"), "Should contain unique patterns");
        Assert.That(resultString, Does.Contain("25.00%"), "Should contain compression percentage");
        Assert.That(resultString, Does.Contain("4.0"), "Should contain average transpositions");
    }

    [Test]
    public void ChordPatternVariations_ShouldHandleEdgeCases()
    {
        // Test invalid fret array length
        var variations = new ChordPatternVariations();

        Assert.Throws<ArgumentException>(() =>
                variations.FromFretArray([1, 2, 3]), // Wrong length
            "Should throw for invalid array length");

        // Test negative frets (should be converted to 0)
        var negativeFretsArray = new[] { -1, -2, 0, 1, 2, 3 };
        var variation = variations.FromFretArray(negativeFretsArray);

        Assert.That(variation.All(rf => rf.Value >= 0), Is.True,
            "Should convert negative frets to 0");
    }

    [Test]
    public void ChordPatternEquivalences_MathematicalFramework_ComprehensiveTest()
    {
        // Test the mathematical framework comprehensively
        var equivalences = ChordPatternEquivalenceFactory.CreateGuitarChordEquivalences();

        // Test translation equivalence properties
        var testPatterns = new[]
        {
            new[] { 0, 2, 2, 1, 0, 0 }, // E major (prime form)
            new[] { 2, 4, 4, 3, 2, 2 }, // F# major (translated +2)
            new[] { 5, 7, 7, 6, 5, 5 }, // A major (translated +5)
            new[] { 7, 9, 9, 8, 7, 7 } // B major (translated +7)
        };

        var patternIds = testPatterns.Select(p => PatternId.FromPattern(PatternIdExtensions.NormalizePattern(p)))
            .ToArray();

        // All should be equivalent (same prime form)
        for (var i = 0; i < patternIds.Length; i++)
        {
            for (var j = i + 1; j < patternIds.Length; j++)
            {
                Assert.That(equivalences.AreEquivalent(patternIds[i], patternIds[j]), Is.True,
                    $"Patterns {i} and {j} should be equivalent");
            }
        }

        // All should have the same prime form
        var primeForm = equivalences.GetPrimeForm(patternIds[0]);
        Assert.That(primeForm, Is.Not.Null, "Should have prime form");

        foreach (var patternId in patternIds)
        {
            var currentPrime = equivalences.GetPrimeForm(patternId);
            Assert.That(currentPrime, Is.EqualTo(primeForm),
                "All equivalent patterns should have same prime form");
        }
    }

    [Test]
    public void ChordPatternEquivalences_PrimeFormNormalization_Test()
    {
        // Test prime form normalization accuracy
        var equivalences = ChordPatternEquivalenceFactory.CreateGuitarChordEquivalences();

        var testCases = new[]
        {
            // (originalPattern, expectedPrimePattern, description)
            ([3, 5, 5, 4, 3, 3], [0, 2, 2, 1, 0, 0], "G major barre -> E major shape"),
            ([5, 7, 6, 5, 5, 5], [0, 2, 1, 0, 0, 0], "C major shape at 5th fret"),
            ([2, 4, 4, 3, 2, 2], [0, 2, 2, 1, 0, 0], "F# major barre"),
            (new[] { 7, 9, 9, 8, 7, 7 }, new[] { 0, 2, 2, 1, 0, 0 }, "B major barre")
        };

        foreach (var (originalPattern, expectedPrimePattern, description) in testCases)
        {
            // Act
            var normalizedOriginal = PatternIdExtensions.NormalizePattern(originalPattern);
            var patternId = PatternId.FromPattern(normalizedOriginal);
            var primeForm = equivalences.GetPrimeForm(patternId);

            // Assert
            Assert.That(primeForm, Is.Not.Null, $"{description}: Should have prime form");
            if (primeForm.HasValue)
            {
                var primePattern = primeForm.Value.ToPattern();
                Assert.That(primePattern, Is.EqualTo(expectedPrimePattern),
                    $"{description}: Prime form should be {string.Join("-", expectedPrimePattern)}");
            }
        }
    }

    [Test]
    public void ChordPatternEquivalences_TranslationGroup_PropertiesTest()
    {
        // Test that translation equivalences form a proper mathematical group
        var equivalences = ChordPatternEquivalenceFactory.CreateGuitarChordEquivalences();

        // Test reflexivity: every pattern is equivalent to itself
        var testPattern = PatternId.FromPattern([0, 2, 2, 1, 0, 0]);
        Assert.That(equivalences.AreEquivalent(testPattern, testPattern), Is.True,
            "Reflexivity: Pattern should be equivalent to itself");

        // Test symmetry: if A ~ B then B ~ A
        var pattern1 = PatternId.FromPattern([0, 2, 2, 1, 0, 0]);
        var pattern2 = PatternId.FromPattern([2, 4, 4, 3, 2, 2]);

        var equiv12 = equivalences.AreEquivalent(pattern1, pattern2);
        var equiv21 = equivalences.AreEquivalent(pattern2, pattern1);
        Assert.That(equiv12, Is.EqualTo(equiv21),
            "Symmetry: A ~ B should equal B ~ A");

        // Test transitivity: if A ~ B and B ~ C then A ~ C
        var pattern3 = PatternId.FromPattern([5, 7, 7, 6, 5, 5]);

        if (equivalences.AreEquivalent(pattern1, pattern2) &&
            equivalences.AreEquivalent(pattern2, pattern3))
        {
            Assert.That(equivalences.AreEquivalent(pattern1, pattern3), Is.True,
                "Transitivity: If A ~ B and B ~ C then A ~ C");
        }
    }

    [Test]
    public void ChordPatternEquivalences_EquivalenceClasses_PartitionTest()
    {
        // Test that equivalence classes form a proper partition
        var equivalences = ChordPatternEquivalenceFactory.CreateGuitarChordEquivalences();

        // Create a set of test patterns
        var testPatterns = new[]
        {
            PatternId.FromPattern([0, 2, 2, 1, 0, 0]), // E major family
            PatternId.FromPattern([2, 4, 4, 3, 2, 2]), // E major family
            PatternId.FromPattern([5, 7, 7, 6, 5, 5]), // E major family
            PatternId.FromPattern([0, 3, 2, 0, 1, 0]), // C major family
            PatternId.FromPattern([5, 8, 7, 5, 6, 5]), // C major family
            PatternId.FromPattern([3, 5, 5, -1, -1, -1]) // Power chord family
        };

        // Group by prime form
        var groups = testPatterns
            .GroupBy(p => equivalences.GetPrimeForm(p))
            .Where(g => g.Key.HasValue)
            .ToList();

        // Each pattern should belong to exactly one equivalence class
        var totalPatterns = groups.Sum(g => g.Count());
        Assert.That(totalPatterns, Is.EqualTo(testPatterns.Length),
            "Each pattern should belong to exactly one equivalence class");

        // Patterns in the same group should be equivalent
        foreach (var group in groups)
        {
            var groupPatterns = group.ToArray();
            for (var i = 0; i < groupPatterns.Length; i++)
            {
                for (var j = i + 1; j < groupPatterns.Length; j++)
                {
                    Assert.That(equivalences.AreEquivalent(groupPatterns[i], groupPatterns[j]), Is.True,
                        "Patterns in same equivalence class should be equivalent");
                }
            }
        }
    }

    [Test]
    public void ChordPatternEquivalences_LargeDataset_PerformanceTest()
    {
        // Test performance with larger dataset
        var equivalences = ChordPatternEquivalenceFactory.CreateGuitarChordEquivalences();
        var stopwatch = Stopwatch.StartNew();

        // Generate 1000 random patterns
        var random = new Random(42);
        var patterns = new List<PatternId>();

        for (var i = 0; i < 1000; i++)
        {
            var frets = new int[6];
            for (var j = 0; j < 6; j++)
            {
                frets[j] = random.Next(-1, 6);
            }

            try
            {
                var normalized = PatternIdExtensions.NormalizePattern(frets);
                patterns.Add(PatternId.FromPattern(normalized));
            }
            catch (ArgumentException)
            {
                // Skip invalid patterns
            }
        }

        stopwatch.Stop();
        var generationTime = stopwatch.ElapsedMilliseconds;

        // Test equivalence checking performance
        stopwatch.Restart();
        var equivalenceCount = 0;

        for (var i = 0; i < Math.Min(patterns.Count, 100); i++)
        {
            for (var j = i + 1; j < Math.Min(patterns.Count, 100); j++)
            {
                if (equivalences.AreEquivalent(patterns[i], patterns[j]))
                {
                    equivalenceCount++;
                }
            }
        }

        stopwatch.Stop();
        var equivalenceTime = stopwatch.ElapsedMilliseconds;

        // Performance assertions
        Assert.That(generationTime, Is.LessThan(5000), "Pattern generation should be fast");
        Assert.That(equivalenceTime, Is.LessThan(1000), "Equivalence checking should be fast");
        Assert.That(patterns.Count, Is.GreaterThan(500), "Should generate substantial number of valid patterns");

        Console.WriteLine($"Generated {patterns.Count} patterns in {generationTime}ms");
        Console.WriteLine($"Found {equivalenceCount} equivalences in {equivalenceTime}ms");
    }

    [Test]
    public void ChordPatternEquivalences_CompressionRatio_AccuracyTest()
    {
        // Test compression ratio calculation accuracy
        var testChords = new List<ChordInvariant>();

        // Add multiple transpositions of the same patterns
        var basePatterns = new[]
        {
            new[] { 0, 2, 2, 1, 0, 0 }, // E major shape
            new[] { 0, 3, 2, 0, 1, 0 }, // C major shape
            new[] { 3, 5, 5, -1, -1, -1 } // Power chord shape
        };

        foreach (var basePattern in basePatterns)
        {
            // Add 5 transpositions of each pattern
            for (var transpose = 0; transpose < 5; transpose++)
            {
                var transposed = basePattern.Select(f => f >= 0 ? f + transpose : f).ToArray();
                testChords.Add(ChordInvariant.FromFrets(transposed, Tuning.Default));
            }
        }

        // Act
        var analysis = ChordPatternEquivalenceFactory.AnalyzeChordDatabase(testChords);

        // Assert
        Assert.That(analysis.TotalChords, Is.EqualTo(15), "Should have 15 total chords");
        Assert.That(analysis.UniquePatterns, Is.EqualTo(3), "Should have 3 unique patterns");
        Assert.That(analysis.CompressionRatio, Is.EqualTo(0.2).Within(0.01), "Compression ratio should be 3/15 = 0.2");
        Assert.That(analysis.AverageTranspositions, Is.EqualTo(5.0), "Average transpositions should be 5");
    }
}
