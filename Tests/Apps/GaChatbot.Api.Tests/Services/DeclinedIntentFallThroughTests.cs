namespace GaChatbot.Api.Tests.Services;

using GA.Business.Core.Orchestration.Models;
using GA.Business.Core.Orchestration.Services;
using GA.Business.ML.Agents.Intents;
using GA.Business.ML.Extensions;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using AiChatResponse = Microsoft.Extensions.AI.ChatResponse;

/// <summary>
/// A semantic intent that declines a message must not answer it: the production orchestrator
/// continues to the LLM agent path, which receives the conversation history. Runs without
/// Ollama by replacing the embedder, chat client, and intent registry with deterministic fakes.
/// </summary>
[TestFixture]
public class DeclinedIntentFallThroughTests
{
    private const string DeclineText = "not a message this intent handles";
    private const string PriorTurn = "My favorite guitarist is Django Reinhardt.";

    [TestCase(true, false)]
    [TestCase(false, false)]
    [TestCase(true, true)]
    [TestCase(false, true)]
    public async Task DeclinedIntent_FallsThroughToAgentPathWithHistory(bool declined, bool streaming)
    {
        var chat = new RecordingChatClient();
        using var factory = new TestWebApplicationFactory().WithWebHostBuilder(builder =>
        {
            builder.UseSetting("Chatbot:Mode", "full");
            // Unreachable Ollama: any call the fakes miss fails fast instead of reaching a local model.
            builder.UseSetting("Ollama:Endpoint", "http://127.0.0.1:9");
            builder.UseSetting("Ollama:BaseUrl", "http://127.0.0.1:9");
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<IIntent>();
                services.AddSingleton<IIntent>(new FixedIntent(declined));
                var embeddings = new ConstantEmbeddingGenerator();
                services.RemoveAll<IEmbeddingGenerator<string, Embedding<float>>>();
                services.AddSingleton<IEmbeddingGenerator<string, Embedding<float>>>(embeddings);
                services.RemoveAll<IEmbeddingGeneratorFactory>();
                services.AddSingleton<IEmbeddingGeneratorFactory>(new ConstantEmbeddingGeneratorFactory(embeddings));
                services.RemoveAll<IChatClient>();
                services.AddSingleton<IChatClient>(chat);
            });
        });
        using var scope = factory.Services.CreateScope();
        var orchestrator = scope.ServiceProvider.GetRequiredService<ProductionOrchestrator>();
        var request = new ChatRequest(
            "Describe his playing style.",
            History: [new ConversationTurn("user", PriorTurn, DateTimeOffset.UnixEpoch)]);

        var response = streaming
            ? await orchestrator.AnswerStreamingAsync(request, _ => Task.CompletedTask)
            : await orchestrator.AnswerAsync(request);

        if (!declined)
        {
            Assert.That(response.NaturalLanguageAnswer, Is.EqualTo(DeclineText));
            Assert.That(chat.Prompts, Is.Empty, "an accepted intent answers without the LLM");
            return;
        }

        Assert.Multiple(() =>
        {
            Assert.That(response.NaturalLanguageAnswer, Is.Not.EqualTo(DeclineText));
            Assert.That(response.Routing?.AgentId, Is.Not.EqualTo(FixedIntent.IntentId));
            Assert.That(chat.Prompts.Any(prompt => prompt.Contains(PriorTurn, StringComparison.Ordinal)), Is.True,
                "the fall-through agent call must carry the prior turn");
        });
    }

    private sealed class FixedIntent(bool declined) : IIntent
    {
        public const string IntentId = "skill.fixed";
        public string Id => IntentId;
        public string Description => "Fixed test intent";
        public IReadOnlyList<string> ExamplePrompts => ["Describe his playing style."];

        public Task<IntentResult> ExecuteAsync(string query, CancellationToken cancellationToken = default) =>
            Task.FromResult(new IntentResult(DeclineText, Confidence: 0.1f, Declined: declined));
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

    private sealed class RecordingChatClient : IChatClient
    {
        private readonly Lock _gate = new();
        private readonly List<string> _prompts = [];

        public IReadOnlyList<string> Prompts
        {
            get { lock (_gate) return [.. _prompts]; }
        }

        public Task<AiChatResponse> GetResponseAsync(
            IEnumerable<ChatMessage> messages,
            ChatOptions? options = null,
            CancellationToken cancellationToken = default)
        {
            Record(messages);
            return Task.FromResult(new AiChatResponse(new ChatMessage(ChatRole.Assistant, "Gypsy jazz with fast runs.")));
        }

        public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
            IEnumerable<ChatMessage> messages,
            ChatOptions? options = null,
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            Record(messages);
            await Task.Yield();
            yield return new ChatResponseUpdate(ChatRole.Assistant, "Gypsy jazz with fast runs.");
        }

        public object? GetService(Type serviceType, object? serviceKey = null) => null;

        public void Dispose() { }

        private void Record(IEnumerable<ChatMessage> messages)
        {
            var prompt = string.Join("\n", messages.Select(message => message.Text));
            lock (_gate) _prompts.Add(prompt);
        }
    }
}
