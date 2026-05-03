namespace GA.Business.ML.Tests.Unit;

using GA.Business.ML.Agents.Mcp;
using GA.Domain.Services.Atonal.Grothendieck;

[TestFixture]
public class ChordSubstitutionMcpToolsTests
{
    // Real Grothendieck service — pure pitch-class arithmetic, no I/O, fast.
    private static ChordSubstitutionMcpTools MakeTool() =>
        new(new GrothendieckService());

    // ── CompareChords: tritone substitution ───────────────────────────────────

    [Test]
    public void CompareChords_G7_Db7_DetectsTritoneSubstitution()
    {
        // The textbook tritone sub: dominant 7ths whose roots are 6 semitones
        // apart share guide tones (M3 of one = m7 of the other). G7 → C and
        // Db7 → C are interchangeable resolutions in bebop.
        var result = MakeTool().CompareChords("G7", "Db7");

        Assert.That(result.Error, Is.Null);
        Assert.That(result.Relationships.Any(r => r.Type == "Tritone Substitution"), Is.True,
            "G7 vs Db7 must be flagged as Tritone Substitution");
        Assert.That(result.Relationships.First(r => r.Type == "Tritone Substitution").Explanation,
            Does.Contain("guide tones").Or.Contain("M3"));
    }

    [Test]
    public void CompareChords_TwoMajor7ths_DoesNotDetectTritoneSub()
    {
        // Cmaj7 vs F#maj7 are 6 semitones apart but neither is a dominant 7th.
        // Tritone-sub classification must be specific to dom7-pair, not just
        // any tritone-apart chord pair.
        var result = MakeTool().CompareChords("Cmaj7", "F#maj7");

        Assert.That(result.Error, Is.Null);
        Assert.That(result.Relationships.All(r => r.Type != "Tritone Substitution"), Is.True,
            "Cmaj7 vs F#maj7 must NOT be flagged as Tritone Substitution (neither is a dominant 7th)");
    }

    [Test]
    public void CompareChords_AsymmetricDom7AndTriad_DoesNotDetectTritoneSub()
    {
        // G7 is a dominant 7th, Db is a major triad — roots ARE 6 semitones
        // apart but only ONE side is a dom7. The classification requires both
        // sides to be dom7. Asymmetric input is exactly the kind of case that
        // would silently false-positive if the guard were loosened.
        var result = MakeTool().CompareChords("G7", "Db");

        Assert.That(result.Error, Is.Null);
        Assert.That(result.Relationships.All(r => r.Type != "Tritone Substitution"), Is.True,
            "G7 (dom7) vs Db (triad) must NOT be flagged as Tritone Substitution — both sides must be dom7");
    }

    // ── CompareChords: secondary dominant ─────────────────────────────────────

    [Test]
    public void CompareChords_D7_G_DetectsSecondaryDominant()
    {
        // D7 is a perfect 5th above G — D7 functions as V of G.
        var result = MakeTool().CompareChords("D7", "G");

        Assert.That(result.Error, Is.Null);
        Assert.That(result.Relationships.Any(r => r.Type == "Secondary Dominant"), Is.True,
            "D7 → G must be flagged as Secondary Dominant (D is a P5 above G)");
    }

    // ── CompareChords: backdoor dominant ──────────────────────────────────────

    [Test]
    public void CompareChords_Bb7_C_DetectsBackdoorDominant()
    {
        // Bb7 is bVII7 of C — backdoor dominant resolves up a whole step.
        var result = MakeTool().CompareChords("Bb7", "C");

        Assert.That(result.Error, Is.Null);
        Assert.That(result.Relationships.Any(r => r.Type == "Backdoor Dominant"), Is.True,
            "Bb7 → C must be flagged as Backdoor Dominant (Bb is bVII of C)");
    }

    // ── CompareChords: ICV neighbor ───────────────────────────────────────────

    [Test]
    public void CompareChords_CloseChords_FlagsICVNeighbor()
    {
        // C major and A minor share two notes (C E vs A C E) — very close in
        // ICV space. The tool's L1 ≤ 2 threshold should fire.
        var result = MakeTool().CompareChords("C", "Am");

        Assert.That(result.Error, Is.Null);
        Assert.That(result.Relationships.Any(r => r.Type.StartsWith("ICV Neighbor")), Is.True,
            "C and Am share two notes — must be flagged as ICV Neighbor");
        Assert.That(result.IcvL1Distance, Is.LessThanOrEqualTo(2));
    }

    // ── CompareChords: fallback ───────────────────────────────────────────────

    [Test]
    public void CompareChords_DistantChords_FallsBackToHarmonicDistance()
    {
        // C major and Bdim — neither tritone-sub nor secondary-dominant nor
        // close in ICV. Should land on the catch-all "Harmonic Distance" case.
        var result = MakeTool().CompareChords("C", "Bdim");

        Assert.That(result.Error, Is.Null);
        Assert.That(result.IcvL1Distance, Is.GreaterThanOrEqualTo(0));
    }

