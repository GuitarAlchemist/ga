namespace GA.Business.ML.Tests.Unit;

using GA.Business.ML.Agents;
using GA.Business.ML.Agents.Mcp;
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
    [TestCase("CΔ7", new[] { 0, 4, 7, 11 })]
    [TestCase("CΔ", new[] { 0, 4, 7, 11 })]
    [TestCase("CM6", new[] { 0, 4, 7, 9 })]
    [TestCase("CM9", new[] { 0, 4, 7, 11, 2 })]
    [TestCase("CM11", new[] { 0, 4, 7, 11, 2, 5 })]
    [TestCase("CM13", new[] { 0, 4, 7, 11, 2, 5, 9 })]
    [TestCase("CM7b5", new[] { 0, 4, 6, 11 })]
    [TestCase("CmM7", new[] { 0, 3, 7, 11 })]
    [TestCase("Cm6", new[] { 0, 3, 7, 9 })]
    [TestCase("Cm9", new[] { 0, 3, 7, 10, 2 })]
    [TestCase("Cm11", new[] { 0, 3, 7, 10, 2, 5 })]
    [TestCase("Cm13", new[] { 0, 3, 7, 10, 2, 5, 9 })]
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

    [TestCase("C", new[] { 0, 4, 7 })]
    [TestCase("CM", new[] { 0, 4, 7 })]
    [TestCase("CM7", new[] { 0, 4, 7, 11 })]
    [TestCase("Cmaj", new[] { 0, 4, 7 })]
    [TestCase("Cm9", new[] { 0, 3, 7, 10, 2 })]
    [TestCase("C13", new[] { 0, 4, 7, 10, 2, 5, 9 })]
    [TestCase("CΔ7", new[] { 0, 4, 7, 11 })]
    [TestCase("CΔ", new[] { 0, 4, 7, 11 })]
    public void TryGetPitchClassesStrict_ReadsKnownQualities(string symbol, int[] expected)
    {
        var ok = ChordVocabulary.TryGetPitchClassesStrict(symbol, out var pitchClasses);

        Assert.Multiple(() =>
        {
            Assert.That(ok, Is.True);
            Assert.That(pitchClasses, Is.EqualTo(expected));
        });
    }

    [TestCase("Cxyz")]
    [TestCase("Cadd")]
    [TestCase("Cdom")]
    [TestCase("Co")]
    [TestCase("C/E")]
    [TestCase("H7")]
    public void TryGetPitchClassesStrict_RefusesUnknownQualityOrRoot(string symbol)
    {
        var ok = ChordVocabulary.TryGetPitchClassesStrict(symbol, out var pitchClasses);

        Assert.Multiple(() =>
        {
            Assert.That(ok, Is.False);
            Assert.That(pitchClasses, Is.Empty);
        });
    }

    // Read through ChordPitchClasses' compositional parser, these qualities outside the alias table now resolve;
    // before, the strict reader refused them and the lenient one read a C major triad.
    [TestCase("C-", new[] { 0, 3, 7 })]
    [TestCase("C+7", new[] { 0, 4, 8, 10 })]
    [TestCase("Caug7", new[] { 0, 4, 8, 10 })]
    [TestCase("Co7", new[] { 0, 3, 6, 9 })]
    [TestCase("C△", new[] { 0, 4, 7 })]
    [TestCase("C△7", new[] { 0, 4, 7, 11 })]
    [TestCase("C69", new[] { 0, 4, 7, 9, 2 })]
    [TestCase("C6/9", new[] { 0, 4, 7, 9, 2 })]
    [TestCase("C7sus", new[] { 0, 5, 7, 10 })]
    [TestCase("C7sus4", new[] { 0, 5, 7, 10 })]
    [TestCase("C9sus4", new[] { 0, 5, 7, 10, 2 })]
    [TestCase("Csus4b9", new[] { 0, 5, 7, 1 })]
    [TestCase("Cadd11", new[] { 0, 4, 7, 5 })]
    [TestCase("Cadd#11", new[] { 0, 4, 7, 6 })]
    [TestCase("Caddb9", new[] { 0, 4, 7, 1 })]
    [TestCase("Cmadd9", new[] { 0, 3, 7, 2 })]
    [TestCase("C7b13", new[] { 0, 4, 7, 10, 8 })]
    [TestCase("C7#11", new[] { 0, 4, 7, 10, 6 })]
    [TestCase("C7(b9)", new[] { 0, 4, 7, 10, 1 })]
    [TestCase("C7(#9)", new[] { 0, 4, 7, 10, 3 })]
    [TestCase("C9#11", new[] { 0, 4, 7, 10, 2, 6 })]
    [TestCase("C13b9", new[] { 0, 4, 7, 10, 1, 5, 9 })]
    [TestCase("C13#9", new[] { 0, 4, 7, 10, 3, 5, 9 })]
    [TestCase("C13no11", new[] { 0, 4, 7, 10, 2, 9 })]
    [TestCase("C9no3", new[] { 0, 7, 10, 2 })]
    [TestCase("Cmaj7#5", new[] { 0, 4, 8, 11 })]
    [TestCase("Cmaj7b5", new[] { 0, 4, 6, 11 })]
    [TestCase("Cmaj7#11", new[] { 0, 4, 7, 11, 6 })]
    [TestCase("Cmaj9#11", new[] { 0, 4, 7, 11, 2, 6 })]
    [TestCase("Cmaj7no5", new[] { 0, 4, 11 })]
    [TestCase("Cmmaj7", new[] { 0, 3, 7, 11 })]
    [TestCase("CmMaj7", new[] { 0, 3, 7, 11 })]
    [TestCase("Cm(maj7)", new[] { 0, 3, 7, 11 })]
    [TestCase("Cm7(b5)", new[] { 0, 3, 6, 10 })]
    [TestCase("Cm7#5", new[] { 0, 3, 8, 10 })]
    [TestCase("Cm11b5", new[] { 0, 3, 6, 10, 2, 5 })]
    [TestCase("C()", new[] { 0, 4, 7 })]
    public void TryGetPitchClasses_ReadsQualitiesBeyondTheAliasTable(string symbol, int[] expected)
    {
        Assert.Multiple(() =>
        {
            Assert.That(ChordVocabulary.TryGetPitchClassesStrict(symbol, out var strict), Is.True);
            Assert.That(strict, Is.EqualTo(expected));
            Assert.That(ChordVocabulary.TryGetPitchClasses(symbol, out var lenient), Is.True);
            Assert.That(lenient, Is.EqualTo(expected));
        });
    }

    // Every canonical quality reads through the parser with exactly the pitch classes, in the same order, as its
    // GetFormula entry: the aliases that resolved through the formula table read exactly as before.
    [TestCaseSource(nameof(CanonicalQualities))]
    public void ParserSuffix_MatchesFormula(string quality)
    {
        var formula = ChordVocabulary.GetFormula(quality);

        Assert.Multiple(() =>
        {
            Assert.That(formula.Quality, Is.EqualTo(quality), "every canonical quality has its own formula");
            Assert.That(ChordVocabulary.TryGetPitchClassesStrict("C" + ChordVocabulary.ParserSuffixes[quality], out var pcs), Is.True);
            Assert.That(pcs, Is.EqualTo(formula.Intervals.Select(i => i % 12).Distinct()));
        });
    }

    private static IEnumerable<string> CanonicalQualities() => ChordVocabulary.ParserSuffixes.Keys;

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

    [Test]
    public async Task VoiceLeadingSkill_ReadsCM7AsMajorSeventh()
    {
        var skill = new VoiceLeadingSkill(NullLogger<VoiceLeadingSkill>.Instance);

        var upper = await skill.ExecuteAsync("voice leading from CM7 to F");
        var spelled = await skill.ExecuteAsync("voice leading from Cmaj7 to F");

        Assert.That(upper.Result.Replace("CM7", "Cmaj7"), Is.EqualTo(spelled.Result));
    }

    [Test]
    public async Task VoiceLeadingSkill_ReadsCMAsMajorTriad()
    {
        var skill = new VoiceLeadingSkill(NullLogger<VoiceLeadingSkill>.Instance);

        var upper = await skill.ExecuteAsync("voice leading from CM to F");
        var plain = await skill.ExecuteAsync("voice leading from C to F");

        Assert.That(upper.Result.Replace("CM →", "C →"), Is.EqualTo(plain.Result));
    }

    [Test]
    public async Task VoiceLeadingSkill_RefusesUnknownQuality()
    {
        // An unreadable quality stays a refusal: the skill must not voice-lead "Cadd" as a C major triad.
        var skill = new VoiceLeadingSkill(NullLogger<VoiceLeadingSkill>.Instance);

        var response = await skill.ExecuteAsync("voice leading from Cadd to F");

        Assert.Multiple(() =>
        {
            Assert.That(response.Result, Does.Contain("couldn't parse 'Cadd'"));
            Assert.That(response.Confidence, Is.LessThan(1.0f));
        });
    }

    [Test]
    public async Task ChordSubstitutionSkill_ReadsCM7AsMajorSeventh()
    {
        var skill = new ChordSubstitutionSkill(Grothendieck, NullLogger<ChordSubstitutionSkill>.Instance);

        var upper = await skill.ExecuteAsync("What can I substitute for CM7?");
        var spelled = await skill.ExecuteAsync("What can I substitute for Cmaj7?");

        Assert.Multiple(() =>
        {
            Assert.That(spelled.Result, Does.StartWith("Harmonic substitutions for **Cmaj7**"));
            Assert.That(upper.Result, Is.EqualTo(spelled.Result));
        });
    }

    [Test]
    public void ChordSubstitutionMcpTools_ReadsCM7AsMajorSeventh()
    {
        var tool = new ChordSubstitutionMcpTools(Grothendieck);

        var upper = tool.GetSubstitutions("CM7");
        var spelled = tool.GetSubstitutions("Cmaj7");

        Assert.Multiple(() =>
        {
            Assert.That(upper.Error, Is.Null);
            Assert.That(upper.SourceChord, Is.EqualTo(spelled.SourceChord));
            Assert.That(upper.Substitutions.Select(s => s.Name), Is.EqualTo(spelled.Substitutions.Select(s => s.Name)));
        });
    }

    [Test]
    public void ChordSubstitutionMcpTools_ReadsCMAsMajorTriad()
    {
        var tool = new ChordSubstitutionMcpTools(Grothendieck);

        var upper = tool.CompareChords("GM", "CM");
        var plain = tool.CompareChords("G", "C");

        Assert.Multiple(() =>
        {
            Assert.That(upper.Error, Is.Null);
            Assert.That((upper.ChordA, upper.ChordB), Is.EqualTo(("G", "C")));
            Assert.That(upper.Relationships.Select(r => r.Type), Is.EqualTo(plain.Relationships.Select(r => r.Type)));
        });
    }

    [Test]
    public void ChordSubstitutionMcpTools_RefusesUnknownQuality() =>
        Assert.That(new ChordSubstitutionMcpTools(Grothendieck).GetSubstitutions("C7sus4").Error, Is.Not.Null);
}
