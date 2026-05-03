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
    public void ComputeInterval_KnownPairs_ReturnsCorrectShape(
        string lower, string upper, string expectedName,
        string expectedQuality, string expectedSize, int expectedSemitones)
    {
        var result = MakeTool().ComputeInterval(lower, upper);

        Assert.That(result.Error, Is.Null, "valid input must not produce an Error");
        Assert.That(result.Name,      Is.EqualTo(expectedName),       $"name mismatch for {lower}→{upper}");
        Assert.That(result.Quality,   Is.EqualTo(expectedQuality),    $"quality mismatch for {lower}→{upper}");
        Assert.That(result.Size,      Is.EqualTo(expectedSize),       $"size mismatch for {lower}→{upper}");
        Assert.That(result.Semitones, Is.EqualTo(expectedSemitones),  $"semitone mismatch for {lower}→{upper}");
    }

    [Test]
    public void ComputeInterval_NormalizesNoteCasing()
    {
        // The LLM may emit lowercase or mixed case — the tool must be
        // case-insensitive and echo back canonical forms.
        var result = MakeTool().ComputeInterval("c", "g");

        Assert.That(result.Error,     Is.Null);
        Assert.That(result.LowerNote, Is.EqualTo("C"));
        Assert.That(result.UpperNote, Is.EqualTo("G"));
        Assert.That(result.Name,      Is.EqualTo("P5"));
    }

    [Test]
    public void ComputeInterval_HandlesAccidentals()
    {
        // F# → D is two cases that often confuse: enharmonic-aware naming
        // (F# rather than Gb) and crossing the octave (the canonical
        // "minor sixth" answer rather than "augmented fifth").
        var result = MakeTool().ComputeInterval("F#", "D");

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
    public void ComputeInterval_InvalidInputs_ReturnFailureResult(string lower, string upper)
    {
        var result = MakeTool().ComputeInterval(lower, upper);

        Assert.That(result.Error, Is.Not.Null,
            $"invalid input ('{lower}', '{upper}') must populate Error rather than throw");
        Assert.That(result.Error, Does.Contain("parse").IgnoreCase
            .Or.Contain("note").IgnoreCase,
            "Error message should mention parsing or note format so the LLM can recover");
    }

    [Test]
    public void ComputeInterval_DoesNotThrowOnNullArguments()
    {
        // Cancellation-safe: tools must convert bad input into structured Error,
        // never throw — an exception would crash the agent's tool-call loop.
        Assert.That(() => MakeTool().ComputeInterval(null!, "G"), Throws.Nothing);
        Assert.That(() => MakeTool().ComputeInterval("C", null!), Throws.Nothing);

        var nullLower = MakeTool().ComputeInterval(null!, "G");
        Assert.That(nullLower.Error, Is.Not.Null);
    }
}
