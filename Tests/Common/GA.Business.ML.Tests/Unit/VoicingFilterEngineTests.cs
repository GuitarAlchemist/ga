namespace GA.Business.ML.Tests.Unit;

using GA.Business.ML.Search;
using GA.Domain.Services.Fretboard.Voicings.Core;

/// <summary>
///     Pins the contract of <see cref="VoicingFilterEngine"/> — the shared metadata-filter seam both
///     search strategies cross (architecture deepening #2). These cases are exactly the points where the
///     GPU strategy's old private copy had <b>drifted</b>: null-safety, case-insensitivity, <c>Tags</c>
///     ALL-match (not ANY), <c>VoicingType</c> <c>Contains</c> (not <c>==</c>), and the metadata filters
///     the GPU path silently skipped. When the GPU adapter is routed through this engine, these are the
///     behaviours it must adopt.
/// </summary>
[TestFixture]
public class VoicingFilterEngineTests
{
    // A baseline voicing; tests vary just the field under test via `with`.
    private static VoicingEmbedding Voicing() => new(
        Id: "v1",
        ChordName: "Cmaj7",
        VoicingType: "drop2 closed",
        Position: "open",
        Difficulty: "easy",
        ModeName: null,
        ModalFamily: null,
        PossibleKeys: [],
        SemanticTags: ["jazz", "shell"],
        PrimeFormId: "4-27",
        TranslationOffset: 0,
        Diagram: "x35453",
        MidiNotes: [48, 52, 55, 59],
        PitchClassSet: "{0,4,7,11}",
        IntervalClassVector: "<101220>",
        MinFret: 3,
        MaxFret: 5,
        BarreRequired: false,
        HandStretch: 2,
        StackingType: "tertian",
        RootPitchClass: 0,
        MidiBassNote: 48,
        HarmonicFunction: "tonic",
        IsNaturallyOccurring: true,
        ConsonanceScore: 0.8,
        BrightnessScore: 0.5,
        IsRootless: false,
        HasGuideTones: true,
        Inversion: 0,
        TopPitchClass: 11,
        TexturalDescription: "warm",
        DoubledTones: [],
        AlternateNames: [],
        OmittedTones: [],
        CagedShape: "C",
        Description: "test voicing",
        Embedding: new double[216],
        TextEmbedding: null);

    private static VoicingSearchFilters Empty() => new();

    [Test]
    public void EmptyFilters_MatchEverything()
    {
        Assert.That(VoicingFilterEngine.Matches(Voicing(), Empty()), Is.True);
    }

    [Test]
    public void NullAttribute_DoesNotThrow_AndDoesNotMatch()
    {
        // The 2026-05-30 fix: a voicing with a null VoicingType must not throw when a VoicingType filter
        // is set; it simply fails the filter. (The GPU path's `v.VoicingType == filters.VoicingType` would
        // not throw but would also wrongly let a null through on an exact-null compare.)
        var voicing = Voicing() with { VoicingType = null };
        var filters = Empty() with { VoicingType = "drop2" };

        Assert.That(() => VoicingFilterEngine.Matches(voicing, filters), Throws.Nothing);
        Assert.That(VoicingFilterEngine.Matches(voicing, filters), Is.False);
    }

    [Test]
    public void VoicingType_Uses_Contains_Not_Exact_Equality()
    {
        // CPU semantics: substring, case-insensitive. The GPU copy used exact `==`, so "drop2" would not
        // have matched "drop2 closed".
        var filters = Empty() with { VoicingType = "drop2" };
        Assert.That(VoicingFilterEngine.Matches(Voicing(), filters), Is.True);
    }

    [Test]
    public void StringFilters_Are_CaseInsensitive()
    {
        var filters = Empty() with { Difficulty = "EASY", Position = "OPEN" };
        Assert.That(VoicingFilterEngine.Matches(Voicing(), filters), Is.True);
    }

    [Test]
    public void Tags_Require_ALL_To_Match_Not_ANY()
    {
        var voicing = Voicing(); // SemanticTags = ["jazz","shell"]

        // ALL present → match.
        Assert.That(VoicingFilterEngine.Matches(voicing, Empty() with { Tags = ["jazz", "shell"] }), Is.True);
        // One missing → no match (the GPU copy used ANY and would have matched this).
        Assert.That(VoicingFilterEngine.Matches(voicing, Empty() with { Tags = ["jazz", "bossa"] }), Is.False);
    }

