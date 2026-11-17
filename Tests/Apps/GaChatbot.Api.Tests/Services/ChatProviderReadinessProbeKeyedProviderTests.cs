namespace GaChatbot.Api.Tests.Services;

using System.Net;
using System.Text;
using GA.Business.ML.Agents.Intents;
using GaChatbot.Api.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

/// <summary>
/// Readiness for the hosted chat providers (Anthropic, Mistral). Before these
/// arms existed, <c>AI:ChatProvider=anthropic|mistral</c> reported "Unsupported
/// chat provider" and the readiness gate blocked every LLM answer. Anthropic is
/// probed with <c>GET /v1/models</c> (no inference cost) using the same headers
/// as chat; Mistral is key-only.
/// </summary>
[TestFixture]
public class ChatProviderReadinessProbeKeyedProviderTests
{
    private const string AnthropicEnv = "ANTHROPIC_API_KEY";
    private const string MistralEnv = "MISTRAL_API_KEY";

    private const string ModelsBody = """
        {"data":[
          {"type":"model","id":"claude-haiku-5-5","display_name":"Claude Haiku 5.5"},
          {"type":"model","id":"claude-sonnet-5-5","display_name":"Claude Sonnet 5.5"}
        ],"has_more":false}
        """;

    private string? _savedAnthropic;
    private string? _savedMistral;

    [SetUp]
    public void ClearAmbientKeys()
    {
        _savedAnthropic = Environment.GetEnvironmentVariable(AnthropicEnv);
        _savedMistral = Environment.GetEnvironmentVariable(MistralEnv);
        Environment.SetEnvironmentVariable(AnthropicEnv, null);
        Environment.SetEnvironmentVariable(MistralEnv, null);
    }

    [TearDown]
    public void RestoreAmbientKeys()
    {
        Environment.SetEnvironmentVariable(AnthropicEnv, _savedAnthropic);
        Environment.SetEnvironmentVariable(MistralEnv, _savedMistral);
    }

    [TestCase("anthropic")]
    [TestCase("claude")]
    [TestCase("Anthropic")]
    public async Task Anthropic_ModelsEndpointListsModel_IsAvailable(string provider)
    {
        var http = new StubHandler(HttpStatusCode.OK, ModelsBody);
        var probe = CreateProbe(new Dictionary<string, string?>
        {
            ["AI:ChatProvider"] = provider,
            ["Anthropic:ApiKey"] = "test-key",
            ["Anthropic:Model"] = "claude-sonnet-5-5",
        }, http);

        var status = await probe.GetStatusAsync();

        Assert.Multiple(() =>
        {
            Assert.That(status.IsAvailable, Is.True, status.Message);
            Assert.That(status.ProviderReachable, Is.True);
            Assert.That(status.ChatModelInstalled, Is.True);
            Assert.That(status.Provider, Is.EqualTo(provider.ToLowerInvariant()));
            Assert.That(status.Message, Does.StartWith("Anthropic is configured."));
            Assert.That(status.ChatModel, Is.EqualTo("claude-sonnet-5-5"));
            Assert.That(status.OrchestratorRoundTripOk, Is.True);
            Assert.That(http.Requests, Has.Count.EqualTo(1));
            Assert.That(http.Requests[0].Method, Is.EqualTo(HttpMethod.Get));
            Assert.That(http.Requests[0].RequestUri!.AbsolutePath, Is.EqualTo("/v1/models"));
        });
    }

    [Test]
    public async Task Anthropic_ProbeSendsKeyAndWorkspaceHeaders()
    {
        Environment.SetEnvironmentVariable(AnthropicEnv, "env-key");
        var http = new StubHandler(HttpStatusCode.OK, ModelsBody);
        var probe = CreateProbe(new Dictionary<string, string?>
        {
            ["AI:ChatProvider"] = "anthropic",
            ["Anthropic:WorkspaceId"] = "wrkspc_test",
        }, http);

        var status = await probe.GetStatusAsync();

        Assert.Multiple(() =>
        {
            Assert.That(status.IsAvailable, Is.True, status.Message);
            Assert.That(status.ChatModel, Is.EqualTo("claude-sonnet-5-5"));
            Assert.That(http.Requests[0].Headers.GetValues("x-api-key"), Is.EqualTo(new[] { "env-key" }));
            Assert.That(http.Requests[0].Headers.GetValues("anthropic-workspace-id"), Is.EqualTo(new[] { "wrkspc_test" }));
        });
    }

