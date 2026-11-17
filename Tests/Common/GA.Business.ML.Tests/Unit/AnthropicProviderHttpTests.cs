namespace GA.Business.ML.Tests.Unit;

using System.Net;
using System.Text;
using System.Text.Json;
using GA.Providers.Anthropic;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;

/// <summary>
/// Wire-level behaviour of the Anthropic chat client, checked against canned
/// Messages API responses: the workspace header, the output-token floor, the
/// default model, and that thinking blocks never reach the caller. Measured on
/// the real API (2026-11-17): the key is not workspace-scoped, so every request
/// needs <c>anthropic-workspace-id</c>; Haiku 5.5 emits a thinking block first,
/// and at max_tokens=300 the thinking used the whole budget and the text was empty.
/// </summary>
[TestFixture]
public class AnthropicProviderHttpTests
{
    private const string AnthropicEnv = "ANTHROPIC_API_KEY";
    private string? _savedEnv;

    [SetUp]
    public void ClearAmbientKey()
    {
        _savedEnv = Environment.GetEnvironmentVariable(AnthropicEnv);
        Environment.SetEnvironmentVariable(AnthropicEnv, null);
    }

    [TearDown]
    public void RestoreAmbientKey() => Environment.SetEnvironmentVariable(AnthropicEnv, _savedEnv);

    private static IConfiguration Config(params (string Key, string? Value)[] extra)
    {
        var values = new Dictionary<string, string?> { ["Anthropic:ApiKey"] = "test-key" };
        foreach (var (key, value) in extra)
        {
            values[key] = value;
        }

        return new ConfigurationBuilder().AddInMemoryCollection(values).Build();
    }

    private const string ThinkingThenTextResponse = """
        {"id":"msg_01","type":"message","role":"assistant","model":"claude-haiku-5-5",
         "content":[
           {"type":"thinking","thinking":"SECRET-REASONING","signature":"sig"},
           {"type":"text","text":"A C major chord is C, E and G."}
         ],
         "stop_reason":"end_turn","stop_sequence":null,
         "usage":{"input_tokens":12,"output_tokens":40}}
        """;

    [TestCase("claude-haiku-5-5", "claude-haiku-5-5")]   // the deploy sets Anthropic__Model
    [TestCase(null, "claude-sonnet-5-5")]                 // unset: AnthropicProvider.DefaultModel
    public async Task Request_UsesConfiguredModel(string? configured, string expected)
    {
        var handler = new RecordingHandler(ThinkingThenTextResponse);
        var client = AnthropicProvider.CreateChatClientFromConfig(Config(("Anthropic:Model", configured)), handler);

        await client.GetResponseAsync("What is in C major?");

        Assert.That(handler.Body.RootElement.GetProperty("model").GetString(), Is.EqualTo(expected));
    }

    [Test]
    public async Task Request_CarriesWorkspaceHeader_WhenConfigured()
    {
        var handler = new RecordingHandler(ThinkingThenTextResponse);
        var client = AnthropicProvider.CreateChatClientFromConfig(
            Config(("Anthropic:WorkspaceId", "wrkspc_test")), handler);

        await client.GetResponseAsync("hi");

        Assert.Multiple(() =>
        {
            Assert.That(handler.Header("anthropic-workspace-id"), Is.EqualTo("wrkspc_test"));
            Assert.That(handler.Header("x-api-key"), Is.EqualTo("test-key"));
        });
    }

    [Test]
    public async Task SkillMdPath_ExplicitModelAndTimeout_CarriesWorkspaceHeaderAndFloor()
    {
        // DefaultChatClientFactory.Create("skill-md") goes through CreateChatClient
        // with an explicit model; that path must send the same header and floor.
        var handler = new RecordingHandler(ThinkingThenTextResponse);
        var client = AnthropicProvider.CreateChatClient(
            Config(("Anthropic:WorkspaceId", "wrkspc_test")),
            "claude-haiku-5-5",
            TimeSpan.FromMinutes(5),
            handler);

        var response = await client.GetResponseAsync("Transpose C-E-G up a tone", new ChatOptions { MaxOutputTokens = 300 });

        Assert.Multiple(() =>
        {
            Assert.That(handler.Header("anthropic-workspace-id"), Is.EqualTo("wrkspc_test"));
            Assert.That(handler.Body.RootElement.GetProperty("model").GetString(), Is.EqualTo("claude-haiku-5-5"));
            Assert.That(handler.Body.RootElement.GetProperty("max_tokens").GetInt32(), Is.EqualTo(2048));
            Assert.That(response.Text, Is.EqualTo("A C major chord is C, E and G."));
        });
    }

    [Test]
    public async Task Request_OmitsWorkspaceHeader_WhenNotConfigured()
    {
        var handler = new RecordingHandler(ThinkingThenTextResponse);
        var client = AnthropicProvider.CreateChatClientFromConfig(Config(), handler);

        await client.GetResponseAsync("hi");

        Assert.That(handler.Header("anthropic-workspace-id"), Is.Null);
    }

