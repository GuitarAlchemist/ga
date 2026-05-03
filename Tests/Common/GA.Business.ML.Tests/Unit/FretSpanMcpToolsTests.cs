namespace GA.Business.ML.Tests.Unit;

using GA.Business.ML.Agents.Mcp;

[TestFixture]
public class FretSpanMcpToolsTests
{
    private static FretSpanMcpTools MakeTool() => new();

    // The eight first-position open chords every beginner learns. The skill
    // computes Min/Max/Span over PRESSED frets only (excludes open strings = 0
    // and muted = -1), which is what matters for hand stretch.
    [TestCase("x-3-2-0-1-0", 1, 3, 2, 5, "easy")]       // C major: pressed [3,2,1]
    [TestCase("3-2-0-0-0-3", 2, 3, 1, 3, "very easy")]  // G major: pressed [3,2,3]
    [TestCase("x-x-0-2-3-2", 2, 3, 1, 3, "very easy")]  // D major: pressed [2,3,2]
    [TestCase("x-0-2-2-2-0", 2, 2, 0, 1, "very easy")]  // A major: pressed [2,2,2]
    [TestCase("0-2-2-1-0-0", 1, 2, 1, 3, "very easy")]  // E major: pressed [2,2,1]
    [TestCase("x-0-2-2-1-0", 1, 2, 1, 3, "very easy")]  // A minor: pressed [2,2,1]
    [TestCase("0-2-2-0-0-0", 2, 2, 0, 1, "very easy")]  // E minor: pressed [2,2]
    [TestCase("x-x-0-2-3-1", 1, 3, 2, 5, "easy")]       // D minor: pressed [2,3,1]
    public void ComputeSpan_OpenChords_ReturnCorrectShape(
        string diagram, int expectedMin, int expectedMax, int expectedSpan,
        int expectedScore, string expectedDifficultyKeyword)
    {
        var result = MakeTool().ComputeSpan(diagram);

        Assert.That(result.Error,            Is.Null,                  $"valid diagram must not produce an Error for {diagram}");
        Assert.That(result.MinFret,          Is.EqualTo(expectedMin),  $"MinFret mismatch for {diagram}");
        Assert.That(result.MaxFret,          Is.EqualTo(expectedMax),  $"MaxFret mismatch for {diagram}");
        Assert.That(result.Span,             Is.EqualTo(expectedSpan), $"Span mismatch for {diagram}");
        Assert.That(result.PlayabilityScore, Is.EqualTo(expectedScore));
        Assert.That(result.Difficulty,       Does.Contain(expectedDifficultyKeyword));
    }

    [Test]
    public void ComputeSpan_AcceptsCompactForm()
    {
        // x32010 is the compact form of x-3-2-0-1-0 (C major).
        var compact = MakeTool().ComputeSpan("x32010");
        var dashed  = MakeTool().ComputeSpan("x-3-2-0-1-0");

        Assert.That(compact.Error, Is.Null);
        Assert.That(compact.Diagram,          Is.EqualTo(dashed.Diagram),
            "compact form must normalize to dashed form for echo consistency");
        Assert.That(compact.Frets,            Is.EqualTo(dashed.Frets));
        Assert.That(compact.Span,             Is.EqualTo(dashed.Span));
        Assert.That(compact.PlayabilityScore, Is.EqualTo(dashed.PlayabilityScore));
    }

    [Test]
    public void ComputeSpan_FourFretStretch_FlagsAsChallenging()
    {
        // 4-fret stretch — barre chords with a 5-fret range live here. The
        // skill's difficulty switch maps span=4 to "challenging".
        // x-3-2-x-3-7: pressed [3,2,3,7] → span 5 (very wide).
        // x-3-2-x-3-6: pressed [3,2,3,6] → span 4 (challenging).
        var result = MakeTool().ComputeSpan("x-3-2-x-3-6");

        Assert.That(result.Error, Is.Null);
        Assert.That(result.Span, Is.EqualTo(4));
        Assert.That(result.Difficulty, Does.Contain("challenging"));
        Assert.That(result.PlayabilityScore, Is.GreaterThanOrEqualTo(7));
    }

    [Test]
    public void ComputeSpan_VeryWideStretch_DescribedAsWide()
    {
        // 5+ fret span goes into the catch-all "very wide" branch.
        // x-3-x-2-3-9: pressed [3,2,3,9] → span 7.
        var result = MakeTool().ComputeSpan("x-3-x-2-3-9");

        Assert.That(result.Error, Is.Null);
        Assert.That(result.Span, Is.EqualTo(7));
        Assert.That(result.Difficulty, Does.Contain("wide"),
            "spans of 5+ frets fall into the 'very wide' difficulty branch");
    }

    [Test]
    public void ComputeSpan_MutedAndOpenOnly_ReturnsError()
    {
        // Diagram has no fretted notes — span is undefined.
        var result = MakeTool().ComputeSpan("x-0-0-0-0-x");

        Assert.That(result.Error, Is.Not.Null);
        Assert.That(result.Error, Does.Contain("open").Or.Contain("muted"));
    }

    [TestCase("")]
    [TestCase("not a diagram")]
    [TestCase("x-3-2-0-1")]      // 5 positions instead of 6
    [TestCase("x-y-2-0-1-0")]    // invalid token 'y'
    public void ComputeSpan_InvalidInputs_ReturnFailureResult(string diagram)
    {
        var result = MakeTool().ComputeSpan(diagram);

        Assert.That(result.Error, Is.Not.Null,
            $"invalid input '{diagram}' must populate Error rather than throw");
        Assert.That(result.Frets, Is.Empty);
    }

    [Test]
    public void ComputeSpan_DoesNotThrowOnNullArgument()
    {
        Assert.That(() => MakeTool().ComputeSpan(null!), Throws.Nothing);
        var result = MakeTool().ComputeSpan(null!);
        Assert.That(result.Error, Is.Not.Null);
    }

    [Test]
    public void ComputeSpan_PathologicallyLongInput_ReturnsErrorWithoutScanning()
    {
        var huge = new string('3', 10_000);

        var sw = System.Diagnostics.Stopwatch.StartNew();
        var result = MakeTool().ComputeSpan(huge);
        sw.Stop();

        Assert.That(result.Error, Is.Not.Null);
        Assert.That(sw.ElapsedMilliseconds, Is.LessThan(10),
            "long-input rejection must short-circuit, not scan the whole string");
    }

    [Test]
    public void ComputeSpan_ControlCharsInError_AreSanitized()
    {
        var esc      = (char)0x1B;
        var injected = "Q\n\r" + esc + "[31m";
        var result   = MakeTool().ComputeSpan(injected);

        Assert.That(result.Error, Is.Not.Null);
        Assert.That(result.Error!.IndexOf('\n'), Is.EqualTo(-1));
        Assert.That(result.Error.IndexOf('\r'), Is.EqualTo(-1));
        Assert.That(result.Error.IndexOf(esc),  Is.EqualTo(-1));
        Assert.That(result.Error.All(c => !char.IsControl(c)), Is.True);
    }

    [Test]
    public void ComputeSpan_NormalizesDiagram_WithMutedTokensAsLowercaseX()
    {
        // The skill emits the canonical 'x' (lowercase) regardless of input case.
        var result = MakeTool().ComputeSpan("X-3-2-0-1-0");

        Assert.That(result.Error,   Is.Null);
        Assert.That(result.Diagram, Does.StartWith("x-"),
            "mute tokens normalize to lowercase 'x' in the echoed diagram");
    }
}
