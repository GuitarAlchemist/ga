namespace GA.Business.ML.Agents;

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

/// <summary>
/// Base class for all Guitar Alchemist specialized agents.
/// </summary>
/// <remarks>
/// <para>
/// Each agent specializes in a specific domain (tabs, theory, technique, composition, critique)
/// and produces structured responses with confidence scores and evidence.
/// </para>
/// <para>
/// Agents use the MEAI <see cref="IChatClient"/> for LLM interactions and can be orchestrated
/// by a <see cref="SemanticRouter"/> for intelligent request routing.
/// </para>
/// </remarks>
public abstract class GuitarAlchemistAgentBase(IChatClient chatClient, ILogger logger) : IDisposable
{
    protected readonly IChatClient ChatClient = chatClient ?? throw new ArgumentNullException(nameof(chatClient));
    protected readonly ILogger Logger = logger ?? throw new ArgumentNullException(nameof(logger));

    /// <summary>
    /// Gets the unique identifier for this agent type.
    /// </summary>
    public abstract string AgentId { get; }

    /// <summary>
    /// Gets a human-readable name for this agent.
    /// </summary>
    public abstract string Name { get; }

    /// <summary>
    /// Gets a description of what this agent does (used for semantic routing).
    /// </summary>
    public abstract string Description { get; }

    /// <summary>
    /// Gets the domains/topics this agent handles (used for semantic routing).
    /// </summary>
    public abstract IReadOnlyList<string> Capabilities { get; }

    /// <summary>
    /// Optional cross-agent delegation coordinator (property-injected to avoid circular DI).
    /// </summary>
    public IAgentCoordinator? Coordinator { get; set; }

