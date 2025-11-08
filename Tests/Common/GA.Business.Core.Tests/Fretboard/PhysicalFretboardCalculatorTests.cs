namespace GA.Business.Core.Tests.Fretboard;

using Core.Fretboard.Analysis;
using Core.Fretboard.Primitives;
using static Core.Fretboard.Primitives.Position;

[TestFixture]
public class PhysicalFretboardCalculatorTests
{
    [SetUp]
    public void Setup()
    {
        _fretboard = new Tests.Fretboard();
    }

    private Tests.Fretboard _fretboard = null!;

    [Test]
    public void FretPosition_ShouldDecreaseLogarithmically()
    {
        // Arrange & Act
        var fret1Width = PhysicalFretboardCalculator.CalculateFretWidthMm(1);
        var fret5Width = PhysicalFretboardCalculator.CalculateFretWidthMm(5);
        var fret12Width = PhysicalFretboardCalculator.CalculateFretWidthMm(12);
        var fret17Width = PhysicalFretboardCalculator.CalculateFretWidthMm(17);

        // Assert - Fret spacing should decrease as you go up the neck
        Assert.That(fret1Width, Is.GreaterThan(fret5Width), "Fret 1 should be wider than fret 5");
        Assert.That(fret5Width, Is.GreaterThan(fret12Width), "Fret 5 should be wider than fret 12");
        Assert.That(fret12Width, Is.GreaterThan(fret17Width), "Fret 12 should be wider than fret 17");

        TestContext.WriteLine("Fret widths (mm):");
        TestContext.WriteLine($"  Fret 1:  {fret1Width:F2}mm");
        TestContext.WriteLine($"  Fret 5:  {fret5Width:F2}mm");
        TestContext.WriteLine($"  Fret 12: {fret12Width:F2}mm");
        TestContext.WriteLine($"  Fret 17: {fret17Width:F2}mm");
        TestContext.WriteLine($"  Ratio (fret 1 / fret 17): {fret1Width / fret17Width:F2}x");
    }

    [Test]
    public void FretDistance_SameFretSpan_DifferentPositions_ShouldHaveDifferentPhysicalDistances()
    {
        // Arrange & Act
        // 5-fret span at different positions
        var lowPosition = PhysicalFretboardCalculator.CalculateFretDistanceMm(0, 5); // Frets 0-5
        var midPosition = PhysicalFretboardCalculator.CalculateFretDistanceMm(7, 12); // Frets 7-12
        var highPosition = PhysicalFretboardCalculator.CalculateFretDistanceMm(12, 17); // Frets 12-17

        // Assert - Same fret span should be physically smaller at higher positions
        Assert.That(lowPosition, Is.GreaterThan(midPosition),
            "Low position 5-fret span should be wider than mid position");
        Assert.That(midPosition, Is.GreaterThan(highPosition),
            "Mid position 5-fret span should be wider than high position");

        TestContext.WriteLine("5-fret span physical distances:");
        TestContext.WriteLine($"  Frets 0-5:   {lowPosition:F2}mm");
        TestContext.WriteLine($"  Frets 7-12:  {midPosition:F2}mm");
        TestContext.WriteLine($"  Frets 12-17: {highPosition:F2}mm");
        TestContext.WriteLine(
            $"  Difference (low vs high): {lowPosition - highPosition:F2}mm ({(lowPosition / highPosition - 1) * 100:F1}% larger)");
    }

