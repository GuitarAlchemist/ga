namespace GaApi.Tests.Services;

using GaApi.Services;

[TestFixture]
[Category("Unit")]
public class ContextualChordServiceTests
{
    private ContextualChordService _sut = null!;

    [SetUp]
    public void SetUp() => _sut = new ContextualChordService();

    // ── GetChordsForModeAsync ────────────────────────────────────────────────

    [Test]
    [TestCase("Ionian",     "C")]
    [TestCase("Dorian",     "D")]
    [TestCase("Phrygian",   "E")]
    [TestCase("Lydian",     "F")]
    [TestCase("Mixolydian", "G")]
    [TestCase("Aeolian",    "A")]
    [TestCase("Locrian",    "B")]
    public async Task GetChordsForModeAsync_Returns7Chords_ForEveryDiatonicMode(string mode, string root)
    {
        var result = await _sut.GetChordsForModeAsync(mode, root);

        Assert.That(result.IsSuccess, Is.True, $"Expected success for {mode}/{root}");
        Assert.That(result.GetValueOrThrow().Count(), Is.EqualTo(7));
    }

    [Test]
    public async Task GetChordsForModeAsync_Fails_WhenModeIsUnknown()
    {
        var result = await _sut.GetChordsForModeAsync("Blorp", "C");

        Assert.That(result.IsFailure, Is.True);
        Assert.That(result.GetErrorOrThrow(), Does.Contain("Blorp"));
    }

    [Test]
    public async Task GetChordsForModeAsync_Fails_WhenRootNoteIsInvalid()
    {
        var result = await _sut.GetChordsForModeAsync("Ionian", "Z#");

        Assert.That(result.IsFailure, Is.True);
    }

    [Test]
    public async Task GetChordsForModeAsync_CMajorIonian_HasCorrectRomanNumerals()
    {
        var result  = await _sut.GetChordsForModeAsync("Ionian", "C");
        var numerals = result.GetValueOrThrow().Select(c => c.RomanNumeral).ToArray();

        // I  ii  iii  IV  V  vi  vii°
        Assert.That(numerals[0], Does.Match("^I$"),   "degree 1 = I");
        Assert.That(numerals[1], Does.Match("^ii"),   "degree 2 = ii");
        Assert.That(numerals[2], Does.Match("^iii"),  "degree 3 = iii");
        Assert.That(numerals[3], Does.Match("^IV$"),  "degree 4 = IV");
        Assert.That(numerals[4], Does.Match("^V$"),   "degree 5 = V");
        Assert.That(numerals[5], Does.Match("^vi"),   "degree 6 = vi");
        Assert.That(numerals[6], Does.Match("°|dim"), "degree 7 = vii°");
    }

    [Test]
    public async Task GetChordsForModeAsync_EachChordHasRequiredFields()
    {
        var result = await _sut.GetChordsForModeAsync("Ionian", "C");
        foreach (var chord in result.GetValueOrThrow())
        {
            Assert.That(chord.TemplateName,   Is.Not.Null.And.Not.Empty, "missing TemplateName");
            Assert.That(chord.Root,           Is.Not.Null.And.Not.Empty, "missing Root");
            Assert.That(chord.ContextualName, Is.Not.Null.And.Not.Empty, "missing ContextualName");
            Assert.That(chord.ScaleDegree,    Is.Not.Null,               "missing ScaleDegree");
            Assert.That(chord.Notes,          Is.Not.Empty,              "missing Notes");
        }
    }

    // ── GetChordsForKeyAsync ─────────────────────────────────────────────────

    [Test]
    [TestCase("C Major")]
    [TestCase("G Major")]
    [TestCase("D Minor")]
    [TestCase("Am")]
    public async Task GetChordsForKeyAsync_Returns7Chords_ForSupportedKeys(string key)
    {
        var result = await _sut.GetChordsForKeyAsync(key);

        Assert.That(result.IsSuccess, Is.True, $"Expected success for key '{key}'");
        Assert.That(result.GetValueOrThrow().Count(), Is.EqualTo(7));
    }

    [Test]
    public async Task GetChordsForKeyAsync_Fails_WhenKeyIsGibberish()
    {
        var result = await _sut.GetChordsForKeyAsync("NotAKey999");

        Assert.That(result.IsFailure, Is.True);
    }

    // ── GetChordsForScaleAsync ───────────────────────────────────────────────

    [Test]
    public async Task GetChordsForScaleAsync_Returns7Chords_ForMajorScale()
    {
        var result = await _sut.GetChordsForScaleAsync("Major", "C");

        Assert.That(result.IsSuccess, Is.True);
        Assert.That(result.GetValueOrThrow().Count(), Is.EqualTo(7));
    }

    [Test]
    public async Task GetChordsForScaleAsync_Fails_ForUnsupportedScale()
    {
        var result = await _sut.GetChordsForScaleAsync("WholeTone", "C");

        Assert.That(result.IsFailure, Is.True);
        Assert.That(result.GetErrorOrThrow(), Does.Contain("WholeTone"));
    }

    // ── GetBorrowedChordsAsync ───────────────────────────────────────────────

    [Test]
    public async Task GetBorrowedChordsAsync_ReturnsNonEmptyList_ForCMajor()
    {
        var result = await _sut.GetBorrowedChordsAsync("C Major");

        Assert.That(result.IsSuccess, Is.True);
        Assert.That(result.GetValueOrThrow(), Is.Not.Empty, "C Major should have borrowed chords");
    }

    [Test]
    public async Task GetBorrowedChordsAsync_AllChordHaveSourceModeField()
    {
        var result = await _sut.GetBorrowedChordsAsync("C Major");
        foreach (var chord in result.GetValueOrThrow())
            Assert.That(chord.SourceMode, Is.Not.Null.And.Not.Empty, "missing SourceMode");
    }

    [Test]
    public async Task GetBorrowedChordsAsync_DoesNotIncludeHomeModeChords()
    {
        var homeResult     = await _sut.GetChordsForModeAsync("Ionian", "C");
        var homeSymbols    = homeResult.GetValueOrThrow()
            .Select(c => c.ContextualName!)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var borrowedResult = await _sut.GetBorrowedChordsAsync("C Major");
        foreach (var chord in borrowedResult.GetValueOrThrow())
        {
            Assert.That(homeSymbols, Does.Not.Contain(chord.ContextualName),
                $"'{chord.ContextualName}' is in C Major and should not appear as borrowed");
        }
    }

    [Test]
    public async Task GetBorrowedChordsAsync_Fails_WhenKeyIsInvalid()
    {
        var result = await _sut.GetBorrowedChordsAsync("NotAKey");

        Assert.That(result.IsFailure, Is.True);
    }
}
