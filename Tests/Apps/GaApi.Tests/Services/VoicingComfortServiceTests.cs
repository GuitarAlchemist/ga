namespace GaApi.Tests.Services;

using GaApi.Models;
using GaApi.Services;

[TestFixture]
[Category("Unit")]
public class VoicingComfortServiceTests
{
    private VoicingFilterService _filterService = null!;
    private VoicingComfortService _sut           = null!;

    [SetUp]
    public void SetUp()
    {
        _filterService = new VoicingFilterService();
        _sut           = new VoicingComfortService(_filterService);
    }

    // ── Stretch calculation ───────────────────────────────────────────────────

    [Test]
    public async Task GetComfortRankedAsync_ResultsSortedByStretchAscending()
    {
        var result = await _sut.GetComfortRankedAsync("C");

        Assert.That(result.IsSuccess, Is.True);
        var ranked = result.GetValueOrThrow().ToList();
        var stretches = ranked.Select(r => r.Stretch).ToList();
        Assert.That(stretches, Is.EqualTo(stretches.OrderBy(s => s).ToList()),
            "Results must be ordered by stretch ascending");
    }

    [Test]
    public async Task GetComfortRankedAsync_StretchExcludesOpenStrings()
    {
        // Open-position C has open strings (fret 0) — stretch should only count fretted strings
        var result = await _sut.GetComfortRankedAsync("C", excludeFullBarre: false);

        Assert.That(result.IsSuccess, Is.True);
        foreach (var r in result.GetValueOrThrow())
        {
            var frettedFrets = r.Voicing.Frets.Where(f => f > 0).ToList();
            var expectedStretch = frettedFrets.Count == 0 ? 0 : frettedFrets.Max() - frettedFrets.Min();
            Assert.That(r.Stretch, Is.EqualTo(expectedStretch),
                $"Stretch mismatch for voicing [{string.Join(",", r.Voicing.Frets)}]");
        }
    }

    // ── Barre detection ───────────────────────────────────────────────────────

    [Test]
    public async Task GetComfortRankedAsync_ExcludesBarre_WhenEnabled()
    {
        // Bm is commonly a full barre chord — default excludeFullBarre=true
        var excluded = await _sut.GetComfortRankedAsync("Bm", excludeFullBarre: true);
        var included = await _sut.GetComfortRankedAsync("Bm", excludeFullBarre: false);

        Assert.That(excluded.IsSuccess, Is.True);
        Assert.That(included.IsSuccess, Is.True);

        // With barre excluded, count should be <= count with barre included
        Assert.That(excluded.GetValueOrThrow().Count, Is.LessThanOrEqualTo(included.GetValueOrThrow().Count));
    }

    [Test]
    public async Task GetComfortRankedAsync_IncludesBarre_WhenDisabled()
    {
        var result = await _sut.GetComfortRankedAsync("Bm", excludeFullBarre: false);

        Assert.That(result.IsSuccess, Is.True);
        Assert.That(result.GetValueOrThrow(), Is.Not.Empty);
    }

    // ── Happy path ────────────────────────────────────────────────────────────

    [Test]
    [TestCase("C")]
    [TestCase("Am")]
    [TestCase("G7")]
    public async Task GetComfortRankedAsync_ReturnsSuccess_ForKnownChords(string chord)
    {
        var result = await _sut.GetComfortRankedAsync(chord);

        Assert.That(result.IsSuccess, Is.True, $"Expected success for '{chord}'");
        Assert.That(result.GetValueOrThrow(), Is.Not.Empty);
    }

    // ── Error propagation ─────────────────────────────────────────────────────

    [Test]
    public async Task GetComfortRankedAsync_ReturnsFailure_WhenChordIsInvalid()
    {
        var result = await _sut.GetComfortRankedAsync("NotAChord!!!");

        Assert.That(result.IsSuccess, Is.False);
        Assert.That(result.GetErrorOrThrow(), Is.EqualTo(VoicingFilterError.InvalidChordSymbol));
    }
}
