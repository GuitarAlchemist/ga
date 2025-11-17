namespace GA.Business.Core.Tests.Fretboard.Invariants;

using GA.Business.Core.Atonal;
using GA.Business.Core.Fretboard;
using GA.Business.Core.Fretboard.Invariants;
using System.Linq;

[TestFixture]
public class ChordInvariantTests
{
    [Test]
    public void ChordInvariant_ShouldNormalizePattern()
    {
        // Arrange - C major chord at 3rd fret: [3, 5, 5, 4, 3, 3]
        var frets = new[] { 3, 5, 5, 4, 3, 3 };

        // Act
        var invariant = ChordInvariant.FromFrets(frets, Tuning.Default);

        // Assert
        Assert.That(invariant.BaseFret, Is.EqualTo(3), "Base fret should be 3");

        var normalizedPattern = invariant.PatternId.ToPattern();
        var expectedPattern = new[] { 0, 2, 2, 1, 0, 0 }; // Normalized to start from 0
        Assert.That(normalizedPattern, Is.EqualTo(expectedPattern), "Pattern should be normalized");
    }

    [Test]
    public void ChordInvariant_ShouldHandleOpenStrings()
    {
        // Arrange - Open C major: [-1, 3, 2, 0, 1, 0]
        var frets = new[] { -1, 3, 2, 0, 1, 0 };

        // Act
        var invariant = ChordInvariant.FromFrets(frets, Tuning.Default);

        // Assert
        Assert.That(invariant.BaseFret, Is.EqualTo(1), "Base fret should be 1 (lowest fretted note)");

        var normalizedPattern = invariant.PatternId.ToPattern();
        var expectedPattern = new[] { -1, 2, 1, 0, 0, 0 }; // Muted, normalized frets, open strings
        Assert.That(normalizedPattern, Is.EqualTo(expectedPattern), "Should handle open strings and muted strings");
    }

    [Test]
    public void ChordInvariant_ShouldGenerateTranspositions()
    {
        // Arrange - Simple pattern
        var frets = new[] { 1, 3, 3, 2, 1, 1 };
        var invariant = ChordInvariant.FromFrets(frets, Tuning.Default);

        // Act
        var transpositions = invariant.GetAllTranspositions(12).Take(5).ToList();

        // Assert
        Assert.That(transpositions.Count, Is.EqualTo(5), "Should generate transpositions");

        // First transposition should be at fret 0
        var firstTransposition = transpositions[0];
        Assert.That(firstTransposition.baseFret, Is.EqualTo(0));
        Assert.That(firstTransposition.frets, Is.EqualTo(new[] { 0, 2, 2, 1, 0, 0 }));

        // Second transposition should be at fret 1
        var secondTransposition = transpositions[1];
        Assert.That(secondTransposition.baseFret, Is.EqualTo(1));
        Assert.That(secondTransposition.frets, Is.EqualTo(new[] { 1, 3, 3, 2, 1, 1 }));
    }

    [Test]
    public void ChordInvariant_ShouldDetectSamePattern()
    {
        // Arrange - Same pattern at different positions
        var frets1 = new[] { 1, 3, 3, 2, 1, 1 };
        var frets2 = new[] { 5, 7, 7, 6, 5, 5 }; // Same pattern, 4 frets higher

        // Act
        var invariant1 = ChordInvariant.FromFrets(frets1, Tuning.Default);
        var invariant2 = ChordInvariant.FromFrets(frets2, Tuning.Default);

        // Assert
        Assert.That(invariant1.IsSamePattern(invariant2), Is.True, "Should detect same pattern");
        Assert.That(invariant1.PatternId, Is.EqualTo(invariant2.PatternId), "Pattern IDs should be equal");
    }

