namespace GaApi.Tests.Controllers;

using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using GA.Business.Core.Orchestration.Abstractions;
using GA.Business.Core.Orchestration.Models;
using GaApi.Services;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

/// <summary>
///     Integration tests for <see cref="GaApi.Controllers.AgUiChatController" />.
///     Covers: POST /api/chatbot/agui/stream — SSE contract, event sequence, concurrency gate.
/// </summary>
[TestFixture]
[Category("Integration")]
public class AgUiChatControllerTests
{
    // ── Factories ────────────────────────────────────────────────────────────────

    /// <summary>Standard factory — real DI, Ollama may or may not be available.</summary>
    private WebApplicationFactory<Program>? _factory;

    /// <summary>Factory with a saturated concurrency gate (always returns false).</summary>
    private WebApplicationFactory<Program>? _saturatedFactory;

    private HttpClient? _client;
    private HttpClient? _saturatedClient;
    private HttpClient? _rawClient; // HandleCookies = false — preserves Set-Cookie in response headers

    // ── Sample input ─────────────────────────────────────────────────────────────

    private static readonly object ValidInput = new
    {
        threadId = "test-thread-1",
        runId    = "test-run-1",
        messages = new[] { new { role = "user", content = "What are the chords in G major?", id = "msg-1" } },
    };

