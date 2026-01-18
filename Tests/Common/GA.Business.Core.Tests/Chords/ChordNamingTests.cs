namespace GA.Business.Core.Tests.Chords;

using Core.Atonal;
using Core.Chords;

[TestFixture]
public class ChordNamingTests
{
    [Test]
    public void BasicChordExtensionsService_ShouldGenerateCorrectSeventhChordNames()
    {
        // Act
        // Test major 7th
        var maj7Name = BasicChordExtensionsService.GenerateChordName(
            PitchClass.C, ChordQuality.Major, ChordExtension.Seventh);
        // Test minor 7th
        var min7Name = BasicChordExtensionsService.GenerateChordName(
            PitchClass.C, ChordQuality.Minor, ChordExtension.Seventh);
        // Test dominant 7th
        var dom7Name = BasicChordExtensionsService.GetExtensionNotation(
            ChordExtension.Seventh, ChordQuality.Major);

        // Assert
        TestContext.WriteLine($"Maj7: Expected=Cmaj7, Actual={maj7Name}");
        TestContext.WriteLine($"Min7: Expected=Cm7, Actual={min7Name}");
        TestContext.WriteLine($"Dom7 Extension: Expected=maj7, Actual={dom7Name} (Legacy dominant 7th extension mapping)");
        Assert.Multiple(() =>
        {
            Assert.That(maj7Name, Is.EqualTo("Cmaj7"), "Major 7th chord should be named with 'maj7' suffix.");
            Assert.That(min7Name, Is.EqualTo("Cm7"), "Minor 7th chord should be named with 'm7' suffix.");
            Assert.That(dom7Name, Is.EqualTo("maj7"), "Internal extension notation for dominant should be 'maj7'.");
        });
    }

    [Test]
    public void BasicChordExtensionsService_ShouldGenerateCorrectNinthChordNames()
    {
        // Act
        // Test major 9th
        var maj9Name = BasicChordExtensionsService.GenerateChordName(
            PitchClass.C, ChordQuality.Major, ChordExtension.Ninth);
        // Test minor 9th
        var min9Name = BasicChordExtensionsService.GenerateChordName(
            PitchClass.C, ChordQuality.Minor, ChordExtension.Ninth);

        // Assert
        TestContext.WriteLine($"Maj9: {maj9Name}, Min9: {min9Name}");
        Assert.Multiple(() =>
        {
            Assert.That(maj9Name, Is.EqualTo("Cmaj9"));
            Assert.That(min9Name, Is.EqualTo("Cm9"));
        });
    }

    [Test]
    public void BasicChordExtensionsService_ShouldGenerateCorrectSuspendedChordNames()
    {
        // Act
        // Test sus4
        var sus4Name = BasicChordExtensionsService.GenerateChordName(
            PitchClass.C, ChordQuality.Suspended, ChordExtension.Sus4);
        // Test sus2
        var sus2Name = BasicChordExtensionsService.GenerateChordName(
            PitchClass.C, ChordQuality.Suspended, ChordExtension.Sus2);

        // Assert
        TestContext.WriteLine($"Sus4: {sus4Name}, Sus2: {sus2Name}");
        Assert.Multiple(() =>
        {
            Assert.That(sus4Name, Is.EqualTo("Csus4"));
            Assert.That(sus2Name, Is.EqualTo("Csus2"));
        });
    }

    [Test]
    public void BasicChordExtensionsService_ShouldGenerateCorrectAddedToneChordNames()
    {
        // Act
        // Test add9
        var add9Name = BasicChordExtensionsService.GenerateChordName(
            PitchClass.C, ChordQuality.Major, ChordExtension.Add9);
        // Test 6/9
        var sixNineName = BasicChordExtensionsService.GenerateChordName(
            PitchClass.C, ChordQuality.Major, ChordExtension.SixNine);

        // Assert
        TestContext.WriteLine($"Add9: {add9Name}, 6/9: {sixNineName}");
        Assert.Multiple(() =>
        {
            Assert.That(add9Name, Is.EqualTo("Cadd9"));
            Assert.That(sixNineName, Is.EqualTo("C6/9"));
        });
    }

    [Test]
    public void BasicChordExtensionsService_ShouldDetectHighestExtension()
    {
        // Arrange
        var thirteenthIntervals = new[] { 3, 7, 10, 2, 5, 9 }; // 3rd, 5th, 7th, 9th, 11th, 13th
        var ninthIntervals = new[] { 3, 7, 10, 2 }; // 3rd, 5th, 7th, 9th
        var triadIntervals = new[] { 3, 7 }; // 3rd, 5th

        // Act
        var highestExtension = BasicChordExtensionsService.GetHighestExtension(thirteenthIntervals);
        var ninthExtension = BasicChordExtensionsService.GetHighestExtension(ninthIntervals);
        var triadExtension = BasicChordExtensionsService.GetHighestExtension(triadIntervals);

        // Assert
        TestContext.WriteLine($"13th Intervals Extension: {highestExtension}");
        TestContext.WriteLine($"9th Intervals Extension: {ninthExtension}");
        TestContext.WriteLine($"Triad Intervals Extension: {triadExtension}");

        Assert.Multiple(() =>
        {
            Assert.That(highestExtension, Is.EqualTo(ChordExtension.Thirteenth));
            Assert.That(ninthExtension, Is.EqualTo(ChordExtension.Ninth));
            Assert.That(triadExtension, Is.EqualTo(ChordExtension.Triad));
        });
    }

