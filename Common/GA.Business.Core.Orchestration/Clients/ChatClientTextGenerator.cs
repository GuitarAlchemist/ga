namespace GA.Business.Core.Orchestration.Clients;

using System.Text.Json;
using Microsoft.Extensions.AI;

/// <summary>
/// Default <see cref="ITextGenerator"/>: runs the prompt through the application's
/// <see cref="IChatClient"/> (Anthropic in CI and production), so the chatbot needs no local model server.
/// </summary>
/// <remarks>
/// No temperature is sent (Haiku 5.5 rejects non-default sampling parameters). For structured output
/// the JSON Schema is written into the prompt and the JSON object is extracted from the reply.
/// </remarks>
public sealed class ChatClientTextGenerator(IChatClient chatClient) : ITextGenerator
{
    private static readonly JsonSerializerOptions _caseInsensitive =
        new() { PropertyNameCaseInsensitive = true };

    public async Task<string> GenerateAsync(
        string prompt,
        float temperature = 0.7f,
        int? numPredict = null,
        CancellationToken ct = default) =>
        await GetReplyAsync(prompt, numPredict, ct);

    public async Task<T?> GenerateStructuredAsync<T>(
        string prompt,
        object jsonSchema,
        float temperature = 0.1f,
        CancellationToken ct = default) where T : class
    {
        var text = $"{prompt}\n\nRespond with a single JSON object matching this JSON Schema, and nothing else:\n{JsonSerializer.Serialize(jsonSchema)}";
        var raw = ExtractJsonObject(await GetReplyAsync(text, numPredict: null, ct));
        if (string.IsNullOrWhiteSpace(raw)) return null;
        return JsonSerializer.Deserialize<T>(raw, _caseInsensitive);
    }

    private async Task<string> GetReplyAsync(string text, int? numPredict, CancellationToken ct)
    {
        var response = await chatClient.GetResponseAsync(
            [new ChatMessage(ChatRole.User, text)],
            new ChatOptions { MaxOutputTokens = numPredict },
            ct);
        return response.Text;
    }

    /// <summary>The outermost <c>{…}</c> of a reply (fences and prose around it dropped), or empty.</summary>
    private static string ExtractJsonObject(string reply)
    {
        var start = reply.IndexOf('{');
        var end = reply.LastIndexOf('}');
        return start >= 0 && end > start ? reply[start..(end + 1)] : string.Empty;
    }
}
