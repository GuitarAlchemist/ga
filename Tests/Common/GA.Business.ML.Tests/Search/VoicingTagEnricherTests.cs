namespace GA.Business.ML.Tests.Search;

using GA.Business.Core.Analysis.Voicings;
using GA.Domain.Services.Fretboard.Voicings.Analysis;

/// <summary>
///     Verifies the derived-tag classification that bumps SYMBOLIC-partition density.
///     Closes the corpus-side gap where style-tagged queries scored lower than bare
///     queries because top voicings had no style bits set.
/// </summary>
[TestFixture]
[Category("Unit")]
public class VoicingTagEnricherTests
{
    // ── Register classification ────────────────────────────────────────────

    [TestCase(new[] { 40, 43, 45 }, "register:low")]         // low E string area
    [TestCase(new[] { 52, 55, 59 }, "register:mid-low")]     // E3–B3
    [TestCase(new[] { 60, 64, 67 }, "register:mid")]         // C4–G4
    [TestCase(new[] { 67, 71, 74 }, "register:mid-high")]    // G4–D5
    [TestCase(new[] { 79, 81, 84 }, "register:high")]        // above D5
    public void Register_ClassifiesByMidiMean(int[] midi, string expected)
    {
        var c = MakeCharacteristics(quality: "maj", consonance: 0.7, dissonance: 0.2);
        var tags = VoicingTagEnricher.Enrich(c, midi).ToList();
        Assert.That(tags, Does.Contain(expected));
    }

    // ── Mood: tense overrides everything when dissonant ────────────────────

    [Test]
    public void Tense_DominatesWhenDissonanceHigh()
    {
        var c = MakeCharacteristics(quality: "m7b5", dissonance: 0.80, consonance: 0.30);
        var tags = VoicingTagEnricher.Enrich(c, [60, 63, 66, 69]).ToList();
        Assert.That(tags, Does.Contain("tense"));
        // When tense fires, no other mood tags should be emitted
        Assert.That(tags, Does.Not.Contain("bright"));
        Assert.That(tags, Does.Not.Contain("dreamy"));
        Assert.That(tags, Does.Not.Contain("melancholy"));
    }

    [Test]
    public void Dreamy_OnExtendedChordWithDropVoicing_MidRegister()
    {
        var c = MakeCharacteristics(
            quality: "maj9", consonance: 0.65, dissonance: 0.30,
            isOpenVoicing: false, dropVoicing: "Drop-2");
        var tags = VoicingTagEnricher.Enrich(c, [60, 64, 67, 71, 74]).ToList();
        Assert.That(tags, Does.Contain("dreamy"));
    }

    [Test]
    public void Bright_OnMajorChordInMidHighRegisterWithConsonance()
    {
        var c = MakeCharacteristics(quality: "maj", consonance: 0.75);
        var tags = VoicingTagEnricher.Enrich(c, [67, 72, 76]).ToList();
        Assert.That(tags, Does.Contain("bright"));
    }

    [Test]
    public void Melancholy_Sad_OnMinorInLowRegister()
    {
        var c = MakeCharacteristics(quality: "m", consonance: 0.60);
        var tags = VoicingTagEnricher.Enrich(c, [45, 52, 57]).ToList();
        Assert.That(tags, Does.Contain("melancholy"));
        Assert.That(tags, Does.Contain("sad"));
    }

    // ── Style heuristics ──────────────────────────────────────────────────

    [Test]
    public void Jazz_FiresForRootlessExtendedChord()
    {
        var c = MakeCharacteristics(quality: "m7", consonance: 0.55, isRootless: true);
        var tags = VoicingTagEnricher.Enrich(c, [62, 65, 68, 71]).ToList();
        Assert.That(tags, Does.Contain("jazz"));
        Assert.That(tags, Does.Contain("rootless"));
    }

    [Test]
    public void Jazz_FiresForDrop2OnMaj7()
    {
        var c = MakeCharacteristics(quality: "maj7", consonance: 0.60, dropVoicing: "Drop-2");
        var tags = VoicingTagEnricher.Enrich(c, [60, 64, 67, 71]).ToList();
        Assert.That(tags, Does.Contain("jazz"));
        Assert.That(tags, Does.Contain("drop-2-voicings"));
    }

