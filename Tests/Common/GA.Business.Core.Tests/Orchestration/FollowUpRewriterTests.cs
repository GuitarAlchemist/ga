namespace GA.Business.Core.Tests.Orchestration;

using GA.Business.Core.Orchestration.Services;
using GA.Business.ML.Agents;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

/// <summary>
/// A follow-up such as "And a C shape?" is rewritten into a question that stands on its own, so
/// routing and the skills, which see only the current message, act on what the user means.
/// </summary>
[TestFixture]
public class FollowUpRewriterTests
{
    private static readonly IReadOnlyList<ChatHistoryTurn> History =
    [
        new("user", "With a capo on fret 2, what key does a G shape sound in?"),
        new("assistant", "A G shape with a capo on fret 2 sounds as A major."),
    ];

    private const string Standalone = "With a capo on fret 2, what key does a C shape sound in?";

    [Test]
    public async Task Enabled_WithHistory_ReturnsTheStandaloneQuestion_AndShowsTheConversation()
    {
        var (rewriter, sent) = Rewriter(enabled: true, reply: $"  {Standalone}\n");

        var result = await rewriter.RewriteAsync("And a C shape?", History);

        Assert.Multiple(() =>
        {
            Assert.That(result, Is.EqualTo(Standalone));
            Assert.That(sent, Has.Count.EqualTo(1));
            Assert.That(sent[0].Single().Text, Does.Contain("G shape sound in?").And.Contain("And a C shape?"));
        });
    }

    [Test]
    public async Task Disabled_ReturnsTheMessage_WithoutCallingTheModel()
    {
        var (rewriter, sent) = Rewriter(enabled: false, reply: Standalone);

        Assert.That(await rewriter.RewriteAsync("And a C shape?", History), Is.EqualTo("And a C shape?"));
        Assert.That(sent, Is.Empty);
    }

    [Test]
    public async Task WithoutHistory_ReturnsTheMessage_WithoutCallingTheModel()
    {
        var (rewriter, sent) = Rewriter(enabled: true, reply: Standalone);

        Assert.That(await rewriter.RewriteAsync("What is a G shape?", []), Is.EqualTo("What is a G shape?"));
        Assert.That(sent, Is.Empty);
    }

    [Test]
    public async Task ModelFailure_KeepsTheMessage()
    {
        var chat = new Mock<IChatClient>();
        chat.Setup(c => c.GetResponseAsync(It.IsAny<IEnumerable<ChatMessage>>(), It.IsAny<ChatOptions?>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("down"));

        var rewriter = new FollowUpRewriter(Config(true), NullLogger<FollowUpRewriter>.Instance, chat.Object);

        Assert.That(await rewriter.RewriteAsync("And a C shape?", History), Is.EqualTo("And a C shape?"));
    }

    [TestCase("")]
    // The model answered instead of rewriting.
    [TestCase("A C shape with a capo on fret 2 sounds as D major. The capo raises every fingered note by two semitones, so C becomes D, its E becomes F#, and its G becomes A. The same reasoning applies to every chord shape you fret behind the capo: name the open shape, then move its root up by the number of frets the capo covers. With the capo on fret 2, an A shape sounds as B and an E shape as F#.")]
    public async Task EmptyReplyOrAnAnswer_KeepsTheMessage(string reply)
    {
        var (rewriter, _) = Rewriter(enabled: true, reply: reply);

        Assert.That(await rewriter.RewriteAsync("And a C shape?", History), Is.EqualTo("And a C shape?"));
    }

    private static (FollowUpRewriter Rewriter, List<List<ChatMessage>> Sent) Rewriter(bool enabled, string reply)
    {
        List<List<ChatMessage>> sent = [];
        var chat = new Mock<IChatClient>();
        chat.Setup(c => c.GetResponseAsync(It.IsAny<IEnumerable<ChatMessage>>(), It.IsAny<ChatOptions?>(), It.IsAny<CancellationToken>()))
            .Callback<IEnumerable<ChatMessage>, ChatOptions?, CancellationToken>((m, _, _) => sent.Add([.. m]))
            .ReturnsAsync(new ChatResponse(new ChatMessage(ChatRole.Assistant, reply)));
        return (new FollowUpRewriter(Config(enabled), NullLogger<FollowUpRewriter>.Instance, chat.Object), sent);
    }

    private static IConfiguration Config(bool enabled) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Chatbot:FollowUpRewrite"] = enabled ? "true" : "false" })
            .Build();
}
