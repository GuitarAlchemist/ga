namespace GaChatbot.Api.Services;

using System.Text.RegularExpressions;
using GA.Business.Core.Orchestration.Models;

public sealed class LightweightChatRouter
{
    private static readonly string[] GreetingKeywords =
    [
        "hello", "hi", "hey", "yo", "good morning", "good afternoon", "good evening"
    ];

    private static readonly string[] TheoryKeywords =
    [
        "theory", "mode", "modes", "scale", "scales", "key", "keys", "chord", "chords",
        "triad", "triads", "harmony", "cadence", "roman numeral", "roman numerals",
        "interval", "intervals", "voice leading",
        "harmonic function", "substitution", "modulation"
    ];

    private static readonly string[] VoicingKeywords =
    [
        "voicing", "voicings", "chord shape", "chord shapes", "fingering", "fingering",
        "drop 2", "drop2", "drop 3", "drop3", "rootless", "shell voicing", "shell",
        "inversion", "barre", "open chord", "grip"
    ];

    private static readonly string[] TabKeywords =
    [
        "tab", "tabs", "tablature", "riff", "lick", "fret numbers", "string numbers",
        "transcribe", "transcription", "play this on guitar"
    ];

    private static readonly string[] CriticKeywords =
    [
        "critique", "critic", "improve this", "improve my", "analyze this progression",
        "why does this sound bad", "make this better", "what is wrong with", "rewrite this progression"
    ];

    public LightweightRouteDecision Route(string message) =>
        Route(message, history: null);

    public LightweightRouteDecision Route(
        string message,
        IReadOnlyList<ConversationTurn>? history)
    {
        var text = message.Trim().ToLowerInvariant();

        if (ContainsGreeting(text))
        {
            return new LightweightRouteDecision(
                new AgentRoutingMetadata("direct", 0.98f, "lightweight-router"),
                PromptProfile.Direct);
        }

        if (ContainsAny(text, VoicingKeywords))
        {
            return new LightweightRouteDecision(
                new AgentRoutingMetadata("voicing-lite", 0.85f, "lightweight-router"),
                PromptProfile.Voicing);
        }

        if (ContainsAny(text, TheoryKeywords))
        {
            return new LightweightRouteDecision(
                new AgentRoutingMetadata("theory-lite", 0.83f, "lightweight-router"),
                PromptProfile.Theory);
        }

        if (ContainsAny(text, TabKeywords))
        {
            return new LightweightRouteDecision(
                new AgentRoutingMetadata("tab-lite", 0.8f, "lightweight-router"),
                PromptProfile.Tab);
        }

        if (ContainsAny(text, CriticKeywords))
        {
            return new LightweightRouteDecision(
                new AgentRoutingMetadata("critic-lite", 0.78f, "lightweight-router"),
                PromptProfile.Critic);
        }

        var contextualDecision = RouteFollowUpFromHistory(text, history);
        if (contextualDecision is not null)
        {
            return contextualDecision;
        }

        return new LightweightRouteDecision(
            new AgentRoutingMetadata("direct", 0.6f, "lightweight-router"),
            PromptProfile.Direct);
    }

    private static LightweightRouteDecision? RouteFollowUpFromHistory(
        string text,
        IReadOnlyList<ConversationTurn>? history)
    {
        if (!IsFollowUp(text) || history is null || history.Count == 0)
        {
            return null;
        }

        var context = string.Join(
            ' ',
            history
                .Where(turn => !string.IsNullOrWhiteSpace(turn.Content))
                .TakeLast(4)
                .Select(turn => turn.Content.ToLowerInvariant()));

        if (ContainsAny(context, VoicingKeywords))
        {
            return new LightweightRouteDecision(
                new AgentRoutingMetadata("voicing-lite", 0.68f, "lightweight-router-context"),
                PromptProfile.Voicing);
        }

        if (ContainsAny(context, TheoryKeywords))
        {
            return new LightweightRouteDecision(
                new AgentRoutingMetadata("theory-lite", 0.66f, "lightweight-router-context"),
                PromptProfile.Theory);
        }

        if (ContainsAny(context, TabKeywords))
        {
            return new LightweightRouteDecision(
                new AgentRoutingMetadata("tab-lite", 0.64f, "lightweight-router-context"),
                PromptProfile.Tab);
        }

        if (ContainsAny(context, CriticKeywords))
        {
            return new LightweightRouteDecision(
                new AgentRoutingMetadata("critic-lite", 0.62f, "lightweight-router-context"),
                PromptProfile.Critic);
        }

        return null;
    }

    private static bool ContainsAny(string text, IEnumerable<string> keywords) =>
        keywords.Any(keyword => text.Contains(keyword, StringComparison.OrdinalIgnoreCase));

    private static bool ContainsGreeting(string text) =>
        GreetingKeywords.Any(keyword =>
            Regex.IsMatch(
                text,
                $@"(?<![a-z0-9]){Regex.Escape(keyword)}(?![a-z0-9])",
                RegexOptions.CultureInvariant | RegexOptions.IgnoreCase));

    private static bool IsFollowUp(string text) =>
        text.Length <= 160
        && (Regex.IsMatch(
                text,
                @"\b(what about|how about|and|also|same|that|those|it|them|another|other|compare|continue|expand|minor|major)\b",
                RegexOptions.CultureInvariant | RegexOptions.IgnoreCase)
            || text.EndsWith('?'));
}

public sealed record LightweightRouteDecision(
    AgentRoutingMetadata Routing,
    PromptProfile PromptProfile);

public enum PromptProfile
{
    Direct,
    Theory,
    Voicing,
    Tab,
    Critic
}
