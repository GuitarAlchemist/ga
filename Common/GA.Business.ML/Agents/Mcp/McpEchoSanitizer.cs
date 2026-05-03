namespace GA.Business.ML.Agents.Mcp;

/// <summary>
/// Shared input-sanitization helpers for MCP tool implementations.
/// </summary>
/// <remarks>
/// <para>Extracted from <see cref="IntervalMcpTools"/> and <see cref="ScaleMcpTools"/>
/// after both reviewers in PR #78 flagged the verbatim-duplicated <c>SanitizeEcho</c>
/// method. Hoisted before the third MCP tool lands so future tools share one
/// sanitization story rather than risking drift.</para>
///
/// <para>This class is deliberately <c>internal</c> — the sanitization contract
/// is part of the MCP-tool implementation surface, not something callers should
/// depend on or replicate. Tests reach it via the <c>InternalsVisibleTo</c>
/// declaration in <c>AssemblyInfo.cs</c>.</para>
/// </remarks>
internal static class McpEchoSanitizer
{
    /// <summary>
    /// Maximum echoed-input length. Long enough for legitimate diagnostics
    /// (note names, mode words, short phrases) without giving an attacker
    /// room to inject substantial prompt-injection payloads.
    /// </summary>
    private const int MaxEchoLength = 16;

    /// <summary>
    /// Sanitizes an echoed input string for inclusion in an MCP tool's structured
    /// <c>Error</c> field. Strips control characters (replaces each with U+00B7
    /// '·') and clamps to <see cref="MaxEchoLength"/> chars with an ellipsis.
    /// </summary>
    /// <remarks>
    /// Defends downstream renderers — log sinks, web UIs, the LLM's response
    /// pipeline — against newline-injection, ANSI-color-injection, and prompt-
    /// injection riding through a tool's <c>Error</c> echo.
    /// </remarks>
    public static string SanitizeEcho(string? raw)
    {
        if (string.IsNullOrEmpty(raw)) return string.Empty;
        var clamped = raw.Length > MaxEchoLength ? raw[..MaxEchoLength] + "…" : raw;
        var buf = new System.Text.StringBuilder(clamped.Length);
        foreach (var c in clamped)
            buf.Append(char.IsControl(c) ? '·' : c);
        return buf.ToString();
    }
}
