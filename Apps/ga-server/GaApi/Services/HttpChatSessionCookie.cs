namespace GaApi.Services;

using System.Security.Cryptography;
using Microsoft.AspNetCore.DataProtection;

/// <summary>
/// Server-issued HTTP session cookie for the chatbot's HTTP transport.
/// Provides the same per-conversation isolation that SignalR's
/// <c>Context.ConnectionId</c> gives the WebSocket transport — see PR #160
/// Phase B for the SignalR side and PR #157 for the storage layer
/// (session-scoped MemoryStore).
/// </summary>
/// <remarks>
/// <para>
/// <b>Why server-issued, not client-supplied:</b> a client-controlled
/// session identifier in cookies (or headers) is a forged-session-cross-
/// pollution vector once Memory:EnrichOnRetrieve=true ships. PR #163
/// security audit (VULN-001) confirmed: a shape-only validator accepts
/// attacker-chosen 22-char values, enabling session fixation. We
/// therefore wrap the random session ID with
/// <see cref="IDataProtector"/> (HMAC + AES-CBC from the app's
/// data-protection key ring). The cookie carries the PROTECTED ciphertext;
/// the inner 22-char ID is what we hand back as <c>SessionId</c>. An
/// attacker who tampers with the cookie or sets their own value will fail
/// <c>Unprotect</c> with a <see cref="CryptographicException"/> and the
/// handler reissues a fresh session.
/// </para>
/// <para>
/// <b>Cookie shape:</b>
/// <list type="bullet">
///   <item>Name: <c>ga_chat_session</c></item>
///   <item>Value: <c>IDataProtector.Protect</c> of a 128-bit random ID</item>
///   <item>HttpOnly: true (not JS-accessible)</item>
///   <item>SameSite: Lax (allows top-level navigation from external links)</item>
///   <item>Secure: true when request is HTTPS</item>
///   <item>Max-Age: 30 days (absolute from issuance, not sliding)</item>
///   <item>Path: <c>/api/chatbot</c></item>
/// </list>
/// </para>
/// <para>
/// <b>Threat model:</b> rotation across browser sessions (cookie cleared,
/// new tab in incognito, etc.) is by design — same trade-off as SignalR's
/// reconnect rotation (see <c>ChatHookContext.SessionId</c> XML docs).
/// The cookie is a CONVENIENCE for HTTP callers to maintain memory
/// continuity across page reloads. Anyone who wants stable cross-device
/// sessions must authenticate, which is out of scope for the public
/// anonymous demo.
/// </para>
/// </remarks>
public static class HttpChatSessionCookie
{
    public const string CookieName = "ga_chat_session";
    private const string ProtectorPurpose = "GaApi.HttpChatSessionCookie.v1";
    private static readonly TimeSpan CookieLifetime = TimeSpan.FromDays(30);

    /// <summary>
    /// Returns the chat session ID for this HTTP request. If a valid
    /// DataProtection-signed <c>ga_chat_session</c> cookie is present, its
    /// inner value is returned. Otherwise a fresh 128-bit random ID is
    /// generated, protected, and set on the outgoing response as a cookie.
    /// </summary>
    public static string GetOrIssue(HttpContext ctx)
    {
        var protector = ctx.RequestServices
            .GetRequiredService<IDataProtectionProvider>()
            .CreateProtector(ProtectorPurpose);

        // If a cookie is present, try to unprotect it. A forged or tampered
        // value will throw CryptographicException — we ignore it and issue
        // a fresh cookie. This closes VULN-001 (session fixation) from the
        // PR #163 audit: shape-only validation accepts attacker-chosen IDs;
        // DataProtection-signed values are unforgeable without the server key.
        if (ctx.Request.Cookies.TryGetValue(CookieName, out var protectedValue)
            && !string.IsNullOrEmpty(protectedValue))
        {
            try
            {
                var existing = protector.Unprotect(protectedValue);
                if (IsValidInnerShape(existing)) return existing;
                // Inner shape is unexpected (would only happen on schema drift) —
                // fall through to reissue.
            }
            catch (CryptographicException)
            {
                // Tampered, forged, or signed with a now-rotated key — reissue.
            }
        }

        // Generate a fresh 128-bit random ID and protect it.
        var bytes = RandomNumberGenerator.GetBytes(16);
        var newId = Convert.ToBase64String(bytes)
            .Replace('+', '-')
            .Replace('/', '_')
            .TrimEnd('=');

        var protectedNewId = protector.Protect(newId);

        ctx.Response.Cookies.Append(CookieName, protectedNewId, new CookieOptions
        {
            HttpOnly = true,
            Secure   = ctx.Request.IsHttps,
            SameSite = SameSiteMode.Lax,
            MaxAge   = CookieLifetime,
            Path     = "/api/chatbot",
        });

        return newId;
    }

    /// <summary>
    /// Sanity check on the UNPROTECTED inner value. Defense in depth: even
    /// if a DataProtection key were compromised, the inner shape must still
    /// look like one of OUR issued IDs (base64url, 16–32 chars).
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
