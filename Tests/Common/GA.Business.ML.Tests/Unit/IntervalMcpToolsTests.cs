namespace GA.Business.ML.Tests.Unit;

using GA.Business.ML.Agents.Mcp;

[TestFixture]
public class IntervalMcpToolsTests
{
    private static IntervalMcpTools MakeTool() => new();

    // Canonical pairs from any first-year theory text — these are the cases an
    // LLM is most likely to call the tool for, so getting them right is the
    // load-bearing acceptance criterion.
    [TestCase("C", "G",  "P5",  "perfect",    "fifth",   7)]
    [TestCase("C", "E",  "M3",  "major",      "third",   4)]
    [TestCase("C", "Eb", "m3",  "minor",      "third",   3)]
    [TestCase("C", "F",  "P4",  "perfect",    "fourth",  5)]
    [TestCase("C", "C",  "P1",  "perfect",    "unison",  0)]
    [TestCase("A", "E",  "P5",  "perfect",    "fifth",   7)]
    public void IntervalCompute_KnownPairs_ReturnsCorrectShape(
        string lower, string upper, string expectedName,
        string expectedQuality, string expectedSize, int expectedSemitones)
    {
        var result = MakeTool().IntervalCompute(lower, upper);

        Assert.That(result.Error, Is.Null, "valid input must not produce an Error");
        Assert.That(result.Name,      Is.EqualTo(expectedName),       $"name mismatch for {lower}→{upper}");
        Assert.That(result.Quality,   Is.EqualTo(expectedQuality),    $"quality mismatch for {lower}→{upper}");
        Assert.That(result.Size,      Is.EqualTo(expectedSize),       $"size mismatch for {lower}→{upper}");
        Assert.That(result.Semitones, Is.EqualTo(expectedSemitones),  $"semitone mismatch for {lower}→{upper}");
    }

    [Test]
    public void IntervalCompute_NormalizesNoteCasing()
    {
        // The LLM may emit lowercase or mixed case — the tool must be
        // case-insensitive and echo back canonical forms.
        var result = MakeTool().IntervalCompute("c", "g");

        Assert.That(result.Error,     Is.Null);
        Assert.That(result.LowerNote, Is.EqualTo("C"));
        Assert.That(result.UpperNote, Is.EqualTo("G"));
        Assert.That(result.Name,      Is.EqualTo("P5"));
    }

    [Test]
    public void IntervalCompute_HandlesAccidentals()
    {
        // F# → D is two cases that often confuse: enharmonic-aware naming
        // (F# rather than Gb) and crossing the octave (the canonical
        // "minor sixth" answer rather than "augmented fifth").
        var result = MakeTool().IntervalCompute("F#", "D");

        Assert.That(result.Error,    Is.Null);
        Assert.That(result.Quality,  Is.EqualTo("minor"));
        Assert.That(result.Size,     Is.EqualTo("sixth"));
        Assert.That(result.Semitones, Is.EqualTo(8));
    }

    [TestCase("",          "G")]
    [TestCase("Q",         "G")]
    [TestCase("C",         "")]
    [TestCase("C",         "Z")]
    [TestCase("notanote",  "G")]
    public void IntervalCompute_InvalidInputs_ReturnFailureResult(string lower, string upper)
    {
        var result = MakeTool().IntervalCompute(lower, upper);

        Assert.That(result.Error, Is.Not.Null,
            $"invalid input ('{lower}', '{upper}') must populate Error rather than throw");
        Assert.That(result.Error, Does.Contain("parse").IgnoreCase
            .Or.Contain("note").IgnoreCase,
            "Error message should mention parsing or note format so the LLM can recover");
    }

    [Test]
    public void IntervalCompute_DoesNotThrowOnNullArguments()
    {
        // Cancellation-safe: tools must convert bad input into structured Error,
        // never throw — an exception would crash the agent's tool-call loop.
        Assert.That(() => MakeTool().IntervalCompute(null!, "G"), Throws.Nothing);
        Assert.That(() => MakeTool().IntervalCompute("C", null!), Throws.Nothing);

        var nullLower = MakeTool().IntervalCompute(null!, "G");
        Assert.That(nullLower.Error, Is.Not.Null);
    }

