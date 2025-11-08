namespace GaApi.Tests.GraphQL.Types;

using GA.Business.Core.Fretboard.Analysis;
using GA.Business.Core.Fretboard.Primitives;
using GaApi.GraphQL.Types;

[TestFixture]
public class PhysicalPlayabilityDataTypeTests
{
    [Test]
    public void FromAnalysis_MapsAllFieldsCorrectly()
    {
        // Arrange
        var fingerPositions = new List<PhysicalFretboardCalculator.FingerPosition>
        {
            new(new Str(5), new Fret(3), 3, "normal"),
            new(new Str(4), new Fret(2), 2, "normal"),
            new(new Str(2), new Fret(1), 1, "normal")
        }.AsReadOnly();

        var analysis = new PhysicalFretboardCalculator.PhysicalPlayabilityAnalysis(
            52.3,
            48.7,
            35.2,
            25.5,
            58.1,
            PhysicalFretboardCalculator.PlayabilityDifficulty.Easy,
            true,
            "Suitable for beginners",
            fingerPositions
        );

        // Act
        var result = PhysicalPlayabilityDataType.FromAnalysis(analysis);

        // Assert
        Assert.That(result.FretSpanMm, Is.EqualTo(52.3));
        Assert.That(result.MaxFingerStretchMm, Is.EqualTo(48.7));
        Assert.That(result.AverageFingerStretchMm, Is.EqualTo(35.2));
        Assert.That(result.VerticalSpanMm, Is.EqualTo(25.5));
        Assert.That(result.DiagonalStretchMm, Is.EqualTo(58.1));
        Assert.That(result.Difficulty, Is.EqualTo("Easy"));
        Assert.That(result.IsPlayable, Is.True);
        Assert.That(result.DifficultyReason, Is.EqualTo("Suitable for beginners"));
        Assert.That(result.SuggestedFingering, Has.Count.EqualTo(3));
    }

    [Test]
    public void FromAnalysis_HandlesEmptyFingering()
    {
        // Arrange
        var analysis = new PhysicalFretboardCalculator.PhysicalPlayabilityAnalysis(
            0,
            0,
            0,
            0,
            0,
            PhysicalFretboardCalculator.PlayabilityDifficulty.VeryEasy,
            true,
            "Open chord - no finger stretch required",
            new List<PhysicalFretboardCalculator.FingerPosition>().AsReadOnly()
        );

        // Act
        var result = PhysicalPlayabilityDataType.FromAnalysis(analysis);

        // Assert
        Assert.That(result.SuggestedFingering, Is.Empty);
        Assert.That(result.Difficulty, Is.EqualTo("VeryEasy"));
        Assert.That(result.IsPlayable, Is.True);
    }

    [Test]
    public void FromAnalysis_HandlesExtremeMeasurements()
    {
        // Arrange - Impossible chord
        var analysis = new PhysicalFretboardCalculator.PhysicalPlayabilityAnalysis(
            250.0,
            180.0,
            150.0,
            50.0,
            255.0,
            PhysicalFretboardCalculator.PlayabilityDifficulty.Impossible,
            false,
            "Finger stretch of 180.0mm exceeds human capability",
            new List<PhysicalFretboardCalculator.FingerPosition>().AsReadOnly()
        );

        // Act
        var result = PhysicalPlayabilityDataType.FromAnalysis(analysis);

        // Assert
        Assert.That(result.FretSpanMm, Is.EqualTo(250.0));
        Assert.That(result.MaxFingerStretchMm, Is.EqualTo(180.0));
        Assert.That(result.Difficulty, Is.EqualTo("Impossible"));
        Assert.That(result.IsPlayable, Is.False);
        Assert.That(result.DifficultyReason, Contains.Substring("exceeds human capability"));
    }

    [Test]
    public void FromAnalysis_MapsAllDifficultyLevels()
    {
        // Test each difficulty level
        var difficulties = new[]
        {
            PhysicalFretboardCalculator.PlayabilityDifficulty.VeryEasy,
            PhysicalFretboardCalculator.PlayabilityDifficulty.Easy,
            PhysicalFretboardCalculator.PlayabilityDifficulty.Moderate,
            PhysicalFretboardCalculator.PlayabilityDifficulty.Challenging,
            PhysicalFretboardCalculator.PlayabilityDifficulty.Difficult,
            PhysicalFretboardCalculator.PlayabilityDifficulty.VeryDifficult,
            PhysicalFretboardCalculator.PlayabilityDifficulty.Extreme,
            PhysicalFretboardCalculator.PlayabilityDifficulty.Impossible
        };

        foreach (var difficulty in difficulties)
        {
            // Arrange
            var analysis = new PhysicalFretboardCalculator.PhysicalPlayabilityAnalysis(
                50.0,
                50.0,
                40.0,
                25.0,
                55.0,
                difficulty,
                difficulty != PhysicalFretboardCalculator.PlayabilityDifficulty.Impossible,
                $"Test {difficulty}",
                new List<PhysicalFretboardCalculator.FingerPosition>().AsReadOnly()
            );

            // Act
            var result = PhysicalPlayabilityDataType.FromAnalysis(analysis);

            // Assert
            Assert.That(result.Difficulty, Is.EqualTo(difficulty.ToString()),
                $"Difficulty {difficulty} should map to string '{difficulty}'");
        }
    }

