namespace GA.Business.Core.Tests.Services;

using System.Threading;
using System.Collections.Generic;
using System.Threading.Tasks;
using GA.Business.Core.AI.Services.Embeddings;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using NUnit.Framework;

public sealed class CountingEmbeddingService : IEmbeddingService
{
    public int CallCount;

    public Task<float[]> GenerateEmbeddingAsync(string text, CancellationToken cancellationToken = default)
    {
        Interlocked.Increment(ref CallCount);
        var vector = new float[text.Length];
        for (var i = 0; i < vector.Length; i++)
        {
            vector[i] = text[i % text.Length];
        }

        return Task.FromResult(vector);
    }
}

[TestFixture]
public class VoicingEmbeddingCacheTests
{
    [Test]
    public async Task GetOrCreateAsync_CachesEmbeddingAcrossRequests()
    {
        var memoryCache = new MemoryCache(new MemoryCacheOptions());
        var embeddingService = new CountingEmbeddingService();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string>
            {
                ["VoicingSearch:EmbeddingCacheMinutes"] = "1"
            })
            .Build();
        var loggerFactory = LoggerFactory.Create(builder => builder.SetMinimumLevel(LogLevel.Debug));
        var cache = new VoicingEmbeddingCache(memoryCache, embeddingService, configuration, loggerFactory.CreateLogger<VoicingEmbeddingCache>());

        var first = await cache.GetOrCreateAsync("hello", CancellationToken.None);
        var second = await cache.GetOrCreateAsync("hello", CancellationToken.None);

        Assert.That(second, Is.SameAs(first), "The cached embedding should return the same reference.");
        Assert.That(embeddingService.CallCount, Is.EqualTo(1), "The embedding service should only be called once.");

        var third = await cache.GetOrCreateAsync("world", CancellationToken.None);
        Assert.That(third, Is.Not.SameAs(first));
        Assert.That(embeddingService.CallCount, Is.EqualTo(2));
    }
}
