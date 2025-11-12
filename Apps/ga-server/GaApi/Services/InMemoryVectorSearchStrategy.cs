namespace GaApi.Services;

using System.Collections.Concurrent;
using System.Diagnostics;
using System.Numerics;

/// <summary>
///     High-performance in-memory vector search using SIMD operations
/// </summary>
public class InMemoryVectorSearchStrategy(ILogger<InMemoryVectorSearchStrategy> logger) : IVectorSearchStrategy
{
    private readonly ConcurrentDictionary<int, ChordEmbedding> _chords = new();
    private readonly Dictionary<int, double[]> _embeddings = new();
    private readonly Lock _initLock = new();
    private bool _isInitialized;
    private long _totalSearches;
    private TimeSpan _totalSearchTime = TimeSpan.Zero;

    public string Name => "InMemory";
    public bool IsAvailable => true;

    public VectorSearchPerformance Performance => new(
        TimeSpan.FromMilliseconds(5), // Very fast
        CalculateMemoryUsage(),
        false,
        false);

    public async Task InitializeAsync(IEnumerable<ChordEmbedding> chords)
    {
        await Task.Run(() =>
        {
            lock (_initLock)
            {
                if (_isInitialized)
                {
                    return;
                }

                logger.LogInformation("Initializing in-memory vector search...");
                var stopwatch = Stopwatch.StartNew();

                foreach (var chord in chords)
                {
                    _chords[chord.Id] = chord;
                    _embeddings[chord.Id] = chord.Embedding;
                }

                stopwatch.Stop();
                _isInitialized = true;

                logger.LogInformation(
                    "In-memory vector search initialized with {ChordCount} chords in {ElapsedMs}ms. Memory usage: {MemoryMB}MB",
                    _chords.Count, stopwatch.ElapsedMilliseconds, CalculateMemoryUsage());
            }
        });
    }

    public async Task<List<ChordSearchResult>> SemanticSearchAsync(
        double[] queryEmbedding,
        int limit = 10,
        int numCandidates = 100)
    {
        return await Task.Run(() =>
        {
            EnsureInitialized();
            var stopwatch = Stopwatch.StartNew();

            try
            {
                var similarities = CalculateAllSimilarities(queryEmbedding);
                var topResults = similarities
                    .OrderByDescending(x => x.Score)
                    .Take(limit)
                    .Select(x => new ChordSearchResult
                    {
                        Id = x.ChordId,
                        Name = _chords[x.ChordId].Name,
                        Quality = _chords[x.ChordId].Quality,
                        Extension = _chords[x.ChordId].Extension,
                        StackingType = _chords[x.ChordId].StackingType,
                        NoteCount = _chords[x.ChordId].NoteCount,
                        Description = _chords[x.ChordId].Description,
                        Score = x.Score
                    })
                    .ToList();

                return topResults;
            }
            finally
            {
                stopwatch.Stop();
                RecordSearchTime(stopwatch.Elapsed);
            }
        });
    }

    public async Task<List<ChordSearchResult>> FindSimilarChordsAsync(
        int chordId,
        int limit = 10,
        int numCandidates = 100)
    {
        return await Task.Run(() =>
        {
            EnsureInitialized();

            if (!_embeddings.TryGetValue(chordId, out var queryEmbedding))
            {
                throw new ArgumentException($"Chord with ID {chordId} not found");
            }

            var stopwatch = Stopwatch.StartNew();

            try
            {
                var similarities = CalculateAllSimilarities(queryEmbedding)
                    .Where(x => x.ChordId != chordId); // Exclude the query chord itself

                var topResults = similarities
                    .OrderByDescending(x => x.Score)
                    .Take(limit)
                    .Select(x => new ChordSearchResult
                    {
                        Id = x.ChordId,
                        Name = _chords[x.ChordId].Name,
                        Quality = _chords[x.ChordId].Quality,
                        Extension = _chords[x.ChordId].Extension,
                        StackingType = _chords[x.ChordId].StackingType,
                        NoteCount = _chords[x.ChordId].NoteCount,
                        Description = _chords[x.ChordId].Description,
                        Score = x.Score
                    })
                    .ToList();

                return topResults;
            }
            finally
            {
                stopwatch.Stop();
                RecordSearchTime(stopwatch.Elapsed);
            }
        });
    }