    [Test]
    public void BasicChordExtensionsService_ShouldValidateExtensions()
    {
        // Arrange
        // 9th chord should require 7th
        var ninthWithSeventh = new[] { 3, 7, 10, 2 };
        var ninthWithoutSeventh = new[] { 3, 7, 2 };

        // Act
        var isValidNinth = BasicChordExtensionsService.IsValidExtension(ChordExtension.Ninth, ninthWithSeventh);
        var isInvalidNinth = BasicChordExtensionsService.IsValidExtension(ChordExtension.Ninth, ninthWithoutSeventh);

        // Assert
        TestContext.WriteLine($"9th with 7th Valid: {isValidNinth}, 9th without 7th Valid: {isInvalidNinth}");
        Assert.Multiple(() =>
        {
            Assert.That(isValidNinth, Is.True);
            Assert.That(isInvalidNinth, Is.False);
        });
    }

    [Test]
    public void SlashChordNamingService_ShouldIdentifyInversions()
    {
        // Arrange
        var majorTriad = CreateMajorTriadTemplate();
        var thirdInBass = PitchClass.E; // E is the 3rd of C major

        // Act
        var isValidSlash = SlashChordNamingService.IsValidSlashChord(majorTriad, PitchClass.C, thirdInBass);
        var analysis = SlashChordNamingService.AnalyzeSlashChord(majorTriad, PitchClass.C, thirdInBass);

        // Assert
        TestContext.WriteLine($"Slash Chord: C/E, Valid: {isValidSlash}, Type: {analysis.Type}, Notation: {analysis.SlashNotation}, Common: {analysis.IsCommonInversion}");
        Assert.Multiple(() =>
        {
            Assert.That(isValidSlash, Is.True);
            Assert.That(analysis.Type, Is.EqualTo(SlashChordNamingService.SlashChordType.Inversion));
            Assert.That(analysis.SlashNotation, Is.EqualTo("C/E"));
            Assert.That(analysis.IsCommonInversion, Is.True);
        });
    }

    [Test]
    public void SlashChordNamingService_ShouldGenerateSlashChordNames()
    {
        // Arrange
        var majorTriad = CreateMajorTriadTemplate();

        // Act
        var slashNames = SlashChordNamingService.GenerateSlashChordNames(
            majorTriad, PitchClass.C, PitchClass.F);

        // Assert
        TestContext.WriteLine($"Generated Slash Names for C Major over F: {string.Join(", ", slashNames)}");
        Assert.That(slashNames, Contains.Item("C/F"));
    }

    [Test]
    public void QuartalChordNamingService_ShouldIdentifyQuartalChords()
    {
        // Arrange
        var quartalTemplate = CreateQuartalChordTemplate();

        // Act
        var isQuartal = QuartalChordNamingService.IsQuartalHarmony(quartalTemplate);
        var analysis = QuartalChordNamingService.AnalyzeQuartalChord(quartalTemplate, PitchClass.C);

        // Assert
        TestContext.WriteLine($"Quartal Template - IsQuartal: {isQuartal}, Analysis Type: {analysis.Type}, Name: {analysis.PrimaryName}");
        Assert.Multiple(() =>
        {
            Assert.That(isQuartal, Is.True);
            Assert.That(analysis.Type, Is.Not.EqualTo(QuartalChordNamingService.QuartalChordType.NotQuartal));
            Assert.That(analysis.PrimaryName, Does.Contain("C"));
            Assert.That(analysis.DetailedDescription, Is.Not.Null.And.Not.Empty);
            Assert.That(analysis.IntervalSizes, Is.Not.Null);
        });
    }

    [Test]
    public void QuartalChordNamingService_ShouldDistinguishQuartalTypes()
    {
        // Arrange
        var perfectFourthsTemplate = CreatePerfectFourthsTemplate();
        var augmentedFourthsTemplate = CreateAugmentedFourthsTemplate();

        // Act
        var perfectAnalysis = QuartalChordNamingService.AnalyzeQuartalChord(perfectFourthsTemplate, PitchClass.C);
        var augmentedAnalysis = QuartalChordNamingService.AnalyzeQuartalChord(augmentedFourthsTemplate, PitchClass.C);

        // Assert
        TestContext.WriteLine($"Perfect 4ths Analysis Type: {perfectAnalysis.Type}, Name: {perfectAnalysis.PrimaryName}");
        TestContext.WriteLine($"Augmented 4ths Analysis Type: {augmentedAnalysis.Type}, Name: {augmentedAnalysis.PrimaryName}");

        Assert.Multiple(() =>
        {
            Assert.That(perfectAnalysis.Type, Is.EqualTo(QuartalChordNamingService.QuartalChordType.PureFourths));
            Assert.That(perfectAnalysis.PrimaryName, Does.Contain("quartal"));
            Assert.That(augmentedAnalysis.Type, Is.EqualTo(QuartalChordNamingService.QuartalChordType.AugmentedFourths));
            Assert.That(augmentedAnalysis.PrimaryName, Does.Contain("aug4"));
        });
    }

