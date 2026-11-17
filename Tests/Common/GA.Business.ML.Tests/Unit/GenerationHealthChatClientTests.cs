namespace GA.Business.ML.Tests.Unit;

using GA.Business.ML.Providers;
using Microsoft.Extensions.AI;
using AiChatMessage = Microsoft.Extensions.AI.ChatMessage;
using AiChatResponse = Microsoft.Extensions.AI.ChatResponse;

[TestFixture]
public sealed class GenerationHealthChatClientTests
{
    private const string OllamaCreditsError =
        "Ollama error: Add usage credits or subscribe to continue using cloud models.";

    [Test]
    public async Task GetResponseAsync_Succeeds_ReplacesAnEarlierFailure()
    {
        // Codex P2 on #892: a successful agent generation must clear a failed
        // fallback, not only another direct-chat success.
        var health = new ChatGenerationHealth();
        health.RecordFailure("earlier failure");
        var client = new GenerationHealthChatClient(new FakeChatClient(), health);

        await client.GetResponseAsync([new AiChatMessage(ChatRole.User, "hi")]);

        Assert.That(health.Latest?.Ok, Is.True);
    }

    [Test]
    public void GetResponseAsync_ProviderFails_RecordsFailureAndRethrows()
    {
        var health = new ChatGenerationHealth();
        var client = new GenerationHealthChatClient(
            new FakeChatClient(new InvalidOperationException(OllamaCreditsError)), health);

        Assert.That(
            async () => await client.GetResponseAsync([new AiChatMessage(ChatRole.User, "hi")]),
            Throws.InstanceOf<InvalidOperationException>());
        Assert.Multiple(() =>
        {
            Assert.That(health.Latest?.Ok, Is.False);
            Assert.That(health.Latest?.Diagnostic, Does.Contain("Add usage credits"));
        });
    }

    [Test]
    public void GetResponseAsync_CallerCancelled_RecordsNothing()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        var health = new ChatGenerationHealth();
        var client = new GenerationHealthChatClient(
            new FakeChatClient(new OperationCanceledException(cts.Token)), health);

        Assert.That(
            async () => await client.GetResponseAsync([new AiChatMessage(ChatRole.User, "hi")], cancellationToken: cts.Token),
            Throws.InstanceOf<OperationCanceledException>());
        Assert.That(health.Latest, Is.Null);
    }

    [Test]
    public async Task GetStreamingResponseAsync_RecordsSuccessAndFailure()
    {
        var health = new ChatGenerationHealth();
        var ok = new GenerationHealthChatClient(new FakeChatClient(), health);
        await foreach (var _ in ok.GetStreamingResponseAsync([new AiChatMessage(ChatRole.User, "hi")]))
        {
        }

        Assert.That(health.Latest?.Ok, Is.True);

        var failing = new GenerationHealthChatClient(
            new FakeChatClient(new HttpRequestException("402 (Payment Required)")), health);
        Assert.That(async () =>
        {
            await foreach (var _ in failing.GetStreamingResponseAsync([new AiChatMessage(ChatRole.User, "hi")]))
            {
            }
        }, Throws.InstanceOf<HttpRequestException>());
        Assert.That(health.Latest?.Ok, Is.False);
    }

    private sealed class FakeChatClient(Exception? failure = null) : IChatClient
    {
        public Task<AiChatResponse> GetResponseAsync(
            IEnumerable<AiChatMessage> messages,
            ChatOptions? options = null,
            CancellationToken cancellationToken = default) =>
            failure is null
                ? Task.FromResult(new AiChatResponse(new AiChatMessage(ChatRole.Assistant, "answer")))
                : Task.FromException<AiChatResponse>(failure);

        public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
            IEnumerable<AiChatMessage> messages,
            ChatOptions? options = null,
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            await Task.Yield();
            if (failure is not null)
            {
                throw failure;
            }

            yield return new ChatResponseUpdate(ChatRole.Assistant, "answer");
        }

        public object? GetService(Type serviceType, object? serviceKey = null) => null;

        public void Dispose()
        {
        }
    }
}
