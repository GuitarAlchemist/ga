namespace GaApi.Tests.Services;

using GA.Business.Core.Orchestration.Intents;
using GA.Business.Core.Orchestration.Models;
using GA.Business.Core.Orchestration.Services;
using GA.Business.ML.Agents.Intents;
using GA.Business.ML.Agents.Skills;
using GA.Business.ML.Extensions;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using AiChatResponse = Microsoft.Extensions.AI.ChatResponse;

/// <summary>
/// A skill picked for a message that lacks its required argument declines, and the production
/// orchestrator answers through the LLM agent path instead of returning the skill's parse error.
/// The prompt was answered "Could not identify a chord symbol in your message." on the live chat
/// (2026-11-16). The production chord-substitution skill is the only registered intent, and every
/// text embeds to the same vector, so the router always picks it. Runs without Ollama.
/// </summary>
[TestFixture]
public class SkillMissingArgumentFallThroughTests
{
    private const string Prompt = "Explain modal interchange in two sentences.";
    private const string AgentAnswer = "Modal interchange borrows chords from a parallel mode.";

    [TestCase(false)]
    [TestCase(true)]
    public async Task ModalInterchangePrompt_ReachesTheAgentPath(bool streaming)
    {
        var chat = new FixedChatClient();
        using var factory = new TestWebApplicationFactory().WithWebHostBuilder(builder =>
        {
            // Unreachable Ollama: any call the fakes miss fails fast instead of reaching a local model.
            builder.UseSetting("Ollama:Endpoint", "http://127.0.0.1:9");
            builder.UseSetting("Ollama:BaseUrl", "http://127.0.0.1:9");
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<IIntent>();
                services.AddSingleton<IIntent>(sp => new OrchestratorSkillIntent(sp.GetRequiredService<ChordSubstitutionSkill>()));
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
        var request = new ChatRequest(Prompt);

        var response = streaming
            ? await orchestrator.AnswerStreamingAsync(request, _ => Task.CompletedTask)
            : await orchestrator.AnswerAsync(request);

        Assert.Multiple(() =>
        {
            Assert.That(response.NaturalLanguageAnswer, Does.Not.Contain("Could not identify"));
            Assert.That(response.Routing?.RoutingMethod, Is.Not.EqualTo("orchestrator-skill-semantic"));
            Assert.That(chat.Calls, Is.GreaterThan(0), "the agent path must answer");
        });
    }

    private sealed class ConstantEmbeddingGeneratorFactory(ConstantEmbeddingGenerator embeddings) : IEmbeddingGeneratorFactory
    {
        public IEmbeddingGenerator<string, Embedding<float>> Create(string purpose) => embeddings;
    }

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

    private sealed class FixedChatClient : IChatClient
    {
        private int _calls;

        public int Calls => Volatile.Read(ref _calls);

        public Task<AiChatResponse> GetResponseAsync(
            IEnumerable<ChatMessage> messages,
            ChatOptions? options = null,
            CancellationToken cancellationToken = default)
        {
            Interlocked.Increment(ref _calls);
            return Task.FromResult(new AiChatResponse(new ChatMessage(ChatRole.Assistant, AgentAnswer)));
        }

        public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
            IEnumerable<ChatMessage> messages,
            ChatOptions? options = null,
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            Interlocked.Increment(ref _calls);
            await Task.Yield();
            yield return new ChatResponseUpdate(ChatRole.Assistant, AgentAnswer);
        }

        public object? GetService(Type serviceType, object? serviceKey = null) => null;

        public void Dispose() { }
    }
}
