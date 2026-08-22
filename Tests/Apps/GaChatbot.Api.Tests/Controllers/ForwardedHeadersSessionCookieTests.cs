namespace GaChatbot.Api.Tests.Controllers;

using System.ComponentModel;
using System.Diagnostics;
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
/// The fixture deliberately supplies no <c>Proxy:PublicHost</c> of its own, so
/// the host under test resolves whatever configuration reaches it. Overriding
/// it here would prove the middleware works when handed a public host while
/// leaving the deployment free to stop supplying one.
/// </para>
/// <para>
/// What reaches the host is NOT
/// <c>Apps/GaChatbot.Api/appsettings.json</c> read live: <c>Program.cs</c> pins
/// <c>ContentRootPath</c> to <c>AppContext.BaseDirectory</c>, which overrides
/// <c>TestWebApplicationFactory</c>'s <c>UseContentRoot</c>, so the host reads
/// the copy MSBuild refreshes into the TEST project's output on every build —
/// and an ambient <c>Proxy__PublicHost</c> environment variable still outranks
/// that copy. <c>ShippedConfiguration_SuppliesTheProxyPublicHost</c> therefore
/// reads the shipped file straight off disk instead: that keeps it meaningful
/// when the run skips the build, and stops the shell it runs in from reddening
/// it while the shipped file is intact.
/// </para>
/// <para>
/// <c>ShippedRunbook_CookieAssertion_TestsTheSessionCookieNotTheWholeHeaderBlock</c>
/// extends that same "guard the shipped artefact" idea to the operator-side
/// check. Nothing in this process runs
/// <c>docs/runbooks/chatbot-deploy.md</c>, so its step-6 assertion — the only
/// check that can observe the PUBLIC cookie losing <c>Secure</c> — had no
/// oracle at all, and the response it inspects can carry several
/// <c>Set-Cookie</c> headers of which only one is the session cookie.
/// </para>
/// </remarks>
[TestFixture]
public class ForwardedHeadersSessionCookieTests
{
    /// <summary>Name of the step-6 assertion this fixture lifts out of the runbook.</summary>
    private const string RunbookAssertionName = "Assert-GaChatSessionSecure";

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
        // Read the shipped file itself rather than the host's resolved
        // configuration — see the class remarks for why the host is the wrong
        // oracle for a claim about what the deployment ships.
        var shipped = new ConfigurationBuilder()
            .AddJsonFile(TestPaths.RepositoryPath("Apps", "GaChatbot.Api", "appsettings.json"))
            .Build();

        var publicHost = shipped["Proxy:PublicHost"];

