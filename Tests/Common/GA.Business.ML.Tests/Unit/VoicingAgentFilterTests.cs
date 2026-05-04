namespace GA.Business.ML.Tests.Unit;

using GA.Business.ML.Agents;
using GA.Business.ML.Search;

/// <summary>
/// Contract tests for <see cref="VoicingAgent.BuildSearchFilters"/>. The
/// agent extracts filters from a structured query and forwards them to
/// the voicing search instead of passing <c>null</c>; pre-fix code was
/// over-fetching from the entire voicing space and relying on score
/// ordering alone for relevance.
/// </summary>
[TestFixture]
public class VoicingAgentFilterTests
{
    [Test]
    public void BuildSearchFilters_AllFieldsNull_ReturnsNullForCheapPath()
    {
        // No filter signals → return null. Important: passing an empty
        // VoicingSearchFilters (rather than null) might still narrow
        // the search behind the scenes; the explicit null preserves the
        // original "no filter" semantics for downstream callers.
        var query = new StructuredQuery(
            ChordSymbol: null,
            RootPitchClass: null,
            PitchClasses: null,
            ModeName: null,
            Tags: null);

        Assert.That(VoicingAgent.BuildSearchFilters(query), Is.Null);
    }

    [Test]
    public void BuildSearchFilters_ChordSymbolOnly_ReturnsFiltersWithChord()
    {
        var query = new StructuredQuery(
            ChordSymbol: "Cmaj7",
            RootPitchClass: null,
            PitchClasses: null,
            ModeName: null,
            Tags: null);

        var filters = VoicingAgent.BuildSearchFilters(query);

        Assert.That(filters,           Is.Not.Null);
        Assert.That(filters!.ChordName, Is.EqualTo("Cmaj7"));
        Assert.That(filters.ModeName,   Is.Null);
        Assert.That(filters.VoicingType, Is.Null);
        Assert.That(filters.Tags,       Is.Null);
    }

    [Test]
    public void BuildSearchFilters_InstrumentMapsToVoicingType()
    {
        var query = new StructuredQuery(
            ChordSymbol: null,
            RootPitchClass: null,
            PitchClasses: null,
            ModeName: null,
            Tags: null) { Instrument = "bass" };

        var filters = VoicingAgent.BuildSearchFilters(query);

        Assert.That(filters,             Is.Not.Null);
        Assert.That(filters!.VoicingType, Is.EqualTo("bass"),
            "the Instrument property maps to VoicingType on the filter so the search filters by voicing-type column");
    }

    [Test]
    public void BuildSearchFilters_AllFieldsSet_PopulatesAllFilterFields()
    {
        var query = new StructuredQuery(
            ChordSymbol: "Cmaj7",
            RootPitchClass: 0,
            PitchClasses: [0, 4, 7, 11],
            ModeName: "Lydian",
            Tags: ["jazz", "smooth"]) { Instrument = "guitar" };

        var filters = VoicingAgent.BuildSearchFilters(query);

        Assert.That(filters,               Is.Not.Null);
        Assert.That(filters!.ChordName,     Is.EqualTo("Cmaj7"));
        Assert.That(filters.ModeName,       Is.EqualTo("Lydian"));
        Assert.That(filters.VoicingType,    Is.EqualTo("guitar"));
        Assert.That(filters.Tags,           Is.EquivalentTo(new[] { "jazz", "smooth" }));
    }

    [Test]
    public void BuildSearchFilters_EmptyTagsList_TreatedAsNull()
    {
        // Empty tags should not trigger filter creation — that signal
        // means "the user did not specify tags", not "the user specified
        // an empty tag set". Pinned to prevent a future refactor from
        // flipping the semantics.
        var query = new StructuredQuery(
            ChordSymbol: null,
            RootPitchClass: null,
            PitchClasses: null,
            ModeName: null,
            Tags: []);

        Assert.That(VoicingAgent.BuildSearchFilters(query), Is.Null,
            "empty tag list is the same as no tags — no filter created");
    }
}
