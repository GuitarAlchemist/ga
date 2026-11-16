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
using ChatResponse = GA.Business.Core.Orchestration.Models.ChatResponse;

/// <summary>
/// The streaming and non-streaming entry points of <see cref="ProductionOrchestrator"/> run one
/// routing cascade and differ only in how the answer is emitted. Each test runs the same request
/// through both and pins what the live chat (the streaming path) used to miss: the skill hook
/// lifecycle, the voicing guard ahead of intent routing, and the response hooks on every branch.
/// Runs without Ollama, with the fakes of <see cref="DeclinedIntentFallThroughTests"/>: the only
/// registered intent always scores 1.0.
/// </summary>
[TestFixture]
public class OrchestratorCascadeParityTests
{
    private const string IntentAnswer = "answer from the fixed intent";

    [TestCase(false)]
    [TestCase(true)]
    public async Task AcceptedIntent_RunsTheSkillHookLifecycle(bool streaming)
    {
        var hook = new RecordingHook();
        var intent = new FixedIntent(declined: false);
        using var factory = CreateFactory(new FixedChatClient(), hook, intent);
        const string sessionId = "cascade-skill";

        var (response, streamed) = await AnswerAsync(factory, new ChatRequest("Describe his playing style.", SessionId: sessionId), streaming);

        Assert.Multiple(() =>
        {
            Assert.That(response.NaturalLanguageAnswer, Is.EqualTo(IntentAnswer));
            Assert.That(hook.Events.Select(e => e.Stage), Is.EqualTo(new[]
            {
                nameof(IChatHook.OnRequestReceived),
                nameof(IChatHook.OnBeforeSkill),
                nameof(IChatHook.OnAfterSkill),
                nameof(IChatHook.OnResponseSent),
            }));
            Assert.That(hook.Events.Skip(1).Select(e => e.Context.MatchedSkillName), Has.All.EqualTo(FixedIntent.IntentId));
            Assert.That(hook.Events.Select(e => e.Context.SessionId), Has.All.EqualTo(sessionId));
            Assert.That(hook.Events.Select(e => e.Context.CorrelationId).Distinct().Count(), Is.EqualTo(1),
                "one correlation id per request, so OnBeforeSkill and OnAfterSkill pair up");
            Assert.That(hook.Sent.Single().Response?.Data, Is.SameAs(FixedIntent.Payload),
                "the intent's payload reaches OnResponseSent (MemoryWriteHook reads it)");
        });
        AssertStreamedAnswer(streaming, streamed, response);
    }

    // "Show me Drop 2 voicings of Cmaj7" used to lose to the modes intent on the semantic router.
    // The voicing guard runs before intent routing on both paths, so an intent that would claim
    // every message never sees an explicit voicing request.
    [TestCase(false)]
    [TestCase(true)]
    public async Task ExplicitVoicingRequest_ReachesTheVoicingAgent_BeforeIntentRouting(bool streaming)
    {
        var hook = new RecordingHook();
        var intent = new FixedIntent(declined: false);
        using var factory = CreateFactory(new FixedChatClient(), hook, intent);
        const string sessionId = "cascade-voicing";

        // The CAGED template answers without the voicing index or the LLM.
        var (response, streamed) = await AnswerAsync(factory, new ChatRequest("Show me a C major voicing in the A shape", SessionId: sessionId), streaming);

        Assert.Multiple(() =>
        {
            Assert.That(response.Routing?.RoutingMethod, Is.EqualTo("deterministic-voicing"));
            Assert.That(intent.Executions, Is.Zero, "the voicing guard must run before intent routing");
            Assert.That(hook.Sent.Single().Response?.Result, Is.EqualTo(response.NaturalLanguageAnswer));
            Assert.That(hook.Sent.Single().SessionId, Is.EqualTo(sessionId));
        });
        AssertStreamedAnswer(streaming, streamed, response);
    }

