namespace GA.Business.Core.Orchestration.Services;

using System.Text.RegularExpressions;
using GA.Business.Core.Orchestration.Abstractions;

public sealed partial class KeywordAlgebraPromptClassifier : IAlgebraPromptClassifier
{
    private static readonly string[] Keywords =
    [
        "prime form",
        "interval class vector",
        "icv",
        "forte",
        "z-related",
        "z relation",
        "z-pair",
        "set class",
        "pitch-class set",
        "pitch class set"
    ];

    public bool IsAlgebraPrompt(string query)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return false;
        }

        var normalized = query.ToLowerInvariant();
        if (Keywords.Any(normalized.Contains))
        {
            return true;
        }

        return PitchClassSetRegex().IsMatch(query);
    }

    [GeneratedRegex(@"[\[\{][0-9TEAB,\s]+[\]\}]|\b[0-9TEAB]{3,12}\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex PitchClassSetRegex();
}
