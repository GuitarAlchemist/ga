namespace GA.Business.Core.Tests.Orchestration;

using GA.Business.Core.Orchestration.Intents;
using GA.Business.ML.Agents;
using GA.Business.ML.Agents.Intents;

/// <summary>
/// Pins that <see cref="OrchestratorSkillIntent"/> forwards <see cref="AgentResponse.Declined"/>
/// to <see cref="IntentResult.Declined"/>, the signal the orchestrator uses to fall through.
/// </summary>
[TestFixture]
public class OrchestratorSkillIntentDeclineTests
{
    [TestCase(true)]
    [TestCase(false)]
    public async Task Declined_FlowsThroughAdapter(bool declined)
    {
        var result = await new OrchestratorSkillIntent(new FixedSkill(declined)).ExecuteAsync("hi");

        Assert.That(result.Declined, Is.EqualTo(declined));
    }

    [Test]
    public async Task History_FlowsThroughAdapter()
    {
        var skill = new HistoryRecordingSkill();
        IIntent intent = new OrchestratorSkillIntent(skill);

        await intent.ExecuteAsync("Now transpose it to F.", [new ChatHistoryTurn("assistant", "Dm – G – C")]);

        Assert.That(skill.ReceivedHistory, Is.EqualTo(new[] { new ChatHistoryTurn("assistant", "Dm – G – C") }));
    }

    private sealed class HistoryRecordingSkill : IOrchestratorSkill
    {
        public IReadOnlyList<ChatHistoryTurn>? ReceivedHistory { get; private set; }
        public string Name => "Recording";
        public string Description => "Test skill that records the history it receives";
        public IReadOnlyList<string> ExamplePrompts => ["test"];
        public bool CanHandle(string message) => false;

        public Task<AgentResponse> ExecuteAsync(string message, CancellationToken cancellationToken = default) =>
            ExecuteAsync(message, [], cancellationToken);

        public Task<AgentResponse> ExecuteAsync(
            string message,
            IReadOnlyList<ChatHistoryTurn> history,
            CancellationToken cancellationToken = default)
        {
            ReceivedHistory = history;
            return Task.FromResult(new AgentResponse { AgentId = "recording", Result = "ok", Confidence = 1f });
        }
    }

    private sealed class FixedSkill(bool declined) : IOrchestratorSkill
    {
        public string Name => "Fixed";
        public string Description => "Test skill with a fixed decline flag";
        public IReadOnlyList<string> ExamplePrompts => ["test"];
        public bool CanHandle(string message) => false;

        public Task<AgentResponse> ExecuteAsync(string message, CancellationToken cancellationToken = default) =>
            Task.FromResult(new AgentResponse
            {
                AgentId = "fixed",
                Result = "not mine",
                Confidence = 0.1f,
                Declined = declined,
            });
    }
}
