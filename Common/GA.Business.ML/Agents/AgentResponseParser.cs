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
    /// the first top-level <c>{...}</c> object after the opening markdown fence (if
    /// any), or, when that object is cut off, everything from its opening brace.
    /// Line breaks written literally inside its strings are escaped. Falls back to
    /// the fence-stripped text when there is no object at all.
    /// </summary>
    public static string ExtractJsonCandidate(string responseText)
    {
        if (string.IsNullOrEmpty(responseText))
            return responseText;

        // Look for the object after the opening fence, if any. Its end is found by
        // brace matching, not at the next ```, because the "result" string may
        // itself hold a fenced example (```vextab ... ```).
        var jsonFence = responseText.IndexOf("```json", StringComparison.Ordinal);
        var anyFence = responseText.IndexOf("```", StringComparison.Ordinal);
        var start = jsonFence >= 0 ? jsonFence + 7 : anyFence >= 0 ? anyFence + 3 : 0;

        // Models often emit a JSON object followed by extra prose
        // ("To distinguish these keys, listen for…"). Extract just the first
        // top-level {...} block so the trailing text doesn't break parsing.
        var firstBrace = responseText.IndexOf('{', start);
        if (firstBrace >= 0)
        {
            var lastBrace = FindMatchingBrace(responseText, firstBrace);
            var json = lastBrace > firstBrace
                ? responseText[firstBrace..(lastBrace + 1)]
                : responseText[firstBrace..];
            return EscapeControlCharactersInStrings(json);
        }

        return responseText[start..].Split("```")[0].Trim();
    }

    /// <summary>
    /// Escapes the line breaks and tabs that models write literally inside JSON
    /// strings (multi-line answers), which strict JSON rejects. Text outside
    /// string literals is unchanged.
    /// </summary>
    private static string EscapeControlCharactersInStrings(string json)
    {
        var escaped = new System.Text.StringBuilder(json.Length);
        var inString = false;
        var escape = false;
        foreach (var ch in json)
        {
            if (inString && !escape && ch < ' ')
            {
                escaped.Append(ch switch
                {
                    '\n' => "\\n",
                    '\r' => "\\r",
                    '\t' => "\\t",
                    _ => $"\\u{(int)ch:x4}",
                });
                continue;
            }

            escaped.Append(ch);
            if (escape) escape = false;
            else if (inString && ch == '\\') escape = true;
            else if (ch == '"') inString = !inString;
        }

        return escaped.ToString();
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
    /// Returns the prose of <paramref name="responseText"/> with the JSON removed,
    /// so a failed parse never shows raw JSON to the user: a fenced block (closed
    /// or not) that holds JSON is dropped, and so is any JSON object (balanced or
    /// cut off) outside a fence. Other fenced blocks, such as a <c>```vextab</c>
    /// example, are kept verbatim. Empty when nothing is left.
    /// </summary>
    public static string ProseOutsideJson(string responseText)
    {
        if (string.IsNullOrEmpty(responseText))
            return string.Empty;

        var kept = new System.Text.StringBuilder();
        var position = 0;
        while (position < responseText.Length)
        {
            var fence = responseText.IndexOf("```", position, StringComparison.Ordinal);
            if (fence < 0)
            {
                kept.Append(WithoutJsonObjects(responseText[position..]));
                break;
            }

            kept.Append(WithoutJsonObjects(responseText[position..fence]));
            var close = responseText.IndexOf("```", fence + 3, StringComparison.Ordinal);
            var end = close < 0 ? responseText.Length : close + 3;
            var block = responseText[fence..end];
            if (!IsJsonFence(block))
                kept.Append(block);
            position = end;
        }

        return kept.ToString().Trim();
    }

    /// <summary>
    /// A fenced block holds JSON when its info string is <c>json</c> or its body
    /// starts with <c>{</c> or <c>[</c>.
    /// </summary>
    private static bool IsJsonFence(string block)
    {
        var body = block[3..];
        if (body.EndsWith("```", StringComparison.Ordinal))
            body = body[..^3];
        var newline = body.IndexOf('\n');
        var info = newline < 0 ? string.Empty : body[..newline].Trim();
        if (info.Equals("json", StringComparison.OrdinalIgnoreCase))
            return true;

        var content = (newline < 0 ? body : body[(newline + 1)..]).TrimStart();
        return content.StartsWith('{') || content.StartsWith('[') || info.StartsWith('{');
    }

    /// <summary>
    /// Removes JSON objects (balanced or cut off) from <paramref name="text"/>. A
    /// <c>{</c> counts as a JSON object only when a key (<c>"</c>), a closing
    /// <c>}</c> or the end of the text follows it, so brace notation such as the
    /// pitch-class set <c>{0, 4, 7}</c> is kept.
    /// </summary>
    private static string WithoutJsonObjects(string text)
    {
        var brace = text.IndexOf('{');
        while (brace >= 0)
        {
            if (!StartsJsonObject(text, brace))
            {
                brace = text.IndexOf('{', brace + 1);
                continue;
            }

            var end = FindMatchingBrace(text, brace);
            text = end < 0 ? text[..brace] : text[..brace] + text[(end + 1)..];
            brace = text.IndexOf('{', brace);
        }

        return text;
    }

    private static bool StartsJsonObject(string text, int openIndex)
    {
        var next = openIndex + 1;
        while (next < text.Length && char.IsWhiteSpace(text[next]))
            next++;
        return next == text.Length || text[next] is '"' or '}';
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
