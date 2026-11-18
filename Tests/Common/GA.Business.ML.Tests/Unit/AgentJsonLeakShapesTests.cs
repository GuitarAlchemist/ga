namespace GA.Business.ML.Tests.Unit;

using Agents;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

/// <summary>
/// Reported shapes from the production chat (2026-11-18): the composer answered
/// "I'm playing Am F C G over and over." with a raw <c>```json {"result": ...}</c>
/// envelope, and the theory agent answered the follow-up "What key am I in?"
/// (with history) with "Analysis failed.". Whatever shape the model returns, the
/// user sees the answer prose: never a JSON envelope, never an error marker.
/// </summary>
[TestFixture]
public class AgentJsonLeakShapesTests
{
    private const string Answer = "Your progression Am – F – C – G is vi – IV – I – V in C major.";

    private static readonly IReadOnlyList<ChatHistoryTurn> History =
    [
        new("user", "I'm playing Am F C G over and over."),
        new("assistant", Answer),
    ];

    private static Mock<IChatClient> ChatReturning(string modelOutput)
    {
        var chat = new Mock<IChatClient>();
        chat.Setup(c => c.GetResponseAsync(It.IsAny<IEnumerable<ChatMessage>>(), It.IsAny<ChatOptions>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ChatResponse(new ChatMessage(ChatRole.Assistant, modelOutput)));
        return chat;
    }

    private static Task<AgentResponse> ComposerAnswerFor(string modelOutput) =>
        new ComposerAgent(ChatReturning(modelOutput).Object, NullLogger<ComposerAgent>.Instance)
            .ProcessAsync(new AgentRequest { Query = "I'm playing Am F C G over and over." });

    private static Task<AgentResponse> TheoryFollowUpFor(string modelOutput) =>
        new TheoryAgent(ChatReturning(modelOutput).Object, NullLogger<TheoryAgent>.Instance)
            .ProcessAsync(new AgentRequest { Query = "What key am I in?", ConversationHistory = History });

    private static readonly string FencedEnvelope =
        "```json\n{   \"result\": \"" + Answer + "\",\n  \"confidence\": 0.9,\n  \"evidence\": [\"vi-IV-I-V\"],\n  \"assumptions\": []\n}\n```";

    // Haiku often writes multi-line answers with literal line breaks inside the
    // JSON string, which strict JSON rejects.
    private const string EnvelopeWithRawNewlines =
        "```json\n{\n  \"result\": \"The key is C major.\n\nA minor is the relative minor reading.\",\n  \"confidence\": 0.9\n}\n```";

    // A fenced musical example inside "result" closes the outer ```json fence early.
    private const string EnvelopeWithCodeBlockInResult =
        "```json\n{\n  \"result\": \"Try this voicing:\\n```vextab\\nnotes :q 0/5\\n```\\nIt rings open.\",\n  \"confidence\": 0.9\n}\n```";

    public static IEnumerable<TestCaseData> Shapes()
    {
        yield return new TestCaseData(FencedEnvelope, Answer).SetName("{m}(fenced json with result)");
        yield return new TestCaseData(FencedEnvelope + "\n\nWant some reharmonization ideas?", Answer).SetName("{m}(fenced json then prose)");
        yield return new TestCaseData(Answer, Answer).SetName("{m}(prose only)");
        yield return new TestCaseData("```json\n{   \"result\": \"" + Answer + "\",\n  \"confidence\": 0.9,\n  \"evidence\": [\"vi-IV", Answer)
            .SetName("{m}(truncated json)");
        yield return new TestCaseData(EnvelopeWithRawNewlines, "The key is C major.\n\nA minor is the relative minor reading.")
            .SetName("{m}(raw newlines in result)");
        yield return new TestCaseData(EnvelopeWithCodeBlockInResult, "Try this voicing:\n```vextab\nnotes :q 0/5\n```\nIt rings open.")
            .SetName("{m}(code block inside result)");
        yield return new TestCaseData(
                "```json\n{\n  \"result\": \"Try this voicing:\\n```vextab\\nnotes :q 0/5\\n```\\nIt rings open.\",\n  \"evidence\": [\"open str",
                "Try this voicing:\n```vextab\nnotes :q 0/5\n```\nIt rings open.")
            .SetName("{m}(code block inside result, truncated)");
    }

    [TestCaseSource(nameof(Shapes))]
    public async Task Composer_ShowsTheAnswerProse(string modelOutput, string expected)
    {
        var response = await ComposerAnswerFor(modelOutput);

        Assert.That(response.Result, Is.EqualTo(expected));
        Assert.That(response.Result, Does.Not.Contain("\"result\""));
    }

    [TestCaseSource(nameof(Shapes))]
    public async Task TheoryFollowUpWithHistory_ShowsTheAnswerProse(string modelOutput, string expected)
    {
        var response = await TheoryFollowUpFor(modelOutput);

        Assert.That(response.Result, Is.EqualTo(expected));
        Assert.That(response.Result, Does.Not.Contain("\"result\""));
    }
}
