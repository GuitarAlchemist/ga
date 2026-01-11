namespace GA.Business.Core.Fretboard.Voicings.Search;

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Core;
using ILGPU;
using ILGPU.Runtime;

/// <summary>
/// GPU-accelerated voicing search strategy for high-performance similarity calculations
/// Uses ILGPU framework for cross-platform GPU acceleration (NVIDIA CUDA, AMD, Intel)
/// </summary>
public class GpuVoicingSearchStrategy : IVoicingSearchStrategy, IDisposable
{
    private readonly Dictionary<string, VoicingEmbedding> _voicings = new();
    private readonly Lock _initLock = new();

    private Context? _context;
    private Accelerator? _accelerator;
    private Action<Index1D, ArrayView<double>, ArrayView<double>, ArrayView<double>, int, int>? _cosineSimilarityKernel;
    private Action<Index1D, ArrayView<double>, ArrayView<double>, ArrayView<int>, ArrayView<double>, int, int>? _filteredCosineSimilarityKernel;

    private double[]? _hostEmbeddings;
    private string[]? _voicingIds;
    private MemoryBuffer1D<double, Stride1D.Dense>? _deviceEmbeddings; // Pre-allocated GPU buffer for embeddings

    private int _embeddingDimensions = 384;
    private bool _isInitialized;
    private bool _isDisposed;
    private long _totalSearches;
    private TimeSpan _totalSearchTime = TimeSpan.Zero;

    public GpuVoicingSearchStrategy()
    {
        InitializeIlgpu();
    }

    public string Name => "GPU";
    public bool IsAvailable { get; private set; }

    public VoicingSearchPerformance Performance => new(
        TimeSpan.FromMilliseconds(0.5),
        CalculateGpuMemoryUsage(),
        true,
        false);

    public async Task InitializeAsync(IEnumerable<VoicingEmbedding> voicings)
    {
        if (!IsAvailable)
            throw new InvalidOperationException("ILGPU is not available on this system");

        await Task.Run(() =>
        {
            lock (_initLock)
            {
                if (_isInitialized)
                    return;

                Trace.TraceInformation("Initializing GPU voicing search...");
                var stopwatch = Stopwatch.StartNew();

                var voicingList = voicings.ToList();
                foreach (var voicing in voicingList)
                {
                    _voicings[voicing.Id] = voicing;
                }

                CopyEmbeddingsToGpu(voicingList);
                _isInitialized = true;

                stopwatch.Stop();
                Trace.TraceInformation(
                    $"GPU voicing search initialized with {_voicings.Count} voicings in {stopwatch.ElapsedMilliseconds}ms. GPU memory: {CalculateGpuMemoryUsage()}MB");
            }
        });
    }

