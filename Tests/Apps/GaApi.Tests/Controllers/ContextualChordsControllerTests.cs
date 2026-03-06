namespace GaApi.Tests.Controllers;

using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;

/// <summary>
///     Integration tests for <see cref="GaApi.Controllers.ContextualChordsController" />.
///     Covers the four endpoints: keys, scales, modes, and borrowed (modal interchange).
///     All endpoints are pure in-process domain logic — no external dependencies required.
/// </summary>
[TestFixture]
[Category("Integration")]
public class ContextualChordsControllerTests
{
    [OneTimeSetUp]
    public void OneTimeSetUp()
    {
        _factory = new();
        _client  = _factory.CreateClient();
    }

    [OneTimeTearDown]
    public void OneTimeTearDown()
    {
        _client?.Dispose();
        _factory?.Dispose();
    }

    private WebApplicationFactory<Program>? _factory;
    private HttpClient?                     _client;

    // ── GET /api/contextual-chords/modes/{modeName}/{rootName} ─────────────────

    [Test]
    public async Task ShouldReturn7Chords_WhenModeIsIonian()
    {
        var response = await _client!.GetAsync("/api/contextual-chords/modes/Ionian/C");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        var chords = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.That(chords.ValueKind, Is.EqualTo(JsonValueKind.Array));
        Assert.That(chords.GetArrayLength(), Is.EqualTo(7), "Ionian has 7 scale degrees");

        TestContext.WriteLine($"C Ionian chords: {string.Join(", ", chords.EnumerateArray().Select(c => c.GetProperty("contextualName").GetString()))}");
    }

    [Test]
    [TestCase("Dorian",     "D")]
    [TestCase("Phrygian",   "E")]
    [TestCase("Lydian",     "F")]
    [TestCase("Mixolydian", "G")]
    [TestCase("Aeolian",    "A")]
    [TestCase("Locrian",    "B")]
    public async Task ShouldReturn7Chords_ForEveryDiatonicMode(string mode, string root)
    {
        var response = await _client!.GetAsync($"/api/contextual-chords/modes/{mode}/{root}");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        var chords = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.That(chords.ValueKind, Is.EqualTo(JsonValueKind.Array));
        Assert.That(chords.GetArrayLength(), Is.EqualTo(7), $"{mode} should have 7 scale-degree chords");

        TestContext.WriteLine($"{root} {mode}: {chords.GetArrayLength()} chords");
    }

    [Test]
    public async Task ShouldReturn400_WhenModeNameIsUnknown()
    {
        var response = await _client!.GetAsync("/api/contextual-chords/modes/Blorp/C");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
    }

    [Test]
    public async Task ShouldReturn400_WhenRootNoteIsInvalid()
    {
        var response = await _client!.GetAsync("/api/contextual-chords/modes/Ionian/Z#");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
    }

    [Test]
    public async Task EachChordShouldHaveRequiredFields()
    {
        var response = await _client!.GetAsync("/api/contextual-chords/modes/Ionian/C");
        var chords   = await response.Content.ReadFromJsonAsync<JsonElement>();

        foreach (var chord in chords.EnumerateArray())
        {
            Assert.That(chord.TryGetProperty("templateName",    out _), Is.True, "missing templateName");
            Assert.That(chord.TryGetProperty("root",            out _), Is.True, "missing root");
            Assert.That(chord.TryGetProperty("contextualName",  out _), Is.True, "missing contextualName");
            Assert.That(chord.TryGetProperty("scaleDegree",     out _), Is.True, "missing scaleDegree");
            Assert.That(chord.TryGetProperty("romanNumeral",    out _), Is.True, "missing romanNumeral");
            Assert.That(chord.TryGetProperty("notes",           out _), Is.True, "missing notes");
        }
    }

    [Test]
    public async Task CMajorIonian_ShouldHaveCorrectRomanNumerals()
    {
        var response = await _client!.GetAsync("/api/contextual-chords/modes/Ionian/C");
        var chords   = await response.Content.ReadFromJsonAsync<JsonElement>();

        var numerals = chords.EnumerateArray()
            .Select(c => c.GetProperty("romanNumeral").GetString())
            .ToArray();

        // Ionian diatonic qualities: I ii iii IV V vi vii°
        Assert.That(numerals[0], Does.Match("^I$"),       "degree 1 = I (major)");
        Assert.That(numerals[1], Does.Match("^ii"),       "degree 2 = ii (minor)");
        Assert.That(numerals[2], Does.Match("^iii"),      "degree 3 = iii (minor)");
        Assert.That(numerals[3], Does.Match("^IV$"),      "degree 4 = IV (major)");
        Assert.That(numerals[4], Does.Match("^V$"),       "degree 5 = V (major)");
        Assert.That(numerals[5], Does.Match("^vi"),       "degree 6 = vi (minor)");
        Assert.That(numerals[6], Does.Match("°|dim"),     "degree 7 = vii° (diminished)");

        TestContext.WriteLine("Roman numerals: " + string.Join(" ", numerals));
    }

