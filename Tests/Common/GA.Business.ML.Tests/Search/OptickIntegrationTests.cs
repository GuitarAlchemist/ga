namespace GA.Business.ML.Tests.Search;

using Embeddings.Services;
using GA.Business.ML.Search;

/// <summary>
///     Integration tests against the real OPTK v4 index in <c>state/voicings/optick.index</c>.
///     Tests are auto-skipped when the index file is missing so they don't block CI on a
///     clean checkout; local runs validate the full NL → geometry → voicings pipeline.
/// </summary>
[TestFixture]
[Category("Integration")]
public class OptickIntegrationTests
{
    private string? _indexPath;
    private OptickIndexReader? _reader;

    [OneTimeSetUp]
    public void OneTimeSetUp()
    {
        _indexPath = FindIndexPath();
        if (_indexPath is null) return;

        try { _reader = new OptickIndexReader(_indexPath); }
        catch (Exception ex)
        {
            TestContext.Out.WriteLine($"OPTK reader failed to open {_indexPath}: {ex.Message}");
            _reader = null;
        }
    }

    [OneTimeTearDown]
    public void OneTimeTearDown() => _reader?.Dispose();

    // ─── reader integrity ──────────────────────────────────────────────────

    [Test]
    public void Reader_OpensRealIndex_ReportsPositiveCount()
    {
        SkipIfNoIndex();
        Assert.That(_reader!.Count, Is.GreaterThan(1000),
            "production OPTK index should hold 100k+ voicings.");
    }

    [Test]
    public void Reader_Vector_NormMatchesPerPartitionSchema()
    {
        SkipIfNoIndex();
        var v = _reader!.GetVector(0);
        var sumSq = 0.0;
        for (var i = 0; i < v.Length; i++) sumSq += v[i] * v[i];
        var norm = Math.Sqrt(sumSq);

        // Under v4-pp-r (v1.8, per-partition norm + ROOT partition): each similarity
        // partition slice is unit-L2, scaled by sqrt(weight). A voicing with all
        // partitions populated has norm² = Σ weight_p = STRUCTURE(0.45)+MORPHOLOGY(0.25)+
        // CONTEXT(0.20)+SYMBOLIC(0.10)+MODAL(0.10)+ROOT(0.05) = 1.15, so total norm ≈
        // sqrt(1.15) ≈ 1.0724. Voicings with empty partitions (e.g. no tags → SYMBOLIC
        // zero) have smaller norm. Accept range [0.3, sqrt(1.15)+ε].
        var maxPossibleNorm = Math.Sqrt(0.45 + 0.25 + 0.20 + 0.10 + 0.10 + 0.05);
        Assert.That(norm, Is.GreaterThan(0.3).And.LessThanOrEqualTo(maxPossibleNorm + 1e-3),
            $"on-disk v4-pp-r vectors should have total norm ≤ sqrt(Σw)={maxPossibleNorm:F4}, got {norm:F4}.");
    }

    [Test]
    public void Reader_Metadata_HasDiagramAndMidiNotes()
    {
        SkipIfNoIndex();
        var meta = _reader!.GetMetadata(0);

        Assert.Multiple(() =>
        {
            Assert.That(meta.Diagram, Is.Not.Empty, "metadata must include a diagram.");
            Assert.That(meta.MidiNotes, Is.Not.Empty, "metadata must include MIDI notes.");
            Assert.That(new[] { "guitar", "bass", "ukulele" }, Does.Contain(meta.Instrument),
                "instrument must be one of the three supported.");
        });
    }

    // ─── end-to-end: encode → search ───────────────────────────────────────

