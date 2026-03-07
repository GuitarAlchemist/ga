namespace GaApi.Tests.Controllers;

using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;

/// <summary>
///     Integration tests for <see cref="GaApi.Controllers.ChatbotController" />.
///     Covers: GET /api/chatbot/status, GET /api/chatbot/examples,
///     and POST /api/chatbot/chat/stream (contract only — Ollama may be offline).
/// </summary>
[TestFixture]
[Category("Integration")]
public class ChatbotControllerTests
{
    [OneTimeSetUp]
    public void OneTimeSetUp()
    {
        _factory = new();
        _client  = _factory.CreateClient();
    }

    [OneTimeTearDown]
    public void OneTimeTearDown()
    {
        _client?.Dispose();
        _factory?.Dispose();
    }

    private WebApplicationFactory<Program>? _factory;
    private HttpClient?                     _client;

    // ── GET /api/chatbot/status ──────────────────────────────────────────────────

    [Test]
    public async Task GetStatus_ShouldReturn200()
    {
        var response = await _client!.GetAsync("/api/chatbot/status");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
    }

    [Test]
    public async Task GetStatus_ShouldReturnIsAvailableField()
    {
        var response = await _client!.GetAsync("/api/chatbot/status");
        var body     = await response.Content.ReadFromJsonAsync<JsonElement>();

        Assert.That(body.TryGetProperty("isAvailable", out var isAvailable), Is.True, "missing isAvailable");
        Assert.That(isAvailable.ValueKind, Is.EqualTo(JsonValueKind.True).Or.EqualTo(JsonValueKind.False));
    }

    [Test]
    public async Task GetStatus_ShouldReturnMessageField()
    {
        var response = await _client!.GetAsync("/api/chatbot/status");
        var body     = await response.Content.ReadFromJsonAsync<JsonElement>();

        Assert.That(body.TryGetProperty("message", out var message), Is.True, "missing message");
        Assert.That(message.GetString(), Is.Not.Null.And.Not.Empty);
    }

    [Test]
    public async Task GetStatus_ShouldReturnTimestampField()
    {
        var response = await _client!.GetAsync("/api/chatbot/status");
        var body     = await response.Content.ReadFromJsonAsync<JsonElement>();

        Assert.That(body.TryGetProperty("timestamp", out var timestamp), Is.True, "missing timestamp");
        Assert.That(DateTime.TryParse(timestamp.GetString(), out _), Is.True, "timestamp is not a valid datetime");
    }

    [Test]
    public async Task GetStatus_WhenOllamaIsUnavailable_ShouldStillReturn200WithFalse()
    {
        // Ollama may or may not be running in CI — but the endpoint must always succeed.
        var response = await _client!.GetAsync("/api/chatbot/status");
        var body     = await response.Content.ReadFromJsonAsync<JsonElement>();

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        // isAvailable must be a boolean — true if Ollama is up, false if it's not
        body.TryGetProperty("isAvailable", out var available);
        Assert.That(available.ValueKind, Is.AnyOf(JsonValueKind.True, JsonValueKind.False));
    }

    // ── GET /api/chatbot/examples ────────────────────────────────────────────────

    [Test]
    public async Task GetExamples_ShouldReturn200()
    {
        var response = await _client!.GetAsync("/api/chatbot/examples");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
    }

    [Test]
    public async Task GetExamples_ShouldReturnNonEmptyStringArray()
    {
        var response = await _client!.GetAsync("/api/chatbot/examples");
        var body     = await response.Content.ReadFromJsonAsync<JsonElement>();

        Assert.That(body.ValueKind, Is.EqualTo(JsonValueKind.Array));
        Assert.That(body.GetArrayLength(), Is.GreaterThan(0), "examples list must not be empty");
    }

    [Test]
    public async Task GetExamples_AllItemsShouldBeNonEmptyStrings()
    {
        var response = await _client!.GetAsync("/api/chatbot/examples");
        var body     = await response.Content.ReadFromJsonAsync<JsonElement>();

        foreach (var item in body.EnumerateArray())
        {
            Assert.That(item.ValueKind, Is.EqualTo(JsonValueKind.String));
            Assert.That(item.GetString(), Is.Not.Null.And.Not.Empty);
        }
    }

    [Test]
    public async Task GetExamples_ShouldContainGuitarRelatedContent()
    {
        var response  = await _client!.GetAsync("/api/chatbot/examples");
        var body      = await response.Content.ReadFromJsonAsync<JsonElement>();
        var examples  = body.EnumerateArray()
            .Select(e => e.GetString()!.ToLowerInvariant())
            .ToList();

        // At least one example should mention chords, scale, or guitar
        var hasMusicalContent = examples.Any(e =>
            e.Contains("chord") || e.Contains("scale") || e.Contains("guitar") || e.Contains("mode"));

        Assert.That(hasMusicalContent, Is.True, "examples should contain music-related queries");

        TestContext.WriteLine($"Examples ({body.GetArrayLength()}): {string.Join(", ", body.EnumerateArray().Select(e => $"\"{e.GetString()}\""))}");
    }

    // ── POST /api/chatbot/chat/stream ────────────────────────────────────────────

    [Test]
    public async Task ChatStream_ShouldReturn400_WhenMessageIsEmpty()
    {
        var request  = new { message = "" };
        var response = await _client!.PostAsJsonAsync("/api/chatbot/chat/stream", request);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
    }

    [Test]
    public async Task ChatStream_ShouldReturn400_WhenMessageIsWhitespace()
    {
        var request  = new { message = "   " };
        var response = await _client!.PostAsJsonAsync("/api/chatbot/chat/stream", request);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
    }

    [Test]
    public async Task ChatStream_WhenOllamaAvailable_ShouldReturnEventStream()
    {
        // Use ResponseHeadersRead so the client returns as soon as the server
        // commits the SSE headers (via Response.StartAsync) — without waiting
        // for the full stream to close, regardless of Ollama availability.
        var content    = JsonContent.Create(new { message = "What is a major chord?" });
        var requestMsg = new HttpRequestMessage(HttpMethod.Post, "/api/chatbot/chat/stream")
        {
            Content = content,
        };

        using var response = await _client!.SendAsync(requestMsg, HttpCompletionOption.ResponseHeadersRead);

        // SSE handshake always returns 200; errors are sent as SSE events.
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        var contentType = response.Content.Headers.ContentType?.MediaType;
        Assert.That(contentType, Is.EqualTo("text/event-stream"),
            "SSE endpoint must return text/event-stream content type");
    }

    [Test]
    public async Task ChatStream_ShouldReturn400_WhenBodyIsMissing()
    {
        var content  = new StringContent("{}", Encoding.UTF8, "application/json");
        var response = await _client!.PostAsync("/api/chatbot/chat/stream", content);

        // message is null/empty after deserialization — should 400
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
    }
}
