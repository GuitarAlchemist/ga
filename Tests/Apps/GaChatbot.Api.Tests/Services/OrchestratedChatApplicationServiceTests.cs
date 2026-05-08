namespace GaChatbot.Api.Tests.Services;

using GA.Business.Core.Orchestration.Models;
using GaChatbot.Api.Controllers;
using GaChatbot.Api.Services;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using AiChatMessage = Microsoft.Extensions.AI.ChatMessage;
using AiChatResponse = Microsoft.Extensions.AI.ChatResponse;
using ChatResponse = GA.Business.Core.Orchestration.Models.ChatResponse;

[TestFixture]
public sealed class OrchestratedChatApplicationServiceTests
{
    [TestCase("", "empty-fallback")]
    [TestCase("   ", "empty-fallback")]
    public async Task ChatAsync_FallsBackWhenOrchestratorReturnsBlankAnswer(
        string orchestratorAnswer,
        string expectedRoutingMethod)
    {
        var service = CreateService(new StubOrchestrator(new ChatResponse(
            orchestratorAnswer,
            [],
            Routing: new AgentRoutingMetadata("theory", 0.9f, "semantic"))));

        var result = await service.ChatAsync(new ChatExecutionRequest("Explain C major."));

        AssertFallback(result, expectedRoutingMethod);
    }

    [Test]
    public async Task ChatAsync_FallsBackWhenOrchestratorThrows()
    {
        var service = CreateService(new StubOrchestrator(Exception: new InvalidOperationException("boom")));

        var result = await service.ChatAsync(new ChatExecutionRequest("Explain C major."));

        AssertFallback(result, "error-fallback");
    }

    [Test]
    public async Task ChatAsync_BoundsDirectFallbackWhenFallbackChatTimesOut()
    {
        var service = CreateService(
            new StubOrchestrator(Exception: new InvalidOperationException("boom")),
            directChatClient: new BlockingChatClient(),
            configurationOverrides: new Dictionary<string, string?>
            {
                ["Chatbot:FallbackTimeoutSeconds"] = "1"
            });

        var result = await service.ChatAsync(new ChatExecutionRequest("Explain C major."));

        Assert.Multiple(() =>
        {
            Assert.That(result.NaturalLanguageAnswer, Does.Contain("fallback timeout"));
            Assert.That(result.Routing.AgentId, Is.EqualTo("fallback-direct"));
            Assert.That(result.Routing.Confidence, Is.EqualTo(0f));
            Assert.That(result.Routing.RoutingMethod, Is.EqualTo("error-fallback-timeout"));
            Assert.That(result.Trace?.Steps.Single(step => step.Name == "gen_ai.chat.fallback").Status, Is.EqualTo("error"));
        });
    }

    [Test]
    public async Task ChatAsync_FallsBackWhenOrchestratorTimesOut()
    {
        var service = CreateService(new StubOrchestrator(Exception: new OperationCanceledException()));

        var result = await service.ChatAsync(new ChatExecutionRequest("Explain C major."));

        AssertFallback(result, "timeout-fallback");
    }

    [Test]
    public async Task ChatAsync_FallsBackWhenLowConfidenceAndUngrounded()
    {
        var service = CreateService(new StubOrchestrator(new ChatResponse(
            "Maybe something about C.",
            [],
            Routing: new AgentRoutingMetadata("theory", 0.1f, "semantic"))));

        var result = await service.ChatAsync(new ChatExecutionRequest("Explain C major."));

        AssertFallback(result, "low-confidence-fallback");
    }

    [Test]
    public async Task ChatAsync_FallsBackWhenOrchestratorReturnsUngroundedNoMatchAnswer()
    {
        var service = CreateService(new StubOrchestrator(new ChatResponse(
            "The OPTIC-K index returned no matches for this query.",
            [],
            Routing: new AgentRoutingMetadata("voicing", 0.97f, "llm"))));

        var result = await service.ChatAsync(new ChatExecutionRequest("What did I ask about previously?"));

        AssertFallback(result, "no-match-fallback");
    }

    [Test]
    public async Task ChatAsync_KeepsLowConfidenceAnswerWhenGrounded()
    {
        var service = CreateService(new StubOrchestrator(new ChatResponse(
            "C major contains C, E, and G.",
            [],
            Routing: new AgentRoutingMetadata("skill.ChordInfo", 0.1f, "orchestrator-skill"),
            Grounding: new GroundingMetadata("skill", "test", "chord"))));

        var result = await service.ChatAsync(new ChatExecutionRequest("Explain C major."));

        Assert.Multiple(() =>
        {
            Assert.That(result.NaturalLanguageAnswer, Is.EqualTo("C major contains C, E, and G."));
            Assert.That(result.Routing.AgentId, Is.EqualTo("skill.ChordInfo"));
            Assert.That(result.Routing.RoutingMethod, Is.EqualTo("orchestrator-skill"));
            Assert.That(result.Grounding?.Source, Is.EqualTo("skill"));
        });
    }

    [Test]
    public async Task ChatAsync_ForwardsRequestAndHistoryToOrchestrator()
    {
        var orchestrator = new StubOrchestrator(new ChatResponse(
            "orchestrated answer",
            [],
            Routing: new AgentRoutingMetadata("theory", 0.8f, "semantic")));
        var service = CreateService(orchestrator);
        var history = new List<ConversationTurn>
        {
            new("user", "Explain C major.", DateTimeOffset.UtcNow),
            new("assistant", "C major contains C, E, and G.", DateTimeOffset.UtcNow)
        };

        await service.ChatAsync(new ChatExecutionRequest("What about minor?", history));

        Assert.That(orchestrator.LastRequest, Is.Not.Null);
        Assert.Multiple(() =>
        {
            Assert.That(orchestrator.LastRequest!.Message, Is.EqualTo("What about minor?"));
            Assert.That(orchestrator.LastRequest.History, Is.SameAs(history));
        });
    }