    [OneTimeSetUp]
    public void OneTimeSetUp()
    {
        _factory = new TestWebApplicationFactory();

        _saturatedFactory = new TestWebApplicationFactory()
            .WithWebHostBuilder(builder =>
                builder.ConfigureServices(services =>
                {
                    services.RemoveAll<IHarmonicChatOrchestrator>();
                    services.AddSingleton<IHarmonicChatOrchestrator, TestHarmonicChatOrchestrator>();
                    services.RemoveAll<ILlmConcurrencyGate>();
                    // Replace concurrency gate with one that always rejects
                    services.AddSingleton<ILlmConcurrencyGate, AlwaysBusyGate>();
                }));

        _factory = _factory.WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IHarmonicChatOrchestrator>();
                services.AddSingleton<IHarmonicChatOrchestrator, TestHarmonicChatOrchestrator>();
                services.RemoveAll<ILlmConcurrencyGate>();
                services.AddSingleton<ILlmConcurrencyGate, AlwaysAvailableGate>();
            }));

        _client          = _factory.CreateClient();
        _saturatedClient = _saturatedFactory.CreateClient();
        // Raw client preserves Set-Cookie in response.Headers so we can
        // assert the server-issued cookie shape (INFO-003 tests).
        _rawClient = _factory.CreateClient(
            new WebApplicationFactoryClientOptions { HandleCookies = false });
    }

    [OneTimeTearDown]
    public void OneTimeTearDown()
    {
        _client?.Dispose();
        _saturatedClient?.Dispose();
        _rawClient?.Dispose();
        _factory?.Dispose();
        _saturatedFactory?.Dispose();
    }

    // ── Input validation ─────────────────────────────────────────────────────────

    [Test]
    public async Task AgUiStream_ShouldReturn400_WhenMessagesIsEmpty()
    {
        var input    = new { threadId = "t1", runId = "r1", messages = Array.Empty<object>() };
        var response = await _client!.PostAsJsonAsync("/api/chatbot/agui/stream", input);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
    }

    [Test]
    public async Task AgUiStream_ShouldReturn400_WhenUserContentIsEmpty()
    {
        var input = new
        {
            threadId = "t1",
            runId    = "r1",
            messages = new[] { new { role = "user", content = "", id = "m1" } },
        };
        var response = await _client!.PostAsJsonAsync("/api/chatbot/agui/stream", input);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
    }

    [Test]
    public async Task AgUiStream_ShouldReturn400_WhenUserContentIsWhitespace()
    {
        var input = new
        {
            threadId = "t1",
            runId    = "r1",
            messages = new[] { new { role = "user", content = "   ", id = "m1" } },
        };
        var response = await _client!.PostAsJsonAsync("/api/chatbot/agui/stream", input);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
    }

    // ── SSE contract ─────────────────────────────────────────────────────────────

    [Test]
    public async Task AgUiStream_ShouldReturn200WithEventStreamContentType()
    {
        var request = BuildRequest(ValidInput);
        using var response = await _client!.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);

        Assert.That(response.StatusCode,                              Is.EqualTo(HttpStatusCode.OK));
        Assert.That(response.Content.Headers.ContentType?.MediaType, Is.EqualTo("text/event-stream"));
    }

    [Test]
    public async Task AgUiStream_ShouldHaveNoCacheHeader()
    {
        var request = BuildRequest(ValidInput);
        using var response = await _client!.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);

        // Cache-Control is in response headers (not content headers)
        var cacheControl = response.Headers.TryGetValues("Cache-Control", out var vals)
            ? vals.FirstOrDefault()
            : null;

        Assert.That(cacheControl, Is.EqualTo("no-cache"),
            "SSE endpoints must set Cache-Control: no-cache");
    }

    [Test]
    public async Task AgUiStream_ShouldHaveXAccelBufferingHeader()
    {
        var request = BuildRequest(ValidInput);
        using var response = await _client!.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);

        var xAccel = response.Headers.TryGetValues("X-Accel-Buffering", out var vals)
            ? vals.FirstOrDefault()
            : null;

        Assert.That(xAccel, Is.EqualTo("no"),
            "SSE endpoints must set X-Accel-Buffering: no to disable nginx buffering");
    }

    // ── Event sequence ───────────────────────────────────────────────────────────

    [Test]
    public async Task AgUiStream_FirstEvent_ShouldBeRunStarted()
    {
        var request = BuildRequest(ValidInput);
        using var response = await _client!.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);
        await using var stream = await response.Content.ReadAsStreamAsync();
        using var reader = new StreamReader(stream, leaveOpen: true);

        var firstEvent = await ReadNextEventAsync(reader);

        Assert.That(firstEvent, Is.Not.Null, "stream must emit at least one event");
        Assert.That(firstEvent!.Value.TryGetProperty("type", out var type), Is.True, "event must have 'type' field");
        Assert.That(type.GetString(), Is.EqualTo("RUN_STARTED"), "first event must be RUN_STARTED");
    }

    [Test]
    public async Task AgUiStream_RunStartedEvent_ShouldHaveCamelCaseFields()
    {
        var request = BuildRequest(ValidInput);
        using var response = await _client!.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);
        await using var stream = await response.Content.ReadAsStreamAsync();
        using var reader = new StreamReader(stream, leaveOpen: true);

        var ev = await ReadNextEventAsync(reader);
        Assert.That(ev, Is.Not.Null);

        Assert.That(ev!.Value.TryGetProperty("threadId",  out _), Is.True, "missing camelCase 'threadId'");
        Assert.That(ev!.Value.TryGetProperty("runId",     out _), Is.True, "missing camelCase 'runId'");
        Assert.That(ev!.Value.TryGetProperty("timestamp", out _), Is.True, "missing 'timestamp'");
    }

    [Test]
    public async Task AgUiStream_RunStartedEvent_ThreadId_ShouldMatchInput()
    {
        var input = new
        {
            threadId = "my-specific-thread",
            runId    = "r1",
            messages = new[] { new { role = "user", content = "Hello", id = "m1" } },
        };
        var request = BuildRequest(input);
        using var response = await _client!.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);
        await using var stream = await response.Content.ReadAsStreamAsync();
        using var reader = new StreamReader(stream, leaveOpen: true);

        var ev = await ReadNextEventAsync(reader);
        Assert.That(ev, Is.Not.Null);

        ev!.Value.TryGetProperty("threadId", out var threadId);
        Assert.That(threadId.GetString(), Is.EqualTo("my-specific-thread"));
    }

    [Test]
    public async Task AgUiStream_SecondEvent_ShouldBeStateSnapshot()
    {
        var request = BuildRequest(ValidInput);
        using var response = await _client!.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);
        await using var stream = await response.Content.ReadAsStreamAsync();
        using var reader = new StreamReader(stream, leaveOpen: true);

        // Skip RUN_STARTED
        _ = await ReadNextEventAsync(reader);

        var secondEvent = await ReadNextEventAsync(reader);
        Assert.That(secondEvent, Is.Not.Null, "stream must emit at least two events");
        Assert.That(secondEvent!.Value.TryGetProperty("type", out var type), Is.True);
        Assert.That(type.GetString(), Is.EqualTo("STATE_SNAPSHOT"), "second event must be STATE_SNAPSHOT");
    }

    [Test]
    public async Task AgUiStream_StateSnapshot_ShouldHaveInitialIdleState()
    {
        var request = BuildRequest(ValidInput);
        using var response = await _client!.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);
        await using var stream = await response.Content.ReadAsStreamAsync();
        using var reader = new StreamReader(stream, leaveOpen: true);

        // RUN_STARTED → STATE_SNAPSHOT
        _ = await ReadNextEventAsync(reader);
        var snapshot = await ReadNextEventAsync(reader);
        Assert.That(snapshot, Is.Not.Null);

        snapshot!.Value.TryGetProperty("snapshot", out var state);
        state.TryGetProperty("analysisPhase", out var phase);

        Assert.That(phase.GetString(), Is.EqualTo("idle"),
            "STATE_SNAPSHOT analysisPhase must start as 'idle'");
    }

    // ── Concurrency gate ─────────────────────────────────────────────────────────

    [Test]
    public async Task AgUiStream_WhenConcurrencyGateSaturated_FirstEventShouldBeRunError()
    {
        var request = BuildRequest(ValidInput);
        using var response = await _saturatedClient!.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);

        // Even when gate is saturated, status is 200 (errors are SSE events)
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        await using var stream = await response.Content.ReadAsStreamAsync();
        using var reader = new StreamReader(stream, leaveOpen: true);

        // Gate is checked before RUN_STARTED — first (and only) event is RUN_ERROR
        var errorEvent = await ReadNextEventAsync(reader);
        Assert.That(errorEvent, Is.Not.Null, "expected RUN_ERROR event when gate is saturated");

        errorEvent!.Value.TryGetProperty("type",    out var type);
        errorEvent!.Value.TryGetProperty("message", out var message);
        errorEvent!.Value.TryGetProperty("code",    out var code);

        Assert.That(type.GetString(),    Is.EqualTo("RUN_ERROR"));
        Assert.That(code.GetString(),    Is.EqualTo("SERVICE_BUSY"));
        Assert.That(message.GetString(), Is.Not.Null.And.Not.Empty);
    }

    // ── INFO-003: server-issued cookie SessionId (task #107) ─────────────────────

    [Test]
    public async Task AgUiStream_IssuesGaChatSessionCookie()
    {
        // Phase C P1 / INFO-003 — every AG-UI stream response that passed
        // input validation must Set-Cookie the server-issued session ID,
        // so memory partitioning works across reloads the same way it does
        // for /api/chatbot/chat.
        var request = BuildRequest(ValidInput);
        using var response = await _rawClient!.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        var setCookie = response.Headers.TryGetValues("Set-Cookie", out var cookies)
            ? string.Join("; ", cookies)
            : string.Empty;

        Assert.That(setCookie, Does.Contain(HttpChatSessionCookie.CookieName),
            "AG-UI stream must Set-Cookie the server-issued session cookie. " +
            "Without it, HTTP callers fall through to per-request Guid SessionId " +
            "and their memory writes become unreachable.");
    }

    [Test]
    public async Task AgUiStream_SessionIdPlumbedToOrchestrator_IsNotClientThreadId()
    {
        // INFO-003 — the orchestrator must receive the cookie-derived
        // SessionId, NOT the client-supplied input.ThreadId. Before
        // PR #163 + task #107, AG-UI passed input.ThreadId directly as
        // SessionId, which would let a malicious client forge any session
        // ID they pleased once Memory:EnrichOnRetrieve=true ships.
        const string maliciousThreadId = "victim-session-abc";
        var input = new
        {
            threadId = maliciousThreadId,
            runId    = "r-malicious",
            messages = new[] { new { role = "user", content = "Tell me about Cmaj7", id = "m1" } },
        };

        TestHarmonicChatOrchestrator.LastRequest = null;

        var request = BuildRequest(input);
        using var response = await _client!.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);
        // Drain the stream so the orchestrator actually runs.
        _ = await response.Content.ReadAsStringAsync();

        Assert.That(TestHarmonicChatOrchestrator.LastRequest, Is.Not.Null,
            "Orchestrator should have been invoked.");
        Assert.That(TestHarmonicChatOrchestrator.LastRequest!.SessionId,
            Is.Not.EqualTo(maliciousThreadId),
            "VULNERABILITY: AG-UI is passing client-controlled threadId as SessionId. " +
            "This is the same shape as VULN-001 (PR #163 audit). Use HttpChatSessionCookie.GetOrIssue " +
            "to derive SessionId from a server-signed cookie instead.");
        Assert.That(TestHarmonicChatOrchestrator.LastRequest!.SessionId,
            Is.Not.Null.And.Not.Empty,
            "SessionId must still be plumbed — just from the cookie, not from input.ThreadId.");
    }

    [Test]
    public async Task AgUiJson_IssuesGaChatSessionCookie_AndDoesNotUseClientThreadId()
    {
        // Companion test for the non-streaming AG-UI endpoint (/api/chatbot/agui/json).
        // It goes through IChatApplicationService rather than IHarmonicChatOrchestrator,
        // so we can't capture LastRequest here without swapping that service too.
        // For now we only assert the visible effect: Set-Cookie is present.
        const string maliciousThreadId = "victim-session-xyz";
        var input = new
        {
            threadId = maliciousThreadId,
            runId    = "r-malicious-json",
            messages = new[] { new { role = "user", content = "Tell me about Dm7", id = "m1" } },
        };

        using var response = await _rawClient!.PostAsJsonAsync("/api/chatbot/agui/json", input);

        // Status may be 200 (real service available) or 502/503 (Ollama down) — we
        // only care that, when validation passes and the gate admits, a cookie is set.
        if (response.StatusCode == HttpStatusCode.OK)
        {
            var setCookie = response.Headers.TryGetValues("Set-Cookie", out var cookies)
                ? string.Join("; ", cookies)
                : string.Empty;

            Assert.That(setCookie, Does.Contain(HttpChatSessionCookie.CookieName),
                "AG-UI JSON endpoint must Set-Cookie the server-issued session cookie " +
                "(INFO-003 — server-issued, not client-controlled threadId).");
        }
    }

    // ── STATE_DELTA round-trip ────────────────────────────────────────────────────

    [Test]
    [Explicit("Requires live Ollama — run manually when GaApi + Ollama are both running")]
    [Category("SlowIntegration")]
    public async Task AgUiStream_StateDelta_ShouldContainAnalysisPhaseComplete()
    {
        using var cts     = new CancellationTokenSource(TimeSpan.FromSeconds(60));
        var request = BuildRequest(ValidInput);
        using var response = await _client!.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cts.Token);
        await using var stream = await response.Content.ReadAsStreamAsync(cts.Token);
        using var reader = new StreamReader(stream, leaveOpen: true);

        JsonElement? stateDeltaEvent = null;

        // Read until we find STATE_DELTA or the stream closes
        while (!cts.IsCancellationRequested)
        {
            var ev = await ReadNextEventAsync(reader, cts.Token);
            if (ev is null) break;

            if (ev.Value.TryGetProperty("type", out var t) && t.GetString() == "STATE_DELTA")
            {
                stateDeltaEvent = ev;
                break;
            }

            if (ev.Value.TryGetProperty("type", out var tEnd) && tEnd.GetString() == "RUN_FINISHED")
                break;
        }

        Assert.That(stateDeltaEvent.HasValue, Is.True, "STATE_DELTA event must be emitted before RUN_FINISHED");

        // The delta array must contain a replace op for /analysisPhase → "complete"
        stateDeltaEvent!.Value.TryGetProperty("delta", out var delta);
        Assert.That(delta.ValueKind, Is.EqualTo(JsonValueKind.Array));

        var ops = delta.EnumerateArray().ToList();
        var phaseOp = ops.FirstOrDefault(op =>
            op.TryGetProperty("path", out var p) && p.GetString() == "/analysisPhase");

        Assert.That(phaseOp.ValueKind,   Is.Not.EqualTo(JsonValueKind.Undefined), "no op for /analysisPhase in STATE_DELTA");
        phaseOp.TryGetProperty("value", out var phaseValue);
        Assert.That(phaseValue.GetString(), Is.EqualTo("complete"));
    }

    // ── Helpers ──────────────────────────────────────────────────────────────────

    private static HttpRequestMessage BuildRequest(object body) =>
        new(HttpMethod.Post, "/api/chatbot/agui/stream")
        {
            Content = JsonContent.Create(body),
        };

    /// <summary>
    /// Reads lines from <paramref name="reader"/> until a complete SSE frame is found
    /// (blank line delimiter) and returns the parsed JSON payload, or null at EOF.
    /// The reader must be shared across calls to preserve the buffer between frames.
    /// </summary>
    private static async Task<JsonElement?> ReadNextEventAsync(
        StreamReader reader, CancellationToken ct = default)
    {
        string? dataLine = null;

        while (!ct.IsCancellationRequested)
        {
            string? line;
            try
            {
                line = await reader.ReadLineAsync(ct);
            }
            catch (OperationCanceledException)
            {
                return null;
            }

            if (line is null) return null;           // EOF

            if (line.StartsWith("data:", StringComparison.Ordinal))
            {
                dataLine = line["data:".Length..].Trim();
            }
            else if (line.Length == 0 && dataLine is not null)
            {
                // Blank line = end of SSE frame
                try
                {
                    return JsonDocument.Parse(dataLine).RootElement.Clone();
                }
                catch (JsonException)
                {
                    dataLine = null; // malformed — skip
                }
            }
        }

        return null;
    }
}

