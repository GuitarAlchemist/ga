namespace GuitarAlchemistChatbot.Tests.Services;

using GaApi.Configuration;
using GaApi.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using NUnit.Framework;

[TestFixture]
[Category("Chatbot")]
public sealed class ChatbotSessionOrchestratorTests
{
    [SetUp]
    public void SetUp()
    {
        _chatClientMock = new Mock<IOllamaChatService>(MockBehavior.Strict);
        _semanticKnowledgeMock = new Mock<ISemanticKnowledgeSource>(MockBehavior.Strict);
        _optionsSnapshotMock = new Mock<IOptionsSnapshot<ChatbotOptions>>();
        _loggerMock = new Mock<ILogger<ChatbotSessionOrchestrator>>();
    }

    private static readonly SemanticSearchService.SearchResult SampleSearchResult =
        new("id", "Pentatonic scale tabs", "theory", new Dictionary<string, object>(), 0.95, "keyword");

    private Mock<IOllamaChatService>? _chatClientMock;
    private Mock<ISemanticKnowledgeSource>? _semanticKnowledgeMock;
    private Mock<IOptionsSnapshot<ChatbotOptions>>? _optionsSnapshotMock;
    private Mock<ILogger<ChatbotSessionOrchestrator>>? _loggerMock;

    [Test]
    public async Task StreamResponseAsync_WithSemanticSearch_IncludesContextInPrompt()
    {
        var options = new ChatbotOptions
        {
            EnableSemanticSearch = true,
            SemanticSearchLimit = 5,
            SemanticContextDocuments = 2,
            HistoryLimit = 5
        };

        var sut = CreateSut(options);

        _semanticKnowledgeMock!
            .Setup(service => service.SearchAsync("Explain modes", 5, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<SemanticSearchService.SearchResult>
            {
                SampleSearchResult
            });

        string? capturedPrompt = null;

        _chatClientMock!
            .Setup(client => client.ChatStreamAsync(
                "Explain modes",
                It.Is<List<ChatMessage>>(history => history.Count == 1 && history[0].Role == "assistant"),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .Callback<string, List<ChatMessage>, string,
                CancellationToken>((_, _, prompt, _) => capturedPrompt = prompt)
            .Returns<string, List<ChatMessage>, string, CancellationToken>((_, _, _, _) =>
                ToAsyncEnumerable("Modes are..."));

        var request = new ChatSessionRequest(
            "Explain modes",
            new[]
            {
                new ChatMessage { Role = "system", Content = "Ignore" },
                new ChatMessage { Role = "assistant", Content = "Sure!" },
                new ChatMessage { Role = "user", Content = "" }
            },
            true);

        var chunks = new List<string>();
        await foreach (var chunk in sut.StreamResponseAsync(request, CancellationToken.None))
        {
            chunks.Add(chunk);
        }

        Assert.That(chunks, Is.Not.Empty);
        Assert.That(capturedPrompt, Does.Contain("Relevant guitar knowledge"),
            "Semantic context should be injected into system prompt.");

        _semanticKnowledgeMock.Verify(
            service => service.SearchAsync("Explain modes", 5, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Test]
    public async Task StreamResponseAsync_WhenSemanticSearchDisabled_SkipsLookup()
    {
        var options = new ChatbotOptions
        {
            EnableSemanticSearch = false
        };

        var sut = CreateSut(options);

        _semanticKnowledgeMock!
            .Setup(service => service.SearchAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<SemanticSearchService.SearchResult>());

        _chatClientMock!
            .Setup(client => client.ChatStreamAsync(
                It.IsAny<string>(),
                It.IsAny<List<ChatMessage>>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .Returns<string, List<ChatMessage>, string, CancellationToken>((_, _, _, _) => ToAsyncEnumerable("Hi"));

        var request = new ChatSessionRequest("Hello", [], true);

        await foreach (var _ in sut.StreamResponseAsync(request, CancellationToken.None))
        {
        }

        _semanticKnowledgeMock.Verify(
            service => service.SearchAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Test]
    public void NormalizeHistory_TrimsAndLimits()
    {
        var options = new ChatbotOptions
        {
            HistoryLimit = 2
        };

        var sut = CreateSut(options);

        var history = new[]
        {
            new ChatMessage { Role = "system", Content = "ignore me" },
            new ChatMessage { Role = "assistant", Content = "First answer" },
            new ChatMessage { Role = "user", Content = "Question one" },
            new ChatMessage { Role = "assistant", Content = "Second answer" },
            new ChatMessage { Role = "user", Content = "Question two" }
        };

        var normalized = sut.NormalizeHistory(history);

        Assert.That(normalized, Has.Count.EqualTo(2));
        Assert.That(normalized[0].Content, Is.EqualTo("Second answer"));
        Assert.That(normalized[1].Content, Is.EqualTo("Question two"));
    }

    [Test]
    public async Task GetResponseAsync_DelegatesToChatClient()
    {
        var options = new ChatbotOptions();
        var sut = CreateSut(options);

        _semanticKnowledgeMock!
            .Setup(service => service.SearchAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<SemanticSearchService.SearchResult>());

        _chatClientMock!
            .Setup(client => client.ChatAsync(
                "Play blues",
                It.IsAny<List<ChatMessage>>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync("Use the I-IV-V progression");

        var response = await sut.GetResponseAsync(
            new ChatSessionRequest("Play blues", [], true),
            CancellationToken.None);

        Assert.That(response, Is.EqualTo("Use the I-IV-V progression"));
    }

    private ChatbotSessionOrchestrator CreateSut(ChatbotOptions options)
    {
        _optionsSnapshotMock!.Setup(snapshot => snapshot.Value).Returns(options);

        return new ChatbotSessionOrchestrator(
            _chatClientMock!.Object,
            _semanticKnowledgeMock!.Object,
            _optionsSnapshotMock.Object,
            _loggerMock!.Object);
    }

    private static async IAsyncEnumerable<string> ToAsyncEnumerable(params string[] chunks)
    {
        foreach (var chunk in chunks)
        {
            yield return chunk;
            await Task.Yield();
        }
    }
}