    [Test]
    public async Task ChatAsync_AugmentsKnownVoicingDiagramsAndAddsDetailedTraceSteps()
    {
        const string answer =
            """
            Found 1 voicing matching chord Dm7 + tags [shell]:

            - **Dm7(shell)** `10-13-10-x-x-x` (guitar, score 0.594)
            """;
        var service = CreateService(new StubOrchestrator(new ChatResponse(
            answer,
            [],
            Routing: new AgentRoutingMetadata("voicing", 0.58f, "semantic"),
            QueryFilters: new QueryFilters
            {
                Intent = "FindVoicing",
                Quality = "m7"
            },
            DebugParams: new { Mode = "Agent", Agent = "voicing" })));

        var result = await service.ChatAsync(new ChatExecutionRequest("Dm7 shell voicings"));

        Assert.Multiple(() =>
        {
            Assert.That(result.NaturalLanguageAnswer, Does.Contain("```vextab"));
            Assert.That(result.NaturalLanguageAnswer, Does.Contain("6/10 5/13 4/10"));
            Assert.That(result.Trace?.Steps.Select(step => step.Name), Does.Contain("orchestration.route"));
            Assert.That(result.Trace?.Steps.Select(step => step.Name), Does.Contain("agent.semantic_result"));
            Assert.That(result.Trace?.Steps.Select(step => step.Name), Does.Contain("notation.vextab"));
            Assert.That(result.Trace?.Steps.Select(step => step.Name), Does.Contain("response.emit"));
        });

        var notationStep = result.Trace!.Steps.Single(step => step.Name == "notation.vextab");
        Assert.Multiple(() =>
        {
            Assert.That(notationStep.Attributes["notation.diagram.count"], Is.EqualTo(1));
            Assert.That(notationStep.Attributes["notation.vextab.added_count"], Is.EqualTo(1));
        });
    }

    private static OrchestratedChatApplicationService CreateService(
        StubOrchestrator orchestrator,
        IChatClient? directChatClient = null,
        Dictionary<string, string?>? configurationOverrides = null)
    {
        var directChat = new DirectChatApplicationService(
            directChatClient ?? new StubChatClient("direct fallback answer"),
            new ReadyProbe());
        var configurationValues = new Dictionary<string, string?>
        {
            ["Chatbot:StreamTimeoutSeconds"] = "5",
            ["Chatbot:FallbackMinConfidence"] = "0.25"
        };
        if (configurationOverrides is not null)
        {
            foreach (var pair in configurationOverrides)
            {
                configurationValues[pair.Key] = pair.Value;
            }
        }

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(configurationValues)
            .Build();

        return new OrchestratedChatApplicationService(
            orchestrator,
            new ReadyProbe(),
            directChat,
            configuration,
            NullLogger<OrchestratedChatApplicationService>.Instance);
    }

    private static void AssertFallback(ChatExecutionResult result, string expectedRoutingMethod) =>
        Assert.Multiple(() =>
        {
            Assert.That(result.NaturalLanguageAnswer, Is.EqualTo("direct fallback answer"));
            Assert.That(result.Routing.AgentId, Is.EqualTo("fallback-direct"));
            Assert.That(result.Routing.Confidence, Is.EqualTo(0f),
                "fallback confidence must be 0 — direct chat is not grounded; " +
                "calling code arbitrating routing must not treat a fallback answer " +
                "as if it were deterministic. P1 #5 silent-degradation rule.");
            Assert.That(result.Routing.RoutingMethod, Is.EqualTo(expectedRoutingMethod));
            Assert.That(result.Grounding, Is.Null);
        });

    private sealed class StubOrchestrator(
        ChatResponse? Response = null,
        Exception? Exception = null) : IProductionChatOrchestratorClient
    {
        public ChatExecutionRequest? LastRequest { get; private set; }

        public Task<ChatResponse> AnswerAsync(
            ChatExecutionRequest request,
            CancellationToken cancellationToken = default)
        {
            LastRequest = request;
            if (Exception is not null)
            {
                throw Exception;
            }

            return Task.FromResult(Response!);
        }
    }

    private sealed class StubChatClient(string responseText) : IChatClient
    {
        public Task<AiChatResponse> GetResponseAsync(
            IEnumerable<AiChatMessage> messages,
            ChatOptions? options = null,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new AiChatResponse(new AiChatMessage(ChatRole.Assistant, responseText)));

        public IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
            IEnumerable<AiChatMessage> messages,
            ChatOptions? options = null,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public object? GetService(Type serviceType, object? serviceKey = null) => null;

        public void Dispose()
        {
        }
    }

    private sealed class BlockingChatClient : IChatClient
    {
        public async Task<AiChatResponse> GetResponseAsync(
            IEnumerable<AiChatMessage> messages,
            ChatOptions? options = null,
            CancellationToken cancellationToken = default)
        {
            await Task.Delay(TimeSpan.FromSeconds(30), cancellationToken);
            return new AiChatResponse(new AiChatMessage(ChatRole.Assistant, "late answer"));
        }

        public IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
            IEnumerable<AiChatMessage> messages,
            ChatOptions? options = null,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public object? GetService(Type serviceType, object? serviceKey = null) => null;

        public void Dispose()
        {
        }
    }

    private sealed class ReadyProbe : IChatProviderReadinessProbe
    {
        public Task<ChatbotStatus> GetStatusAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(new ChatbotStatus
            {
                IsAvailable = true,
                Message = "ready",
                Timestamp = DateTime.UtcNow
            });
    }
}
