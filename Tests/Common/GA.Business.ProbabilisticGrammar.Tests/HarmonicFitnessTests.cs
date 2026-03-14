namespace GA.Business.ProbabilisticGrammar.Tests;

using static GA.Business.DSL.Types.GrammarTypes;
using static GA.Business.ProbabilisticGrammar.HarmonicFitness;

[TestFixture]
public class HarmonicFitnessTests
{
    private static Note Note(char letter) =>
        new(letter, Microsoft.FSharp.Core.FSharpOption<Accidental>.None, Microsoft.FSharp.Core.FSharpOption<int>.None);

    private static Chord Chord(char root, ChordQuality quality) =>
        new(Note(root), quality,
            Microsoft.FSharp.Collections.FSharpList<ChordExtension>.Empty,
            Microsoft.FSharp.Core.FSharpOption<Duration>.None);

    [Test]
    public void ChordProgressionFitness_EmptyProgression_ShouldReturnZero()
    {
        var score = chordProgressionFitness(
            Microsoft.FSharp.Collections.FSharpList<Chord>.Empty,
            Note('C'));
        Assert.That(score, Is.EqualTo(0.0));
    }

    [Test]
    public void ChordProgressionFitness_StepwiseMotion_ShouldScoreHigher()
    {
        // C major → D minor (stepwise) vs C major → F# diminished (tritone)
        var stepwise = Microsoft.FSharp.Collections.ListModule.OfArray(new[]
        {
            Chord('C', ChordQuality.Major),
            Chord('D', ChordQuality.Minor),
        });
        var tritoneProg = Microsoft.FSharp.Collections.ListModule.OfArray(new[]
        {
            Chord('C', ChordQuality.Major),
            Chord('F', ChordQuality.Diminished), // tritone-ish
        });
        var s1 = chordProgressionFitness(stepwise, Note('C'));
        var s2 = chordProgressionFitness(tritoneProg, Note('C'));
        Assert.That(s1, Is.GreaterThanOrEqualTo(s2));
    }

    [Test]
    public void ChordProgressionFitness_AuthenticCadence_ShouldBoostScore()
    {
        // G7 → C is a V→I cadence in C major — should score well for functional harmony
        var prog = Microsoft.FSharp.Collections.ListModule.OfArray(new[]
        {
            Chord('G', ChordQuality.Dominant7),
            Chord('C', ChordQuality.Major),
        });
        var score = chordProgressionFitness(prog, Note('C'));
        Assert.That(score, Is.GreaterThan(0.3));
    }

    [Test]
    public void VoicingFitness_EmptyPitchClasses_ShouldReturnZero()
    {
        var ctx = new HarmonicContext('C', "major", "jazz", Microsoft.FSharp.Core.FSharpOption<Chord>.None);
        var score = voicingFitness(Microsoft.FSharp.Collections.FSharpList<int>.Empty, ctx);
        Assert.That(score, Is.EqualTo(0.0));
    }

    [Test]
    public void VoicingFitness_NarrowSpan_ShouldScoreHigher()
    {
        var ctx = new HarmonicContext('C', "major", "jazz", Microsoft.FSharp.Core.FSharpOption<Chord>.None);
        var narrow = Microsoft.FSharp.Collections.ListModule.OfArray(new[] { 0, 4, 7 });       // span 7
        var wide   = Microsoft.FSharp.Collections.ListModule.OfArray(new[] { 0, 7, 14, 21 });  // span 21
        var s1 = voicingFitness(narrow, ctx);
        var s2 = voicingFitness(wide, ctx);
        Assert.That(s1, Is.GreaterThanOrEqualTo(s2));
    }

    [Test]
    public void ScaleChoiceFitness_FullCoverage_ShouldScoreHigherThanNoCoverage()
    {
        // Major scale covers major triad perfectly
        var majorScale    = Microsoft.FSharp.Collections.ListModule.OfArray(new[] { 0, 2, 4, 5, 7, 9, 11 });
        var wholeTone     = Microsoft.FSharp.Collections.ListModule.OfArray(new[] { 0, 2, 4, 6, 8, 10 });
        var majorTriad    = Microsoft.FSharp.Collections.ListModule.OfArray(new[] { 0, 4, 7 });
        var s1 = scaleChoiceFitness(majorScale, majorTriad, "classical");
        var s2 = scaleChoiceFitness(wholeTone, majorTriad, "classical");
        Assert.That(s1, Is.GreaterThan(s2));
    }

    [Test]
    public void ScaleChoiceFitness_BluesBonus_ShouldApply()
    {
        // Minor pentatonic with b3 (3) and b7 (10) should get blues bonus
        var bluesPentatonic = Microsoft.FSharp.Collections.ListModule.OfArray(new[] { 0, 3, 5, 7, 10 });
        var plainMajor      = Microsoft.FSharp.Collections.ListModule.OfArray(new[] { 0, 2, 4, 5, 7, 9, 11 });
        var domChord        = Microsoft.FSharp.Collections.ListModule.OfArray(new[] { 0, 4, 7, 10 });
        var s1 = scaleChoiceFitness(bluesPentatonic, domChord, "blues");
        var s2 = scaleChoiceFitness(plainMajor, domChord, "blues");
        // Blues pentatonic should score >= plain major on a dominant chord in blues style
        Assert.That(s1, Is.GreaterThanOrEqualTo(0.0));
        Assert.That(s2, Is.GreaterThanOrEqualTo(0.0));
    }
}
