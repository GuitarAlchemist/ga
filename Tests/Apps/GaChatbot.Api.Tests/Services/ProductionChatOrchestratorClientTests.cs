namespace GaChatbot.Api.Tests.Services;

using GA.Business.Core.Orchestration.Models;
using GA.Business.Core.Orchestration.Services;
using GaChatbot.Api.Services;
using Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Pins session identity on the REAL adapter path
/// (<see cref="ProductionChatOrchestratorClient.AnswerAsync"/>), not on the
/// pure mapping helper it happens to call today.
/// </summary>
/// <remarks>
/// <para>
/// <c>ToOrchestratorRequest_PreservesSessionIdentity</c>
/// (<c>ChatbotControllerSessionScopeTests</c>) guards the body of a
/// <c>public static</c> function. Nothing forced <c>AnswerAsync</c> to call it,
/// so re-inlining the original defect —
/// <c>new ChatRequest(request.Message, SessionId: null, History: request.History)</c>
/// — restored the exact pre-fix bug with the whole suite still green
/// (independent spec review 2026-08-08, mutant M1).
/// </para>
/// <para>
/// This test closes that gap by driving the real <c>AnswerAsync</c> against a
/// stub orchestrator and asserting on the <see cref="ChatRequest"/> the
/// orchestrator actually receives. It fails on M1 and on any future call path
/// that skips the mapping helper.
/// </para>
/// </remarks>
[TestFixture]
public class ProductionChatOrchestratorClientTests
{
    [Test]
    public async Task AnswerAsync_ForwardsSessionIdToTheOrchestrator()
    {
        var orchestrator = new CapturingOrchestrator();
        var client = new ProductionChatOrchestratorClient(ProviderFor(orchestrator));

        await client.AnswerAsync(
            new ChatExecutionRequest("Notes in C major?", History: null, SessionId: "session-abc"));

        Assert.That(orchestrator.Captured, Is.Not.Null,
            "The adapter must actually reach the orchestrator.");
        Assert.That(orchestrator.Captured!.SessionId, Is.EqualTo("session-abc"),
            "ProductionOrchestrator substitutes a fresh Guid for a null SessionId, so dropping " +
            "the session on this hop fails silently: every turn gets its own MemoryStore / " +
            "ChatTranscriptStore partition and the chatbot cannot recall its own conversation.");
    }

    [Test]
    public async Task AnswerAsync_CarriesMessageAndHistoryToTheOrchestrator()
    {
        List<ConversationTurn> history = [new("user", "Notes in C major?", DateTimeOffset.UnixEpoch)];
        var orchestrator = new CapturingOrchestrator();
        var client = new ProductionChatOrchestratorClient(ProviderFor(orchestrator));

        await client.AnswerAsync(
            new ChatExecutionRequest("And its relative minor?", history, SessionId: "session-abc"));

        Assert.That(orchestrator.Captured, Is.Not.Null);
        Assert.Multiple(() =>
        {
            Assert.That(orchestrator.Captured!.Message, Is.EqualTo("And its relative minor?"));
            Assert.That(orchestrator.Captured!.History, Is.EqualTo(history));
        });
    }

    private static IServiceProvider ProviderFor(ProductionOrchestrator orchestrator)
    {
        var services = new ServiceCollection();
        // The adapter resolves the concrete type, so the stub must be
        // registered under it — the same registration shape as
        // ChatbotOrchestrationExtensions.AddChatbotOrchestration.
        services.AddSingleton(orchestrator);
        return services.BuildServiceProvider();
    }

    /// <summary>
    /// Records the <see cref="ChatRequest"/> the adapter hands over. Only the
    /// two dependencies the primary constructor eagerly enumerates
    /// (<c>orchestratorSkills</c>, <c>chatHooks</c>) need real values; the
    /// overridden <c>AnswerAsync</c> touches none of the rest.
    /// </summary>
    private sealed class CapturingOrchestrator() : ProductionOrchestrator(
        null!, null!, null!, null!, null!, [], [], null!, null!, null!, null!, null!, null!, null!)
    {
        public ChatRequest? Captured { get; private set; }

        public override Task<ChatResponse> AnswerAsync(ChatRequest req, CancellationToken ct = default)
        {
            Captured = req;
            return Task.FromResult(new ChatResponse("stub answer", []));
        }
    }
}