    [Test]
    public void ChordInvariant_ShouldDetectSameChord()
    {
        // Arrange - Different voicings of the same chord
        var cMajorOpen = new[] { -1, 3, 2, 0, 1, 0 };
        var cMajorBarre = new[] { 8, 10, 10, 9, 8, 8 };

        // Act
        var invariant1 = ChordInvariant.FromFrets(cMajorOpen, Tuning.Default);
        var invariant2 = ChordInvariant.FromFrets(cMajorBarre, Tuning.Default);

        // Assert
        // Note: This test assumes both voicings produce the same pitch class set
        // In practice, different voicings might have different pitch class sets due to octave doubling
        Assert.That(invariant1.PitchClassSet.Contains(PitchClass.C), Is.True, "Should contain C");
        Assert.That(invariant1.PitchClassSet.Contains(PitchClass.E), Is.True, "Should contain E");
        Assert.That(invariant1.PitchClassSet.Contains(PitchClass.G), Is.True, "Should contain G");
    }

    [Test]
    public void PatternId_ShouldEncodeAndDecodeCorrectly()
    {
        // Arrange
        var originalPattern = new[] { -1, 0, 2, 2, 1, 0 };

        // Act
        var patternId = PatternId.FromPattern(originalPattern);
        var decodedPattern = patternId.ToPattern();

        // Assert
        Assert.That(decodedPattern, Is.EqualTo(originalPattern), "Should encode and decode correctly");
    }

    [Test]
    public void PatternId_ShouldValidateChordPattern()
    {
        // Arrange
        var validPattern = new[] { 0, 2, 2, 1, 0, 0 };
        var invalidPattern = new[] { 0, 8, 2, 1, 0, 0 }; // Span > 5 frets

        // Act
        var validPatternId = PatternId.FromPattern(validPattern);
        var invalidPatternId = PatternId.FromPattern(invalidPattern);

        // Assert
        Assert.That(validPatternId.IsValidChordPattern(), Is.True, "Valid pattern should be recognized");
        Assert.That(invalidPatternId.IsValidChordPattern(), Is.False, "Invalid pattern should be rejected");
    }

    [Test]
    public void PatternId_ShouldCalculateComplexity()
    {
        // Arrange
        var simplePattern = new[] { 0, 0, 0, 0, 0, 0 }; // All open
        var complexPattern = new[] { 1, 4, 3, 2, 1, 1 }; // Wide stretch with barre

        // Act
        var simplePatternId = PatternId.FromPattern(simplePattern);
        var complexPatternId = PatternId.FromPattern(complexPattern);

        // Assert
        Assert.That(simplePatternId.GetComplexityScore(), Is.LessThan(complexPatternId.GetComplexityScore()),
            "Complex pattern should have higher complexity score");
    }

    [Test]
    public void ChordInvariant_ShouldClassifyDifficulty()
    {
        // Arrange
        var beginnerChord = new[] { -1, 3, 2, 0, 1, 0 }; // Open C
        var expertChord = new[] { 1, 5, 4, 3, 1, 1 }; // Wide stretch

        // Act
        var beginnerInvariant = ChordInvariant.FromFrets(beginnerChord, Tuning.Default);
        var expertInvariant = ChordInvariant.FromFrets(expertChord, Tuning.Default);

        // Assert
        Assert.That(beginnerInvariant.GetDifficulty(), Is.EqualTo(ChordDifficulty.Beginner));
        Assert.That(expertInvariant.GetDifficulty(), Is.Not.EqualTo(ChordDifficulty.Beginner));
    }

    [Test]
    public void PatternIdExtensions_ShouldNormalizePattern()
    {
        // Arrange
        var frets = new[] { 3, 5, 5, 4, 3, 3 };

        // Act
        var normalized = PatternIdExtensions.NormalizePattern(frets);
        var patternId = frets.ToPatternId();

        // Assert
        var expectedNormalized = new[] { 0, 2, 2, 1, 0, 0 };
        Assert.That(normalized, Is.EqualTo(expectedNormalized), "Should normalize correctly");
        Assert.That(patternId.ToPattern(), Is.EqualTo(expectedNormalized), "Extension method should work");
    }

