namespace GaApi.Tests.GraphQL.Types;

using GA.Business.Core.Fretboard.Analysis;
using GA.Business.Core.Fretboard.Primitives;
using GaApi.GraphQL.Types;

[TestFixture]
public class FingerPositionTypeTests
{
    [Test]
    public void FromFingerPosition_MapsAllFieldsCorrectly()
    {
        // Arrange
        var fingerPosition = new PhysicalFretboardCalculator.FingerPosition(
            new Str(5),
            new Fret(3),
            3,
            "normal"
        );

        // Act
        var result = FingerPositionType.FromFingerPosition(fingerPosition);

        // Assert
        Assert.That(result.String, Is.EqualTo(5));
        Assert.That(result.Fret, Is.EqualTo(3));
        Assert.That(result.FingerNumber, Is.EqualTo(3));
        Assert.That(result.Technique, Is.EqualTo("normal"));
    }

    [Test]
    public void FromFingerPosition_HandlesNormalTechnique()
    {
        // Arrange
        var fingerPosition = new PhysicalFretboardCalculator.FingerPosition(
            new Str(4),
            new Fret(2),
            2,
            "normal"
        );

        // Act
        var result = FingerPositionType.FromFingerPosition(fingerPosition);

        // Assert
        Assert.That(result.Technique, Is.EqualTo("normal"));
        Assert.That(result.FingerNumber, Is.EqualTo(2));
    }

    [Test]
    public void FromFingerPosition_HandlesBarreTechnique()
    {
        // Arrange
        var fingerPosition = new PhysicalFretboardCalculator.FingerPosition(
            new Str(6),
            new Fret(1),
            -1, // Barre uses -1
            "barre"
        );

        // Act
        var result = FingerPositionType.FromFingerPosition(fingerPosition);

        // Assert
        Assert.That(result.Technique, Is.EqualTo("barre"));
        Assert.That(result.FingerNumber, Is.EqualTo(-1));
        Assert.That(result.Fret, Is.EqualTo(1));
    }

    [Test]
    public void FromFingerPosition_HandlesStretchTechnique()
    {
        // Arrange
        var fingerPosition = new PhysicalFretboardCalculator.FingerPosition(
            new Str(3),
            new Fret(7),
            4,
            "stretch"
        );

        // Act
        var result = FingerPositionType.FromFingerPosition(fingerPosition);

        // Assert
        Assert.That(result.Technique, Is.EqualTo("stretch"));
        Assert.That(result.FingerNumber, Is.EqualTo(4));
    }

    [Test]
    public void FromFingerPosition_HandlesThumbTechnique()
    {
        // Arrange
        var fingerPosition = new PhysicalFretboardCalculator.FingerPosition(
            new Str(6),
            new Fret(3),
            0, // Thumb uses 0
            "thumb"
        );

        // Act
        var result = FingerPositionType.FromFingerPosition(fingerPosition);

        // Assert
        Assert.That(result.Technique, Is.EqualTo("thumb"));
        Assert.That(result.FingerNumber, Is.EqualTo(0));
    }

    [Test]
    public void FromFingerPosition_HandlesAllStrings()
    {
        // Test all 6 strings
        for (var stringNum = 1; stringNum <= 6; stringNum++)
        {
            // Arrange
            var fingerPosition = new PhysicalFretboardCalculator.FingerPosition(
                new Str(stringNum),
                new Fret(1),
                1,
                "normal"
            );

            // Act
            var result = FingerPositionType.FromFingerPosition(fingerPosition);

            // Assert
            Assert.That(result.String, Is.EqualTo(stringNum),
                $"String {stringNum} should map correctly");
        }
    }

    [Test]
    public void FromFingerPosition_HandlesAllFrets()
    {
        // Test frets 0-24 (common range)
        for (var fretNum = 0; fretNum <= 24; fretNum++)
        {
            // Arrange
            var fingerPosition = new PhysicalFretboardCalculator.FingerPosition(
                new Str(3),
                new Fret(fretNum),
                1,
                "normal"
            );

            // Act
            var result = FingerPositionType.FromFingerPosition(fingerPosition);

            // Assert
            Assert.That(result.Fret, Is.EqualTo(fretNum),
                $"Fret {fretNum} should map correctly");
        }
    }

    [Test]
    public void FromFingerPosition_HandlesAllFingerNumbers()
    {
        // Test finger numbers: 0 (thumb), 1-4 (fingers), -1 (barre)
        var fingerNumbers = new[] { -1, 0, 1, 2, 3, 4 };

        foreach (var fingerNum in fingerNumbers)
        {
            // Arrange
            var technique = fingerNum == -1 ? "barre" :
                fingerNum == 0 ? "thumb" : "normal";

            var fingerPosition = new PhysicalFretboardCalculator.FingerPosition(
                new Str(3),
                new Fret(1),
                fingerNum,
                technique
            );

            // Act
            var result = FingerPositionType.FromFingerPosition(fingerPosition);

            // Assert
            Assert.That(result.FingerNumber, Is.EqualTo(fingerNum),
                $"Finger number {fingerNum} should map correctly");
        }
    }

    [Test]
    public void FromFingerPosition_HandlesOpenString()
    {
        // Arrange - Open string (fret 0)
        var fingerPosition = new PhysicalFretboardCalculator.FingerPosition(
            new Str(1),
            new Fret(0),
            0, // No finger needed for open string
            "normal"
        );

        // Act
        var result = FingerPositionType.FromFingerPosition(fingerPosition);

        // Assert
        Assert.That(result.Fret, Is.EqualTo(0));
        Assert.That(result.FingerNumber, Is.EqualTo(0));
    }

    [Test]
    public void FromFingerPosition_HandlesHighFrets()
    {
        // Arrange - High fret position (fret 22)
        var fingerPosition = new PhysicalFretboardCalculator.FingerPosition(
            new Str(1),
            new Fret(22),
            1,
            "normal"
        );

        // Act
        var result = FingerPositionType.FromFingerPosition(fingerPosition);

        // Assert
        Assert.That(result.Fret, Is.EqualTo(22));
        Assert.That(result.String, Is.EqualTo(1));
    }

    [Test]
    public void FromFingerPosition_PreservesExactValues()
    {
        // Arrange - Specific combination
        var fingerPosition = new PhysicalFretboardCalculator.FingerPosition(
            new Str(2),
            new Fret(5),
            3,
            "stretch"
        );

        // Act
        var result = FingerPositionType.FromFingerPosition(fingerPosition);

        // Assert - Verify exact values are preserved
        Assert.That(result.String, Is.EqualTo(2));
        Assert.That(result.Fret, Is.EqualTo(5));
        Assert.That(result.FingerNumber, Is.EqualTo(3));
        Assert.That(result.Technique, Is.EqualTo("stretch"));
    }

    [Test]
    public void FromFingerPosition_MultipleConversions_ProduceSameResults()
    {
        // Arrange
        var fingerPosition = new PhysicalFretboardCalculator.FingerPosition(
            new Str(4),
            new Fret(7),
            2,
            "normal"
        );

        // Act - Convert multiple times
        var result1 = FingerPositionType.FromFingerPosition(fingerPosition);
        var result2 = FingerPositionType.FromFingerPosition(fingerPosition);

        // Assert - Results should be identical
        Assert.That(result1.String, Is.EqualTo(result2.String));
        Assert.That(result1.Fret, Is.EqualTo(result2.Fret));
        Assert.That(result1.FingerNumber, Is.EqualTo(result2.FingerNumber));
        Assert.That(result1.Technique, Is.EqualTo(result2.Technique));
    }
}
