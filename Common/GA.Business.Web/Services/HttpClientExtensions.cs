namespace GA.Business.Web.Services;

using System.Text.Json;

/// <summary>
///     Extension methods for HttpClient to simplify common operations
/// </summary>
public static class HttpClientExtensions
{
    /// <summary>
    ///     Read and parse JSON document from HTTP response
    /// </summary>
    public static async Task<JsonDocument> ReadJsonDocumentAsync(
        this HttpClient client,
        string requestUri,
        CancellationToken cancellationToken = default)
    {
        using var response = await client.GetAsync(requestUri, cancellationToken);
        response.EnsureSuccessStatusCode();

        var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        return await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
    }

    /// <summary>
    ///     Read HTML content from HTTP response
    /// </summary>
    public static async Task<string> ReadHtmlAsync(
        this HttpClient client,
        string requestUri,
        CancellationToken cancellationToken = default)
    {
        using var response = await client.GetAsync(requestUri, cancellationToken);
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadAsStringAsync(cancellationToken);
    }

    /// <summary>
    ///     Read text content with custom headers
    /// </summary>
    public static async Task<(string Content, Dictionary<string, string> Headers)> ReadTextWithHeadersAsync(
        this HttpClient client,
        string requestUri,
        CancellationToken cancellationToken = default)
    {
        using var response = await client.GetAsync(requestUri, cancellationToken);
        response.EnsureSuccessStatusCode();

        var content = await response.Content.ReadAsStringAsync(cancellationToken);
        var headers = response.Headers.ToDictionary(
            h => h.Key,
            h => string.Join(", ", h.Value));

        return (content, headers);
    }
}