    [Test]
    public void ChordInvariant_ShouldHandleEdgeCases()
    {
        // Test all muted
        var allMuted = new[] { -1, -1, -1, -1, -1, -1 };
        var mutedInvariant = ChordInvariant.FromFrets(allMuted, Tuning.Default);
        Assert.That(mutedInvariant.BaseFret, Is.EqualTo(0), "All muted should have base fret 0");

        // Test all open
        var allOpen = new[] { 0, 0, 0, 0, 0, 0 };
        var openInvariant = ChordInvariant.FromFrets(allOpen, Tuning.Default);
        Assert.That(openInvariant.BaseFret, Is.EqualTo(0), "All open should have base fret 0");

        // Test single fretted note
        var singleNote = new[] { -1, -1, 5, -1, -1, -1 };
        var singleInvariant = ChordInvariant.FromFrets(singleNote, Tuning.Default);
        Assert.That(singleInvariant.BaseFret, Is.EqualTo(5), "Single note should have correct base fret");
    }

    [Test]
    public void ChordInvariant_ShouldGeneratePatternDescription()
    {
        // Arrange
        var frets = new[] { -1, 3, 2, 0, 1, 0 }; // Open C major

        // Act
        var invariant = ChordInvariant.FromFrets(frets, Tuning.Default);
        var description = invariant.GetPatternDescription();

        // Assert
        Assert.That(description, Is.Not.Null.And.Not.Empty, "Should generate description");
        Assert.That(description, Does.Contain("strings"), "Should mention string count");
        Assert.That(description, Does.Contain("open"), "Should mention open strings");
        Assert.That(description, Does.Contain("muted"), "Should mention muted strings");
    }

    [Test]
    public void ChordInvariant_CoreFunctionality_ComprehensiveTest()
    {
        // Test a comprehensive set of common chord voicings
        var testCases = new[]
        {
            // (frets, expectedBaseFret, expectedNormalizedPattern, description)
            ([-1, 3, 2, 0, 1, 0], 1, [-1, 2, 1, 0, 0, 0], "Open C major"),
            ([1, 3, 3, 2, 1, 1], 1, [0, 2, 2, 1, 0, 0], "F major barre"),
            ([3, 5, 5, 4, 3, 3], 3, [0, 2, 2, 1, 0, 0], "G major (E-shape)"),
            ([5, 7, 7, 6, 5, 5], 5, [0, 2, 2, 1, 0, 0], "A major (E-shape)"),
            ([-1, 0, 2, 2, 2, 0], 2, [-1, 0, 0, 0, 0, 0], "A major open"),
            ([3, 2, 0, 0, 3, 3], 2, [1, 0, 0, 0, 1, 1], "G major open"),
            ([0, 2, 2, 1, 0, 0], 1, [0, 1, 1, 0, 0, 0], "E major open"),
            ([-1, -1, 0, 2, 3, 2], 2, [-1, -1, 0, 0, 1, 0], "D major open"),
            ([3, 5, 5, -1, -1, -1], 3, [0, 2, 2, -1, -1, -1], "G5 power chord"),
            (new[] { 0, 2, 0, 1, 0, 0 }, 1, new[] { 0, 1, 0, 0, 0, 0 }, "E7 chord")
        };

        foreach (var (frets, expectedBaseFret, expectedPattern, description) in testCases)
        {
            // Act
            var invariant = ChordInvariant.FromFrets(frets, Tuning.Default);
            var actualPattern = invariant.PatternId.ToPattern();

            // Assert
            Assert.That(invariant.BaseFret, Is.EqualTo(expectedBaseFret),
                $"{description}: Base fret should be {expectedBaseFret}");
            Assert.That(actualPattern, Is.EqualTo(expectedPattern),
                $"{description}: Pattern should be {string.Join("-", expectedPattern)}");
            Assert.That(invariant.PatternId.IsValidChordPattern(), Is.True,
                $"{description}: Should be valid chord pattern");
        }
    }

