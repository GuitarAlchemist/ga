namespace GaApi.Tests.Services;

using System.Net;
using GaApi.Configuration;
using GaApi.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
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

    private static ChatProviderAvailability Create(string provider, StubHandler handler)
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["AI:ChatProvider"] = provider
        }).Build();
        var factory = new Mock<IHttpClientFactory>();
        factory.Setup(f => f.CreateClient("Ollama"))
            .Returns(() => new HttpClient(handler, disposeHandler: false) { BaseAddress = new Uri("http://localhost:11434") });
        factory.Setup(f => f.CreateClient("DockerModelRunner"))
            .Returns(() => new HttpClient(handler, disposeHandler: false) { BaseAddress = new Uri("http://localhost:12434/v1") });
        var options = Mock.Of<IOptionsMonitor<ChatbotOptions>>(o => o.CurrentValue == new ChatbotOptions { Model = "llama3.2:3b" });
        return new ChatProviderAvailability(factory.Object, configuration, options);
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