    public async Task<List<ChordSearchResult>> HybridSearchAsync(
        double[] queryEmbedding,
        ChordSearchFilters filters,
        int limit = 10,
        int numCandidates = 100)
    {
        return await Task.Run(() =>
        {
            EnsureInitialized();
            var stopwatch = Stopwatch.StartNew();

            try
            {
                // First apply filters
                var filteredChords = _chords.Values.AsEnumerable();

                if (!string.IsNullOrEmpty(filters.Quality))
                {
                    filteredChords = filteredChords.Where(c => c.Quality == filters.Quality);
                }

                if (!string.IsNullOrEmpty(filters.Extension))
                {
                    filteredChords = filteredChords.Where(c => c.Extension == filters.Extension);
                }

                if (!string.IsNullOrEmpty(filters.StackingType))
                {
                    filteredChords = filteredChords.Where(c => c.StackingType == filters.StackingType);
                }

                if (filters.NoteCount.HasValue)
                {
                    filteredChords = filteredChords.Where(c => c.NoteCount == filters.NoteCount.Value);
                }

                var filteredIds = filteredChords.Select(c => c.Id).ToHashSet();

                // Then calculate similarities only for filtered chords
                var similarities = CalculateFilteredSimilarities(queryEmbedding, filteredIds);

                var topResults = similarities
                    .OrderByDescending(x => x.Score)
                    .Take(limit)
                    .Select(x => new ChordSearchResult
                    {
                        Id = x.ChordId,
                        Name = _chords[x.ChordId].Name,
                        Quality = _chords[x.ChordId].Quality,
                        Extension = _chords[x.ChordId].Extension,
                        StackingType = _chords[x.ChordId].StackingType,
                        NoteCount = _chords[x.ChordId].NoteCount,
                        Description = _chords[x.ChordId].Description,
                        Score = x.Score
                    })
                    .ToList();

                return topResults;
            }
            finally
            {
                stopwatch.Stop();
                RecordSearchTime(stopwatch.Elapsed);
            }
        });
    }

    public VectorSearchStats GetStats()
    {
        return new VectorSearchStats(
            _chords.Count,
            CalculateMemoryUsage(),
            _totalSearches > 0 ? TimeSpan.FromTicks(_totalSearchTime.Ticks / _totalSearches) : TimeSpan.Zero,
            _totalSearches);
    }

    private void EnsureInitialized()
    {
        if (!_isInitialized)
        {
            throw new InvalidOperationException("Vector search strategy not initialized. Call InitializeAsync first.");
        }
    }

    private IEnumerable<(int ChordId, double Score)> CalculateAllSimilarities(double[] queryEmbedding)
    {
        return _embeddings.Select(kvp =>
            (kvp.Key, CalculateCosineSimilarity(queryEmbedding, kvp.Value)));
    }

    private IEnumerable<(int ChordId, double Score)> CalculateFilteredSimilarities(
        double[] queryEmbedding,
        HashSet<int> allowedIds)
    {
        return _embeddings
            .Where(kvp => allowedIds.Contains(kvp.Key))
            .Select(kvp => (kvp.Key, CalculateCosineSimilarity(queryEmbedding, kvp.Value)));
    }

    /// <summary>
    ///     Calculate cosine similarity using SIMD operations for performance
    /// </summary>
    private static double CalculateCosineSimilarity(double[] a, double[] b)
    {
        if (a.Length != b.Length)
        {
            throw new ArgumentException("Vectors must have the same length");
        }

        // Use SIMD operations when available
        if (Vector.IsHardwareAccelerated && a.Length >= Vector<double>.Count)
        {
            return CalculateCosineSimilaritySimd(a, b);
        }

        // Fallback to standard calculation
        return CalculateCosineSimilarityStandard(a, b);
    }

    private static double CalculateCosineSimilaritySimd(double[] a, double[] b)
    {
        var dotProduct = 0.0;
        var normA = 0.0;
        var normB = 0.0;

        var vectorSize = Vector<double>.Count;
        var vectorizedLength = a.Length - a.Length % vectorSize;

        // SIMD operations for the bulk of the calculation
        for (var i = 0; i < vectorizedLength; i += vectorSize)
        {
            var va = new Vector<double>(a, i);
            var vb = new Vector<double>(b, i);

            dotProduct += Vector.Dot(va, vb);
            normA += Vector.Dot(va, va);
            normB += Vector.Dot(vb, vb);
        }

        // Handle remaining elements
        for (var i = vectorizedLength; i < a.Length; i++)
        {
            dotProduct += a[i] * b[i];
            normA += a[i] * a[i];
            normB += b[i] * b[i];
        }

        var magnitude = Math.Sqrt(normA) * Math.Sqrt(normB);
        return magnitude > 0 ? dotProduct / magnitude : 0;
    }

    private static double CalculateCosineSimilarityStandard(double[] a, double[] b)
    {
        var dotProduct = 0.0;
        var normA = 0.0;
        var normB = 0.0;

        for (var i = 0; i < a.Length; i++)
        {
            dotProduct += a[i] * b[i];
            normA += a[i] * a[i];
            normB += b[i] * b[i];
        }

        var magnitude = Math.Sqrt(normA) * Math.Sqrt(normB);
        return magnitude > 0 ? dotProduct / magnitude : 0;
    }

    private long CalculateMemoryUsage()
    {
        if (_chords.Count == 0)
        {
            return 0;
        }

        // Rough calculation: each chord + embedding
        var avgEmbeddingSize = _embeddings.Values.FirstOrDefault()?.Length ?? 384;
        var bytesPerChord = avgEmbeddingSize * sizeof(double) + 1024; // embedding + metadata
        return _chords.Count * bytesPerChord / (1024 * 1024); // Convert to MB
    }

    private void RecordSearchTime(TimeSpan elapsed)
    {
        Interlocked.Increment(ref _totalSearches);
        lock (_initLock)
        {
            _totalSearchTime = _totalSearchTime.Add(elapsed);
        }
    }
}
