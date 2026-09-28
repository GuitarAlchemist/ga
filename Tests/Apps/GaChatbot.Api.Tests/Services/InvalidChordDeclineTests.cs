namespace GaChatbot.Api.Tests.Services;

using GA.Business.Core.Orchestration.Models;
using GA.Business.Core.Orchestration.Services;
using GA.Business.ML.Agents.Hooks;
using GA.Business.ML.Agents.Intents;
using GA.Business.ML.Extensions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using AiChatResponse = Microsoft.Extensions.AI.ChatResponse;

/// <summary>
/// ga#745: the production orchestrator declines an improvisation request that names only
/// invalid chords without calling the LLM. Like its other answers, the decline reaches the
/// response hooks on the non-streaming path, and enters the session history only once the
/// streaming path has delivered it. Runs without Ollama, with the fakes of
/// <see cref="DeclinedIntentFallThroughTests"/>: the only intent declines, so the request
/// reaches the invalid-chord guard.
/// </summary>
[TestFixture]
public class InvalidChordDeclineTests
{
    private const string InvalidChordPrompt = "which arpeggio fits Hm Q7";

    [TestCase(false)]
    [TestCase(true)]
    public async Task InvalidChords_AreDeclinedWithoutTheLlm_AndRecorded(bool streaming)
    {
        var chat = new CountingChatClient();
        var hook = new RecordingHook();
        using var factory = CreateFactory(chat, hook);
        using var scope = factory.Services.CreateScope();
        var orchestrator = scope.ServiceProvider.GetRequiredService<ProductionOrchestrator>();
        var history = factory.Services.GetRequiredService<ConversationHistoryStore>();
        var sessionId = $"ga745-{streaming}";
        var request = new ChatRequest(InvalidChordPrompt, SessionId: sessionId);

        var streamed = new List<string>();
        var response = streaming
            ? await orchestrator.AnswerStreamingAsync(request, token => { streamed.Add(token); return Task.CompletedTask; })
            : await orchestrator.AnswerAsync(request);

        var answer = response.NaturalLanguageAnswer;
        var turns = history.GetHistory(sessionId);
        Assert.Multiple(() =>
        {
            Assert.That(response.Routing?.RoutingMethod, Is.EqualTo("invalid-chord-guard"));
            Assert.That(answer, Does.Contain("don't recognize"));
            Assert.That(chat.Calls, Is.Zero, "the decline must not reach the LLM");
            Assert.That(turns.Select(t => t.Role), Is.EqualTo(new[] { "user", "assistant" }));
            Assert.That(turns[^1].Content, Is.EqualTo(answer));
        });

        if (streaming)
        {
            Assert.That(string.Concat(streamed).TrimEnd(), Is.EqualTo(answer));
            return;
        }

        var sent = hook.Sent.Single();
        Assert.Multiple(() =>
        {
            Assert.That(sent.SessionId, Is.EqualTo(sessionId));
            Assert.That(sent.Response?.Result, Is.EqualTo(answer));
            Assert.That(sent.Response?.Confidence, Is.EqualTo(0.9f));
        });
    }

    [Test]
    public void StreamingDecline_IsNotRecorded_WhenDeliveryFails()
    {
        using var factory = CreateFactory(new CountingChatClient(), new RecordingHook());
        using var scope = factory.Services.CreateScope();
        var orchestrator = scope.ServiceProvider.GetRequiredService<ProductionOrchestrator>();
        var history = factory.Services.GetRequiredService<ConversationHistoryStore>();
        const string sessionId = "ga745-disconnected";

        Assert.ThrowsAsync<IOException>(() => orchestrator.AnswerStreamingAsync(
            new ChatRequest(InvalidChordPrompt, SessionId: sessionId),
            _ => throw new IOException("client disconnected")));

        Assert.That(history.GetHistory(sessionId).Select(t => t.Role), Is.EqualTo(new[] { "user" }),
            "an answer the client never received must not enter the session history");
    }

    private static WebApplicationFactory<Program> CreateFactory(CountingChatClient chat, RecordingHook hook) =>
        new TestWebApplicationFactory().WithWebHostBuilder(builder =>
        {
            builder.UseSetting("Chatbot:Mode", "full");
            // Unreachable Ollama: any call the fakes miss fails fast instead of reaching a local model.
            builder.UseSetting("Ollama:Endpoint", "http://127.0.0.1:9");
            builder.UseSetting("Ollama:BaseUrl", "http://127.0.0.1:9");
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<IIntent>();
                services.AddSingleton<IIntent>(new DecliningIntent());
                var embeddings = new ConstantEmbeddingGenerator();
                services.RemoveAll<IEmbeddingGenerator<string, Embedding<float>>>();
                services.AddSingleton<IEmbeddingGenerator<string, Embedding<float>>>(embeddings);
                services.RemoveAll<IEmbeddingGeneratorFactory>();
                services.AddSingleton<IEmbeddingGeneratorFactory>(new ConstantEmbeddingGeneratorFactory(embeddings));
                services.RemoveAll<IChatClient>();
                services.AddSingleton<IChatClient>(chat);
                // Only the recording hook: the production hooks would persist the decline.
                services.RemoveAll<IChatHook>();
                services.AddSingleton<IChatHook>(hook);
            });
        });

    private sealed class DecliningIntent : IIntent
    {
        public string Id => "skill.declining";
        public string Description => "Declines every message";
        public IReadOnlyList<string> ExamplePrompts => [InvalidChordPrompt];

        public Task<IntentResult> ExecuteAsync(string query, CancellationToken cancellationToken = default) =>
            Task.FromResult(new IntentResult("declined", Confidence: 0.1f, Declined: true));
    }

    private sealed class RecordingHook : IChatHook
    {
        private readonly Lock _gate = new();
        private readonly List<ChatHookContext> _sent = [];

        public IReadOnlyList<ChatHookContext> Sent
        {
            get { lock (_gate) return [.. _sent]; }
        }

        public Task<HookResult> OnResponseSent(ChatHookContext ctx, CancellationToken ct = default)
        {
            lock (_gate) _sent.Add(ctx);
            return Task.FromResult(HookResult.Continue);
        }
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

    private sealed class CountingChatClient : IChatClient
    {
        private int _calls;

        public int Calls => Volatile.Read(ref _calls);

        public Task<AiChatResponse> GetResponseAsync(
            IEnumerable<ChatMessage> messages,
            ChatOptions? options = null,
            CancellationToken cancellationToken = default)
        {
            Interlocked.Increment(ref _calls);
            return Task.FromResult(new AiChatResponse(new ChatMessage(ChatRole.Assistant, "The note Hm Q7 (also known as H)...")));
        }

        public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
            IEnumerable<ChatMessage> messages,
            ChatOptions? options = null,
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            Interlocked.Increment(ref _calls);
            await Task.Yield();
            yield return new ChatResponseUpdate(ChatRole.Assistant, "The note Hm Q7 (also known as H)...");
        }

        public object? GetService(Type serviceType, object? serviceKey = null) => null;

        public void Dispose() { }
    }
}
