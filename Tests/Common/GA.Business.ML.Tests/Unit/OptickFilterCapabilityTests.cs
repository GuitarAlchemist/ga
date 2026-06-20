namespace GA.Business.ML.Tests.Unit;

using GA.Business.ML.Search;

/// <summary>
///     OPTK's index-bound filter ceiling (ADR-0002). It honors only chord-quality, MIDI range,
///     instrument-as-<c>VoicingType</c>, and the diagram-derived comfort filters; everything else it
///     declares via <see cref="OptickSearchStrategy.ComputeUnsupportedFilters"/> so the gap is observable
///     (telemetry <c>dropped</c>) rather than silently ignored.
/// </summary>
[TestFixture]
public class OptickFilterCapabilityTests
{
    [Test]
    public void EmptyFilters_DropNothing()
    {
        Assert.That(OptickSearchStrategy.ComputeUnsupportedFilters(new VoicingSearchFilters()), Is.Empty);
    }

    [Test]
    public void HonoredFilters_AreNotDropped()
    {
        var f = new VoicingSearchFilters(
            ChordName: "Cmaj7", MinMidiPitch: 40, MaxMidiPitch: 76,
            VoicingType: "guitar",                          // instrument route — honored
            MinComfortScore: 0.5, MustBeErgonomic: true);   // diagram-derived comfort — honored
        Assert.That(OptickSearchStrategy.ComputeUnsupportedFilters(f), Is.Empty);
    }

    [Test]
    public void VoicingType_AsInstrument_IsHonored_ButOtherwiseDropped()
    {
        Assert.Multiple(() =>
        {
            Assert.That(OptickSearchStrategy.ComputeUnsupportedFilters(new VoicingSearchFilters(VoicingType: "ukulele")),
                Is.Empty);
            Assert.That(OptickSearchStrategy.ComputeUnsupportedFilters(new VoicingSearchFilters(VoicingType: "drop2")),
                Does.Contain("VoicingType"));
        });
    }

    [Test]
    public void RichMetadataFilters_AreDropped()
    {
        var f = new VoicingSearchFilters(
            Difficulty: "easy", Tags: ["jazz"], ModeName: "Dorian",
            StackingType: "Quartal", IsRootless: true, SetClassId: "4-27");
        Assert.That(
            OptickSearchStrategy.ComputeUnsupportedFilters(f),
            Is.SupersetOf(new[] { "Difficulty", "Tags", "ModeName", "StackingType", "IsRootless", "SetClassId" }));
    }
}
