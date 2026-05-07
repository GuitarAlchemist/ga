namespace GaApi.Controllers;

using System.ComponentModel.DataAnnotations;
using GA.Business.Core.Orchestration.Models;
using Services;

public class ChatRequest
{
    [Required] [MaxLength(2000)] public string Message { get; set; } = string.Empty;

    public List<ChatMessage>? ConversationHistory { get; set; }
    public bool UseSemanticSearch { get; set; } = true;
}

/// <summary>Non-streaming JSON response returned by POST /api/chatbot/chat.</summary>
public record ChatJsonResponse(
    string NaturalLanguageAnswer,
    string AgentId,
    float Confidence,
    string RoutingMethod,
    GroundingMetadata? Grounding = null,
    long ElapsedMs = 0,
    string? TraceId = null);
