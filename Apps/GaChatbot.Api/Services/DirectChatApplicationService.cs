namespace GaChatbot.Api.Services;

using GA.Business.Core.Orchestration.Models;
using GA.Business.ML.Notation;
using GaChatbot.Api.Controllers;
using Microsoft.Extensions.AI;

public sealed class DirectChatApplicationService(
    IChatClient chatClient,
    IChatProviderReadinessProbe readinessProbe) : IChatApplicationService
{
    private static readonly AgentRoutingMetadata DirectRouting = new("direct", 1f, "direct-chat");

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
                ["chat.mode"] = "direct",
                ["history.turn_count"] = request.History?.Count ?? 0
            });

        using var llmStep = trace.StartStep(
            "gen_ai.chat",
            new Dictionary<string, object?>
            {
                ["gen_ai.system"] = "ollama",
                ["agent.id"] = DirectRouting.AgentId
            });
        var text = await GenerateAnswerAsync(request, cancellationToken);
        var notation = PlayableNotationFormatter.AugmentMarkdownWithVexTabFences(text);
        llmStep.Complete(
            finalAttributes: new Dictionary<string, object?>
            {
                ["response.length"] = notation.Text.Length
            });

        AddNotationTrace(trace, notation);

        return new ChatExecutionResult(notation.Text, DirectRouting, Grounding: null, trace.Build());
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
        CancellationToken cancellationToken)
    {
        var messages = new List<Microsoft.Extensions.AI.ChatMessage>
        {
            new(ChatRole.System,
                "You are Guitar Alchemist, a concise assistant for guitar, chords, voicings, and music theory. " +
                "Answer clearly and directly. If the user asks about music, stay grounded in practical musical guidance." +
                Environment.NewLine +
                Environment.NewLine +
                PlayableNotationFormatter.PromptGuidance)
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
        return response.Messages.LastOrDefault()?.Text?.Trim()
               ?? "I could not generate a response right now.";
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
