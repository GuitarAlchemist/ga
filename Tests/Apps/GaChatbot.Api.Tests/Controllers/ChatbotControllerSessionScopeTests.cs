namespace GaChatbot.Api.Tests.Controllers;

using System.Net.Http.Json;
using GA.Business.Core.Orchestration.Models;
using GA.Business.Core.Orchestration.Trace;
using GaChatbot.Api.Services;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

/// <summary>
/// End-to-end contract for chatbot session scoping on the canonical public
/// host: the transport must resolve a stable per-conversation identity and
/// hand it to the application service, which is what ultimately scopes
/// <c>MemoryStore</c> and <c>ChatTranscriptStore</c> to one conversation.
/// </summary>
/// <remarks>
/// <para>
/// Before this slice, <c>ChatExecutionRequest</c> had no session dimension
/// and <c>ProductionChatOrchestratorClient</c> passed
/// <c>SessionId: null</c> to the orchestrator unconditionally, so
/// <c>ProductionOrchestrator</c> minted a throwaway <c>Guid</c> per request.
/// Every turn — including consecutive turns from the same guitarist — landed
/// in its own memory partition, and <c>Memory:EnrichOnRetrieve</c> could
/// never surface anything on this host.
/// </para>
/// <para>
/// These tests assert the behaviour a user experiences (same browser =&gt; same
/// session, different browsers =&gt; different sessions), not the cookie
/// mechanics — those are pinned by <c>HttpChatSessionCookieTests</c>.
/// </para>
/// </remarks>
[TestFixture]
public class ChatbotControllerSessionScopeTests
{
    [Test]
    public async Task Chat_IssuesSessionAndPassesItToTheApplicationService()
    {
        var chatService = new CapturingChatApplicationService();
        using var factory = CreateFactory(chatService);
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/chatbot/chat", new { message = "What key is Am F C G in?" });
        response.EnsureSuccessStatusCode();

        Assert.That(chatService.Requests, Has.Count.EqualTo(1));
        Assert.That(chatService.Requests[0].SessionId, Is.Not.Null.And.Not.Empty,
            "Without a SessionId the orchestrator mints a throwaway Guid, so nothing the " +
            "guitarist says is reachable from their next turn.");
    }

    [Test]
    public async Task Chat_SameBrowserAcrossTurns_KeepsOneSession()
    {
        var chatService = new CapturingChatApplicationService();
        using var factory = CreateFactory(chatService);
        // HandleCookies (the default) makes the client behave like a browser:
        // it stores the Set-Cookie from turn 1 and replays it on turn 2.
        using var client = factory.CreateClient();

        (await client.PostAsJsonAsync("/api/chatbot/chat", new { message = "Notes in C major?" }))
            .EnsureSuccessStatusCode();
        (await client.PostAsJsonAsync("/api/chatbot/chat", new { message = "And its relative minor?" }))
            .EnsureSuccessStatusCode();

        Assert.That(chatService.Requests, Has.Count.EqualTo(2));
        Assert.Multiple(() =>
        {
            // Assert non-null FIRST: "both turns agree" is vacuously true when
            // both are null, which is precisely the broken state this slice
            // fixes. Without this guard the test passes on the bug.
            Assert.That(chatService.Requests[0].SessionId, Is.Not.Null.And.Not.Empty);
            Assert.That(chatService.Requests[1].SessionId, Is.EqualTo(chatService.Requests[0].SessionId),
                "Consecutive turns from one browser must share a session — that continuity is " +
                "what lets the chatbot recall the conversation it is already having.");
        });
    }

    [Test]
    public async Task Chat_DifferentBrowsers_GetDifferentSessions()
    {
        var chatService = new CapturingChatApplicationService();
        using var factory = CreateFactory(chatService);
        using var browserA = factory.CreateClient();
        using var browserB = factory.CreateClient();

        (await browserA.PostAsJsonAsync("/api/chatbot/chat", new { message = "Notes in C major?" }))
            .EnsureSuccessStatusCode();
        (await browserB.PostAsJsonAsync("/api/chatbot/chat", new { message = "Notes in C major?" }))
            .EnsureSuccessStatusCode();

        Assert.That(chatService.Requests, Has.Count.EqualTo(2));
        Assert.That(chatService.Requests[1].SessionId, Is.Not.EqualTo(chatService.Requests[0].SessionId),
            "Two unrelated visitors to the public demo must not share a memory partition.");
    }

