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
