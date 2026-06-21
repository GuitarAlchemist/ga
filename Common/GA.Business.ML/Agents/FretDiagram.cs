namespace GA.Business.ML.Agents;

using System.Text.RegularExpressions;

/// <summary>
///     Shared 6-string fret-diagram parser — the single seam both <see cref="Mcp.FretSpanMcpTools"/>
///     (MCP transport adapter) and <see cref="Skills.FretSpanSkill"/> (orchestrator adapter) cross.
/// </summary>
/// <remarks>
///     Parsing previously lived in both adapters, and their compact-form regexes had drifted: the
///     skill required a leading <c>x</c> (<c>\b[xX]\d{5}\b</c>) and silently rejected very common
///     voicings like <c>032010</c> (G), <c>355463</c> (Ab barre), <c>133211</c> (F barre), while the
///     MCP tool accepted them (PR #83). Consolidating here makes the two adapters parse identically.
///     Tokens are low-to-high (E A D G B e); <c>-1</c> = muted, <c>0</c> = open, otherwise the fret.
/// </remarks>
public static partial class FretDiagram
{
    /// <summary>
    ///     True if the message contains a dash-separated (<c>x-3-2-0-1-0</c>) or compact
    ///     (<c>x32010</c>, <c>032010</c>) 6-string diagram.
    /// </summary>
    public static bool Contains(string message) =>
        DashDiagramRegex().IsMatch(message) || CompactDiagramRegex().IsMatch(message);

    /// <summary>
    ///     Parses the 6 fret values from the first diagram in <paramref name="message"/>, or returns
    ///     null if none is present. Dash form supports two-digit frets; compact form is six single
    ///     characters (<c>x</c>/<c>X</c>/<c>0</c>-<c>9</c>).
    /// </summary>
    public static List<int>? TryParseFrets(string message)
    {
        var dash = DashDiagramRegex().Match(message);
        if (dash.Success)
        {
            return Enumerable.Range(1, 6)
                .Select(i => ParseToken(dash.Groups[i].Value))
                .ToList();
        }

        var compact = CompactDiagramRegex().Match(message);
        if (!compact.Success)
        {
            return null;
        }

        return compact.Value.Select(c => c is 'x' or 'X' ? -1 : c - '0').ToList();
    }

    private static int ParseToken(string s) =>
        s.Equals("x", StringComparison.OrdinalIgnoreCase) ? -1 : int.Parse(s);

    [GeneratedRegex(
        @"\b([xX]|\d{1,2})-([xX]|\d{1,2})-([xX]|\d{1,2})-([xX]|\d{1,2})-([xX]|\d{1,2})-([xX]|\d{1,2})\b",
        RegexOptions.CultureInvariant)]
    private static partial Regex DashDiagramRegex();

    // Compact form: 6 chars, each x/X or a single digit. Two-digit frets (e.g. fret 12) are not
    // expressible compactly — those use the dash form.
    [GeneratedRegex(@"\b[xX0-9]{6}\b", RegexOptions.CultureInvariant)]
    private static partial Regex CompactDiagramRegex();
}