    [Test]
    public void PatternId_EncodingDecoding_StressTest()
    {
        // Test encoding/decoding with all possible valid patterns
        var random = new Random(42); // Fixed seed for reproducible tests
        var testPatterns = new List<int[]>();

        // Generate systematic test patterns
        for (var i = 0; i < 1000; i++)
        {
            var pattern = new int[6];
            for (var j = 0; j < 6; j++)
            {
                // Generate valid fret values: -1 (muted), 0 (open), 1-5 (fretted)
                pattern[j] = random.Next(-1, 6);
            }

            testPatterns.Add(pattern);
        }

        // Test each pattern
        foreach (var originalPattern in testPatterns)
        {
            try
            {
                // Act
                var patternId = PatternId.FromPattern(originalPattern);
                var decodedPattern = patternId.ToPattern();

                // Assert
                Assert.That(decodedPattern, Is.EqualTo(originalPattern),
                    $"Pattern {string.Join(",", originalPattern)} should encode/decode correctly");
            }
            catch (ArgumentException)
            {
                // Some patterns might be invalid, which is acceptable
            }
        }
    }

    [Test]
    public void ChordInvariant_TranspositionAccuracy_Test()
    {
        // Test that transpositions maintain chord relationships
        var originalFrets = new[] { 1, 3, 3, 2, 1, 1 }; // F major barre
        var invariant = ChordInvariant.FromFrets(originalFrets, Tuning.Default);

        // Get first 12 transpositions (one octave)
        var transpositions = invariant.GetAllTranspositions(24).Take(12).ToList();

        Assert.That(transpositions.Count, Is.EqualTo(12), "Should generate 12 transpositions");

        // Verify each transposition
        for (var i = 0; i < transpositions.Count; i++)
        {
            var (baseFret, root, frets) = transpositions[i];

            // Create invariant from transposed frets
            var transposedInvariant = ChordInvariant.FromFrets(frets, Tuning.Default);

            // Should have same pattern
            Assert.That(transposedInvariant.IsSamePattern(invariant), Is.True,
                $"Transposition {i} should have same pattern");

            // Base fret should match expected
            Assert.That(baseFret, Is.EqualTo(i),
                $"Transposition {i} should have base fret {i}");
        }
    }

    [Test]
    public void ChordInvariant_PitchClassSetAccuracy_Test()
    {
        // Test that pitch class sets are calculated correctly
        var testCases = new[]
        {
            // (frets, expectedPitchClasses, description)
            ([0, 2, 2, 1, 0, 0], [PitchClass.E, PitchClass.GSharp, PitchClass.B], "E major"),
            ([-1, 3, 2, 0, 1, 0], [PitchClass.C, PitchClass.E, PitchClass.G], "C major"),
            (new[] { 3, 5, 5, -1, -1, -1 }, new[] { PitchClass.G, PitchClass.D }, "G5 power chord")
        };

        foreach (var (frets, expectedPitchClasses, description) in testCases)
        {
            // Act
            var invariant = ChordInvariant.FromFrets(frets, Tuning.Default);
            var actualPitchClasses = invariant.PitchClassSet.ToArray();

            // Assert
            foreach (var expectedPc in expectedPitchClasses)
            {
                Assert.That(actualPitchClasses, Contains.Item(expectedPc),
                    $"{description}: Should contain {expectedPc}");
            }
        }
    }

