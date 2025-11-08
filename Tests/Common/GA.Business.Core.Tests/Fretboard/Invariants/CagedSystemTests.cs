namespace GA.Business.Core.Tests.Fretboard.Invariants;

[TestFixture]
public class CagedSystemTests
{
    [Test]
    public void CagedSystemIntegration_ShouldIdentifyShapes()
    {
        // Arrange - Open chord shapes
        var cShape = new[] { -1, 3, 2, 0, 1, 0 }; // Open C
        var aShape = new[] { -1, 0, 2, 2, 2, 0 }; // Open A
        var gShape = new[] { 3, 2, 0, 0, 3, 3 }; // Open G

        // Act
        var cShapeId = CagedSystemIntegration.IdentifyCagedShape(cShape);
        var aShapeId = CagedSystemIntegration.IdentifyCagedShape(aShape);
        var gShapeId = CagedSystemIntegration.IdentifyCagedShape(gShape);

        // Assert
        Assert.That(cShapeId, Is.EqualTo(CagedSystemIntegration.CagedShape.C), "Should identify C shape");
        Assert.That(aShapeId, Is.EqualTo(CagedSystemIntegration.CagedShape.A), "Should identify A shape");
        Assert.That(gShapeId, Is.EqualTo(CagedSystemIntegration.CagedShape.G), "Should identify G shape");
    }

    [Test]
    public void CagedSystemIntegration_ShouldGenerateTranspositions()
    {
        // Arrange
        var cShape = CagedSystemIntegration.CagedShape.C;

        // Act
        var transpositions = CagedSystemIntegration.GetCagedTranspositions(cShape, 5).Take(3).ToList();

        // Assert
        Assert.That(transpositions.Count, Is.EqualTo(3), "Should generate transpositions");
        Assert.That(transpositions.All(t => t.chordName != null), Is.True, "All should have chord names");
        Assert.That(transpositions.All(t => t.frets.Length == 6), Is.True, "All should have 6 fret positions");
    }

    [Test]
    public void CagedSystemIntegration_ShouldAnalyzeCompatibility()
    {
        // Arrange
        var cShapeChord = ChordInvariant.FromFrets([-1, 3, 2, 0, 1, 0], Tuning.Default);
        var randomChord = ChordInvariant.FromFrets([1, 2, 3, 4, 5, 1], Tuning.Default);

        // Act
        var cShapeAnalysis = CagedSystemIntegration.AnalyzeCagedCompatibility(cShapeChord);
        var randomAnalysis = CagedSystemIntegration.AnalyzeCagedCompatibility(randomChord);

        // Assert
        Assert.That(cShapeAnalysis.IsStandardCaged, Is.True, "C shape should be standard CAGED");
        Assert.That(cShapeAnalysis.Similarity, Is.EqualTo(1.0), "Perfect match should have similarity 1.0");
        Assert.That(randomAnalysis.IsStandardCaged, Is.False, "Random chord should not be standard CAGED");
        Assert.That(randomAnalysis.Similarity, Is.LessThan(1.0), "Non-CAGED should have similarity < 1.0");
    }

    [Test]
    public void CagedSystemIntegration_AllShapes_RecognitionAccuracyTest()
    {
        // Test recognition accuracy for all CAGED shapes
        var cagedTestCases = new[]
        {
            // (frets, expectedShape, description)
            ([-1, 3, 2, 0, 1, 0], CagedSystemIntegration.CagedShape.C, "Open C major"),
            ([-1, 0, 2, 2, 2, 0], CagedSystemIntegration.CagedShape.A, "Open A major"),
            ([3, 2, 0, 0, 3, 3], CagedSystemIntegration.CagedShape.G, "Open G major"),
            (new[] { 0, 2, 2, 1, 0, 0 }, CagedSystemIntegration.CagedShape.E, "Open E major"),
            (new[] { -1, -1, 0, 2, 3, 2 }, CagedSystemIntegration.CagedShape.D, "Open D major")
        };

        foreach (var (frets, expectedShape, description) in cagedTestCases)
        {
            // Act
            var identifiedShape = CagedSystemIntegration.IdentifyCagedShape(frets);
            var invariant = ChordInvariant.FromFrets(frets, Tuning.Default);
            var analysis = CagedSystemIntegration.AnalyzeCagedCompatibility(invariant);

            // Assert
            Assert.That(identifiedShape, Is.EqualTo(expectedShape),
                $"{description}: Should identify as {expectedShape} shape");
            Assert.That(analysis.IsStandardCaged, Is.True,
                $"{description}: Should be recognized as standard CAGED");
            Assert.That(analysis.ClosestShape, Is.EqualTo(expectedShape),
                $"{description}: Closest shape should be {expectedShape}");
            Assert.That(analysis.Similarity, Is.EqualTo(1.0),
                $"{description}: Should have perfect similarity");
        }
    }

