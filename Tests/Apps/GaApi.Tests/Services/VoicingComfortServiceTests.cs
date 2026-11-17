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

    // ── Ranking ───────────────────────────────────────────────────────────────

    // Frets hold string 1 (high E) first: { 0, 1, 0, 2, 3, -1 } is the open C x-3-2-0-1-0,
    // { 0, 1, 2, 2, 0, -1 } the open Am x-0-2-2-1-0, { 1, 1, 2, 3, 3, 1 } the F barre 1-3-3-2-1-1.
    [Test]
    [TestCase("C", new[] { 0, 1, 0, 2, 3, -1 })]
    [TestCase("Am", new[] { 0, 1, 2, 2, 0, -1 })]
    [TestCase("F", new[] { 1, 1, 2, 3, 3, 1 })]
    public async Task GetComfortRankedAsync_TopThreeIncludesTheStandardShape(string chord, int[] standardFrets)
    {
        var result = await _sut.GetComfortRankedAsync(chord);

        Assert.That(result.IsSuccess, Is.True);
        var topThree = result.GetValueOrThrow().Take(3).Select(r => r.Voicing.Frets).ToList();
        Assert.That(topThree, Has.Some.EqualTo(standardFrets),
            $"Top three for {chord}: {string.Join(" | ", topThree.Select(f => string.Join(",", f)))}");
    }

    [Test]
    public async Task GetComfortRankedAsync_RootPositionVoicingsComeBeforeInversions()
    {
        var result = await _sut.GetComfortRankedAsync("C");

        Assert.That(result.IsSuccess, Is.True);
        // Bass = last sounding string (string 6 is last); C is pitch class 0.
        int[] openStringPitchClasses = [4, 11, 7, 2, 9, 4];
        var rootInBass = result.GetValueOrThrow()
            .Select(r =>
            {
                var bass = Array.FindLastIndex(r.Voicing.Frets, f => f >= 0);
                return (openStringPitchClasses[bass] + r.Voicing.Frets[bass]) % 12 == 0;
            })
            .ToList();
        Assert.That(rootInBass, Is.EqualTo(rootInBass.OrderByDescending(b => b).ToList()),
            "Every root-position voicing must rank before every inversion");
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

    // ── Chord identity (issue #886) ───────────────────────────────────────────

    [Test]
    [TestCase("F", new[] { 5, 9, 0 })]
    [TestCase("C", new[] { 0, 4, 7 })]
    [TestCase("Am", new[] { 9, 0, 4 })]
    [TestCase("Bm", new[] { 11, 2, 6 })]
    [TestCase("G7", new[] { 7, 11, 2, 5 })]
    public async Task GetComfortRankedAsync_EveryVoicingSoundsExactlyTheRequestedChord(string chord, int[] expectedPcs)
    {
        var result = await _sut.GetComfortRankedAsync(chord);

        Assert.That(result.IsSuccess, Is.True);
        var ranked = result.GetValueOrThrow();
        Assert.That(ranked, Is.Not.Empty);
        foreach (var r in ranked)
        {
            Assert.That(SoundingPitchClasses(r.Voicing.Frets), Is.EquivalentTo(expectedPcs),
                $"Voicing [{string.Join(",", r.Voicing.Frets)}] is not {chord}");
        }
    }

    [Test]
    [TestCase("C")]
    [TestCase("F")]
    [TestCase("G")]
    [TestCase("Am")]
    [TestCase("Bm")]
    public async Task GetComfortRankedAsync_TopSuggestionsLabelledBeginnerStayInOpenPosition(string chord)
    {
        var result = await _sut.GetComfortRankedAsync(chord);

        Assert.That(result.IsSuccess, Is.True);
        foreach (var r in result.GetValueOrThrow().Take(3).Where(r => r.Voicing.Difficulty == "Beginner"))
        {
            Assert.That(r.Voicing.Frets.Max(), Is.LessThanOrEqualTo(5),
                $"A 'Beginner' suggestion must not reach above fret 5: [{string.Join(",", r.Voicing.Frets)}]");
        }
    }

    // Frets hold string 1 (high E) first, as Voicing.Positions does.
    private static int[] SoundingPitchClasses(int[] frets)
    {
        int[] openStringPitchClasses = [4, 11, 7, 2, 9, 4];
        return [.. frets
            .Select((fret, i) => fret < 0 ? -1 : (openStringPitchClasses[i] + fret) % 12)
            .Where(pc => pc >= 0)
            .Distinct()];
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
