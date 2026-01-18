namespace GA.Business.ML.Tabs;

using System.Text.RegularExpressions;
using Models;

/// <summary>
/// Tokenizes raw ASCII text into structured TabBlocks and TabSlices.
/// </summary>
public class TabTokenizer
{
    // Regex to identify a tab line (starts with optional string name, followed by - or |)
    // Examples: "e|---", "E|---", "---", "|---"
    // Regex to identify a tab line (unanchored to allow text prefixes)
    // Matches "String|Content" or "|Content"
    private static readonly Regex TabLineRegex = new(@"([A-Ga-g]?[#b]?\|?|\|)[-0-9|hbp/\\svx~]+", RegexOptions.Compiled);
    
    // Regex to split inline tabs (e.g. "e|---| B|---|")
    // Loosely: patterns that look like [Space][PossibleStringName][|]
    private static readonly Regex InlineSplitRegex = new(@"(?<=\|)(\s+)(?=[A-Ga-g]?[#b]?\|)", RegexOptions.Compiled);

    public List<TabBlock> Tokenize(string asciiTab)
    {
        var blocks = new List<TabBlock>();
        if (string.IsNullOrWhiteSpace(asciiTab)) return blocks;

        // 1. Pre-process to handle inline tabs (Q22)
        // "e|--| B|--|" -> "e|--|\nB|--|"
        var preProcessed = InlineSplitRegex.Replace(asciiTab, "\n");
        
        var allLines = preProcessed.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
        
        var currentBlockLines = new List<string>();
        
        foreach (var line in allLines)
        {
            var trimmed = line.Trim();
            var match = TabLineRegex.Match(trimmed);
            
            if (match.Success && match.Length > 3)
            {
                // Extract only the tab part to ensure vertical alignment (removes "Analyze: " prefix)
                string tabContent = match.Value;
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
        if (lines.Count < 1 || lines.Count > 8) return;

        var block = new TabBlock
        {
            StringCount = lines.Count,
            RawLines = new(lines)
        };

        // Normalize line lengths
        int minLength = lines.Min(l => l.Length);

        // Iterate columns using a while loop to handle variable step size (1 or 2 chars)
        int i = 0;
        while (i < minLength)
        {
            var notes = new List<TabNote>();
            bool isBarLineSlice = true;
            bool anyDigit = false;
            
            // First pass: Check what we have at this column
            for (int lineIdx = 0; lineIdx < lines.Count; lineIdx++)
            {
                if (i >= lines[lineIdx].Length) continue; // Safety
                char c = lines[lineIdx][i];
                if (c != '|' && c != '/') isBarLineSlice = false; // Allow / as barline sometimes? Strict | for now.
                if (char.IsDigit(c)) anyDigit = true;
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
            bool doubleDigitFound = false;
            if (i + 1 < minLength)
            {
                 for (int lineIdx = 0; lineIdx < lines.Count; lineIdx++)
                 {
                     if (i + 1 >= lines[lineIdx].Length) continue;
                     if (char.IsDigit(lines[lineIdx][i]) && char.IsDigit(lines[lineIdx][i+1]))
                     {
                         doubleDigitFound = true;
                         break;
                     }
                 }
            }
            
            // Extract notes
            for (int lineIdx = 0; lineIdx < lines.Count; lineIdx++)
            {
                if (i >= lines[lineIdx].Length) continue;

                // String Index: 0 = Bottom string in visual (Low E if standard), usually.
                // Standard Tab: Top line is High E (String 0 or 5 depending on indexing).
                // GA Standard: String 0 = Low E. Tab Line 0 = High E.
                // So StringIdx = lines.Count - 1 - lineIdx.
                int stringIdx = lines.Count - 1 - lineIdx;
                
                char c = lines[lineIdx][i];

                if (char.IsDigit(c))
                {
                    int fret = 0;
                    if (doubleDigitFound && i + 1 < minLength && i+1 < lines[lineIdx].Length && char.IsDigit(lines[lineIdx][i+1]))
                    {
                        // 2-digit number found on this string
                        var s = new string(new[] { c, lines[lineIdx][i+1] });
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