    [Test]
    public void OpenChord_ShouldBeVeryEasy()
    {
        // Arrange - Open E major chord: [0, 2, 2, 1, 0, 0]
        var positions = ImmutableList.Create<Position>(
            new Played(new(Str.FromValue(1), Fret.Open), 64),
            new Played(new(Str.FromValue(2), Fret.Open), 59),
            new Played(new(Str.FromValue(3), Fret.One), 55),
            new Played(new(Str.FromValue(4), Fret.Two), 52),
            new Played(new(Str.FromValue(5), Fret.Two), 47),
            new Played(new(Str.FromValue(6), Fret.Open), 40)
        );

        // Act
        var analysis = PhysicalFretboardCalculator.AnalyzePlayability(positions);

        // Assert
        Assert.That(analysis.Difficulty, Is.EqualTo(PhysicalFretboardCalculator.PlayabilityDifficulty.VeryEasy));
        Assert.That(analysis.IsPlayable, Is.True);
        Assert.That(analysis.MaxFingerStretchMm, Is.LessThan(40), "Open chord should have minimal stretch");

        TestContext.WriteLine("Open E Major Analysis:");
        TestContext.WriteLine($"  Difficulty: {analysis.Difficulty}");
        TestContext.WriteLine($"  Max Stretch: {analysis.MaxFingerStretchMm:F2}mm");
        TestContext.WriteLine($"  Reason: {analysis.DifficultyReason}");
    }

    [Test]
    public void BarreChord_LowPosition_ShouldBeMoreDifficultThanHighPosition()
    {
        // Arrange - F major barre chord at 1st fret: [1, 3, 3, 2, 1, 1]
        var lowBarre = ImmutableList.Create<Position>(
            new Played(new(Str.FromValue(1), Fret.One), 65),
            new Played(new(Str.FromValue(2), Fret.One), 60),
            new Played(new(Str.FromValue(3), Fret.Two), 56),
            new Played(new(Str.FromValue(4), Fret.Three), 53),
            new Played(new(Str.FromValue(5), Fret.Three), 48),
            new Played(new(Str.FromValue(6), Fret.One), 41)
        );

        // Same shape at 12th fret
        var highBarre = ImmutableList.Create<Position>(
            new Played(new(Str.FromValue(1), Fret.FromValue(12)), 76),
            new Played(new(Str.FromValue(2), Fret.FromValue(12)), 71),
            new Played(new(Str.FromValue(3), Fret.FromValue(13)), 67),
            new Played(new(Str.FromValue(4), Fret.FromValue(14)), 64),
            new Played(new(Str.FromValue(5), Fret.FromValue(14)), 59),
            new Played(new(Str.FromValue(6), Fret.FromValue(12)), 52)
        );

        // Act
        var lowAnalysis = PhysicalFretboardCalculator.AnalyzePlayability(lowBarre);
        var highAnalysis = PhysicalFretboardCalculator.AnalyzePlayability(highBarre);

        // Assert - Same fret span should be physically easier at higher positions
        Assert.That(lowAnalysis.FretSpanMm, Is.GreaterThan(highAnalysis.FretSpanMm),
            "Low position barre should have larger physical span");
        Assert.That(lowAnalysis.MaxFingerStretchMm, Is.GreaterThan(highAnalysis.MaxFingerStretchMm),
            "Low position barre should require more finger stretch");

        TestContext.WriteLine("Barre Chord Comparison (same shape, different positions):");
        TestContext.WriteLine("  Low Position (1st fret):");
        TestContext.WriteLine($"    Physical Span: {lowAnalysis.FretSpanMm:F2}mm");
        TestContext.WriteLine($"    Max Stretch: {lowAnalysis.MaxFingerStretchMm:F2}mm");
        TestContext.WriteLine($"    Difficulty: {lowAnalysis.Difficulty}");
        TestContext.WriteLine("  High Position (12th fret):");
        TestContext.WriteLine($"    Physical Span: {highAnalysis.FretSpanMm:F2}mm");
        TestContext.WriteLine($"    Max Stretch: {highAnalysis.MaxFingerStretchMm:F2}mm");
        TestContext.WriteLine($"    Difficulty: {highAnalysis.Difficulty}");
        TestContext.WriteLine(
            $"  Difference: {lowAnalysis.FretSpanMm - highAnalysis.FretSpanMm:F2}mm ({(lowAnalysis.FretSpanMm / highAnalysis.FretSpanMm - 1) * 100:F1}% larger at low position)");
    }

