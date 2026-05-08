namespace GaApi.Controllers;

using System.ComponentModel.DataAnnotations;
using GA.Business.Core.Orchestration.Models;
using GA.Business.Core.Orchestration.Trace;
using Services;

public class ChatRequest
{
    [Required] [MaxLength(2000)] public string Message { get; set; } = string.Empty;

    public List<ChatMessage>? ConversationHistory { get; set; }
    public bool UseSemanticSearch { get; set; } = true;
}

/// <summary>Non-streaming JSON response returned by POST /api/chatbot/chat.</summary>
/// <remarks>
/// <see cref="Trace"/> mirrors GaChatbot.Api's <c>ChatJsonResponse.Trace</c> shape so
/// any client written against either host gets the same observability contract.
/// Roadmap P1 #7 commit 1 — codex CLI 2026-05-08 design review.
/// </remarks>
public record ChatJsonResponse(
    string NaturalLanguageAnswer,
    string AgentId,
    float Confidence,
    string RoutingMethod,
    GroundingMetadata? Grounding = null,
    long ElapsedMs = 0,
    string? TraceId = null,
    AgenticTrace? Trace = null);
