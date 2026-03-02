namespace MyFirstMCP.Tools;

internal static class HttpClientExt
{
    extension(HttpClient client)
    {
        public async Task<JsonDocument> ReadJsonDocumentAsync(string requestUri)
        {
            using var response = await client.GetAsync(requestUri);
            response.EnsureSuccessStatusCode();
            return await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
        }
    }
}