    public async Task<List<VoicingSearchResult>> SemanticSearchAsync(
        double[] queryEmbedding,
        int limit = 10)
    {
        return await Task.Run(() =>
        {
            EnsureInitialized();
            var stopwatch = Stopwatch.StartNew();

            try
            {
                var similarities = CalculateSimilaritiesIlgpu(queryEmbedding);

                var topResults = similarities
                    .OrderByDescending(x => x.Score)
                    .Take(limit)
                    .Select(x => MapToSearchResult(_voicings[x.VoicingId], x.Score, "semantic search"))
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

    public async Task<List<VoicingSearchResult>> FindSimilarVoicingsAsync(
        string voicingId,
        int limit = 10)
    {
        return await Task.Run(() =>
        {
            EnsureInitialized();

            if (!_voicings.TryGetValue(voicingId, out var queryVoicing))
                throw new ArgumentException($"Voicing with ID {voicingId} not found");

            var stopwatch = Stopwatch.StartNew();

            try
            {
                var similarities = CalculateSimilaritiesIlgpu(queryVoicing.Embedding)
                    .Where(x => x.VoicingId != voicingId);

                var topResults = similarities
                    .OrderByDescending(x => x.Score)
                    .Take(limit)
                    .Select(x => MapToSearchResult(_voicings[x.VoicingId], x.Score, $"similar to {voicingId}"))
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

    public async Task<List<VoicingSearchResult>> HybridSearchAsync(
        double[] queryEmbedding,
        VoicingSearchFilters filters,
        int limit = 10)
    {
        return await Task.Run(() =>
        {
            EnsureInitialized();
            var stopwatch = Stopwatch.StartNew();

            try
            {
                var filteredVoicings = _voicings.Values.AsEnumerable();

                // Basic filters
                if (!string.IsNullOrEmpty(filters.Difficulty))
                    filteredVoicings = filteredVoicings.Where(v => v.Difficulty == filters.Difficulty);
                if (!string.IsNullOrEmpty(filters.Position))
                    filteredVoicings = filteredVoicings.Where(v => v.Position == filters.Position);
                if (!string.IsNullOrEmpty(filters.VoicingType))
                    filteredVoicings = filteredVoicings.Where(v => v.VoicingType == filters.VoicingType);
                if (!string.IsNullOrEmpty(filters.ModeName))
                    filteredVoicings = filteredVoicings.Where(v => v.ModeName == filters.ModeName);
                if (filters.MinFret.HasValue)
                    filteredVoicings = filteredVoicings.Where(v => v.MinFret >= filters.MinFret.Value);
                if (filters.MaxFret.HasValue)
                    filteredVoicings = filteredVoicings.Where(v => v.MaxFret <= filters.MaxFret.Value);
                if (filters.RequireBarreChord.HasValue)
                    filteredVoicings = filteredVoicings.Where(v => v.BarreRequired == filters.RequireBarreChord.Value);
                if (filters.Tags != null && filters.Tags.Length > 0)
                    filteredVoicings = filteredVoicings.Where(v => filters.Tags.Any(tag => v.SemanticTags.Contains(tag)));

                // Biomechanical filters (Quick Win 1)
                if (filters.HandSize.HasValue || filters.MaxFingerStretch.HasValue ||
                    filters.MinComfortScore.HasValue || filters.MustBeErgonomic.HasValue)
                {
                    filteredVoicings = ApplyBiomechanicalFilters(filteredVoicings, filters);
                }

                // Musical characteristic filters (Quick Win 3) and Extended Filters (Phase 3)
                if (filters.IsOpenVoicing.HasValue || filters.IsRootless.HasValue ||
                    !string.IsNullOrEmpty(filters.DropVoicing) || !string.IsNullOrEmpty(filters.CagedShape) ||
                    !string.IsNullOrEmpty(filters.HarmonicFunction) || filters.IsNaturallyOccurring.HasValue ||
                    filters.HasGuideTones.HasValue || filters.Inversion.HasValue ||
                    filters.MinConsonance.HasValue || filters.MinBrightness.HasValue || filters.MaxBrightness.HasValue ||
                    (filters.OmittedTones != null && filters.OmittedTones.Length > 0))
                {
                    filteredVoicings = ApplyMusicalFilters(filteredVoicings, filters);
                }

                var filteredIds = filteredVoicings.Select(v => v.Id).ToList();
                var similarities = CalculateFilteredSimilaritiesIlgpu(queryEmbedding, filteredIds);

                var topResults = similarities
                    .OrderByDescending(x => x.Score)
                    .Take(limit)
                    .Select(x => MapToSearchResult(_voicings[x.VoicingId], x.Score, "hybrid search"))
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

    public VoicingSearchStats GetStats()
    {
        return new(
            _voicings.Count,
            CalculateGpuMemoryUsage(),
            _totalSearches > 0 ? TimeSpan.FromTicks(_totalSearchTime.Ticks / _totalSearches) : TimeSpan.Zero,
            _totalSearches);
    }

    private void InitializeIlgpu()
    {
        try
        {
            // Initialize ILGPU context
            _context = Context.CreateDefault();

            // Try to get a GPU accelerator first
            foreach (var device in _context.Devices)
            {
                if (device.AcceleratorType == AcceleratorType.Cuda ||
                    device.AcceleratorType == AcceleratorType.OpenCL)
                {
                    _accelerator = device.CreateAccelerator(_context);
                    Trace.TraceInformation($"GPU initialized with {device.AcceleratorType} accelerator: {device.Name}");

                    // Load and compile GPU kernels
                    LoadKernels();

                    IsAvailable = true;
                    return;
                }
            }

            // Fall back to CPU if no GPU available
            _accelerator = _context.GetPreferredDevice(preferCPU: true).CreateAccelerator(_context);
            Trace.TraceInformation("GPU initialized with CPU accelerator (no GPU available)");
            IsAvailable = true;
        }
        catch (Exception ex)
        {
            Trace.TraceWarning($"Failed to initialize GPU context: {ex.Message}");
            IsAvailable = false;
        }
    }

    private void LoadKernels()
    {
        if (_accelerator == null)
            return;

        try
        {
            // Load cosine similarity kernel
            _cosineSimilarityKernel = _accelerator.LoadAutoGroupedStreamKernel<
                Index1D, ArrayView<double>, ArrayView<double>, ArrayView<double>, int, int>(
                CosineSimilarityKernel);

            // Load filtered cosine similarity kernel
            _filteredCosineSimilarityKernel = _accelerator.LoadAutoGroupedStreamKernel<
                Index1D, ArrayView<double>, ArrayView<double>, ArrayView<int>, ArrayView<double>, int, int>(
                FilteredCosineSimilarityKernel);

            Trace.TraceInformation("GPU kernels loaded successfully");
        }
        catch (Exception ex)
        {
            Trace.TraceWarning($"Failed to load GPU kernels, will fall back to CPU: {ex.Message}");
        }
    }

    /// <summary>
    /// GPU kernel for cosine similarity calculation
    /// </summary>
    private static void CosineSimilarityKernel(
        Index1D index,
        ArrayView<double> queryVector,
        ArrayView<double> voicingEmbeddings,
        ArrayView<double> similarities,
        int embeddingDim,
        int numVoicings)
    {
        if (index >= numVoicings)
            return;

        var voicingOffset = index * embeddingDim;

        var dotProduct = 0.0;
        var queryNorm = 0.0;
        var voicingNorm = 0.0;

        for (var i = 0; i < embeddingDim; i++)
        {
            var queryVal = queryVector[i];
            var voicingVal = voicingEmbeddings[voicingOffset + i];

            dotProduct += queryVal * voicingVal;
            queryNorm += queryVal * queryVal;
            voicingNorm += voicingVal * voicingVal;
        }

        var magnitude = Math.Sqrt(queryNorm) * Math.Sqrt(voicingNorm);
        similarities[index] = magnitude > 0 ? dotProduct / magnitude : 0.0;
    }

    /// <summary>
    /// GPU kernel for filtered cosine similarity (only compute for allowed voicing indices)
    /// </summary>
    private static void FilteredCosineSimilarityKernel(
        Index1D index,
        ArrayView<double> queryVector,
        ArrayView<double> voicingEmbeddings,
        ArrayView<int> allowedIndices,
        ArrayView<double> similarities,
        int embeddingDim,
        int numAllowedVoicings)
    {
        if (index >= numAllowedVoicings)
            return;

        var voicingIdx = allowedIndices[index];
        var voicingOffset = voicingIdx * embeddingDim;

        var dotProduct = 0.0;
        var queryNorm = 0.0;
        var voicingNorm = 0.0;

        for (var i = 0; i < embeddingDim; i++)
        {
            var queryVal = queryVector[i];
            var voicingVal = voicingEmbeddings[voicingOffset + i];

            dotProduct += queryVal * voicingVal;
            queryNorm += queryVal * queryVal;
            voicingNorm += voicingVal * voicingVal;
        }

        var magnitude = Math.Sqrt(queryNorm) * Math.Sqrt(voicingNorm);
        similarities[index] = magnitude > 0 ? dotProduct / magnitude : 0.0;
    }

    private void CopyEmbeddingsToGpu(List<VoicingEmbedding> voicings)
    {
        if (_accelerator == null)
            throw new InvalidOperationException("Accelerator not initialized");

        var embeddingDim = voicings.First().Embedding.Length;
        _embeddingDimensions = embeddingDim;

        // Store embeddings in host memory for GPU transfer
        _hostEmbeddings = new double[voicings.Count * embeddingDim];
        _voicingIds = new string[voicings.Count];

        for (var i = 0; i < voicings.Count; i++)
        {
            Array.Copy(voicings[i].Embedding, 0, _hostEmbeddings, i * embeddingDim, embeddingDim);
            _voicingIds[i] = voicings[i].Id;
        }

        // Pre-allocate GPU memory for embeddings (one-time cost, reused for all searches)
        if (_accelerator.AcceleratorType != AcceleratorType.CPU && _cosineSimilarityKernel != null)
        {
            _deviceEmbeddings = _accelerator.Allocate1D(_hostEmbeddings);
            var memoryMB = _hostEmbeddings.Length * sizeof(double) / (1024.0 * 1024.0);
            Trace.TraceInformation($"Pre-allocated {memoryMB:F2} MB GPU memory for {voicings.Count} embeddings");
        }
        else
        {
            Trace.TraceInformation($"Prepared {voicings.Count} embeddings for CPU acceleration");
        }
    }

    private IEnumerable<(string VoicingId, double Score)> CalculateSimilaritiesIlgpu(double[] queryEmbedding)
    {
        if (_accelerator == null || _hostEmbeddings == null || _voicingIds == null)
            throw new InvalidOperationException("GPU not properly initialized");

        if (_isDisposed || _accelerator.AcceleratorType == AcceleratorType.CPU || _cosineSimilarityKernel == null || _deviceEmbeddings == null)
        {
            // Fall back to CPU computation if GPU is not available or disposed
            Trace.WriteLine("Using CPU fallback for similarity calculation");
            return CalculateSimilaritiesCpu(queryEmbedding);
        }

        try
        {
            // Allocate GPU memory only for query vector and results (embeddings are pre-allocated)
            using var deviceQueryVector = _accelerator.Allocate1D(queryEmbedding);
            using var deviceSimilarities = _accelerator.Allocate1D<double>(_voicings.Count);

            // Launch GPU kernel for parallel cosine similarity calculation
            _cosineSimilarityKernel(_voicings.Count, deviceQueryVector.View, _deviceEmbeddings.View,
                deviceSimilarities.View, _embeddingDimensions, _voicings.Count);

            // Wait for GPU to complete
            _accelerator.Synchronize();

            // Copy results back to CPU
            var similarities = deviceSimilarities.GetAsArray1D();

            return _voicingIds.Select((id, idx) => (id, similarities[idx]));
        }
        catch (Exception ex)
        {
            Trace.TraceWarning($"GPU kernel execution failed, falling back to CPU: {ex.Message}");
            return CalculateSimilaritiesCpu(queryEmbedding);
        }
    }

    private IEnumerable<(string VoicingId, double Score)> CalculateSimilaritiesCpu(double[] queryEmbedding)
    {
        if (_voicingIds == null || _hostEmbeddings == null)
            throw new InvalidOperationException("Voicing data not initialized");

        var count = _voicingIds.Length;
        var similarities = new double[count];
        for (var i = 0; i < count; i++)
        {
            similarities[i] = CalculateCosineSimilarity(queryEmbedding, i);
        }
        return _voicingIds.Select((id, idx) => (id, similarities[idx]));
    }

    private IEnumerable<(string VoicingId, double Score)> CalculateFilteredSimilaritiesIlgpu(
        double[] queryEmbedding,
        List<string> allowedIds)
    {
        if (_accelerator == null || _hostEmbeddings == null || _voicingIds == null)
            throw new InvalidOperationException("ILGPU not properly initialized");

        var allowedIndices = allowedIds
            .Select(id => Array.IndexOf(_voicingIds, id))
            .Where(idx => idx >= 0)
            .ToArray();

        if (_isDisposed || _accelerator.AcceleratorType == AcceleratorType.CPU || _filteredCosineSimilarityKernel == null || _deviceEmbeddings == null)
        {
            // Fall back to CPU computation if GPU is not available or disposed
            Trace.WriteLine("Using CPU fallback for filtered similarity calculation");
            return CalculateFilteredSimilaritiesCpu(queryEmbedding, allowedIds, allowedIndices);
        }

        try
        {
            // Allocate GPU memory only for query vector, filter indices, and results (embeddings are pre-allocated)
            using var deviceQueryVector = _accelerator.Allocate1D(queryEmbedding);
            using var deviceAllowedIndices = _accelerator.Allocate1D(allowedIndices);
            using var deviceFilteredSimilarities = _accelerator.Allocate1D<double>(allowedIds.Count);

            // Launch GPU kernel for parallel filtered cosine similarity calculation
            _filteredCosineSimilarityKernel(allowedIds.Count, deviceQueryVector.View, _deviceEmbeddings.View,
                deviceAllowedIndices.View, deviceFilteredSimilarities.View, _embeddingDimensions, allowedIds.Count);

            // Wait for GPU to complete
            _accelerator.Synchronize();

            // Copy results back to CPU
            var similarities = deviceFilteredSimilarities.GetAsArray1D();

            return allowedIds.Select((id, idx) => (id, similarities[idx]));
        }
        catch (Exception ex)
        {
            Trace.TraceWarning($"GPU kernel execution failed for filtered search, falling back to CPU: {ex.Message}");
            return CalculateFilteredSimilaritiesCpu(queryEmbedding, allowedIds, allowedIndices);
        }
    }

    private IEnumerable<(string VoicingId, double Score)> CalculateFilteredSimilaritiesCpu(
        double[] queryEmbedding,
        List<string> allowedIds,
        int[] allowedIndices)
    {
        var similarities = new double[allowedIds.Count];
        for (var i = 0; i < allowedIds.Count; i++)
        {
            var voicingIdx = allowedIndices[i];
            similarities[i] = CalculateCosineSimilarity(queryEmbedding, voicingIdx);
        }
        return allowedIds.Select((id, idx) => (id, similarities[idx]));
    }

    private double CalculateCosineSimilarity(double[] queryEmbedding, int voicingIndex)
    {
        // Validate inputs
        if (queryEmbedding == null)
            throw new ArgumentNullException(nameof(queryEmbedding));

        if (queryEmbedding.Length != _embeddingDimensions)
            throw new ArgumentException(
                $"Query embedding dimension mismatch. Expected {_embeddingDimensions}, got {queryEmbedding.Length}",
                nameof(queryEmbedding));

        if (_hostEmbeddings == null)
            throw new InvalidOperationException("Host embeddings not initialized");

        if (_voicingIds == null)
            throw new InvalidOperationException("Voicing IDs not initialized");

        if (voicingIndex < 0 || voicingIndex >= _voicingIds.Length)
            throw new ArgumentOutOfRangeException(
                nameof(voicingIndex),
                $"Voicing index {voicingIndex} is out of range [0, {_voicingIds.Length})");

        var embeddingStartIdx = voicingIndex * _embeddingDimensions;
        var embeddingEndIdx = embeddingStartIdx + _embeddingDimensions;

        if (embeddingEndIdx > _hostEmbeddings.Length)
            throw new InvalidOperationException(
                $"Embedding array access out of bounds. Index range [{embeddingStartIdx}, {embeddingEndIdx}) exceeds array length {_hostEmbeddings.Length}");

        double dotProduct = 0.0, queryNorm = 0.0, voicingNorm = 0.0;
        for (var j = 0; j < _embeddingDimensions; j++)
        {
            var q = queryEmbedding[j];
            var v = _hostEmbeddings[embeddingStartIdx + j];
            dotProduct += q * v;
            queryNorm += q * q;
            voicingNorm += v * v;
        }
        return dotProduct / (Math.Sqrt(queryNorm) * Math.Sqrt(voicingNorm) + 1e-10);
    }

    private static VoicingSearchResult MapToSearchResult(VoicingEmbedding voicing, double score, string query)
    {
        var document = new VoicingDocument
        {
            Id = voicing.Id,
            SearchableText = voicing.Description,
            ChordName = voicing.ChordName,
            VoicingType = voicing.VoicingType,
            Position = voicing.Position,
            Difficulty = voicing.Difficulty,
            ModeName = voicing.ModeName,
            ModalFamily = voicing.ModalFamily,
            SemanticTags = voicing.SemanticTags,
            PossibleKeys = voicing.PossibleKeys,
            PrimeFormId = voicing.PrimeFormId,
            TranslationOffset = voicing.TranslationOffset,
            YamlAnalysis = voicing.Description, // Using description as YAML for now
            Diagram = voicing.Diagram,
            MidiNotes = voicing.MidiNotes,
            PitchClasses = [.. voicing.MidiNotes.Select(n => n % 12).Distinct().OrderBy(p => p)],
            PitchClassSet = voicing.PitchClassSet,
            IntervalClassVector = voicing.IntervalClassVector,
            MinFret = voicing.MinFret,
            MaxFret = voicing.MaxFret,
            BarreRequired = voicing.BarreRequired,
            HandStretch = voicing.HandStretch,

            // New fields populated with defaults/derived values
            AnalysisEngine = "GpuVoicingSearchStrategy",
            AnalysisVersion = "1.0.0",
            Jobs = [],
            TuningId = "Standard",
            PitchClassSetId = voicing.PrimeFormId,
            StackingType = voicing.StackingType,
            RootPitchClass = voicing.RootPitchClass,
            MidiBassNote = voicing.MidiBassNote,
            DifficultyScore = voicing.Difficulty == "Beginner" ? 1.0 : (voicing.Difficulty == "Intermediate" ? 2.0 : 3.0),

            // Phase 3 Mapping
            HarmonicFunction = voicing.HarmonicFunction,
            IsNaturallyOccurring = voicing.IsNaturallyOccurring,
            HasGuideTones = voicing.HasGuideTones,
            IsRootless = voicing.IsRootless,
            Inversion = voicing.Inversion,
            TopPitchClass = voicing.TopPitchClass, // Added for Chord Melody support
            Consonance = voicing.ConsonanceScore,
            Brightness = voicing.BrightnessScore,
            Roughness = 1.0 - voicing.ConsonanceScore, // Rough approximation if needed
            OmittedTones = voicing.OmittedTones,

            // AI Agent Metadata
            TexturalDescription = voicing.TexturalDescription,
            DoubledTones = voicing.DoubledTones,
            AlternateNames = voicing.AlternateNames
        };

        return new(document, score, query);
    }

    private void EnsureInitialized()
    {
        if (!_isInitialized)
            throw new InvalidOperationException("GPU voicing search not initialized. Call InitializeAsync first.");
    }

    private long CalculateGpuMemoryUsage()
    {
        if (_voicings.Count == 0)
            return 0;

        var bytesPerEmbedding = _embeddingDimensions * sizeof(double);
        return _voicings.Count * bytesPerEmbedding / (1024 * 1024);
    }

    private void RecordSearchTime(TimeSpan elapsed)
    {
        Interlocked.Increment(ref _totalSearches);
        lock (_initLock)
        {
            _totalSearchTime = _totalSearchTime.Add(elapsed);
        }
    }

    /// <summary>
    /// Apply biomechanical filters to voicings (Quick Win 1)
    /// </summary>
    private IEnumerable<VoicingEmbedding> ApplyBiomechanicalFilters(
        IEnumerable<VoicingEmbedding> voicings,
        VoicingSearchFilters filters)
    {
        // Use pre-computed HandStretch field for fast filtering
        // Only do full biomechanical analysis if comfort/ergonomic filters are specified
        var needsFullAnalysis = filters.MinComfortScore.HasValue ||
                                filters.MustBeErgonomic.HasValue && filters.MustBeErgonomic.Value;

        if (!needsFullAnalysis)
        {
            // Fast path: use pre-computed HandStretch field
            return voicings.Where(v =>
            {
                if (filters.MaxFingerStretch.HasValue && v.HandStretch > filters.MaxFingerStretch.Value)
                    return false;
                return true;
            });
        }

        // Slow path: full biomechanical analysis (only for comfort/ergonomic filters)
        var analyzer = new Biomechanics.BiomechanicalAnalyzer(
            filters.HandSize ?? Biomechanics.HandSize.Medium);

        return voicings.Where(v =>
        {
            // First check HandStretch (fast)
            if (filters.MaxFingerStretch.HasValue && v.HandStretch > filters.MaxFingerStretch.Value)
                return false;

            // Parse diagram to positions
            var positions = ParseDiagramToPositions(v.Diagram);
            if (positions.Count == 0)
                return true; // Keep voicings we can't analyze

            // Analyze biomechanical playability
            var analysis = analyzer.AnalyzeChordPlayability(positions);

            // Apply comfort filter
            if (filters.MinComfortScore.HasValue &&
                analysis.Comfort < filters.MinComfortScore.Value)
                return false;

            // Apply ergonomic filter
            if (filters.MustBeErgonomic.HasValue &&
                filters.MustBeErgonomic.Value &&
                analysis.WristPostureAnalysis != null &&
                !analysis.WristPostureAnalysis.IsErgonomic)
                return false;

            return true;
        });
    }

    /// <summary>
    /// Apply musical characteristic filters (Quick Win 3)
    /// </summary>
    private IEnumerable<VoicingEmbedding> ApplyMusicalFilters(
        IEnumerable<VoicingEmbedding> voicings,
        VoicingSearchFilters filters)
    {
        return voicings.Where(v =>
        {
            // Filter by open/closed voicing
            if (filters.IsOpenVoicing.HasValue)
            {
                var isOpen = v.VoicingType?.Contains("open", StringComparison.OrdinalIgnoreCase) ?? false;
                if (isOpen != filters.IsOpenVoicing.Value)
                    return false;
            }

            // Filter by rootless
            if (filters.IsRootless.HasValue)
            {
                if (v.IsRootless != filters.IsRootless.Value)
                    return false;
            }

            // Filter by drop voicing
            if (!string.IsNullOrEmpty(filters.DropVoicing))
            {
                var hasDropVoicing = v.VoicingType?.Contains(filters.DropVoicing, StringComparison.OrdinalIgnoreCase) ?? false;
                if (!hasDropVoicing)
                    return false;
            }

            // Filter by CAGED shape
            if (!string.IsNullOrEmpty(filters.CagedShape))
            {
                var hasCagedShape = v.SemanticTags.Any(tag =>
                    tag.Contains($"CAGED-{filters.CagedShape}", StringComparison.OrdinalIgnoreCase) ||
                    tag.Contains($"{filters.CagedShape} shape", StringComparison.OrdinalIgnoreCase));
                if (!hasCagedShape)
                    return false;
            }

            // Phase 3 Filters

            if (!string.IsNullOrEmpty(filters.HarmonicFunction))
            {
                if (!string.Equals(v.HarmonicFunction, filters.HarmonicFunction, StringComparison.OrdinalIgnoreCase))
                    return false;
            }

            if (filters.IsNaturallyOccurring.HasValue)
            {
                if (v.IsNaturallyOccurring != filters.IsNaturallyOccurring.Value)
                    return false;
            }

            if (filters.HasGuideTones.HasValue)
            {
                if (v.HasGuideTones != filters.HasGuideTones.Value)
                    return false;
            }

            if (filters.Inversion.HasValue)
            {
                if (v.Inversion != filters.Inversion.Value)
                    return false;
            }

            if (filters.MinConsonance.HasValue)
            {
                if (v.ConsonanceScore < filters.MinConsonance.Value)
                    return false;
            }

            if (filters.MinBrightness.HasValue)
            {
                if (v.BrightnessScore < filters.MinBrightness.Value)
                    return false;
            }

            if (filters.MaxBrightness.HasValue)
            {
                if (v.BrightnessScore > filters.MaxBrightness.Value)
                    return false;
            }

            if (filters.TopPitchClass.HasValue)
            {
                if (v.TopPitchClass != filters.TopPitchClass.Value)
                    return false;
            }

            if (filters.OmittedTones != null && filters.OmittedTones.Length > 0)
            {
                // Check if ALL requested omissions are present in voicing's OmittedTones
                // Or maybe just ANY? Usually filters are restrictive (Must omit X).
                // Let's assume MUST omit all specified.
                foreach (var omission in filters.OmittedTones)
                {
                    if (v.OmittedTones == null || !v.OmittedTones.Contains(omission, StringComparer.OrdinalIgnoreCase))
                        return false;
                }
            }

            return true;
        });
    }

    /// <summary>
    /// Parse voicing diagram to Position list for biomechanical analysis
    /// </summary>
    private ImmutableList<Primitives.Position> ParseDiagramToPositions(string diagram)
    {
        // Diagram format: "x-3-2-0-1-0" or "3-2-0-1-0-3"
        var parts = diagram.Split('-');
        var positions = new List<Primitives.Position>();

        for (var i = 0; i < parts.Length && i < 6; i++)
        {
            var part = parts[i].Trim();
            var str = new Primitives.Str(i + 1); // Str is 1-based (strings 1-6)

            if (part == "x" || part == "X")
            {
                // Muted string
                positions.Add(new Primitives.Position.Muted(str));
            }
            else if (int.TryParse(part, out var fretValue))
            {
                var fret = new Primitives.Fret(fretValue);
                var location = new Positions.PositionLocation(str, fret);

                // Create a played position with estimated MIDI note
                // Standard tuning: E2(40), A2(45), D3(50), G3(55), B3(59), E4(64)
                var openMidiNotes = new[] { 64, 59, 55, 50, 45, 40 }; // High E to Low E
                var midiNoteValue = i < openMidiNotes.Length
                    ? openMidiNotes[i] + fretValue
                    : 60 + fretValue; // Fallback
                var midiNote = new Notes.Primitives.MidiNote(midiNoteValue);

                positions.Add(new Primitives.Position.Played(location, midiNote));
            }
        }

        return [.. positions];
    }

    public void Dispose()
    {
        if (_isDisposed)
            return;

        try { _deviceEmbeddings?.Dispose(); } catch { /* Ignore cleanup errors */ }
        try { _accelerator?.Dispose(); } catch { /* Ignore cleanup errors */ }
        try { _context?.Dispose(); } catch { /* Ignore cleanup errors */ }

        _isDisposed = true;
    }

}