    [Test]
    public void WideStretch_ShouldBeClassifiedCorrectly()
    {
        // Arrange - Extreme stretch chord: [0, x, 0, 5, 5, 5] (5-fret span with open strings)
        var extremeStretch = ImmutableList.Create<Position>(
            new Played(new(Str.FromValue(1), Fret.Five), 69),
            new Played(new(Str.FromValue(2), Fret.Five), 64),
            new Played(new(Str.FromValue(3), Fret.Five), 60),
            new Muted(Str.FromValue(4)),
            new Played(new(Str.FromValue(5), Fret.Open), 45),
            new Played(new(Str.FromValue(6), Fret.Open), 40)
        );

        // Act
        var analysis = PhysicalFretboardCalculator.AnalyzePlayability(extremeStretch);

        // Assert
        Assert.That(analysis.FretSpanMm, Is.GreaterThan(100), "5-fret span at low position should be > 100mm");
        Assert.That(analysis.Difficulty,
            Is.GreaterThanOrEqualTo(PhysicalFretboardCalculator.PlayabilityDifficulty.Challenging));

        TestContext.WriteLine("Wide Stretch Chord Analysis:");
        TestContext.WriteLine($"  Physical Span: {analysis.FretSpanMm:F2}mm");
        TestContext.WriteLine($"  Max Stretch: {analysis.MaxFingerStretchMm:F2}mm");
        TestContext.WriteLine($"  Difficulty: {analysis.Difficulty}");
        TestContext.WriteLine($"  Reason: {analysis.DifficultyReason}");
    }

    [Test]
    public void ImpossibleVoicing_ShouldBeDetected()
    {
        // Arrange - Impossible 7-fret span: [0, 7, 7, 7, 7, 7]
        var impossible = ImmutableList.Create<Position>(
            new Played(new(Str.FromValue(1), Fret.FromValue(7)), 71),
            new Played(new(Str.FromValue(2), Fret.FromValue(7)), 66),
            new Played(new(Str.FromValue(3), Fret.FromValue(7)), 62),
            new Played(new(Str.FromValue(4), Fret.FromValue(7)), 59),
            new Played(new(Str.FromValue(5), Fret.FromValue(7)), 54),
            new Played(new(Str.FromValue(6), Fret.Open), 40)
        );

        // Act
        var analysis = PhysicalFretboardCalculator.AnalyzePlayability(impossible);

        // Assert
        Assert.That(analysis.IsPlayable, Is.False, "7-fret span should be impossible");
        Assert.That(analysis.Difficulty, Is.EqualTo(PhysicalFretboardCalculator.PlayabilityDifficulty.Impossible));

        TestContext.WriteLine("Impossible Voicing Analysis:");
        TestContext.WriteLine($"  Physical Span: {analysis.FretSpanMm:F2}mm");
        TestContext.WriteLine($"  Difficulty: {analysis.Difficulty}");
        TestContext.WriteLine($"  Reason: {analysis.DifficultyReason}");
    }

    [Test]
    public void StringSpacing_ShouldIncreaseTowardBridge()
    {
        // Arrange & Act
        var nutSpacing = PhysicalFretboardCalculator.CalculateStringSpacingMM(0);
        var midSpacing = PhysicalFretboardCalculator.CalculateStringSpacingMM(12);
        var bridgeSpacing = PhysicalFretboardCalculator.CalculateStringSpacingMM(24);

        // Assert
        Assert.That(midSpacing, Is.GreaterThan(nutSpacing), "String spacing should increase toward bridge");
        Assert.That(bridgeSpacing, Is.GreaterThan(midSpacing), "String spacing should continue increasing");

        TestContext.WriteLine("String spacing at different positions:");
        TestContext.WriteLine($"  At nut (fret 0):   {nutSpacing:F2}mm");
        TestContext.WriteLine($"  At 12th fret:      {midSpacing:F2}mm");
        TestContext.WriteLine($"  At bridge (fret 24): {bridgeSpacing:F2}mm");
    }