    [Test]
    public void Jazz_DoesNotFireForPlainMajorTriad()
    {
        // Don't over-tag: a plain C major triad is not a jazz voicing.
        var c = MakeCharacteristics(quality: "maj", consonance: 0.80);
        var tags = VoicingTagEnricher.Enrich(c, [60, 64, 67]).ToList();
        Assert.That(tags, Does.Not.Contain("jazz"));
    }

    [Test]
    public void CampfireChord_FiresForSimpleOpenTriad()
    {
        var c = MakeCharacteristics(quality: "maj", consonance: 0.75, isOpenVoicing: true);
        var tags = VoicingTagEnricher.Enrich(c, [40, 52, 55, 60, 64, 67]).ToList();
        Assert.That(tags, Does.Contain("campfire-chord"));
    }

    // ── Technique tags ─────────────────────────────────────────────────────

    [Test]
    public void OpenClosedVoicing_MutuallyExclusive()
    {
        var open   = VoicingTagEnricher.Enrich(MakeCharacteristics(quality: "maj", isOpenVoicing: true),  [40, 52, 60, 67]).ToList();
        var closed = VoicingTagEnricher.Enrich(MakeCharacteristics(quality: "maj", isOpenVoicing: false), [60, 64, 67]).ToList();

        Assert.Multiple(() =>
        {
            Assert.That(open, Does.Contain("open-voicing"));
            Assert.That(open, Does.Not.Contain("closed-voicing"));
            Assert.That(closed, Does.Contain("closed-voicing"));
            Assert.That(closed, Does.Not.Contain("open-voicing"));
        });
    }

    [Test]
    public void ShellVoicing_RequiresRootlessAnd3Notes()
    {
        var shell = VoicingTagEnricher.Enrich(
            MakeCharacteristics(quality: "m7", isRootless: true, noteCount: 3),
            [62, 65, 68]).ToList();
        Assert.That(shell, Does.Contain("shell-voicing"));

        var notShell = VoicingTagEnricher.Enrich(
            MakeCharacteristics(quality: "m7", isRootless: false, noteCount: 4),
            [60, 63, 67, 70]).ToList();
        Assert.That(notShell, Does.Not.Contain("shell-voicing"));
    }

    // ── Harmless-on-empty ──────────────────────────────────────────────────

    [Test]
    public void EmptyMidi_StillEmitsStyleAndTechnique()
    {
        // Register tag is gated on midi > 0, but other classifiers must still work.
        var c = MakeCharacteristics(quality: "maj7", dropVoicing: "Drop-2");
        var tags = VoicingTagEnricher.Enrich(c, []).ToList();

        Assert.That(tags, Does.Not.Contain("register:mid"));
        Assert.That(tags, Does.Contain("drop-2-voicings"));
    }

    // ── helper ─────────────────────────────────────────────────────────────

    private static VoicingCharacteristics MakeCharacteristics(
        string quality = "maj",
        double consonance = 0.5,
        double dissonance = 0.5,
        bool isRootless = false,
        bool isOpenVoicing = false,
        string? dropVoicing = null,
        int noteCount = 4,
        int intervalSpread = 12)
        => new(
            ChordId: new ChordIdentification(
                ChordName: $"C{quality}",
                RootPitchClass: "C",
                HarmonicFunction: "Unknown",
                IsNaturallyOccurring: true,
                FunctionalDescription: "",
                Quality: quality,
                SlashChordInfo: null,
                ExtensionInfo: null),
            DissonanceScore: dissonance,
            Consonance: consonance,
            IntervalSpread: intervalSpread,
            NoteCount: noteCount,
            IntervalClassVector: "<0,0,0,0,0,0>",
            IsRootless: isRootless,
            DropVoicing: dropVoicing,
            IsOpenVoicing: isOpenVoicing,
            Features: [],
            SemanticTags: []);
}
