namespace GaApi.Services;

/// <summary>
///     The chatbot API calls the public page (<c>wwwroot/chatbot/index.html</c>) makes. Outside
///     Development, <c>Program.cs</c> requires <c>X-Api-Key</c> on <c>/api/chatbot/*</c>; these
///     calls stay anonymous, as they were on GaChatbot.Api, or the page answers 401 everywhere.
/// </summary>
/// <remarks>
///     Keep this list in step with the page's <c>fetch</c> calls. Everything else under
///     <c>/api/chatbot</c> (the JSON <c>chat</c>, AG-UI, <c>skills</c>) stays behind the key.
/// </remarks>
public static class PublicChatbotPageApi
{
    private static readonly (string Method, string Path)[] PageCalls =
    [
        (HttpMethods.Get, "/api/chatbot/status"),
        (HttpMethods.Get, "/api/chatbot/examples"),
        (HttpMethods.Get, "/api/chatbot/demo"),
        (HttpMethods.Get, "/api/chatbot/qa-summary"),
        (HttpMethods.Post, "/api/chatbot/chat/stream"),
    ];

    /// <summary>Whether <paramref name="request"/> is one of the page's calls (method and exact path).</summary>
    public static bool AllowsAnonymous(HttpRequest request) =>
        PageCalls.Any(call =>
            HttpMethods.Equals(request.Method, call.Method)
            && string.Equals(request.Path.Value?.TrimEnd('/'), call.Path, StringComparison.OrdinalIgnoreCase));
}
