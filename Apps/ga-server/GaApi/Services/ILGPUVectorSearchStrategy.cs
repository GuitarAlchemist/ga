namespace GaApi.Services;

using ILGPU;
using ILGPU.Runtime;
using System.Diagnostics;

/// <summary>
/// ILGPU-accelerated vector search strategy for GPU-based similarity calculations
/// Provides cross-platform GPU acceleration (NVIDIA, AMD, Intel)
/// Following ILGPU documentation: https://ilgpu.net/docs/01-primers/01-setting-up-ilgpu/
/// </summary>
public class ILGPUVectorSearchStrategy : IVectorSearchStrategy, IDisposable
{
    private readonly Dictionary<int, ChordEmbedding> _chords = new();
    private readonly object _initLock = new();
    private readonly ILogger<ILGPUVectorSearchStrategy> _logger;
    
    private Context? _context;
    private Accelerator? _accelerator;
    private Action<Index1D, ArrayView<double>, ArrayView<double>, ArrayView<double>, int, int>? _cosineSimilarityKernel;
    private Action<Index1D, ArrayView<double>, ArrayView<double>, ArrayView<int>, ArrayView<double>, int, int>? _filteredKernel;
    
    private MemoryBuffer<double>? _deviceEmbeddings;
    private MemoryBuffer<double>? _deviceQueryVector;
    private MemoryBuffer<double>? _deviceSimilarities;
    
    private int _embeddingDimensions = 384;
    private bool _isInitialized;
    private bool _isDisposed;
    private long _totalSearches;
    private TimeSpan _totalSearchTime = TimeSpan.Zero;

    public ILGPUVectorSearchStrategy(ILogger<ILGPUVectorSearchStrategy> logger)
    {
        _logger = logger;
        InitializeILGPU();
    }

    public string Name => "ILGPU";
    public bool IsAvailable { get; private set; }

    public VectorSearchPerformance Performance => new(
        TimeSpan.FromMilliseconds(0.5),
        CalculateGpuMemoryUsage(),
        true,
        false);

