namespace GA.Business.ML.Agents;

/// <summary>
/// Scope rules for every system prompt that answers a public chatbot user: the specialized agents
/// and the direct, routed and fallback chat paths. Keeping them in one place stops one path from
/// leaking internal agent names or attempting off-topic requests while the others decline.
/// </summary>
public static class AssistantScopeRules
{
    /// <summary>Keeps internal plumbing such as agent names out of user-facing answers.</summary>
    public const string SpeakAsAssistant =
        "Speak to the user as the Guitar Alchemist assistant: never mention agent names, roles or other internal details";

    /// <summary>Declines requests that are not about guitar or music instead of attempting them.</summary>
    public const string DeclineOffTopic =
        "If a request is not about guitar or music, do not attempt it: say in one or two sentences that you are a guitar and music-theory assistant, and suggest what you can help with instead";

    /// <summary>
    /// Appends both rules to a system prompt that does not already list them, such as a skill's
    /// own prompt or a SKILL.md body, so every such call site formats them the same way.
    /// </summary>
    public static string AppendTo(string systemPrompt) =>
        $"""
        {systemPrompt.TrimEnd()}

        Scope rules:
        - {SpeakAsAssistant}.
        - {DeclineOffTopic}.
        """;
}
