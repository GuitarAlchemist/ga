namespace GA.Business.Core.Orchestration.Helpers;

using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

/// <summary>
/// Splits a response string into sentence-boundary chunks for progressive SSE rendering.
/// </summary>
/// <remarks>
/// Candidate #2 (contract-neutral part) from /improve-codebase-architecture. Previously
/// duplicated as <c>internal static SseChunker</c> in both GaApi and GaChatbot.Api with a
/// divergent empty-check (<c>IsNullOrEmpty</c> vs <c>IsNullOrWhiteSpace</c> — equivalent
/// here because the trailing <c>Where</c> filters whitespace-only segments either way).
/// One shared copy in the orchestration layer both hosts already reference.
/// </remarks>
public static class SseChunker
{
    private static readonly Regex SentenceSplit = new(@"(?<=[.!?])\s+", RegexOptions.Compiled);

    /// <summary>Yields sentence-boundary chunks; empty/whitespace input yields nothing.</summary>
    public static IEnumerable<string> SplitIntoChunks(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            yield break;

        foreach (var sentence in SentenceSplit.Split(text).Where(s => !string.IsNullOrWhiteSpace(s)))
            yield return sentence;
    }
}
