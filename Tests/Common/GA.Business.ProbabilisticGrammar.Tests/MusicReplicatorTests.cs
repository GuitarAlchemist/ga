namespace GA.Business.ProbabilisticGrammar.Tests;

using System.Linq;
using static GA.Business.ProbabilisticGrammar.MusicReplicator;
using static GA.Business.ProbabilisticGrammar.WeightedMusicRuleModule;

[TestFixture]
public class MusicReplicatorTests
{
    private static Microsoft.FSharp.Collections.FSharpList<MusicSpecies> MakeSpecies(params (string id, double p, double f)[] items)
    {
        var arr = System.Array.ConvertAll(items, x => new MusicSpecies(x.id, x.p, x.f, Microsoft.FSharp.Core.FSharpOption<string>.None));
        return Microsoft.FSharp.Collections.ListModule.OfArray(arr);
    }

    [Test]
    public void Step_FitterSpecies_ShouldGrowProportion()
    {
        var species = MakeSpecies(("fit", 0.5, 1.0), ("weak", 0.5, 0.2));
        var next = step(species, 0.1);
        var fitNext = next.First(s => s.RuleId == "fit");
        var weakNext = next.First(s => s.RuleId == "weak");
        Assert.That(fitNext.Proportion, Is.GreaterThan(0.5));
        Assert.That(weakNext.Proportion, Is.LessThan(0.5));
    }

    [Test]
    public void Step_ShouldKeepProportionsSummingToOne()
    {
        var species = MakeSpecies(("a", 0.3, 0.8), ("b", 0.4, 0.5), ("c", 0.3, 0.2));
        var next = step(species, 0.05);
        var sum = next.Sum(s => s.Proportion);
        Assert.That(sum, Is.EqualTo(1.0).Within(1e-9));
    }

    [Test]
    public void DetectStableIdioms_ShouldReturnAboveThreshold()
    {
        var species = MakeSpecies(("dominant", 0.7, 0.9), ("minor", 0.2, 0.5), ("rare", 0.1, 0.1));
        var stable = detectStableIdioms(species, 0.15);
        var ids = stable.Select(s => s.RuleId).ToList();
        Assert.That(ids, Does.Contain("dominant"));
        Assert.That(ids, Does.Contain("minor"));
        Assert.That(ids, Does.Not.Contain("rare"));
    }

    [Test]
    public void EvolveFromPreferences_NoOutcomes_ShouldReturnSameRuleCount()
    {
        var rules = new[]
        {
            create("r1", "I IV V I", MusicRuleSource.ChordGrammar, Microsoft.FSharp.Core.FSharpOption<string>.None),
            create("r2", "ii V I",   MusicRuleSource.ChordGrammar, Microsoft.FSharp.Core.FSharpOption<string>.None),
        };
        var ruleList = Microsoft.FSharp.Collections.ListModule.OfArray(rules);
        var result = evolveFromPreferences(ruleList, Microsoft.FSharp.Collections.ListModule.Empty<System.Tuple<string, bool>>());
        Assert.That(result.FinalSpecies.Count(), Is.EqualTo(2));
    }

    [Test]
    public void EvolveFromPreferences_PositiveFeedback_ShouldFavourReinforced()
    {
        var rules = new[]
        {
            create("ii_V_I",  "ii V I",   MusicRuleSource.ChordGrammar, Microsoft.FSharp.Core.FSharpOption<string>.None),
            create("I_IV_V",  "I IV V I", MusicRuleSource.ChordGrammar, Microsoft.FSharp.Core.FSharpOption<string>.None),
        };
        var ruleList = Microsoft.FSharp.Collections.ListModule.OfArray(rules);
        var outcomes = Enumerable.Repeat(System.Tuple.Create("ii_V_I", true), 10).ToList();
        var fsharpOutcomes = Microsoft.FSharp.Collections.ListModule.OfSeq(outcomes);
        var result = evolveFromPreferences(ruleList, fsharpOutcomes);
        var iiVI = result.FinalSpecies.First(s => s.RuleId == "ii_V_I");
        Assert.That(iiVI.Proportion, Is.GreaterThan(0.5));
    }

    [Test]
    public void MergeGenres_ShouldAverageProportions()
    {
        var a = MakeSpecies(("r1", 0.8, 0.9), ("r2", 0.2, 0.3));
        var b = MakeSpecies(("r1", 0.4, 0.5), ("r2", 0.6, 0.7));
        var merged = mergeGenres(a, b);
        var r1 = merged.First(s => s.RuleId == "r1");
        Assert.That(r1.Proportion, Is.EqualTo(0.6).Within(1e-9)); // (0.8+0.4)/2
    }
}