    [Test]
    public void Metadata_Filters_The_Gpu_Skipped_Are_Applied()
    {
        var voicing = Voicing(); // PrimeFormId "4-27", TopPitchClass 11

        // SetClassId (substring on PrimeFormId) — GPU never applied this.
        Assert.That(VoicingFilterEngine.Matches(voicing, Empty() with { SetClassId = "4-27" }), Is.True);
        Assert.That(VoicingFilterEngine.Matches(voicing, Empty() with { SetClassId = "3-11" }), Is.False);

        // TopPitchClass (melody-note filter) — GPU never applied this.
        Assert.That(VoicingFilterEngine.Matches(voicing, Empty() with { TopPitchClass = 11 }), Is.True);
        Assert.That(VoicingFilterEngine.Matches(voicing, Empty() with { TopPitchClass = 0 }), Is.False);
    }

    [Test]
    public void FretRange_Filters_Boundaries()
    {
        var voicing = Voicing(); // MinFret 3, MaxFret 5
        Assert.That(VoicingFilterEngine.Matches(voicing, Empty() with { MinFret = 3, MaxFret = 5 }), Is.True);
        Assert.That(VoicingFilterEngine.Matches(voicing, Empty() with { MaxFret = 4 }), Is.False, "voicing reaches fret 5");
    }

    // ── Filters migrated from the GPU adapter (the GPU-reconciliation slice). These were GPU-only and
    //    now live in the engine so both strategies apply them identically. ──

    [Test]
    public void ModeName_Matches_CaseInsensitively_And_NullSafe()
    {
        var voicing = Voicing() with { ModeName = "Dorian" };
        Assert.That(VoicingFilterEngine.Matches(voicing, Empty() with { ModeName = "dorian" }), Is.True);
        Assert.That(VoicingFilterEngine.Matches(voicing, Empty() with { ModeName = "Lydian" }), Is.False);
        // null ModeName on the voicing must not throw and must not match.
        var nullMode = Voicing() with { ModeName = null };
        Assert.That(() => VoicingFilterEngine.Matches(nullMode, Empty() with { ModeName = "Dorian" }), Throws.Nothing);
        Assert.That(VoicingFilterEngine.Matches(nullMode, Empty() with { ModeName = "Dorian" }), Is.False);
    }

    [Test]
    public void MaxFingerStretch_Filters_On_HandStretch()
    {
        var voicing = Voicing(); // HandStretch = 2
        Assert.That(VoicingFilterEngine.Matches(voicing, Empty() with { MaxFingerStretch = 3 }), Is.True);
        Assert.That(VoicingFilterEngine.Matches(voicing, Empty() with { MaxFingerStretch = 1 }), Is.False, "stretch 2 exceeds max 1");
    }

    [Test]
    public void IsOpenVoicing_Derives_From_VoicingType()
    {
        Assert.That(VoicingFilterEngine.Matches(Voicing(), Empty() with { IsOpenVoicing = false }), Is.True, "baseline is 'drop2 closed' — not open");
        Assert.That(VoicingFilterEngine.Matches(Voicing(), Empty() with { IsOpenVoicing = true }), Is.False);

        var open = Voicing() with { VoicingType = "open triad" };
        Assert.That(VoicingFilterEngine.Matches(open, Empty() with { IsOpenVoicing = true }), Is.True);
    }

    [Test]
    public void DropVoicing_Matches_VoicingType_Substring()
    {
        Assert.That(VoicingFilterEngine.Matches(Voicing(), Empty() with { DropVoicing = "drop2" }), Is.True, "baseline VoicingType is 'drop2 closed'");
        Assert.That(VoicingFilterEngine.Matches(Voicing(), Empty() with { DropVoicing = "drop3" }), Is.False);
    }

    [Test]
    public void CagedShape_Matches_SemanticTags()
    {
        Assert.That(VoicingFilterEngine.Matches(Voicing(), Empty() with { CagedShape = "C" }), Is.False, "baseline has no CAGED tag");

        var caged = Voicing() with { SemanticTags = ["CAGED-C", "jazz"] };
        Assert.That(VoicingFilterEngine.Matches(caged, Empty() with { CagedShape = "C" }), Is.True);

        var cagedAlt = Voicing() with { SemanticTags = ["A shape", "blues"] };
        Assert.That(VoicingFilterEngine.Matches(cagedAlt, Empty() with { CagedShape = "A" }), Is.True);
    }
}
