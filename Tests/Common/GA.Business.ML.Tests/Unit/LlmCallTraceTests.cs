namespace GA.Business.ML.Tests.Unit;

using Agents;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging.Abstractions;

/// <summary>
/// Pins that agents report every LLM round trip to the observer of the current request.
/// The agentic trace showed one opaque "orchestration.answer" step of up to 60 s; the
/// theory agent alone makes three sequential calls (draft, critique, rewrite) inside it.
/// </summary>
[TestFixture]
public class LlmCallTraceTests
{
    [Test]
    public async Task TheoryAgent_ReportsItsDraftCritiqueAndRewriteCalls()
    {
        var agent = new TheoryAgent(new ScriptedChatClient(), NullLogger<TheoryAgent>.Instance);
        List<LlmCall> calls = [];

        using (LlmCallTrace.Observe(calls.Add))
        {
            await agent.ProcessAsync(new AgentRequest { Query = "Why does Dm7 sound sad?" });
        }

        Assert.That(calls.Select(c => c.Pass), Is.EqualTo(new[] { "draft", "critique", "rewrite" }));
        Assert.Multiple(() =>
        {
            Assert.That(calls.All(c => c.AgentId == agent.AgentId && c.Succeeded));
            Assert.That(calls[0].Model, Is.EqualTo("test-model"));
            Assert.That(calls[0].InputTokens, Is.EqualTo(120));
            Assert.That(calls[0].OutputTokens, Is.EqualTo(30));
            Assert.That(calls[0].ToolCalls, Is.EqualTo(1));
            Assert.That(calls[0].ResponseLength, Is.GreaterThan(0));
        });
    }

    [Test]
    public async Task NoObserver_ReportsNothing_AndAnObserverIsRestoredOnDispose()
    {
        var agent = new TheoryAgent(new ScriptedChatClient(), NullLogger<TheoryAgent>.Instance);
        List<LlmCall> calls = [];

        using (LlmCallTrace.Observe(calls.Add)) { }
        await agent.ProcessAsync(new AgentRequest { Query = "Why does Dm7 sound sad?" });

        Assert.That(calls, Is.Empty);
    }

    [Test]
    public void FailedCall_IsReportedAsFailed()
    {
        var agent = new TheoryAgent(new ScriptedChatClient { Fail = true }, NullLogger<TheoryAgent>.Instance);
        List<LlmCall> calls = [];

        using (LlmCallTrace.Observe(calls.Add))
        {
            Assert.ThrowsAsync<HttpRequestException>(() =>
                agent.ProcessAsync(new AgentRequest { Query = "Why does Dm7 sound sad?" }));
        }

        Assert.That(calls, Has.Count.EqualTo(1));
        Assert.That(calls[0].Succeeded, Is.False);
    }

    private sealed class ScriptedChatClient : IChatClient
    {
        public bool Fail { get; init; }

        public Task<ChatResponse> GetResponseAsync(
            IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default)
        {
            if (Fail) throw new HttpRequestException("Connection refused");

            // A tool round trip followed by the final answer, as FunctionInvokingChatClient returns it.
            ChatMessage[] reply =
            [
                new(ChatRole.Assistant, [new FunctionCallContent("call-1", "ga_chord_info")]),
                new(ChatRole.Tool, [new FunctionResultContent("call-1", "D F A C")]),
                new(ChatRole.Assistant, """{"result":"Dm7 has a minor third.","confidence":0.9}"""),
            ];
            return Task.FromResult(new ChatResponse(reply)
            {
                ModelId = "test-model",
                Usage = new UsageDetails { InputTokenCount = 120, OutputTokenCount = 30 },
            });
        }

        public IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
            IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public object? GetService(Type serviceType, object? serviceKey = null) => null;
        public void Dispose() { }
    }
}
