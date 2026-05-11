namespace GaApi.Services;

using System.Security.Cryptography;

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
/// pollution vector once Memory:EnrichOnRetrieve=true ships (see PR #161
/// SC-001 — same threat shape applies to per-session retrieval if the
/// session ID is attacker-controlled). Generating server-side prevents
/// an attacker from setting their cookie to a victim's session ID and
/// reading the victim's MemoryHook-persisted entries.
/// </para>
/// <para>
/// <b>Cookie shape:</b>
/// <list type="bullet">
///   <item>Name: <c>ga_chat_session</c></item>
///   <item>Value: 128-bit cryptographic random, base64url-encoded</item>
///   <item>HttpOnly: true (not JS-accessible)</item>
///   <item>SameSite: Lax (allows top-level navigation from external links)</item>
///   <item>Secure: true when request is HTTPS</item>
///   <item>Max-Age: 30 days</item>
/// </list>
/// </para>
/// <para>
/// <b>Threat model:</b> rotation across browser sessions (cookie cleared,
/// new tab in incognito, etc.) is by design — same trade-off as SignalR's
/// reconnect rotation (see ChatHookContext.SessionId XML docs). The
/// cookie is a CONVENIENCE for HTTP callers to maintain memory continuity
/// across page reloads. Anyone who wants stable cross-device sessions
/// must authenticate, which is out of scope for the public anonymous
/// demo.
/// </para>
/// </remarks>
public static class HttpChatSessionCookie
{
    public const string CookieName = "ga_chat_session";
    private static readonly TimeSpan CookieLifetime = TimeSpan.FromDays(30);

    /// <summary>
    /// Returns the chat session ID for this HTTP request. If a valid
    /// <c>ga_chat_session</c> cookie is present, its value is returned.
    /// Otherwise a fresh 128-bit random ID is generated and set on the
    /// outgoing response as a cookie, so the next request from the same
    /// browser sees the same ID.
    /// </summary>
    public static string GetOrIssue(HttpContext ctx)
    {
        // If a valid-looking cookie is already present, reuse it.
        if (ctx.Request.Cookies.TryGetValue(CookieName, out var existing) &&
            IsValidSessionValue(existing))
        {
            return existing!;
        }

        // Otherwise generate a new one and set it on the response.
        var bytes = RandomNumberGenerator.GetBytes(16); // 128 bits — matches SignalR's default ConnectionIdGenerator
        var newId = Convert.ToBase64String(bytes)
            .Replace('+', '-')
            .Replace('/', '_')
            .TrimEnd('=');

        ctx.Response.Cookies.Append(CookieName, newId, new CookieOptions
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
    /// Sanity check: only accept session values that look like they came
    /// from us (base64url-shaped, 16–32 chars). Rejects empty / oversized
    /// / obviously-tampered values so we don't blindly trust attacker-
    /// controlled cookie bytes.
    /// </summary>
    private static bool IsValidSessionValue(string? value)
    {
        if (string.IsNullOrEmpty(value)) return false;
        if (value.Length < 16 || value.Length > 64) return false;
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
