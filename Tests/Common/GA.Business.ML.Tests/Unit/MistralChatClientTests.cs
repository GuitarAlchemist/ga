namespace GA.Business.ML.Tests.Unit;

using System.Net;
using System.Text;
using GA.Business.ML.Providers.Mistral;
using Microsoft.Extensions.AI;

[TestFixture]
public class MistralChatClientTests
{
    [Test]
    public async Task GetResponseAsync_ParsesOpenAiCompatibleBody()
    {
        const string jsonBody = """
        {
            "id":"chatcmpl-abc",
            "model":"mistral-medium-latest",
            "choices":[
                {"index":0,"message":{"role":"assistant","content":"Hello from Mistral"},"finish_reason":"stop"}
            ],
            "usage":{"prompt_tokens":5,"completion_tokens":7,"total_tokens":12}
        }
        """;

        var handler = new RecordingHandler((req, _) =>
        {
            Assert.That(req.RequestUri!.AbsoluteUri, Is.EqualTo("https://api.mistral.ai/v1/chat/completions"));
            Assert.That(req.Headers.Authorization?.Scheme,    Is.EqualTo("Bearer"));
            Assert.That(req.Headers.Authorization?.Parameter, Is.EqualTo("test-key"));
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(jsonBody, Encoding.UTF8, "application/json"),
            };
        });

        using var http = new HttpClient(handler);
        using var client = new MistralChatClient("test-key", "mistral-medium-latest", new Uri("https://api.mistral.ai"), http);

        var response = await client.GetResponseAsync([new ChatMessage(ChatRole.User, "hi")]);

        Assert.That(response.Messages[0].Text,          Is.EqualTo("Hello from Mistral"));
        Assert.That(response.ModelId,                   Is.EqualTo("mistral-medium-latest"));
        Assert.That(response.FinishReason,              Is.EqualTo(ChatFinishReason.Stop));
        Assert.That(response.Usage?.TotalTokenCount,    Is.EqualTo(12));
    }

    [Test]
    public void GetResponseAsync_NonSuccessStatus_ThrowsWithoutEchoingApiKey()
    {
        var handler = new RecordingHandler((_, _) =>
            new HttpResponseMessage(HttpStatusCode.Unauthorized)
            {
                Content = new StringContent("""{"message":"Invalid API key"}""", Encoding.UTF8, "application/json"),
            });

        using var http = new HttpClient(handler);
        using var client = new MistralChatClient("sk-secret-key-do-not-leak", "mistral-medium-latest", new Uri("https://api.mistral.ai"), http);

        var ex = Assert.ThrowsAsync<HttpRequestException>(async () =>
            await client.GetResponseAsync([new ChatMessage(ChatRole.User, "hi")]));

        Assert.That(ex!.Message, Does.Not.Contain("sk-secret-key-do-not-leak"),
            "exception message must never echo the API key from the request headers");
        Assert.That(ex.Message, Does.Contain("401").Or.Contain("Unauthorized"));
    }

    [Test]
    public void GetService_ReturnsMetadataForChatClientMetadata()
    {
        using var client = new MistralChatClient("test-key", "mistral-medium-latest", new Uri("https://api.mistral.ai"));

        var metadata = client.GetService(typeof(ChatClientMetadata));

        Assert.That(metadata, Is.InstanceOf<ChatClientMetadata>());
        var m = (ChatClientMetadata)metadata!;
        Assert.That(m.ProviderName, Is.EqualTo("mistral"));
        Assert.That(m.DefaultModelId, Is.EqualTo("mistral-medium-latest"));
    }

    [TestCase("https://api.mistral.ai",      "https://api.mistral.ai/v1/chat/completions")]
    [TestCase("https://api.mistral.ai/",     "https://api.mistral.ai/v1/chat/completions")]
    [TestCase("https://api.mistral.ai/v1",   "https://api.mistral.ai/v1/chat/completions")]
    [TestCase("https://api.mistral.ai/v1/",  "https://api.mistral.ai/v1/chat/completions")]
    [TestCase("https://api.mistral.ai/V1/",  "https://api.mistral.ai/V1/chat/completions")]  // case-insensitive v1 detection
    [TestCase("https://proxy.example/openai/v1/", "https://proxy.example/openai/v1/chat/completions")]
    public async Task GetResponseAsync_NormalizesVersionedBaseUrl(string baseUrl, string expectedAbsoluteUri)
    {
        // Codex P2 review on PR #225: operators commonly set Mistral:BaseUrl to
        // either the bare host or the already-versioned `/v1/` form. The
        // hand-rolled client must produce the same endpoint either way.
        Uri? observed = null;
        var handler = new RecordingHandler((req, _) =>
        {
            observed = req.RequestUri;
            return new HttpResponseMessage(System.Net.HttpStatusCode.OK)
            {
                Content = new StringContent("""{"choices":[{"message":{"role":"assistant","content":"ok"},"finish_reason":"stop"}]}""", Encoding.UTF8, "application/json"),
            };
        });

        using var http = new HttpClient(handler);
        using var client = new MistralChatClient("test-key", "mistral-medium-latest", new Uri(baseUrl), http);

        await client.GetResponseAsync([new ChatMessage(ChatRole.User, "hi")]);

        Assert.That(observed!.AbsoluteUri, Is.EqualTo(expectedAbsoluteUri));
    }

    [Test]
    public void Constructor_NullArgs_Throws()
    {
        Assert.That(() => new MistralChatClient(null!, "model",  new Uri("https://x")), Throws.InstanceOf<ArgumentNullException>().Or.InstanceOf<ArgumentException>());
        Assert.That(() => new MistralChatClient("k",  null!,     new Uri("https://x")), Throws.InstanceOf<ArgumentNullException>().Or.InstanceOf<ArgumentException>());
        Assert.That(() => new MistralChatClient("k",  "m",       null!),                Throws.InstanceOf<ArgumentNullException>());
    }

    private sealed class RecordingHandler(Func<HttpRequestMessage, CancellationToken, HttpResponseMessage> respond) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(respond(request, cancellationToken));
    }
}
