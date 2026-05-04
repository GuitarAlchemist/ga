namespace GaChatbot.Api.Tests.Controllers;

using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;

[TestFixture]
public class ChatbotControllerAlgebraTests
{
    private WebApplicationFactory<Program>? _factory;
    private HttpClient? _client;

    [OneTimeSetUp]
    public void OneTimeSetUp()
    {
        _factory = new TestWebApplicationFactory()
            .WithWebHostBuilder(builder =>
            {
                builder.UseSetting("Chatbot:Mode", "full");
                builder.UseSetting("IX:Source", "ix-compatible");
                builder.UseSetting("IX:Revision", "7b02a56");
            });

        _client = _factory.CreateClient();
    }

    [OneTimeTearDown]
    public void OneTimeTearDown()
    {
        _client?.Dispose();
        _factory?.Dispose();
    }

    [Test]
    public async Task Chat_AlgebraPrompt_UsesIxAlgebraRoute_AndReturnsGrounding()
    {
        var response = await _client!.PostAsJsonAsync("/api/chatbot/chat", new
        {
            message = "Are 0146 and 0137 z-related?"
        });

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.That(body.TryGetProperty("routingMethod", out var routingMethod), Is.True);
        Assert.That(routingMethod.GetString(), Is.EqualTo("ix-algebra"));

        Assert.That(body.TryGetProperty("agentId", out var agentId), Is.True);
        Assert.That(agentId.GetString(), Is.EqualTo("algebra"));

        Assert.That(body.TryGetProperty("grounding", out var grounding), Is.True);
        Assert.That(grounding.GetProperty("source").GetString(), Is.EqualTo("ix-compatible"));
        Assert.That(grounding.GetProperty("revision").GetString(), Is.EqualTo("7b02a56"));
        Assert.That(grounding.GetProperty("queryType").GetString(), Is.EqualTo("z-relation"));

        var answer = body.GetProperty("naturalLanguageAnswer").GetString();
        Assert.That(answer, Does.Contain("share ICV").Or.Contain("not Z-related"));
    }

    [Test]
    public async Task Chat_AlgebraPrompt_UsesExternalIxBinary_WhenAvailable()
    {
        var ixBinary = FindSiblingIxBinary();
        if (ixBinary is null)
        {
            Assert.Ignore("Sibling IX ga-chatbot release binary was not found.");
        }

        using var factory = new TestWebApplicationFactory()
            .WithWebHostBuilder(builder =>
            {
                builder.UseSetting("Chatbot:Mode", "full");
                builder.UseSetting("IX:Source", "ix-compatible");
                builder.UseSetting("IX:Revision", "7b02a56");
                builder.UseSetting("IX:External:ExecutablePath", ixBinary);
            });
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/chatbot/chat", new
        {
            message = "Are 0146 and 0137 z-related?"
        });

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.That(body.GetProperty("routingMethod").GetString(), Is.EqualTo("ix-algebra"));
        Assert.That(body.GetProperty("agentId").GetString(), Is.EqualTo("algebra"));

        var grounding = body.GetProperty("grounding");
        Assert.That(grounding.GetProperty("source").GetString(), Is.EqualTo("ix"));
        Assert.That(grounding.GetProperty("revision").GetString(), Is.EqualTo("7b02a56"));
        Assert.That(grounding.GetProperty("queryType").GetString(), Is.EqualTo("z-relation"));

        var answer = body.GetProperty("naturalLanguageAnswer").GetString();
        Assert.That(answer, Does.Contain("4-Z15"));
        Assert.That(answer, Does.Contain("4-Z29"));
    }

    private static string? FindSiblingIxBinary()
    {
        var directory = new DirectoryInfo(TestContext.CurrentContext.TestDirectory);
        while (directory is not null)
        {
            if (directory.Name.Equals("ga", StringComparison.OrdinalIgnoreCase) &&
                directory.Parent is not null)
            {
                var candidate = Path.Combine(
                    directory.Parent.FullName,
                    "ix",
                    "target",
                    "release",
                    OperatingSystem.IsWindows() ? "ga-chatbot.exe" : "ga-chatbot");

                return File.Exists(candidate) ? candidate : null;
            }

            directory = directory.Parent;
        }

        return null;
    }
}
