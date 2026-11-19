namespace GA.Business.Core.Tests.Orchestration;

using GA.Business.Core.Orchestration.Clients;
using GA.Business.Core.Orchestration.Services;
using GA.Infrastructure.Documentation;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

/// <summary>
/// Filter extraction sends the JSON schema of <c>QueryFilters</c>: Ollama enforces it while decoding,
/// and the chat mode writes it into the prompt. Without it Haiku 5.5 answered
/// <c>"Quality": ["Minor"]</c> for some queries and deserialization dropped every filter.
/// </summary>
[TestFixture]
public class QueryUnderstandingSchemaTests
{
    [Test]
    public async Task ExtractFilters_SendsTheFilterSchema_WithScalarFields()
    {
        string? prompt = null;
        var chat = new Mock<IChatClient>();
        chat.Setup(c => c.GetResponseAsync(It.IsAny<IEnumerable<ChatMessage>>(), It.IsAny<ChatOptions?>(), It.IsAny<CancellationToken>()))
            .Callback<IEnumerable<ChatMessage>, ChatOptions?, CancellationToken>((m, _, _) => prompt = m.Single().Text)
            .ReturnsAsync(new ChatResponse(new ChatMessage(ChatRole.Assistant, "{\"Quality\":\"Minor\",\"Key\":\"G major\"}")));
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["AI:Generate:Provider"] = "chat" })
            .Build();
        var service = new QueryUnderstandingService(
            new DomainMetadataPrompter(new SchemaDiscoveryService()),
            new OllamaGenerateClient(new Mock<IHttpClientFactory>(MockBehavior.Strict).Object, configuration, chat.Object),
            NullLogger<QueryUnderstandingService>.Instance);

        var filters = await service.ExtractFiltersAsync("sad minor chords in G major");

        Assert.Multiple(() =>
        {
            Assert.That(filters?.Quality, Is.EqualTo("Minor"));
            Assert.That(filters?.Key, Is.EqualTo("G major"));
            Assert.That(prompt, Does.Contain("JSON Schema"));
            Assert.That(prompt, Does.Match("\"Quality\":\\{\"type\":\\[\"string\",\"null\"\\]\\}"));
        });
    }
}