    [Test]
    public void CagedSystemIntegration_TranspositionGeneration_AccuracyTest()
    {
        // Test transposition generation for each CAGED shape
        foreach (var shape in Enum.GetValues<CagedSystemIntegration.CagedShape>())
        {
            // Act
            var transpositions = CagedSystemIntegration.GetCagedTranspositions(shape, 12).ToList();

            // Assert
            Assert.That(transpositions.Count, Is.GreaterThan(0),
                $"{shape} shape should generate transpositions");
            Assert.That(transpositions.Count, Is.LessThanOrEqualTo(13),
                $"{shape} shape should not exceed 13 transpositions (0-12 frets)");

            // Verify each transposition
            foreach (var (baseFret, frets, chordName) in transpositions)
            {
                Assert.That(baseFret, Is.InRange(0, 12),
                    $"{shape} transposition should have valid base fret");
                Assert.That(frets.Length, Is.EqualTo(6),
                    $"{shape} transposition should have 6 fret positions");
                Assert.That(chordName, Is.Not.Null.And.Not.Empty,
                    $"{shape} transposition should have chord name");

                // Verify the transposed pattern matches the original shape
                var transposedInvariant = ChordInvariant.FromFrets(frets, Tuning.Default);
                var identifiedShape = CagedSystemIntegration.IdentifyCagedShape(transposedInvariant);
                Assert.That(identifiedShape, Is.EqualTo(shape),
                    $"Transposed {shape} should still be identified as {shape}");
            }
        }
    }

    [Test]
    public void CagedSystemIntegration_BestVoicingSelection_Test()
    {
        // Test best voicing selection for different scenarios
        var testCases = new[]
        {
            // (chordName, preferredFret, description)
            ("C", 0, "C major at open position"),
            ("F", 1, "F major near 1st fret"),
            ("G", 3, "G major near 3rd fret"),
            ("A", 5, "A major near 5th fret"),
            ("D", 10, "D major near 10th fret")
        };

        foreach (var (chordName, preferredFret, description) in testCases)
        {
            // Act
            var bestVoicing = CagedSystemIntegration.FindBestCagedVoicing(chordName, preferredFret);

            // Assert
            Assert.That(bestVoicing, Is.Not.Null, $"{description}: Should find a voicing");

            if (bestVoicing.HasValue)
            {
                var (shape, baseFret, frets) = bestVoicing.Value;

                Assert.That(Enum.IsDefined(typeof(CagedSystemIntegration.CagedShape), shape),
                    Is.True, $"{description}: Should return valid CAGED shape");
                Assert.That(baseFret, Is.InRange(0, 24),
                    $"{description}: Should have reasonable base fret");
                Assert.That(frets.Length, Is.EqualTo(6),
                    $"{description}: Should have 6 fret positions");

                // Verify the distance from preferred fret is reasonable
                var distance = Math.Abs(baseFret - preferredFret);
                Assert.That(distance, Is.LessThanOrEqualTo(12),
                    $"{description}: Should be within reasonable distance of preferred fret");
            }
        }
    }

    [Test]
    public void CagedSystemIntegration_ChordProgression_GenerationTest()
    {
        // Test CAGED-based chord progression generation
        var progression = new[] { "C", "Am", "F", "G" }; // Common I-vi-IV-V progression

        // Act
        var cagedProgression = CagedSystemIntegration.GetCagedProgression(progression, 0).ToList();

        // Assert
        Assert.That(cagedProgression.Count, Is.EqualTo(4), "Should generate 4 chord voicings");

        for (var i = 0; i < cagedProgression.Count; i++)
        {
            var voicing = cagedProgression[i];

            Assert.That(voicing.ChordName, Is.EqualTo(progression[i]),
                $"Chord {i} should have correct name");
            Assert.That(Enum.IsDefined(typeof(CagedSystemIntegration.CagedShape), voicing.Shape),
                Is.True, $"Chord {i} should have valid CAGED shape");
            Assert.That(voicing.Frets.Length, Is.EqualTo(6),
                $"Chord {i} should have 6 fret positions");
            Assert.That(voicing.BaseFret, Is.InRange(0, 24),
                $"Chord {i} should have reasonable base fret");
        }

        // Verify progression maintains reasonable fret positions (voice leading)
        for (var i = 1; i < cagedProgression.Count; i++)
        {
            var previousFret = cagedProgression[i - 1].BaseFret;
            var currentFret = cagedProgression[i].BaseFret;
            var jump = Math.Abs(currentFret - previousFret);

            Assert.That(jump, Is.LessThanOrEqualTo(12),
                $"Fret jump between chord {i - 1} and {i} should be reasonable");
        }
    }

