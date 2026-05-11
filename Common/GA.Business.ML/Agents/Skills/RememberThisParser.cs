namespace GA.Business.ML.Agents.Skills;

using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Memory;

/// <summary>
/// Pure parser that turns a "remember that X" / "save this: X" / "note: X"
/// user utterance into a <see cref="MemoryWriteRequest"/>. Kept separate
/// from the skill so the parsing rules are independently unit-testable
/// and so reasonable tests don't need to spin up an orchestrator.
/// </summary>
/// <remarks>
/// <para>
/// <b>Type inference (in priority order):</b>
/// </para>
/// <list type="bullet">
///   <item><c>preference</c> — phrasing signals taste: "I prefer", "my favorite",
///         "I like", "I always", "I usually"</item>
///   <item><c>focus</c> — phrasing signals current attention: "I'm working on",
///         "right now I'm", "this week", "currently"</item>
///   <item><c>fact</c> — everything else (default)</item>
/// </list>
/// <para>
/// <b>Key generation:</b> a stable slug from the first 4 content words
/// plus the first 8 hex chars of a SHA256 over the full content. Same
/// content → same key, so subsequent "remember the same thing" calls
/// idempotently overwrite the prior entry rather than creating a
/// fact_xyz1, fact_xyz2, fact_xyz3 spray.
/// </para>
/// <para>
/// <b>What this parser is NOT:</b> an LLM-driven semantic extractor.
/// It's a deliberately tiny regex layer that handles the explicit
/// "remember X" / "save X" / "note X" surface. Auto-extraction of
/// implicit preferences from arbitrary chat — option C from the
/// PR #172 audit — is a separate, riskier feature deferred for now.
/// </para>
/// </remarks>
public static class RememberThisParser
{
    /// <summary>
    /// Lead phrases stripped from the input to isolate the content. The
    /// regex is anchored on word boundaries and case-insensitive; only
    /// matches at the start of the trimmed message.
    /// </summary>
    private static readonly Regex LeadPhrase = new(
        @"^\s*(?:please\s+)?(?:can\s+you\s+)?(?:remember(?:\s+that)?|save(?:\s+this)?|note(?:\s+this)?|store(?:\s+that)?|don'?t\s+forget(?:\s+that)?)[:,]?\s*",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex PreferenceCue = new(
        @"\b(?:i\s+prefer|my\s+favorite|i\s+like|i\s+always|i\s+usually|i\s+love)\b",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex FocusCue = new(
        @"\b(?:i'?m\s+working\s+on|i'?m\s+focused\s+on|right\s+now\s+i'?m|this\s+week|currently|focused\s+on|practicing)\b",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    /// <summary>
    /// Attempts to parse <paramref name="message"/> as a remember-request.
    /// Returns null when no lead-phrase is detected — the message isn't
    /// addressed to this skill.
    /// </summary>
    public static MemoryWriteRequest? TryParse(string message)
    {
        if (string.IsNullOrWhiteSpace(message)) return null;

        var match = LeadPhrase.Match(message);
        if (!match.Success) return null;

        var content = message[match.Length..].Trim().TrimEnd('.', '!', '?');
        if (content.Length == 0) return null;

        var type = InferType(content);
        var key  = GenerateKey(type, content);

        return new MemoryWriteRequest(
            Key:     key,
            Type:    type,
            Content: content,
            Tags:    ["user-stated", $"type:{type}"]);
    }

    /// <summary>
    /// True when the lead-phrase regex would match — exposed so the skill's
    /// <c>CanHandle</c> can short-circuit cheaply without re-running parse.
    /// </summary>
    public static bool LooksLikeRememberRequest(string message) =>
        !string.IsNullOrWhiteSpace(message) && LeadPhrase.IsMatch(message);

    private static string InferType(string content)
    {
        // Preference takes priority over focus when both fire (e.g.,
        // "I prefer working on jazz" — taste statement primary).
        if (PreferenceCue.IsMatch(content)) return "preference";
        if (FocusCue.IsMatch(content))      return "focus";
        return "fact";
    }

    private static string GenerateKey(string type, string content)
    {
        var slugWords = content
            .Split(new[] { ' ', '\t', '\n' }, StringSplitOptions.RemoveEmptyEntries)
            .Take(4)
            .Select(static w => new string([.. w.ToLowerInvariant().Where(char.IsLetterOrDigit)]))
            .Where(w => w.Length > 0)
            .ToList();
        var slug = slugWords.Count > 0 ? string.Join("-", slugWords) : "entry";

        var hashHex = Convert.ToHexString(
            SHA256.HashData(Encoding.UTF8.GetBytes(content)))[..8].ToLowerInvariant();

        return $"{type}_{slug}_{hashHex}";
    }
}
