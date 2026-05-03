namespace GA.Business.ML.Tests.Unit;

using GA.Business.ML.Agents.Mcp;

[TestFixture]
public class KeyIdentificationMcpToolsTests
{
    private static KeyIdentificationMcpTools MakeTool() => new();

    // ── Common-key progressions ───────────────────────────────────────────────

    [Test]
    public void IdentifyKey_CAmFG_BareList_FindsCMajorAndAMinorTied()
    {
        // I vi IV V in C major. C major and A minor share the same diatonic
        // set (relative pair), so they tie at the top score. This is exactly
        // the kind of ambiguity the LLM-phrasing layer in the SKILL.md is
        // meant to disambiguate.
        var result = MakeTool().IdentifyKey("C Am F G");

        Assert.That(result.Error, Is.Null);
        Assert.That(result.RecognizedChords, Is.EqualTo(new[] { "C", "Am", "F", "G" }));
        Assert.That(result.TotalChords, Is.EqualTo(4));

        var topKeys = result.TopCandidates.Select(c => c.Key).ToArray();
        Assert.That(topKeys, Has.Member("C major"),
            "C Am F G must include C major in the top tied set");
        Assert.That(topKeys, Has.Member("A minor"),
            "C Am F G must also include A minor (relative pair tied at the top score)");

        // All top candidates should report 4/4 diatonic.
        Assert.That(result.TopCandidates.All(c => c.MatchCount == 4 && c.TotalChords == 4), Is.True);
    }

    [Test]
    public void IdentifyKey_DmGC_FindsCMajorOrAMinor()
    {
        // ii V I in C major (or iv VII III in A minor). All three diatonic
        // in both relative-pair keys.
        var result = MakeTool().IdentifyKey("Dm G C");

        Assert.That(result.Error, Is.Null);
        Assert.That(result.TopCandidates.Select(c => c.Key), Has.Some.Contain("C major"));
    }

    [Test]
    public void IdentifyKey_DiatonicSetSurfacedForLlmExplanation()
    {
        // The LLM phrases its answer using the DiatonicSet — verify the tool
        // exposes a 7-element ordered list (I, ii, iii, IV, V, vi, vii°).
        var result = MakeTool().IdentifyKey("C F G");

        Assert.That(result.Error, Is.Null);
        Assert.That(result.TopCandidates, Is.Not.Empty);

        var top = result.TopCandidates[0];
        Assert.That(top.DiatonicSet, Has.Length.EqualTo(7),
            "DiatonicSet must always have exactly 7 chords (I…vii°)");
    }

    // ── Query parsing variants ────────────────────────────────────────────────

    [Test]
    public void IdentifyKey_AcceptsFullQuestionWithSurroundingProse()
    {
        // The LLM may pass the user's whole question rather than just the
        // chord list — the tool must extract chords correctly either way.
        var result = MakeTool().IdentifyKey("what key is C Am F G in?");

        Assert.That(result.Error, Is.Null);
        Assert.That(result.RecognizedChords, Is.EqualTo(new[] { "C", "Am", "F", "G" }),
            "extraction must drop surrounding prose and return only chord symbols");
    }

    [Test]
    public void IdentifyKey_AcceptsCommaSeparatedChords()
    {
        var result = MakeTool().IdentifyKey("Tell me the key of: G, D, Em, C");

        Assert.That(result.Error, Is.Null);
        Assert.That(result.RecognizedChords.Length, Is.EqualTo(4));
    }

    // ── Partial match ranking ─────────────────────────────────────────────────

    [Test]
    public void IdentifyKey_PartialMatchesCappedAt3()
    {
        // Whatever progression we pass, PartialMatches must never exceed
        // MaxPartialCandidates = 3. The tool's responsibility, not the LLM's.
        var result = MakeTool().IdentifyKey("C Am F G");

        Assert.That(result.Error, Is.Null);
        Assert.That(result.PartialMatches, Has.Length.LessThanOrEqualTo(3));
    }

    // ── Error paths ───────────────────────────────────────────────────────────

    [Test]
    public void IdentifyKey_NoChords_ReturnsErrorAboutSymbols()
    {
        var result = MakeTool().IdentifyKey("what key is this in?");

        Assert.That(result.Error, Is.Not.Null);
        Assert.That(result.Error, Does.Contain("chord").IgnoreCase
            .Or.Contain("symbol").IgnoreCase);
        Assert.That(result.RecognizedChords, Is.Empty);
        Assert.That(result.TopCandidates,    Is.Empty);
    }

    [Test]
    public void IdentifyKey_EmptyOrNullInput_ReturnsError()
    {
        Assert.That(MakeTool().IdentifyKey("").Error,    Is.Not.Null);
        Assert.That(MakeTool().IdentifyKey(null!).Error, Is.Not.Null);
    }

    [Test]
    public void IdentifyKey_PathologicallyLongInput_ReturnsErrorWithoutScanning()
    {
        var huge = new string('C', 10_000);

        var sw = System.Diagnostics.Stopwatch.StartNew();
        var result = MakeTool().IdentifyKey(huge);
        sw.Stop();

        Assert.That(result.Error, Is.Not.Null);
        Assert.That(sw.ElapsedMilliseconds, Is.LessThan(10),
            "long-input rejection must short-circuit before extraction");
    }

    [Test]
    public void IdentifyKey_ControlCharsInError_AreSanitized()
    {
        var esc      = (char)0x1B;
        var injected = "Q\n\r" + esc + "[31m";
        var result   = MakeTool().IdentifyKey(injected);

        // Either the input is too short to extract chords (Error about chord
        // symbols) or it triggers the long-input length guard. Both paths use
        // McpEchoSanitizer, so either way Error must be control-char free.
        if (result.Error is not null)
        {
            Assert.That(result.Error.IndexOf('\n'), Is.EqualTo(-1));
            Assert.That(result.Error.IndexOf('\r'), Is.EqualTo(-1));
            Assert.That(result.Error.IndexOf(esc),  Is.EqualTo(-1));
            Assert.That(result.Error.All(c => !char.IsControl(c)), Is.True);
        }
    }

    // ── Result invariant ──────────────────────────────────────────────────────

    [Test]
    public void Failure_LeavesAllOtherFieldsAtDefault()
    {
        var fail = KeyIdentificationResult.Failure("test");

        Assert.That(fail.RecognizedChords, Is.Empty);
        Assert.That(fail.TopCandidates,    Is.Empty);
        Assert.That(fail.PartialMatches,   Is.Empty);
        Assert.That(fail.TotalChords,      Is.EqualTo(0));
    }
}
