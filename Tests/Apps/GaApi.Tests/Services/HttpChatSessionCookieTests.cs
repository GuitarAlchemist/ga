namespace GaApi.Tests.Services;

using GaApi.Services;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Tests for the Phase C HTTP session cookie that mirrors SignalR's
/// per-connection isolation onto the HTTP transport. See task #103
/// and PR #160 review SD-001 for the original gap. PR #163 review
/// VULN-001 prompted the IDataProtection wrap that closes session
/// fixation — these tests pin that fix.
/// </summary>
[TestFixture]
public class HttpChatSessionCookieTests
{
    private static HttpContext NewHttpContext(bool https = true, string? applicationName = null)
    {
        var services = new ServiceCollection();
        // Use ephemeral data protection so each test fixture has its own
        // signing key. SetApplicationName forces key isolation per test
        // (otherwise multiple test contexts share the same machine-key
        // derivation path and Unprotect succeeds across what should be
        // isolated key rings — which would defeat the cross-key-ring
        // negative test).
        services.AddDataProtection()
            .SetApplicationName(applicationName ?? $"GaApiTest-{Guid.NewGuid():N}");
        var sp = services.BuildServiceProvider();

        var ctx = new DefaultHttpContext
        {
            RequestServices = sp,
        };
        ctx.Request.Scheme = https ? "https" : "http";
        return ctx;
    }

    [Test]
    public void GetOrIssue_FirstRequest_IssuesProtectedCookie()
    {
        var ctx = NewHttpContext(https: true);

        var sessionId = HttpChatSessionCookie.GetOrIssue(ctx);

        Assert.That(sessionId, Is.Not.Null.And.Not.Empty);
        Assert.That(sessionId.Length, Is.GreaterThanOrEqualTo(16),
            "Returned inner ID should be ≥ 16 chars (128-bit base64url-encoded).");

        var setCookieHeader = ctx.Response.Headers["Set-Cookie"].ToString();
        Assert.That(setCookieHeader, Does.Contain(HttpChatSessionCookie.CookieName));
        Assert.That(setCookieHeader, Does.Not.Contain(sessionId),
            "The PROTECTED cookie value MUST NOT be the same as the inner SessionId — " +
            "VULN-001 fix (PR #163 audit): cookie carries DataProtection-signed ciphertext, " +
            "not the raw inner ID.");
        Assert.That(setCookieHeader, Does.Contain("httponly").IgnoreCase);
        Assert.That(setCookieHeader, Does.Contain("samesite=lax").IgnoreCase);
        Assert.That(setCookieHeader, Does.Contain("secure").IgnoreCase,
            "HTTPS request must produce a Secure cookie.");
        // TEST-001 from PR #163 audit: Path attribute pinned.
        Assert.That(setCookieHeader, Does.Contain("path=/api/chatbot").IgnoreCase);
        Assert.That(setCookieHeader, Does.Not.Contain("domain=").IgnoreCase,
            "Cookie must remain host-only — adding Domain= would leak to subdomains (VULN-002).");
        // TEST-002 from PR #163 audit: MaxAge=30 days exact.
        Assert.That(setCookieHeader, Does.Contain("max-age=2592000").IgnoreCase,
            "MaxAge must be 30 days (2592000 seconds).");
    }

    [Test]
    public void GetOrIssue_NonHttpsRequest_OmitsSecureFlag()
    {
        var ctx = NewHttpContext(https: false);

        HttpChatSessionCookie.GetOrIssue(ctx);

        var setCookieHeader = ctx.Response.Headers["Set-Cookie"].ToString();
        Assert.That(setCookieHeader, Does.Contain(HttpChatSessionCookie.CookieName));
        Assert.That(setCookieHeader, Does.Not.Contain("secure").IgnoreCase,
            "Plain HTTP must not set the Secure flag — cookie would otherwise be unset by the browser.");
    }

    [Test]
    public void GetOrIssue_ValidProtectedCookiePresent_ReturnsSameInnerValue()
    {
        // Round-trip: issue a cookie on request A, then create request B
        // sharing the same DataProtection key ring and presenting the
        // protected value. Should yield the same inner SessionId.
        var ctx1 = NewHttpContext();
        var firstId = HttpChatSessionCookie.GetOrIssue(ctx1);
        var protectedCookieValue = ExtractCookieValue(ctx1, HttpChatSessionCookie.CookieName);

        // Reuse the same DI container (same key ring) so Unprotect succeeds.
        var ctx2 = new DefaultHttpContext { RequestServices = ctx1.RequestServices };
        ctx2.Request.Scheme = "https";
        ctx2.Request.Headers["Cookie"] = $"{HttpChatSessionCookie.CookieName}={protectedCookieValue}";

        var secondId = HttpChatSessionCookie.GetOrIssue(ctx2);

        Assert.That(secondId, Is.EqualTo(firstId),
            "Round-trip: Unprotect(Protect(id)) must return id verbatim.");
        Assert.That(ctx2.Response.Headers["Set-Cookie"].ToString(), Is.Empty,
            "When a valid protected cookie is present, no new Set-Cookie should be issued.");
    }

