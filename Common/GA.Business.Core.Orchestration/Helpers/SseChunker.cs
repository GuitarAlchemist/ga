namespace GA.Business.Core.Orchestration.Helpers;

using System.Collections.Generic;
using System.Text.RegularExpressions;

/// <summary>
/// Splits a response string into sentence-boundary chunks for progressive SSE rendering.
/// </summary>
/// <remarks>
/// Candidate #2 (contract-neutral part) from /improve-codebase-architecture. Previously
/// duplicated as <c>internal static SseChunker</c> in both GaApi and GaChatbot.Api with a
/// divergent empty-check (<c>IsNullOrEmpty</c> vs <c>IsNullOrWhiteSpace</c>).
/// One shared copy in the orchestration layer both hosts already reference.
/// </remarks>
public static class SseChunker
{
    // Zero-width split after a sentence's trailing whitespace: each chunk keeps the spaces or
    // line breaks that follow it. Clients concatenate chunks verbatim, so consuming that
    // whitespace glued sentences and collapsed markdown lists into one line.
    private static readonly Regex SentenceSplit = new(@"(?<=[.!?]\s+)(?=\S)", RegexOptions.Compiled);

    /// <summary>
    /// Yields sentence-boundary chunks whose concatenation equals <paramref name="text"/>;
    /// empty/whitespace input yields nothing.
    /// </summary>
    public static IEnumerable<string> SplitIntoChunks(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            yield break;

        foreach (var sentence in SentenceSplit.Split(text))
            yield return sentence;
    }
}
