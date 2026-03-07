namespace GaMcpServer.Tools;

using System.Net.Http.Json;
using ModelContextProtocol.Server;

[McpServerToolType]
public class ChatTool(IHttpClientFactory httpClientFactory)
{
    [McpServerTool]
    [Description("Ask the Guitar Alchemist chatbot a music theory or guitar question. Returns a grounded answer with chord voicings and agent routing metadata.")]
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
        return await response.Content.ReadAsStringAsync(cancellationToken);
    }
}
