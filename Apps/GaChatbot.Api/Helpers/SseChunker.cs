namespace GaChatbot.Api.Helpers;

internal static class SseChunker
{
    private static readonly System.Text.RegularExpressions.Regex SentenceSplit =
        new(@"(?<=[.!?])\s+", System.Text.RegularExpressions.RegexOptions.Compiled);

    public static IEnumerable<string> SplitIntoChunks(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            yield break;
        }

        foreach (var sentence in SentenceSplit.Split(text).Where(s => !string.IsNullOrWhiteSpace(s)))
        {
            yield return sentence;
        }
    }
}