    [Test]
    public void DifferentScaleLengths_ShouldAffectDifficulty()
    {
        // Arrange - Same chord shape on different scale lengths
        var positions = ImmutableList.Create<Position>(
            new Played(new(Str.FromValue(1), Fret.Five), 69),
            new Played(new(Str.FromValue(2), Fret.Five), 64),
            new Played(new(Str.FromValue(3), Fret.Five), 60),
            new Played(new(Str.FromValue(4), Fret.Three), 57),
            new Played(new(Str.FromValue(5), Fret.Two), 50),
            new Played(new(Str.FromValue(6), Fret.Open), 40)
        );

        // Act
        var classicalAnalysis = PhysicalFretboardCalculator.AnalyzePlayability(
            positions, PhysicalFretboardCalculator.ScaleLengths.Classical);
        var electricAnalysis = PhysicalFretboardCalculator.AnalyzePlayability(
            positions);
        var bassAnalysis = PhysicalFretboardCalculator.AnalyzePlayability(
            positions, PhysicalFretboardCalculator.ScaleLengths.Bass);

        // Assert - Longer scale = larger physical distances
        Assert.That(bassAnalysis.FretSpanMm, Is.GreaterThan(classicalAnalysis.FretSpanMm));
        Assert.That(classicalAnalysis.FretSpanMm, Is.GreaterThan(electricAnalysis.FretSpanMm));

        TestContext.WriteLine("Same chord on different scale lengths:");
        TestContext.WriteLine($"  Electric ({PhysicalFretboardCalculator.ScaleLengths.Electric}mm):");
        TestContext.WriteLine(
            $"    Span: {electricAnalysis.FretSpanMm:F2}mm, Difficulty: {electricAnalysis.Difficulty}");
        TestContext.WriteLine($"  Classical ({PhysicalFretboardCalculator.ScaleLengths.Classical}mm):");
        TestContext.WriteLine(
            $"    Span: {classicalAnalysis.FretSpanMm:F2}mm, Difficulty: {classicalAnalysis.Difficulty}");
        TestContext.WriteLine($"  Bass ({PhysicalFretboardCalculator.ScaleLengths.Bass}mm):");
        TestContext.WriteLine($"    Span: {bassAnalysis.FretSpanMm:F2}mm, Difficulty: {bassAnalysis.Difficulty}");
    }

    [Test]
    public void SuggestedFingering_ShouldDetectBarreChords()
    {
        // Arrange - F major barre chord: [1, 3, 3, 2, 1, 1]
        var barreChord = ImmutableList.Create<Position>(
            new Played(new(Str.FromValue(1), Fret.One), 65),
            new Played(new(Str.FromValue(2), Fret.One), 60),
            new Played(new(Str.FromValue(3), Fret.Two), 56),
            new Played(new(Str.FromValue(4), Fret.Three), 53),
            new Played(new(Str.FromValue(5), Fret.Three), 48),
            new Played(new(Str.FromValue(6), Fret.One), 41)
        );

        // Act
        var analysis = PhysicalFretboardCalculator.AnalyzePlayability(barreChord);

        // Assert
        var barreFingers = analysis.SuggestedFingering.Where(f => f.Technique == "barre").ToList();
        Assert.That(barreFingers, Is.Not.Empty, "Should detect barre chord pattern");
        Assert.That(barreFingers.Count, Is.GreaterThanOrEqualTo(2), "Barre should cover at least 2 strings");

        TestContext.WriteLine("Barre Chord Fingering:");
        foreach (var finger in analysis.SuggestedFingering)
        {
            TestContext.WriteLine(
                $"  String {finger.String.Value}, Fret {finger.Fret.Value}: Finger {finger.FingerNumber} ({finger.Technique})");
        }
    }
}
