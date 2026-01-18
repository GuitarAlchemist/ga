namespace GA.Business.ML.Text.Internal;

using Abstractions;

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

/// <summary>
/// Caches embeddings produced by <see cref="ITextEmbeddingService"/> calls.
/// </summary>
public interface IVoicingEmbeddingCache
{
    Task<double[]> GetOrCreateAsync(string text, CancellationToken cancellationToken);
}

public sealed class VoicingEmbeddingCache(
    IMemoryCache memoryCache,
    ITextEmbeddingService embeddingService,
    IConfiguration configuration,
    ILogger<VoicingEmbeddingCache> logger) : IVoicingEmbeddingCache
{
    private readonly MemoryCacheEntryOptions _cacheOptions = new()
    {
        AbsoluteExpirationRelativeToNow = GetExpiration(configuration),
        SlidingExpiration = TimeSpan.FromMinutes(5)
    };

    public async Task<double[]> GetOrCreateAsync(string text, CancellationToken cancellationToken)
    {
        if (memoryCache.TryGetValue(text, out double[]? cached))
        {
            logger.LogDebug("Voicing embedding cache hit for '{Text}'", text);
            return cached!;
        }

        var floats = await embeddingService.GenerateEmbeddingAsync(text, cancellationToken);
        var embedding = Array.ConvertAll(floats, static f => (double)f);

        memoryCache.Set(text, embedding, _cacheOptions);
        logger.LogDebug("Cached embedding for '{Text}' (dim={Dimension})", text, embedding.Length);

        return embedding;
    }

    private static TimeSpan GetExpiration(IConfiguration configuration)
    {
        var minutes = configuration.GetValue("VoicingSearch:EmbeddingCacheMinutes", 30);
        if (minutes <= 0)
        {
            minutes = 30;
        }

        return TimeSpan.FromMinutes(minutes);
    }
}