    // ── GET /api/contextual-chords/keys/{keyName} ──────────────────────────────

    [Test]
    [TestCase("C Major")]
    [TestCase("G Major")]
    [TestCase("D Minor")]
    [TestCase("Am")]
    public async Task ShouldReturn7Chords_ForSupportedKeyFormats(string key)
    {
        var response = await _client!.GetAsync($"/api/contextual-chords/keys/{Uri.EscapeDataString(key)}");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        var chords = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.That(chords.GetArrayLength(), Is.EqualTo(7));

        TestContext.WriteLine($"Key '{key}': {chords.GetArrayLength()} chords");
    }

    [Test]
    public async Task ShouldReturn400_WhenKeyNameIsGibberish()
    {
        var response = await _client!.GetAsync("/api/contextual-chords/keys/NotAKey999");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
    }

    // ── GET /api/contextual-chords/scales/{scaleName}/{rootName} ──────────────

    [Test]
    public async Task ShouldReturn7Chords_ForCMajorScale()
    {
        var response = await _client!.GetAsync("/api/contextual-chords/scales/Major/C");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        var chords = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.That(chords.GetArrayLength(), Is.EqualTo(7));
    }

    [Test]
    public async Task ShouldReturn400_WhenScaleIsUnsupported()
    {
        var response = await _client!.GetAsync("/api/contextual-chords/scales/WholeTone/C");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
    }

    // ── GET /api/contextual-chords/borrowed/{keyName} ─────────────────────────

    [Test]
    public async Task BorrowedChords_ShouldReturnNonEmptyList_ForCMajor()
    {
        var response = await _client!.GetAsync("/api/contextual-chords/borrowed/C%20Major");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        var chords = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.That(chords.ValueKind, Is.EqualTo(JsonValueKind.Array));
        Assert.That(chords.GetArrayLength(), Is.GreaterThan(0), "C Major should have borrowed chords from parallel modes");

        TestContext.WriteLine($"C Major borrowed chords ({chords.GetArrayLength()}):");
        foreach (var c in chords.EnumerateArray())
        {
            var name = c.GetProperty("contextualName").GetString();
            var src  = c.GetProperty("sourceMode").GetString();
            TestContext.WriteLine($"  {name}  ←  {src}");
        }
    }

    [Test]
    public async Task BorrowedChords_ShouldHaveSourceModeField()
    {
        var response = await _client!.GetAsync("/api/contextual-chords/borrowed/C%20Major");
        var chords   = await response.Content.ReadFromJsonAsync<JsonElement>();

        foreach (var chord in chords.EnumerateArray())
        {
            Assert.That(chord.TryGetProperty("sourceMode", out var src), Is.True, "missing sourceMode");
            Assert.That(src.GetString(), Is.Not.Null.And.Not.Empty,               "sourceMode must be non-empty");
        }
    }

    [Test]
    public async Task BorrowedChords_ShouldNotContainHomeModeChords()
    {
        // First, get the home key's chord symbols
        var homeResponse = await _client!.GetAsync("/api/contextual-chords/modes/Ionian/C");
        var homeChords   = await homeResponse.Content.ReadFromJsonAsync<JsonElement>();
        var homeSymbols  = homeChords.EnumerateArray()
            .Select(c => c.GetProperty("contextualName").GetString()!)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        // Then get borrowed chords
        var borrowedResponse = await _client!.GetAsync("/api/contextual-chords/borrowed/C%20Major");
        var borrowed         = await borrowedResponse.Content.ReadFromJsonAsync<JsonElement>();

        foreach (var chord in borrowed.EnumerateArray())
        {
            var symbol = chord.GetProperty("contextualName").GetString()!;
            Assert.That(homeSymbols, Does.Not.Contain(symbol),
                $"Chord '{symbol}' already exists in C Major — should not appear as borrowed");
        }
    }

    [Test]
    public async Task BorrowedChords_ShouldReturn400_WhenKeyIsInvalid()
    {
        var response = await _client!.GetAsync("/api/contextual-chords/borrowed/NotAKey");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
    }
}
