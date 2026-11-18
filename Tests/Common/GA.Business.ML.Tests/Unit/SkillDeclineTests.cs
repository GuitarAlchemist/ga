namespace GA.Business.ML.Tests.Unit;

using GA.Business.ML.Agents.Skills;
using GA.Domain.Services.Atonal.Grothendieck;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

/// <summary>
/// Pins <see cref="GA.Business.ML.Agents.AgentResponse.Declined"/>: a skill reached by a
/// message without its input shape declines so the orchestrator can answer elsewhere, while a
/// recognized but unresolvable request keeps its deterministic error.
/// </summary>
/// <remarks>The declining prompts are follow-ups that the live 2026-09-13 router sent to these skills.</remarks>
[TestFixture]
public sealed class SkillDeclineTests
{
    [Test]
    public async Task Capo_MessageWithoutCapoPhrase_Declines()
    {
        var response = await new CapoSkill(NullLogger<CapoSkill>.Instance)
            .ExecuteAsync("Describe his playing style in two sentences.");

        Assert.That(response.Declined, Is.True);
    }

    [Test]
    public async Task Capo_RecognizedButOutOfRangeFret_DoesNotDecline()
    {
        var response = await new CapoSkill(NullLogger<CapoSkill>.Instance)
            .ExecuteAsync("I play a C shape with capo 25, what does it sound like?");

        Assert.That(response.Declined, Is.False, response.Result);
    }

    [Test]
    public async Task RememberThis_MessageWithoutRememberPhrase_Declines()
    {
        var response = await new RememberThisSkill(NullLogger<RememberThisSkill>.Instance)
            .ExecuteAsync("What is my name, and who is my favorite guitarist?");

        Assert.That(response.Declined, Is.True);
    }

    [Test]
    public async Task KeyIdentification_MessageWithoutChords_Declines()
    {
        var response = await new KeyIdentificationSkill(Mock.Of<IChatClient>(), NullLogger<KeyIdentificationSkill>.Instance)
            .ExecuteAsync("Which key did I say I am composing in? Answer in one sentence.");

        Assert.That(response.Declined, Is.True);
    }

    // A skill whose required argument is absent declines instead of answering with its parse
    // error, so the turn continues to the LLM agent path. The first two prompts were answered
    // with the parse error on the live chat (2026-11-16).
    [Test]
    public async Task ChordSubstitution_MessageWithoutChordSymbol_Declines()
    {
        var response = await new ChordSubstitutionSkill(new GrothendieckService(), NullLogger<ChordSubstitutionSkill>.Instance)
            .ExecuteAsync("Explain modal interchange in two sentences.");

        Assert.That(response.Declined, Is.True, response.Result);
    }

    [Test]
    public async Task ChordInfo_MessageWithoutChordName_Declines()
    {
        // "Which common chords contain both E and G#?" used to decline here; issue #888 answers it
        // with the chords holding both notes, so this prompt names no chord and no note.
        var response = await new ChordInfoSkill(NullLogger<ChordInfoSkill>.Instance)
            .ExecuteAsync("Which common chords sound the saddest?");

        Assert.That(response.Declined, Is.True, response.Result);
    }

    [Test]
    public async Task FretSpan_MessageWithoutChordDiagram_Declines()
    {
        var response = await new FretSpanSkill(NullLogger<FretSpanSkill>.Instance)
            .ExecuteAsync("How hard is a barre chord to play for a beginner?");

        Assert.That(response.Declined, Is.True, response.Result);
    }

    [Test]
    public async Task FretSpan_DiagramWithoutPressedFret_DoesNotDecline()
    {
        var response = await new FretSpanSkill(NullLogger<FretSpanSkill>.Instance)
            .ExecuteAsync("What is the fret span of x00000?");

        Assert.That(response.Declined, Is.False, response.Result);
    }

    [Test]
    public async Task Interval_MessageWithoutTwoNotes_Declines()
    {
        var response = await new IntervalSkill(NullLogger<IntervalSkill>.Instance)
            .ExecuteAsync("Which interval sounds the most dissonant?");

        Assert.That(response.Declined, Is.True, response.Result);
    }

    [Test]
    public async Task Interval_PairWithNoStandardName_DoesNotDecline()
    {
        var response = await new IntervalSkill(NullLogger<IntervalSkill>.Instance)
            .ExecuteAsync("interval from B# to Fb");

        Assert.That(response.Declined, Is.False, response.Result);
    }

    [Test]
    public async Task ScaleInfo_MessageWithoutKeyName_Declines()
    {
        var response = await new ScaleInfoSkill(NullLogger<ScaleInfoSkill>.Instance)
            .ExecuteAsync("Which notes should I practice first on guitar?");

        Assert.That(response.Declined, Is.True, response.Result);
    }

    [Test]
    public async Task VoiceLeading_MessageWithoutTwoChords_Declines()
    {
        var response = await new VoiceLeadingSkill(NullLogger<VoiceLeadingSkill>.Instance)
            .ExecuteAsync("What does good voice leading mean when comping chords on guitar?");

        Assert.That(response.Declined, Is.True, response.Result);
    }

    [Test]
    public async Task VoiceLeading_TwoChords_DoesNotDecline()
    {
        var response = await new VoiceLeadingSkill(NullLogger<VoiceLeadingSkill>.Instance)
            .ExecuteAsync("voice leading from C to F");

        Assert.That(response.Declined, Is.False, response.Result);
    }

    // The article "a" is not a chord root: "a sus4 chord" names no chord, so the skill declines
    // instead of answering "A sus4 chord contains A, D, and E."
    [Test]
    public async Task ChordInfo_ArticleBeforeQuality_Declines()
    {
        var response = await new ChordInfoSkill(NullLogger<ChordInfoSkill>.Instance)
            .ExecuteAsync("What is a sus4 chord and how does it usually resolve?");

        Assert.That(response.Declined, Is.True, response.Result);
    }

    [TestCase("What notes are in a C major triad", "C, E, and G")]
    [TestCase("What is an A sus4 chord?", "A, D, and E")]
    public async Task ChordInfo_RootAfterArticle_Answers(string question, string notes)
    {
        var response = await new ChordInfoSkill(NullLogger<ChordInfoSkill>.Instance).ExecuteAsync(question);

        Assert.Multiple(() =>
        {
            Assert.That(response.Declined, Is.False, response.Result);
            Assert.That(response.Result, Does.Contain(notes));
        });
    }
}