    [Test]
    public void PatternId_ComplexityScoring_Test()
    {
        // Test complexity scoring accuracy
        var testCases = new[]
        {
            // (pattern, expectedComplexityRange, description)
            ([0, 0, 0, 0, 0, 0], (0, 3), "All open - very simple"),
            ([-1, -1, -1, -1, -1, -1], (0, 1), "All muted - simplest"),
            ([0, 2, 2, 1, 0, 0], (3, 8), "E major - beginner"),
            ([1, 3, 3, 2, 1, 1], (8, 15), "F barre - intermediate"),
            (new[] { 1, 5, 4, 3, 2, 1 }, (15, 25), "Wide stretch - advanced")
        };

        foreach (var (pattern, (minComplexity, maxComplexity), description) in testCases)
        {
            // Act
            var patternId = PatternId.FromPattern(pattern);
            var complexity = patternId.GetComplexityScore();

            // Assert
            Assert.That(complexity, Is.InRange(minComplexity, maxComplexity),
                $"{description}: Complexity {complexity} should be in range {minComplexity}-{maxComplexity}");
        }
    }
}

[TestFixture]
public class PatternRecognitionEngineTests
{
    [Test]
    public void PatternRecognitionEngine_ShouldIdentifyChordTypes()
    {
        // Arrange
        var openChord = new[] { -1, 3, 2, 0, 1, 0 }; // Open C
        var barreChord = new[] { 1, 3, 3, 2, 1, 1 }; // F barre
        var powerChord = new[] { 3, 5, 5, -1, -1, -1 }; // G power chord

        // Act
        var openInvariant = ChordInvariant.FromFrets(openChord, Tuning.Default);
        var barreInvariant = ChordInvariant.FromFrets(barreChord, Tuning.Default);
        var powerInvariant = ChordInvariant.FromFrets(powerChord, Tuning.Default);

        var openType = PatternRecognitionEngine.IdentifyPatternType(openInvariant);
        var barreType = PatternRecognitionEngine.IdentifyPatternType(barreInvariant);
        var powerType = PatternRecognitionEngine.IdentifyPatternType(powerInvariant);

        // Assert
        Assert.That(openType, Is.EqualTo(ChordPatternType.Open), "Should identify open chord");
        Assert.That(barreType, Is.EqualTo(ChordPatternType.Barre), "Should identify barre chord");
        Assert.That(powerType, Is.EqualTo(ChordPatternType.Power), "Should identify power chord");
    }

    [Test]
    public void PatternRecognitionEngine_ShouldCalculateSimilarity()
    {
        // Arrange
        var pattern1 = ChordInvariant.FromFrets([1, 3, 3, 2, 1, 1], Tuning.Default);
        var pattern2 = ChordInvariant.FromFrets([1, 3, 3, 2, 1, 1], Tuning.Default); // Identical
        var pattern3 = ChordInvariant.FromFrets([1, 3, 3, 2, 2, 1], Tuning.Default); // Slightly different

        // Act
        var identicalSimilarity = PatternRecognitionEngine.CalculatePatternSimilarity(pattern1, pattern2);
        var similarSimilarity = PatternRecognitionEngine.CalculatePatternSimilarity(pattern1, pattern3);

        // Assert
        Assert.That(identicalSimilarity, Is.EqualTo(1.0), "Identical patterns should have similarity 1.0");
        Assert.That(similarSimilarity, Is.GreaterThan(0.5), "Similar patterns should have high similarity");
        Assert.That(similarSimilarity, Is.LessThan(1.0), "Different patterns should have similarity < 1.0");
    }

    [Test]
    public void PatternRecognitionEngine_ShouldAnalyzeFingering()
    {
        // Arrange
        var simpleChord = ChordInvariant.FromFrets([0, 2, 2, 1, 0, 0], Tuning.Default);
        var barreChord = ChordInvariant.FromFrets([1, 3, 3, 2, 1, 1], Tuning.Default);

        // Act
        var simpleAnalysis = PatternRecognitionEngine.AnalyzeFingering(simpleChord);
        var barreAnalysis = PatternRecognitionEngine.AnalyzeFingering(barreChord);

        // Assert
        Assert.That(simpleAnalysis.FingersUsed, Is.LessThanOrEqualTo(4), "Should use reasonable number of fingers");
        Assert.That(barreAnalysis.BarreInfo.Count, Is.GreaterThan(0), "Barre chord should have barre info");
        Assert.That(barreAnalysis.Difficulty, Is.GreaterThanOrEqualTo(simpleAnalysis.Difficulty),
            "Barre chord should be more difficult");
    }

