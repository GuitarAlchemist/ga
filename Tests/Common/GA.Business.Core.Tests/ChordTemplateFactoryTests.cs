namespace GA.Business.Core.Tests;

using Core.Chords;
using Core.Tonal.Modes.Diatonic;
using Core.Tonal.Primitives.Diatonic;

[TestFixture]
public class ChordTemplateFactoryTests
{
    [Test]
    public void GetChordsByCharacteristics_ShouldReturnCorrectChords()
    {
        // Test finding chords by their musical characteristics, not names
        var majorTriads = ChordTemplateFactory.GetChordsByCharacteristics(
            ChordQuality.Major,
            ChordExtension.Triad).Take(10).ToList();

        var minorTriads = ChordTemplateFactory.GetChordsByCharacteristics(
            ChordQuality.Minor,
            ChordExtension.Triad).Take(10).ToList();

        var diminishedChords = ChordTemplateFactory.GetChordsByCharacteristics(
            ChordQuality.Diminished).Take(10).ToList();

        // Assert - All should be found from systematic scale generation
        Assert.That(majorTriads.Count, Is.GreaterThan(0), "Should find major triads from scale generation");
        Assert.That(minorTriads.Count, Is.GreaterThan(0), "Should find minor triads from scale generation");
        Assert.That(diminishedChords.Count, Is.GreaterThan(0), "Should find diminished chords from scale generation");

        // Verify all have correct characteristics
        Assert.That(majorTriads.All(c => c.Quality == ChordQuality.Major && c.Extension == ChordExtension.Triad),
            Is.True);
        Assert.That(minorTriads.All(c => c.Quality == ChordQuality.Minor && c.Extension == ChordExtension.Triad),
            Is.True);
        Assert.That(diminishedChords.All(c => c.Quality == ChordQuality.Diminished), Is.True);
    }

    [Test]
    public void GetChordsByIntervalPattern_ShouldReturnMatchingChords()
    {
        // Test finding chords by their actual interval structure
        var majorTriadPattern = new[] { 4, 7 }; // Major third + perfect fifth
        var minorTriadPattern = new[] { 3, 7 }; // Minor third + perfect fifth

        var majorTriads = ChordTemplateFactory.GetChordsByIntervalPattern(majorTriadPattern).Take(5).ToList();
        var minorTriads = ChordTemplateFactory.GetChordsByIntervalPattern(minorTriadPattern).Take(5).ToList();

        // Assert - Should find chords with exact interval patterns
        Assert.That(majorTriads.Count, Is.GreaterThan(0), "Should find chords with major triad intervals");
        Assert.That(minorTriads.Count, Is.GreaterThan(0), "Should find chords with minor triad intervals");

        // Verify interval patterns match
        foreach (var chord in majorTriads)
        {
            var intervals = chord.Intervals.Select(i => i.Interval.Semitones.Value).OrderBy(s => s).ToArray();
            Assert.That(intervals, Is.EqualTo(majorTriadPattern),
                $"Chord {chord.Name} should have major triad intervals");
        }
    }

    [Test]
    public void GenerateAllPossibleChords_ShouldReturnSystematicGeneration()
    {
        // Test the core systematic generation method
        var allChords = ChordTemplateFactory.GenerateAllPossibleChords().ToList(); // Limit for performance

        // Assert - Should generate many chords from scale relationships
        Assert.That(allChords.Count, Is.GreaterThan(100), "Should generate many chords from all scales and modes");

        // Verify we have different stacking types
        Assert.That(allChords.Any(c => c.StackingType == ChordStackingType.Tertian), Is.True,
            "Should have tertian chords");
        Assert.That(allChords.Any(c => c.StackingType == ChordStackingType.Quartal), Is.True,
            "Should have quartal chords");
        Assert.That(allChords.Any(c => c.StackingType == ChordStackingType.Quintal), Is.True,
            "Should have quintal chords");

        // Verify we have different extensions
        Assert.That(allChords.Any(c => c.Extension == ChordExtension.Triad), Is.True, "Should have triads");
        Assert.That(allChords.Any(c => c.Extension == ChordExtension.Seventh), Is.True, "Should have seventh chords");
        Assert.That(allChords.Any(c => c.Extension == ChordExtension.Ninth), Is.True, "Should have ninth chords");

        // All chords should be derived from scale relationships
        Assert.That(allChords.All(c => c is ChordTemplate.TonalModal or ChordTemplate.Analytical), Is.True,
            "All chords should be generated from scale modes or analytical methods");
    }

    [Test]
    public void CreateModalChords_ShouldGenerateCorrectNumberOfChords()
    {
        // Arrange
        var ionianMode = MajorScaleMode.Get(MajorScaleDegree.Ionian);

        // Act
        var modalChords = ChordTemplateFactory.CreateModalChords(ionianMode).ToList();

        // Assert
        Assert.That(modalChords.Count, Is.EqualTo(7)); // 7 degrees in major scale
        Assert.That(modalChords.All(c => c.Extension == ChordExtension.Triad), Is.True);
    }