    public async Task InitializeAsync(IEnumerable<ChordEmbedding> chords)
    {
        if (!IsAvailable)
            throw new InvalidOperationException("ILGPU is not available on this system");

        await Task.Run(() =>
        {
            lock (_initLock)
            {
                if (_isInitialized)
                    return;

                _logger.LogInformation("Initializing ILGPU vector search...");
                var stopwatch = Stopwatch.StartNew();

                var chordList = chords.ToList();
                foreach (var chord in chordList)
                {
                    _chords[chord.Id] = chord;
                }

                CopyEmbeddingsToGpu(chordList);
                _isInitialized = true;

                stopwatch.Stop();
                _logger.LogInformation(
                    "ILGPU vector search initialized with {ChordCount} chords in {ElapsedMs}ms. GPU memory: {MemoryMB}MB",
                    _chords.Count, stopwatch.ElapsedMilliseconds, CalculateGpuMemoryUsage());
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
                var similarities = CalculateSimilaritiesILGPU(queryEmbedding);

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

            if (!_chords.TryGetValue(chordId, out var queryChord))
                throw new ArgumentException($"Chord with ID {chordId} not found");

            var stopwatch = Stopwatch.StartNew();

            try
            {
                var similarities = CalculateSimilaritiesILGPU(queryChord.Embedding)
                    .Where(x => x.ChordId != chordId);

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
                var filteredChords = _chords.Values.AsEnumerable();

                if (!string.IsNullOrEmpty(filters.Quality))
                    filteredChords = filteredChords.Where(c => c.Quality == filters.Quality);
                if (!string.IsNullOrEmpty(filters.Extension))
                    filteredChords = filteredChords.Where(c => c.Extension == filters.Extension);
                if (!string.IsNullOrEmpty(filters.StackingType))
                    filteredChords = filteredChords.Where(c => c.StackingType == filters.StackingType);
                if (filters.NoteCount.HasValue)
                    filteredChords = filteredChords.Where(c => c.NoteCount == filters.NoteCount.Value);

                var filteredIds = filteredChords.Select(c => c.Id).ToList();
                var similarities = CalculateFilteredSimilaritiesILGPU(queryEmbedding, filteredIds);

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
            CalculateGpuMemoryUsage(),
            _totalSearches > 0 ? TimeSpan.FromTicks(_totalSearchTime.Ticks / _totalSearches) : TimeSpan.Zero,
            _totalSearches);
    }

    private void InitializeILGPU()
    {
        try
        {
            _context = Context.CreateDefault();
            _accelerator = _context.CreateCudaAccelerator(0);
            
            if (_accelerator == null)
            {
                _logger.LogWarning("No CUDA accelerator found, trying CPU accelerator");
                _accelerator = _context.CreateCPUAccelerator(0);
            }

            _cosineSimilarityKernel = _accelerator.LoadStreamKernel(ILGPUKernels.CosineSimilarityKernel);
            _filteredKernel = _accelerator.LoadStreamKernel(ILGPUKernels.FilteredCosineSimilarityKernel);

            IsAvailable = true;
            _logger.LogInformation("ILGPU initialized successfully with {AcceleratorName}", _accelerator.Name);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to initialize ILGPU");
            IsAvailable = false;
        }
    }

    private void CopyEmbeddingsToGpu(List<ChordEmbedding> chords)
    {
        if (_accelerator == null)
            throw new InvalidOperationException("Accelerator not initialized");

        var embeddingDim = chords.First().Embedding.Length;
        _embeddingDimensions = embeddingDim;

        var flatEmbeddings = new double[chords.Count * embeddingDim];
        for (int i = 0; i < chords.Count; i++)
        {
            Array.Copy(chords[i].Embedding, 0, flatEmbeddings, i * embeddingDim, embeddingDim);
        }

        _deviceEmbeddings = _accelerator.Allocate1D(flatEmbeddings);
        _deviceQueryVector = _accelerator.Allocate1D<double>(embeddingDim);
        _deviceSimilarities = _accelerator.Allocate1D<double>(chords.Count);

        _logger.LogInformation("Copied {Count} embeddings to GPU", chords.Count);
    }

    private IEnumerable<(int ChordId, double Score)> CalculateSimilaritiesILGPU(double[] queryEmbedding)
    {
        if (_accelerator == null || _cosineSimilarityKernel == null || _deviceEmbeddings == null)
            throw new InvalidOperationException("ILGPU not properly initialized");

        _deviceQueryVector!.CopyFromCPU(queryEmbedding);
        _cosineSimilarityKernel(_chords.Count, _deviceQueryVector, _deviceEmbeddings, _deviceSimilarities!, _embeddingDimensions, _chords.Count);

        var similarities = _deviceSimilarities!.GetAsArray1D();
        return _chords.Keys.Select((id, idx) => (id, similarities[idx]));
    }

    private IEnumerable<(int ChordId, double Score)> CalculateFilteredSimilaritiesILGPU(double[] queryEmbedding, List<int> allowedIds)
    {
        if (_accelerator == null || _filteredKernel == null)
            throw new InvalidOperationException("ILGPU not properly initialized");

        var allowedIndices = allowedIds.Select(id => _chords.Keys.ToList().IndexOf(id)).ToArray();
        var deviceAllowedIndices = _accelerator.Allocate1D(allowedIndices);
        var deviceFilteredSimilarities = _accelerator.Allocate1D<double>(allowedIds.Count);

        _deviceQueryVector!.CopyFromCPU(queryEmbedding);
        _filteredKernel(allowedIds.Count, _deviceQueryVector, _deviceEmbeddings!, deviceAllowedIndices, deviceFilteredSimilarities, _embeddingDimensions, allowedIds.Count);

        var similarities = deviceFilteredSimilarities.GetAsArray1D();
        deviceAllowedIndices.Dispose();
        deviceFilteredSimilarities.Dispose();

        return allowedIds.Select((id, idx) => (id, similarities[idx]));
    }

    private void EnsureInitialized()
    {
        if (!_isInitialized)
            throw new InvalidOperationException("ILGPU vector search not initialized. Call InitializeAsync first.");
    }

    private long CalculateGpuMemoryUsage()
    {
        if (_chords.Count == 0)
            return 0;

        var bytesPerEmbedding = _embeddingDimensions * sizeof(double);
        return _chords.Count * bytesPerEmbedding / (1024 * 1024);
    }

    private void RecordSearchTime(TimeSpan elapsed)
    {
        Interlocked.Increment(ref _totalSearches);
        lock (_initLock)
        {
            _totalSearchTime = _totalSearchTime.Add(elapsed);
        }
    }

    public void Dispose()
    {
        if (_isDisposed)
            return;

        _deviceEmbeddings?.Dispose();
        _deviceQueryVector?.Dispose();
        _deviceSimilarities?.Dispose();
        _accelerator?.Dispose();
        _context?.Dispose();

        _isDisposed = true;
    }
}

