namespace GA.Business.ProbabilisticGrammar.Tests;

using static GA.Business.ProbabilisticGrammar.WeightedMusicRuleModule;

[TestFixture]
public class WeightedMusicRuleTests
{
    [Test]
    public void Create_ShouldInitialiseWithUniformPrior()
    {
        var rule = create("r1", "I IV V I", MusicRuleSource.ChordGrammar, Microsoft.FSharp.Core.FSharpOption<string>.None);
        Assert.That(rule.Alpha, Is.EqualTo(1.0));
        Assert.That(rule.Beta, Is.EqualTo(1.0));
        Assert.That(rule.Weight, Is.EqualTo(0.5));
    }

    [Test]
    public void BayesianUpdate_Success_ShouldIncrementAlpha()
    {
        var rule = create("r1", "I IV V I", MusicRuleSource.ChordGrammar, Microsoft.FSharp.Core.FSharpOption<string>.None);
        var updated = bayesianUpdate(rule, true);
        Assert.That(updated.Alpha, Is.EqualTo(2.0));
        Assert.That(updated.Beta, Is.EqualTo(1.0));
        Assert.That(updated.Weight, Is.EqualTo(2.0 / 3.0).Within(1e-9));
    }

    [Test]
    public void BayesianUpdate_Failure_ShouldIncrementBeta()
    {
        var rule = create("r1", "I IV V I", MusicRuleSource.ChordGrammar, Microsoft.FSharp.Core.FSharpOption<string>.None);
        var updated = bayesianUpdate(rule, false);
        Assert.That(updated.Alpha, Is.EqualTo(1.0));
        Assert.That(updated.Beta, Is.EqualTo(2.0));
        Assert.That(updated.Weight, Is.EqualTo(1.0 / 3.0).Within(1e-9));
    }

    [Test]
    public void BayesianUpdate_RepeatedSuccess_ShouldConvergeTowardOne()
    {
        var rule = create("r1", "I IV V I", MusicRuleSource.ChordGrammar, Microsoft.FSharp.Core.FSharpOption<string>.None);
        for (var i = 0; i < 100; i++)
            rule = bayesianUpdate(rule, true);
        Assert.That(rule.Weight, Is.GreaterThan(0.99));
    }

    [Test]
    public void SoftmaxByContext_EmptyList_ShouldReturnEmptyList()
    {
        var probs = softmaxByContext(Microsoft.FSharp.Collections.ListModule.Empty<WeightedMusicRule>(), "jazz");
        Assert.That(probs, Is.Empty);
    }

    [Test]
    public void SoftmaxByContext_SingleRule_ShouldReturnOne()
    {
        var rule = create("r1", "I IV V I", MusicRuleSource.ChordGrammar, Microsoft.FSharp.Core.FSharpOption<string>.None);
        var rules = Microsoft.FSharp.Collections.FSharpList<WeightedMusicRule>.Cons(rule, Microsoft.FSharp.Collections.FSharpList<WeightedMusicRule>.Empty);
        var probs = softmaxByContext(rules, "jazz");
        Assert.That(probs.Head, Is.EqualTo(1.0).Within(1e-9));
    }

    [Test]
    public void SoftmaxByContext_MultipleRules_ShouldSumToOne()
    {
        var rules = new[]
        {
            create("r1", "I IV V I",  MusicRuleSource.ChordGrammar, Microsoft.FSharp.Core.FSharpOption<string>.None),
            create("r2", "ii V I",    MusicRuleSource.ChordGrammar, Microsoft.FSharp.Core.FSharpOption<string>.None),
            create("r3", "I vi IV V", MusicRuleSource.ChordGrammar, Microsoft.FSharp.Core.FSharpOption<string>.None),
        };
        var fsList = Microsoft.FSharp.Collections.ListModule.OfArray(rules);
        var probs = softmaxByContext(fsList, "jazz");
        var sum = 0.0;
        foreach (var p in probs) sum += p;
        Assert.That(sum, Is.EqualTo(1.0).Within(1e-9));
    }

    [Test]
    public void SelectWeighted_EmptyList_ShouldReturnNone()
    {
        var result = selectWeighted(Microsoft.FSharp.Collections.ListModule.Empty<WeightedMusicRule>(), new System.Random(42));
        Assert.That(Microsoft.FSharp.Core.FSharpOption<WeightedMusicRule>.get_IsNone(result), Is.True);
    }

    [Test]
    public void SelectWeighted_SingleRule_ShouldAlwaysReturnThatRule()
    {
        var rule = create("r1", "I IV V I", MusicRuleSource.ChordGrammar, Microsoft.FSharp.Core.FSharpOption<string>.None);
        var rules = Microsoft.FSharp.Collections.FSharpList<WeightedMusicRule>.Cons(rule, Microsoft.FSharp.Collections.FSharpList<WeightedMusicRule>.Empty);
        var selected = selectWeighted(rules, new System.Random(42));
        Assert.That(Microsoft.FSharp.Core.FSharpOption<WeightedMusicRule>.get_IsSome(selected), Is.True);
        Assert.That(selected.Value.RuleId, Is.EqualTo("r1"));
    }

    [Test]
    public void TopN_ShouldReturnHighestWeightFirst()
    {
        var r1 = create("low", "I IV",  MusicRuleSource.ChordGrammar, Microsoft.FSharp.Core.FSharpOption<string>.None);
        var r2 = bayesianUpdate(create("high", "I V I", MusicRuleSource.ChordGrammar, Microsoft.FSharp.Core.FSharpOption<string>.None), true);
        var rules = Microsoft.FSharp.Collections.ListModule.OfArray(new[] { r1, r2 });
        var top = topN(1, rules);
        Assert.That(top.Head.RuleId, Is.EqualTo("high"));
    }
}