    [Test]
    public async Task Anthropic_ModelsEndpointRejects_IsUnavailable_WithApiMessage()
    {
        var http = new StubHandler(
            HttpStatusCode.BadRequest,
            """{"type":"error","error":{"type":"invalid_request_error","message":"This API key is not scoped to a workspace"}}""");
        var probe = CreateProbe(new Dictionary<string, string?>
        {
            ["AI:ChatProvider"] = "anthropic",
            ["Anthropic:ApiKey"] = "test-key",
        }, http);

        var status = await probe.GetStatusAsync();

        Assert.Multiple(() =>
        {
            Assert.That(status.IsAvailable, Is.False);
            Assert.That(status.ProviderReachable, Is.False);
            Assert.That(status.Message, Does.Contain("400"));
            Assert.That(status.Message, Does.Contain("not scoped to a workspace"));
            Assert.That(status.Message, Does.Not.Contain("test-key"));
        });
    }

    [Test]
    public async Task Anthropic_ConfiguredModelNotListed_IsUnavailable()
    {
        var probe = CreateProbe(new Dictionary<string, string?>
        {
            ["AI:ChatProvider"] = "anthropic",
            ["Anthropic:ApiKey"] = "test-key",
            ["Anthropic:Model"] = "claude-nonexistent",
        }, new StubHandler(HttpStatusCode.OK, ModelsBody));

        var status = await probe.GetStatusAsync();

        Assert.Multiple(() =>
        {
            Assert.That(status.IsAvailable, Is.False);
            Assert.That(status.ProviderReachable, Is.True);
            Assert.That(status.ChatModelInstalled, Is.False);
            Assert.That(status.Message, Does.Contain("claude-nonexistent"));
        });
    }

    [TestCase("claude-opus-5-5", "claude-opus-5-5-20261001", true)]   // dated snapshot of the alias
    [TestCase("claude-haiku-5", "claude-haiku-5-5", false)]           // a different model, not a snapshot
    public async Task Anthropic_ModelMatching(string configured, string listed, bool expected)
    {
        var probe = CreateProbe(new Dictionary<string, string?>
        {
            ["AI:ChatProvider"] = "anthropic",
            ["Anthropic:ApiKey"] = "test-key",
            ["Anthropic:Model"] = configured,
        }, new StubHandler(HttpStatusCode.OK, $$"""{"data":[{"type":"model","id":"{{listed}}"}],"has_more":false}"""));

        var status = await probe.GetStatusAsync();

        Assert.That(status.ChatModelInstalled, Is.EqualTo(expected));
    }

    [Test]
    public async Task Anthropic_Unreachable_IsUnavailable()
    {
        var probe = CreateProbe(new Dictionary<string, string?>
        {
            ["AI:ChatProvider"] = "anthropic",
            ["Anthropic:ApiKey"] = "test-key",
        }, new StubHandler(new HttpRequestException("connection refused")));

        var status = await probe.GetStatusAsync();

        Assert.Multiple(() =>
        {
            Assert.That(status.IsAvailable, Is.False);
            Assert.That(status.ProviderReachable, Is.False);
            Assert.That(status.Message, Does.StartWith("Anthropic is not reachable:"));
            Assert.That(status.Message, Does.Contain("connection refused"));
        });
    }

    [Test]
    public async Task Anthropic_NoKey_IsUnavailable_WithoutHttpCall()
    {
        var http = new StubHandler(HttpStatusCode.OK, ModelsBody);
        var probe = CreateProbe(new Dictionary<string, string?> { ["AI:ChatProvider"] = "anthropic" }, http);

        var status = await probe.GetStatusAsync();

        Assert.Multiple(() =>
        {
            Assert.That(status.IsAvailable, Is.False);
            Assert.That(status.ProviderReachable, Is.False);
            Assert.That(status.Message, Does.StartWith("Anthropic is not configured."));
            Assert.That(status.Message, Does.Contain(AnthropicEnv));
            Assert.That(status.Message, Does.Contain("Anthropic:ApiKey"));
            Assert.That(http.Requests, Is.Empty);
        });
    }

