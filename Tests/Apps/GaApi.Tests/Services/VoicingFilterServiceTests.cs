namespace GaApi.Tests.Services;

using GaApi.Services;

[TestFixture]
[Category("Unit")]
public class VoicingFilterServiceTests
{
    private VoicingFilterService _sut = null!;

    [SetUp]
    public void SetUp() => _sut = new VoicingFilterService();

    // ── Happy-path voicing generation ────────────────────────────────────────

    [Test]
    [TestCase("C")]
    [TestCase("Am")]
    [TestCase("G7")]
    [TestCase("Dm")]
    [TestCase("Fmaj7")]
    public async Task GetVoicingsForChordAsync_ReturnsVoicings_ForKnownChords(string chord)
    {
        var results = await _sut.GetVoicingsForChordAsync(chord);

        Assert.That(results, Is.Not.Empty, $"Expected voicings for '{chord}'");
    }

    [Test]
    public async Task GetVoicingsForChordAsync_ResultsAreSortedByDifficulty()
    {
        var results = (await _sut.GetVoicingsForChordAsync("C")).ToList();

        var scores = results.Select(v => v.DifficultyScore).ToList();
        Assert.That(scores, Is.EqualTo(scores.OrderBy(s => s).ToList()),
            "Voicings should be ordered by ascending difficulty score");
    }

    [Test]
    public async Task GetVoicingsForChordAsync_EachVoicingHasRequiredFields()
    {
        var results = (await _sut.GetVoicingsForChordAsync("C")).Take(5);
        foreach (var v in results)
        {
            Assert.That(v.ChordName,   Is.Not.Null.And.Not.Empty);
            Assert.That(v.Frets,       Is.Not.Null);
            Assert.That(v.Difficulty,  Is.Not.Null.And.Not.Empty);
        }
    }

    // ── maxDifficulty filter ─────────────────────────────────────────────────

    [Test]
    public async Task GetVoicingsForChordAsync_MaxDifficultyFilter_ExcludesHardVoicings()
    {
        var easy = (await _sut.GetVoicingsForChordAsync("C", maxDifficulty: 3)).ToList();
        var all  = (await _sut.GetVoicingsForChordAsync("C")).ToList();

        // easy set should be a strict subset (or equal) of all
        Assert.That(easy.Count, Is.LessThanOrEqualTo(all.Count));
        Assert.That(easy.All(v => v.DifficultyScore <= 3), Is.True,
            "All returned voicings should respect maxDifficulty");
    }

    // ── noOpenStrings filter ─────────────────────────────────────────────────

    [Test]
    public async Task GetVoicingsForChordAsync_NoOpenStrings_ExcludesOpenStringVoicings()
    {
        var results = (await _sut.GetVoicingsForChordAsync("C", noOpenStrings: true)).ToList();

        // Every fret in every voicing must be > 0 (−1 = muted/unplayed is OK)
        foreach (var v in results)
            Assert.That(v.Frets.Where(f => f >= 0).All(f => f > 0), Is.True,
                $"Voicing {string.Join(",", v.Frets)} contains an open string");
    }

    // ── Invalid chord parsing ────────────────────────────────────────────────

    [Test]
    public void GetVoicingsForChordAsync_Throws_WhenChordSymbolIsGibberish() =>
        Assert.ThrowsAsync<ArgumentException>(() =>
            _sut.GetVoicingsForChordAsync("NotAChord!!!"));
}
