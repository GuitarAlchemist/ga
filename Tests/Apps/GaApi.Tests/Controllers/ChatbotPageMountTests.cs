namespace GaApi.Tests.Controllers;

using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using GA.Business.Core.Orchestration.Abstractions;
using GA.Business.Core.Orchestration.Models;
using GA.Core.Functional;
using GaApi.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Moq;

/// <summary>
///     The public chatbot page is served by GaApi under <c>/chatbot/</c>, the path cloudflared
///     forwards from demos.guitaralchemist.com. The page calls its API with relative URLs, so
///     every call it makes arrives as <c>/chatbot/api/chatbot/*</c>. These tests pin what the
///     page needs: the shell and its assets, the API under the mount, a session cookie the
///     browser sends back, and SSE frames its parser reads whole (ADR-0005, GaChatbot.Api cutover).
/// </summary>
[TestFixture]
[Category("Integration")]
public class ChatbotPageMountTests
{
    private const string MultiLineAnswer =
        "The diatonic chords of G major:\n\n| Degree | Chord |\n|---|---|\n| I | G |\n| ii | Am |";

    private WebApplicationFactory<Program>? _factory;
    private HttpClient? _client;
    private Mock<IChatIntake>? _intake;

    [OneTimeSetUp]
    public void OneTimeSetUp()
    {
        _intake = new Mock<IChatIntake>();
        _intake.Setup(service => service.IntakeAsync(It.IsAny<ChatIntakeRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<ChatResponse, ChatIntakeError>.Success(new ChatResponse(MultiLineAnswer, [])));
        var provider = new Mock<IChatService>();
        provider.Setup(service => service.IsAvailableAsync(It.IsAny<CancellationToken>())).ReturnsAsync(true);
        _factory = new TestWebApplicationFactory().WithWebHostBuilder(builder => builder.ConfigureServices(services =>
        {
            services.RemoveAll<IChatIntake>();
            services.AddSingleton(_intake.Object);
            services.RemoveAll<IChatService>();
            services.AddSingleton(provider.Object);
        }));
        _client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = false });
    }

    [OneTimeTearDown]
    public void OneTimeTearDown()
    {
        _client?.Dispose();
        _factory?.Dispose();
    }

    [Test]
    public async Task MountWithoutTrailingSlash_RedirectsToTheMount_KeepingTheQuery()
    {
        // Without the slash the browser resolves the page's relative URLs against `/`.
        using var response = await _client!.GetAsync("/chatbot?ref=reddit");

        Assert.Multiple(() =>
        {
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.PermanentRedirect));
            Assert.That(response.Headers.Location?.OriginalString, Is.EqualTo("/chatbot/?ref=reddit"));
        });
    }

    [Test]
    public async Task Mount_ServesTheChatbotPage()
    {
        using var response = await _client!.GetAsync("/chatbot/");
        var html = await response.Content.ReadAsStringAsync();

        Assert.Multiple(() =>
        {
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            Assert.That(response.Content.Headers.ContentType?.MediaType, Is.EqualTo("text/html"));
            Assert.That(html, Does.Contain("id=\"chatForm\""));
        });
    }

    [Test]
    public async Task Mount_ServesThePageAssets()
    {
        using var response = await _client!.GetAsync("/chatbot/vendor/vexflow/vexflow.js");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
    }

    [TestCase("/chatbot/api/chatbot/status")]
    [TestCase("/chatbot/api/chatbot/examples")]
    [TestCase("/chatbot/api/chatbot/demo")]
    [TestCase("/chatbot/api/chatbot/qa-summary")]
    [TestCase("/api/chatbot/qa-summary")]
    public async Task PageApiCalls_ReachTheControllers(string path)
    {
        using var response = await _client!.GetAsync(path);

        Assert.Multiple(() =>
        {
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            Assert.That(response.Content.Headers.ContentType?.MediaType, Is.EqualTo("application/json"));
        });
    }

    [TestCase("/chatbot/api/chatbot/chat/stream", "/chatbot/api/chatbot")]
    [TestCase("/api/chatbot/chat/stream", "/api/chatbot")]
    public async Task SessionCookie_IsScopedToThePathTheBrowserCalls(string endpoint, string cookiePath)
    {
        using var response = await _client!.PostAsJsonAsync(endpoint, new { message = "What are the diatonic chords in G major?" });

        Assert.That(response.Headers.TryGetValues("Set-Cookie", out var cookies), Is.True, "no session cookie issued");
        var sessionCookie = cookies!.Single(cookie => cookie.StartsWith("ga_chat_session=", StringComparison.Ordinal));
        Assert.That(sessionCookie, Does.Contain($"path={cookiePath}").IgnoreCase);
    }

    [Test]
    public async Task ChatStream_UnderTheMount_SendsAnswersThePageParserReadsWhole()
    {
        using var response = await _client!.PostAsJsonAsync(
            "/chatbot/api/chatbot/chat/stream",
            new { message = "What are the diatonic chords in G major?", source = "reddit" });
        var body = await response.Content.ReadAsStringAsync();

        var events = ParseLikeThePage(body);
        Assert.Multiple(() =>
        {
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            Assert.That(events[0].Data, Does.StartWith("{").And.Contain("\"type\":\"routing\""));
            Assert.That(events[^1].Data, Is.EqualTo("[DONE]"));
            Assert.That(string.Concat(events.Skip(1).SkipLast(1).Select(e => e.Data)), Is.EqualTo(MultiLineAnswer),
                "every line of a multi-line chunk must carry its own data: prefix");
        });
    }

    [Test]
    public async Task ChatStream_Failure_IsANamedErrorEvent()
    {
        _intake!.Setup(service => service.IntakeAsync(
                It.Is<ChatIntakeRequest>(request => request.Message == "busy please"), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<ChatResponse, ChatIntakeError>.Failure(new ChatIntakeError.Busy()));

        using var response = await _client!.PostAsJsonAsync("/chatbot/api/chatbot/chat/stream", new { message = "busy please" });
        var events = ParseLikeThePage(await response.Content.ReadAsStringAsync());

        var error = events.Single();
        Assert.Multiple(() =>
        {
            Assert.That(error.Name, Is.EqualTo("error"));
            Assert.That(JsonDocument.Parse(error.Data).RootElement.GetProperty("error").GetString(), Does.Contain("busy"));
        });
    }

    [Test]
    public void ThePage_MatchesGaChatbotApisCopy_UntilThatHostIsRetired()
    {
        // GaChatbot.Api serves the same page until cloudflared moves /chatbot/ to GaApi.
        // One copy edited without the other would make the cutover change what users see.
        var gaApiCopy = TestPaths.RepositoryPath("Apps", "ga-server", "GaApi", "wwwroot", "chatbot");
        var chatbotApiCopy = TestPaths.RepositoryPath("Apps", "GaChatbot.Api", "wwwroot");
        if (!Directory.Exists(chatbotApiCopy)) Assert.Ignore("GaChatbot.Api is retired; GaApi holds the only copy.");

        foreach (var relative in new[] { "index.html", Path.Combine("vendor", "vexflow", "vexflow.js") })
        {
            Assert.That(File.ReadAllBytes(Path.Combine(gaApiCopy, relative)),
                Is.EqualTo(File.ReadAllBytes(Path.Combine(chatbotApiCopy, relative))), relative);
        }
    }

    private sealed record SseEvent(string? Name, string Data);

    // The page's consumeSseStream: events end at a blank line; `event:` names one; the
    // `data: ` lines of an event are joined with '\n'; anything else is ignored.
    private static List<SseEvent> ParseLikeThePage(string body) =>
        [.. body.Replace("\r", string.Empty)
            .Split("\n\n")
            .Select(raw => raw.Split('\n'))
            .Select(lines => (
                Name: lines.FirstOrDefault(line => line.StartsWith("event:", StringComparison.Ordinal))?[6..].Trim(),
                Data: lines.Where(line => line.StartsWith("data: ", StringComparison.Ordinal)).Select(line => line[6..]).ToList()))
            .Where(e => e.Data.Count > 0)
            .Select(e => new SseEvent(e.Name, string.Join('\n', e.Data)))];
}

