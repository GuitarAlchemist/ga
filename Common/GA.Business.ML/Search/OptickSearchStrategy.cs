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

    public OptickSearchStrategy(string indexPath) => _reader = new OptickIndexReader(indexPath);

    public string Name => "OPTK-mmap";
    public bool IsAvailable => true;
    public QueryVectorSpace QuerySpace => QueryVectorSpace.OpticCompact112;

    /// <summary>The OPTK index is mmap-loaded in the constructor, so it serves queries
    /// immediately — no host warmup gating (avoids the cold-start "Service not initialized" race).</summary>
    public bool RequiresWarmup => false;

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
            // Index-bound metadata filters (ApplyFilters) + the shared comfort seam. Comfort is the one
            // rich filter OPTK *can* honor without a reindex — it only needs the diagram, which the index
            // carries. The ~30 metadata filters it can't back are surfaced via UnsupportedPopulatedFilters
            // (telemetry `dropped`) rather than silently ignored. See ADR-0002.
            return ApplyFilters(pool, filters)
                .Where(r => VoicingComfortFilter.Matches(r.Document.Diagram, filters))
                .Take(limit)
                .ToList();
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

    /// <summary>
    ///     The populated filters OPTK structurally cannot honor — its mmap index carries only diagram,
    ///     inferred quality, instrument, and MIDI notes (ADR-0002). It honors: <c>ChordName</c>,
    ///     <c>MinMidiPitch</c>/<c>MaxMidiPitch</c>, <c>VoicingType</c> <i>only</i> as an instrument route
    ///     (guitar/bass/ukulele), and the diagram-derived comfort filters
    ///     (<c>MinComfortScore</c>/<c>MustBeErgonomic</c>/<c>HandSize</c>). Everything else, if populated,
    ///     is dropped — listed here so the caller-side telemetry can record it instead of dropping silently.
    ///     (<c>SymbolicBitIndices</c> is a ranking signal, not a filter, so it is out of scope.)
    /// </summary>
    public IReadOnlyList<string> UnsupportedPopulatedFilters(VoicingSearchFilters f) =>
        ComputeUnsupportedFilters(f);

    /// <summary>
    ///     Pure (index-independent) drop-list logic, factored out so it is unit-testable without an
    ///     on-disk OPTK index. See <see cref="UnsupportedPopulatedFilters"/>.
    /// </summary>
    internal static IReadOnlyList<string> ComputeUnsupportedFilters(VoicingSearchFilters f)
    {
        var dropped = new List<string>();

        // VoicingType is honored ONLY as an instrument route; any other value (drop2, shell, …) is dropped.
        if (f.VoicingType is { Length: > 0 } vt &&
            vt.ToLowerInvariant() is not ("guitar" or "bass" or "ukulele"))
        {
            dropped.Add(nameof(f.VoicingType));
        }

        if (f.Difficulty is not null) dropped.Add(nameof(f.Difficulty));
        if (f.Position is not null) dropped.Add(nameof(f.Position));
        if (f.ModeName is not null) dropped.Add(nameof(f.ModeName));
        if (f.Tags is { Length: > 0 }) dropped.Add(nameof(f.Tags));
        if (f.MinFret.HasValue) dropped.Add(nameof(f.MinFret));
        if (f.MaxFret.HasValue) dropped.Add(nameof(f.MaxFret));
        if (f.RequireBarreChord.HasValue) dropped.Add(nameof(f.RequireBarreChord));
        if (f.MaxFingerStretch.HasValue) dropped.Add(nameof(f.MaxFingerStretch));
        if (f.IsOpenVoicing.HasValue) dropped.Add(nameof(f.IsOpenVoicing));
        if (f.IsRootless.HasValue) dropped.Add(nameof(f.IsRootless));
        if (f.DropVoicing is { Length: > 0 }) dropped.Add(nameof(f.DropVoicing));
        if (f.CagedShape is { Length: > 0 }) dropped.Add(nameof(f.CagedShape));
        if (f.StackingType is not null) dropped.Add(nameof(f.StackingType));
        if (f.IsSlashChord.HasValue) dropped.Add(nameof(f.IsSlashChord));
        if (f.FingerCount.HasValue) dropped.Add(nameof(f.FingerCount));
        if (f.SetClassId is not null) dropped.Add(nameof(f.SetClassId));
        if (f.RahnPrimeForm is not null) dropped.Add(nameof(f.RahnPrimeForm));
        if (f.HarmonicFunction is not null) dropped.Add(nameof(f.HarmonicFunction));
        if (f.IsNaturallyOccurring.HasValue) dropped.Add(nameof(f.IsNaturallyOccurring));
        if (f.HasGuideTones.HasValue) dropped.Add(nameof(f.HasGuideTones));
        if (f.Inversion.HasValue) dropped.Add(nameof(f.Inversion));
        if (f.MinConsonance.HasValue) dropped.Add(nameof(f.MinConsonance));
        if (f.MinBrightness.HasValue) dropped.Add(nameof(f.MinBrightness));
        if (f.MaxBrightness.HasValue) dropped.Add(nameof(f.MaxBrightness));
        if (f.OmittedTones is { Length: > 0 }) dropped.Add(nameof(f.OmittedTones));
        if (f.TopPitchClass.HasValue) dropped.Add(nameof(f.TopPitchClass));
        if (f.TexturalDescriptionContains is not null) dropped.Add(nameof(f.TexturalDescriptionContains));
        if (f.DoubledTonesContain is { Length: > 0 }) dropped.Add(nameof(f.DoubledTonesContain));
        if (f.AlternateNameMatch is not null) dropped.Add(nameof(f.AlternateNameMatch));

        return dropped;
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
            : [];

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
        // Extract the chord-quality fragment from filters.ChordName once
        // so the per-row check below stays cheap. Codex CLI 2026-05-08
        // pinpointed the bug: callers pass full symbols ("Cmaj7", "Dm7"),
        // but MapToSearchResult sets d.ChordName = meta.QualityInferred
        // ("maj7", "m7") so a strict Contains check rejected every row.
        // Match against quality + verify root pitch class — keeps the
        // filter selective enough that "Cmaj7" doesn't match every maj7
        // in the corpus.
        var (filterRootPitchClass, filterQuality) = filters.ChordName is { Length: > 0 } cn
            ? ParseChordSymbol(cn)
            : ((int?)null, (string?)null);

        foreach (var r in pool)
        {
            var d = r.Document;
            if (filterQuality is not null)
            {
                var docQuality = d.ChordName ?? string.Empty;
                if (!docQuality.Contains(filterQuality, StringComparison.OrdinalIgnoreCase)) continue;
                if (filterRootPitchClass is int rpc && d.MidiNotes.Length > 0)
                {
                    var docRootPc = ((d.MidiNotes.Min() % 12) + 12) % 12;
                    if (docRootPc != rpc) continue;
                }
            }
            if (filters.MinMidiPitch is int lo && d.MidiNotes.Length > 0 && d.MidiNotes.Min() < lo) continue;
            if (filters.MaxMidiPitch is int hi && d.MidiNotes.Length > 0 && d.MidiNotes.Max() > hi) continue;
            yield return r;
        }
    }

    /// <summary>
    /// Splits a chord symbol like <c>"Cmaj7"</c> / <c>"F#m7"</c> /
    /// <c>"Bbmaj9"</c> into (root pitch class, quality fragment).
    /// Returns nulls when the input doesn't start with a note letter.
    /// </summary>
    /// <remarks>
    /// Deliberately permissive on quality — anything after the optional
    /// accidental is the quality fragment, and the per-row filter does a
    /// case-insensitive <c>Contains</c> against the document's
    /// <c>QualityInferred</c> field. Codex CLI 2026-05-08 fix.
    /// </remarks>
    private static (int? RootPitchClass, string? Quality) ParseChordSymbol(string symbol)
    {
        if (string.IsNullOrEmpty(symbol)) return (null, null);

        var noteIndex = char.ToUpperInvariant(symbol[0]) switch
        {
            'C' => 0, 'D' => 2, 'E' => 4, 'F' => 5, 'G' => 7, 'A' => 9, 'B' => 11,
            _   => -1,
        };
        if (noteIndex < 0) return (null, null);

        var i = 1;
        if (i < symbol.Length)
        {
            switch (symbol[i])
            {
                case '#': noteIndex = (noteIndex + 1) % 12; i++; break;
                case 'b': noteIndex = (noteIndex + 11) % 12; i++; break;
            }
        }

        var quality = i < symbol.Length ? symbol[i..] : string.Empty;
        return (noteIndex, quality);
    }
}