    [TestCase(null, null, 2048)]      // no caller cap: the default floor
    [TestCase(512, null, 2048)]       // a small caller cap is raised: thinking shares max_tokens
    [TestCase(8000, null, 8000)]      // a larger caller cap is kept
    [TestCase(null, "4096", 4096)]    // the floor is configurable
    public async Task Request_MaxTokens_HasFloor(int? callerCap, string? configuredFloor, int expected)
    {
        var handler = new RecordingHandler(ThinkingThenTextResponse);
        var client = AnthropicProvider.CreateChatClientFromConfig(
            Config(("Anthropic:MaxOutputTokens", configuredFloor)), handler);

        await client.GetResponseAsync("hi", new ChatOptions { MaxOutputTokens = callerCap });

        Assert.That(handler.Body.RootElement.GetProperty("max_tokens").GetInt32(), Is.EqualTo(expected));
    }

    [Test]
    public async Task Response_SurfacesOnlyTextBlocks()
    {
        var client = AnthropicProvider.CreateChatClientFromConfig(Config(), new RecordingHandler(ThinkingThenTextResponse));

        var response = await client.GetResponseAsync("What is in C major?");

        var contents = response.Messages.SelectMany(m => m.Contents).ToList();
        Assert.Multiple(() =>
        {
            Assert.That(response.Text, Is.EqualTo("A C major chord is C, E and G."));
            Assert.That(contents.OfType<TextReasoningContent>(), Is.Empty);
            Assert.That(contents.OfType<TextContent>().Select(c => c.Text), Has.None.Contains("SECRET-REASONING"));
        });
    }

    [Test]
    public async Task Streaming_SurfacesOnlyTextDeltas()
    {
        var sse = string.Join("\n\n",
            Event("message_start", """{"type":"message_start","message":{"id":"msg_01","type":"message","role":"assistant","model":"claude-haiku-5-5","content":[],"stop_reason":null,"stop_sequence":null,"usage":{"input_tokens":12,"output_tokens":1}}}"""),
            Event("content_block_start", """{"type":"content_block_start","index":0,"content_block":{"type":"thinking","thinking":"","signature":""}}"""),
            Event("content_block_delta", """{"type":"content_block_delta","index":0,"delta":{"type":"thinking_delta","thinking":"SECRET-REASONING"}}"""),
            Event("content_block_delta", """{"type":"content_block_delta","index":0,"delta":{"type":"signature_delta","signature":"sig"}}"""),
            Event("content_block_stop", """{"type":"content_block_stop","index":0}"""),
            Event("content_block_start", """{"type":"content_block_start","index":1,"content_block":{"type":"text","text":""}}"""),
            Event("content_block_delta", """{"type":"content_block_delta","index":1,"delta":{"type":"text_delta","text":"C, E "}}"""),
            Event("content_block_delta", """{"type":"content_block_delta","index":1,"delta":{"type":"text_delta","text":"and G."}}"""),
            Event("content_block_stop", """{"type":"content_block_stop","index":1}"""),
            Event("message_delta", """{"type":"message_delta","delta":{"stop_reason":"end_turn","stop_sequence":null},"usage":{"output_tokens":40}}"""),
            Event("message_stop", """{"type":"message_stop"}""")) + "\n\n";
        var client = AnthropicProvider.CreateChatClientFromConfig(
            Config(), new RecordingHandler(sse, "text/event-stream"));

        var text = new StringBuilder();
        var reasoning = 0;
        await foreach (var update in client.GetStreamingResponseAsync("hi"))
        {
            text.Append(update.Text);
            reasoning += update.Contents.OfType<TextReasoningContent>().Count();
        }

        Assert.Multiple(() =>
        {
            Assert.That(text.ToString(), Is.EqualTo("C, E and G."));
            Assert.That(reasoning, Is.Zero);
        });
    }

    [Test]
    public void ModelsRequest_IsGetWithAuthAndWorkspaceHeaders()
    {
        using var request = AnthropicProvider.CreateModelsRequest(Config(("Anthropic:WorkspaceId", "wrkspc_test")));

        Assert.Multiple(() =>
        {
            Assert.That(request.Method, Is.EqualTo(HttpMethod.Get));
            Assert.That(request.RequestUri!.AbsoluteUri, Does.StartWith("https://api.anthropic.com/v1/models"));
            Assert.That(request.Headers.GetValues("x-api-key"), Is.EqualTo(new[] { "test-key" }));
            Assert.That(request.Headers.Contains("anthropic-version"), Is.True);
            Assert.That(request.Headers.GetValues("anthropic-workspace-id"), Is.EqualTo(new[] { "wrkspc_test" }));
        });
    }

    [Test]
    public void ModelsRequest_NoKey_Throws() =>
        Assert.Throws<InvalidOperationException>(() =>
            AnthropicProvider.CreateModelsRequest(new ConfigurationBuilder().Build()));

    private static string Event(string name, string data) => $"event: {name}\ndata: {data}";

    private sealed class RecordingHandler(string responseBody, string mediaType = "application/json") : HttpMessageHandler
    {
        private HttpRequestMessage? _request;
        private string? _body;

        public JsonDocument Body => JsonDocument.Parse(_body ?? throw new InvalidOperationException("no request sent"));

        public string? Header(string name) =>
            _request is not null && _request.Headers.TryGetValues(name, out var values) ? values.Single() : null;

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            _request = request;
            _body = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken);
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(responseBody, Encoding.UTF8, mediaType),
            };
        }
    }
}
