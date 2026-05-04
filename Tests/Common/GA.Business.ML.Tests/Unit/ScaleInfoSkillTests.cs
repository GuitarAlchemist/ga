namespace GA.Business.ML.Tests.Unit;

using GA.Business.ML.Agents.Skills;
using Microsoft.Extensions.Logging.Abstractions;

[TestFixture]
public class ScaleInfoSkillTests
{
    private static ScaleInfoSkill MakeSkill() => new(NullLogger<ScaleInfoSkill>.Instance);

    [Test]
    public void ExamplePrompts_IncludeBareWhatIsXMajorPattern()
    {
        // Regression test for the user-reported routing failure:
        // "What is C major?" was falling back to the LLM because no example
        // prompt was structurally close enough to clear the 0.65 cosine
        // threshold. The fix is to include the bare "What is X major/minor?"
        // shape in ExamplePrompts so the SemanticIntentRouter scores it
        // near-1.0 for that phrasing.
        //
        // If this test fails, "What is C major?" / "What is A minor?" /
        // "What is F# major?" queries will likely fall back to the LLM in
        // production. Re-add the patterns before removing them.
        var skill = MakeSkill();

        Assert.That(skill.ExamplePrompts, Has.Some.EqualTo("What is C major?"),
            "bare major-key pattern must be in ExamplePrompts so the semantic router clears the confidence threshold");
        Assert.That(skill.ExamplePrompts, Has.Some.EqualTo("What is A minor?"),
            "bare minor-key pattern must be in ExamplePrompts");
        Assert.That(skill.ExamplePrompts, Has.Some.EqualTo("What is F# major?"),
            "accidental-key pattern must be in ExamplePrompts so accidentals don't drop below the threshold");
    }

    [Test]
    public void ExamplePrompts_AlsoIncludeNotes_ResponsiblePhrasings()
    {
        // The bare "What is X major?" patterns coexist with the more explicit
        // "What notes are in X major?" / "Show me the X scale" phrasings —
        // the regression-fix added new prompts without removing existing ones.
        var skill = MakeSkill();

        Assert.That(skill.ExamplePrompts, Has.Some.Contain("notes are in"));
        Assert.That(skill.ExamplePrompts, Has.Some.Contain("scale"));
    }

    [Test]
    public void Description_DescribesKeyOrScaleLookup()
    {
        var skill = MakeSkill();

        Assert.That(skill.Description, Does.Contain("major").IgnoreCase
            .Or.Contain("minor").IgnoreCase
            .Or.Contain("key").IgnoreCase
            .Or.Contain("scale").IgnoreCase);
    }
}
