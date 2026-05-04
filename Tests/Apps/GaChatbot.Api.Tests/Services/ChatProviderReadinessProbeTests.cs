namespace GaChatbot.Api.Tests.Services;

using System.Net;
using System.Text;
using GaChatbot.Api.Services;
using Microsoft.Extensions.Configuration;

[TestFixture]
public class ChatProviderReadinessProbeTests
{
    [Test]
    public async Task GetStatusAsync_UnsupportedProvider_ReturnsUnavailable()
    {
        var probe = CreateProbe(new Dictionary<string, string?>
        {
            ["AI:ChatProvider"] = "unknown-provider"
        });

        var status = await probe.GetStatusAsync();

        Assert.Multiple(() =>
        {
            Assert.That(status.IsAvailable, Is.False);
            Assert.That(status.Message, Is.EqualTo("Unsupported chat provider 'unknown-provider'."));
            Assert.That(status.Provider, Is.EqualTo("unknown-provider"));
        });
    }

    [Test]
    public async Task GetStatusAsync_OllamaNonSuccess_ReturnsUnavailableWithStatusCode()
    {
        var probe = CreateProbe(
            new Dictionary<string, string?>
            {
                ["AI:ChatProvider"] = "ollama"
            },
            new HttpResponseMessage(HttpStatusCode.ServiceUnavailable));

        var status = await probe.GetStatusAsync();

        Assert.Multiple(() =>
        {
            Assert.That(status.IsAvailable, Is.False);
            Assert.That(status.ProviderReachable, Is.False);
            Assert.That(status.Message, Does.Contain("503"));
            Assert.That(status.Message, Does.Contain("/api/tags"));
            Assert.That(status.Provider, Is.EqualTo("ollama"));
        });
    }

    [Test]
    public async Task GetStatusAsync_OllamaRequestFailure_ReturnsUnavailableWithReason()
    {
        var probe = CreateProbe(
            new Dictionary<string, string?>
            {
                ["AI:ChatProvider"] = "ollama"
            },
            exception: new HttpRequestException("connection refused"));

        var status = await probe.GetStatusAsync();

        Assert.Multiple(() =>
        {
            Assert.That(status.IsAvailable, Is.False);
            Assert.That(status.ProviderReachable, Is.False);
            Assert.That(status.Message, Does.StartWith("Ollama is not reachable:"));
            Assert.That(status.Message, Does.Contain("connection refused"));
        });
    }

    // ── New tests for the deeper readiness probe (PR #96) ─────────────────────

    [Test]
    public async Task GetStatusAsync_OllamaModelsAllInstalled_ReturnsAvailable()
    {
        // Healthy case: /api/tags returns 200 with both chat AND embedding
        // models present → IsAvailable=true. Pre-PR-#96 behaviour did NOT
        // verify model presence, so this test pins the new contract.
        var tagsBody = """
        {"models":[
            {"name":"llama3.2:3b","size":1234},
            {"name":"nomic-embed-text:latest","size":5678}
        ]}
        """;
        var probe = CreateProbe(
            new Dictionary<string, string?>
            {
                ["AI:ChatProvider"]        = "ollama",
                ["Ollama:ChatModel"]       = "llama3.2:3b",
                ["Ollama:EmbeddingModel"]  = "nomic-embed-text",
            },
            JsonOk(tagsBody));

        var status = await probe.GetStatusAsync();

        Assert.Multiple(() =>
        {
            Assert.That(status.IsAvailable, Is.True);
            Assert.That(status.ProviderReachable, Is.True);
            Assert.That(status.ChatModelInstalled, Is.True);
            Assert.That(status.EmbeddingModelInstalled, Is.True);
            Assert.That(status.Message, Does.Contain("ready"));
        });
    }

    [Test]
    public async Task GetStatusAsync_OllamaChatModelMissing_ReturnsUnavailable()
    {
        // The exact failure mode the user reported: Ollama HTTP-reachable
        // (was reporting healthy), but the configured chat model is NOT
        // installed → real chat request would have failed at first call.
        var tagsBody = """
        {"models":[
            {"name":"nomic-embed-text:latest","size":5678}
        ]}
        """;
        var probe = CreateProbe(
            new Dictionary<string, string?>
            {
                ["AI:ChatProvider"]        = "ollama",
                ["Ollama:ChatModel"]       = "llama3.2:3b",
                ["Ollama:EmbeddingModel"]  = "nomic-embed-text",
            },
            JsonOk(tagsBody));

        var status = await probe.GetStatusAsync();

        Assert.Multiple(() =>
        {
            Assert.That(status.IsAvailable, Is.False,
                "missing chat model means we cannot serve a request — must report unavailable");
            Assert.That(status.ProviderReachable, Is.True,
                "Ollama HTTP is up; the new probe distinguishes provider-reachable from chatbot-ready");
            Assert.That(status.ChatModelInstalled, Is.False);
            Assert.That(status.EmbeddingModelInstalled, Is.True);
            Assert.That(status.Message, Does.Contain("llama3.2:3b").And.Contain("missing"));
        });
    }

