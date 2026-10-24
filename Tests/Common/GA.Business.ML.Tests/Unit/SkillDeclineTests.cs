namespace GA.Business.ML.Tests.Unit;

using GA.Business.ML.Agents.Skills;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

/// <summary>
/// Pins <see cref="GA.Business.ML.Agents.AgentResponse.Declined"/>: a skill reached by a
/// message without its input shape declines so the orchestrator can answer elsewhere, while a
/// recognized but unresolvable request keeps its deterministic error.
/// </summary>
/// <remarks>The declining prompts are follow-ups that the live 2026-09-13 router sent to these skills.</remarks>
[TestFixture]
public sealed class SkillDeclineTests
{
    [Test]
    public async Task Capo_MessageWithoutCapoPhrase_Declines()
    {
        var response = await new CapoSkill(NullLogger<CapoSkill>.Instance)
            .ExecuteAsync("Describe his playing style in two sentences.");

        Assert.That(response.Declined, Is.True);
    }

    [Test]
    public async Task Capo_RecognizedButOutOfRangeFret_DoesNotDecline()
    {
        var response = await new CapoSkill(NullLogger<CapoSkill>.Instance)
            .ExecuteAsync("I play a C shape with capo 25, what does it sound like?");

        Assert.That(response.Declined, Is.False, response.Result);
    }

    [Test]
    public async Task RememberThis_MessageWithoutRememberPhrase_Declines()
    {
        var response = await new RememberThisSkill(NullLogger<RememberThisSkill>.Instance)
            .ExecuteAsync("What is my name, and who is my favorite guitarist?");

        Assert.That(response.Declined, Is.True);
    }

    [Test]
    public async Task KeyIdentification_MessageWithoutChords_Declines()
    {
        var response = await new KeyIdentificationSkill(Mock.Of<IChatClient>(), NullLogger<KeyIdentificationSkill>.Instance)
            .ExecuteAsync("Which key did I say I am composing in? Answer in one sentence.");

        Assert.That(response.Declined, Is.True);
    }
}
