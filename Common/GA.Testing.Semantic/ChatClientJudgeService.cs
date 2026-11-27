namespace GA.Testing.Semantic;

using System.Text.Json;
using Microsoft.Extensions.AI;

/// <summary>
///     Implementation of IJudgeService over any <see cref="IChatClient" />; the provider is chosen by
///     whoever builds the client (see <see cref="AssertAi.AutoConfigureJudge" />).
///     Asks for JSON output when the provider supports it.
/// </summary>
public class ChatClientJudgeService(IChatClient chatClient) : IJudgeService
{
    public async Task<JudgeResult> EvaluateAsync(string text, string prompt, string rubric,
        CancellationToken cancellationToken = default)
    {
        var systemPrompt =
            $"{prompt}\n\n" +
            $"You are an AI Judge. Evaluate the following text against the provided rubric.\n" +
            $"TEXT TO EVALUATE:\n\"\"\"\n{text}\n\"\"\"\n\n" +
            $"RUBRIC:\n{rubric}\n\n" +
            $"Response Format: You MUST return a JSON object with exactly three fields:\n" +
            $"1. \"isPassing\": (boolean)\n" +
            $"2. \"rationale\": (string explanation)\n" +
            $"3. \"confidence\": (float between 0 and 1)\n";

        var response = await chatClient.GetResponseAsync(
            [new ChatMessage(ChatRole.User, systemPrompt)],
            new ChatOptions { ResponseFormat = ChatResponseFormat.Json },
            cancellationToken);
        var content = response.Text;

        try
        {
            var result = JsonSerializer.Deserialize<JudgeResultJson>(content,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            return new(result?.IsPassing ?? false, result?.Rationale ?? "No rationale provided",
                result?.Confidence ?? 0);
        }
        catch (JsonException ex)
        {
            return new(false, $"Failed to parse judge JSON: {ex.Message}. Raw content: {content}", 0);
        }
    }

    private record JudgeResultJson(bool IsPassing, string Rationale, double Confidence);
}