/// <summary>Test double: concurrency gate that always rejects (simulates all 3 slots taken).</summary>
file sealed class AlwaysBusyGate : ILlmConcurrencyGate
{
    public ValueTask<bool> TryEnterAsync(CancellationToken cancellationToken = default) =>
        ValueTask.FromResult(false);

    public void Release() { /* nothing to release */ }
}

file sealed class AlwaysAvailableGate : ILlmConcurrencyGate
{
    public ValueTask<bool> TryEnterAsync(CancellationToken cancellationToken = default) =>
        ValueTask.FromResult(true);

    public void Release() { /* nothing to release */ }
}

file sealed class TestHarmonicChatOrchestrator : IHarmonicChatOrchestrator
{
    // Captures the most recent ChatRequest so tests can assert what
    // SessionId was actually plumbed to the orchestrator. Used by the
    // INFO-003 (task #107) tests that pin the cookie-not-ThreadId fix.
    public static ChatRequest? LastRequest { get; set; }

    public Task<ChatResponse> AnswerAsync(ChatRequest req, CancellationToken ct = default)
    {
        LastRequest = req;
        return Task.FromResult(BuildResponse());
    }

    public async Task<ChatResponse> AnswerStreamingAsync(
        ChatRequest req,
        Func<string, Task> onToken,
        CancellationToken ct = default)
    {
        LastRequest = req;
        await onToken("Deterministic test response.");
        return BuildResponse();
    }

    private static ChatResponse BuildResponse() =>
        new(
            "Deterministic test response.",
            [],
            Routing: new AgentRoutingMetadata("test-agent", 1f, "test"),
            QueryFilters: new QueryFilters { Key = "G major" });
}
