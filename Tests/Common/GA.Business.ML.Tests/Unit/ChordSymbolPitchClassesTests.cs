namespace GA.Business.ML.Tests.Unit;

using GA.Business.ML.Agents;
using GA.Business.ML.Agents.Skills;
using GA.Domain.Services.Atonal.Grothendieck;
using Microsoft.Extensions.Logging.Abstractions;

/// <summary>
///     Chord symbol → pitch classes, through the one shared reader (<see cref="ChordVocabulary.TryGetPitchClasses"/>)
///     and through each atonal skill that used to carry its own copy of the quality table.
/// </summary>
/// <remarks>
///     The four copies lowercased the quality before matching <c>"M7"</c>, so <c>CM7</c> read as C minor 7 and
///     <c>CM</c> as C minor. The skill cases pin that each skill now answers <c>CM7</c> exactly as it answers
///     <c>Cmaj7</c>.
/// </remarks>
[TestFixture]
public class ChordSymbolPitchClassesTests
{
    [TestCase("C", new[] { 0, 4, 7 })]
    [TestCase("CM", new[] { 0, 4, 7 })]
    [TestCase("Cm", new[] { 0, 3, 7 })]
    [TestCase("CM7", new[] { 0, 4, 7, 11 })]
    [TestCase("Cmaj7", new[] { 0, 4, 7, 11 })]
    [TestCase("Cm7", new[] { 0, 3, 7, 10 })]
    [TestCase("C7", new[] { 0, 4, 7, 10 })]
    [TestCase("Bm7b5", new[] { 11, 2, 5, 9 })]
    [TestCase("F#°7", new[] { 6, 9, 0, 3 })]
    [TestCase("C°", new[] { 0, 3, 6 })]
    [TestCase("Ebdim", new[] { 3, 6, 9 })]
    [TestCase("B♭9", new[] { 10, 2, 5, 8, 0 })]
    [TestCase("gsus4", new[] { 7, 0, 2 })]
    public void TryGetPitchClasses_ReadsRootAndQuality(string symbol, int[] expected)
    {
        var ok = ChordVocabulary.TryGetPitchClasses(symbol, out var pitchClasses);

        Assert.Multiple(() =>
        {
            Assert.That(ok, Is.True);
            Assert.That(pitchClasses, Is.EqualTo(expected));
        });
    }

    [TestCase("")]
    [TestCase("H7")]
    [TestCase("7")]
    public void TryGetPitchClasses_RejectsUnknownRoot(string symbol) =>
        Assert.That(ChordVocabulary.TryGetPitchClasses(symbol, out _), Is.False);

    private static readonly IGrothendieckService Grothendieck = new GrothendieckService();

    [Test]
    public async Task IntervalClassVectorSkill_ReadsCM7AsMajorSeventh()
    {
        var skill = new IntervalClassVectorSkill(Grothendieck, NullLogger<IntervalClassVectorSkill>.Instance);

        var upper = await skill.ExecuteAsync("what is the ICV of CM7");
        var spelled = await skill.ExecuteAsync("what is the ICV of Cmaj7");

        Assert.That(upper.Result.Replace("CM7", "Cmaj7"), Is.EqualTo(spelled.Result));
    }

    [Test]
    public async Task GrothendieckDeltaSkill_ReadsCM7AsMajorSeventh()
    {
        // Pins the chord reading only (both ICVs). The delta itself reporting L1 = 1 for equal
        // vectors is a separate defect in the delta computation (issues #776 / #802).
        var skill = new GrothendieckDeltaSkill(Grothendieck, NullLogger<GrothendieckDeltaSkill>.Instance);

        var response = await skill.ExecuteAsync("Grothendieck delta from CM7 to Cmaj7");

        Assert.Multiple(() =>
        {
            Assert.That(response.Result, Does.Contain("**ICV CM7**: `<1 0 1 2 2 0>`"));
            Assert.That(response.Result, Does.Contain("**ICV Cmaj7**: `<1 0 1 2 2 0>`"));
        });
    }

    [Test]
    public async Task IcvNeighborsSkill_ReadsCM7AsMajorSeventh()
    {
        var skill = new IcvNeighborsSkill(Grothendieck, NullLogger<IcvNeighborsSkill>.Instance);

        var upper = await skill.ExecuteAsync("find ICV neighbors of CM7");
        var spelled = await skill.ExecuteAsync("find ICV neighbors of Cmaj7");

        Assert.That(upper.Result.Replace("CM7", "Cmaj7"), Is.EqualTo(spelled.Result));
    }

    [Test]
    public async Task IcvShortestPathSkill_ReadsCM7AsMajorSeventh()
    {
        var skill = new IcvShortestPathSkill(Grothendieck, NullLogger<IcvShortestPathSkill>.Instance);

        var upper = await skill.ExecuteAsync("shortest harmonic path from CM7 to Dm7");
        var spelled = await skill.ExecuteAsync("shortest harmonic path from Cmaj7 to Dm7");

        Assert.That(upper.Result.Replace("CM7", "Cmaj7"), Is.EqualTo(spelled.Result));
    }
}