    [TestCase(false)]
    [TestCase(true)]
    public async Task AlgebraPrompt_IsAnsweredByTheFastPath_BeforeIntentRouting(bool streaming)
    {
        var hook = new RecordingHook();
        var intent = new FixedIntent(declined: false);
        using var factory = CreateFactory(new FixedChatClient(), hook, intent);

        var (response, streamed) = await AnswerAsync(factory, new ChatRequest("What is the prime form of [0,4,7]?", SessionId: "cascade-algebra"), streaming);

        Assert.Multiple(() =>
        {
            Assert.That(response.Routing?.RoutingMethod, Is.EqualTo("ix-algebra"));
            Assert.That(intent.Executions, Is.Zero, "the algebra fast path must run before intent routing");
            Assert.That(hook.Sent.Single().Response?.AgentId, Is.EqualTo("algebra"));
        });
        AssertStreamedAnswer(streaming, streamed, response);
    }

    // A declined intent still closes its OnBeforeSkill with OnAfterSkill (ObservabilityHook opens a
    // span on the first and disposes it on the second), then the LLM agent answers.
    [TestCase(false)]
    [TestCase(true)]
    public async Task DeclinedIntent_ClosesTheSkillHooks_AndTheAgentAnswerRunsTheResponseHooks(bool streaming)
    {
        var hook = new RecordingHook();
        using var factory = CreateFactory(new FixedChatClient(), hook, new FixedIntent(declined: true));
        const string sessionId = "cascade-agent";

        var (response, streamed) = await AnswerAsync(factory, new ChatRequest("Describe his playing style.", SessionId: sessionId), streaming);

        Assert.Multiple(() =>
        {
            Assert.That(response.NaturalLanguageAnswer, Does.Contain("Gypsy jazz"));
            Assert.That(hook.Events.Select(e => e.Stage), Is.EqualTo(new[]
            {
                nameof(IChatHook.OnRequestReceived),
                nameof(IChatHook.OnBeforeSkill),
                nameof(IChatHook.OnAfterSkill),
                nameof(IChatHook.OnResponseSent),
            }));
            Assert.That(hook.Sent.Single().Response?.Result, Is.EqualTo(response.NaturalLanguageAnswer));
            Assert.That(hook.Sent.Single().SessionId, Is.EqualTo(sessionId));
        });
        AssertStreamedAnswer(streaming, streamed, response);
    }

    // A blocked request answers with the hook's reason on both paths; the stream used to end empty.
    [TestCase(false)]
    [TestCase(true)]
    public async Task BlockedRequest_AnswersWithTheHookReason(bool streaming)
    {
        var intent = new FixedIntent(declined: false);
        using var factory = CreateFactory(new FixedChatClient(), new BlockingHook(), intent);

        var (response, streamed) = await AnswerAsync(factory, new ChatRequest("Describe his playing style.", SessionId: "cascade-blocked"), streaming);

        Assert.Multiple(() =>
        {
            Assert.That(response.NaturalLanguageAnswer, Is.EqualTo(BlockingHook.Reason));
            Assert.That(response.Routing?.RoutingMethod, Is.EqualTo("hook-blocked"));
            Assert.That(intent.Executions, Is.Zero);
        });
        AssertStreamedAnswer(streaming, streamed, response);
    }

    private static async Task<(ChatResponse Response, string Streamed)> AnswerAsync(
        WebApplicationFactory<Program> factory, ChatRequest request, bool streaming)
    {
        using var scope = factory.Services.CreateScope();
        var orchestrator = scope.ServiceProvider.GetRequiredService<ProductionOrchestrator>();
        if (!streaming) return (await orchestrator.AnswerAsync(request), string.Empty);

        var tokens = new List<string>();
        var response = await orchestrator.AnswerStreamingAsync(request, token =>
        {
            tokens.Add(token);
            return Task.CompletedTask;
        });
        return (response, string.Concat(tokens));
    }

    private static void AssertStreamedAnswer(bool streaming, string streamed, ChatResponse response)
    {
        if (!streaming) return;
        Assert.That(Normalize(streamed), Is.EqualTo(Normalize(response.NaturalLanguageAnswer)),
            "the streaming path emits the answer it returns");
    }

