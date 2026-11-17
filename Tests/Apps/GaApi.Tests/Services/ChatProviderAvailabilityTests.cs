namespace GaApi.Tests.Services;

using System.Net;
using GaApi.Extensions;
using GaApi.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Moq;

/// <summary>
///     Pins the provider availability checks that used to live in the IChatService
///     implementations (OllamaChatService, DockerModelRunnerChatService, ClaudeChatService).
/// </summary>
[TestFixture]
public class ChatProviderAvailabilityTests
{
    [TestCase("""{"models":[{"name":"llama3.2:3b"}]}""", ExpectedResult = true)]
    [TestCase("""{"models":[{"name":"llama3.2:1b"}]}""", ExpectedResult = true)]
    [TestCase("""{"models":[{"name":"qwen2.5:7b"}]}""", ExpectedResult = false)]
    [TestCase("""{"models":[]}""", ExpectedResult = false)]
    public async Task<bool> Ollama_IsAvailableWhenTagsListTheChatbotModelFamily(string tagsJson)
    {
        var handler = new StubHandler(HttpStatusCode.OK, tagsJson);
        var availability = Create("ollama", handler);

        var available = await availability.IsAvailableAsync();

        Assert.That(handler.RequestedPaths, Is.EqualTo(new[] { "/api/tags" }));
        return available;
    }

    [TestCase("""{"data":[{"id":"docker.io/ai/llama3.2:latest"}]}""", ExpectedResult = true)]
    [TestCase("""{"data":[{"id":"docker.io/ai/qwen3:latest"}]}""", ExpectedResult = false)]
    public async Task<bool> Docker_IsAvailableWhenModelsMentionTheChatModelFamily(string modelsJson)
    {
        var handler = new StubHandler(HttpStatusCode.OK, modelsJson);
        var availability = Create("docker", handler);

        var available = await availability.IsAvailableAsync();

        Assert.That(handler.RequestedPaths, Is.EqualTo(new[] { "/v1/models" }));
        return available;
    }

    [TestCase("ollama")]
    [TestCase("docker")]
    public async Task HttpFailureOrUnreachableProvider_IsUnavailable(string provider)
    {
        Assert.That(await Create(provider, new StubHandler(HttpStatusCode.ServiceUnavailable, "")).IsAvailableAsync(), Is.False);
        Assert.That(await Create(provider, new StubHandler(new HttpRequestException("offline"))).IsAvailableAsync(), Is.False);
    }

    [Test]
    public async Task Claude_IsAlwaysAvailableWithoutAnHttpCall()
    {
        var handler = new StubHandler(HttpStatusCode.OK, "");

        Assert.That(await Create("claude", handler).IsAvailableAsync(), Is.True);
        Assert.That(handler.RequestedPaths, Is.Empty);
    }

    [Test]
    public async Task Ollama_UsesTheChatbotStreamTimeout()
    {
        var handler = new StubHandler(HttpStatusCode.OK, """{"models":[]}""");
        var created = new List<HttpClient>();

        await Create("ollama", handler, created, streamTimeoutSeconds: 7).IsAvailableAsync();

        Assert.That(created.Select(client => client.Timeout), Is.EqualTo(new[] { TimeSpan.FromSeconds(7) }),
            "/api/chatbot/status must stay bounded by Chatbot:StreamTimeoutSeconds, not the 100 s HttpClient default");
    }

    [Test]
    public async Task Registration_SnapshotsTheProviderForStatusAndChatClient()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["AI:ChatProvider"] = "ollama",
            ["Ollama:BaseUrl"] = "http://127.0.0.1:9"
        }).Build();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IConfiguration>(configuration);
        services.AddAiServices(configuration);

        // A reload after registration must not switch status to a provider the
        // singleton IChatClient (built from the same snapshot) does not use.
        configuration["AI:ChatProvider"] = "claude";
        using var container = services.BuildServiceProvider();

        Assert.That(container.GetRequiredService<ChatProviderSelection>().Provider, Is.EqualTo("ollama"));
        Assert.That(await container.GetRequiredService<IChatProviderAvailability>().IsAvailableAsync(), Is.False,
            "the unreachable Ollama primary is reported, not an always-available claude");
    }

    private static ChatProviderAvailability Create(
        string provider,
        StubHandler handler,
        List<HttpClient>? created = null,
        int streamTimeoutSeconds = 60)
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["AI:ChatProvider"] = provider,
            ["Chatbot:Model"] = "llama3.2:3b",
            ["Chatbot:StreamTimeoutSeconds"] = streamTimeoutSeconds.ToString()
        }).Build();
        var factory = new Mock<IHttpClientFactory>();
        factory.Setup(f => f.CreateClient("Ollama"))
            .Returns(() => Track(new HttpClient(handler, disposeHandler: false) { BaseAddress = new Uri("http://localhost:11434") }));
        factory.Setup(f => f.CreateClient("DockerModelRunner"))
            .Returns(() => Track(new HttpClient(handler, disposeHandler: false) { BaseAddress = new Uri("http://localhost:12434/v1") }));
        return new ChatProviderAvailability(factory.Object, ChatProviderSelection.FromConfiguration(configuration));

        HttpClient Track(HttpClient client)
        {
            created?.Add(client);
            return client;
        }
    }

    private sealed class StubHandler : HttpMessageHandler
    {
        private readonly Func<HttpResponseMessage> _respond;

        public StubHandler(HttpStatusCode status, string body) =>
            _respond = () => new HttpResponseMessage(status) { Content = new StringContent(body) };

        public StubHandler(Exception error) => _respond = () => throw error;

        public List<string> RequestedPaths { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            RequestedPaths.Add(request.RequestUri!.AbsolutePath);
            return Task.FromResult(_respond());
        }
    }
}
