namespace GA.Business.ML.Agents;

using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;

/// <summary>
/// Extracts chord names from agent response text and appends a markdown chord diagram block.
/// </summary>
/// <remarks>
/// The <c>[diagram: ChordName]</c> marker convention is consumed by the React frontend,
/// which intercepts these tokens and renders an interactive chord diagram component.
/// </remarks>
public static partial class ChordDiagramRenderer
{
    // Matches chord names that are whole-word tokens in prose text.
    // Root note: [A-G][#b]?
    // Quality: optional quality suffix (maj7, min7, m7, 7, 9, 11, 13, dim7, aug, sus2/4, add2/9)
    // Extension: optional alteration (b5, #5, b9, #9, #11, b13)
    // Must be preceded and followed by a word boundary (space, punctuation, start/end of string),
    // and must be at least 2 characters long so bare root letters like "A" or "G" are excluded.
    [GeneratedRegex(
        @"(?<![A-Za-z])([A-G][#b]?(?:maj7?|min7?|m7?|7|9|11|13|dim7?|aug|sus[24]?|add[29])?(?:b5|#5|b9|#9|#11|b13)?)(?![A-Za-z])",
        RegexOptions.Compiled | RegexOptions.CultureInvariant)]
    private static partial Regex ChordPattern();

    /// <summary>
    /// Extracts unique chord names from <paramref name="text"/>, preserving order of first appearance.
    /// Single-letter root notes with no quality suffix are excluded.
    /// </summary>
    /// <param name="text">The agent response text to scan.</param>
    /// <returns>Deduplicated chord names in first-appearance order.</returns>
    public static IReadOnlyList<string> ExtractChordNames(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return [];

        var seen = new HashSet<string>(StringComparer.Ordinal);
        var result = new List<string>();

        foreach (Match match in ChordPattern().Matches(text))
        {
            var chord = match.Value;

            // Require at least 2 chars: single bare root notes (A–G) are too ambiguous.
            if (chord.Length < 2)
                continue;

            if (seen.Add(chord))
                result.Add(chord);
        }

        return result;
    }

    /// <summary>
    /// Appends a "Chord Diagrams" markdown section to <paramref name="responseText"/>.
    /// Each chord produces a <c>[diagram: ChordName]</c> marker the frontend can render.
    /// </summary>
    /// <param name="responseText">The existing response text.</param>
    /// <param name="chords">Chord names to include; empty collections produce no output.</param>
    /// <returns>The original text, unchanged if <paramref name="chords"/> is empty; otherwise with a diagram block appended.</returns>
    public static string AppendDiagrams(string responseText, IReadOnlyList<string> chords)
    {
        if (chords.Count == 0)
            return responseText;

        var sb = new StringBuilder(responseText.Length + chords.Count * 40 + 64);
        sb.Append(responseText);

        if (!responseText.EndsWith('\n'))
            sb.AppendLine();

        sb.AppendLine();
        sb.AppendLine("---");
        sb.AppendLine("**Chord Diagrams**");

        foreach (var chord in chords)
            sb.AppendLine($"- **{chord}** `[diagram: {chord}]`");

        return sb.ToString();
    }
}
