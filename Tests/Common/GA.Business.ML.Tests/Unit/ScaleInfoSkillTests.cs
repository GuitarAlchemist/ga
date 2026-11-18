namespace GA.Business.ML.Tests.Unit;

using GA.Business.ML.Agents.Skills;
using GA.Business.ML.Notation;
using Microsoft.Extensions.Logging.Abstractions;

[TestFixture]
public class ScaleInfoSkillTests
{
    private static ScaleInfoSkill MakeSkill() => new(NullLogger<ScaleInfoSkill>.Instance);

    [Test]
    public void ExamplePrompts_IncludeBareWhatIsXMajorPattern()
    {
        // Regression test for the user-reported routing failure:
        // "What is C major?" was falling back to the LLM because no example
        // prompt was structurally close enough to clear the 0.65 cosine
        // threshold. The fix is to include the bare "What is X major/minor?"
        // shape in ExamplePrompts so the SemanticIntentRouter scores it
        // near-1.0 for that phrasing.
        //
        // If this test fails, "What is C major?" / "What is A minor?" /
        // "What is F# major?" queries will likely fall back to the LLM in
        // production. Re-add the patterns before removing them.
        var skill = MakeSkill();

        Assert.That(skill.ExamplePrompts, Has.Some.EqualTo("What is C major?"),
            "bare major-key pattern must be in ExamplePrompts so the semantic router clears the confidence threshold");
        Assert.That(skill.ExamplePrompts, Has.Some.EqualTo("What is A minor?"),
            "bare minor-key pattern must be in ExamplePrompts");
        Assert.That(skill.ExamplePrompts, Has.Some.EqualTo("What is F# major?"),
            "accidental-key pattern must be in ExamplePrompts so accidentals don't drop below the threshold");
    }

    [Test]
    public void ExamplePrompts_AlsoIncludeNotes_ResponsiblePhrasings()
    {
        // The bare "What is X major?" patterns coexist with the more explicit
        // "What notes are in X major?" / "Show me the X scale" phrasings —
        // the regression-fix added new prompts without removing existing ones.
        var skill = MakeSkill();

        Assert.That(skill.ExamplePrompts, Has.Some.Contain("notes are in"));
        Assert.That(skill.ExamplePrompts, Has.Some.Contain("scale"));
    }

    // The 2026-10-03 probe asked "What notes are in the A minor pentatonic scale?" and got the
    // 7-note A minor scale: KeyPattern matched "A minor" and ignored "pentatonic".
    [TestCase("What notes are in the A minor pentatonic scale?", "A – C – D – E – G")]
    [TestCase("Show me the E pentatonic minor scale", "E – G – A – B – D")]
    [TestCase("F# minor pentatonic notes", "F# – A – B – C# – E")]
    [TestCase("notes in the C major pentatonic scale", "C – D – E – G – A")]
    [TestCase("What notes are in the A blues scale?", "A – C – D – Eb – E – G")]
    [TestCase("notes in the C major blues scale", "C – D – Eb – E – G – A")]
    [TestCase("What is the G# harmonic minor scale?", "G# – A# – B – C# – D# – E – Fx")]
    [TestCase("notes in the D melodic minor scale", "D – E – F – G – A – B – C#")]
    public async Task ScaleVariant_SpellsTheVariantFromTheKey(string question, string notes)
    {
        var response = await MakeSkill().ExecuteAsync(question);

        Assert.That(response.Declined, Is.False, response.Result);
        Assert.That(response.Result, Does.Contain($"**{notes}**"));
    }

    [Test]
    public async Task PlainMinorKey_StillListsSevenNotes()
    {
        var response = await MakeSkill().ExecuteAsync("What notes are in the A minor scale?");

        Assert.That(response.Result, Does.Contain("**A – B – C – D – E – F – G**"));
    }

    [TestCase("notes in the C major bebop scale")]
    [TestCase("What notes are in A minor dorian?")]
    [TestCase("What notes are in the A pentatonic scale?")]
    public async Task ScaleItCannotSpell_Declines(string question)
    {
        var response = await MakeSkill().ExecuteAsync(question);

        Assert.That(response.Declined, Is.True, response.Result);
    }

    [TestCase("What notes are in the A minor pentatonic scale?", true)]
    [TestCase("What is the G# harmonic minor scale?", true)]
    [TestCase("notes in the C major bebop scale", false)]
    public void CanHandle_MatchesSpelledVariantsOnly(string question, bool expected) =>
        Assert.That(MakeSkill().CanHandle(question), Is.EqualTo(expected));

    [Test]
    public void Description_DescribesKeyOrScaleLookup()
    {
        var skill = MakeSkill();

        Assert.That(skill.Description, Does.Contain("major").IgnoreCase
            .Or.Contain("minor").IgnoreCase
            .Or.Contain("key").IgnoreCase
            .Or.Contain("scale").IgnoreCase);
    }

    // Issue #888: "as guitar tab" was answered with note names only.
    [TestCase("Show me the C major scale as guitar tab in open position", "notes :8 3/5 0/4 2/4 3/4 0/3 2/3 0/2 1/2")]
    [TestCase("G major scale tab", "notes :8 3/6 0/5 2/5 3/5 0/4 2/4 4/4 0/3 2/3 0/2 1/2 3/2 0/1 2/1 3/1")]
    [TestCase("tab for the E minor pentatonic scale", "notes :8 0/6 3/6 0/5 2/5 0/4 2/4 0/3 2/3 0/2 3/2 0/1")]
    [TestCase("A harmonic minor scale as tab", "notes :8 0/5 2/5 3/5 0/4 2/4 3/4 1/3 2/3")]
    public async Task ScaleAsTab_AddsAnOpenPositionVexTabBlock(string question, string notesLine)
    {
        var response = await MakeSkill().ExecuteAsync(question);

        Assert.Multiple(() =>
        {
            Assert.That(response.Result, Does.Contain(notesLine));
            Assert.That(PlayableNotationFormatter.CountVexTabBlocks(response.Result), Is.EqualTo((1, 1)),
                "one fenced vextab block that GA's VexTab parser reads");
            Assert.That(response.Result, Does.Contain("open position"));
        });
    }

    [Test]
    public async Task ScaleWithoutTabRequest_HasNoTab()
    {
        var response = await MakeSkill().ExecuteAsync("Show me the C major scale");

        Assert.That(PlayableNotationFormatter.CountVexTabBlocks(response.Result).Blocks, Is.Zero);
    }
}