    [Test]
    public async Task Anthropic_Healthy_ButOrchestratorWedged_IsUnavailable()
    {
        // The provider arm only answers "can the provider serve"; the round-trip
        // probe still gates IsAvailable exactly as for the other providers.
        var probe = CreateProbe(
            new Dictionary<string, string?>
            {
                ["AI:ChatProvider"] = "anthropic",
                ["Anthropic:ApiKey"] = "test-key",
            },
            new StubHandler(HttpStatusCode.OK, ModelsBody),
            intents: []);

        var status = await probe.GetStatusAsync();

        Assert.Multiple(() =>
        {
            Assert.That(status.IsAvailable, Is.False);
            Assert.That(status.ProviderReachable, Is.True);
            Assert.That(status.OrchestratorRoundTripOk, Is.False);
            Assert.That(status.Message, Does.Contain("Round-trip:"));
        });
    }

    [TestCase("config")]
    [TestCase("env")]
    public async Task Mistral_KeyConfigured_IsAvailable(string source)
    {
        var values = new Dictionary<string, string?>
        {
            ["AI:ChatProvider"] = "mistral",
            ["Mistral:Model"] = "mistral-test-model",
        };
        if (source == "config")
        {
            values["Mistral:ApiKey"] = "test-key";
        }
        else
        {
            Environment.SetEnvironmentVariable(MistralEnv, "env-key");
        }

        var http = new StubHandler(HttpStatusCode.OK, "{}");
        var status = await CreateProbe(values, http).GetStatusAsync();

        Assert.Multiple(() =>
        {
            Assert.That(status.IsAvailable, Is.True, status.Message);
            Assert.That(status.ProviderReachable, Is.True);
            Assert.That(status.Provider, Is.EqualTo("mistral"));
            Assert.That(status.Message, Does.StartWith("Mistral is configured."));
            Assert.That(status.ChatModel, Is.EqualTo("mistral-test-model"));
            Assert.That(http.Requests, Is.Empty, "Mistral readiness is key-only");
        });
    }

    [Test]
    public async Task Mistral_NoKey_IsUnavailable()
    {
        var probe = CreateProbe(
            new Dictionary<string, string?> { ["AI:ChatProvider"] = "mistral" },
            new StubHandler(HttpStatusCode.OK, "{}"));

        var status = await probe.GetStatusAsync();

        Assert.Multiple(() =>
        {
            Assert.That(status.IsAvailable, Is.False);
            Assert.That(status.ProviderReachable, Is.False);
            Assert.That(status.Message, Does.StartWith("Mistral is not configured."));
            Assert.That(status.Message, Does.Contain(MistralEnv));
        });
    }

    private static ChatProviderReadinessProbe CreateProbe(
        IReadOnlyDictionary<string, string?> values,
        StubHandler http,
        IIntent[]? intents = null)
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(values).Build();

        var services = new ServiceCollection();
        foreach (var intent in intents ?? [new CatalogIntent()])
        {
            services.AddSingleton(intent);
        }

        return new ChatProviderReadinessProbe(
            configuration,
            new StubHttpClientFactory(new HttpClient(http)),
            services.BuildServiceProvider(),
            NullLogger<ChatProviderReadinessProbe>.Instance);
    }

    private sealed class StubHttpClientFactory(HttpClient client) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => client;
    }

    private sealed class StubHandler : HttpMessageHandler
    {
        private readonly HttpStatusCode _status;
        private readonly string _body = string.Empty;
        private readonly Exception? _exception;

        public StubHandler(HttpStatusCode status, string body) => (_status, _body) = (status, body);

        public StubHandler(Exception exception) => _exception = exception;

        public List<HttpRequestMessage> Requests { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add(request);
            if (_exception is not null)
            {
                return Task.FromException<HttpResponseMessage>(_exception);
            }

            return Task.FromResult(new HttpResponseMessage(_status)
            {
                Content = new StringContent(_body, Encoding.UTF8, "application/json"),
            });
        }
    }

    private sealed class CatalogIntent : IIntent
    {
        public string Id => "skill.beginnerchords";
        public string Description => "catalog intent for tests";
        public IReadOnlyList<string> ExamplePrompts => [];
        public Task<IntentResult> ExecuteAsync(string query, CancellationToken cancellationToken = default) =>
            Task.FromResult(new IntentResult("ok"));
    }
}
