namespace GaChatbot.Api.Tests.Controllers;

using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using GA.Business.ML.Agents.Memory;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

/// <summary>
/// The chatbot with no model backend at all: Ollama points at a closed port, so every
/// embedding and chat call fails fast (as on a CI runner or a laptop without Ollama).
/// Deterministic skills must still answer, and questions that need the LLM must get
/// an honest answer rather than an HTTP 500.
/// </summary>
[TestFixture]
public class ChatbotControllerOfflineTests
{
    // Port 9 (discard) is closed on CI runners and dev machines.
    private const string DeadOllama = "http://127.0.0.1:9";

    private WebApplicationFactory<Program>? _factory;
    private HttpClient? _client;
    private string? _memoryDir;

    [OneTimeSetUp]
    public void OneTimeSetUp()
    {
        _memoryDir = Path.Combine(Path.GetTempPath(), $"ga-chatbot-offline-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_memoryDir);

        _factory = new TestWebApplicationFactory()
            .WithWebHostBuilder(builder =>
            {
                builder.UseSetting("Chatbot:Mode", "full");
                builder.UseSetting("Ollama:BaseUrl", DeadOllama);
                builder.UseSetting("Ollama:Endpoint", DeadOllama);
                builder.UseSetting("IX:External:Enabled", "false");
                builder.ConfigureTestServices(services =>
                {
                    // Keep chat memory out of the user's ~/.ga.
                    services.AddSingleton(new MemoryStore(Path.Combine(_memoryDir, "memory.json")));
                    services.AddSingleton(new ChatTranscriptStore(Path.Combine(_memoryDir, "transcripts.json")));
                });
            });

        _client = _factory.CreateClient();
    }

    [OneTimeTearDown]
    public void OneTimeTearDown()
    {
        _client?.Dispose();
        _factory?.Dispose();
        if (_memoryDir is not null && Directory.Exists(_memoryDir))
        {
            Directory.Delete(_memoryDir, recursive: true);
        }
    }

    [Test]
    public async Task Chat_RelativeKeyQuestion_IsAnsweredByRelativeKeySkillWithoutEmbeddings()
    {
        var response = await _client!.PostAsJsonAsync("/api/chatbot/chat", new
        {
            message = "What is the relative minor of C major?"
        });

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Multiple(() =>
        {
            Assert.That(body.GetProperty("agentId").GetString(), Is.EqualTo("skill.relativekey"));
            Assert.That(body.GetProperty("naturalLanguageAnswer").GetString(), Does.Contain("**Am**"));
        });
    }

    [Test]
    public async Task Chat_QuestionThatNeedsTheLlm_GetsAnHonestAnswerInsteadOf500()
    {
        var response = await _client!.PostAsJsonAsync("/api/chatbot/chat", new
        {
            message = "Why does a ii-V-I cadence sound resolved in harmonic theory?"
        });

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Multiple(() =>
        {
            Assert.That(body.GetProperty("agentId").GetString(), Is.EqualTo("fallback-direct"));
            Assert.That(body.GetProperty("naturalLanguageAnswer").GetString(), Does.Contain("unavailable"));
        });
    }
}
