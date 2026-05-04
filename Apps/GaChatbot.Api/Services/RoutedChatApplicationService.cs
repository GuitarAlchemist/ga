namespace GaChatbot.Api.Services;

using GA.Business.ML.Notation;
using GaChatbot.Api.Controllers;
using Microsoft.Extensions.AI;

public sealed class RoutedChatApplicationService(
    IChatClient chatClient,
    IChatProviderReadinessProbe readinessProbe,
    LightweightChatRouter router,
    LightweightTheorySanityChecker theorySanityChecker) : IChatApplicationService
{
    public async Task<ChatExecutionResult> ChatAsync(
        ChatExecutionRequest request,
        CancellationToken cancellationToken = default)
    {
        var trace = new AgenticTraceBuilder($"run_{Guid.NewGuid():N}");
        trace.AddStep(
            "chat.request",
            "completed",
            0,
            new Dictionary<string, object?>
            {
                ["gen_ai.operation.name"] = "chat",
                ["chat.mode"] = "routed",
                ["history.turn_count"] = request.History?.Count ?? 0
            });

        LightweightRouteDecision decision;
        using (var routeStep = trace.StartStep("routing.route"))
        {
            decision = router.Route(request.Message, request.History);
            routeStep.Complete(
                finalAttributes: new Dictionary<string, object?>
                {
                    ["agent.id"] = decision.Routing.AgentId,
                    ["routing.method"] = decision.Routing.RoutingMethod,
                    ["routing.confidence"] = decision.Routing.Confidence,
                    ["prompt.profile"] = decision.PromptProfile.ToString()
                });
        }

        string text;
        using (var llmStep = trace.StartStep(
            "gen_ai.chat",
            new Dictionary<string, object?>
            {
                ["gen_ai.system"] = "ollama",
                ["agent.id"] = decision.Routing.AgentId
            }))
        {
            text = await GenerateAnswerAsync(request, decision.PromptProfile, cancellationToken);
            var notation = PlayableNotationFormatter.AugmentMarkdownWithVexTabFences(text);
            text = notation.Text;
            llmStep.Complete(
                finalAttributes: new Dictionary<string, object?>
                {
                    ["response.length"] = text.Length
                });
            AddNotationTrace(trace, notation);
        }

        return new ChatExecutionResult(text, decision.Routing, Grounding: null, trace.Build());
    }

    public async IAsyncEnumerable<ChatStreamUpdate> ChatStreamAsync(
        ChatExecutionRequest request,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var result = await ChatAsync(request, cancellationToken);
        yield return new ChatStreamUpdate(Routing: result.Routing, Grounding: result.Grounding, Trace: result.Trace);

        foreach (var chunk in Helpers.SseChunker.SplitIntoChunks(result.NaturalLanguageAnswer))
        {
            yield return new ChatStreamUpdate(Chunk: chunk);
        }

        yield return new ChatStreamUpdate(IsCompleted: true);
    }

    public Task<ChatbotStatus> GetStatusAsync(CancellationToken cancellationToken = default) =>
        readinessProbe.GetStatusAsync(cancellationToken);

    private async Task<string> GenerateAnswerAsync(
        ChatExecutionRequest request,
        PromptProfile promptProfile,
        CancellationToken cancellationToken)
    {
        var messages = new List<Microsoft.Extensions.AI.ChatMessage>
        {
            new(ChatRole.System, BuildSystemPrompt(promptProfile))
        };

        if (request.History is { Count: > 0 })
        {
            foreach (var turn in request.History)
            {
                var role = string.Equals(turn.Role, "assistant", StringComparison.OrdinalIgnoreCase)
                    ? ChatRole.Assistant
                    : ChatRole.User;
                messages.Add(new Microsoft.Extensions.AI.ChatMessage(role, turn.Content));
            }
        }

        messages.Add(new Microsoft.Extensions.AI.ChatMessage(ChatRole.User, request.Message));

        var response = await chatClient.GetResponseAsync(messages, cancellationToken: cancellationToken);
        var answer = response.Messages.LastOrDefault()?.Text?.Trim()
                     ?? "I could not generate a response right now.";
        return theorySanityChecker.Apply(request.Message, promptProfile, answer);
    }

    private static string BuildSystemPrompt(PromptProfile promptProfile)
    {
        var commonRules =
            """
            You are Guitar Alchemist.

            Global rules:
            - Be concise and practical.
            - Start with the direct answer, not scene-setting.
            - Prefer short paragraphs or short bullet lists.
            - Do not use filler, hype, or vague emotional descriptions.
            - Do not invent specific chord shapes, tabs, keys, or musical facts unless you can justify them from the user's prompt.
            - If the request is underspecified, say what assumption you are making.
            - Keep the answer useful for a guitarist, not academic for its own sake.
            """ +
            Environment.NewLine +
            Environment.NewLine +
            PlayableNotationFormatter.PromptGuidance;

        return promptProfile switch
        {
            PromptProfile.Theory =>
                commonRules +
                """

                Theory mode:
                - Explain scales, harmony, intervals, keys, cadences, substitutions, and voice leading clearly.
                - Use plain musical language.
                - When listing concepts, keep it to the essential distinctions.
                - If an example helps, give one compact example.
                - Avoid poetic descriptions like "sunny", "mystical", or "dreamy" unless the user asked for vibe language.
                """,

            PromptProfile.Voicing =>
                commonRules +
                """

                Voicing mode:
                - Focus on playable guitar chord shapes, inversions, fingering choices, range, and texture.
                - Prefer practical fretboard guidance over abstract theory.
                - If suggesting voicings without exact fret numbers, be explicit that you are describing the idea, not a verified shape.
                - Explain why a voicing works in one or two concrete musical points.
                """,

            PromptProfile.Tab =>
                commonRules +
                """

                Tab mode:
                - Help with riffs, tablature, fretboard positions, and turning ideas into playable guitar parts.
                - Be explicit about strings, frets, rhythm, or picking only when you actually know them.
                - If the user did not provide enough detail for exact tab, say so and offer a close playable outline instead.
                """,

            PromptProfile.Critic =>
                commonRules +
                """

                Critique mode:
                - Evaluate the progression or idea directly.
                - Say what works, what does not, and what to change.
                - Prefer specific musical fixes over generic advice.
                - Keep the tone direct and constructive.
                """,

            _ =>
                commonRules +
                """

                Direct mode:
                - Answer clearly and directly.
                - Stay grounded in practical guitar and music guidance.
                - Keep greetings brief.
                """
        };
    }

    private static void AddNotationTrace(AgenticTraceBuilder trace, NotationAugmentationResult notation) =>
        trace.AddStep(
            "notation.vextab",
            "completed",
            0,
            new Dictionary<string, object?>
            {
                ["notation.diagram.count"] = notation.DiagramCount,
                ["notation.vextab.added_count"] = notation.AddedFenceCount,
                ["notation.format"] = "vextab",
                ["notation.renderer"] = "vexflow"
            });
}
