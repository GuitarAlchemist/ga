namespace GaChatbot.Api.Services;

using System.Security.Cryptography;
using Microsoft.AspNetCore.DataProtection;

/// <summary>
/// Server-issued HTTP session cookie for the canonical public chatbot host.
/// Gives the REST/SSE transport the same per-conversation isolation that
/// SignalR's <c>Context.ConnectionId</c> gives GaApi's WebSocket transport,
/// and that <c>GaApi.Services.HttpChatSessionCookie</c> gives GaApi's HTTP
/// transport.
/// </summary>
/// <remarks>
/// <para>
/// <b>Why this exists here and not in a shared library:</b> cookie issuance
/// is a transport concern, and the five-layer rule keeps transport in
/// <c>Apps/</c>. Hoisting it into <c>GA.Business.Core.Orchestration</c>
/// (the only library both hosts reference) would drag
/// <c>Microsoft.AspNetCore.Http</c> into an orchestration-layer assembly.
/// Each ASP.NET host therefore owns its own issuance; what the hosts share
/// is the <i>opaque SessionId string</i> on
/// <see cref="GA.Business.Core.Orchestration.Models.ChatRequest.SessionId"/>,
/// which is already a layer-appropriate contract. Consolidation trigger: a
/// third host needing this — then extract a shared web-primitives project
/// rather than growing a third copy.
/// </para>
/// <para>
/// <b>Why server-issued, not client-supplied:</b> a client-controlled session
/// identifier is a forged-session-cross-pollution vector once
/// <c>Memory:EnrichOnRetrieve=true</c> ships — a shape-only validator accepts
/// attacker-chosen values, enabling session fixation (GaApi PR #163 security
/// audit, VULN-001). The random session ID is therefore wrapped with
/// <see cref="IDataProtector"/> (HMAC + AES-CBC from the app's data-protection
/// key ring). The cookie carries the PROTECTED ciphertext; the inner ID is
/// what we hand back as the SessionId. Tampering fails <c>Unprotect</c> with a
/// <see cref="CryptographicException"/> and a fresh session is issued.
/// </para>
/// <para>
/// <b>Path scoping under a path-base mount:</b> this host can be mounted
/// under a prefix (<c>Chatbot:PathBase</c>, e.g.
/// <c>demos.guitaralchemist.com/chatbot</c>) — see
/// <c>ChatbotPathBaseTests</c>. The cookie Path is therefore
/// <c>{PathBase}/api/chatbot</c>, not a hardcoded <c>/api/chatbot</c>: a
/// hardcoded path would never be echoed back by the browser on the mounted
/// deployment, silently rotating the session on every single turn — exactly
/// the bug this class exists to fix.
/// </para>
/// <para>
/// <b><c>Secure</c> depends on forwarded headers:</b> the flag follows
/// <c>Request.IsHttps</c>, and the deployment terminates TLS at a cloudflared
/// tunnel that forwards to plain HTTP. <c>Program.cs</c> must therefore
/// register <c>UseForwardedHeaders</c> for this cookie to be <c>Secure</c> in
/// production — pinned by <c>ForwardedHeadersSessionCookieTests</c>.
/// </para>
/// <para>
/// <b>Threat model:</b> rotation across browser sessions (cookie cleared, new
/// incognito window) is by design — the same trade-off SignalR reconnect
/// rotation makes. The cookie is a CONVENIENCE that keeps memory continuity
/// across page reloads for an anonymous demo. Stable cross-device sessions
/// require authentication, which is out of scope here.
/// </para>
/// </remarks>
public static class HttpChatSessionCookie
{
    public const string CookieName = "ga_chat_session";

    // Purpose is host-specific on purpose: a cookie minted by GaApi must not
    // Unprotect here (and vice versa). The two hosts serve the same paths on
    // different ports, so a shared purpose would let one host's session ID
    // silently become the other's.
    private const string ProtectorPurpose = "GaChatbot.Api.HttpChatSessionCookie.v1";

    private const string CookiePathSuffix = "/api/chatbot";

    private static readonly TimeSpan CookieLifetime = TimeSpan.FromDays(30);

    /// <summary>
    /// Returns the chat session ID for this HTTP request. If a valid
    /// DataProtection-signed <c>ga_chat_session</c> cookie is present, its
    /// inner value is returned. Otherwise a fresh 128-bit random ID is
    /// generated, protected, and set on the outgoing response as a cookie.
    /// </summary>
    /// <remarks>
    /// MUST be called before <c>Response.StartAsync()</c> on streaming
    /// endpoints: appending a cookie mutates response headers, and headers
    /// become read-only once the response is committed to the wire.
    /// </remarks>
    public static string GetOrIssue(HttpContext ctx)
    {
        var protector = ctx.RequestServices
            .GetRequiredService<IDataProtectionProvider>()
            .CreateProtector(ProtectorPurpose);

        if (ctx.Request.Cookies.TryGetValue(CookieName, out var protectedValue)
            && !string.IsNullOrEmpty(protectedValue))
        {
            try
            {
                var existing = protector.Unprotect(protectedValue);
                if (IsValidInnerShape(existing)) return existing;
                // Inner shape is unexpected (only reachable on schema drift) —
                // fall through and reissue.
            }
            catch (CryptographicException)
            {
                // Tampered, forged, or signed with a now-rotated key — reissue.
            }
        }

        var newId = Convert.ToBase64String(RandomNumberGenerator.GetBytes(16))
            .Replace('+', '-')
            .Replace('/', '_')
            .TrimEnd('=');

        ctx.Response.Cookies.Append(CookieName, protector.Protect(newId), new CookieOptions
        {
            HttpOnly = true,
            Secure   = ctx.Request.IsHttps,
            SameSite = SameSiteMode.Lax,
            MaxAge   = CookieLifetime,
            Path     = ctx.Request.PathBase.HasValue
                ? ctx.Request.PathBase.Value + CookiePathSuffix
                : CookiePathSuffix,
        });

        return newId;
    }

    /// <summary>
    /// Sanity check on the UNPROTECTED inner value. Defense in depth: even if
    /// a DataProtection key were compromised, the inner shape must still look
    /// like one of OUR issued IDs (base64url, 16–32 chars).
    /// </summary>
    private static bool IsValidInnerShape(string? value)
    {
        if (string.IsNullOrEmpty(value)) return false;
        if (value.Length < 16 || value.Length > 32) return false;
        foreach (var c in value)
        {
            var ok = (c >= 'A' && c <= 'Z')
                  || (c >= 'a' && c <= 'z')
                  || (c >= '0' && c <= '9')
                  || c == '-' || c == '_';
            if (!ok) return false;
        }
        return true;
    }
}