    [Test]
    public void FromAnalysis_MapsSuggestedFingeringCorrectly()
    {
        // Arrange - C major chord fingering
        var fingerPositions = new List<PhysicalFretboardCalculator.FingerPosition>
        {
            new(new Str(5), new Fret(3), 3, "normal"), // C on A string
            new(new Str(4), new Fret(2), 2, "normal"), // E on D string
            new(new Str(2), new Fret(1), 1, "normal") // C on B string
        }.AsReadOnly();

        var analysis = new PhysicalFretboardCalculator.PhysicalPlayabilityAnalysis(
            52.3,
            48.7,
            35.2,
            25.5,
            58.1,
            PhysicalFretboardCalculator.PlayabilityDifficulty.Easy,
            true,
            "Suitable for beginners",
            fingerPositions
        );

        // Act
        var result = PhysicalPlayabilityDataType.FromAnalysis(analysis);

        // Assert
        Assert.That(result.SuggestedFingering, Has.Count.EqualTo(3));

        // Check first finger position
        Assert.That(result.SuggestedFingering[0].String, Is.EqualTo(5));
        Assert.That(result.SuggestedFingering[0].Fret, Is.EqualTo(3));
        Assert.That(result.SuggestedFingering[0].FingerNumber, Is.EqualTo(3));
        Assert.That(result.SuggestedFingering[0].Technique, Is.EqualTo("normal"));

        // Check second finger position
        Assert.That(result.SuggestedFingering[1].String, Is.EqualTo(4));
        Assert.That(result.SuggestedFingering[1].Fret, Is.EqualTo(2));
        Assert.That(result.SuggestedFingering[1].FingerNumber, Is.EqualTo(2));

        // Check third finger position
        Assert.That(result.SuggestedFingering[2].String, Is.EqualTo(2));
        Assert.That(result.SuggestedFingering[2].Fret, Is.EqualTo(1));
        Assert.That(result.SuggestedFingering[2].FingerNumber, Is.EqualTo(1));
    }

    [Test]
    public void FromAnalysis_HandlesBarreChordFingering()
    {
        // Arrange - Barre chord with barre technique
        var fingerPositions = new List<PhysicalFretboardCalculator.FingerPosition>
        {
            new(new Str(6), new Fret(1), -1, "barre"), // Barre on fret 1
            new(new Str(5), new Fret(1), -1, "barre"),
            new(new Str(4), new Fret(1), -1, "barre"),
            new(new Str(3), new Fret(1), -1, "barre"),
            new(new Str(2), new Fret(1), -1, "barre"),
            new(new Str(1), new Fret(1), -1, "barre"),
            new(new Str(4), new Fret(3), 3, "normal"), // Additional fingers
            new(new Str(3), new Fret(3), 4, "normal"),
            new(new Str(2), new Fret(3), 2, "normal")
        }.AsReadOnly();

        var analysis = new PhysicalFretboardCalculator.PhysicalPlayabilityAnalysis(
            65.0,
            60.0,
            45.0,
            42.0,
            75.0,
            PhysicalFretboardCalculator.PlayabilityDifficulty.Moderate,
            true,
            "Standard chord voicing",
            fingerPositions
        );

        // Act
        var result = PhysicalPlayabilityDataType.FromAnalysis(analysis);

        // Assert
        Assert.That(result.SuggestedFingering, Has.Count.EqualTo(9));

        // Check barre positions
        var barrePositions = result.SuggestedFingering.Where(f => f.Technique == "barre").ToList();
        Assert.That(barrePositions, Has.Count.EqualTo(6));
        Assert.That(barrePositions.All(f => f.FingerNumber == -1), Is.True);
        Assert.That(barrePositions.All(f => f.Fret == 1), Is.True);

        // Check normal positions
        var normalPositions = result.SuggestedFingering.Where(f => f.Technique == "normal").ToList();
        Assert.That(normalPositions, Has.Count.EqualTo(3));
        Assert.That(normalPositions.All(f => f.Fret == 3), Is.True);
    }

    [Test]
    public void FromAnalysis_PreservesZeroValues()
    {
        // Arrange - All open strings
        var analysis = new PhysicalFretboardCalculator.PhysicalPlayabilityAnalysis(
            0,
            0,
            0,
            0,
            0,
            PhysicalFretboardCalculator.PlayabilityDifficulty.VeryEasy,
            true,
            "No notes played",
            new List<PhysicalFretboardCalculator.FingerPosition>().AsReadOnly()
        );

        // Act
        var result = PhysicalPlayabilityDataType.FromAnalysis(analysis);

        // Assert - Verify zero values are preserved, not converted to null or default
        Assert.That(result.FretSpanMm, Is.EqualTo(0));
        Assert.That(result.MaxFingerStretchMm, Is.EqualTo(0));
        Assert.That(result.AverageFingerStretchMm, Is.EqualTo(0));
        Assert.That(result.VerticalSpanMm, Is.EqualTo(0));
        Assert.That(result.DiagonalStretchMm, Is.EqualTo(0));
    }
}
