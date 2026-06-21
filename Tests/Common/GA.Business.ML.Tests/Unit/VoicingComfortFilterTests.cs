namespace GA.Business.ML.Tests.Unit;

using GA.Business.ML.Search;

/// <summary>
///     The shared comfort/ergonomic seam (ADR-0002). Pins its lenient unknown-bias (the opposite of
///     <see cref="VoicingFilterEngine"/>'s strict bias) and guards the diagram-parser bug fixed while
///     lifting comfort out of the GPU adapter.
/// </summary>
[TestFixture]
public class VoicingComfortFilterTests
{
    [Test]
    public void NoComfortFilter_IsInactive_AndMatchesEverything()
    {
        Assert.That(VoicingComfortFilter.IsActive(new VoicingSearchFilters()), Is.False);
        Assert.That(VoicingComfortFilter.Matches("133211", new VoicingSearchFilters()), Is.True);
    }

    [Test]
    public void UnparseableOrEmptyDiagram_IsLenient_Passes()
    {
        // Opposite bias to the metadata engine: a diagram we cannot analyze is KEPT, not rejected.
        var filters = new VoicingSearchFilters(MinComfortScore: 0.99);
        Assert.That(VoicingComfortFilter.IsActive(filters), Is.True);
        Assert.Multiple(() =>
        {
            Assert.That(VoicingComfortFilter.Matches(null, filters), Is.True);
            Assert.That(VoicingComfortFilter.Matches("", filters), Is.True);
            Assert.That(VoicingComfortFilter.Matches("   ", filters), Is.True);
        });
    }

    // Regression: the lifted parser must handle BOTH diagram formats. The old GPU-private parser split on
    // '-' only, so a compact "x35453" produced zero positions → comfort silently no-op'd for those voicings.
    [Test]
    public void ParseDiagram_HandlesCompactAndDashFormats_Equivalently()
    {
        var compact = VoicingComfortFilter.ParseDiagramToPositions("x35453");
        var dashed = VoicingComfortFilter.ParseDiagramToPositions("x-3-5-4-5-3");

        Assert.Multiple(() =>
        {
            Assert.That(compact, Has.Count.EqualTo(6), "compact diagram must parse to 6 string positions");
            Assert.That(dashed, Has.Count.EqualTo(compact.Count),
                "compact and dash diagram formats must yield the same number of positions");
        });
    }

    [Test]
    public void ParseDiagram_CompactFormat_IsNotSilentlyEmpty()
    {
        // The specific bug: compact format used to yield zero positions (→ lenient always-pass).
        Assert.That(VoicingComfortFilter.ParseDiagramToPositions("355433"), Is.Not.Empty);
    }

    // The parser must never THROW on an out-of-range / malformed fret token: it runs inside the
    // search's Parallel.ForEach / kernel filter, so a throw faults the whole query (the 2026-05-30
    // collapse-the-whole-search failure class). Out-of-range tokens are skipped rather than fed to the
    // throwing Fret/MidiNote constructors. (We assert no-throw, not a specific verdict: a partially
    // parseable diagram still yields analyzable positions, so its comfort verdict is the analyzer's.)
    [TestCase("x-3-2-0-1-40")]   // fret 40 > Fret max
    [TestCase("-5-3-2-0-1-0")]   // negative fret token
    [TestCase("x-99-99-99-99-99")]
    public void Matches_OutOfRangeDashFret_DoesNotThrow(string diagram)
    {
        var filters = new VoicingSearchFilters(MinComfortScore: 0.99, MustBeErgonomic: true);
        Assert.That(() => VoicingComfortFilter.Matches(diagram, filters), Throws.Nothing);
    }

    [Test]
    public void Matches_WhollyUnparseableDiagram_IsLenient_Passes()
    {
        // No token yields a valid position → zero positions → lenient keep (true).
        var filters = new VoicingSearchFilters(MinComfortScore: 0.99);
        Assert.That(VoicingComfortFilter.Matches("z-q-?-!", filters), Is.True);
    }
}
