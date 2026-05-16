namespace GA.Business.ML.Tests.Unit;

using GA.Business.ML.Providers;
using Microsoft.Extensions.AI;
using Moq;

[TestFixture]
public class CascadingChatClientTests
{
    private static ChatMessage SystemMessage(string content) => new(ChatRole.System, content);
    private static ChatMessage UserMessage(string content)   => new(ChatRole.User, content);

    private static ChatResponse FakeResponse(string text, string modelId = "primary-model") =>
        new([new ChatMessage(ChatRole.Assistant, text)]) { ModelId = modelId };

    [Test]
    public async Task GetResponseAsync_PrimarySucceeds_SecondaryNeverCalled()
    {
        var primary   = new Mock<IChatClient>(MockBehavior.Strict);
        var secondary = new Mock<IChatClient>(MockBehavior.Strict);

        primary
            .Setup(c => c.GetResponseAsync(It.IsAny<IEnumerable<ChatMessage>>(), It.IsAny<ChatOptions>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(FakeResponse("hello from primary"));

        var cascade = new CascadingChatClient(primary.Object, secondary.Object);
        var response = await cascade.GetResponseAsync([UserMessage("hi")]);

        Assert.That(response.Messages[0].Text, Is.EqualTo("hello from primary"));
        secondary.VerifyNoOtherCalls();
    }

    [Test]
    public async Task GetResponseAsync_PrimaryTimesOut_FallsBackToSecondary()
    {
        var primary   = new Mock<IChatClient>(MockBehavior.Strict);
        var secondary = new Mock<IChatClient>(MockBehavior.Strict);

        // Primary cancels via its own inner CTS (not the caller's token).
        primary
            .Setup(c => c.GetResponseAsync(It.IsAny<IEnumerable<ChatMessage>>(), It.IsAny<ChatOptions>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new OperationCanceledException("primary timed out"));

        secondary
            .Setup(c => c.GetResponseAsync(It.IsAny<IEnumerable<ChatMessage>>(), It.IsAny<ChatOptions>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(FakeResponse("recovered by secondary", "secondary-model"));

        var cascade = new CascadingChatClient(primary.Object, secondary.Object);
        var response = await cascade.GetResponseAsync([UserMessage("hi")]);

        Assert.That(response.Messages[0].Text, Is.EqualTo("recovered by secondary"));
        Assert.That(response.ModelId,          Is.EqualTo("secondary-model"));
    }

    [Test]
    public async Task GetResponseAsync_PrimaryHttpFailure_FallsBackToSecondary()
    {
        var primary   = new Mock<IChatClient>();
        var secondary = new Mock<IChatClient>();

        primary
            .Setup(c => c.GetResponseAsync(It.IsAny<IEnumerable<ChatMessage>>(), It.IsAny<ChatOptions>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("connection refused"));

        secondary
            .Setup(c => c.GetResponseAsync(It.IsAny<IEnumerable<ChatMessage>>(), It.IsAny<ChatOptions>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(FakeResponse("from secondary"));

        var cascade = new CascadingChatClient(primary.Object, secondary.Object);
        var response = await cascade.GetResponseAsync([UserMessage("hi")]);

        Assert.That(response.Messages[0].Text, Is.EqualTo("from secondary"));
    }

    [Test]
    public void GetResponseAsync_CallerCancels_DoesNotCascade()
    {
        var primary   = new Mock<IChatClient>();
        var secondary = new Mock<IChatClient>(MockBehavior.Strict);

        primary
            .Setup(c => c.GetResponseAsync(It.IsAny<IEnumerable<ChatMessage>>(), It.IsAny<ChatOptions>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new OperationCanceledException("cancelled by caller"));

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var cascade = new CascadingChatClient(primary.Object, secondary.Object);

        Assert.That(
            async () => await cascade.GetResponseAsync([UserMessage("hi")], cancellationToken: cts.Token),
            Throws.InstanceOf<OperationCanceledException>());

        // Strict mock ensures secondary was never invoked.
        secondary.VerifyNoOtherCalls();
    }

    [Test]
    public async Task GetResponseAsync_NonRecoverableException_PropagatesWithoutCascade()
    {
        var primary   = new Mock<IChatClient>();
        var secondary = new Mock<IChatClient>(MockBehavior.Strict);

        primary
            .Setup(c => c.GetResponseAsync(It.IsAny<IEnumerable<ChatMessage>>(), It.IsAny<ChatOptions>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("bug in primary, not network"));

        var cascade = new CascadingChatClient(primary.Object, secondary.Object);

        Assert.That(
            async () => await cascade.GetResponseAsync([UserMessage("hi")]),
            Throws.InstanceOf<InvalidOperationException>());

        secondary.VerifyNoOtherCalls();
        await Task.CompletedTask;
    }

    [Test]
    public async Task GetResponseAsync_MaterializesMessagesOnce_AllowsCascadeReplay()
    {
        var primary   = new Mock<IChatClient>();
        var secondary = new Mock<IChatClient>();

        primary
            .Setup(c => c.GetResponseAsync(It.IsAny<IEnumerable<ChatMessage>>(), It.IsAny<ChatOptions>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("primary down"));

        IEnumerable<ChatMessage>? capturedFromSecondary = null;
        secondary
            .Setup(c => c.GetResponseAsync(It.IsAny<IEnumerable<ChatMessage>>(), It.IsAny<ChatOptions>(), It.IsAny<CancellationToken>()))
            .Callback<IEnumerable<ChatMessage>, ChatOptions?, CancellationToken>((msgs, _, _) => capturedFromSecondary = msgs)
            .ReturnsAsync(FakeResponse("recovered"));

        var cascade = new CascadingChatClient(primary.Object, secondary.Object);

        // Pass a deliberately single-use enumerable to prove materialization is happening.
        IEnumerable<ChatMessage> Stream()
        {
            yield return SystemMessage("you are helpful");
            yield return UserMessage("hello");
        }

        await cascade.GetResponseAsync(Stream());

        Assert.That(capturedFromSecondary, Is.Not.Null);
        Assert.That(capturedFromSecondary!.Count(), Is.EqualTo(2), "secondary must receive the same messages the primary saw");
    }

    [Test]
    public async Task GetStreamingResponseAsync_PrimaryYieldsTokens_DoesNotCascadeOnLaterFailure()
    {
        var primary   = new Mock<IChatClient>();
        var secondary = new Mock<IChatClient>(MockBehavior.Strict);

        primary
            .Setup(c => c.GetStreamingResponseAsync(It.IsAny<IEnumerable<ChatMessage>>(), It.IsAny<ChatOptions>(), It.IsAny<CancellationToken>()))
            .Returns(YieldThenFail("hello ", "world", new HttpRequestException("connection dropped mid-stream")));

        var cascade = new CascadingChatClient(primary.Object, secondary.Object);

        var collected = new List<string>();
        Assert.That(async () =>
        {
            await foreach (var update in cascade.GetStreamingResponseAsync([UserMessage("hi")]))
            {
                if (!string.IsNullOrEmpty(update.Text)) collected.Add(update.Text);
            }
        }, Throws.InstanceOf<HttpRequestException>(),
        "mid-stream failures must surface, not silently cascade to a different model");

        Assert.That(collected, Is.EquivalentTo(new[] { "hello ", "world" }));
        secondary.VerifyNoOtherCalls();
        await Task.CompletedTask;
    }

    [Test]
    public void GetStreamingResponseAsync_CallerCancelsBeforeFirstToken_DoesNotCascade()
    {
        // Codex P1 review on PR #225: the pre-enumerator catch must distinguish
        // caller-driven cancellation from primary-internal failures. Otherwise
        // a cancelled caller still gets a wasted outbound request to secondary.
        var primary   = new Mock<IChatClient>();
        var secondary = new Mock<IChatClient>(MockBehavior.Strict);

        primary
            .Setup(c => c.GetStreamingResponseAsync(It.IsAny<IEnumerable<ChatMessage>>(), It.IsAny<ChatOptions>(), It.IsAny<CancellationToken>()))
            .Returns(FailBeforeYielding(new OperationCanceledException("caller cancelled")));

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var cascade = new CascadingChatClient(primary.Object, secondary.Object);

        Assert.That(async () =>
        {
            await foreach (var _ in cascade.GetStreamingResponseAsync([UserMessage("hi")], cancellationToken: cts.Token))
            {
                // empty
            }
        }, Throws.InstanceOf<OperationCanceledException>());

        secondary.VerifyNoOtherCalls();
    }

    [Test]
    public async Task GetStreamingResponseAsync_PrimaryFailsBeforeFirstToken_CascadesToSecondary()
    {
        var primary   = new Mock<IChatClient>();
        var secondary = new Mock<IChatClient>();

        primary
            .Setup(c => c.GetStreamingResponseAsync(It.IsAny<IEnumerable<ChatMessage>>(), It.IsAny<ChatOptions>(), It.IsAny<CancellationToken>()))
            .Returns(FailBeforeYielding(new OperationCanceledException("primary inner timeout")));

        secondary
            .Setup(c => c.GetStreamingResponseAsync(It.IsAny<IEnumerable<ChatMessage>>(), It.IsAny<ChatOptions>(), It.IsAny<CancellationToken>()))
            .Returns(YieldThenComplete("from ", "mistral"));

        var cascade = new CascadingChatClient(primary.Object, secondary.Object);

        var collected = new List<string>();
        await foreach (var update in cascade.GetStreamingResponseAsync([UserMessage("hi")]))
        {
            if (!string.IsNullOrEmpty(update.Text)) collected.Add(update.Text);
        }

        Assert.That(string.Concat(collected), Is.EqualTo("from mistral"));
    }

    private static async IAsyncEnumerable<ChatResponseUpdate> YieldThenFail(string a, string b, Exception throwAfter)
    {
        yield return new ChatResponseUpdate(ChatRole.Assistant, a);
        await Task.Yield();
        yield return new ChatResponseUpdate(ChatRole.Assistant, b);
        await Task.Yield();
        throw throwAfter;
    }

    private static async IAsyncEnumerable<ChatResponseUpdate> FailBeforeYielding(Exception toThrow)
    {
        await Task.Yield();
        if (toThrow is not null) throw toThrow;
        yield break;
    }

    private static async IAsyncEnumerable<ChatResponseUpdate> YieldThenComplete(params string[] parts)
    {
        foreach (var p in parts)
        {
            await Task.Yield();
            yield return new ChatResponseUpdate(ChatRole.Assistant, p);
        }
    }
}
