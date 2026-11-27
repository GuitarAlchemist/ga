namespace GA.Business.Core.Tests.AI.Chatbot;

using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Testing.Semantic;

[TestFixture]
[NonParallelizable] // AutoConfigureJudge writes AssertAi's process-wide judge.
public class ChatClientJudgeServiceTests
{
    [Test]
    public async Task EvaluateAsync_SendsTheJudgePromptToTheChatClient()
    {
        var chatClient = new RecordingChatClient("""{"isPassing": true, "rationale": "ok", "confidence": 0.5}""");
        var judge = new ChatClientJudgeService(chatClient);

        await judge.EvaluateAsync("C major is C E G.", "Be strict.", "Must name the triad.");

        Assert.That(chatClient.LastMessages, Has.Count.EqualTo(1));
        var message = chatClient.LastMessages![0];
        Assert.Multiple(() =>
        {
            Assert.That(message.Role, Is.EqualTo(ChatRole.User));
            Assert.That(message.Text, Does.StartWith("Be strict.\n\nYou are an AI Judge."));
            Assert.That(message.Text, Does.Contain("TEXT TO EVALUATE:\n\"\"\"\nC major is C E G.\n\"\"\""));
            Assert.That(message.Text, Does.Contain("RUBRIC:\nMust name the triad."));
            Assert.That(message.Text, Does.Contain("\"isPassing\": (boolean)"));
            Assert.That(chatClient.LastOptions?.ResponseFormat, Is.InstanceOf<ChatResponseFormatJson>());
        });
    }

    [Test]
    public async Task EvaluateAsync_ParsesTheJudgeVerdict()
    {
        var chatClient = new RecordingChatClient(
            """{"IsPassing": true, "Rationale": "Names the root, third and fifth.", "Confidence": 0.85}""");
        var judge = new ChatClientJudgeService(chatClient);

        var result = await judge.EvaluateAsync("text", "prompt", "rubric");

        Assert.That(result, Is.EqualTo(new JudgeResult(true, "Names the root, third and fifth.", 0.85)));
    }

    [Test]
    public async Task EvaluateAsync_ReportsAReplyThatIsNotJsonAsAFailure()
    {
        var chatClient = new RecordingChatClient("I think it passes.");
        var judge = new ChatClientJudgeService(chatClient);

        var result = await judge.EvaluateAsync("text", "prompt", "rubric");

        Assert.Multiple(() =>
        {
            Assert.That(result.IsPassing, Is.False);
            Assert.That(result.Confidence, Is.Zero);
            Assert.That(result.Rationale, Does.StartWith("Failed to parse judge JSON:"));
            Assert.That(result.Rationale, Does.EndWith("Raw content: I think it passes."));
        });
    }

    [Test]
    public async Task EvaluateAsync_DefaultsMissingFields()
    {
        var chatClient = new RecordingChatClient("null");
        var judge = new ChatClientJudgeService(chatClient);

        var result = await judge.EvaluateAsync("text", "prompt", "rubric");

        Assert.That(result, Is.EqualTo(new JudgeResult(false, "No rationale provided", 0)));
    }

    [Test]
    public void AutoConfigureJudge_UsesClaudeByDefault_WhenAnApiKeyIsConfigured()
    {
        var configuration = Configuration(("Anthropic:ApiKey", "test-key"));

        Assert.That(AssertAi.AutoConfigureJudge(configuration), Is.EqualTo("claude"));
    }

    [Test]
    public void AutoConfigureJudge_ConfiguresNothing_WhenClaudeHasNoApiKey()
    {
        // A blank configured key masks any ANTHROPIC_API_KEY on the machine.
        var configuration = Configuration(("Anthropic:ApiKey", " "));

        Assert.That(AssertAi.AutoConfigureJudge(configuration), Is.Null);
    }

    [Test]
    public void AutoConfigureJudge_UsesOllamaOnlyWhenTheSettingSaysSo()
    {
        var configuration = Configuration(("AI:ChatProvider", "Ollama"));

        Assert.That(AssertAi.AutoConfigureJudge(configuration), Is.EqualTo("ollama"));
    }

    private static IConfiguration Configuration(params (string Key, string Value)[] values) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(values.Select(v => new KeyValuePair<string, string?>(v.Key, v.Value)))
            .Build();

    private sealed class RecordingChatClient(string responseText) : IChatClient
    {
        public List<ChatMessage>? LastMessages { get; private set; }

        public ChatOptions? LastOptions { get; private set; }

        public Task<ChatResponse> GetResponseAsync(
            IEnumerable<ChatMessage> messages,
            ChatOptions? options = null,
            CancellationToken cancellationToken = default)
        {
            LastMessages = [.. messages];
            LastOptions = options;
            return Task.FromResult(new ChatResponse(new ChatMessage(ChatRole.Assistant, responseText)));
        }

        public IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
            IEnumerable<ChatMessage> messages,
            ChatOptions? options = null,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public object? GetService(Type serviceType, object? serviceKey = null) => null;

        public void Dispose()
        {
        }
    }
}