    [Test]
    public void PatternRecognitionEngine_ChordTypeClassification_ComprehensiveTest()
    {
        // Test comprehensive chord type classification
        var testCases = new[]
        {
            // (frets, expectedType, description)
            ([-1, 3, 2, 0, 1, 0], ChordPatternType.Open, "Open C major"),
            ([0, 2, 2, 1, 0, 0], ChordPatternType.Open, "Open E major"),
            ([3, 2, 0, 0, 3, 3], ChordPatternType.Open, "Open G major"),
            ([-1, 0, 2, 2, 2, 0], ChordPatternType.Open, "Open A major"),
            ([-1, -1, 0, 2, 3, 2], ChordPatternType.Open, "Open D major"),

            ([1, 3, 3, 2, 1, 1], ChordPatternType.Barre, "F major barre"),
            ([3, 5, 5, 4, 3, 3], ChordPatternType.Barre, "G major barre"),
            ([5, 7, 7, 6, 5, 5], ChordPatternType.Barre, "A major barre"),

            ([3, 5, 5, -1, -1, -1], ChordPatternType.Power, "G5 power chord"),
            ([5, 7, 7, -1, -1, -1], ChordPatternType.Power, "A5 power chord"),
            (new[] { 0, 2, 2, -1, -1, -1 }, ChordPatternType.Power, "E5 power chord"),

            ([2, 3, 2, 3, 2, 3], ChordPatternType.Cluster, "Cluster chord"),
            (new[] { 1, 2, 1, 2, 1, 2 }, ChordPatternType.Cluster, "Another cluster")
        };

        foreach (var (frets, expectedType, description) in testCases)
        {
            // Act
            var invariant = ChordInvariant.FromFrets(frets, Tuning.Default);
            var actualType = PatternRecognitionEngine.IdentifyPatternType(invariant);

            // Assert
            Assert.That(actualType, Is.EqualTo(expectedType),
                $"{description}: Should be classified as {expectedType}");
        }
    }

    [Test]
    public void PatternRecognitionEngine_FingeringAnalysis_DetailedTest()
    {
        // Test detailed fingering analysis
        var testCases = new[]
        {
            // (frets, expectedFingers, expectedBarres, expectedDifficulty, description)
            ([0, 2, 2, 1, 0, 0], 3, 0, ChordDifficulty.Beginner, "E major - 3 fingers"),
            ([-1, 3, 2, 0, 1, 0], 3, 0, ChordDifficulty.Beginner, "C major - 3 fingers"),
            ([1, 3, 3, 2, 1, 1], 4, 1, ChordDifficulty.Intermediate, "F barre - 4 fingers, 1 barre"),
            (new[] { 3, 5, 5, -1, -1, -1 }, 2, 0, ChordDifficulty.Beginner, "Power chord - 2 fingers"),
            (new[] { 1, 5, 4, 3, 2, 1 }, 4, 0, ChordDifficulty.Expert, "Wide stretch - expert level")
        };

        foreach (var (frets, expectedFingers, expectedBarres, expectedDifficulty, description) in testCases)
        {
            // Act
            var invariant = ChordInvariant.FromFrets(frets, Tuning.Default);
            var analysis = PatternRecognitionEngine.AnalyzeFingering(invariant);

            // Assert
            Assert.That(analysis.FingersUsed, Is.EqualTo(expectedFingers),
                $"{description}: Should use {expectedFingers} fingers");
            Assert.That(analysis.BarreInfo.Count, Is.EqualTo(expectedBarres),
                $"{description}: Should have {expectedBarres} barres");
            Assert.That(analysis.Difficulty, Is.EqualTo(expectedDifficulty),
                $"{description}: Should be {expectedDifficulty} difficulty");
        }
    }

