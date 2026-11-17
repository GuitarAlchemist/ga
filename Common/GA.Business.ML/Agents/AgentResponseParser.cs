namespace GA.Business.ML.Agents;

/// <summary>
/// Single home for extracting a JSON object out of a free-form LLM response —
/// stripping markdown code fences and narrowing to the first balanced top-level
/// <c>{...}</c> object so trailing prose doesn't make the strict deserializer throw.
/// </summary>
/// <remarks>
/// Previously duplicated character-for-character in <see cref="GuitarAlchemistAgentBase"/>
/// and <see cref="AgentSkillBase"/>. The "trailing prose regression" (the
/// "Identify the key of Am F C G" showcase bug, 2026-05-16) had to be fixed in
/// both copies independently; concentrating the logic here means the next fix
/// lands once. Callers keep their own deserialize + fallback mapping — only the
/// fence/brace extraction is shared.
/// </remarks>
internal static class AgentResponseParser
{
    /// <summary>
    /// Returns the best JSON candidate string from <paramref name="responseText"/>:
    /// markdown fences removed, and — when a balanced top-level object is present —
    /// narrowed to just that <c>{...}</c> block. Falls back to the fence-stripped
    /// text when no balanced object is found (preserving the prior behaviour where
    /// the deserializer still gets a chance on the stripped text).
    /// </summary>
    public static string ExtractJsonCandidate(string responseText)
    {
        if (string.IsNullOrEmpty(responseText))
            return responseText;

        var json = responseText;

        // Strip code fences first.
        if (json.Contains("```json"))
            json = json.Split("```json")[1].Split("```")[0].Trim();
        else if (json.Contains("```"))
            json = json.Split("```")[1].Split("```")[0].Trim();

        // Models often emit a JSON object followed by extra prose
        // ("To distinguish these keys, listen for…"). Extract just the first
        // top-level {...} block so the trailing text doesn't break parsing.
        var firstBrace = json.IndexOf('{');
        if (firstBrace >= 0)
        {
            var lastBrace = FindMatchingBrace(json, firstBrace);
            if (lastBrace > firstBrace)
                json = json.Substring(firstBrace, lastBrace - firstBrace + 1);
        }

        return json;
    }

    /// <summary>
    /// Reads the top-level <c>"result"</c> string out of a JSON object that may be
    /// cut off later on (a response truncated at the output-token limit). Returns
    /// <see langword="null"/> when the value is missing or itself incomplete.
    /// </summary>
    public static string? TryReadPartialResult(string responseText)
    {
        if (string.IsNullOrEmpty(responseText))
            return null;

        var candidate = ExtractJsonCandidate(responseText);
        var firstBrace = candidate.IndexOf('{');
        if (firstBrace < 0)
            return null;

        var reader = new System.Text.Json.Utf8JsonReader(
            System.Text.Encoding.UTF8.GetBytes(candidate[firstBrace..]),
            isFinalBlock: false,
            state: default);
        try
        {
            while (reader.Read())
            {
                if (reader.CurrentDepth == 1
                    && reader.TokenType == System.Text.Json.JsonTokenType.PropertyName
                    && reader.ValueTextEquals("result"))
                {
                    return reader.Read() && reader.TokenType == System.Text.Json.JsonTokenType.String
                        ? reader.GetString()
                        : null;
                }
            }
        }
        catch (System.Text.Json.JsonException)
        {
            // Not JSON after all; the caller falls back to the prose.
        }

        return null;
    }

    /// <summary>
    /// Returns the prose of <paramref name="responseText"/> with any fenced block
    /// (closed or not) and any JSON object (balanced or cut off) removed, so a
    /// failed parse never shows raw JSON to the user. Empty when nothing is left.
    /// </summary>
    public static string ProseOutsideJson(string responseText)
    {
        if (string.IsNullOrEmpty(responseText))
            return string.Empty;

        var text = responseText;
        int fence;
        while ((fence = text.IndexOf("```", StringComparison.Ordinal)) >= 0)
        {
            var close = text.IndexOf("```", fence + 3, StringComparison.Ordinal);
            text = close < 0 ? text[..fence] : text[..fence] + text[(close + 3)..];
        }

        int brace;
        while ((brace = text.IndexOf('{')) >= 0)
        {
            var end = FindMatchingBrace(text, brace);
            text = end < 0 ? text[..brace] : text[..brace] + text[(end + 1)..];
        }

        return text.Trim();
    }

    /// <summary>
    /// Returns the index of the <c>}</c> that closes the <c>{</c> at
    /// <paramref name="openIndex"/>, respecting string literals and nested braces.
    /// Returns -1 if no balanced match.
    /// </summary>
    private static int FindMatchingBrace(string s, int openIndex)
    {
        var depth = 0;
        var inString = false;
        var escape = false;
        for (var i = openIndex; i < s.Length; i++)
        {
            var ch = s[i];
            if (inString)
            {
                if (escape) { escape = false; }
                else if (ch == '\\') { escape = true; }
                else if (ch == '"') { inString = false; }
                continue;
            }
            switch (ch)
            {
                case '"': inString = true; break;
                case '{': depth++; break;
                case '}':
                    depth--;
                    if (depth == 0) return i;
                    break;
            }
        }
        return -1;
    }
}
