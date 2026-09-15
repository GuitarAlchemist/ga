namespace GA.Business.ML.Tests.Unit;

using GA.Business.ML.Agents.Skills;
using Microsoft.Extensions.Logging.Abstractions;

[TestFixture]
public class FretSpanSkillTests
{
    [Test]
    public void ExamplePrompts_AreDeclared_SoSemanticRoutingCanReachTheSkill()
    {
        // SemanticIntentRouter ignores intents without example prompts.
        var skill = new FretSpanSkill(NullLogger<FretSpanSkill>.Instance);

        Assert.That(skill.ExamplePrompts, Has.Count.GreaterThanOrEqualTo(3));
    }

    [Test]
    public void ExamplePrompts_AreAllQuestionsTheSkillCanAnswer()
    {
        var skill = new FretSpanSkill(NullLogger<FretSpanSkill>.Instance);

        Assert.Multiple(() =>
        {
            foreach (var prompt in skill.ExamplePrompts)
            {
                Assert.That(skill.CanHandle(prompt), Is.True, prompt);
            }
        });
    }
}
