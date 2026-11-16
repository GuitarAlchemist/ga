namespace GaApi.Tests.Controllers;

using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using GA.Business.Core.Orchestration.Abstractions;
using GA.Business.Core.Orchestration.Models;
using GA.Core.Functional;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

/// <summary>
///     The A2A JSON-RPC contract ported from GaChatbot.Api (ADR-0005): discovery card,
///     <c>message/send</c>, <c>message/stream</c>, and JSON-RPC errors, at the root and under
///     the public <c>/chatbot/</c> mount. Runs against a recording chat intake, no model.
/// </summary>
[TestFixture]
[Category("Integration")]
public class A2AControllerTests
{
    [TestCase("", "http://localhost/a2a")]
    [TestCase("/chatbot", "http://localhost/chatbot/a2a")]
    public async Task AgentCard_AdvertisesTheEndpointUnderTheMountItWasFetchedFrom(string mount, string expectedUrl)
    {
        using var factory = CreateFactory(new RecordingIntake());
        using var client = factory.CreateClient();

        var body = await client.GetFromJsonAsync<JsonElement>($"{mount}/.well-known/agent-card.json");

        Assert.Multiple(() =>
        {
            Assert.That(body.GetProperty("name").GetString(), Is.EqualTo("Guitar Alchemist Chatbot"));
            Assert.That(body.GetProperty("url").GetString(), Is.EqualTo(expectedUrl));
            Assert.That(body.GetProperty("preferredTransport").GetString(), Is.EqualTo("JSONRPC"));
            Assert.That(body.GetProperty("capabilities").GetProperty("streaming").GetBoolean(), Is.True);
            Assert.That(body.GetProperty("skills")[0].GetProperty("id").GetString(), Is.EqualTo("music-theory-chat"));
        });
    }