    [Test]
    public void CreateDiatonicChords_ShouldCreateSevenTriads()
    {
        // Arrange
        var ionianMode = MajorScaleMode.Get(MajorScaleDegree.Ionian);

        // Act
        var diatonicChords = ChordTemplateFactory.CreateDiatonicChords(ionianMode);

        // Assert
        Assert.That(diatonicChords.Count, Is.EqualTo(7));
        Assert.That(diatonicChords.All(c => c.Extension == ChordExtension.Triad), Is.True);
        Assert.That(diatonicChords.All(c => c.StackingType == ChordStackingType.Tertian), Is.True);
    }

    [Test]
    public void CreateDiatonicSevenths_ShouldCreateSevenSeventhChords()
    {
        // Arrange
        var ionianMode = MajorScaleMode.Get(MajorScaleDegree.Ionian);

        // Act
        var seventhChords = ChordTemplateFactory.CreateDiatonicSevenths(ionianMode);

        // Assert
        Assert.That(seventhChords.Count, Is.EqualTo(7));
        Assert.That(seventhChords.All(c => c.Extension == ChordExtension.Seventh), Is.True);
        Assert.That(seventhChords.All(c => c.StackingType == ChordStackingType.Tertian), Is.True);
    }

    [Test]
    public void GenerateFromScaleMode_ShouldCreateAllStackingTypes()
    {
        // Test that the systematic generation creates all stacking types for a mode
        var ionianMode = MajorScaleMode.Get(MajorScaleDegree.Ionian);

        // Act
        var allChords = ChordTemplateFactory.GenerateFromScaleMode(ionianMode).ToList();

        // Assert - Should generate chords with all stacking types
        Assert.That(allChords.Any(c => c.StackingType == ChordStackingType.Tertian), Is.True,
            "Should have tertian chords");
        Assert.That(allChords.Any(c => c.StackingType == ChordStackingType.Quartal), Is.True,
            "Should have quartal chords");
        Assert.That(allChords.Any(c => c.StackingType == ChordStackingType.Quintal), Is.True,
            "Should have quintal chords");
        Assert.That(allChords.Any(c => c.StackingType == ChordStackingType.Secundal), Is.True,
            "Should have secundal chords");

        // Should have different extensions
        Assert.That(allChords.Any(c => c.Extension == ChordExtension.Triad), Is.True, "Should have triads");
        Assert.That(allChords.Any(c => c.Extension == ChordExtension.Seventh), Is.True, "Should have seventh chords");

        // All should be TonalModal chords from the scale
        Assert.That(allChords.All(c => c is ChordTemplate.TonalModal), Is.True, "All should be tonal modal chords");
    }

    [Test]
    public void GenerateFromAllModalFamilies_ShouldCoverAllScales()
    {
        // Test that we generate from all modal families systematically
        var modalFamilyChords =
            ChordTemplateFactory.GenerateFromAllModalFamilies().Take(500).ToList(); // Limit for performance

        // Assert - Should generate from many different modal families
        Assert.That(modalFamilyChords.Count, Is.GreaterThan(50), "Should generate chords from many modal families");

        // Should have variety in chord types from different modal families
        var qualities = modalFamilyChords.Select(c => c.Quality).Distinct().ToList();
        Assert.That(qualities.Count, Is.GreaterThan(1),
            "Should have chords with different qualities from different modal families");

        // All should be from modal families
        Assert.That(modalFamilyChords.All(c => c is ChordTemplate.TonalModal), Is.True,
            "All should be tonal modal chords");
    }

    [Test]
    public void CreateTraditionalChordLibrary_ShouldReturnManyChords()
    {
        // Act - Test the traditional chord library generation
        var chords = ChordTemplateFactory.CreateTraditionalChordLibrary().Take(200).ToList(); // Limit for performance

        // Assert - Should generate many chords from traditional scales
        Assert.That(chords.Count, Is.GreaterThan(50),
            "Should generate many chords from traditional scale relationships");

        // All should be from scale modes, not hard-coded
        Assert.That(chords.All(c => c is ChordTemplate.TonalModal), Is.True,
            "All should be generated from scale modes");

        // Should have variety in qualities and extensions
        Assert.That(chords.Any(c => c.Quality == ChordQuality.Major), Is.True, "Should have major chords");
        Assert.That(chords.Any(c => c.Quality == ChordQuality.Minor), Is.True, "Should have minor chords");
        Assert.That(chords.Any(c => c.Extension == ChordExtension.Seventh), Is.True, "Should have seventh chords");
    }
}
