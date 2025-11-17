namespace GA.Business.Core.AI.Services.Embeddings;

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

/// <summary>
/// Caches embeddings produced by <see cref="IEmbeddingService"/> calls.
/// </summary>
public interface IVoicingEmbeddingCache
{
    Task<double[]> GetOrCreateAsync(string text, CancellationToken cancellationToken);
}

public sealed class VoicingEmbeddingCache(
    IMemoryCache memoryCache,
    IEmbeddingService embeddingService,
    IConfiguration configuration,
    ILogger<VoicingEmbeddingCache> logger) : IVoicingEmbeddingCache
{
    private readonly IMemoryCache _memoryCache = memoryCache;
    private readonly IEmbeddingService _embeddingService = embeddingService;
    private readonly ILogger<VoicingEmbeddingCache> _logger = logger;
    private readonly MemoryCacheEntryOptions _cacheOptions = new()
    {
        AbsoluteExpirationRelativeToNow = GetExpiration(configuration),
        SlidingExpiration = TimeSpan.FromMinutes(5)
    };

    public async Task<double[]> GetOrCreateAsync(string text, CancellationToken cancellationToken)
    {
        if (_memoryCache.TryGetValue(text, out double[]? cached))
        {
            _logger.LogDebug("Voicing embedding cache hit for '{Text}'", text);
            return cached!;
        }

        var floats = await _embeddingService.GenerateEmbeddingAsync(text, cancellationToken);
        var embedding = Array.ConvertAll(floats, static f => (double)f);

        _memoryCache.Set(text, embedding, _cacheOptions);
        _logger.LogDebug("Cached embedding for '{Text}' (dim={Dimension})", text, embedding.Length);

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
