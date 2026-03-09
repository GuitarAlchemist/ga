namespace GA.Business.ML.Agents;

using GA.Data.MongoDB.Models;
using GA.Data.MongoDB.Services;
using Microsoft.Extensions.Logging;
using MongoDB.Driver;

/// <summary>
/// MongoDB-backed <see cref="IRoutingFeedback"/> that persists corrections across process restarts.
/// </summary>
/// <remarks>
/// On first use the implementation lazily loads all existing corrections from MongoDB into an
/// in-memory bias cache. Subsequent reads are served entirely from memory. Writes are committed
/// to MongoDB and then applied to the cache atomically under the semaphore.
/// </remarks>
public sealed class MongoRoutingFeedback(
    MongoDbService mongoDb,
    ILogger<MongoRoutingFeedback> logger) : IRoutingFeedback, IAsyncDisposable
{
    private const float BiasStep = 0.05f;
    private const float MaxBias = 0.15f;

    private readonly Dictionary<string, int> _correctionCounts = [];
    private readonly List<FeedbackEntry> _entries = [];
    private readonly SemaphoreSlim _semaphore = new(1, 1);

    private volatile bool _loaded;

    // ---------------------------------------------------------------------------
    // IRoutingFeedback
    // ---------------------------------------------------------------------------

    /// <inheritdoc/>
    public async Task RecordCorrectionAsync(
        string query,
        string wrongAgentId,
        string correctAgentId,
        CancellationToken cancellationToken = default)
    {
        await EnsureLoadedAsync(cancellationToken);

        var timestamp = DateTimeOffset.UtcNow;
        var doc = new RoutingFeedbackDocument
        {
            Id = RoutingFeedbackDocument.BuildId(wrongAgentId, correctAgentId, timestamp),
            Query = query,
            WrongAgentId = wrongAgentId,
            CorrectAgentId = correctAgentId,
            Timestamp = timestamp
        };

        try
        {
            // InsertOne — duplicate key is silently ignored on retry via upsert option
            await mongoDb.RoutingFeedback.ReplaceOneAsync(
                filter: Builders<RoutingFeedbackDocument>.Filter.Eq(d => d.Id, doc.Id),
                replacement: doc,
                options: new ReplaceOptions { IsUpsert = true },
                cancellationToken: cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex,
                "Failed to persist routing correction ({Wrong} → {Correct}) — bias will still be updated in memory",
                wrongAgentId, correctAgentId);
        }

        await _semaphore.WaitAsync(cancellationToken);
        try
        {
            ApplyCorrection(query, wrongAgentId, correctAgentId, timestamp);
        }
        finally
        {
            _semaphore.Release();
        }
    }

    /// <inheritdoc/>
    public float GetBias(string agentId)
    {
        // Synchronous: return cached value; EnsureLoadedAsync fires on the write path.
        // Callers that need guaranteed freshness should await RecordCorrectionAsync first.
        _semaphore.Wait();
        try
        {
            var count = _correctionCounts.GetValueOrDefault(agentId);
            return Math.Clamp(count * BiasStep, -MaxBias, MaxBias);
        }
        finally
        {
            _semaphore.Release();
        }
    }

    /// <inheritdoc/>
    public IReadOnlyList<FeedbackEntry> GetEntries()
    {
        _semaphore.Wait();
        try
        {
            return [.. _entries];
        }
        finally
        {
            _semaphore.Release();
        }
    }

    // ---------------------------------------------------------------------------
    // IAsyncDisposable
    // ---------------------------------------------------------------------------

    public ValueTask DisposeAsync()
    {
        _semaphore.Dispose();
        return ValueTask.CompletedTask;
    }

    // ---------------------------------------------------------------------------
    // Private helpers
    // ---------------------------------------------------------------------------

    /// <summary>
    /// Lazily loads all existing corrections from MongoDB into the in-memory cache.
    /// Uses double-checked locking to avoid repeated DB round-trips.
    /// </summary>
    private async Task EnsureLoadedAsync(CancellationToken cancellationToken)
    {
        if (_loaded) return;

        await _semaphore.WaitAsync(cancellationToken);
        try
        {
            if (_loaded) return;

            logger.LogDebug("Loading routing feedback from MongoDB...");

            var cursor = await mongoDb.RoutingFeedback
                .FindAsync(Builders<RoutingFeedbackDocument>.Filter.Empty, cancellationToken: cancellationToken);

            var documents = await cursor.ToListAsync(cancellationToken);

            foreach (var d in documents)
            {
                ApplyCorrection(d.Query, d.WrongAgentId, d.CorrectAgentId, d.Timestamp);
            }

            _loaded = true;

            logger.LogInformation("Loaded {Count} routing feedback entries from MongoDB", documents.Count);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to load routing feedback from MongoDB — starting with empty cache");
            _loaded = true; // Prevent repeated failed attempts
        }
        finally
        {
            _semaphore.Release();
        }
    }

    /// <summary>
    /// Applies a single correction to the in-memory collections.
    /// Must be called while holding <see cref="_semaphore"/>.
    /// </summary>
    private void ApplyCorrection(
        string query,
        string wrongAgentId,
        string correctAgentId,
        DateTimeOffset timestamp)
    {
        _correctionCounts[wrongAgentId] = _correctionCounts.GetValueOrDefault(wrongAgentId) - 1;
        _correctionCounts[correctAgentId] = _correctionCounts.GetValueOrDefault(correctAgentId) + 1;

        _entries.Add(new FeedbackEntry(
            Timestamp: timestamp,
            Query: query,
            WrongAgentId: wrongAgentId,
            CorrectAgentId: correctAgentId));
    }
}
