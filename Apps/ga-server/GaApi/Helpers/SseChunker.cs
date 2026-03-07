namespace GaApi.Helpers;

/// <summary>
/// Splits a response string into sentence-boundary chunks for progressive SSE rendering.
/// Shared between <see cref="GaApi.Controllers.ChatbotController"/> and <see cref="GaApi.Hubs.ChatbotHub"/>.
/// </summary>
internal static class SseChunker
{
    private static readonly System.Text.RegularExpressions.Regex _sentenceSplit =
        new(@"(?<=[.!?])\s+", System.Text.RegularExpressions.RegexOptions.Compiled);

    public static IEnumerable<string> SplitIntoChunks(string text)
    {
        if (string.IsNullOrEmpty(text)) yield break;

        foreach (var sentence in _sentenceSplit.Split(text).Where(s => !string.IsNullOrWhiteSpace(s)))
            yield return sentence;
    }
}
