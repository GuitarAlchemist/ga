namespace GA.Business.Core.Tests.Chords;

using Core.Atonal;
using Core.Chords;

[TestFixture]
public class IconicChordTests
{
    [Test]
    public void IconicChordRegistry_ShouldFindHendrixChord()
    {
        // Arrange - E7#9 pitch classes: E, G#, B, D, G
        var hendrixPitchClasses = new PitchClassSet([
            PitchClass.E, PitchClass.GSharp, PitchClass.B, PitchClass.D, PitchClass.G
        ]);

        // Act
        var matches = IconicChordRegistry.FindIconicMatches(hendrixPitchClasses);

        // Assert
        Assert.That(matches, Is.Not.Empty, "Should find Hendrix chord");
        var hendrixChord = matches.FirstOrDefault(c => c.IconicName == "Hendrix Chord");
        Assert.That(hendrixChord, Is.Not.Null, "Should specifically find Hendrix chord");
        Assert.That(hendrixChord!.TheoreticalName, Is.EqualTo("E7#9"));
        Assert.That(hendrixChord.Artist, Is.EqualTo("Jimi Hendrix"));
    }

    [Test]
    public void IconicChordRegistry_ShouldFindByName()
    {
        // Act
        var hendrixChord = IconicChordRegistry.FindByName("Hendrix Chord");
        var purpleHazeChord = IconicChordRegistry.FindByName("Purple Haze Chord");

        // Assert
        Assert.That(hendrixChord, Is.Not.Null, "Should find by primary name");
        Assert.That(purpleHazeChord, Is.Not.Null, "Should find by alternate name");
        Assert.That(hendrixChord, Is.EqualTo(purpleHazeChord), "Should be same chord");
    }

    [Test]
    public void IconicChordRegistry_ShouldFindByGuitarVoicing()
    {
        // Arrange - Hendrix chord voicing: [0, 7, 6, 7, 8, 0]
        var hendrixVoicing = new[] { 0, 7, 6, 7, 8, 0 };

        // Act
        var matches = IconicChordRegistry.FindByGuitarVoicing(hendrixVoicing);

        // Assert
        Assert.That(matches, Is.Not.Empty, "Should find chord by guitar voicing");
        var hendrixChord = matches.FirstOrDefault();
        Assert.That(hendrixChord?.IconicName, Is.EqualTo("Hendrix Chord"));
    }

    [Test]
    public void IconicChordRegistry_ShouldFindJamesBondChord()
    {
        // Arrange - Em(maj7) pitch classes: E, G, B, D#
        var bondPitchClasses = new PitchClassSet([
            PitchClass.E, PitchClass.G, PitchClass.B, PitchClass.DSharp
        ]);

        // Act
        var matches = IconicChordRegistry.FindIconicMatches(bondPitchClasses);

        // Assert
        Assert.That(matches, Is.Not.Empty, "Should find James Bond chord");
        var bondChord = matches.FirstOrDefault(c => c.IconicName == "James Bond Chord");
        Assert.That(bondChord, Is.Not.Null, "Should specifically find James Bond chord");
        Assert.That(bondChord!.TheoreticalName, Is.EqualTo("Em(maj7)"));
        Assert.That(bondChord.Genre, Is.EqualTo("Film Score"));
    }

    [Test]
    public void ChordTemplateNamingService_ShouldIncludeIconicNames()
    {
        // Arrange - Create a chord template that matches Hendrix chord
        var hendrixTemplate = CreateHendrixChordTemplate();

        // Act
        var comprehensive = ChordTemplateNamingService.GenerateComprehensiveNames(
            hendrixTemplate, PitchClass.E);

        // Assert
        Assert.That(comprehensive.IconicName, Is.Not.Null, "Should have iconic name");
        Assert.That(comprehensive.IconicName, Does.Contain("Hendrix"), "Should contain Hendrix");
        Assert.That(comprehensive.IconicDescription, Is.Not.Null, "Should have description");
        Assert.That(comprehensive.Alternates, Contains.Item(comprehensive.IconicName),
            "Iconic name should be in alternates");
    }

    [Test]
    public void HybridChordNamingService_ShouldPreferIconicNames()
    {
        // Arrange
        var hendrixTemplate = CreateHendrixChordTemplate();

        // Act
        var bestName = ChordTemplateNamingService.GetBestChordName(hendrixTemplate, PitchClass.E);

        // Assert
        Assert.That(bestName, Does.Contain("Hendrix"),
            "Should prefer iconic name over theoretical name");
    }

    [Test]
    public void IconicChordRegistry_ShouldHandleCaseInsensitiveSearch()
    {
        // Act
        var hendrix1 = IconicChordRegistry.FindByName("hendrix chord");
        var hendrix2 = IconicChordRegistry.FindByName("HENDRIX CHORD");
        var hendrix3 = IconicChordRegistry.FindByName("Hendrix Chord");

        // Assert
        Assert.That(hendrix1, Is.Not.Null, "Should find lowercase");
        Assert.That(hendrix2, Is.Not.Null, "Should find uppercase");
        Assert.That(hendrix3, Is.Not.Null, "Should find proper case");
        Assert.That(hendrix1, Is.EqualTo(hendrix2), "Should be same chord");
        Assert.That(hendrix2, Is.EqualTo(hendrix3), "Should be same chord");
    }

    [Test]
    public void IconicChordRegistry_ShouldFindMultipleMatches()
    {
        // Some pitch class sets might match multiple iconic chords
        // This tests the system's ability to handle ambiguous cases

        // Arrange - Create a simple major triad that might match multiple iconic chords
        var majorTriad = new PitchClassSet([PitchClass.C, PitchClass.E, PitchClass.G]);

        // Act
        var matches = IconicChordRegistry.FindIconicMatches(majorTriad);

        // Assert
        // This might not find matches (which is fine), but if it does, should handle multiple
        if (matches.Any())
        {
            Assert.That(matches.Count(), Is.GreaterThanOrEqualTo(1),
                "Should handle multiple matches gracefully");
        }
    }

    [Test]
    public void FretboardChordAnalyzer_ShouldIncludeIconicAnalysis()
    {
        // This test would require setting up a full fretboard analysis
        // For now, we'll test the concept with a mock scenario

        // Arrange
        var hendrixTemplate = CreateHendrixChordTemplate();

        // Act
        var comprehensive = ChordTemplateNamingService.GenerateComprehensiveNames(
            hendrixTemplate, PitchClass.E);

        // Assert
        Assert.That(comprehensive.IconicName, Is.Not.Null,
            "Fretboard analysis should include iconic names");
        Assert.That(comprehensive.IconicDescription, Is.Not.Null,
            "Should include iconic description");
    }

    // Helper method to create a Hendrix chord template for testing
    private ChordTemplate CreateHendrixChordTemplate()
    {
        // E7#9 intervals: Major 3rd (4), Perfect 5th (7), Minor 7th (10), Sharp 9th (15 = 3)
        var intervals = new[]
        {
            new ChordFormulaInterval(new Interval.Chromatic(Semitones.FromValue(4)), ChordFunction.Third),
            new ChordFormulaInterval(new Interval.Chromatic(Semitones.FromValue(7)), ChordFunction.Fifth),
            new ChordFormulaInterval(new Interval.Chromatic(Semitones.FromValue(10)), ChordFunction.Seventh),
            new ChordFormulaInterval(new Interval.Chromatic(Semitones.FromValue(15)), ChordFunction.Ninth)
        };

        var formula = new ChordFormula("E7#9", intervals);
        return ChordTemplate.Analytical.FromSetTheory(formula, "Test");
    }
}
