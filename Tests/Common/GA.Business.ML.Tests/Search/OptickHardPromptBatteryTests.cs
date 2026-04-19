namespace GA.Business.ML.Tests.Search;

using Embeddings.Services;
using GA.Business.ML.Search;

/// <summary>
///     Hard-prompt battery. Each probe asks a question a guitarist would have an opinion
///     about — "does the ranking make musical sense", not "is it non-empty". Results are
///     printed for inspection; assertions cover only the invariants that MUST hold for
///     the pipeline to be working at all.
/// </summary>
[TestFixture]
[Category("Integration")]
public class OptickHardPromptBatteryTests
{
    private string? _indexPath;
    private OptickSearchStrategy? _strategy;
    private MusicalQueryEncoder? _encoder;
    private TypedMusicalQueryExtractor? _extractor;

    [OneTimeSetUp]
    public void SetUp()
    {
        // These are index-independent and used by tests that don't touch the mmap.
        _encoder = new MusicalQueryEncoder(
            new TheoryVectorService(),
            new ModalVectorService(),
            new SymbolicVectorService(),
            new RootVectorService());
        _extractor = new TypedMusicalQueryExtractor();

        _indexPath = FindIndex();
        if (_indexPath is null) return;
        try
        {
            _strategy = new OptickSearchStrategy(_indexPath);
        }
        catch (InvalidDataException ex)
        {
            // Schema-hash mismatch (index built under a different schema version) —
            // skip gracefully. Covers the v4 → v4-pp transition window.
            TestContext.Out.WriteLine($"OPTK index incompatible with current code: {ex.Message}");
            _strategy = null;
        }
    }

    private void SkipIfNoStrategy()
    {
        if (_strategy is null)
        {
            Assert.Ignore(
                "optick.index missing or schema-incompatible (rebuild via FretboardVoicingsCLI " +
                "to activate v4-pp per-partition normalization).");
        }
    }

    [OneTimeTearDown]
    public void TearDown() => _strategy?.Dispose();

    // ─── Probe 1: STRUCTURE partition — baseline structural match ─────────────
    [Test]
    public async Task P1_Cmaj7_BaselineStructuralMatch()
    {
        // Why: Cmaj7 = {C,E,G,B} = {0,4,7,11}. A good OPTIC-K index should rank voicings
        // containing these pitch classes at the top, with scores well above noise.
        await RunAndDump("Cmaj7", 5);
    }

    // ─── Probe 2: T-invariance — Cmaj7 vs Dmaj7 parallel ──────────────────────
    [Test]
    public async Task P2_Dmaj7_TransposedParallel_ShouldDifferInDiagramsNotScores()
    {
        // Why: STRUCTURE partition encodes chroma + ICV; rotating the root should yield
        // different diagrams but similar *shape* of score distribution (same ICV => same
        // STRUCTURE subspace cosine profile). Under old text-embedding this wasn't true.
        await RunAndDump("Dmaj7", 5);
    }

    // ─── Probe 3: Dissonant-quality discrimination ────────────────────────────
    [Test]
    public async Task P3_FSharpM7b5_DissonantChord()
    {
        // Why: F#m7b5 = half-diminished. PC set {6,9,0,4}. Tonal-ness dim (26-29 of
        // STRUCTURE) should read lower than a major7. Test: top scores should be similar
        // magnitude to consonant chords (the cosine doesn't penalize absolute consonance —
        // it matches similarity), but the RESULTS should contain m7b5-pattern voicings.
        await RunAndDump("F#m7b5", 5);
    }

    // ─── Probe 4: MODAL partition — mode-only query ───────────────────────────
    [Test]
    public async Task P4_FSharpLydian_ModeOnly()
    {
        // Why: no chord = STRUCTURE stays zero (no PC-set seed). MODAL partition (40 dim,
        // weight 0.10) does the entire ranking. Spread should be narrower than chord-based
        // queries because only one of 5 similarity partitions is active.
        await RunAndDump("F# Lydian", 5);
    }

    // ─── Probe 5: Multi-partition hit (STRUCTURE + SYMBOLIC + MODAL implicit) ─
    [Test]
    public async Task P5_Cmaj7_Drop2_Jazz_TriplePartition()
    {
        // Why: chord seeds STRUCTURE; "drop2" + "jazz" seed SYMBOLIC; PC set also seeds
        // MODAL scoring vs every mode. Should rank HIGHER than bare "Cmaj7" if corpus has
        // drop2-jazz-tagged voicings containing Cmaj7 PC set.
        await RunAndDump("Cmaj7 drop2 jazz", 5);
    }

    // ─── Probe 6: Instrument filter ───────────────────────────────────────────
    [Test]
    public async Task P6_Am_UkuleleFilter()
    {
        // Why: HybridSearchAsync should route to the ukulele slice of the mmap. All
        // returned voicings must have instrument="ukulele" and MIDI notes in ukulele
        // range (roughly 55-84 for reentrant G-C-E-A tuning).
        SkipIfNoStrategy();
        var extracted = await _extractor!.ExtractAsync("Am");
        var q = _encoder!.Encode(extracted);
        var filters = new VoicingSearchFilters(VoicingType: "ukulele");
        var results = await _strategy!.HybridSearchAsync(q, filters, 5);

        TestContext.Out.WriteLine($"=== P6: 'Am' (ukulele) → {results.Count} results ===");
        foreach (var r in results)
        {
            var instr = r.Document.VoicingType ?? "?";
            var midi = r.Document.MidiNotes is { Length: > 0 } m
                ? $"[{m.Min()}..{m.Max()}]" : "none";
            TestContext.Out.WriteLine($"  score={r.Score:F4}  {instr}  {r.Document.Diagram}  midi={midi}");
            Assert.That(instr, Is.EqualTo("ukulele").IgnoreCase,
                "every result under instrument=ukulele filter must be a ukulele voicing.");
        }
    }