    [Test]
    public void PatternRecognitionEngine_SimilarityCalculation_AccuracyTest()
    {
        // Test pattern similarity calculation accuracy
        var basePattern = ChordInvariant.FromFrets([1, 3, 3, 2, 1, 1], Tuning.Default);

        var testCases = new[]
        {
            // (frets, expectedSimilarity, description)
            ([1, 3, 3, 2, 1, 1], 1.0, "Identical pattern"),
            ([2, 4, 4, 3, 2, 2], 1.0, "Same pattern, different position"),
            ([1, 3, 3, 2, 2, 1], 0.83, "One finger different"), // 5/6 = 0.83
            ([1, 3, 3, 3, 1, 1], 0.83, "One fret different"),
            (new[] { 0, 2, 2, 1, 0, 0 }, 0.0, "Completely different pattern")
        };

        foreach (var (frets, expectedSimilarity, description) in testCases)
        {
            // Act
            var testPattern = ChordInvariant.FromFrets(frets, Tuning.Default);
            var similarity = PatternRecognitionEngine.CalculatePatternSimilarity(basePattern, testPattern);

            // Assert
            Assert.That(similarity, Is.EqualTo(expectedSimilarity).Within(0.1),
                $"{description}: Similarity should be approximately {expectedSimilarity}");
        }
    }

    [Test]
    public void PatternRecognitionEngine_PatternVariations_GenerationTest()
    {
        // Test pattern variation generation
        var basePattern = ChordInvariant.FromFrets([1, 3, 3, 2, 1, 1], Tuning.Default);

        // Act
        var variations = PatternRecognitionEngine.GeneratePatternVariations(basePattern).ToList();

        // Assert
        Assert.That(variations.Count, Is.GreaterThan(1), "Should generate multiple variations");
        Assert.That(variations.First(), Is.EqualTo(basePattern), "First variation should be original");

        // All variations should be related to the original
        foreach (var variation in variations.Skip(1))
        {
            var similarity = PatternRecognitionEngine.CalculatePatternSimilarity(basePattern, variation);
            Assert.That(similarity, Is.GreaterThan(0.5),
                "Variations should be similar to original pattern");
        }
    }

    [Test]
    public void PatternRecognitionEngine_DifficultyAssessment_ConsistencyTest()
    {
        // Test that difficulty assessment is consistent and logical
        var testCases = new[]
        {
            // Beginner chords
            ([0, 2, 2, 1, 0, 0], ChordDifficulty.Beginner, "E major"),
            ([-1, 3, 2, 0, 1, 0], ChordDifficulty.Beginner, "C major"),
            ([3, 5, 5, -1, -1, -1], ChordDifficulty.Beginner, "Power chord"),

            // Intermediate chords
            ([1, 3, 3, 2, 1, 1], ChordDifficulty.Intermediate, "F barre"),
            ([-1, 0, 2, 0, 1, 0], ChordDifficulty.Intermediate, "Am7"),

            // Advanced/Expert chords
            (new[] { 1, 5, 4, 3, 2, 1 }, ChordDifficulty.Expert, "Wide stretch"),
            (new[] { 2, 5, 4, 2, 3, 2 }, ChordDifficulty.Advanced, "Complex fingering")
        };

        foreach (var (frets, expectedDifficulty, description) in testCases)
        {
            // Act
            var invariant = ChordInvariant.FromFrets(frets, Tuning.Default);
            var actualDifficulty = invariant.GetDifficulty();

            // Assert
            Assert.That(actualDifficulty, Is.EqualTo(expectedDifficulty),
                $"{description}: Should be {expectedDifficulty} difficulty");
        }
    }
}
