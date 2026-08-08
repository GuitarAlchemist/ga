namespace GaChatbot.Api.Tests.Services;

using GaChatbot.Api.Services;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Pins the session-identity contract for the canonical public chatbot host's
/// HTTP transport: a browser that keeps its cookie keeps its session, a
/// browser without one gets a fresh session, and a forged cookie cannot
/// pin the caller onto a session of their choosing.
/// </summary>
[TestFixture]
public class HttpChatSessionCookieTests
{
    private static HttpContext NewHttpContext(
        bool https = true,
        string? applicationName = null,
        string pathBase = "")
    {
        var services = new ServiceCollection();
        // Ephemeral, per-context key ring. SetApplicationName forces isolation
        // between contexts so the cross-key-ring negative test is meaningful.
        services.AddDataProtection()
            .SetApplicationName(applicationName ?? $"GaChatbotApiTest-{Guid.NewGuid():N}");

        var ctx = new DefaultHttpContext { RequestServices = services.BuildServiceProvider() };
        ctx.Request.Scheme = https ? "https" : "http";
        ctx.Request.PathBase = pathBase;
        return ctx;
    }

    private static string IssuedCookieValue(HttpContext ctx)
    {
        var setCookie = ctx.Response.Headers["Set-Cookie"].ToString();
        var start = setCookie.IndexOf(HttpChatSessionCookie.CookieName + "=", StringComparison.Ordinal);
        Assert.That(start, Is.GreaterThanOrEqualTo(0), "Expected a Set-Cookie header for the chat session.");
        var valueStart = start + HttpChatSessionCookie.CookieName.Length + 1;
        var end = setCookie.IndexOf(';', valueStart);
        return end < 0 ? setCookie[valueStart..] : setCookie[valueStart..end];
    }

    [Test]
    public void GetOrIssue_FirstRequest_IssuesProtectedCookieAndReturnsInnerId()
    {
        var ctx = NewHttpContext();

        var sessionId = HttpChatSessionCookie.GetOrIssue(ctx);

        var setCookie = ctx.Response.Headers["Set-Cookie"].ToString();
        Assert.Multiple(() =>
        {
            Assert.That(sessionId, Is.Not.Null.And.Not.Empty);
            Assert.That(sessionId.Length, Is.GreaterThanOrEqualTo(16),
                "Inner ID should be >= 16 chars (128 bits, base64url-encoded).");
            Assert.That(setCookie, Does.Contain(HttpChatSessionCookie.CookieName));
            Assert.That(setCookie, Does.Not.Contain(sessionId),
                "The cookie must carry DataProtection ciphertext, not the raw inner ID — " +
                "otherwise a caller can read and replay another session's identifier.");
            Assert.That(setCookie, Does.Contain("httponly").IgnoreCase);
            Assert.That(setCookie, Does.Contain("samesite=lax").IgnoreCase);
            Assert.That(setCookie, Does.Contain("secure").IgnoreCase);
            Assert.That(setCookie, Does.Contain("path=/api/chatbot").IgnoreCase);
        });
    }

    [Test]
    public void GetOrIssue_PlainHttp_DoesNotMarkCookieSecure()
    {
        var ctx = NewHttpContext(https: false);

        HttpChatSessionCookie.GetOrIssue(ctx);

        Assert.That(ctx.Response.Headers["Set-Cookie"].ToString(), Does.Not.Contain("secure").IgnoreCase,
            "Secure on a plain-HTTP dev request would make the cookie undeliverable.");
    }

    [Test]
    public void GetOrIssue_ExistingValidCookie_ReturnsSameSessionAndDoesNotReissue()
    {
        const string appName = "GaChatbotApiTest-stable";
        var first = NewHttpContext(applicationName: appName);
        var firstId = HttpChatSessionCookie.GetOrIssue(first);
        var cookie = IssuedCookieValue(first);

        var second = NewHttpContext(applicationName: appName);
        second.Request.Headers.Cookie = $"{HttpChatSessionCookie.CookieName}={cookie}";

        var secondId = HttpChatSessionCookie.GetOrIssue(second);

        Assert.Multiple(() =>
        {
            Assert.That(secondId, Is.EqualTo(firstId),
                "A returning browser must land on the same session — that continuity is the " +
                "whole point of scoping chatbot memory per conversation.");
            Assert.That(second.Response.Headers["Set-Cookie"].ToString(), Is.Empty,
                "A valid cookie must not be reissued; reissuing would rotate the session.");
        });
    }

    [Test]
    public void GetOrIssue_TwoFreshBrowsers_GetDistinctSessions()
    {
        const string appName = "GaChatbotApiTest-distinct";

        var a = HttpChatSessionCookie.GetOrIssue(NewHttpContext(applicationName: appName));
        var b = HttpChatSessionCookie.GetOrIssue(NewHttpContext(applicationName: appName));

        Assert.That(a, Is.Not.EqualTo(b),
            "Two unrelated visitors must not share a memory partition.");
    }

    [Test]
    public void GetOrIssue_ForgedCookie_IsRejectedAndFreshSessionIssued()
    {
        var ctx = NewHttpContext();
        // Attacker-chosen value with a valid-looking inner shape. Shape-only
        // validation would accept it and pin the caller onto a chosen session.
        ctx.Request.Headers.Cookie = $"{HttpChatSessionCookie.CookieName}=AAAAAAAAAAAAAAAAAAAAAA";

        var sessionId = HttpChatSessionCookie.GetOrIssue(ctx);

        Assert.Multiple(() =>
        {
            Assert.That(sessionId, Is.Not.EqualTo("AAAAAAAAAAAAAAAAAAAAAA"));
            Assert.That(ctx.Response.Headers["Set-Cookie"].ToString(),
                Does.Contain(HttpChatSessionCookie.CookieName),
                "A rejected cookie must be replaced with a server-issued one.");
        });
    }

    [Test]
    public void GetOrIssue_CookieFromAnotherKeyRing_IsRejected()
    {
        var issuer = NewHttpContext(applicationName: "GaChatbotApiTest-ring-a");
        HttpChatSessionCookie.GetOrIssue(issuer);
        var foreignCookie = IssuedCookieValue(issuer);

        var ctx = NewHttpContext(applicationName: "GaChatbotApiTest-ring-b");
        ctx.Request.Headers.Cookie = $"{HttpChatSessionCookie.CookieName}={foreignCookie}";

        HttpChatSessionCookie.GetOrIssue(ctx);

        Assert.That(ctx.Response.Headers["Set-Cookie"].ToString(),
            Does.Contain(HttpChatSessionCookie.CookieName),
            "A cookie signed by a different key ring must not Unprotect into a usable session.");
    }

    [Test]
    public void GetOrIssue_UnderPathBase_ScopesCookieToTheMountedPrefix()
    {
        // This host is mounted under /chatbot on demos.guitaralchemist.com
        // (Chatbot:PathBase — see ChatbotPathBaseTests). A cookie pinned to a
        // hardcoded /api/chatbot would never be sent back by the browser
        // there, silently rotating the session on every turn.
        var ctx = NewHttpContext(pathBase: "/chatbot");

        HttpChatSessionCookie.GetOrIssue(ctx);

        Assert.That(ctx.Response.Headers["Set-Cookie"].ToString(),
            Does.Contain("path=/chatbot/api/chatbot").IgnoreCase);
    }
}