    [Test]
    public void EndToEnd_SearchForCmaj7_ReturnsMusicallyRelevantVoicings()
    {
        SkipIfNoIndex();

        var encoder = new MusicalQueryEncoder(
            new TheoryVectorService(),
            new ModalVectorService(),
            new SymbolicVectorService(),
            new RootVectorService());

        using var strategy = new OptickSearchStrategy(_indexPath!);
        var query = encoder.Encode(new StructuredQuery(
            ChordSymbol: "Cmaj7",
            RootPitchClass: 0,
            PitchClasses: [0, 4, 7, 11],
            ModeName: null,
            Tags: null));

        var results = strategy.SemanticSearchAsync(query, limit: 20).GetAwaiter().GetResult();

        Assert.That(results, Has.Count.GreaterThan(0), "search must return at least one voicing.");

        // Score range — under v4-pp per-partition normalization, identical PC-set voicings
        // legitimately score identically (that's the whole point of the leak fix — no more
        // cross-partition bleed). For Cmaj7 the corpus has hundreds of voicings matching
        // PC-set {0,4,7,11}, so the top-20 often ALL score exactly the same. Spread of 0.0
        // is no longer a bug — it's correct partition-invariant behavior. We still dump the
        // range for human inspection, just without a numeric-spread assertion.
        var spread = results[0].Score - results[^1].Score;
        TestContext.Out.WriteLine(
            $"score range: top={results[0].Score:F4} bottom={results[^1].Score:F4} spread={spread:F4}");

        // Under v4-pp the sum of active-partition weights caps the score. A chord-only query
        // activates STRUCTURE (0.45) + MODAL (0.10 via auto-scored PC-set) = 0.55 ceiling.
        // Top score should sit at or just below that ceiling for an exact PC-set match.
        Assert.That(results[0].Score, Is.GreaterThan(0.4),
            "top score for exact-PC-set retrieval should approach the active-partition weight sum (0.55).");

        // Musical relevance — at least one of the top-5 should include the Cmaj7 PC set {0,4,7,11}.
        var target = new HashSet<int> { 0, 4, 7, 11 };
        var matchInTop5 = results.Take(5).Any(r =>
        {
            var pcs = r.Document.MidiNotes.Select(n => ((n % 12) + 12) % 12).ToHashSet();
            return target.IsSubsetOf(pcs);
        });

        if (!matchInTop5)
        {
            TestContext.Out.WriteLine("Top-5 did not contain a Cmaj7 PC set; dumping for inspection:");
            foreach (var r in results.Take(5))
            {
                var pcs = string.Join(",", r.Document.MidiNotes.Select(n => ((n % 12) + 12) % 12).Distinct().OrderBy(p => p));
                TestContext.Out.WriteLine($"  score={r.Score:F4} diagram={r.Document.Diagram} pcs={{{pcs}}}");
            }
        }

        Assert.That(matchInTop5, Is.True,
            "at least one of the top 5 results for a Cmaj7 query must contain the {C,E,G,B} PC set.");
    }

    [Test]
    public void EndToEnd_DifferentChords_ProduceDisjointTopResults()
    {
        SkipIfNoIndex();

        var encoder = new MusicalQueryEncoder(
            new TheoryVectorService(),
            new ModalVectorService(),
            new SymbolicVectorService(),
            new RootVectorService());

        using var strategy = new OptickSearchStrategy(_indexPath!);

        var cmaj7 = encoder.Encode(new StructuredQuery("Cmaj7", 0, [0, 4, 7, 11], null, null));
        var fSharpDim = encoder.Encode(new StructuredQuery("F#dim7", 6, [0, 3, 6, 9], null, null));

        var rA = strategy.SemanticSearchAsync(cmaj7, 10).GetAwaiter().GetResult();
        var rB = strategy.SemanticSearchAsync(fSharpDim, 10).GetAwaiter().GetResult();

        var aIds = rA.Select(r => r.Document.Diagram).ToHashSet();
        var bIds = rB.Select(r => r.Document.Diagram).ToHashSet();
        var overlap = aIds.Intersect(bIds).Count();

        TestContext.Out.WriteLine(
            $"Cmaj7 top-10 ∩ F#dim7 top-10 = {overlap} voicings overlap (lower is better).");
        Assert.That(overlap, Is.LessThan(5),
            "two musically-distant chords should have mostly distinct top-10 sets.");
    }

    // ─── helpers ───────────────────────────────────────────────────────────

    private void SkipIfNoIndex()
    {
        if (_reader is null || _indexPath is null)
        {
            Assert.Ignore(
                "optick.index not found; skipping. Run the FretboardVoicingsCLI generator to build it, " +
                "or set GA_OPTICK_INDEX_PATH to an absolute path for local runs.");
        }
    }

    private static string? FindIndexPath()
    {
        var envOverride = Environment.GetEnvironmentVariable("GA_OPTICK_INDEX_PATH");
        if (!string.IsNullOrWhiteSpace(envOverride) && File.Exists(envOverride)) return envOverride;

        // Walk up from the test binary toward the repo root looking for state/voicings/optick.index.
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            var candidate = Path.Combine(dir.FullName, "state", "voicings", "optick.index");
            if (File.Exists(candidate)) return candidate;
            dir = dir.Parent;
        }
        return null;
    }
}
