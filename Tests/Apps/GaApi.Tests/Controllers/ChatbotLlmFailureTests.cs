namespace GaApi.Tests.Controllers;

using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using GA.Business.ML.Agents.Intents;
using GA.Business.ML.Extensions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using AiChatResponse = Microsoft.Extensions.AI.ChatResponse;

/// <summary>
/// When the LLM provider refuses to generate (Ollama Cloud answering 402 Payment
/// Required, or the Ollama adapter's "Ollama error: …" InvalidOperationException),
/// a prompt that reaches the LLM must still get HTTP 200 with an honest answer,
/// not an HTTP 500 or an SSE error frame. Mirrors the graceful answer of the
/// retiring GaChatbot.Api host.
/// </summary>
[TestFixture]
public class ChatbotLlmFailureTests
{
    private const string Prompt = "What is the weather in Paris today?";

    private static IEnumerable<TestCaseData> ProviderFailures()
    {
        yield return new TestCaseData(new Func<Exception>(() => new HttpRequestException(
            "Response status code does not indicate success: 402 (Payment Required).",
            null,
            HttpStatusCode.PaymentRequired))).SetArgDisplayNames("402 HttpRequestException");
        yield return new TestCaseData(new Func<Exception>(() => new InvalidOperationException(
            "Ollama error: Add usage credits or subscribe to continue using cloud models.")))
            .SetArgDisplayNames("Ollama InvalidOperationException");
    }

    [TestCaseSource(nameof(ProviderFailures))]
    public async Task JsonChat_ProviderFails_Returns200WithGracefulAnswer(Func<Exception> failure)
    {
        using var factory = CreateFactory(new ThrowingChatClient(failure));
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/chatbot/chat", new { message = Prompt });
        var body = await response.Content.ReadAsStringAsync();

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK), body);
        using var json = JsonDocument.Parse(body);
        var answer = json.RootElement.GetProperty("naturalLanguageAnswer").GetString();
        Assert.Multiple(() =>
        {
            Assert.That(answer, Does.Contain("reasoning service is currently unavailable"));
            Assert.That(json.RootElement.GetProperty("confidence").GetSingle(), Is.Zero);
        });
    }

    [TestCaseSource(nameof(ProviderFailures))]
    public async Task StreamChat_ProviderFails_StreamsGracefulAnswer(Func<Exception> failure)
    {
        using var factory = CreateFactory(new ThrowingChatClient(failure));
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/chatbot/chat/stream", new { message = Prompt });
        var body = await response.Content.ReadAsStringAsync();

        Assert.Multiple(() =>
        {
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            Assert.That(body, Does.Not.Contain("event: error"), body);
            Assert.That(body, Does.Contain("reasoning service is currently unavailable"), body);
            Assert.That(body, Does.Contain("data: [DONE]"), body);
        });
    }

    private static WebApplicationFactory<Program> CreateFactory(IChatClient chat) =>
        new TestWebApplicationFactory().WithWebHostBuilder(builder =>
        {
            // Unreachable Ollama: any call the fakes miss fails fast instead of reaching a local model.
            builder.UseSetting("Ollama:Endpoint", "http://127.0.0.1:9");
            builder.UseSetting("Ollama:BaseUrl", "http://127.0.0.1:9");
            builder.UseSetting("IX:External:Enabled", "false");
            builder.ConfigureTestServices(services =>
            {
                // The only intent declines, so the orchestrator falls through to the LLM agents.
                services.RemoveAll<IIntent>();
                services.AddSingleton<IIntent>(new DecliningIntent());
                var embeddings = new ConstantEmbeddingGenerator();
                services.RemoveAll<IEmbeddingGenerator<string, Embedding<float>>>();
                services.AddSingleton<IEmbeddingGenerator<string, Embedding<float>>>(embeddings);
                services.RemoveAll<IEmbeddingGeneratorFactory>();
                services.AddSingleton<IEmbeddingGeneratorFactory>(new ConstantEmbeddingGeneratorFactory(embeddings));
                services.RemoveAll<IChatClient>();
                services.AddSingleton(chat);
            });
        });

    private sealed class ThrowingChatClient(Func<Exception> failure) : IChatClient
    {
        public Task<AiChatResponse> GetResponseAsync(
            IEnumerable<ChatMessage> messages,
            ChatOptions? options = null,
            CancellationToken cancellationToken = default) =>
            Task.FromException<AiChatResponse>(failure());

        public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
            IEnumerable<ChatMessage> messages,
            ChatOptions? options = null,
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            await Task.Yield();
            throw failure();
#pragma warning disable CS0162 // An async iterator needs a yield to compile.
            yield break;
#pragma warning restore CS0162
        }

        public object? GetService(Type serviceType, object? serviceKey = null) => null;

        public void Dispose() { }
    }

    private sealed class DecliningIntent : IIntent
    {
        public string Id => "skill.declining";
        public string Description => "Declines every message";
        public IReadOnlyList<string> ExamplePrompts => [Prompt];

        public Task<IntentResult> ExecuteAsync(string query, CancellationToken cancellationToken = default) =>
            Task.FromResult(new IntentResult("declined", Confidence: 0.1f, Declined: true));
    }

    private sealed class ConstantEmbeddingGeneratorFactory(ConstantEmbeddingGenerator embeddings) : IEmbeddingGeneratorFactory
    {
        public IEmbeddingGenerator<string, Embedding<float>> Create(string purpose) => embeddings;
    }

    // Every text embeds to the same vector, so the only registered intent always scores 1.0.
    private sealed class ConstantEmbeddingGenerator : IEmbeddingGenerator<string, Embedding<float>>
    {
        public Task<GeneratedEmbeddings<Embedding<float>>> GenerateAsync(
            IEnumerable<string> values,
            EmbeddingGenerationOptions? options = null,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new GeneratedEmbeddings<Embedding<float>>(
                values.Select(_ => new Embedding<float>(new float[] { 1f, 0f, 0f }))));

        public object? GetService(Type serviceType, object? serviceKey = null) => null;

        public void Dispose() { }
    }
}
