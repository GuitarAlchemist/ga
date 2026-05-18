namespace GA.Business.Core.Orchestration.Services;

using System.Text.RegularExpressions;
using GA.Business.Core.Orchestration.Models;

/// <summary>
/// Enriches the embedding-router input with prior conversation context when
/// the current message looks like a follow-up to an earlier turn. Used ONLY
/// for the semantic-routing input — downstream agents and the LLM still see
/// the raw message.
/// </summary>
/// <remarks>
/// Originally inlined inside <see cref="ProductionOrchestrator"/>. Extracted
/// 2026-05-14 to let unit tests cover the prefix list, the source-of-truth
/// fallback between session-store and request-supplied history, and the
/// UTF-16 surrogate-safe truncation without spinning up the full
/// orchestrator. Reason for the heuristic itself: "Show me a practical
/// example on guitar" after "How do I make this progression sound darker?"
/// embedded into the practice-routine centroid because "practical" and
/// "practice" share semantic space. Prepending the prior turn pulls the
/// embedding back into the chord-substitution / harmony region the user
/// was actually still talking about.
/// </remarks>
public sealed class RoutingContextEnricher(ConversationHistoryStore historyStore)
{
    /// <summary>
    /// Maximum length (chars) of a message that we consider a candidate for
    /// follow-up context enrichment. Long messages already carry enough
    /// embedding signal on their own; this gates the heuristic to short
    /// utterances that look like pronoun-bearing follow-ups
    /// ("show me a practical example on guitar", "and how about minor",
    /// "do that for Dm7").
    /// </summary>
    public const int FollowUpMaxLength = 80;

    /// <summary>
    /// Surface tokens that signal a follow-up turn — these are deictic
    /// phrases that REQUIRE a prior turn to make sense (they don't take a
    /// noun-phrase complement on their own). Matched case-insensitively at
    /// the start of the trimmed message.
    /// </summary>
    /// <remarks>
    /// Tightened 2026-05-14 (correctness review). The earlier list included
    /// "show me", "and", "expand", "elaborate" which routinely take their
    /// own object noun — "show me the C major scale", "and the diatonic
    /// chords of G?", "expand C7 into voicings" are STANDALONE questions
    /// that an UNRELATED prior turn should not contaminate. The remaining
    /// prefixes are genuinely context-bound: "what about" / "how about"
    /// always reference something prior; "do that" / "same for" / "more
    /// like that" / "tell me more" / "continue" / "in the same key" /
    /// "for that" can only resolve against a prior turn.
    /// </remarks>
    private static readonly Regex FollowUpPrefix =
        new(@"^(?:what about|how about|do (?:that|the same)|same for|more (?:like )?(?:that|this)|tell me more|continue|in (?:the )?(?:same|that) key|for (?:that|this)|show me a practical example|give (?:me )?an? example|show me an? example)\b",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

    /// <summary>
    /// Length cap (UTF-16 code units) on the prior-turn text we splice in.
    /// 240 chars ≈ ~60 tokens — fits alongside a short follow-up without
    /// blowing past the ~512-token silent-truncation limit some embedders
    /// have.
    /// </summary>
    public const int PriorTurnMaxChars = 240;

    /// <summary>
    /// If <paramref name="message"/> looks like a follow-up to a prior turn,
    /// return the most recent prior user message prepended; otherwise return
    /// the input unchanged.
    /// </summary>
    public string EnrichIfFollowUp(
        string message,
        string sessionId,
        IReadOnlyList<ConversationTurn>? requestHistory)
    {
        if (string.IsNullOrWhiteSpace(message)) return message;
        if (message.Length > FollowUpMaxLength) return message;
        if (!FollowUpPrefix.IsMatch(message.TrimStart())) return message;

        // Look in two places. (1) Session-scoped store — the just-added user
        // turn is the last entry; we want the most recent EARLIER user turn.
        // (2) Request-supplied history — the React frontend posts the prior
        // turns per request without a stable sessionId, so the store can be
        // empty even mid-conversation. We fall through to requestHistory in
        // that case.
        ConversationTurn? priorUser = FindMostRecentPriorUserTurn(
            historyStore.GetHistory(sessionId),
            skipLast: true);

        if (priorUser is null && requestHistory is { Count: > 0 })
        {
            // requestHistory does NOT include the just-arrived turn, so don't
            // skip the last entry — the last "user" entry IS the prior turn.
            priorUser = FindMostRecentPriorUserTurn(requestHistory, skipLast: false);
        }

        if (priorUser is null) return message;

        var priorContent = TruncatePreservingSurrogates(priorUser.Content, PriorTurnMaxChars);
        return $"{priorContent} {message}";
    }

    /// <summary>
    /// UTF-16-safe string truncation. If the cut would land between a
    /// high surrogate and its low surrogate, back the cut up by one so the
    /// resulting string contains only well-formed code points. Appends an
    /// ellipsis when truncation actually happens.
    /// </summary>
    public static string TruncatePreservingSurrogates(string s, int maxUtf16Units)
    {
        if (s.Length <= maxUtf16Units) return s;
        var cut = maxUtf16Units;
        if (cut > 0 && char.IsHighSurrogate(s[cut - 1])) cut--;
        return s[..cut] + "…";
    }

    public static ConversationTurn? FindMostRecentPriorUserTurn(
        IReadOnlyList<ConversationTurn> turns,
        bool skipLast)
    {
        var start = skipLast ? turns.Count - 2 : turns.Count - 1;
        for (var i = start; i >= 0; i--)
        {
            if (string.Equals(turns[i].Role, "user", StringComparison.OrdinalIgnoreCase))
                return turns[i];
        }
        return null;
    }
}
