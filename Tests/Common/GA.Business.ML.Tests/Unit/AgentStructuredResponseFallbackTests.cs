namespace GA.Business.ML.Tests.Unit;

using Agents;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

/// <summary>
/// When an agent's structured JSON cannot be parsed (production 2026-11-17:
/// Haiku 5.5's thinking ate the output budget and the JSON was cut off at
/// <c>$.evidence[0]</c>), the user must get the answer text, never a raw
/// <c>```json {...}</c> blob and never the internal "Analysis failed." marker.
/// </summary>
[TestFixture]
public class AgentStructuredResponseFallbackTests
{
    private static async Task<AgentResponse> TheoryAnswerFor(string modelOutput)
    {
        var chat = new Mock<IChatClient>();
        chat.Setup(c => c.GetResponseAsync(It.IsAny<IEnumerable<ChatMessage>>(), It.IsAny<ChatOptions>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ChatResponse(new ChatMessage(ChatRole.Assistant, modelOutput)));
        var agent = new TheoryAgent(chat.Object, NullLogger<TheoryAgent>.Instance);
        return await agent.ProcessAsync(new AgentRequest { Query = "What is the function of G7 in C major?" });
    }

    private static void AssertNoRawJson(string result) =>
        Assert.Multiple(() =>
        {
            Assert.That(result, Does.Not.Contain("```"));
            Assert.That(result, Does.Not.Contain("{"));
            Assert.That(result, Does.Not.Contain("\"evidence\""));
            Assert.That(result, Is.Not.EqualTo("Analysis failed."));
            Assert.That(result, Is.Not.Empty);
        });

    [Test]
    public async Task FencedValidJson_IsParsed()
    {
        var response = await TheoryAnswerFor("""
            ```json
            {"result":"G7 is the dominant of C major.","confidence":0.9,"evidence":["V7"],"assumptions":[]}
            ```
            """);

        Assert.Multiple(() =>
        {
            Assert.That(response.Result, Is.EqualTo("G7 is the dominant of C major."));
            Assert.That(response.Confidence, Is.EqualTo(0.9f).Within(0.001f));
        });
    }

    [Test]
    public async Task TruncatedJson_WithCompleteResult_SurfacesTheResult()
    {
        // The production failure: cut off inside $.evidence[0].
        var response = await TheoryAnswerFor("""
            ```json
            {
              "result": "G7 is the dominant seventh of C major; it resolves to C.",
              "confidence": 0.92,
              "evidence": ["G-B-D-F contains the tritone B-F that resolv
            """);

        Assert.That(response.Result, Is.EqualTo("G7 is the dominant seventh of C major; it resolves to C."));
        AssertNoRawJson(response.Result);
    }

    [Test]
    public async Task TruncatedJson_WithEscapedQuotesInResult_UnescapesTheResult()
    {
        var response = await TheoryAnswerFor("{\"result\": \"The \\\"dominant\\\" chord is G7.\", \"evidence\": [\"tri");

        Assert.That(response.Result, Is.EqualTo("The \"dominant\" chord is G7."));
    }

    [Test]
    public async Task ProseAroundBrokenJson_SurfacesTheProse()
    {
        var response = await TheoryAnswerFor("""
            G7 is the dominant chord in C major.
            ```json
            {"confidence": 0.9, "evidence": ["tritone B-F
            """);

        Assert.That(response.Result, Is.EqualTo("G7 is the dominant chord in C major."));
        AssertNoRawJson(response.Result);
    }

    [Test]
    public async Task PlainProse_IsSurfacedAsIs()
    {
        var response = await TheoryAnswerFor("G7 is the dominant chord in C major.");

        Assert.That(response.Result, Is.EqualTo("G7 is the dominant chord in C major."));
    }

    [TestCase("")]
    [TestCase("```json\n{\"confidence\": 0.9, \"evid")]
    public async Task NothingUsable_NeverShowsRawJsonOrInternalMarker(string modelOutput)
    {
        var response = await TheoryAnswerFor(modelOutput);

        AssertNoRawJson(response.Result);
        Assert.That(response.Result, Does.Not.Contain("failed"));
    }

    private const string VexTabExample = """
        ```vextab
        tabstave notation=true
        notes :q 3/5 2/4 0/3 1/2
        ```
        """;

    [Test]
    public async Task ProseWithFencedTab_KeepsTheTab()
    {
        // A usable plain-text answer with a fenced musical example must survive
        // the fallback intact (Codex P2 on #903).
        var response = await TheoryAnswerFor("Here is a C major arpeggio:\n" + VexTabExample);

        Assert.Multiple(() =>
        {
            Assert.That(response.Result, Does.Contain("Here is a C major arpeggio:"));
            Assert.That(response.Result, Does.Contain("```vextab"));
            Assert.That(response.Result, Does.Contain("notes :q 3/5 2/4 0/3 1/2"));
        });
    }

    [Test]
    public async Task EntirelyFencedTab_IsKept()
    {
        var response = await TheoryAnswerFor(VexTabExample);

        Assert.That(response.Result, Does.Contain("notes :q 3/5 2/4 0/3 1/2"));
    }

    [Test]
    public async Task ProseTabAndBrokenJson_DropsOnlyTheJson()
    {
        var response = await TheoryAnswerFor(
            "Here is a C major arpeggio:\n" + VexTabExample + "\n```json\n{\"confidence\": 0.9, \"evidence\": [\"tri");

        Assert.Multiple(() =>
        {
            Assert.That(response.Result, Does.Contain("Here is a C major arpeggio:"));
            Assert.That(response.Result, Does.Contain("notes :q 3/5 2/4 0/3 1/2"));
            Assert.That(response.Result, Does.Not.Contain("```json"));
            Assert.That(response.Result, Does.Not.Contain("\"confidence\""));
        });
    }
}
