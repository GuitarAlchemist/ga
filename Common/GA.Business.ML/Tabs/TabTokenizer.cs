namespace GA.Business.ML.Tabs;

using System.Text.RegularExpressions;
using Models;

/// <summary>
///     Tokenizes raw ASCII text into structured TabBlocks and TabSlices.
/// </summary>
public class TabTokenizer
{
    // Regex to identify a tab line (unanchored to allow text prefixes).
    // Examples that match: "e|---3---|", "E|---3---|", "---3-----|", "|---0-2-0|"
    // Examples that DON'T match (used to be false-positives, now rejected):
    //   "0146"                        — bare pitch-class-set notation
    //   "12-bar blues form"           — '12-b' has no pipe
    //   "Released --12-04-- last week" — hyphenated date, no pipe
    //   "Are 0146 and 0137 z-related?" — algebra query, no pipe
    //
    // Why the trailing `(?=[-0-9|hbp/\\svx~]*\|)` lookahead matters:
    // tab notation always carries at least one bar-line `|` somewhere on
    // the line — that's the inherent measure separator. Without that
    // anchor the regex matches any digit/dash/letter run from prose
    // (algebra set descriptors, "12-bar blues", dates) and the
    // tokenizer dispatched theory questions to tab analysis. The
    // lookahead requires a `|` to appear within the next run of
    // tab-content characters, so:
    //   - "e|---3---|" still matches (pipe in matched run)
    //   - "---3-----0-----|" still matches (pipe at end)
    //   - "0146" no longer matches (no pipe anywhere)
    // Anonymous-row tab without pipes (extremely rare in the wild) IS
    // rejected — pinned by Tokenize_BareDigitProseInputs_NoLongerProduceNoteSlices
    // and the existing anonymous-row positive control which uses pipes.
    private static readonly Regex
        TabLineRegex = new(
            @"([A-Ga-g]?[#b]?\|?|\|)(?=[-0-9|hbp/\\svx~]*\|)[-0-9|hbp/\\svx~]+",
            RegexOptions.Compiled);

    // Regex to split inline tabs (e.g. "e|---| B|---|")
    // Loosely: patterns that look like [Space][PossibleStringName][|]
    private static readonly Regex InlineSplitRegex = new(@"(?<=\|)(\s+)(?=[A-Ga-g]?[#b]?\|)", RegexOptions.Compiled);

    public List<TabBlock> Tokenize(string asciiTab)
    {
        var blocks = new List<TabBlock>();
        if (string.IsNullOrWhiteSpace(asciiTab))
        {
            return blocks;
        }

        // 1. Pre-process to handle inline tabs (Q22)
        // "e|--| B|--|" -> "e|--|\nB|--|"
        var preProcessed = InlineSplitRegex.Replace(asciiTab, "\n");

        var allLines = preProcessed.Split(["\r\n", "\r", "\n"], StringSplitOptions.None);

        var currentBlockLines = new List<string>();

        foreach (var line in allLines)
        {
            var trimmed = line.Trim();
            var match = TabLineRegex.Match(trimmed);

            if (match.Success && match.Length > 3)
            {
                // Extract only the tab part to ensure vertical alignment (removes "Analyze: " prefix)
                var tabContent = match.Value;
                currentBlockLines.Add(tabContent);
            }
            else
            {
                // End of a block?
                if (currentBlockLines.Count > 0)
                {
                    ProcessBlock(currentBlockLines, blocks);
                    currentBlockLines.Clear();
                }
            }
        }

        // Final block
        if (currentBlockLines.Count > 0)
        {
            ProcessBlock(currentBlockLines, blocks);
        }

        return blocks;
    }

    private void ProcessBlock(List<string> lines, List<TabBlock> blocks)
    {
        // Heuristic: Allow single string (bass line/riff) to 8 strings.
        if (lines.Count < 1 || lines.Count > 8)
        {
            return;
        }

        var block = new TabBlock
        {
            StringCount = lines.Count,
            RawLines = [.. lines]
        };

        // Normalize line lengths
        var minLength = lines.Min(l => l.Length);

        // Iterate columns using a while loop to handle variable step size (1 or 2 chars)
        var i = 0;
        while (i < minLength)
        {
            var notes = new List<TabNote>();
            var isBarLineSlice = true;
            var anyDigit = false;

            // First pass: Check what we have at this column
            for (var lineIdx = 0; lineIdx < lines.Count; lineIdx++)
            {
                if (i >= lines[lineIdx].Length)
                {
                    continue; // Safety
                }

                var c = lines[lineIdx][i];
                if (c != '|' && c != '/')
                {
                    isBarLineSlice = false; // Allow / as barline sometimes? Strict | for now.
                }

                if (char.IsDigit(c))
                {
                    anyDigit = true;
                }
            }

            if (isBarLineSlice)
            {
                block.Slices.Add(new() { IsBarLine = true });
                i++;
                continue;
            }

            if (!anyDigit)
            {
                i++;
                continue;
            }

            // We have digits. Parse them.
            // Check if any string has a 2-digit number starting at i.
            var doubleDigitFound = false;
            if (i + 1 < minLength)
            {
                for (var lineIdx = 0; lineIdx < lines.Count; lineIdx++)
                {
                    if (i + 1 >= lines[lineIdx].Length)
                    {
                        continue;
                    }

                    if (char.IsDigit(lines[lineIdx][i]) && char.IsDigit(lines[lineIdx][i + 1]))
                    {
                        doubleDigitFound = true;
                        break;
                    }
                }
            }

            // Extract notes
            for (var lineIdx = 0; lineIdx < lines.Count; lineIdx++)
            {
                if (i >= lines[lineIdx].Length)
                {
                    continue;
                }

                // String Index: 0 = Bottom string in visual (Low E if standard), usually.
                // Standard Tab: Top line is High E (String 0 or 5 depending on indexing).
                // GA Standard: String 0 = Low E. Tab Line 0 = High E.
                // So StringIdx = lines.Count - 1 - lineIdx.
                var stringIdx = lines.Count - 1 - lineIdx;

                var c = lines[lineIdx][i];

                if (char.IsDigit(c))
                {
                    var fret = 0;
                    if (doubleDigitFound && i + 1 < minLength && i + 1 < lines[lineIdx].Length &&
                        char.IsDigit(lines[lineIdx][i + 1]))
                    {
                        // 2-digit number found on this string
                        var s = new string([c, lines[lineIdx][i + 1]]);
                        int.TryParse(s, out fret);
                    }
                    else
                    {
                        // Single digit
                        fret = c - '0';
                    }

                    notes.Add(new(stringIdx, fret));
                }
            }

            if (notes.Count > 0)
            {
                block.Slices.Add(new() { Notes = notes });
            }

            // Advance
            i += doubleDigitFound ? 2 : 1;
        }

        blocks.Add(block);
    }
}
