namespace GaMcpServer.Tools;

using System.Net.Http.Json;
using System.Text.Json;
using ModelContextProtocol.Server;

[McpServerToolType]
public class ChatTool(IHttpClientFactory httpClientFactory)
{
    private static readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web);

    [McpServerTool]
    [Description("Ask the Guitar Alchemist chatbot a music theory or guitar question. Returns a grounded natural-language answer.")]
    public async Task<string> AskChatbot(
        [Description("The music theory or guitar question to ask")] string question,
        CancellationToken cancellationToken = default)
    {
        var client = httpClientFactory.CreateClient("gaapi");
        var response = await client.PostAsJsonAsync(
            "/api/chatbot/chat",
            new { message = question },
            cancellationToken);
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync(cancellationToken);
        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.TryGetProperty("naturalLanguageAnswer", out var answerEl))
            {
                var answer = answerEl.GetString();
                if (!string.IsNullOrWhiteSpace(answer)) return answer;
            }
        }
        catch (JsonException) { /* fall through */ }

        return json;
    }
}