    [Test]
    public void ChordTemplateNamingService_ShouldGenerateComprehensiveNames()
    {
        // Arrange
        var majorTriad = CreateMajorTriadTemplate();

        // Act
        var comprehensive = ChordTemplateNamingService.GenerateComprehensiveNames(
            majorTriad, PitchClass.C);

        // Assert
        TestContext.WriteLine($"Comprehensive Names for C Major: Primary={comprehensive.Primary}, Slash={comprehensive.SlashChord}, Quartal={comprehensive.Quartal}");
        Assert.Multiple(() =>
        {
            Assert.That(comprehensive.Primary, Is.EqualTo("C"));
            Assert.That(comprehensive.SlashChord, Is.Null);
            Assert.That(comprehensive.Quartal, Is.Null);
        });
    }

    [Test]
    public void ChordTemplateNamingService_ShouldHandleSlashChords()
    {
        // Arrange
        var majorTriad = CreateMajorTriadTemplate();

        // Act
        var comprehensive = ChordTemplateNamingService.GenerateComprehensiveNames(
            majorTriad, PitchClass.C, PitchClass.E);

        // Assert
        TestContext.WriteLine($"Comprehensive Names for C/E: Primary={comprehensive.Primary}, Slash={comprehensive.SlashChord}");
        Assert.Multiple(() =>
        {
            Assert.That(comprehensive.Primary, Is.EqualTo("C/E"));
            Assert.That(comprehensive.SlashChord, Is.EqualTo("C/E"));
        });
    }

    [Test]
    public void ChordTemplateNamingService_ShouldGetBestName()
    {
        // Arrange
        var majorTriad = CreateMajorTriadTemplate();

        // Act
        var bestName = ChordTemplateNamingService.GetBestChordName(
            majorTriad, PitchClass.C, PitchClass.E);

        // Assert
        TestContext.WriteLine($"Best Chord Name for C over E: {bestName}");
        Assert.That(bestName, Is.EqualTo("C/E"));
    }

    [Test]
    public void ChordTemplateNamingService_ShouldGetAllNamingOptions()
    {
        // Arrange
        var majorTriad = CreateMajorTriadTemplate();

        // Act
        var allOptions = ChordTemplateNamingService.GetAllNamingOptions(
            majorTriad, PitchClass.C, PitchClass.E).ToList();

        // Assert
        TestContext.WriteLine($"All Naming Options for C over E: {string.Join(", ", allOptions)}");
        Assert.Multiple(() =>
        {
            Assert.That(allOptions, Contains.Item("C/E"));
            Assert.That(allOptions.Count, Is.GreaterThan(0));
        });
    }

    // Helper methods to create test chord templates
    private ChordTemplate CreateMajorTriadTemplate()
    {
        var formula = CommonChordFormulas.Major;
        return ChordTemplate.Analytical.FromSetTheory(formula, "Test");
    }

    private ChordTemplate CreateQuartalChordTemplate()
    {
        var intervals = new List<ChordFormulaInterval>
        {
            new(new Interval.Chromatic(Semitones.FromValue(5)), ChordFunction.Eleventh),
            new(new Interval.Chromatic(Semitones.FromValue(10)), ChordFunction.Seventh)
        };
        var formula = new ChordFormula("Quartal", intervals, ChordStackingType.Quartal);
        return ChordTemplate.Analytical.FromSetTheory(formula, "Test");
    }

    private ChordTemplate CreatePerfectFourthsTemplate()
    {
        // C-F-Bb (perfect fourths: 5 + 5 semitones)
        var intervals = new List<ChordFormulaInterval>
        {
            new(new Interval.Chromatic(Semitones.FromValue(5)), ChordFunction.Eleventh), // F (perfect 4th)
            new(new Interval.Chromatic(Semitones.FromValue(10)), ChordFunction.Seventh) // Bb (minor 7th)
        };
        var formula = new ChordFormula("Perfect Quartal", intervals, ChordStackingType.Quartal);
        return ChordTemplate.Analytical.FromSetTheory(formula, "Perfect Fourths");
    }

    private ChordTemplate CreateAugmentedFourthsTemplate()
    {
        // C-F#-C (augmented fourths: 6 + 6 semitones)
        var intervals = new List<ChordFormulaInterval>
        {
            new(new Interval.Chromatic(Semitones.FromValue(6)), ChordFunction.Fifth), // F# (tritone)
            new(new Interval.Chromatic(Semitones.FromValue(12)), ChordFunction.Root) // C (octave)
        };
        var formula = new ChordFormula("Augmented Quartal", intervals, ChordStackingType.Quartal);
        return ChordTemplate.Analytical.FromSetTheory(formula, "Augmented Fourths");
    }
}
