namespace GA.Business.ML.Search;

using System.Collections.Concurrent;
using System.Diagnostics;
using System.Numerics.Tensors;
using GA.Domain.Services.Fretboard.Voicings.Core;
using Rag.Models;

/// <summary>
///     Voicing search backed by the OPTK v4 mmap index (<see cref="OptickIndexReader"/>).
///     Since on-disk vectors are pre-scaled and L2-normalized, cosine similarity reduces
///     to a dot product; search is a parallel scan + top-K heap.
///
///     Query vectors are expected to already be weighted + L2-normalized in the compact
///     112-dim layout — produced by <c>MusicalQueryEncoder</c>.
/// </summary>
public sealed class OptickSearchStrategy : IVoicingSearchStrategy, IDisposable
{
    private readonly OptickIndexReader _reader;
    private long _totalSearches;
    private long _totalSearchTicks;
    private int _findSimilarWarned;

    public OptickSearchStrategy(string indexPath)
    {
        _reader = new OptickIndexReader(indexPath);
    }

    public string Name => "OPTK-mmap";
    public bool IsAvailable => true;
    public QueryVectorSpace QuerySpace => QueryVectorSpace.OpticCompact112;

    public VoicingSearchPerformance Performance => new(
        TimeSpan.FromMilliseconds(20),
        MemoryUsageMb: 0,          // mmap — OS handles paging
        RequiresGpu: false,
        RequiresNetwork: false);

    /// <summary>No-op: the OPTK index is pre-built on disk.</summary>
    public Task InitializeAsync(IEnumerable<VoicingEmbedding> voicings) => Task.CompletedTask;

    public Task<List<VoicingSearchResult>> SemanticSearchAsync(
        double[] queryEmbedding,
        int limit = 10,
        CancellationToken cancellationToken = default)
        => Task.Run(() => SearchInternal(queryEmbedding, limit, instrumentFilter: null, query: "semantic search", cancellationToken), cancellationToken);

    public Task<List<VoicingSearchResult>> HybridSearchAsync(
        double[] queryEmbedding,
        VoicingSearchFilters filters,
        int limit = 10,
        CancellationToken cancellationToken = default)
        => Task.Run(() =>
        {
            // Filters that can route to per-instrument slices use the mmap instrument index;
            // other filters are applied post-hoc against a larger candidate pool.
            string? instrument = filters.VoicingType?.ToLowerInvariant() switch
            {
                "guitar" or "bass" or "ukulele" => filters.VoicingType.ToLowerInvariant(),
                _ => null
            };
            var poolSize = Math.Max(limit * 10, 100);
            var pool = SearchInternal(queryEmbedding, poolSize, instrument, "hybrid search", cancellationToken);
            return ApplyFilters(pool, filters).Take(limit).ToList();
        }, cancellationToken);

    public Task<List<VoicingSearchResult>> FindSimilarVoicingsAsync(
        string voicingId,
        int limit = 10,
        CancellationToken cancellationToken = default)
    {
        _ = cancellationToken;
        // OPTK metadata is keyed by index, not voicing id; until the index grows a reverse lookup,
        // this returns empty rather than throwing so the chatbot path degrades silently.
        // Emit a one-shot trace so operators notice the feature is not wired rather than
        // silently diagnosing empty result sets.
        if (Interlocked.Exchange(ref _findSimilarWarned, 1) == 0)
        {
            System.Diagnostics.Trace.TraceWarning(
                "OptickSearchStrategy.FindSimilarVoicingsAsync is not implemented for OPTK v4 indexes " +
                "(no id→index reverse lookup). Returning empty for voicingId={0}.", voicingId);
        }
        return Task.FromResult(new List<VoicingSearchResult>());
    }

    public VoicingSearchStats GetStats()
    {
        var avg = _totalSearches > 0 ? TimeSpan.FromTicks(_totalSearchTicks / _totalSearches) : TimeSpan.Zero;
        return new VoicingSearchStats(_reader.Count, 0, avg, _totalSearches);
    }

    public void Dispose() => _reader.Dispose();

    // ─── internals ─────────────────────────────────────────────────────────

