namespace GA.Business.ML.Agents.Hooks;

using System.Text;
using System.Text.RegularExpressions;

/// <summary>
/// Sanitizes user input before it reaches any skill or LLM call.
/// Blocks prompt-injection patterns identified in the GA security review.
/// </summary>
/// <remarks>
/// Fires at <see cref="IChatHook.OnRequestReceived"/> — before skill routing.
/// Two-pass approach:
/// <list type="number">
///   <item>NFKD Unicode normalization (neutralises look-alike characters).</item>
///   <item>Regex rejection for known injection command prefixes.</item>
/// </list>
/// </remarks>
public sealed class PromptSanitizationHook(ILogger<PromptSanitizationHook> logger) : IChatHook
{
    // Injection patterns from the GA security review (ce-review-security-arch-hygiene.md)
    private static readonly Regex InjectionPattern = new(
        @"(?:SYSTEM|USER|ASSISTANT)\s*:|###\s*\w|```\s*system",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public Task<HookResult> OnRequestReceived(ChatHookContext ctx, CancellationToken ct = default)
    {
        // 1. NFKD normalization — flattens look-alike Unicode characters
        var normalized = NormalizeUnicode(ctx.CurrentMessage);

        // 2. Reject obvious injection attempts
        if (InjectionPattern.IsMatch(normalized))
        {
            logger.LogWarning(
                "PromptSanitizationHook: blocked injection attempt in message (length={Length})",
                ctx.CurrentMessage.Length);

            return Task.FromResult(HookResult.Block(
                "Your message contains patterns that cannot be processed. " +
                "Please rephrase your question."));
        }

        // 3. Replace with normalized form so downstream sees clean text
        ctx.CurrentMessage = normalized;
        return Task.FromResult(HookResult.Continue);
    }

    private static string NormalizeUnicode(string input)
    {
        // NFKD decomposes look-alike characters (e.g., fullwidth letters → ASCII)
        var normalized = input.Normalize(NormalizationForm.FormKD);

        // Strip combining diacritical marks left by decomposition where safe
        var sb = new StringBuilder(normalized.Length);
        foreach (var c in normalized)
        {
            var category = char.GetUnicodeCategory(c);
            if (category != System.Globalization.UnicodeCategory.NonSpacingMark)
                sb.Append(c);
        }

        return sb.ToString();
    }
}
