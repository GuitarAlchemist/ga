namespace GaApi.Services;

/// <summary>
///     The chatbot API calls that stay anonymous outside Development. <c>Program.cs</c> requires
///     <c>X-Api-Key</c> on <c>/api/chatbot/*</c>; the retired GaChatbot.Api served these calls
///     without a key, so the cutover to GaApi (ADR-0005) kept that contract.
/// </summary>
/// <remarks>
///     The public page (<c>wwwroot/chatbot/index.html</c>) makes the first five; keep them in step
///     with its <c>fetch</c> calls. The JSON <c>chat</c> serves external callers and the deploy
///     smoke test (docs/runbooks/gaapi-deploy.md). AG-UI and <c>skills</c> stay behind the key.
/// </remarks>
public static class AnonymousChatbotApi
{
    private static readonly (string Method, string Path)[] AnonymousCalls =
    [
        (HttpMethods.Get, "/api/chatbot/status"),
        (HttpMethods.Get, "/api/chatbot/examples"),
        (HttpMethods.Get, "/api/chatbot/demo"),
        (HttpMethods.Get, "/api/chatbot/qa-summary"),
        (HttpMethods.Post, "/api/chatbot/chat/stream"),
        (HttpMethods.Post, "/api/chatbot/chat"),
    ];

    /// <summary>Whether <paramref name="request"/> is one of the anonymous calls (method and exact path).</summary>
    public static bool AllowsAnonymous(HttpRequest request) =>
        AnonymousCalls.Any(call =>
            HttpMethods.Equals(request.Method, call.Method)
            && string.Equals(request.Path.Value?.TrimEnd('/'), call.Path, StringComparison.OrdinalIgnoreCase));
}