/// <summary>
///     Outside Development, Program.cs requires X-Api-Key on /api/chatbot/*. The public page
///     sends no key, so its own calls must pass anonymously, as they did on GaChatbot.Api,
///     while the rest of /api/chatbot stays behind the key.
/// </summary>
[TestFixture]
[Category("Integration")]
public class ChatbotPageMountProductionTests
{
    private WebApplicationFactory<Program>? _factory;
    private HttpClient? _client;

    [OneTimeSetUp]
    public void OneTimeSetUp()
    {
        var intake = new Mock<IChatIntake>();
        intake.Setup(service => service.IntakeAsync(It.IsAny<ChatIntakeRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<ChatResponse, ChatIntakeError>.Success(new ChatResponse("A major triad.", [])));
        var provider = new Mock<IChatService>();
        provider.Setup(service => service.IsAvailableAsync(It.IsAny<CancellationToken>())).ReturnsAsync(true);
        _factory = new TestWebApplicationFactory().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Production");
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IChatIntake>();
                services.AddSingleton(intake.Object);
                services.RemoveAll<IChatService>();
                services.AddSingleton(provider.Object);
            });
        });
        _client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = false });
    }

    [OneTimeTearDown]
    public void OneTimeTearDown()
    {
        _client?.Dispose();
        _factory?.Dispose();
    }

    [TestCase("/chatbot/")]
    [TestCase("/chatbot/vendor/vexflow/vexflow.js")]
    [TestCase("/chatbot/api/chatbot/status")]
    [TestCase("/chatbot/api/chatbot/examples")]
    [TestCase("/chatbot/api/chatbot/demo")]
    [TestCase("/chatbot/api/chatbot/qa-summary")]
    public async Task ThePagesGets_PassWithoutAKey(string path)
    {
        using var response = await _client!.GetAsync(path);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
    }

    [Test]
    public async Task ThePagesChatStream_PassesWithoutAKey()
    {
        using var response = await _client!.PostAsJsonAsync("/chatbot/api/chatbot/chat/stream", new { message = "What is a major triad?" });
        var body = await response.Content.ReadAsStringAsync();

        Assert.Multiple(() =>
        {
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            Assert.That(body, Does.Contain("data: A major triad.").And.Contain("data: [DONE]"));
        });
    }

    [TestCase("POST", "/chatbot/api/chatbot/chat")]
    [TestCase("POST", "/api/chatbot/chat")]
    [TestCase("POST", "/chatbot/api/chatbot/agui/json")]
    [TestCase("GET", "/api/chatbot/skills")]
    public async Task OtherChatbotRoutes_StillRequireAKey(string method, string path)
    {
        using var request = new HttpRequestMessage(new HttpMethod(method), path)
        {
            Content = method == "POST" ? JsonContent.Create(new { message = "What is a major triad?" }) : null,
        };
        using var response = await _client!.SendAsync(request);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
    }
}

/// <summary>
///     The page polls status after every message, and the CI Playwright job probes it with a 2 s
///     timeout to decide GaApi is up. An unreachable provider must not hold the answer.
/// </summary>
[TestFixture]
[Category("Integration")]
public class ChatbotStatusBoundTests
{
    [Test]
    public async Task Status_WhenTheProviderHangs_AnswersUnavailableWithinTwoSeconds()
    {
        var provider = new Mock<IChatService>();
        provider.Setup(service => service.IsAvailableAsync(It.IsAny<CancellationToken>()))
            .Returns<CancellationToken>(async ct =>
            {
                await Task.Delay(Timeout.Infinite, ct);
                return true;
            });
        using var factory = new TestWebApplicationFactory().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Production");
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IChatService>();
                services.AddSingleton(provider.Object);
            });
        });
        using var client = factory.CreateClient();

        var clock = System.Diagnostics.Stopwatch.StartNew();
        using var response = await client.GetAsync("/chatbot/api/chatbot/status");
        clock.Stop();
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Multiple(() =>
        {
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            Assert.That(body.GetProperty("isAvailable").GetBoolean(), Is.False);
            Assert.That(clock.Elapsed, Is.LessThan(TimeSpan.FromSeconds(2)));
        });
    }
}
