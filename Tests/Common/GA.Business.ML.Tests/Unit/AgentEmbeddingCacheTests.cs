namespace GA.Business.ML.Tests.Unit;

using Agents;
using GA.Business.ML.Extensions;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

/// <summary>
/// Pins the process-wide cache for agent-description embeddings.
/// <see cref="SemanticRouter"/> is scoped, so before this cache every chat request
/// re-embedded the agent descriptions (one remote embedding call, up to 5 s on a
/// cold Ollama). These tests pin: one embedding call across router instances,
/// re-embedding when a description or the generator changes, no poisoning on
/// failure, and a single call under concurrent load.
/// </summary>
[TestFixture]
public class AgentEmbeddingCacheTests
{
    [Test]
    public async Task TwoRouters_SharingTheCache_EmbedAgentDescriptionsOnce()
    {
        var chatClient = new Mock<IChatClient>().Object;
        var generator = new CountingEmbedder();
        using var cache = new AgentEmbeddingCache();

        foreach (var _ in Enumerable.Range(0, 2))
        {
            using var router = new SemanticRouter(
                [
                    new TheoryAgent(chatClient, NullLogger<TheoryAgent>.Instance),
                    new TabAgent(chatClient, NullLogger<TabAgent>.Instance)
                ],
                null,
                generator,
                NullLogger<SemanticRouter>.Instance,
                embeddingCache: cache);

            await router.RouteAsync("Explain the circle of fifths");
        }

        // Agent descriptions are embedded as one batch of two; queries as batches of one.
        Assert.That(generator.Batches.Count(b => b.Count == 2), Is.EqualTo(1));
    }

    [Test]
    public async Task ChangedDescription_EmbedsOnlyTheNewText()
    {
        var generator = new CountingEmbedder();
        using var cache = new AgentEmbeddingCache();

        await cache.GetOrCreateAsync(generator, ["theory agent", "tab agent"], CancellationToken.None);
        var second = await cache.GetOrCreateAsync(generator, ["theory agent", "tab agent v2"], CancellationToken.None);

        Assert.That(generator.Batches, Has.Count.EqualTo(2));
        Assert.That(generator.Batches[1], Is.EqualTo(new[] { "tab agent v2" }));
        Assert.That(second.Vectors, Has.Count.EqualTo(2));
        Assert.That(second.Vectors[1], Is.EqualTo(CountingEmbedder.VectorFor("tab agent v2")));
    }

    [Test]
    public async Task DifferentGenerator_ReEmbeds()
    {
        var first = new CountingEmbedder();
        var second = new CountingEmbedder();
        using var cache = new AgentEmbeddingCache();

        await cache.GetOrCreateAsync(first, ["theory agent"], CancellationToken.None);
        await cache.GetOrCreateAsync(second, ["theory agent"], CancellationToken.None);

        Assert.That(first.Batches, Has.Count.EqualTo(1));
        Assert.That(second.Batches, Has.Count.EqualTo(1));
    }

    [Test]
    public async Task FailedEmbeddingCall_LeavesNothingCached_AndNextCallRetries()
    {
        var generator = new CountingEmbedder { FailuresBeforeSuccess = 1 };
        using var cache = new AgentEmbeddingCache();

        Assert.ThrowsAsync<HttpRequestException>(() =>
            cache.GetOrCreateAsync(generator, ["theory agent"], CancellationToken.None));

        var result = await cache.GetOrCreateAsync(generator, ["theory agent"], CancellationToken.None);

        Assert.That(generator.Batches, Has.Count.EqualTo(2));
        Assert.That(result.GeneratedCount, Is.EqualTo(1));
        Assert.That(result.Vectors[0], Is.EqualTo(CountingEmbedder.VectorFor("theory agent")));
    }

    [Test]
    public async Task CachedHit_ReportsZeroGenerated()
    {
        var generator = new CountingEmbedder();
        using var cache = new AgentEmbeddingCache();

        var first = await cache.GetOrCreateAsync(generator, ["theory agent"], CancellationToken.None);
        var second = await cache.GetOrCreateAsync(generator, ["theory agent"], CancellationToken.None);

        Assert.That(first.GeneratedCount, Is.EqualTo(1));
        Assert.That(second.GeneratedCount, Is.Zero);
    }

    [Test]
    public async Task ConcurrentCallers_TriggerASingleEmbeddingCall()
    {
        var generator = new CountingEmbedder { DelayMs = 50 };
        using var cache = new AgentEmbeddingCache();

        await Task.WhenAll(Enumerable.Range(0, 20).Select(_ =>
            cache.GetOrCreateAsync(generator, ["theory agent", "tab agent"], CancellationToken.None)));

        Assert.That(generator.Batches, Has.Count.EqualTo(1));
    }

    [Test]
    public void BothAgentRegistrationPaths_RegisterTheCacheAsSingleton()
    {
        var upper = new ServiceCollection().AddGuitarAlchemistAI();
        var lower = new ServiceCollection().AddGuitarAlchemistAgents();

        foreach (var services in new[] { upper, lower })
        {
            var descriptor = services.SingleOrDefault(d => d.ServiceType == typeof(AgentEmbeddingCache));
            Assert.That(descriptor, Is.Not.Null);
            Assert.That(descriptor!.Lifetime, Is.EqualTo(ServiceLifetime.Singleton));
        }
    }

    private sealed class CountingEmbedder : IEmbeddingGenerator<string, Embedding<float>>
    {
        private readonly Lock _sync = new();
        private readonly List<IReadOnlyList<string>> _batches = [];

        public int FailuresBeforeSuccess { get; init; }
        public int DelayMs { get; init; }

        public IReadOnlyList<IReadOnlyList<string>> Batches
        {
            get
            {
                lock (_sync) return [.. _batches];
            }
        }

        public static float[] VectorFor(string text) => [text.Length, 1f];

        public async Task<GeneratedEmbeddings<Embedding<float>>> GenerateAsync(
            IEnumerable<string> values,
            EmbeddingGenerationOptions? options = null,
            CancellationToken cancellationToken = default)
        {
            var batch = values.ToList();
            int callIndex;
            lock (_sync)
            {
                _batches.Add(batch);
                callIndex = _batches.Count;
            }

            if (DelayMs > 0) await Task.Delay(DelayMs, cancellationToken);
            if (callIndex <= FailuresBeforeSuccess) throw new HttpRequestException("Connection refused");

            return [.. batch.Select(text => new Embedding<float>(VectorFor(text)))];
        }

        public object? GetService(Type serviceType, object? serviceKey = null) => null;
        public void Dispose() { }
    }
}