    [Test]
    public async Task GetStatusAsync_OllamaEmbeddingModelMissing_ReturnsUnavailable()
    {
        var tagsBody = """{"models":[{"name":"llama3.2:3b","size":1234}]}""";
        var probe = CreateProbe(
            new Dictionary<string, string?>
            {
                ["AI:ChatProvider"]        = "ollama",
                ["Ollama:ChatModel"]       = "llama3.2:3b",
                ["Ollama:EmbeddingModel"]  = "nomic-embed-text",
            },
            JsonOk(tagsBody));

        var status = await probe.GetStatusAsync();

        Assert.Multiple(() =>
        {
            Assert.That(status.IsAvailable, Is.False);
            Assert.That(status.ChatModelInstalled, Is.True);
            Assert.That(status.EmbeddingModelInstalled, Is.False);
            Assert.That(status.Message, Does.Contain("nomic-embed-text").And.Contain("missing"));
        });
    }

    [Test]
    public async Task GetStatusAsync_OllamaBothModelsMissing_ListsBothInMessage()
    {
        var tagsBody = """{"models":[{"name":"some-other-model","size":99}]}""";
        var probe = CreateProbe(
            new Dictionary<string, string?>
            {
                ["AI:ChatProvider"]        = "ollama",
                ["Ollama:ChatModel"]       = "llama3.2:3b",
                ["Ollama:EmbeddingModel"]  = "nomic-embed-text",
            },
            JsonOk(tagsBody));

        var status = await probe.GetStatusAsync();

        Assert.Multiple(() =>
        {
            Assert.That(status.IsAvailable, Is.False);
            Assert.That(status.Message, Does.Contain("llama3.2:3b"));
            Assert.That(status.Message, Does.Contain("nomic-embed-text"));
            Assert.That(status.Message, Does.Contain("some-other-model"),
                "the message should list installed models so the operator knows what IS available");
        });
    }

    [Test]
    public async Task GetStatusAsync_OllamaMalformedTagsBody_ReportsAsModelsMissing()
    {
        // Robustness: if /api/tags returns 200 with garbage body, treat as
        // "no models found" rather than crashing the probe.
        var probe = CreateProbe(
            new Dictionary<string, string?>
            {
                ["AI:ChatProvider"]        = "ollama",
                ["Ollama:ChatModel"]       = "llama3.2:3b",
                ["Ollama:EmbeddingModel"]  = "nomic-embed-text",
            },
            JsonOk("not valid json {"));

        var status = await probe.GetStatusAsync();

        Assert.That(status.IsAvailable, Is.False);
        Assert.That(status.ProviderReachable, Is.True);
    }

    [Test]
    public async Task GetStatusAsync_OllamaNoModelsConfigured_ReturnsAvailableOnReachable()
    {
        // Back-compat: when no specific models are configured (chat/embedding
        // both null), fall back to the old "reachable = available" semantics.
        var probe = CreateProbe(
            new Dictionary<string, string?>
            {
                ["AI:ChatProvider"] = "ollama",
                // ChatModel + EmbeddingModel intentionally not set
            },
            JsonOk("""{"models":[]}"""));

        var status = await probe.GetStatusAsync();

        Assert.That(status.IsAvailable, Is.True,
            "no models configured = no concrete check to fail; reachable counts as available");
        Assert.That(status.Message, Does.Contain("no specific models"));
    }

    [Test]
    public async Task GetStatusAsync_OllamaBaseNameMatch_ResolvesToInstalledTag()
    {
        // Operator pattern: configure base name "llama3.2", accept whichever
        // tagged version Ollama happens to have. Probe should match by base
        // name in either direction (config base → installed tag, OR config
        // tag → installed base).
        var tagsBody = """
        {"models":[
            {"name":"llama3.2:3b","size":1234},
            {"name":"nomic-embed-text:latest","size":5678}
        ]}
        """;
        var probe = CreateProbe(
            new Dictionary<string, string?>
            {
                ["AI:ChatProvider"]        = "ollama",
                ["Ollama:ChatModel"]       = "llama3.2",      // base name, no tag
                ["Ollama:EmbeddingModel"]  = "nomic-embed-text",
            },
            JsonOk(tagsBody));

        var status = await probe.GetStatusAsync();

        Assert.That(status.IsAvailable, Is.True,
            "configured 'llama3.2' should match installed 'llama3.2:3b' via base-name match");
        Assert.That(status.ChatModelInstalled, Is.True);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static HttpResponseMessage JsonOk(string body) =>
        new(HttpStatusCode.OK)
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json"),
        };

    private static ChatProviderReadinessProbe CreateProbe(
        IReadOnlyDictionary<string, string?> values,
        HttpResponseMessage? response = null,
        Exception? exception = null)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(values)
            .Build();

        var httpClient = new HttpClient(new StubHandler(response, exception))
        {
            BaseAddress = new Uri("http://localhost:11434")
        };

        return new ChatProviderReadinessProbe(configuration, new StubHttpClientFactory(httpClient));
    }

    private sealed class StubHttpClientFactory(HttpClient client) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => client;
    }

    private sealed class StubHandler(HttpResponseMessage? response, Exception? exception) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            if (exception is not null)
            {
                throw exception;
            }

            return Task.FromResult(response ?? new HttpResponseMessage(HttpStatusCode.OK));
        }
    }
}
