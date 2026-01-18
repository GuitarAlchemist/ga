namespace GaChatbot.Services;

using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using GA.Business.DSL.Services;
using GaChatbot.Models;

/// <summary>
/// Verifies that an LLM response hasn't hallucinated musical data.
/// Part of Phase 5.2.5.
/// </summary>
public class ResponseValidator
{
    private readonly ChordDslService _chordDsl = new();

    public record ValidationResult(bool IsValid, List<string> HallucinatedChords, string CleanedMessage);

    /// <summary>
    /// Validates the message against the allowed list of candidates.
    /// </summary>
    public ValidationResult Validate(string message, List<CandidateVoicing> allowedCandidates)
    {
        var allowedNames = allowedCandidates.Select(c => c.DisplayName).ToHashSet();
        var hallucinated = new List<string>();

        // 1. Regex to find potential chord symbols in the text
        // (Simplified for spike: looking for capitalized words that look like chords)
        var potentialChords = Regex.Matches(message, @"\b[A-G][b#]?(maj|min|m|dim|aug|sus|7|9|11|13|Δ|ø)?\d*\b");

        foreach (Match match in potentialChords)
        {
            var symbol = match.Value;
            
            // Try to normalize using DSL to see if it's a valid chord
            var parseResult = _chordDsl.Parse(symbol);
            if (parseResult.IsOk)
            {
                // It's a real chord symbol. Is it in our manifest?
                if (!allowedNames.Contains(symbol))
                {
                    hallucinated.Add(symbol);
                }
            }
        }

        if (hallucinated.Count > 0)
        {
            return new ValidationResult(false, hallucinated, message + "\n\n[WARNING: This response mentioned chords not found in the verified database.]");
        }

        return new ValidationResult(true, new List<string>(), message);
    }
}
