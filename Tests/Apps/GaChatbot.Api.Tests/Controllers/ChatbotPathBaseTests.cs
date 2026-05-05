namespace GaChatbot.Api.Tests.Controllers;

using System.Net;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

/// <summary>
/// Pins the contract for hosting GaChatbot.Api under a path-base mount
/// (e.g. <c>https://demos.guitaralchemist.com/chatbot</c>). Cloudflare
/// Tunnel ingress forwards <c>/chatbot/*</c> verbatim to
/// <c>localhost:5252</c>; <c>app.UsePathBase("/chatbot")</c> in
/// <c>Program.cs</c> strips the prefix on the way in so controllers and
/// the static-file middleware see clean paths.
/// </summary>
/// <remarks>
/// Regression guards for two related production bugs:
/// (1) the inline chatbot HTML originally used absolute URLs
///     (<c>/api/chatbot/status</c>, <c>/vendor/vexflow/vexflow.js</c>),
///     so even with the tunnel + path-base mount in place, browser-side
///     requests bypassed the prefix and 404'd; and
/// (2) <c>wwwroot/vendor/vexflow/vexflow.js</c> wasn't shipping to
///     <c>bin/</c> because <c>Program.cs</c> overrides
///     <c>ContentRootPath = AppContext.BaseDirectory</c> and the Web
///     SDK's implicit wwwroot items default to
///     <c>CopyToOutputDirectory=Never</c>. Fixed by an explicit
///     <c>&lt;Content Update="wwwroot\**\*"&gt;</c> in the csproj.
/// Both modes (direct localhost root and prefixed mount) must coexist
/// so dev workflow doesn't change.
/// </remarks>
[TestFixture]
public class ChatbotPathBaseTests
{
    private const string PathBase = "/chatbot";

    [Test]
    public async Task Root_UnderPathBase_ReturnsChatbotHtml()
    {
        using var factory = CreatePathBaseFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync(PathBase + "/");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Assert.That(response.Content.Headers.ContentType?.MediaType, Is.EqualTo("text/html"));
        var html = await response.Content.ReadAsStringAsync();
        Assert.That(html, Does.Contain("id=\"chatForm\""));
    }

    [Test]
    public async Task Root_DirectRoot_StillReturnsChatbotHtml_WhenPathBaseIsConfigured()
    {
        // UsePathBase only ACTIVATES when the request path starts with the
        // base. A direct request to "/" still flows through the pipeline
        // unchanged so localhost dev usage isn't broken by configuring the
        // production path-base.
        using var factory = CreatePathBaseFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Assert.That(response.Content.Headers.ContentType?.MediaType, Is.EqualTo("text/html"));
    }

    [Test]
    public async Task ApiStatus_UnderPathBase_RoutesToController()
    {
        using var factory = CreatePathBaseFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync(PathBase + "/api/chatbot/status");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Assert.That(response.Content.Headers.ContentType?.MediaType, Is.EqualTo("application/json"));
    }

    [Test]
    public async Task ApiStatus_DirectRoot_StillRoutesToController_WhenPathBaseIsConfigured()
    {
        using var factory = CreatePathBaseFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/chatbot/status");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
    }

    [Test]
    public async Task VexFlowAsset_UnderPathBase_ServesFromStaticFiles()
    {
        // The bug we shipped: wwwroot/vendor/vexflow/vexflow.js wasn't
        // being copied to bin/, so static-files middleware 404'd at
        // /chatbot/vendor/vexflow/vexflow.js and the chatbot UI rendered
        // "VexFlow is not loaded" under every chord. Pins both the
        // copy-to-output csproj rule AND the path-base resolution.
        using var factory = CreatePathBaseFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync(PathBase + "/vendor/vexflow/vexflow.js");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Assert.That(response.Content.Headers.ContentType?.MediaType, Is.EqualTo("text/javascript"));

        var body = await response.Content.ReadAsStringAsync();
        Assert.That(body, Does.Contain("TabStave"),
            "must be the real VexFlow bundle, not an HTML fallback page");
    }

    [Test]
    public async Task VexFlowAsset_DirectRoot_StillServesFromStaticFiles()
    {
        using var factory = CreatePathBaseFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/vendor/vexflow/vexflow.js");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
    }

    [Test]
    public async Task UnknownPathInsideBase_ReturnsNotFound_NotChatbotHtml()
    {
        using var factory = CreatePathBaseFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync(PathBase + "/this-route-does-not-exist");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
    }

    private static WebApplicationFactory<Program> CreatePathBaseFactory() =>
        new TestWebApplicationFactory()
            .WithWebHostBuilder(builder =>
            {
                builder.UseSetting("Chatbot:PathBase", PathBase);
            });
}