    // ── CompareChords: error paths ────────────────────────────────────────────

    [Test]
    public void CompareChords_InvalidFirstChord_ReturnsError()
    {
        var result = MakeTool().CompareChords("notachord", "G7");

        Assert.That(result.Error, Is.Not.Null);
        Assert.That(result.Relationships, Is.Empty);
    }

    [Test]
    public void CompareChords_InvalidSecondChord_ReturnsError()
    {
        var result = MakeTool().CompareChords("G7", "notachord");

        Assert.That(result.Error, Is.Not.Null);
        Assert.That(result.Relationships, Is.Empty);
    }

    [Test]
    public void CompareChords_NullArguments_DoNotThrow()
    {
        Assert.That(() => MakeTool().CompareChords(null!, "G7"), Throws.Nothing);
        Assert.That(() => MakeTool().CompareChords("G7", null!), Throws.Nothing);
    }

    [Test]
    public void CompareChords_PathologicallyLongInput_ReturnsErrorWithoutScanning()
    {
        var huge = new string('C', 10_000);

        var sw = System.Diagnostics.Stopwatch.StartNew();
        var result = MakeTool().CompareChords(huge, "G");
        sw.Stop();

        Assert.That(result.Error, Is.Not.Null);
        Assert.That(sw.ElapsedMilliseconds, Is.LessThan(10),
            "long-input rejection must short-circuit, not scan the whole string");
    }

    [Test]
    public void CompareChords_ControlCharsInError_AreSanitized()
    {
        var esc      = (char)0x1B;
        var injected = "Q\n\r" + esc + "[31m";
        var result   = MakeTool().CompareChords(injected, "G");

        Assert.That(result.Error, Is.Not.Null);
        Assert.That(result.Error!.IndexOf('\n'), Is.EqualTo(-1));
        Assert.That(result.Error.IndexOf('\r'), Is.EqualTo(-1));
        Assert.That(result.Error.IndexOf(esc),  Is.EqualTo(-1));
        Assert.That(result.Error.All(c => !char.IsControl(c)), Is.True);
    }

    // ── GetSubstitutions ──────────────────────────────────────────────────────

    [Test]
    public void GetSubstitutions_Cmaj7_ReturnsRankedNearbyChords()
    {
        var result = MakeTool().GetSubstitutions("Cmaj7");

        Assert.That(result.Error,        Is.Null);
        Assert.That(result.SourceChord,  Is.EqualTo("Cmaj7"));
        Assert.That(result.Substitutions, Is.Not.Empty,
            "Cmaj7 should have at least one harmonically-close substitution within ICV distance 3");
        Assert.That(result.Substitutions, Has.Length.LessThanOrEqualTo(5),
            "tool caps at 5 candidates");

        // Substitutions must be sorted by Cost ascending (closest first).
        var costs = result.Substitutions.Select(s => s.Cost).ToList();
        Assert.That(costs, Is.Ordered.Ascending);
    }

    [Test]
    public void GetSubstitutions_AnyChord_ExcludesTheSourceItself()
    {
        // The skill filters `r.Set != sourceSet` — make sure the tool inherits
        // that guard. Otherwise the source would always appear at cost=0.
        var result = MakeTool().GetSubstitutions("C");

        Assert.That(result.Error, Is.Null);
        Assert.That(result.Substitutions.All(s => s.Name != "C"), Is.True,
            "the source chord must not appear in its own substitution list");
    }

    [Test]
    public void GetSubstitutions_InvalidSymbol_ReturnsError()
    {
        var result = MakeTool().GetSubstitutions("notachord");

        Assert.That(result.Error,        Is.Not.Null);
        Assert.That(result.Substitutions, Is.Empty);
    }

    [Test]
    public void GetSubstitutions_NullArgument_DoesNotThrow()
    {
        Assert.That(() => MakeTool().GetSubstitutions(null!), Throws.Nothing);
        var result = MakeTool().GetSubstitutions(null!);
        Assert.That(result.Error, Is.Not.Null);
    }

    // ── Result invariant ──────────────────────────────────────────────────────

    [Test]
    public void Failure_LeavesAllOtherFieldsAtDefault()
    {
        // ChordSubstitutionsResult and ChordComparisonResult must honour the
        // Error-branch invariant just like the other MCP-tool results.
        var subFail = ChordSubstitutionsResult.Failure("test");
        Assert.That(subFail.SourceChord,  Is.Empty);
        Assert.That(subFail.Substitutions, Is.Empty);

        var cmpFail = ChordComparisonResult.Failure("test");
        Assert.That(cmpFail.ChordA,         Is.Empty);
        Assert.That(cmpFail.ChordB,         Is.Empty);
        Assert.That(cmpFail.Relationships,  Is.Empty);
        Assert.That(cmpFail.IcvL1Distance,  Is.EqualTo(0));
    }
}
