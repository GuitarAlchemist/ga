namespace GA.Business.ML.Notation;

using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using GA.Business.DSL.Parsers;

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
        - Write VexTab: a `tabstave` line, then a `notes` line. A chord is `(fret/string.fret/string...)`,
          fret first, lowest string first, with string 6 = low E and string 1 = high E; leave muted strings out.
        - Example, open C (x-3-2-0-1-0):
          ```vextab
          tabstave
          notes :w (3/5.2/4.0/3.1/2.0/1)
          ```
        - Only emit a `vextab` block when the frets are known. Do not invent exact tabs.
        """;

    /// <summary>
    /// Converts a voicing diagram as GA stores it (<c>Voicing.Diagram</c> and index documents list
    /// string 1, the highest, first) into chord-chart order, lowest string first, which is what
    /// readers expect and what <see cref="TryFormatChordDiagramAsVexTab"/> parses. Open C is stored
    /// as <c>0-1-0-2-3-x</c> and charted as <c>x-3-2-0-1-0</c>.
    /// </summary>
    public static string? ToChartOrder(string? voicingDiagram) =>
        string.IsNullOrEmpty(voicingDiagram)
            ? voicingDiagram
            : string.Join("-", Enumerable.Reverse(voicingDiagram.Split('-')));

    /// <summary>
    /// Converts a six-string chord diagram in chord-chart order (lowest string first), such as
    /// <c>x-3-2-0-1-0</c> or <c>x32010</c>, into VexTab: a stave and one whole-note chord,
    /// <c>tabstave</c> then <c>notes :w (3/5.2/4.0/3.1/2.0/1)</c>, fret before string as VexTab
    /// writes them, muted strings left out.
    /// Diagrams read from GA voicings must go through <see cref="ToChartOrder"/> first, or use the
    /// overload taking <see cref="DiagramStringOrder.HighToLow"/>.
    /// </summary>
    public static string? TryFormatChordDiagramAsVexTab(string? diagram) =>
        TryFormatChordDiagramAsVexTab(diagram, DiagramStringOrder.LowToHigh);

    /// <summary>
    /// Converts a six-string chord diagram whose fret tokens are in <paramref name="order"/>
    /// into VexTab (string 6 = low E), lowest string first.
    /// </summary>
    public static string? TryFormatChordDiagramAsVexTab(string? diagram, DiagramStringOrder order)
    {
        var frets = TryParseSixStringDiagram(diagram);
        if (frets is null)
        {
            return null;
        }

        List<string> tokens = [];
        for (var guitarString = frets.Count; guitarString >= 1; guitarString--)
        {
            var index = order == DiagramStringOrder.LowToHigh ? frets.Count - guitarString : guitarString - 1;
            if (frets[index] is not { } fret)
            {
                continue;
            }

            tokens.Add(FormattableString.Invariant($"{fret}/{guitarString}"));
        }

        return tokens.Count == 0 ? null : $"tabstave\nnotes :w ({string.Join(".", tokens)})";
    }

    /// <summary>
    /// Counts the fenced <c>vextab</c> blocks of a markdown text, and how many of them are VexTab
    /// that GA's VexTab parser reads.
    /// </summary>
    public static (int Blocks, int Valid) CountVexTabBlocks(string? markdown)
    {
        if (string.IsNullOrEmpty(markdown))
        {
            return (0, 0);
        }

        var blocks = 0;
        var valid = 0;
        foreach (Match match in VexTabFenceRegex().Matches(markdown.ReplaceLineEndings("\n")))
        {
            blocks++;
            if (VexTabParser.parse(match.Groups["body"].Value).IsOk)
            {
                valid++;
            }
        }

        return (blocks, valid);
    }

    /// <summary>
    /// Converts a six-string chord diagram (guitarist convention, low E first) into a fenced <c>vextab</c> block.
    /// </summary>
    public static string? TryFormatChordDiagramAsMarkdownFence(string? diagram) =>
        TryFormatChordDiagramAsMarkdownFence(diagram, DiagramStringOrder.LowToHigh);

    /// <summary>
    /// Converts a six-string chord diagram whose fret tokens are in <paramref name="order"/> into a fenced <c>vextab</c> block.
    /// </summary>
    public static string? TryFormatChordDiagramAsMarkdownFence(string? diagram, DiagramStringOrder order)
    {
        var notation = TryFormatChordDiagramAsVexTab(diagram, order);
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
            return new NotationAugmentationResult(markdown ?? string.Empty, 0, 0, 0, 0);
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

        var text = builder.ToString().TrimEnd();
        var (blocks, valid) = CountVexTabBlocks(text);
        return new NotationAugmentationResult(text, diagramCount, addedFenceCount, blocks, valid);
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

    [GeneratedRegex(@"^[ \t]*```vextab[ \t]*\n(?<body>.*?)^[ \t]*```", RegexOptions.Multiline | RegexOptions.Singleline | RegexOptions.IgnoreCase)]
    private static partial Regex VexTabFenceRegex();

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

/// <summary>
/// Order of the six fret tokens in a chord-diagram string.
/// </summary>
public enum DiagramStringOrder
{
    /// <summary>Guitarist convention: string 6 (low E) first, e.g. <c>x-3-2-0-1-0</c> = open C.
    /// Used by hand-written and LLM-written diagrams, <c>FretSpanSkill</c> and <c>BeginnerChordsSkill</c>.</summary>
    LowToHigh,

    /// <summary><c>Voicing.Diagram</c> convention (<c>Str</c> 1 = high E first), e.g. <c>0-1-0-2-3-x</c> = open C.
    /// Used by the voicing generator, the OPTK index and therefore voicing search results.</summary>
    HighToLow,
}

/// <param name="Text">The markdown, with the fences added.</param>
/// <param name="DiagramCount">Chord diagrams found in it.</param>
/// <param name="AddedFenceCount">Fenced <c>vextab</c> blocks added after them.</param>
/// <param name="VexTabBlockCount">Fenced <c>vextab</c> blocks in the final text, added or written by the model.</param>
/// <param name="ValidVexTabBlockCount">Of those, the ones GA's VexTab parser reads.</param>
public sealed record NotationAugmentationResult(
    string Text,
    int DiagramCount,
    int AddedFenceCount,
    int VexTabBlockCount,
    int ValidVexTabBlockCount);
