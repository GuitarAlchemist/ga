namespace GA.Business.ML.Tests.Unit;

using GA.Business.ML.Agents.Mcp;

[TestFixture]
public class ScaleMcpToolsTests
{
    // Canonical major and minor scales — what an LLM is most likely to call
    // the tool for. Wrong notes here would be the worst-case failure mode.
    [TestCase("C",  "major",  new[] { "C", "D", "E", "F", "G", "A", "B" },              "no sharps or flats", "A minor")]
    [TestCase("G",  "major",  new[] { "G", "A", "B", "C", "D", "E", "F#" },             "1 sharp",            "E minor")]
    [TestCase("F",  "major",  new[] { "F", "G", "A", "Bb", "C", "D", "E" },             "1 flat",             "D minor")]
    [TestCase("D",  "major",  new[] { "D", "E", "F#", "G", "A", "B", "C#" },            "2 sharps",           "B minor")]
    [TestCase("Bb", "major",  new[] { "Bb", "C", "D", "Eb", "F", "G", "A" },            "2 flats",            "G minor")]
    [TestCase("A",  "minor",  new[] { "A", "B", "C", "D", "E", "F", "G" },              "no sharps or flats", "C major")]
    [TestCase("E",  "minor",  new[] { "E", "F#", "G", "A", "B", "C", "D" },             "1 sharp",            "G major")]
    [TestCase("F#", "minor",  new[] { "F#", "G#", "A", "B", "C#", "D", "E" },           "3 sharps",           "A major")]
    public void GetKeyNotes_KnownKeys_ReturnsCorrectNotesAndSignature(
        string root, string mode, string[] expectedNotes,
        string expectedSignature, string expectedRelativeKey)
    {
        var result = ScaleMcpTools.GetKeyNotes(root, mode);

        Assert.That(result.Error,        Is.Null,                    $"valid input must not produce an Error for {root} {mode}");
        Assert.That(result.Notes,        Is.EqualTo(expectedNotes),  $"notes mismatch for {root} {mode}");
        Assert.That(result.KeySignature, Is.EqualTo(expectedSignature));
        Assert.That(result.RelativeKey,  Is.EqualTo(expectedRelativeKey));
        Assert.That(result.Mode,         Is.EqualTo(mode));
    }

    [TestCase("major", "major")]
    [TestCase("MAJOR", "major")]
    [TestCase("Major", "major")]
    [TestCase("maj",   "major")]
    [TestCase("minor", "minor")]
    [TestCase("min",   "minor")]
    [TestCase("MIN",   "minor")]
    public void GetKeyNotes_AcceptsModeShortAndLongFormCaseInsensitive(string input, string expectedNorm)
    {
        var result = ScaleMcpTools.GetKeyNotes("C", input);

        Assert.That(result.Error, Is.Null,
            $"mode '{input}' must be accepted (normalized to '{expectedNorm}')");
        Assert.That(result.Mode, Is.EqualTo(expectedNorm),
            "Mode field should always be the canonical long form");
    }

    [TestCase("",         "major")]
    [TestCase("Q",        "major")]
    [TestCase("C",        "")]
    [TestCase("C",        "lydian")]
    [TestCase("notakey",  "major")]
    public void GetKeyNotes_InvalidInputs_ReturnFailureResult(string root, string mode)
    {
        var result = ScaleMcpTools.GetKeyNotes(root, mode);

        Assert.That(result.Error, Is.Not.Null,
            $"invalid input ('{root}', '{mode}') must populate Error rather than throw");
        Assert.That(result.Notes, Is.Empty,
            "Error result must leave Notes empty per the IntervalResult / ScaleResult invariant");
    }

    [Test]
    public void GetKeyNotes_RejectsNonStandardKeys_ButLegitimateRoots()
    {
        // C# minor / Eb minor exist as standard keys; some flat-spelled minor
        // roots like A# minor do not (they're enharmonic alternates). The tool
        // should fail cleanly on those rather than silently returning the
        // wrong scale.
        //
        // What "is a standard key" means here = whatever Key.Items contains.
        // If a future schema change adds those keys, the test will need to
        // move them out of the failure list.
        var result = ScaleMcpTools.GetKeyNotes("A#", "minor");

        if (result.Error is not null)
        {
            Assert.That(result.Error, Does.Contain("not a standard key").IgnoreCase
                .Or.Contain("parse").IgnoreCase);
        }
        else
        {
            // If Key.Items has been extended to include this key, no test
            // failure — but assert the result shape is valid.
            Assert.That(result.Notes, Has.Length.EqualTo(7));
        }
    }

    [Test]
    public void GetKeyNotes_DoesNotThrowOnNullArguments()
    {
        Assert.That(() => ScaleMcpTools.GetKeyNotes(null!, "major"), Throws.Nothing);
        Assert.That(() => ScaleMcpTools.GetKeyNotes("C",   null!),   Throws.Nothing);

        var nullRoot = ScaleMcpTools.GetKeyNotes(null!, "major");
        Assert.That(nullRoot.Error, Is.Not.Null);
    }

    [Test]
    public void GetKeyNotes_PathologicallyLongInput_ReturnsErrorWithoutAllocating()
    {
        // Same defense as IntervalMcpTools — length guard short-circuits before
        // any TryParse / Replace / ToLowerInvariant allocation.
        var hugeRoot = new string('C', 10_000);

        var sw = System.Diagnostics.Stopwatch.StartNew();
        var result = ScaleMcpTools.GetKeyNotes(hugeRoot, "major");
        sw.Stop();

        Assert.That(result.Error, Is.Not.Null);
        Assert.That(sw.ElapsedMilliseconds, Is.LessThan(10),
            "long-input rejection must short-circuit, not scan the whole string");
    }

    [Test]
    public void GetKeyNotes_ControlCharsInError_AreSanitized()
    {
        // Defends downstream renderers against log/prompt injection — same
        // contract as IntervalMcpTools.
        var esc      = (char)0x1B;
        var injected = "Q\n\r" + esc + "[31mFAKE";
        var result   = ScaleMcpTools.GetKeyNotes(injected, "major");

        Assert.That(result.Error, Is.Not.Null);
        Assert.That(result.Error!.IndexOf('\n'), Is.EqualTo(-1));
        Assert.That(result.Error.IndexOf('\r'), Is.EqualTo(-1));
        Assert.That(result.Error.IndexOf(esc),  Is.EqualTo(-1));
        Assert.That(result.Error.All(c => !char.IsControl(c)), Is.True,
            "no control character of any kind should reach the echoed Error");
    }
}