    [Test]
    public void IntervalCompute_DescendingInput_ComputesSimpleInterval()
    {
        // The SKILL.md tells the LLM to pass first-mentioned as lower, but if
        // the LLM gets the order wrong the tool must still return a sensible
        // simple interval rather than throwing or returning negative semitones.
        // G → C is a perfect fourth (5 semitones) when read as a simple interval.
        var result = MakeTool().IntervalCompute("G", "C");

        Assert.That(result.Error, Is.Null);
        Assert.That(result.Quality,   Is.EqualTo("perfect"));
        Assert.That(result.Size,      Is.EqualTo("fourth"));
        Assert.That(result.Semitones, Is.EqualTo(5));
    }

    [Test]
    public void IntervalCompute_EnharmonicSpellings_ProduceDistinctNamesSameSemitones()
    {
        // C → D# is augmented second (A2); C → Eb is minor third (m3). Same
        // semitone count (3) but different theoretical spelling. The tool must
        // honour whichever spelling the LLM passes — the worst failure mode here
        // is silently normalising one to the other.
        var augmented = MakeTool().IntervalCompute("C", "D#");
        var minor     = MakeTool().IntervalCompute("C", "Eb");

        Assert.That(augmented.Error, Is.Null);
        Assert.That(minor.Error,     Is.Null);
        Assert.That(augmented.Semitones, Is.EqualTo(minor.Semitones),
            "enharmonic spellings must agree on semitone count");
        Assert.That(augmented.Name, Is.Not.EqualTo(minor.Name),
            "but the short interval name MUST distinguish A2 from m3 — silently normalising would erase a real theoretical distinction");
    }

    [Test]
    public void IntervalCompute_PathologicallyLongInput_ReturnsErrorWithoutAllocating()
    {
        // 10 KB input — defends against a wedged or malicious LLM passing
        // unbounded strings that would otherwise force Note.{Sharp,Flat}.TryParse
        // to allocate proportional Trim()/ToUpperInvariant() intermediates.
        // The MaxNoteTokenLength guard short-circuits before any allocation.
        var huge = new string('C', 10_000);

        var sw = System.Diagnostics.Stopwatch.StartNew();
        var result = MakeTool().IntervalCompute(huge, "G");
        sw.Stop();

        Assert.That(result.Error, Is.Not.Null);
        // < 10 ms should be plenty even on a slow CI runner — proves the
        // length guard fired rather than the parser scanning the full input.
        Assert.That(sw.ElapsedMilliseconds, Is.LessThan(10),
            "long-input rejection must short-circuit, not scan the whole string");
    }

    [Test]
    public void IntervalCompute_ControlCharsInError_AreSanitized()
    {
        // Defends downstream renderers against log/prompt injection: the user's
        // bad input is echoed in the Error message, so the tool MUST strip
        // newlines, ANSI escapes, and other control characters before placing
        // them in the structured response.
        var esc      = (char)0x1B;
        var injected = "Q\n\r" + esc + "[31mFAKE LOG";
        var result   = MakeTool().IntervalCompute(injected, "G");

        Assert.That(result.Error, Is.Not.Null);
        // Check for individual control bytes via char predicates rather than
        // Does.Not.Contain — NUnit's string-contains is culture-aware and
        // can spuriously flag control characters (e.g. ESC) as "contained"
        // in strings that demonstrably don't include them.
        Assert.That(result.Error!.IndexOf('\n'), Is.EqualTo(-1),
            "newline must be stripped from echoed input — no fake-log-line forging");
        Assert.That(result.Error.IndexOf('\r'), Is.EqualTo(-1));
        Assert.That(result.Error.IndexOf(esc),  Is.EqualTo(-1),
            "ANSI escape sequences must not survive into the echoed Error");
        Assert.That(result.Error.All(c => !char.IsControl(c)), Is.True,
            "no control character of any kind should reach the echoed Error");
    }
}