    [Test]
    public async Task WhenDisabled_DiscoveryAndRpcAreNotFound()
    {
        using var factory = CreateFactory(new RecordingIntake(), builder => builder.UseSetting("A2A:Enabled", "false"));
        using var client = factory.CreateClient();

        using var card = await client.GetAsync("/.well-known/agent-card.json");
        using var rpc = await client.PostAsJsonAsync("/a2a", Rpc("message/send", TextMessage("Explain C major.")));

        Assert.Multiple(() =>
        {
            Assert.That(card.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
            Assert.That(rpc.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
        });
    }

    [TestCase("/a2a")]
    [TestCase("/chatbot/a2a")]
    public async Task MessageSend_ReturnsAnAgentMessage_KeyedToTheContextsSession(string endpoint)
    {
        var intake = new RecordingIntake();
        using var factory = CreateFactory(intake);
        using var client = factory.CreateClient();

        using var response = await client.PostAsJsonAsync(endpoint, Rpc("message/send", TextMessage("Explain C major.", "ctx-existing")));
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        var result = body.GetProperty("result");

        Assert.Multiple(() =>
        {
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            Assert.That(intake.Requests.Single().Message, Is.EqualTo("Explain C major."));
            Assert.That(intake.Requests.Single().SessionId, Is.EqualTo("a2a:ctx-existing"),
                "the orchestrator keeps the context's history under this session id");
            Assert.That(body.GetProperty("id").GetString(), Is.EqualTo("req-1"));
            Assert.That(result.GetProperty("kind").GetString(), Is.EqualTo("message"));
            Assert.That(result.GetProperty("role").GetString(), Is.EqualTo("agent"));
            Assert.That(result.GetProperty("contextId").GetString(), Is.EqualTo("ctx-existing"));
            Assert.That(result.GetProperty("parts")[0].GetProperty("text").GetString(), Is.EqualTo(RecordingIntake.Answer));
            Assert.That(result.GetProperty("metadata").GetProperty("agentId").GetString(), Is.EqualTo("fake-agent"));
            Assert.That(result.GetProperty("metadata").GetProperty("routingMethod").GetString(), Is.EqualTo("fake-route"));
            Assert.That(body.TryGetProperty("error", out _), Is.False);
        });
    }

    [Test]
    public async Task MessageSend_DifferentContexts_UseDifferentSessions_AndANewContextGetsAnId()
    {
        var intake = new RecordingIntake();
        using var factory = CreateFactory(intake);
        using var client = factory.CreateClient();

        await PostAsync(client, "message/send", TextMessage("Explain C major.", "ctx-one"));
        await PostAsync(client, "message/send", TextMessage("Continue that.", "ctx-two"));
        var fresh = await PostAsync(client, "message/send", TextMessage("Explain voice leading."));

        Assert.Multiple(() =>
        {
            Assert.That(intake.Requests.Select(r => r.SessionId).Take(2), Is.EqualTo(new[] { "a2a:ctx-one", "a2a:ctx-two" }));
            var contextId = fresh.GetProperty("result").GetProperty("contextId").GetString();
            Assert.That(contextId, Does.StartWith("ctx_"));
            Assert.That(intake.Requests[2].SessionId, Is.EqualTo("a2a:" + contextId));
        });
    }

    [Test]
    public async Task MessageSend_WhenBusy_ReturnsJsonRpcBusyError()
    {
        using var factory = CreateFactory(new RecordingIntake(busy: true));
        using var client = factory.CreateClient();

        var body = await PostAsync(client, "message/send", TextMessage("Explain C major."));

        Assert.Multiple(() =>
        {
            Assert.That(body.GetProperty("error").GetProperty("code").GetInt32(), Is.EqualTo(-32000));
            Assert.That(body.GetProperty("error").GetProperty("message").GetString(), Does.Contain("busy"));
            Assert.That(body.TryGetProperty("result", out _), Is.False);
        });
    }

    [TestCase("/a2a")]
    [TestCase("/chatbot/a2a")]
    public async Task MessageStream_EmitsTaskArtifactAndFinalStatusEvents(string endpoint)
    {
        var intake = new RecordingIntake();
        using var factory = CreateFactory(intake);
        using var client = factory.CreateClient();

        using var response = await client.PostAsJsonAsync(endpoint, Rpc("message/stream", TextMessage("Explain C major.", "ctx-stream")));
        var events = ReadSseData(await response.Content.ReadAsStringAsync());

        Assert.That(response.Content.Headers.ContentType?.MediaType, Is.EqualTo("text/event-stream"));
        Assert.That(events, Has.Count.EqualTo(3));
        var task = JsonSerializer.Deserialize<JsonElement>(events[0]).GetProperty("result");
        var artifact = JsonSerializer.Deserialize<JsonElement>(events[1]).GetProperty("result");
        var status = JsonSerializer.Deserialize<JsonElement>(events[2]).GetProperty("result");
        Assert.Multiple(() =>
        {
            Assert.That(intake.Requests.Single().SessionId, Is.EqualTo("a2a:ctx-stream"));
            Assert.That(task.GetProperty("kind").GetString(), Is.EqualTo("task"));
            Assert.That(task.GetProperty("contextId").GetString(), Is.EqualTo("ctx-stream"));
            Assert.That(task.GetProperty("status").GetProperty("state").GetString(), Is.EqualTo("working"));
            Assert.That(artifact.GetProperty("kind").GetString(), Is.EqualTo("artifact-update"));
            Assert.That(artifact.GetProperty("artifact").GetProperty("parts")[0].GetProperty("text").GetString(), Is.EqualTo(RecordingIntake.Answer));
            Assert.That(artifact.GetProperty("append").GetBoolean(), Is.False);
            Assert.That(status.GetProperty("kind").GetString(), Is.EqualTo("status-update"));
            Assert.That(status.GetProperty("status").GetProperty("state").GetString(), Is.EqualTo("completed"));
            Assert.That(status.GetProperty("final").GetBoolean(), Is.True);
        });
    }

    [Test]
    public async Task MessageStream_WhenBusy_EmitsOnlyAJsonRpcBusyError()
    {
        using var factory = CreateFactory(new RecordingIntake(busy: true));
        using var client = factory.CreateClient();

        using var response = await client.PostAsJsonAsync("/a2a", Rpc("message/stream", TextMessage("Explain C major.")));
        var events = ReadSseData(await response.Content.ReadAsStringAsync());

        Assert.That(events, Has.Count.EqualTo(1));
        var body = JsonSerializer.Deserialize<JsonElement>(events[0]);
        Assert.Multiple(() =>
        {
            Assert.That(body.GetProperty("error").GetProperty("code").GetInt32(), Is.EqualTo(-32000));
            Assert.That(body.GetProperty("error").GetProperty("message").GetString(), Does.Contain("busy"));
        });
    }

    [Test]
    public async Task UnsupportedMethod_ReturnsJsonRpcMethodNotFound()
    {
        using var factory = CreateFactory(new RecordingIntake());
        using var client = factory.CreateClient();

        var body = await PostAsync(client, "tasks/get", new { id = "task-1" });

        Assert.Multiple(() =>
        {
            Assert.That(body.GetProperty("error").GetProperty("code").GetInt32(), Is.EqualTo(-32601));
            Assert.That(body.GetProperty("error").GetProperty("message").GetString(), Does.Contain("tasks/get"));
        });
    }

    [Test]
    public async Task MessageWithoutText_ReturnsJsonRpcInvalidParams()
    {
        var intake = new RecordingIntake();
        using var factory = CreateFactory(intake);
        using var client = factory.CreateClient();

        var body = await PostAsync(client, "message/send", new { message = new { role = "user", parts = Array.Empty<object>() } });

        Assert.Multiple(() =>
        {
            Assert.That(body.GetProperty("error").GetProperty("code").GetInt32(), Is.EqualTo(-32602));
            Assert.That(body.GetProperty("error").GetProperty("message").GetString(), Does.Contain("params.message.parts"));
            Assert.That(intake.Requests, Is.Empty);
        });
    }

    // GaChatbot.Api serves A2A without a key; outside Development GaApi gates only /api/chatbot/*.
    [TestCase("/chatbot")]
    [TestCase("")]
    public async Task InProduction_CardSendAndStream_PassWithoutAKey(string mount)
    {
        var intake = new RecordingIntake();
        using var factory = CreateFactory(intake, builder => builder.UseEnvironment("Production"));
        using var client = factory.CreateClient();

        using var card = await client.GetAsync($"{mount}/.well-known/agent-card.json");
        using var send = await client.PostAsJsonAsync($"{mount}/a2a", Rpc("message/send", TextMessage("Explain C major.", "ctx-prod")));
        using var stream = await client.PostAsJsonAsync($"{mount}/a2a", Rpc("message/stream", TextMessage("Explain C major.", "ctx-prod")));
        var sent = await send.Content.ReadFromJsonAsync<JsonElement>();
        var streamed = ReadSseData(await stream.Content.ReadAsStringAsync());

        Assert.Multiple(() =>
        {
            Assert.That(card.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            Assert.That(send.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            Assert.That(sent.GetProperty("result").GetProperty("parts")[0].GetProperty("text").GetString(), Is.EqualTo(RecordingIntake.Answer));
            Assert.That(stream.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            Assert.That(streamed, Has.Count.EqualTo(3));
        });
    }

    private static WebApplicationFactory<Program> CreateFactory(RecordingIntake intake, Action<IWebHostBuilder>? configure = null) =>
        new TestWebApplicationFactory().WithWebHostBuilder(builder =>
        {
            configure?.Invoke(builder);
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IChatIntake>();
                services.AddSingleton<IChatIntake>(intake);
            });
        });

    private static object TextMessage(string text, string? contextId = null) => new
    {
        message = new { role = "user", contextId, parts = new[] { new { kind = "text", text } } }
    };

    private static object Rpc(string method, object parameters) =>
        new { jsonrpc = "2.0", id = "req-1", method, @params = parameters };

    private static async Task<JsonElement> PostAsync(HttpClient client, string method, object parameters)
    {
        using var response = await client.PostAsJsonAsync("/a2a", Rpc(method, parameters));
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        return await response.Content.ReadFromJsonAsync<JsonElement>();
    }

    private static List<string> ReadSseData(string body) =>
        [.. body.Replace("\r", string.Empty)
            .Split('\n')
            .Where(line => line.StartsWith("data: ", StringComparison.Ordinal))
            .Select(line => line[6..])];

    private sealed class RecordingIntake(bool busy = false) : IChatIntake
    {
        public const string Answer = "fake answer";
        private readonly Lock _gate = new();
        private readonly List<ChatIntakeRequest> _requests = [];

        public IReadOnlyList<ChatIntakeRequest> Requests
        {
            get { lock (_gate) return [.. _requests]; }
        }

        public Task<Result<ChatResponse, ChatIntakeError>> IntakeAsync(
            ChatIntakeRequest request, CancellationToken cancellationToken = default) =>
            Task.FromResult(Record(request));

        public async Task<Result<ChatResponse, ChatIntakeError>> IntakeStreamingAsync(
            ChatIntakeRequest request, Func<string, Task> onToken, CancellationToken cancellationToken = default)
        {
            var result = Record(request);
            if (result.IsSuccess) await onToken(Answer);
            return result;
        }

        private Result<ChatResponse, ChatIntakeError> Record(ChatIntakeRequest request)
        {
            lock (_gate) _requests.Add(request);
            return busy
                ? Result<ChatResponse, ChatIntakeError>.Failure(new ChatIntakeError.Busy())
                : Result<ChatResponse, ChatIntakeError>.Success(
                    new ChatResponse(Answer, [], Routing: new AgentRoutingMetadata("fake-agent", 0.9f, "fake-route")));
        }
    }
}
