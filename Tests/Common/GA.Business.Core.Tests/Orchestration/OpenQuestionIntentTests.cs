namespace GA.Business.Core.Tests.Orchestration;

using GA.Business.Core.Orchestration.Intents;
using GA.Business.ML.Agents;
using GA.Business.ML.Agents.Intents;

/// <summary>
/// Pins <see cref="OpenQuestionIntent"/>: it gives the semantic router a target for open
/// questions that no deterministic skill answers, and it always declines, so the orchestrator
/// hands the turn, with history, to the LLM agent path.
/// </summary>
[TestFixture]
public class OpenQuestionIntentTests
{
    [Test]
    public async Task Execute_AlwaysDeclines()
    {
        IIntent intent = new OpenQuestionIntent();

        var result = await intent.ExecuteAsync("Why does a minor chord sound sad compared to a major chord?");
        var withHistory = await intent.ExecuteAsync(
            "Give me an example of a famous song in Dorian mode.",
            [new ChatHistoryTurn("user", "What is the Dorian mode?")]);

        Assert.Multiple(() =>
        {
            Assert.That(result.Declined, Is.True);
            Assert.That(withHistory.Declined, Is.True);
        });
    }

    // The live router sent these to skill.progressionmood (0.824 via "How can I make my chords
    // sound sadder?") and skill.modes (0.777 via "What are other famous modes"), which answered
    // a different question (2026-11-18).
    [TestCase("Why does a minor chord sound sad compared to a major chord?")]
    [TestCase("Give me an example of a famous song in Dorian mode.")]
    public void ExamplePrompts_AnchorTheLiveMisroutes(string prompt)
    {
        var normalized = prompt.TrimEnd('.', '?');

        Assert.That(
            new OpenQuestionIntent().ExamplePrompts.Select(p => p.TrimEnd('.', '?')),
            Does.Contain(normalized));
    }
}