    [Test]
    public void CagedSystemIntegration_CompatibilityAnalysis_EdgeCasesTest()
    {
        // Test compatibility analysis with edge cases
        var edgeCases = new[]
        {
            // (frets, description, expectedStandardCaged)
            ([-1, -1, -1, -1, -1, -1], "All muted", false),
            ([0, 0, 0, 0, 0, 0], "All open", false),
            ([12, 14, 14, 13, 12, 12], "High fret barre", false), // Same pattern as F, but high
            ([1, 3, 3, 2, 1, 1], "F major barre", false), // Barre version, not open
            (new[] { 2, 4, 4, 3, 2, 2 }, "G major barre (E-shape)", false) // E-shape at 2nd fret
        };

        foreach (var (frets, description, expectedStandardCaged) in edgeCases)
        {
            // Act
            var invariant = ChordInvariant.FromFrets(frets, Tuning.Default);
            var analysis = CagedSystemIntegration.AnalyzeCagedCompatibility(invariant);

            // Assert
            Assert.That(analysis.IsStandardCaged, Is.EqualTo(expectedStandardCaged),
                $"{description}: Standard CAGED recognition should be {expectedStandardCaged}");
            Assert.That(analysis.ClosestShape, Is.Not.Null,
                $"{description}: Should identify closest shape");
            Assert.That(analysis.Similarity, Is.InRange(0.0, 1.0),
                $"{description}: Similarity should be between 0 and 1");
            Assert.That(analysis.RelatedShapes, Is.Not.Null,
                $"{description}: Should have related shapes");
        }
    }

    [Test]
    public void CagedSystemIntegration_ShapeDefinitions_ConsistencyTest()
    {
        // Test that all CAGED shape definitions are consistent
        foreach (var (shape, shapeInfo) in CagedSystemIntegration.Shapes)
        {
            // Act & Assert
            Assert.That(shapeInfo.Shape, Is.EqualTo(shape),
                $"{shape}: Shape property should match key");
            Assert.That(shapeInfo.Name, Is.Not.Null.And.Not.Empty,
                $"{shape}: Should have name");
            Assert.That(shapeInfo.Description, Is.Not.Null.And.Not.Empty,
                $"{shape}: Should have description");
            Assert.That(shapeInfo.OpenVoicing.Length, Is.EqualTo(6),
                $"{shape}: Open voicing should have 6 positions");
            Assert.That(shapeInfo.Invariant, Is.Not.Null,
                $"{shape}: Should have invariant");
            Assert.That(shapeInfo.Fingering, Is.Not.Null,
                $"{shape}: Should have fingering analysis");

            // Verify the open voicing creates the expected invariant
            var voicingInvariant = ChordInvariant.FromFrets(shapeInfo.OpenVoicing, Tuning.Default);
            Assert.That(voicingInvariant.PatternId, Is.EqualTo(shapeInfo.Invariant.PatternId),
                $"{shape}: Open voicing should match invariant pattern");
        }
    }

    [Test]
    public void CagedSystemIntegration_PatternToShapeMapping_BijectivityTest()
    {
        // Test that pattern to shape mapping is bijective (one-to-one)
        var patternToShape = CagedSystemIntegration.PatternToShape;
        var shapes = CagedSystemIntegration.Shapes;

        // Each shape should have a unique pattern
        var shapePatterns = shapes.Values.Select(s => s.PatternId).ToList();
        var uniquePatterns = shapePatterns.Distinct().ToList();

        Assert.That(uniquePatterns.Count, Is.EqualTo(shapePatterns.Count),
            "Each CAGED shape should have a unique pattern");

        // Each pattern should map back to the correct shape
        foreach (var (shape, shapeInfo) in shapes)
        {
            Assert.That(patternToShape.ContainsKey(shapeInfo.PatternId), Is.True,
                $"{shape}: Pattern should be in mapping");
            Assert.That(patternToShape[shapeInfo.PatternId], Is.EqualTo(shape),
                $"{shape}: Pattern should map back to correct shape");
        }
    }
}