    private List<VoicingSearchResult> SearchInternal(
        double[] queryEmbedding,
        int limit,
        string? instrumentFilter,
        string query,
        CancellationToken cancellationToken)
    {
        if (queryEmbedding.Length != OptickIndexReader.Dimension)
            throw new ArgumentException(
                $"OPTK query must be {OptickIndexReader.Dimension}-dim, got {queryEmbedding.Length}.",
                nameof(queryEmbedding));

        cancellationToken.ThrowIfCancellationRequested();
        var sw = Stopwatch.StartNew();

        // Convert to float32 once; the on-disk index is f32 so mixing precisions is wasteful.
        var q = new float[queryEmbedding.Length];
        for (var i = 0; i < queryEmbedding.Length; i++) q[i] = (float)queryEmbedding[i];

        var (start, count) = _reader.GetInstrumentRange(instrumentFilter);

        // Parallel partitioned scan — each partition keeps a local top-K heap; merged at end.
        // Pre-sizing at (limit + 1) avoids amortized heap-grow allocations during enqueue.
        // ParallelOptions.CancellationToken lets the caller tear down mid-scan.
        //
        // Priority is (score, -idx): min-heap root holds the WORST candidate (lowest score,
        // or tied-score with highest index). Replacement condition `score > worstScore OR
        // (score == worst AND idx < worstIdx)` — i.e. prefer lower indices on ties. Without
        // this, post-v4-pp queries like "Cmaj7" where MANY voicings have IDENTICAL PC-set
        // (and thus identical STRUCTURE+MODAL scores) produce nondeterministic top-K because
        // candidate selection depends on partition-scan order under Parallel.For.
        var parallelOpts = new ParallelOptions { CancellationToken = cancellationToken };
        var partitionHeaps = new ConcurrentBag<PriorityQueue<long, (float Score, long NegIdx)>>();
        Parallel.For(
            fromInclusive: 0L,
            toExclusive: count,
            parallelOptions: parallelOpts,
            localInit: () => new PriorityQueue<long, (float, long)>(limit + 1),
            body: (offset, loopState, heap) =>
            {
                _ = loopState;
                var idx = start + offset;
                var score = TensorPrimitives.Dot(q.AsSpan(), _reader.GetVector(idx));
                var priority = (score, -idx);
                if (heap.Count < limit)
                {
                    heap.Enqueue(idx, priority);
                }
                else if (heap.TryPeek(out _, out var worst)
                         && ValueTuple.Create(score, -idx).CompareTo(worst) > 0)
                {
                    heap.Dequeue();
                    heap.Enqueue(idx, priority);
                }
                return heap;
            },
            localFinally: heap => partitionHeaps.Add(heap));

        // Merge partition heaps under the same composite-priority comparison.
        var merged = new PriorityQueue<long, (float Score, long NegIdx)>(limit + 1);
        foreach (var heap in partitionHeaps)
        {
            while (heap.TryDequeue(out var idx, out var priority))
            {
                if (merged.Count < limit)
                {
                    merged.Enqueue(idx, priority);
                }
                else if (merged.TryPeek(out _, out var worst) && priority.CompareTo(worst) > 0)
                {
                    merged.Dequeue();
                    merged.Enqueue(idx, priority);
                }
            }
        }

        // Drain, then stable-sort by (score desc, index asc) for deterministic output
        // ordering — matches the priority-selection semantics above.
        var buffer = new List<(long Index, float Score)>(merged.Count);
        while (merged.TryDequeue(out var idx, out var priority)) buffer.Add((idx, priority.Score));
        buffer.Sort((a, b) =>
        {
            var byScore = b.Score.CompareTo(a.Score);
            return byScore != 0 ? byScore : a.Index.CompareTo(b.Index);
        });

        var results = new List<VoicingSearchResult>(buffer.Count);
        foreach (var (idx, score) in buffer)
        {
            var meta = _reader.GetMetadata(idx);
            results.Add(MapToSearchResult(idx, meta, score, query));
        }

        sw.Stop();
        Interlocked.Increment(ref _totalSearches);
        Interlocked.Add(ref _totalSearchTicks, sw.Elapsed.Ticks);

        return results;
    }

    private static VoicingSearchResult MapToSearchResult(long idx, OptickMetadata meta, double score, string query)
    {
        var pitchClasses = meta.MidiNotes.Length > 0
            ? meta.MidiNotes.Select(n => ((n % 12) + 12) % 12).Distinct().OrderBy(p => p).ToArray()
            : Array.Empty<int>();

        var document = new ChordVoicingRagDocument
        {
            SearchableText = meta.Diagram,
            ChordName = meta.QualityInferred ?? "Unknown",
            VoicingType = meta.Instrument,
            Diagram = meta.Diagram,
            MidiNotes = meta.MidiNotes,
            PitchClasses = pitchClasses,
            PitchClassSet = "{" + string.Join(",", pitchClasses) + "}",
            SemanticTags = [],
            PossibleKeys = [],
            PrimeFormId = "",
            PitchClassSetId = "",
            TranslationOffset = 0,
            AnalysisEngine = nameof(OptickSearchStrategy),
            AnalysisVersion = "optk-v4",
            Jobs = [],
            TuningId = "Standard",
            YamlAnalysis = "",
            IntervalClassVector = "",
            DifficultyScore = 1.0,
        };

        return new VoicingSearchResult(document, score, query);
    }

    private static IEnumerable<VoicingSearchResult> ApplyFilters(
        IEnumerable<VoicingSearchResult> pool,
        VoicingSearchFilters filters)
    {
        foreach (var r in pool)
        {
            var d = r.Document;
            if (filters.ChordName != null &&
                !(d.ChordName ?? "").Contains(filters.ChordName, StringComparison.OrdinalIgnoreCase)) continue;
            if (filters.MinMidiPitch is int lo && d.MidiNotes.Length > 0 && d.MidiNotes.Min() < lo) continue;
            if (filters.MaxMidiPitch is int hi && d.MidiNotes.Length > 0 && d.MidiNotes.Max() > hi) continue;
            yield return r;
        }
    }
}