    [Test]
    public void GetOrIssue_AttackerChosenRawValue_RejectedByDataProtection()
    {
        // VULN-001 — session fixation. An attacker presents a 22-char
        // base64url-shaped value that DID NOT come from this server. Even
        // though it passes shape-validation, DataProtection's Unprotect
        // throws because the value lacks a valid MAC. Handler issues a
        // fresh cookie instead of accepting the attacker's choice.
        var ctx = NewHttpContext();
        var attackerValue = "AAAAAAAAAAAAAAAAAAAAAA"; // 22 'A's — shape-valid but unsigned

        ctx.Request.Headers["Cookie"] = $"{HttpChatSessionCookie.CookieName}={attackerValue}";

        var sessionId = HttpChatSessionCookie.GetOrIssue(ctx);

        Assert.That(sessionId, Is.Not.EqualTo(attackerValue),
            "Unsigned attacker-chosen value MUST be rejected by Unprotect — VULN-001 closure.");
        Assert.That(ctx.Response.Headers["Set-Cookie"].ToString(),
            Does.Contain(HttpChatSessionCookie.CookieName),
            "Rejection path must issue a fresh server-signed cookie.");
    }

    [Test]
    public void GetOrIssue_TamperedProtectedCookie_RejectsAndReissues()
    {
        // Round-trip: issue, then tamper one character before re-presenting.
        var ctx1 = NewHttpContext();
        HttpChatSessionCookie.GetOrIssue(ctx1);
        var protectedValue = ExtractCookieValue(ctx1, HttpChatSessionCookie.CookieName);

        // Flip the last character — breaks the MAC.
        var tampered = protectedValue[..^1] + (protectedValue[^1] == 'A' ? 'B' : 'A');

        var ctx2 = new DefaultHttpContext { RequestServices = ctx1.RequestServices };
        ctx2.Request.Scheme = "https";
        ctx2.Request.Headers["Cookie"] = $"{HttpChatSessionCookie.CookieName}={tampered}";

        var sessionId = HttpChatSessionCookie.GetOrIssue(ctx2);

        Assert.That(ctx2.Response.Headers["Set-Cookie"].ToString(),
            Does.Contain(HttpChatSessionCookie.CookieName),
            "Tampered cookie must trigger reissue, not silent acceptance.");
        Assert.That(sessionId.Length, Is.GreaterThanOrEqualTo(16));
    }

    [TestCase("")]
    [TestCase("x")]                             // too short
    [TestCase("malformed!@#$%^&*()")]            // not base64url + DataProtection signature
    public void GetOrIssue_InvalidCookieValue_IssuesFreshOne(string badValue)
    {
        var ctx = NewHttpContext();
        ctx.Request.Headers["Cookie"] = $"{HttpChatSessionCookie.CookieName}={badValue}";

        var sessionId = HttpChatSessionCookie.GetOrIssue(ctx);

        Assert.That(sessionId, Is.Not.EqualTo(badValue));
        Assert.That(ctx.Response.Headers["Set-Cookie"].ToString(),
            Does.Contain(HttpChatSessionCookie.CookieName));
    }

    [Test]
    public void GetOrIssue_TwoFreshContexts_IssueDifferentInnerIds()
    {
        var ctx1 = NewHttpContext();
        var ctx2 = NewHttpContext();

        var id1 = HttpChatSessionCookie.GetOrIssue(ctx1);
        var id2 = HttpChatSessionCookie.GetOrIssue(ctx2);

        Assert.That(id1, Is.Not.EqualTo(id2),
            "Two independent requests must get distinct inner IDs (128-bit CSPRNG entropy).");
    }

    [Test]
    public void GetOrIssue_CrossKeyRing_RejectsForgedSignature()
    {
        // A cookie signed by a DIFFERENT key ring is unforgeable from this
        // server's perspective. Simulates an attacker who somehow obtained
        // a valid-shaped DataProtection payload from elsewhere.
        var ctx1 = NewHttpContext();
        HttpChatSessionCookie.GetOrIssue(ctx1);
        var foreignCookieValue = ExtractCookieValue(ctx1, HttpChatSessionCookie.CookieName);

        // ctx2 has its OWN ephemeral key ring — Unprotect will fail.
        var ctx2 = NewHttpContext();
        ctx2.Request.Headers["Cookie"] = $"{HttpChatSessionCookie.CookieName}={foreignCookieValue}";

        HttpChatSessionCookie.GetOrIssue(ctx2);

        Assert.That(ctx2.Response.Headers["Set-Cookie"].ToString(),
            Does.Contain(HttpChatSessionCookie.CookieName),
            "Cookie signed by a foreign key ring must trigger reissue — VULN-001 defense.");
    }

    // ─── Helpers ─────────────────────────────────────────────────────────

    private static string ExtractCookieValue(HttpContext ctx, string name)
    {
        // Set-Cookie header is "name=value; attr1; attr2; ..." — pull the value.
        var raw = ctx.Response.Headers["Set-Cookie"].ToString();
        var prefix = $"{name}=";
        var start = raw.IndexOf(prefix, StringComparison.Ordinal);
        if (start < 0) return string.Empty;
        start += prefix.Length;
        var end = raw.IndexOf(';', start);
        return end < 0 ? raw[start..] : raw[start..end];
    }
}