    // ─── Probe 7: Determinism ─────────────────────────────────────────────────
    [Test]
    public async Task P7_DeterministicRanking()
    {
        // Why: geometry-only retrieval = identical inputs should give identical outputs.
        // Catches hidden nondeterminism in parallel heap merging, float ordering, etc.
        SkipIfNoStrategy();
        var e = await _extractor!.ExtractAsync("Cmaj7");
        var q = _encoder!.Encode(e);
        var r1 = await _strategy!.SemanticSearchAsync(q, 10);
        var r2 = await _strategy!.SemanticSearchAsync(q, 10);

        var d1 = string.Join("|", r1.Select(x => x.Document.Diagram));
        var d2 = string.Join("|", r2.Select(x => x.Document.Diagram));
        TestContext.Out.WriteLine($"=== P7: determinism ===  run1 == run2 ? {d1 == d2}");
        Assert.That(d1, Is.EqualTo(d2),
            "two identical queries must return identical diagram sequences.");
    }

    // ─── Probe 8: Discrimination — distant chords yield disjoint top-K ────────
    [Test]
    public async Task P8_DisjointTopK_CmajVsFSharpDim()
    {
        SkipIfNoStrategy();
        var cMaj7 = _encoder!.Encode(await _extractor!.ExtractAsync("Cmaj7"));
        var fShd7 = _encoder!.Encode(await _extractor!.ExtractAsync("F#dim7"));
        var rA = await _strategy!.SemanticSearchAsync(cMaj7, 10);
        var rB = await _strategy!.SemanticSearchAsync(fShd7, 10);
        var overlap = rA.Select(x => x.Document.Diagram)
                        .Intersect(rB.Select(x => x.Document.Diagram))
                        .Count();
        TestContext.Out.WriteLine($"=== P8: Cmaj7 ∩ F#dim7 top-10 overlap = {overlap} ===");
        Assert.That(overlap, Is.LessThan(5),
            "distant harmonic qualities should yield mostly-disjoint top-10 sets.");
    }

    // ─── Probe 9: Vocabulary edge — German notation rejection ─────────────────
    [Test]
    public async Task P9_GermanNotation_Hm7_ShouldRejectCleanly()
    {
        // Why: "H" isn't in our root table (German "H" = B-natural). Typed parser should
        // reject → StructuredQuery empty → vocabulary-miss path. MCP tool would return
        // empty with "supply a chord" note; in-process path the encoder produces zero
        // vector which ranks arbitrarily. We just verify typed extractor declines.
        var e = await _extractor!.ExtractAsync("Hm7");
        TestContext.Out.WriteLine($"=== P9: 'Hm7' (German) → chord={e.ChordSymbol ?? "<null>"} tags={(e.Tags is null ? "<null>" : string.Join(",", e.Tags))} ===");
        Assert.That(e.ChordSymbol, Is.Null,
            "German H notation is not supported; typed parser must reject cleanly.");
    }

    // ─── Probe 10: Purely descriptive — honest-empty path ─────────────────────
    [Test]
    public async Task P10_FuzzyDescriptive_NoVocabMatch()
    {
        // Why: "something dreamy and moody" has no chord/mode/tag. Min-length-3 guard
        // prevents stop-words ("a", "me") from false-matching. Typed parser must return
        // empty; in real MCP flow the vocabulary tool lets Claude pre-canonicalize.
        var e = await _extractor!.ExtractAsync("something dreamy and moody");
        TestContext.Out.WriteLine(
            $"=== P10: 'something dreamy and moody' → chord={e.ChordSymbol ?? "<null>"} "
            + $"mode={e.ModeName ?? "<null>"} tags={(e.Tags is null ? "<null>" : string.Join(",", e.Tags))} ===");
        Assert.That(e.ChordSymbol, Is.Null);
    }

    // ─── helpers ──────────────────────────────────────────────────────────────

    private async Task RunAndDump(string query, int limit)
    {
        SkipIfNoStrategy();
        var extracted = await _extractor!.ExtractAsync(query);
        var qv = _encoder!.Encode(extracted);
        var results = await _strategy!.SemanticSearchAsync(qv, limit);

        TestContext.Out.WriteLine($"=== '{query}' → extracted: chord={extracted.ChordSymbol ?? "·"}"
            + $" mode={extracted.ModeName ?? "·"} tags={(extracted.Tags is null ? "·" : string.Join(",", extracted.Tags))}");
        TestContext.Out.WriteLine($"    top-{limit} from {_strategy!.GetStats().TotalVoicings} voicings:");
        foreach (var r in results)
        {
            var pcs = r.Document.MidiNotes is { Length: > 0 } m
                ? string.Join(",", m.Select(n => ((n % 12) + 12) % 12).Distinct().OrderBy(p => p))
                : "";
            TestContext.Out.WriteLine(
                $"    score={r.Score:F4}  {r.Document.VoicingType ?? "?"}  {r.Document.Diagram}  pcs={{{pcs}}}");
        }
        Assert.That(results.Count, Is.GreaterThan(0), $"query '{query}' returned no results.");
    }

    private static string? FindIndex()
    {
        var env = Environment.GetEnvironmentVariable("GA_OPTICK_INDEX_PATH");
        if (!string.IsNullOrWhiteSpace(env) && File.Exists(env)) return env;
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
