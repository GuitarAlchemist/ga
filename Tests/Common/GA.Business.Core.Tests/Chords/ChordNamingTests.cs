namespace GA.Business.Core.Tests.Chords;

using Core.Atonal;
using Core.Chords;

[TestFixture]
public class ChordNamingTests
{
    [Test]
    public void BasicChordExtensionsService_ShouldGenerateCorrectSeventhChordNames()
    {
        // Test major 7th
        var maj7Name = BasicChordExtensionsService.GenerateChordName(
            PitchClass.C, ChordQuality.Major, ChordExtension.Seventh);
        Assert.That(maj7Name, Is.EqualTo("Cmaj7"));

        // Test minor 7th
        var min7Name = BasicChordExtensionsService.GenerateChordName(
            PitchClass.C, ChordQuality.Minor, ChordExtension.Seventh);
        Assert.That(min7Name, Is.EqualTo("Cm7"));

        // Test dominant 7th
        var dom7Name = BasicChordExtensionsService.GetExtensionNotation(
            ChordExtension.Seventh, ChordQuality.Major);
        Assert.That(dom7Name, Is.EqualTo("maj7"));
    }

    [Test]
    public void BasicChordExtensionsService_ShouldGenerateCorrectNinthChordNames()
    {
        // Test major 9th
        var maj9Name = BasicChordExtensionsService.GenerateChordName(
            PitchClass.C, ChordQuality.Major, ChordExtension.Ninth);
        Assert.That(maj9Name, Is.EqualTo("Cmaj9"));

        // Test minor 9th
        var min9Name = BasicChordExtensionsService.GenerateChordName(
            PitchClass.C, ChordQuality.Minor, ChordExtension.Ninth);
        Assert.That(min9Name, Is.EqualTo("Cm9"));
    }

    [Test]
    public void BasicChordExtensionsService_ShouldGenerateCorrectSuspendedChordNames()
    {
        // Test sus4
        var sus4Name = BasicChordExtensionsService.GenerateChordName(
            PitchClass.C, ChordQuality.Suspended, ChordExtension.Sus4);
        Assert.That(sus4Name, Is.EqualTo("Csus4"));

        // Test sus2
        var sus2Name = BasicChordExtensionsService.GenerateChordName(
            PitchClass.C, ChordQuality.Suspended, ChordExtension.Sus2);
        Assert.That(sus2Name, Is.EqualTo("Csus2"));
    }

    [Test]
    public void BasicChordExtensionsService_ShouldGenerateCorrectAddedToneChordNames()
    {
        // Test add9
        var add9Name = BasicChordExtensionsService.GenerateChordName(
            PitchClass.C, ChordQuality.Major, ChordExtension.Add9);
        Assert.That(add9Name, Is.EqualTo("Cadd9"));

        // Test 6/9
        var sixNineName = BasicChordExtensionsService.GenerateChordName(
            PitchClass.C, ChordQuality.Major, ChordExtension.SixNine);
        Assert.That(sixNineName, Is.EqualTo("C6/9"));
    }

    [Test]
    public void BasicChordExtensionsService_ShouldDetectHighestExtension()
    {
        // Test 13th chord intervals
        var thirteenthIntervals = new[] { 3, 7, 10, 2, 5, 9 }; // 3rd, 5th, 7th, 9th, 11th, 13th
        var highestExtension = BasicChordExtensionsService.GetHighestExtension(thirteenthIntervals);
        Assert.That(highestExtension, Is.EqualTo(ChordExtension.Thirteenth));

        // Test 9th chord intervals
        var ninthIntervals = new[] { 3, 7, 10, 2 }; // 3rd, 5th, 7th, 9th
        var ninthExtension = BasicChordExtensionsService.GetHighestExtension(ninthIntervals);
        Assert.That(ninthExtension, Is.EqualTo(ChordExtension.Ninth));

        // Test basic triad
        var triadIntervals = new[] { 3, 7 }; // 3rd, 5th
        var triadExtension = BasicChordExtensionsService.GetHighestExtension(triadIntervals);
        Assert.That(triadExtension, Is.EqualTo(ChordExtension.Triad));
    }

    [Test]
    public void BasicChordExtensionsService_ShouldValidateExtensions()
    {
        // 9th chord should require 7th
        var ninthWithSeventh = new[] { 3, 7, 10, 2 };
        var isValidNinth = BasicChordExtensionsService.IsValidExtension(ChordExtension.Ninth, ninthWithSeventh);
        Assert.That(isValidNinth, Is.True);

        // 9th chord without 7th should be invalid
        var ninthWithoutSeventh = new[] { 3, 7, 2 };
        var isInvalidNinth = BasicChordExtensionsService.IsValidExtension(ChordExtension.Ninth, ninthWithoutSeventh);
        Assert.That(isInvalidNinth, Is.False);
    }

