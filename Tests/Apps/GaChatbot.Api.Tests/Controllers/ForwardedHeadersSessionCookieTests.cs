namespace GaChatbot.Api.Tests.Controllers;

using System.Net.Http.Json;
using GaChatbot.Api.Services;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

/// <summary>
/// Pins the <c>Secure</c> attribute of the chat session cookie under the
/// deployed proxy topology.
/// </summary>
/// <remarks>
/// <para>
/// <c>HttpChatSessionCookie</c> sets <c>Secure = ctx.Request.IsHttps</c>, and
/// the deployment terminates TLS at a cloudflared tunnel that forwards to
/// <c>http://localhost:5252</c> (<c>docs/runbooks/chatbot-deploy.md</c>).
/// Without <c>UseForwardedHeaders</c>, <c>IsHttps</c> is false on EVERY
/// production request, so the public cookie on
/// <c>https://demos.guitaralchemist.com</c> ships without <c>Secure</c> —
/// the gap the independent standards review filed as F-1.
/// </para>
/// <para>
/// <c>GetOrIssue_PlainHttp_DoesNotMarkCookieSecure</c> pins the dev-convenience
/// branch; this fixture pins the deployed one, and the spoof test below pins
/// the guard that keeps them apart.
/// </para>
/// <para>
/// The fixture deliberately supplies no <c>Proxy:PublicHost</c> of its own: it
/// reads the value the host ships in <c>Apps/GaChatbot.Api/appsettings.json</c>
/// (<c>TestWebApplicationFactory</c> points the content root at the app source
/// directory). Overriding it here would prove the middleware works when handed
/// a public host while leaving the deployment free to stop supplying one.
/// </para>
/// </remarks>
[TestFixture]
public class ForwardedHeadersSessionCookieTests
{
    [Test]
    public async Task ChatBehindTheTunnel_IssuesASecureSessionCookie()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();

        var response = await client.SendAsync(ChatRequest(cloudflare: true, forwardedProto: "https"));
        response.EnsureSuccessStatusCode();

        Assert.That(SessionSetCookie(response), Does.Contain("secure").IgnoreCase,
            "Behind the TLS-terminating tunnel the origin hop is plain HTTP, so the session " +
            "cookie is only Secure if X-Forwarded-Proto is honoured. This cookie keys private " +
            "conversational memory the moment Memory:EnrichOnRetrieve flips.");
    }

    [Test]
    public async Task ChatBehindTheTunnel_KeepsTheCookieHttpOnlyAndPathScoped()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();

        var response = await client.SendAsync(ChatRequest(cloudflare: true, forwardedProto: "https"));
        response.EnsureSuccessStatusCode();

        var setCookie = SessionSetCookie(response);
        Assert.Multiple(() =>
        {
            // Honouring the forwarded scheme must not disturb the rest of the
            // cookie shape — in particular the PathBase-aware Path.
            Assert.That(setCookie, Does.Contain("httponly").IgnoreCase);
            Assert.That(setCookie, Does.Contain("path=/api/chatbot").IgnoreCase);
        });
    }

    [Test]
    public async Task DirectRequestSpoofingForwardedProto_DoesNotGetASecureCookie()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();

        // No CF-Connecting-IP: a direct caller (or a dev proxy emitting a
        // partial forwarded-header set) must not be able to talk the host into
        // an https view of itself. Otherwise local http development gets a
        // Secure cookie the browser then refuses to send back, silently
        // rotating the session on every turn.
        var response = await client.SendAsync(ChatRequest(cloudflare: false, forwardedProto: "https"));
        response.EnsureSuccessStatusCode();

        Assert.That(SessionSetCookie(response), Does.Not.Contain("secure").IgnoreCase);
    }

    [Test]
    public void ShippedConfiguration_SuppliesTheProxyPublicHost()
    {
        using var factory = CreateFactory();

        var publicHost = factory.Services.GetRequiredService<IConfiguration>()["Proxy:PublicHost"];

        Assert.That(publicHost, Is.Not.Null.And.Not.Empty,
            "Apps/GaChatbot.Api/appsettings.json must ship Proxy:PublicHost. Without it the " +
            "forwarded-header guard takes the strip branch on every request — including tunnel " +
            "traffic — so the Program.cs fix is inert in production and the public session " +
            "cookie silently loses Secure again. See docs/runbooks/chatbot-deploy.md step 4.");
    }

    private static HttpRequestMessage ChatRequest(bool cloudflare, string forwardedProto)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/chatbot/chat")
        {
            Content = JsonContent.Create(new { message = "Notes in C major?" })
        };
        request.Headers.TryAddWithoutValidation("X-Forwarded-Proto", forwardedProto);
        if (cloudflare) request.Headers.TryAddWithoutValidation("CF-Connecting-IP", "203.0.113.7");
        return request;
    }

    private static string SessionSetCookie(HttpResponseMessage response)
    {
        Assert.That(response.Headers.TryGetValues("Set-Cookie", out var values), Is.True,
            "Expected the chat endpoint to issue a session cookie.");
        var cookie = values!.FirstOrDefault(v =>
            v.StartsWith(HttpChatSessionCookie.CookieName + "=", StringComparison.Ordinal));
        Assert.That(cookie, Is.Not.Null,
            $"Expected a {HttpChatSessionCookie.CookieName} Set-Cookie header.");
        return cookie!;
    }

    private static WebApplicationFactory<Program> CreateFactory() =>
        new TestWebApplicationFactory()
            .WithWebHostBuilder(builder =>
            {
                // No Proxy:PublicHost override on purpose — see the class remarks.
                // The shipped appsettings.json value is what mirrors the deployed
                // cloudflared ingress (docs/runbooks/chatbot-deploy.md:24), and it
                // is what these tests must depend on.
                builder.ConfigureTestServices(services =>
                {
                    // The chat provider is irrelevant here — only the cookie the
                    // transport emits is under test.
                    services.RemoveAll<IChatApplicationService>();
                    services.AddSingleton<IChatApplicationService, StubChatApplicationService>();
                });
            });

    private sealed class StubChatApplicationService : IChatApplicationService
    {
        public Task<ChatExecutionResult> ChatAsync(
            ChatExecutionRequest request,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new ChatExecutionResult(
                "stub answer",
                new GA.Business.Core.Orchestration.Models.AgentRoutingMetadata("fake-agent", 0.75f, "fake-route")));

        public async IAsyncEnumerable<ChatStreamUpdate> ChatStreamAsync(
            ChatExecutionRequest request,
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            yield return new ChatStreamUpdate("stub answer");
            await Task.Yield();
            yield return new ChatStreamUpdate(IsCompleted: true);
        }

        public Task<GaChatbot.Api.Controllers.ChatbotStatus> GetStatusAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(new GaChatbot.Api.Controllers.ChatbotStatus
            {
                IsAvailable = true,
                Message = "stub ready",
                Timestamp = DateTime.UtcNow
            });
    }
}
