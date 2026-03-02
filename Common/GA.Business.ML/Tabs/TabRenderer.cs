namespace GA.Business.ML.Tabs;

using Domain.Services.Fretboard.Analysis;

/// <summary>
///     Renders internal fretboard realizations back into ASCII tablature.
/// </summary>
public class TabRenderer
{
    public string Render(List<List<FretboardPosition>> progression)
    {
        if (progression == null || progression.Count == 0)
        {
            return string.Empty;
        }

        // 6 strings: e, B, G, D, A, E
        var stringBuilders = new StringBuilder[6];
        for (var i = 0; i < 6; i++)
        {
            stringBuilders[i] = new();
            var name = GetStringName(5 - i); // Reversed for standard tab view
            stringBuilders[i].Append($"{name}|--");
        }

        foreach (var slice in progression)
        {
            // Map realization to strings
            var frets = new string[6];
            for (var i = 0; i < 6; i++)
            {
                frets[i] = "-";
            }

            foreach (var pos in slice)
            {
                if (pos.StringIndex >= 1 && pos.StringIndex <= 6)
                {
                    frets[pos.StringIndex - 1] = pos.Fret.ToString();
                }
            }

            // Determine max length of any fret number in this slice
            var maxLength = frets.Max(f => f.Length);

            for (var i = 0; i < 6; i++)
            {
                stringBuilders[i].Append(frets[i].PadRight(maxLength + 1, '-'));
                stringBuilders[i].Append("--");
            }
        }

        var result = new StringBuilder();
        for (var i = 0; i < 6; i++)
        {
            stringBuilders[i].Append("|");
            result.AppendLine(stringBuilders[i].ToString());
        }

        return result.ToString();
    }

    private string GetStringName(int stringIdx) => stringIdx switch
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
