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

    // ── Chord identity: complete triads, essential tones for extended chords (#886) ─────────

    // Frets hold string 1 (high E) first: { 5, 3, 3, 2, 3, -1 } is C13 x-3-2-3-3-5,
    // { 3, 3, 3, 2, 3, -1 } is C9 x-3-2-3-3-3.
    [Test]
    [TestCase("C13", new[] { 5, 3, 3, 2, 3, -1 })]
    [TestCase("C9", new[] { 3, 3, 3, 2, 3, -1 })]
    public async Task GetVoicingsForChordAsync_KeepsTheStandardShape_ForExtendedChords(string chord, int[] frets)
    {
        var results = await _sut.GetVoicingsForChordAsync(chord);

        Assert.That(results.Select(v => v.Frets), Has.Some.EqualTo(frets),
            $"{chord} must keep its standard shape [{string.Join(",", frets)}]");
    }

    // Root, third, seventh and the defining extension must always sound; only the perfect fifth,
    // then a natural 11th under a 13th, then the 9th may be left out.
    [Test]
    [TestCase("F", new[] { 5, 9, 0 })]
    [TestCase("G7", new[] { 7, 11, 2, 5 })]
    [TestCase("C9", new[] { 0, 4, 10, 2 })]
    [TestCase("C11", new[] { 0, 4, 10, 5 })]
    [TestCase("C13", new[] { 0, 4, 10, 9 })]
    public async Task GetVoicingsForChordAsync_EveryVoicingSoundsTheEssentialTones(string chord, int[] essential)
    {
        var results = (await _sut.GetVoicingsForChordAsync(chord)).ToList();

        Assert.That(results, Is.Not.Empty, $"Expected voicings for '{chord}'");
        int[] openStringPitchClasses = [4, 11, 7, 2, 9, 4];
        foreach (var v in results)
        {
            var sounding = v.Frets
                .Select((fret, i) => fret < 0 ? -1 : (openStringPitchClasses[i] + fret) % 12)
                .Where(pc => pc >= 0)
                .ToHashSet();
            Assert.That(sounding, Is.SupersetOf(essential),
                $"Voicing [{string.Join(",", v.Frets)}] lacks an essential tone of {chord}");
        }
    }

    // ── Invalid chord parsing ────────────────────────────────────────────────

    [Test]
    public void GetVoicingsForChordAsync_Throws_WhenChordSymbolIsGibberish() =>
        Assert.ThrowsAsync<ArgumentException>(() =>
            _sut.GetVoicingsForChordAsync("NotAChord!!!"));
}
