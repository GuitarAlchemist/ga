namespace GA.Business.ML.Tabs;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Core.Fretboard.Analysis;

/// <summary>
/// Renders internal fretboard realizations back into ASCII tablature.
/// </summary>
public class TabRenderer
{
    public string Render(List<List<FretboardPosition>> progression)
    {
        if (progression == null || progression.Count == 0) return string.Empty;

        // 6 strings: e, B, G, D, A, E
        var stringBuilders = new StringBuilder[6];
        for (int i = 0; i < 6; i++)
        {
            stringBuilders[i] = new StringBuilder();
            string name = GetStringName(5 - i); // Reversed for standard tab view
            stringBuilders[i].Append($"{name}|--");
        }

        foreach (var slice in progression)
        {
            // Map realization to strings
            var frets = new string[6];
            for (int i = 0; i < 6; i++) frets[i] = "-";

            foreach (var pos in slice)
            {
                if (pos.StringIndex >= 1 && pos.StringIndex <= 6)
                {
                    frets[pos.StringIndex - 1] = pos.Fret.ToString();
                }
            }

            // Determine max length of any fret number in this slice
            int maxLength = frets.Max(f => f.Length);

            for (int i = 0; i < 6; i++)
            {
                stringBuilders[i].Append(frets[i].PadRight(maxLength + 1, '-'));
                stringBuilders[i].Append("--");
            }
        }

        var result = new StringBuilder();
        for (int i = 0; i < 6; i++)
        {
            stringBuilders[i].Append("|");
            result.AppendLine(stringBuilders[i].ToString());
        }

        return result.ToString();
    }

    private string GetStringName(int stringIdx)
    {
        return stringIdx switch
        {
            0 => "E",
            1 => "A",
            2 => "D",
            3 => "G",
            4 => "B",
            5 => "e",
            _ => "?"
        };
    }
}