        // Program.cs:73 and :91 both branch on IsNullOrWhiteSpace, so a
        // whitespace-only value is as inert as a missing one.
        Assert.That(string.IsNullOrWhiteSpace(publicHost), Is.False,
            "Apps/GaChatbot.Api/appsettings.json must ship a non-blank Proxy:PublicHost. Without it the " +
            "forwarded-header guard takes the strip branch on every request — including tunnel " +
            "traffic — so the Program.cs fix is inert in production and the public session " +
            "cookie silently loses Secure again. See docs/runbooks/chatbot-deploy.md step 4.");
    }

    [Test]
    public void ShippedRunbook_CookieAssertion_TestsTheSessionCookieNotTheWholeHeaderBlock()
    {
        var assertion = ShippedRunbookCookieAssertion();

        // The cookies that are NOT the session cookie are the likelier ones to
        // be Secure — Cloudflare issues __cf_bm / cf_clearance Secure whenever
        // the zone emits them. An assertion that joins the headers and matches
        // /secure/ therefore reports success on this exact pair while the
        // session cookie, the only one that matters here, ships bare.
        const string cloudflareDecoy =
            "__cf_bm=Jd8n3o.decoy; path=/; domain=.guitaralchemist.com; HttpOnly; Secure; SameSite=None";
        const string bareSession =
            "ga_chat_session=6f1c9d2e-4a70-4f5b-9d21-0b2c3e4f5a61; path=/api/chatbot; samesite=lax; httponly";
        const string secureSession = bareSession + "; secure";

        var bare = RunShippedRunbookAssertion(assertion, cloudflareDecoy, bareSession);
        var secure = RunShippedRunbookAssertion(assertion, cloudflareDecoy, secureSession);

        Assert.Multiple(() =>
        {
            Assert.That(bare.ExitCode, Is.Not.Zero,
                "docs/runbooks/chatbot-deploy.md step 6 accepted a response whose ga_chat_session cookie " +
                "carries no Secure attribute, because another cookie in the same response does. Step 6 " +
                "must select the ga_chat_session Set-Cookie header before asserting Secure on it — " +
                "otherwise a deploy that silently dropped Secure passes its own regression check.");
            Assert.That(bare.Stderr, Does.Contain("lacks Secure"),
                "Step 6 must name the failure it found; the operator acts on that message.");
            Assert.That(secure.ExitCode, Is.Zero,
                "Step 6 rejected a genuinely Secure session cookie, which would block a healthy deploy. " +
                $"stderr: {secure.Stderr}");
        });
    }

    /// <summary>
    /// Lifts the step-6 cookie assertion out of the shipped runbook so it can be
    /// executed rather than merely read.
    /// </summary>
    private static string ShippedRunbookCookieAssertion()
    {
        var runbook = TestPaths.RepositoryPath("docs", "runbooks", "chatbot-deploy.md");
        var lines = File.ReadAllLines(runbook);

        var start = Array.FindIndex(
            lines,
            line => line.StartsWith($"function {RunbookAssertionName}", StringComparison.Ordinal));
        Assert.That(start, Is.GreaterThanOrEqualTo(0),
            $"{runbook} no longer defines a column-0 '{RunbookAssertionName}' function. Step 6's cookie " +
            "assertion is the only check that can see the public session cookie lose Secure, so it stays " +
            "in a named function this guard can execute; inlining it back into the procedure removes the " +
            "only oracle the runbook has.");

        // The body's braces are balanced and none appear inside its strings or
        // regexes; an unbalanced edit falls through to the failure below.
        var depth = 0;
        for (var i = start; i < lines.Length; i++)
        {
            depth += lines[i].Count(c => c == '{') - lines[i].Count(c => c == '}');
            if (depth == 0) return string.Join(Environment.NewLine, lines[start..(i + 1)]);
        }

        Assert.Fail($"{RunbookAssertionName} in {runbook} is never closed.");
        return string.Empty;
    }

    private static (int ExitCode, string Stderr) RunShippedRunbookAssertion(
        string assertion,
        params string[] setCookieHeaders)
    {
        var headers = string.Join(", ", setCookieHeaders.Select(h => $"'{h.Replace("'", "''")}'"));
        var script = Path.Combine(Path.GetTempPath(), $"ga-runbook-cookie-{Guid.NewGuid():N}.ps1");
        File.WriteAllText(script, string.Join(Environment.NewLine,
            assertion,
            $"{RunbookAssertionName} -SetCookieHeaders @({headers})"));

        try
        {
            using var pwsh = Process.Start(new ProcessStartInfo("pwsh")
            {
                ArgumentList = { "-NoProfile", "-NonInteractive", "-File", script },
                RedirectStandardOutput = true,
                RedirectStandardError = true
            }) ?? throw new InvalidOperationException("pwsh produced no process handle.");

            var stdout = pwsh.StandardOutput.ReadToEndAsync();
            var stderr = pwsh.StandardError.ReadToEnd();
            pwsh.WaitForExit();
            stdout.GetAwaiter().GetResult();
            return (pwsh.ExitCode, stderr);
        }
        catch (Win32Exception e)
        {
            // A skip here would make the guard vacuous on exactly the machines
            // that run the deploy, so this fails instead. pwsh is already a hard
            // dependency of the repo (Scripts/*.ps1, .githooks/pre-commit).
            Assert.Fail($"pwsh could not be launched, so the shipped runbook assertion was never executed: {e.Message}");
            return (0, string.Empty);
        }
        finally
        {
            File.Delete(script);
        }
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
                // The deployed value must reach the host through the same
                // configuration path production uses (docs/runbooks/chatbot-deploy.md:24);
                // a test setting here would keep these tests green after the
                // deployment stopped supplying one.
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
