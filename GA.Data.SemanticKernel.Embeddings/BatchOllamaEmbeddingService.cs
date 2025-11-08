namespace GA.Data.SemanticKernel.Embeddings;

using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

/// <summary>
///     High-performance batch embedding service for Ollama
///     Optimized for throughput with concurrent requests and intelligent batching
/// </summary>
public class BatchOllamaEmbeddingService : IBatchEmbeddingService
{
    private readonly SemaphoreSlim _concurrencyLimiter;
    private readonly HttpClient _httpClient;
    private readonly ILogger<BatchOllamaEmbeddingService> _logger;
    private readonly int _maxConcurrentRequests;
    private readonly string _modelName;

    public BatchOllamaEmbeddingService(
        HttpClient httpClient,
        string modelName = "nomic-embed-text",
        int maxConcurrentRequests = 10,
        ILogger<BatchOllamaEmbeddingService>? logger = null)
    {
        _httpClient = httpClient;
        _modelName = modelName;
        _logger = logger ?? NullLogger<BatchOllamaEmbeddingService>.Instance;
        _maxConcurrentRequests = maxConcurrentRequests;
        _concurrencyLimiter = new SemaphoreSlim(maxConcurrentRequests, maxConcurrentRequests);
    }

    /// <summary>
    ///     Generate embeddings for multiple texts concurrently
    /// </summary>
    public async Task<float[][]> GenerateBatchEmbeddingsAsync(string[] texts,
        CancellationToken cancellationToken = default)
    {
        if (texts.Length == 0)
        {
            return Array.Empty<float[]>();
        }

        _logger.LogDebug("Generating embeddings for {Count} texts", texts.Length);

        // For small batches, use concurrent individual requests
        if (texts.Length <= _maxConcurrentRequests)
        {
            return await GenerateConcurrentEmbeddingsAsync(texts, cancellationToken);
        }

        // For large batches, chunk them
        var results = new float[texts.Length][];
        var chunks = ChunkArray(texts, _maxConcurrentRequests);
        var currentIndex = 0;

        foreach (var chunk in chunks)
        {
            var chunkResults = await GenerateConcurrentEmbeddingsAsync(chunk, cancellationToken);
            Array.Copy(chunkResults, 0, results, currentIndex, chunkResults.Length);
            currentIndex += chunkResults.Length;
        }

        return results;
    }

    /// <summary>
    ///     Generate embedding for a single text
    /// </summary>
    public async Task<float[]> GenerateEmbeddingAsync(string text, CancellationToken cancellationToken = default)
    {
        var results = await GenerateBatchEmbeddingsAsync(new[] { text }, cancellationToken);
        return results[0];
    }

    /// <summary>
    ///     Generate embeddings concurrently for a batch of texts
    /// </summary>
    private async Task<float[][]> GenerateConcurrentEmbeddingsAsync(string[] texts, CancellationToken cancellationToken)
    {
        var tasks = texts.Select(async (text, index) =>
        {
            await _concurrencyLimiter.WaitAsync(cancellationToken);
            try
            {
                return await GenerateSingleEmbeddingAsync(text, cancellationToken);
            }
            finally
            {
                _concurrencyLimiter.Release();
            }
        });

        return await Task.WhenAll(tasks);
    }

    /// <summary>
    ///     Generate embedding for a single text via Ollama API
    /// </summary>
    private async Task<float[]> GenerateSingleEmbeddingAsync(string text, CancellationToken cancellationToken)
    {
        try
        {
            var request = new OllamaEmbeddingRequest
            {
                Model = _modelName,
                Prompt = text
            };

            var response = await _httpClient.PostAsJsonAsync("/api/embeddings", request, cancellationToken);
            response.EnsureSuccessStatusCode();

            var result = await response.Content.ReadFromJsonAsync<OllamaEmbeddingResponse>(cancellationToken);

            if (result?.Embedding == null)
            {
                throw new InvalidOperationException(
                    $"Invalid embedding response for text: {text[..Math.Min(50, text.Length)]}...");
            }

            return result.Embedding;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to generate embedding for text: {Text}", text[..Math.Min(50, text.Length)]);
            throw;
        }
    }

    /// <summary>
    ///     Chunk array into smaller arrays
    /// </summary>
    private static T[][] ChunkArray<T>(T[] array, int chunkSize)
    {
        var chunks = new List<T[]>();
        for (var i = 0; i < array.Length; i += chunkSize)
        {
            var chunk = new T[Math.Min(chunkSize, array.Length - i)];
            Array.Copy(array, i, chunk, 0, chunk.Length);
            chunks.Add(chunk);
        }

        return chunks.ToArray();
    }

    public void Dispose()
    {
        _concurrencyLimiter?.Dispose();
    }
}

/// <summary>
///     Request model for Ollama embedding API
/// </summary>
internal class OllamaEmbeddingRequest
{
    [JsonPropertyName("model")] public string Model { get; set; } = string.Empty;

    [JsonPropertyName("prompt")] public string Prompt { get; set; } = string.Empty;
}

/// <summary>
///     Response model for Ollama embedding API
/// </summary>
internal class OllamaEmbeddingResponse
{
    [JsonPropertyName("embedding")] public float[]? Embedding { get; set; }
}
