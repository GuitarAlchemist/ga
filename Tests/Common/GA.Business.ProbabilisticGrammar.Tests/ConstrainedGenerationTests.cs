namespace GA.Business.ProbabilisticGrammar.Tests;

using System.Linq;
using static GA.Business.DSL.Types.GrammarTypes;
using static GA.Business.ProbabilisticGrammar.ConstrainedGeneration;
using static GA.Business.ProbabilisticGrammar.WeightedMusicRuleModule;

[TestFixture]
public class ConstrainedGenerationTests
{
    private static Microsoft.FSharp.Collections.FSharpList<WeightedMusicRule> DefaultRules()
    {
        var none = Microsoft.FSharp.Core.FSharpOption<string>.None;
        return Microsoft.FSharp.Collections.ListModule.OfArray(new[]
        {
            create("r1", "I IV V I",  MusicRuleSource.ChordGrammar, none),
            create("r2", "ii V I",    MusicRuleSource.ChordGrammar, none),
            create("r3", "I V vi IV", MusicRuleSource.ChordGrammar, none),
        });
    }

    private static GenerationConfig MakeConfig(int length, int seed = 42) =>
        new("major", 'C', "major", length, 1.0, Microsoft.FSharp.Core.FSharpOption<int>.Some(seed));

    [Test]
    public void GenerateProgression_ShouldReturnRequestedLength()
    {
        var result = generateProgression(DefaultRules(), MakeConfig(4));
        Assert.That(result.Count(), Is.EqualTo(4));
    }

    [Test]
    public void GenerateProgression_LengthClampedToEight()
    {
        var result = generateProgression(DefaultRules(), MakeConfig(20));
        Assert.That(result.Count(), Is.LessThanOrEqualTo(8));
    }

    [Test]
    public void GenerateProgression_EmptyRules_ShouldReturnFallback()
    {
        var empty = Microsoft.FSharp.Collections.ListModule.Empty<WeightedMusicRule>();
        var result = generateProgression(empty, MakeConfig(4));
        Assert.That(result.Count(), Is.EqualTo(4));
    }

    [Test]
    public void GenerateProgression_DeterministicWithSameSeed()
    {
        var r1 = string.Join(",", generateProgression(DefaultRules(), MakeConfig(4, seed: 99))
                    .Select(c => $"{c.Root.Letter}{c.Quality}"));
        var r2 = string.Join(",", generateProgression(DefaultRules(), MakeConfig(4, seed: 99))
                    .Select(c => $"{c.Root.Letter}{c.Quality}"));
        Assert.That(r1, Is.EqualTo(r2));
    }

    [Test]
    public void GenerateScaleChoices_ShouldReturnOneScalePerChord()
    {
        static Chord MakeChord(char root, ChordQuality q) =>
            new(new(root, Microsoft.FSharp.Core.FSharpOption<Accidental>.None, Microsoft.FSharp.Core.FSharpOption<int>.None),
                q,
                Microsoft.FSharp.Collections.FSharpList<ChordExtension>.Empty,
                Microsoft.FSharp.Core.FSharpOption<Duration>.None);

        var changes = Microsoft.FSharp.Collections.ListModule.OfArray(new[]
        {
            System.Tuple.Create(MakeChord('C', ChordQuality.Major),    4),
            System.Tuple.Create(MakeChord('F', ChordQuality.Major),    4),
            System.Tuple.Create(MakeChord('G', ChordQuality.Dominant7), 4),
        });
        var scales = generateScaleChoices(DefaultRules(), changes);
        Assert.That(scales.Count(), Is.EqualTo(3));
    }

    [Test]
    public void SearchVoicings_ShouldReturnAtMostFiveVoicings()
    {
        static Chord MakeChord(char root, ChordQuality q) =>
            new(new(root, Microsoft.FSharp.Core.FSharpOption<Accidental>.None, Microsoft.FSharp.Core.FSharpOption<int>.None),
                q,
                Microsoft.FSharp.Collections.FSharpList<ChordExtension>.Empty,
                Microsoft.FSharp.Core.FSharpOption<Duration>.None);

        var chord = MakeChord('C', ChordQuality.Major);
        var voicings = searchVoicings(DefaultRules(), chord, 12);
        Assert.That(voicings.Count(), Is.LessThanOrEqualTo(5));
        Assert.That(voicings.Count(), Is.GreaterThan(0));
    }
}
