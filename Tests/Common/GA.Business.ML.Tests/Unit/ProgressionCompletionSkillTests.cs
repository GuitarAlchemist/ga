namespace GA.Business.ML.Tests.Unit;

using GA.Business.ML.Agents.Skills;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Microsoft.Extensions.AI;

/// <summary>
/// Tests for <see cref="ProgressionCompletionSkill"/>.
/// LLM calls are mocked — only domain routing logic is exercised here.
/// </summary>
[TestFixture]
public sealed class ProgressionCompletionSkillTests
{
    private Mock<IChatClient> _chatMock = null!;
    private ProgressionCompletionSkill _skill = null!;

    [SetUp]
    public void SetUp()
    {
        _chatMock = new Mock<IChatClient>();

        // Default LLM response: valid structured JSON with ga:completion-suggestions
        _chatMock
            .Setup(c => c.GetResponseAsync(
                It.IsAny<IEnumerable<ChatMessage>>(),
                It.IsAny<ChatOptions>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ChatResponse(new ChatMessage(ChatRole.Assistant,
                """
                {
                  "result": "Suggested completions for Am F C:\n  1. E7  - authentic cadence (V7-i)\n  2. G   - half cadence",
                  "confidence": 0.9,
                  "evidence": ["key: A minor"],
                  "assumptions": ["diatonic only"],
                  "data": {
                    "event": "ga:completion-suggestions",
                    "suggestions": [
                      { "chords": ["E7"], "cadence": "authentic", "roman": "V7-i", "explanation": "Strongest resolution back to Am." },
                      { "chords": ["G"],  "cadence": "half",      "roman": "bVII-i", "explanation": "Open loop." }
                    ]
                  }
                }
                """)));

        _skill = new ProgressionCompletionSkill(
            _chatMock.Object,
            NullLogger<ProgressionCompletionSkill>.Instance);
    }

    // ── CanHandle ─────────────────────────────────────────────────────────────

    [TestCase("Help me finish Am F C")]
    [TestCase("Am F C — how do I end this?")]
    [TestCase("I have G D Em, what comes next?")]
    [TestCase("Can you complete this progression: Am F C?")]
    [TestCase("How to end Am F?")]
    [TestCase("Am F C G — what should follow?")]
    [TestCase("I want to continue Am F C")]
    [TestCase("help me extend this: G D Em")]
    public void CanHandle_ReturnsTrueForCompletionMessages(string message) =>
        Assert.That(_skill.CanHandle(message), Is.True);

    [TestCase("What key is Am F C G in?")]
    [TestCase("How do I play Am?")]
    [TestCase("Tell me about jazz harmony")]
    [TestCase("What substitutes for G7?")]
    [TestCase("Am")]  // single chord — no trigger
    public void CanHandle_ReturnsFalseWithoutTriggerOrEnoughChords(string message) =>
        Assert.That(_skill.CanHandle(message), Is.False);

    [TestCase("Help me finish Am")]  // trigger present but only 1 chord
    public void CanHandle_ReturnsFalseWithFewerThanTwoChords(string message) =>
        Assert.That(_skill.CanHandle(message), Is.False);

    // ── No collision with KeyIdentificationSkill ──────────────────────────────

    [Test]
    public void CanHandle_DoesNotFireOnKeyIdentificationQuery()
    {
        // "what key" alone should not trigger ProgressionCompletionSkill
        Assert.That(_skill.CanHandle("Am F C G — what key am I in?"), Is.False);
    }

    // ── ExecuteAsync ──────────────────────────────────────────────────────────

    [Test]
    public async Task ExecuteAsync_ReturnsHighConfidence_ForKnownProgression()
    {
        var response = await _skill.ExecuteAsync("Help me finish Am F C");

        Assert.That(response.Confidence, Is.GreaterThan(0.5f));
        Assert.That(response.Result, Is.Not.Empty);
    }

    [Test]
    public async Task ExecuteAsync_IncludesDetectedKeyInEvidence()
    {
        var response = await _skill.ExecuteAsync("Help me finish Am F C");

        Assert.That(response.Evidence, Has.Some.Contains("A minor").Or.Matches<string>(e => e.Contains("key")),
            "Evidence should mention the detected key");
    }

    [Test]
    public async Task ExecuteAsync_PopulatesAgentResponseData_WithCompletionSuggestionsEvent()
    {
        var response = await _skill.ExecuteAsync("Help me finish Am F C");

        Assert.That(response.Data, Is.Not.Null, "AgentResponse.Data should contain the AG-UI event payload");
    }

    [Test]
    public async Task ExecuteAsync_LowConfidence_WhenNoChordsParsed()
    {
        var response = await _skill.ExecuteAsync("Help me finish blah blah and something");

        // No parseable chord symbols → immediate low-confidence fallback (no LLM call)
        Assert.That(response.Confidence, Is.LessThan(0.5f));
        _chatMock.Verify(
            c => c.GetResponseAsync(
                It.IsAny<IEnumerable<ChatMessage>>(),
                It.IsAny<ChatOptions>(),
                It.IsAny<CancellationToken>()),
            Times.Never,
            "LLM should not be called when no chords are found");
    }

    // ── Key detection feeds correct diatonic set ──────────────────────────────

    [Test]
    public async Task ExecuteAsync_GDEm_DetectsGMajor()
    {
        // G D Em → G major is the best fit
        var response = await _skill.ExecuteAsync("G D Em — what comes next?");

        // The evidence should mention G major (or C major as relative, but G dominates)
        Assert.That(
            response.Evidence.Any(e => e.Contains("G") || e.Contains("major")),
            Is.True,
            "Evidence should mention the detected key");
    }

    [Test]
    public async Task ExecuteAsync_AmFC_DetectsAMinorOrCMajor()
    {
        var response = await _skill.ExecuteAsync("Help me finish Am F C");

        Assert.That(
            response.Evidence.Any(e => e.Contains("A minor") || e.Contains("C major") || e.Contains("Am")),
            Is.True);
    }
}