    [Test]
    public void SlashChordNamingService_ShouldIdentifyInversions()
    {
        // Create a simple major triad template
        var majorTriad = CreateMajorTriadTemplate();

        // Test first inversion (3rd in bass)
        var thirdInBass = PitchClass.E; // E is the 3rd of C major
        var isValidSlash = SlashChordNamingService.IsValidSlashChord(majorTriad, PitchClass.C, thirdInBass);
        Assert.That(isValidSlash, Is.True);

        var analysis = SlashChordNamingService.AnalyzeSlashChord(majorTriad, PitchClass.C, thirdInBass);
        Assert.That(analysis.Type, Is.EqualTo(SlashChordNamingService.SlashChordType.Inversion));
        Assert.That(analysis.SlashNotation, Is.EqualTo("C/E"));
        Assert.That(analysis.IsCommonInversion, Is.True);
    }

    [Test]
    public void SlashChordNamingService_ShouldGenerateSlashChordNames()
    {
        var majorTriad = CreateMajorTriadTemplate();

        // Test various slash chord combinations
        var slashNames = SlashChordNamingService.GenerateSlashChordNames(
            majorTriad, PitchClass.C, PitchClass.F);

        Assert.That(slashNames, Contains.Item("C/F"));
    }

    [Test]
    public void QuartalChordNamingService_ShouldIdentifyQuartalChords()
    {
        var quartalTemplate = CreateQuartalChordTemplate();

        var isQuartal = QuartalChordNamingService.IsQuartalHarmony(quartalTemplate);
        Assert.That(isQuartal, Is.True);

        var analysis = QuartalChordNamingService.AnalyzeQuartalChord(quartalTemplate, PitchClass.C);
        Assert.That(analysis.Type, Is.Not.EqualTo(QuartalChordNamingService.QuartalChordType.NotQuartal));
        Assert.That(analysis.PrimaryName, Does.Contain("C"));
        Assert.That(analysis.DetailedDescription, Is.Not.Null.And.Not.Empty);
        Assert.That(analysis.IntervalSizes, Is.Not.Null);
    }

    [Test]
    public void QuartalChordNamingService_ShouldDistinguishQuartalTypes()
    {
        // Test perfect fourths
        var perfectFourthsTemplate = CreatePerfectFourthsTemplate();
        var perfectAnalysis = QuartalChordNamingService.AnalyzeQuartalChord(perfectFourthsTemplate, PitchClass.C);
        Assert.That(perfectAnalysis.Type, Is.EqualTo(QuartalChordNamingService.QuartalChordType.PureFourths));
        Assert.That(perfectAnalysis.PrimaryName, Does.Contain("quartal"));

        // Test augmented fourths (tritones)
        var augmentedFourthsTemplate = CreateAugmentedFourthsTemplate();
        var augmentedAnalysis = QuartalChordNamingService.AnalyzeQuartalChord(augmentedFourthsTemplate, PitchClass.C);
        Assert.That(augmentedAnalysis.Type, Is.EqualTo(QuartalChordNamingService.QuartalChordType.AugmentedFourths));
        Assert.That(augmentedAnalysis.PrimaryName, Does.Contain("aug4"));
    }

    [Test]
    public void ChordTemplateNamingService_ShouldGenerateComprehensiveNames()
    {
        var majorTriad = CreateMajorTriadTemplate();

        // Test basic chord naming
        var comprehensive = ChordTemplateNamingService.GenerateComprehensiveNames(
            majorTriad, PitchClass.C);

        Assert.That(comprehensive.Primary, Is.EqualTo("C"));
        Assert.That(comprehensive.SlashChord, Is.Null);
        Assert.That(comprehensive.Quartal, Is.Null);
    }

    [Test]
    public void ChordTemplateNamingService_ShouldHandleSlashChords()
    {
        var majorTriad = CreateMajorTriadTemplate();

        // Test slash chord naming
        var comprehensive = ChordTemplateNamingService.GenerateComprehensiveNames(
            majorTriad, PitchClass.C, PitchClass.E);

        Assert.That(comprehensive.Primary, Is.EqualTo("C/E"));
        Assert.That(comprehensive.SlashChord, Is.EqualTo("C/E"));
    }

    [Test]
    public void ChordTemplateNamingService_ShouldGetBestName()
    {
        var majorTriad = CreateMajorTriadTemplate();

        // Test best name selection for slash chord
        var bestName = ChordTemplateNamingService.GetBestChordName(
            majorTriad, PitchClass.C, PitchClass.E);

        Assert.That(bestName, Is.EqualTo("C/E"));
    }

    [Test]
    public void ChordTemplateNamingService_ShouldGetAllNamingOptions()
    {
        var majorTriad = CreateMajorTriadTemplate();

        var allOptions = ChordTemplateNamingService.GetAllNamingOptions(
            majorTriad, PitchClass.C, PitchClass.E);

        Assert.That(allOptions, Contains.Item("C/E"));
        Assert.That(allOptions.Count(), Is.GreaterThan(0));
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