    [Test]
    public async Task ChatStream_IssuesSessionBeforeCommittingTheSseResponse()
    {
        var chatService = new CapturingChatApplicationService();
        using var factory = CreateFactory(chatService);
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/chatbot/chat/stream", new { message = "Notes in C major?" });
        response.EnsureSuccessStatusCode();
        await response.Content.ReadAsStringAsync();

        Assert.That(chatService.Requests, Has.Count.EqualTo(1));
        Assert.Multiple(() =>
        {
            Assert.That(chatService.Requests[0].SessionId, Is.Not.Null.And.Not.Empty);
            // Appending a cookie after Response.StartAsync throws because the
            // headers are already on the wire; the SSE path must resolve the
            // session first. A Set-Cookie here proves it did.
            Assert.That(response.Headers.TryGetValues("Set-Cookie", out _), Is.True,
                "The streaming endpoint must issue its session cookie before committing headers.");
        });
    }

    [Test]
    public void ToOrchestratorRequest_PreservesSessionIdentity()
    {
        // The adapter hop is where session identity was being dropped. It fails
        // silently — ProductionOrchestrator substitutes a fresh Guid for a null
        // SessionId — so pin it directly.
        var mapped = ProductionChatOrchestratorClient.ToOrchestratorRequest(
            new ChatExecutionRequest("Notes in C major?", History: null, SessionId: "session-abc"));

        Assert.That(mapped.SessionId, Is.EqualTo("session-abc"));
    }

    [Test]
    public void ToOrchestratorRequest_CarriesMessageAndHistoryThrough()
    {
        List<ConversationTurn> history = [new("user", "Notes in C major?", DateTimeOffset.UnixEpoch)];

        var mapped = ProductionChatOrchestratorClient.ToOrchestratorRequest(
            new ChatExecutionRequest("And its relative minor?", history, SessionId: "session-abc"));

        Assert.Multiple(() =>
        {
            Assert.That(mapped.Message, Is.EqualTo("And its relative minor?"));
            Assert.That(mapped.History, Is.EqualTo(history));
        });
    }

    private static WebApplicationFactory<Program> CreateFactory(IChatApplicationService chatService) =>
        new TestWebApplicationFactory()
            .WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<IChatApplicationService>();
                services.AddSingleton(chatService);
            }));

    private sealed class CapturingChatApplicationService : IChatApplicationService
    {
        private readonly List<ChatExecutionRequest> _requests = [];
        private readonly Lock _gate = new();

        public IReadOnlyList<ChatExecutionRequest> Requests
        {
            get { lock (_gate) return [.. _requests]; }
        }

        public Task<ChatExecutionResult> ChatAsync(
            ChatExecutionRequest request,
            CancellationToken cancellationToken = default)
        {
            Capture(request);
            return Task.FromResult(new ChatExecutionResult(
                "captured answer",
                new AgentRoutingMetadata("fake-agent", 0.75f, "fake-route")));
        }

        public async IAsyncEnumerable<ChatStreamUpdate> ChatStreamAsync(
            ChatExecutionRequest request,
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            Capture(request);
            yield return new ChatStreamUpdate(Routing: new AgentRoutingMetadata("fake-agent", 0.75f, "fake-route"));
            yield return new ChatStreamUpdate("captured answer");
            await Task.Yield();
            yield return new ChatStreamUpdate(IsCompleted: true);
        }

        public Task<GaChatbot.Api.Controllers.ChatbotStatus> GetStatusAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(new GaChatbot.Api.Controllers.ChatbotStatus
            {
                IsAvailable = true,
                Message = "fake ready",
                Timestamp = DateTime.UtcNow
            });

        private void Capture(ChatExecutionRequest request)
        {
            lock (_gate) _requests.Add(request);
        }
    }
}
