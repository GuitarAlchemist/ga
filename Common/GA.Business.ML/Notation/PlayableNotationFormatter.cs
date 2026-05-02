namespace GA.Business.ML.Notation;

using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

/// <summary>
/// Formats known playable guitar positions for the chatbot notation renderer.
/// </summary>
public static partial class PlayableNotationFormatter
{
    /// <summary>
    /// Shared instruction for model paths that may include playable guitar fret positions.
    /// </summary>
    public const string PromptGuidance =
        """
        Playable notation:
        - When you include exact playable guitar frets, include a fenced `vextab` block immediately after the shape.
        - Use GA VexTab token format `string/fret`, with string 6 = low E and string 1 = high E.
        - Example:
          ```vextab
          5/3 4/2 3/0 2/1 1/0
          ```
        - Only emit a `vextab` block when the frets are known. Do not invent exact tabs.
        """;

    /// <summary>
    /// Converts a six-string chord diagram such as <c>x-3-2-0-1-0</c> or <c>x32010</c>
    /// into the GA VexTab token format consumed by the mini UI.
    /// </summary>
    public static string? TryFormatChordDiagramAsVexTab(string? diagram)
    {
        var frets = TryParseSixStringDiagram(diagram);
        if (frets is null)
        {
            return null;
        }

        List<string> tokens = [];
        for (var index = 0; index < frets.Count; index++)
        {
            var fret = frets[index];
            if (fret is null)
            {
                continue;
            }

            var guitarString = frets.Count - index;
            tokens.Add(FormattableString.Invariant($"{guitarString}/{fret.Value}"));
        }

        return tokens.Count == 0 ? null : string.Join(" ", tokens);
    }

    /// <summary>
    /// Converts a six-string chord diagram into a fenced <c>vextab</c> block.
    /// </summary>
    public static string? TryFormatChordDiagramAsMarkdownFence(string? diagram)
    {
        var notation = TryFormatChordDiagramAsVexTab(diagram);
        return notation is null
            ? null
            : $"```vextab{Environment.NewLine}{notation}{Environment.NewLine}```";
    }

    /// <summary>
    /// Adds fenced <c>vextab</c> blocks after markdown lines that contain known six-string diagrams.
    /// </summary>
    public static NotationAugmentationResult AugmentMarkdownWithVexTabFences(string? markdown)
    {
        if (string.IsNullOrWhiteSpace(markdown))
        {
            return new NotationAugmentationResult(markdown ?? string.Empty, 0, 0);
        }

        var lines = markdown.ReplaceLineEndings("\n").Split('\n');
        var builder = new StringBuilder(markdown.Length + 256);
        var diagramCount = 0;
        var addedFenceCount = 0;
        var insideFence = false;

        for (var lineIndex = 0; lineIndex < lines.Length; lineIndex++)
        {
            var line = lines[lineIndex];
            builder.Append(line);
            if (lineIndex < lines.Length - 1)
            {
                builder.AppendLine();
            }

            var trimmed = line.TrimStart();
            if (trimmed.StartsWith("```", StringComparison.Ordinal))
            {
                insideFence = !insideFence;
                continue;
            }

            if (insideFence)
            {
                continue;
            }

            var diagrams = ExtractChordDiagrams(line);
            if (diagrams.Count == 0)
            {
                continue;
            }

            diagramCount += diagrams.Count;
            if (NextMeaningfulLineIsVexTabFence(lines, lineIndex + 1))
            {
                continue;
            }

            foreach (var diagram in diagrams)
            {
                if (TryFormatChordDiagramAsMarkdownFence(diagram) is not { } fence)
                {
                    continue;
                }

                if (lineIndex >= lines.Length - 1)
                {
                    builder.AppendLine();
                }

                builder.AppendLine(fence);
                addedFenceCount++;
            }
        }

        return new NotationAugmentationResult(builder.ToString().TrimEnd(), diagramCount, addedFenceCount);
    }

    /// <summary>
    /// Extracts six-string chord diagrams from text.
    /// </summary>
    public static IReadOnlyList<string> ExtractChordDiagrams(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return [];
        }

        List<string> diagrams = [];
        foreach (Match match in InlineDiagramRegex().Matches(text))
        {
            var diagram = match.Groups["diagram"].Value;
            if (TryFormatChordDiagramAsVexTab(diagram) is not null)
            {
                diagrams.Add(diagram);
            }
        }

        return diagrams;
    }

    private static IReadOnlyList<int?>? TryParseSixStringDiagram(string? diagram)
    {
        if (string.IsNullOrWhiteSpace(diagram))
        {
            return null;
        }

        var normalized = diagram.Trim();
        var dash = DashDiagramRegex().Match(normalized);
        if (dash.Success)
        {
            List<int?> frets = [];
            for (var groupIndex = 1; groupIndex <= 6; groupIndex++)
            {
                if (!TryParseFret(dash.Groups[groupIndex].Value, out var fret))
                {
                    return null;
                }

                frets.Add(fret);
            }

            return frets;
        }

        var compact = CompactDiagramRegex().Match(normalized);
        if (!compact.Success)
        {
            return null;
        }

        List<int?> compactFrets = [];
        foreach (var symbol in compact.Value.Trim())
        {
            if (symbol is 'x' or 'X')
            {
                compactFrets.Add(null);
                continue;
            }

            compactFrets.Add(symbol - '0');
        }

        return compactFrets;
    }

    private static bool TryParseFret(string token, out int? fret)
    {
        if (token.Equals("x", StringComparison.OrdinalIgnoreCase))
        {
            fret = null;
            return true;
        }

        if (!int.TryParse(token, NumberStyles.None, CultureInfo.InvariantCulture, out var parsed) ||
            parsed is < 0 or > 36)
        {
            fret = null;
            return false;
        }

        fret = parsed;
        return true;
    }

    [GeneratedRegex(@"^\s*([xX]|\d{1,2})-([xX]|\d{1,2})-([xX]|\d{1,2})-([xX]|\d{1,2})-([xX]|\d{1,2})-([xX]|\d{1,2})\s*$")]
    private static partial Regex DashDiagramRegex();

    [GeneratedRegex(@"^\s*[xX0-9]{6}\s*$")]
    private static partial Regex CompactDiagramRegex();

    [GeneratedRegex(@"(?<![\w/])(?<diagram>(?:[xX]|\d{1,2})(?:-(?:[xX]|\d{1,2})){5})(?![\w/])")]
    private static partial Regex InlineDiagramRegex();

    private static bool NextMeaningfulLineIsVexTabFence(IReadOnlyList<string> lines, int startIndex)
    {
        for (var i = startIndex; i < lines.Count; i++)
        {
            var trimmed = lines[i].TrimStart();
            if (trimmed.Length == 0)
            {
                continue;
            }

            return trimmed.StartsWith("```vextab", StringComparison.OrdinalIgnoreCase);
        }

        return false;
    }
}

public sealed record NotationAugmentationResult(
    string Text,
    int DiagramCount,
    int AddedFenceCount);
