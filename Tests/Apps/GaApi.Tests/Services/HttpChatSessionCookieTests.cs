namespace GaApi.Tests.Services;

using GaApi.Services;
using Microsoft.AspNetCore.Http;

/// <summary>
/// Tests for the Phase C HTTP session cookie that mirrors SignalR's
/// per-connection isolation onto the HTTP transport. See task #103
/// and PR #160 review SD-001 for the original gap.
/// </summary>
[TestFixture]
public class HttpChatSessionCookieTests
{
    [Test]
    public void GetOrIssue_FirstRequest_IssuesNewCookie()
    {
        var ctx = new DefaultHttpContext();
        // Default scheme is http in DefaultHttpContext — explicit set is for clarity.
        ctx.Request.Scheme = "https";

        var sessionId = HttpChatSessionCookie.GetOrIssue(ctx);

        Assert.That(sessionId, Is.Not.Null.And.Not.Empty);
        Assert.That(sessionId.Length, Is.GreaterThanOrEqualTo(16),
            "128-bit value base64url-encoded should be at least 16 chars.");

        // The Set-Cookie header on the response should reference our cookie name.
        var setCookieHeader = ctx.Response.Headers["Set-Cookie"].ToString();
        Assert.That(setCookieHeader, Does.Contain(HttpChatSessionCookie.CookieName));
        Assert.That(setCookieHeader, Does.Contain(sessionId),
            "Set-Cookie should carry the issued session ID verbatim.");
        Assert.That(setCookieHeader, Does.Contain("httponly").IgnoreCase);
        Assert.That(setCookieHeader, Does.Contain("samesite=lax").IgnoreCase);
        Assert.That(setCookieHeader, Does.Contain("secure").IgnoreCase,
            "HTTPS request must produce a Secure cookie.");
    }

    [Test]
    public void GetOrIssue_NonHttpsRequest_OmitsSecureFlag()
    {
        var ctx = new DefaultHttpContext();
        ctx.Request.Scheme = "http";

        HttpChatSessionCookie.GetOrIssue(ctx);

        var setCookieHeader = ctx.Response.Headers["Set-Cookie"].ToString();
        Assert.That(setCookieHeader, Does.Contain(HttpChatSessionCookie.CookieName));
        Assert.That(setCookieHeader, Does.Not.Contain("secure").IgnoreCase,
            "Plain HTTP must not set the Secure flag — cookie would otherwise be unset by the browser.");
    }

    [Test]
    public void GetOrIssue_ValidCookiePresent_ReturnsSameValue()
    {
        var existing = "abcdef0123456789ABCDEF";
        var ctx = new DefaultHttpContext();
        ctx.Request.Headers["Cookie"] = $"{HttpChatSessionCookie.CookieName}={existing}";

        var sessionId = HttpChatSessionCookie.GetOrIssue(ctx);

        Assert.That(sessionId, Is.EqualTo(existing));
        Assert.That(ctx.Response.Headers["Set-Cookie"].ToString(), Is.Empty,
            "When a valid cookie is present, no new Set-Cookie should be issued.");
    }

    [TestCase("")]
    [TestCase("x")]                         // too short (< 16)
    [TestCase("a")]                         // ditto
    [TestCase("malformed-cookie-with-illegal-chars!@#")]  // bad chars
    public void GetOrIssue_InvalidCookieValue_IssuesFreshOne(string badValue)
    {
        var ctx = new DefaultHttpContext();
        ctx.Request.Headers["Cookie"] = $"{HttpChatSessionCookie.CookieName}={badValue}";

        var sessionId = HttpChatSessionCookie.GetOrIssue(ctx);

        Assert.That(sessionId, Is.Not.EqualTo(badValue),
            "Malformed cookie values must be rejected; a fresh session is issued.");
        Assert.That(ctx.Response.Headers["Set-Cookie"].ToString(),
            Does.Contain(HttpChatSessionCookie.CookieName),
            "Fresh cookie must be set on response when rejecting a malformed inbound cookie.");
    }

    [Test]
    public void GetOrIssue_TwoCallsWithoutCookie_IssuesDifferentValues()
    {
        // Server-issued entropy: each fresh request gets a distinct 128-bit
        // ID so two anonymous users land in distinct memory partitions.
        var ctx1 = new DefaultHttpContext();
        var ctx2 = new DefaultHttpContext();

        var id1 = HttpChatSessionCookie.GetOrIssue(ctx1);
        var id2 = HttpChatSessionCookie.GetOrIssue(ctx2);

        Assert.That(id1, Is.Not.EqualTo(id2),
            "Two independent requests must get distinct session IDs (random entropy from RandomNumberGenerator).");
    }
}
