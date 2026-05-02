namespace GA.Business.Core.Orchestration.Services;

using System.Text.RegularExpressions;
using GA.Business.Core.Orchestration.Models;
using GA.Business.DSL.Services;

/// <summary>
/// Verifies that an LLM response has not hallucinated musical data not present in the manifest.
/// Strips hallucinated chord symbols from the response rather than appending a warning.
/// </summary>
public class ResponseValidator
{
    private readonly ChordDslService _chordDsl = new();
    private static readonly Regex PotentialChordPattern = new(
        @"\b[A-G][b#]?(?:maj|min|m|dim|aug|sus|add|7|9|11|13|Δ|ø)[#b]?\d*\b",
        RegexOptions.Compiled);

    public record ValidationResult(bool IsValid, IReadOnlyList<string> HallucinatedChords, string CleanedMessage);

    public ValidationResult Validate(string message, IReadOnlyList<CandidateVoicing> allowedCandidates)
    {
        var allowedNames = allowedCandidates
            .Select(c => c.DisplayName)
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var hallucinated = new List<string>();

        var potentialChords = PotentialChordPattern.Matches(message);

        foreach (Match match in potentialChords)
        {
            var symbol = match.Value;
            var parseResult = _chordDsl.Parse(symbol);
            if (parseResult.IsOk && !allowedNames.Contains(symbol))
            {
                hallucinated.Add(symbol);
            }
        }

        if (hallucinated.Count > 0)
        {
            var cleaned = message;
            foreach (var symbol in hallucinated)
                cleaned = Regex.Replace(cleaned, $@"\b{Regex.Escape(symbol)}\b", "[?]");
            return new ValidationResult(false, hallucinated, cleaned);
        }

        return new ValidationResult(true, [], message);
    }
}
