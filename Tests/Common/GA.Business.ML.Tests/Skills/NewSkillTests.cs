namespace GA.Business.ML.Tests.Skills;

using GA.Business.ML.Agents;
using GA.Business.ML.Agents.Skills;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

/// <summary>
/// Tests for ModeExplorationSkill, ProgressionSuggestionSkill, and HarmonicAnalysisSkill.
/// </summary>
[TestFixture]
public class NewSkillTests
{
    private Mock<IChatClient> _chatClientMock = null!;

    [SetUp]
    public void Setup()
    {
        _chatClientMock = new Mock<IChatClient>();
        // Default: return a valid JSON response for LLM-backed skills
        _chatClientMock
            .Setup(c => c.GetResponseAsync(
                It.IsAny<IEnumerable<ChatMessage>>(),
                It.IsAny<ChatOptions?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ChatResponse(
                [new ChatMessage(ChatRole.Assistant,
                    """{"result":"Test response.","confidence":0.85,"evidence":["test"],"assumptions":[],"data":null}""")]));
    }

    // ── ModeExplorationSkill ─────────────────────────────────────────────────

    [TestCase("What are the modes of the major scale?")]
    [TestCase("list all modes")]
    [TestCase("show me all modes of the major scale")]
    public void ModeExploration_ShouldHandle_AllModesQuery(string message)
    {
        var skill = new ModeExplorationSkill(NullLogger<ModeExplorationSkill>.Instance);
        Assert.That(skill.CanHandle(message), Is.True);
    }

    [TestCase("What is Dorian?")]
    [TestCase("Explain Phrygian mode")]
    [TestCase("Tell me about Mixolydian")]
    [TestCase("Describe Lydian")]
    public void ModeExploration_ShouldHandle_ModeNameQuery(string message)
    {
        var skill = new ModeExplorationSkill(NullLogger<ModeExplorationSkill>.Instance);
        Assert.That(skill.CanHandle(message), Is.True);
    }

    [TestCase("D Dorian")]
    [TestCase("E Phrygian")]
    [TestCase("F Lydian")]
    [TestCase("G Mixolydian")]
    [TestCase("A Aeolian")]
    [TestCase("B Locrian")]
    public void ModeExploration_ShouldHandle_SpecificRootAndMode(string message)
    {
        var skill = new ModeExplorationSkill(NullLogger<ModeExplorationSkill>.Instance);
        Assert.That(skill.CanHandle(message), Is.True);
    }

    [TestCase("What notes are in C major?")]
    [TestCase("How do I play a barre chord?")]
    [TestCase("Give me a practice routine")]
    public void ModeExploration_ShouldNotHandle_UnrelatedQueries(string message)
    {
        var skill = new ModeExplorationSkill(NullLogger<ModeExplorationSkill>.Instance);
        Assert.That(skill.CanHandle(message), Is.False);
    }

    [Test]
    public async Task ModeExploration_AllModes_ShouldListSevenModes()
    {
        var skill = new ModeExplorationSkill(NullLogger<ModeExplorationSkill>.Instance);
        var response = await skill.ExecuteAsync("What are the modes of the major scale?");

        Assert.That(response.Confidence, Is.EqualTo(1.0f));
        Assert.That(response.Result, Contains.Substring("Ionian"));
        Assert.That(response.Result, Contains.Substring("Dorian"));
        Assert.That(response.Result, Contains.Substring("Phrygian"));
        Assert.That(response.Result, Contains.Substring("Lydian"));
        Assert.That(response.Result, Contains.Substring("Mixolydian"));
        Assert.That(response.Result, Contains.Substring("Aeolian"));
        Assert.That(response.Result, Contains.Substring("Locrian"));
    }

    [Test]
    public async Task ModeExploration_AllModes_ShouldShowCMajorNotes()
    {
        var skill = new ModeExplorationSkill(NullLogger<ModeExplorationSkill>.Instance);
        var response = await skill.ExecuteAsync("list all modes");

        // C major notes should appear (C Ionian = C D E F G A B)
        Assert.That(response.Result, Contains.Substring("C D E F G A B"));
    }

    [Test]
    public async Task ModeExploration_Dorian_ShouldExplainCharacter()
    {
        var skill = new ModeExplorationSkill(NullLogger<ModeExplorationSkill>.Instance);
        var response = await skill.ExecuteAsync("What is Dorian?");

        Assert.That(response.Confidence, Is.EqualTo(1.0f));
        Assert.That(response.Result, Contains.Substring("Dorian"));
        // D Dorian from C major reference: D E F G A B C
        Assert.That(response.Result, Contains.Substring("D E F G A B C"));
    }

    [Test]
    public async Task ModeExploration_DDorianSpecific_ShouldShowCorrectNotes()
    {
        var skill = new ModeExplorationSkill(NullLogger<ModeExplorationSkill>.Instance);
        var response = await skill.ExecuteAsync("D Dorian");

        Assert.That(response.Confidence, Is.EqualTo(1.0f));
        // D Dorian parent key = C major → notes D E F G A B C
        Assert.That(response.Result, Contains.Substring("D E F G A B C"));
        Assert.That(response.Result, Contains.Substring("C major"));
    }

    [Test]
    public async Task ModeExploration_EMixolydianSpecific_ShouldShowCorrectParentKey()
    {
        var skill = new ModeExplorationSkill(NullLogger<ModeExplorationSkill>.Instance);
        var response = await skill.ExecuteAsync("E Mixolydian");

        Assert.That(response.Confidence, Is.EqualTo(1.0f));
        // E Mixolydian = degree 5 of A major → parent key A major
        Assert.That(response.Result, Contains.Substring("A major"));
    }

    [Test]
    public async Task ModeExploration_SpecificMode_ShouldHaveFullConfidence()
    {
        var skill = new ModeExplorationSkill(NullLogger<ModeExplorationSkill>.Instance);
        var response = await skill.ExecuteAsync("G Mixolydian");

        Assert.That(response.AgentId, Is.EqualTo(AgentIds.Theory));
        Assert.That(response.Confidence, Is.EqualTo(1.0f));
        Assert.That(response.Evidence, Is.Not.Empty);
    }

    // ── ProgressionSuggestionSkill ───────────────────────────────────────────

    [TestCase("suggest progressions in G major")]
    [TestCase("give me chord progressions for blues in E")]
    [TestCase("show me some jazz progressions in F")]
    [TestCase("what are some common pop progressions in C major")]
    [TestCase("create a progression in D minor")]
    public void ProgressionSuggestion_ShouldHandle(string message)
    {
        var skill = new ProgressionSuggestionSkill(
            _chatClientMock.Object, NullLogger<ProgressionSuggestionSkill>.Instance);
        Assert.That(skill.CanHandle(message), Is.True);
    }

    [TestCase("what key is Am F C G in?")]
    [TestCase("What notes are in G major?")]
    [TestCase("practice routine")]
    [TestCase("interval quiz")]
    public void ProgressionSuggestion_ShouldNotHandle_UnrelatedQueries(string message)
    {
        var skill = new ProgressionSuggestionSkill(
            _chatClientMock.Object, NullLogger<ProgressionSuggestionSkill>.Instance);
        Assert.That(skill.CanHandle(message), Is.False);
    }

    [Test]
    public async Task ProgressionSuggestion_ShouldCallLlmAndReturnResponse()
    {
        var skill = new ProgressionSuggestionSkill(
            _chatClientMock.Object, NullLogger<ProgressionSuggestionSkill>.Instance);
        var response = await skill.ExecuteAsync("suggest progressions in G major");

        _chatClientMock.Verify(
            c => c.GetResponseAsync(
                It.IsAny<IEnumerable<ChatMessage>>(),
                It.IsAny<ChatOptions?>(),
                It.IsAny<CancellationToken>()),
            Times.Once);

        Assert.That(response, Is.Not.Null);
        Assert.That(response.Result, Is.Not.Empty);
    }

    [Test]
    public async Task ProgressionSuggestion_WithStyle_ShouldIncludeStyleInPrompt()
    {
        string? capturedPrompt = null;
        _chatClientMock
            .Setup(c => c.GetResponseAsync(
                It.IsAny<IEnumerable<ChatMessage>>(),
                It.IsAny<ChatOptions?>(),
                It.IsAny<CancellationToken>()))
            .Callback<IEnumerable<ChatMessage>, ChatOptions?, CancellationToken>(
                (messages, _, _) => capturedPrompt = messages.FirstOrDefault()?.Text)
            .ReturnsAsync(new ChatResponse(
                [new ChatMessage(ChatRole.Assistant,
                    """{"result":"Blues progressions.","confidence":0.85,"evidence":[],"assumptions":[],"data":null}""")]));

        var skill = new ProgressionSuggestionSkill(
            _chatClientMock.Object, NullLogger<ProgressionSuggestionSkill>.Instance);
        await skill.ExecuteAsync("give me blues progressions in E major");

        Assert.That(capturedPrompt, Is.Not.Null);
        Assert.That(capturedPrompt, Does.Contain("blues").IgnoreCase);
    }

    // ── HarmonicAnalysisSkill ────────────────────────────────────────────────

    [TestCase("analyze the progression Am F C G")]
    [TestCase("harmonic analysis of Dm G C Am")]
    [TestCase("what is the function of each chord in Am F C G")]
    [TestCase("roman numeral analysis of G D Em C")]
    [TestCase("break down the progression Dm Am F C")]
    public void HarmonicAnalysis_ShouldHandle(string message)
    {
        var skill = new HarmonicAnalysisSkill(
            _chatClientMock.Object, NullLogger<HarmonicAnalysisSkill>.Instance);
        Assert.That(skill.CanHandle(message), Is.True);
    }

    [TestCase("what key is Am F C G in?")]
    [TestCase("What notes are in C major?")]
    [TestCase("Give me a practice routine")]
    [TestCase("analyze the music")]  // no chord symbols — should not handle
    public void HarmonicAnalysis_ShouldNotHandle_UnrelatedQueries(string message)
    {
        var skill = new HarmonicAnalysisSkill(
            _chatClientMock.Object, NullLogger<HarmonicAnalysisSkill>.Instance);
        Assert.That(skill.CanHandle(message), Is.False);
    }

    [Test]
    public async Task HarmonicAnalysis_ShouldDetectKeyAndCallLlm()
    {
        var skill = new HarmonicAnalysisSkill(
            _chatClientMock.Object, NullLogger<HarmonicAnalysisSkill>.Instance);
        var response = await skill.ExecuteAsync("analyze the progression Am F C G");

        _chatClientMock.Verify(
            c => c.GetResponseAsync(
                It.IsAny<IEnumerable<ChatMessage>>(),
                It.IsAny<ChatOptions?>(),
                It.IsAny<CancellationToken>()),
            Times.Once);

        Assert.That(response, Is.Not.Null);
        // Evidence should contain detected key info
        Assert.That(response.Evidence.Any(e => e.Contains("Chords analysed")), Is.True);
    }

    [Test]
    public async Task HarmonicAnalysis_ShouldReturnNoChords_WhenNoneFound()
    {
        var skill = new HarmonicAnalysisSkill(
            _chatClientMock.Object, NullLogger<HarmonicAnalysisSkill>.Instance);
        var response = await skill.ExecuteAsync("analyze the music theory concept");

        // CanHandle guards this from being called without chords,
        // but ExecuteAsync should still be safe if called directly with no chords
        Assert.That(response.Confidence, Is.LessThan(1.0f));
    }

    // ── Non-shadowing: new skills don't grab existing skill queries ──────────

    [TestCase("What notes are in C major?")]
    [TestCase("interval quiz")]
    [TestCase("chord quiz")]
    [TestCase("practice routine")]
    [TestCase("my progress")]
    public void NewSkills_ShouldNotShadow_ExistingSkillBehavior(string message)
    {
        var modeSkill       = new ModeExplorationSkill(NullLogger<ModeExplorationSkill>.Instance);
        var suggestionSkill = new ProgressionSuggestionSkill(
            _chatClientMock.Object, NullLogger<ProgressionSuggestionSkill>.Instance);
        var analysisSkill   = new HarmonicAnalysisSkill(
            _chatClientMock.Object, NullLogger<HarmonicAnalysisSkill>.Instance);

        Assert.That(modeSkill.CanHandle(message), Is.False,
            $"ModeExplorationSkill should not handle: {message}");
        Assert.That(suggestionSkill.CanHandle(message), Is.False,
            $"ProgressionSuggestionSkill should not handle: {message}");
        Assert.That(analysisSkill.CanHandle(message), Is.False,
            $"HarmonicAnalysisSkill should not handle: {message}");
    }
}