    // Word-level emission collapses runs of whitespace, so compare word sequences.
    private static string Normalize(string text) =>
        string.Join(' ', text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));

    private static WebApplicationFactory<Program> CreateFactory(FixedChatClient chat, IChatHook hook, IIntent intent) =>
        new TestWebApplicationFactory().WithWebHostBuilder(builder =>
        {
            builder.UseSetting("Chatbot:Mode", "full");
            // Unreachable Ollama: any call the fakes miss fails fast instead of reaching a local model.
            builder.UseSetting("Ollama:Endpoint", "http://127.0.0.1:9");
            builder.UseSetting("Ollama:BaseUrl", "http://127.0.0.1:9");
            // The in-process algebra service answers; no external ix binary.
            builder.UseSetting("IX:External:Enabled", "false");
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<IIntent>();
                services.AddSingleton(intent);
                var embeddings = new ConstantEmbeddingGenerator();
                services.RemoveAll<IEmbeddingGenerator<string, Embedding<float>>>();
                services.AddSingleton<IEmbeddingGenerator<string, Embedding<float>>>(embeddings);
                services.RemoveAll<IEmbeddingGeneratorFactory>();
                services.AddSingleton<IEmbeddingGeneratorFactory>(new ConstantEmbeddingGeneratorFactory(embeddings));
                services.RemoveAll<IChatClient>();
                services.AddSingleton<IChatClient>(chat);
                // Only the recording hook: the production hooks would persist the answers.
                services.RemoveAll<IChatHook>();
                services.AddSingleton<IChatHook>(hook);
            });
        });

    private sealed class FixedIntent(bool declined) : IIntent
    {
        public const string IntentId = "skill.fixed";
        public static readonly object Payload = new();
        private int _executions;

        public int Executions => Volatile.Read(ref _executions);
        public string Id => IntentId;
        public string Description => "Fixed test intent";
        public IReadOnlyList<string> ExamplePrompts => ["Describe his playing style."];

        public Task<IntentResult> ExecuteAsync(string query, CancellationToken cancellationToken = default)
        {
            Interlocked.Increment(ref _executions);
            return Task.FromResult(new IntentResult(IntentAnswer, Confidence: 0.9f, Data: Payload, Declined: declined));
        }
    }

    private sealed record HookEvent(string Stage, ChatHookContext Context);

    private sealed class RecordingHook : IChatHook
    {
        private readonly Lock _gate = new();
        private readonly List<HookEvent> _events = [];

        public IReadOnlyList<HookEvent> Events
        {
            get { lock (_gate) return [.. _events]; }
        }

        public IReadOnlyList<ChatHookContext> Sent =>
            [.. Events.Where(e => e.Stage == nameof(OnResponseSent)).Select(e => e.Context)];

        public Task<HookResult> OnRequestReceived(ChatHookContext ctx, CancellationToken ct = default) =>
            Record(nameof(OnRequestReceived), ctx);

        public Task<HookResult> OnBeforeSkill(ChatHookContext ctx, CancellationToken ct = default) =>
            Record(nameof(OnBeforeSkill), ctx);

        public Task<HookResult> OnAfterSkill(ChatHookContext ctx, CancellationToken ct = default) =>
            Record(nameof(OnAfterSkill), ctx);

        public Task<HookResult> OnResponseSent(ChatHookContext ctx, CancellationToken ct = default) =>
            Record(nameof(OnResponseSent), ctx);

        private Task<HookResult> Record(string stage, ChatHookContext ctx)
        {
            lock (_gate) _events.Add(new HookEvent(stage, ctx));
            return Task.FromResult(HookResult.Continue);
        }
    }

    private sealed class BlockingHook : IChatHook
    {
        public const string Reason = "Blocked by policy.";

        public Task<HookResult> OnRequestReceived(ChatHookContext ctx, CancellationToken ct = default) =>
            Task.FromResult(HookResult.Block(Reason));
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

    private sealed class FixedChatClient : IChatClient
    {
        public Task<AiChatResponse> GetResponseAsync(
            IEnumerable<ChatMessage> messages,
            ChatOptions? options = null,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new AiChatResponse(new ChatMessage(ChatRole.Assistant, "Gypsy jazz with fast runs.")));

        public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
            IEnumerable<ChatMessage> messages,
            ChatOptions? options = null,
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            await Task.Yield();
            yield return new ChatResponseUpdate(ChatRole.Assistant, "Gypsy jazz ");
            yield return new ChatResponseUpdate(ChatRole.Assistant, "with fast runs.");
        }

        public object? GetService(Type serviceType, object? serviceKey = null) => null;

        public void Dispose() { }
    }
}