    /// <summary>
    /// Processes a user request and returns a structured response.
    /// </summary>
    /// <param name="request">The agent request containing the user's query.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A structured agent response with confidence and evidence.</returns>
    public abstract Task<AgentResponse> ProcessAsync(
        AgentRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Builds the system prompt for this agent.
    /// </summary>
    protected virtual string BuildSystemPrompt() => $"""
            You are {Name}, a specialized AI agent for Guitar Alchemist.

            Your role: {Description}

            Capabilities: {string.Join(", ", Capabilities)}

            Guidelines:
            - Provide accurate, evidence-based responses about guitar and music theory
            - When uncertain, express your confidence level honestly
            - Cite specific music theory concepts or techniques when applicable
            - If a request falls outside your expertise, indicate this clearly
            """;

    /// <summary>
    /// Delegates a sub-query to another agent via the coordinator.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown if no coordinator is wired.</exception>
    protected Task<AgentResponse> DelegateToAsync(string query, string? agentId = null, CancellationToken ct = default)
        => Coordinator?.DelegateAsync(query, agentId, ct)
           ?? throw new InvalidOperationException("No IAgentCoordinator wired — cannot delegate.");

    /// <summary>
    /// Sends a chat request to the LLM.
    /// </summary>
    protected async Task<string> ChatAsync(
        string userMessage,
        string? systemPrompt = null,
        CancellationToken cancellationToken = default,
        IReadOnlyList<ChatHistoryTurn>? conversationHistory = null)
    {
        var messages = new List<ChatMessage>();

        var prompt = systemPrompt ?? BuildSystemPrompt();
        messages.Add(new ChatMessage(ChatRole.System, prompt));

        // Inject conversation history between system and user message
        if (conversationHistory is { Count: > 0 })
        {
            foreach (var turn in conversationHistory)
            {
                var role = turn.Role.Equals("assistant", StringComparison.OrdinalIgnoreCase)
                    ? ChatRole.Assistant
                    : ChatRole.User;
                messages.Add(new ChatMessage(role, turn.Content));
            }
        }

        messages.Add(new ChatMessage(ChatRole.User, userMessage));

        Logger.LogDebug("Agent {AgentId} processing request: {Message}", AgentId, userMessage[..Math.Min(100, userMessage.Length)]);

        using var chatActivity = ChatbotActivitySource.Source.StartActivity(ChatbotActivitySource.AgentChat);
        chatActivity?.SetTag(ChatbotActivitySource.TagAgentId, AgentId);
        chatActivity?.SetTag(ChatbotActivitySource.TagQueryLength, userMessage.Length);

        var sw = Stopwatch.StartNew();
        var response = await ChatClient.GetResponseAsync(messages, cancellationToken: cancellationToken);
        sw.Stop();

        var responseText = response.Messages.LastOrDefault()?.Text ?? "";
        chatActivity?.SetTag("llm.response_ms", sw.ElapsedMilliseconds);
        chatActivity?.SetTag("llm.response_length", responseText.Length);

        Logger.LogDebug("Agent {AgentId} LLM call took {Ms}ms, response length {Len}", AgentId, sw.ElapsedMilliseconds, responseText.Length);
        Logger.LogDebug("Agent {AgentId} response: {Response}", AgentId, responseText[..Math.Min(100, responseText.Length)]);

        return responseText;
    }

    /// <summary>
    /// Sends a chat request through three passes: initial draft → self-critique → refined answer.
    /// Use for complex theory questions where accuracy matters more than latency.
    /// </summary>
    protected async Task<string> ChatWithCritiqueAsync(
        string userMessage,
        string? systemPrompt = null,
        CancellationToken cancellationToken = default)
    {
        using var critiqueActivity = ChatbotActivitySource.Source.StartActivity(ChatbotActivitySource.AgentChatWithCritique);
        critiqueActivity?.SetTag(ChatbotActivitySource.TagAgentId, AgentId);

        var draft = await ChatAsync(userMessage, systemPrompt, cancellationToken);

        var critiqueMessage = $"""
            I gave this answer to a music theory question. Identify any errors, missing context, or improvements. Be concise.

            QUESTION: {userMessage}
            ANSWER: {draft}
            """;
        var critique = await ChatAsync(critiqueMessage, systemPrompt, cancellationToken);

        var refineMessage = $"""
            Rewrite the answer incorporating the critique. Return valid JSON in the same format as before.

            QUESTION: {userMessage}
            ORIGINAL ANSWER: {draft}
            CRITIQUE: {critique}
            """;
        return await ChatAsync(refineMessage, systemPrompt, cancellationToken);
    }

    /// <summary>
    /// Streams a chat request to the LLM token-by-token using <see cref="IChatClient.GetStreamingResponseAsync"/>.
    /// </summary>
    /// <param name="userMessage">The user's message.</param>
    /// <param name="systemPrompt">Optional system prompt override; defaults to <see cref="BuildSystemPrompt"/>.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>An async enumerable that yields each text token as it arrives from the LLM.</returns>
    public async IAsyncEnumerable<string> ProcessStreamingAsync(
        string userMessage,
        string? systemPrompt = null,
        IReadOnlyList<ChatHistoryTurn>? conversationHistory = null,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var messages = new List<ChatMessage>();

        var prompt = systemPrompt ?? BuildSystemPrompt();
        messages.Add(new ChatMessage(ChatRole.System, prompt));

        if (conversationHistory is { Count: > 0 })
        {
            foreach (var turn in conversationHistory)
            {
                var role = turn.Role.Equals("assistant", StringComparison.OrdinalIgnoreCase)
                    ? ChatRole.Assistant
                    : ChatRole.User;
                messages.Add(new ChatMessage(role, turn.Content));
            }
        }

        messages.Add(new ChatMessage(ChatRole.User, userMessage));

        Logger.LogDebug("Agent {AgentId} streaming request: {Message}", AgentId, userMessage[..Math.Min(100, userMessage.Length)]);

        await foreach (var update in ChatClient.GetStreamingResponseAsync(messages, cancellationToken: cancellationToken))
        {
            var text = update.Text;
            if (!string.IsNullOrEmpty(text))
                yield return text;
        }
    }

    /// <summary>
    /// Parses a JSON response from the LLM into a structured agent response.
    /// </summary>
    protected AgentResponse ParseStructuredResponse(string responseText, string fallbackResult)
    {
        try
        {
            // Extract JSON if it's wrapped in markdown code blocks
            var json = responseText;
            if (json.Contains("```json"))
            {
                json = json.Split("```json")[1].Split("```")[0].Trim();
            }
            else if (json.Contains("```"))
            {
                json = json.Split("```")[1].Split("```")[0].Trim();
            }

            var structured = JsonSerializer.Deserialize<StructuredAgentResponse>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (structured != null)
            {
                return new AgentResponse
                {
                    AgentId = AgentId,
                    Result = structured.Result,
                    Confidence = structured.Confidence,
                    Evidence = structured.Evidence,
                    Assumptions = structured.Assumptions,
                    Data = structured.Data
                };
            }
        }
        catch (Exception ex)
        {
            Logger.LogWarning(ex, "Failed to parse structured JSON response from agent {AgentId}. Falling back to text.", AgentId);
        }

        return new AgentResponse
        {
            AgentId = AgentId,
            Result = responseText.Length > 0 ? responseText : fallbackResult,
            Confidence = 0.5f,
            Evidence = ["Fallback logic used due to parsing failure"],
            Assumptions = ["The agent response was not in the expected JSON format"]
        };
    }

    /// <inheritdoc />
    public virtual void Dispose()
    {
        // Subclasses may override to dispose resources
    }
}

/// <summary>
/// A single turn in a multi-turn conversation, used for history injection into agents.
/// </summary>
public sealed record ChatHistoryTurn(string Role, string Content);

/// <summary>
/// Represents a request to an agent.
/// </summary>
public record AgentRequest
{
    /// <summary>
    /// Gets or sets the user's query.
    /// </summary>
    public required string Query { get; init; }

    /// <summary>
    /// Gets or sets optional context about the current musical situation.
    /// </summary>
    public string? Context { get; init; }

    /// <summary>
    /// Gets or sets optional related voicing IDs for reference.
    /// </summary>
    public IReadOnlyList<string>? RelatedVoicingIds { get; init; }

    /// <summary>
    /// Gets or sets any key/value metadata.
    /// </summary>
    public IReadOnlyDictionary<string, object>? Metadata { get; init; }

    /// <summary>
    /// Optional multi-turn conversation history for context-aware responses.
    /// </summary>
    public IReadOnlyList<ChatHistoryTurn>? ConversationHistory { get; init; }
}

/// <summary>
/// Internally used to parse structured the LLM JSON response.
/// </summary>
public record StructuredAgentResponse
{
    public required string Result { get; init; }
    public float Confidence { get; init; } = 0.85f;
    public List<string> Evidence { get; init; } = [];
    public List<string> Assumptions { get; init; } = [];
    public object? Data { get; init; }
}

/// <summary>
/// Represents a structured response from an agent.
/// </summary>
public record AgentResponse
{
    /// <summary>
    /// Gets or sets the main result/answer from the agent.
    /// </summary>
    public required string Result { get; init; }

    /// <summary>
    /// Gets or sets the confidence score (0.0 to 1.0).
    /// </summary>
    public required float Confidence { get; init; }

    /// <summary>
    /// Gets or sets evidence supporting the result.
    /// </summary>
    public IReadOnlyList<string> Evidence { get; init; } = [];

    /// <summary>
    /// Gets or sets assumptions made by the agent.
    /// </summary>
    public IReadOnlyList<string> Assumptions { get; init; } = [];

    /// <summary>
    /// Gets or sets the agent that produced this response.
    /// </summary>
    public required string AgentId { get; init; }

    /// <summary>
    /// Gets or sets any structured data returned.
    /// </summary>
    public object? Data { get; init; }

    /// <summary>
    /// Creates a low-confidence response indicating the agent cannot help.
    /// </summary>
    public static AgentResponse CannotHelp(string agentId, string reason) => new()
    {
        AgentId = agentId,
        Result = reason,
        Confidence = 0.0f,
        Evidence = [],
        Assumptions = ["Request falls outside agent's domain"]
    };
}
