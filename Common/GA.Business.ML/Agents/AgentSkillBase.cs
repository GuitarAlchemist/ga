namespace GA.Business.ML.Agents;

using System.Diagnostics;
using System.Text.Json;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

/// <summary>
/// Base class for agent skills. Provides LLM access and structured response parsing
/// so skills can combine domain computation with LLM explanation.
/// </summary>
/// <remarks>
/// Implements both <see cref="IAgentSkill"/> and <see cref="IOrchestratorSkill"/>.
/// Subclasses implement the string-based <c>CanHandle</c> and <c>ExecuteAsync</c> overloads.
/// The <see cref="IAgentSkill"/> adapters (which take <see cref="AgentRequest"/>) are sealed
/// in this base class — no boilerplate needed in concrete skills.
/// </remarks>
public abstract class AgentSkillBase(string agentId, IChatClient chatClient, ILogger logger)
    : IAgentSkill, IOrchestratorSkill
{
    private static readonly JsonSerializerOptions JsonOptions =
        new() { PropertyNameCaseInsensitive = true };

    protected readonly string AgentId = agentId;
    protected readonly IChatClient ChatClient = chatClient;
    protected readonly ILogger Logger = logger;

    public abstract string Name { get; }
    public abstract string Description { get; }

    // ── IOrchestratorSkill — implement in subclasses ──────────────────────────

    public abstract bool CanHandle(string message);
    public abstract Task<AgentResponse> ExecuteAsync(string message, CancellationToken cancellationToken = default);

    // ── IAgentSkill adapters — sealed here, delegate to string overloads ──────

    bool IAgentSkill.CanHandle(AgentRequest request) => CanHandle(request.Query);

    Task<AgentResponse> IAgentSkill.ExecuteAsync(
        AgentRequest request,
        CancellationToken cancellationToken) =>
        ExecuteAsync(request.Query, cancellationToken);

    // ── Helpers ───────────────────────────────────────────────────────────────

    /// <summary>Calls the LLM with a system prompt and user message.</summary>
    protected async Task<string> ChatAsync(
        string userMessage,
        string systemPrompt,
        CancellationToken cancellationToken = default)
    {
        using var activity = ChatbotActivitySource.Source.StartActivity(ChatbotActivitySource.AgentChat);
        activity?.SetTag(ChatbotActivitySource.TagAgentId, AgentId);
        activity?.SetTag("skill.name", Name);

        var messages = new List<ChatMessage>
        {
            new(ChatRole.System, systemPrompt),
            new(ChatRole.User, userMessage)
        };

        var sw = Stopwatch.StartNew();
        var response = await ChatClient.GetResponseAsync(messages, cancellationToken: cancellationToken);
        sw.Stop();

        var text = response.Messages.LastOrDefault()?.Text ?? "";
        activity?.SetTag("llm.response_ms", sw.ElapsedMilliseconds);
        Logger.LogDebug("Skill '{Skill}' LLM call: {Ms}ms", Name, sw.ElapsedMilliseconds);
        return text;
    }

    /// <summary>Parses a structured JSON response, falling back to plain text on failure.</summary>
    protected AgentResponse ParseStructuredResponse(string responseText, string fallbackResult)
    {
        try
        {
            var json = responseText;
            if (json.Contains("```json"))
                json = json.Split("```json")[1].Split("```")[0].Trim();
            else if (json.Contains("```"))
                json = json.Split("```")[1].Split("```")[0].Trim();

            var structured = JsonSerializer.Deserialize<StructuredAgentResponse>(json, JsonOptions);
            if (structured != null)
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
        catch (Exception ex)
        {
            Logger.LogWarning(ex, "Skill '{Skill}' failed to parse JSON response; using fallback.", Name);
        }

        return new AgentResponse
        {
            AgentId = AgentId,
            Result = responseText.Length > 0 ? responseText : fallbackResult,
            Confidence = 0.5f,
            Evidence = ["Fallback used — JSON parse failed"],
            Assumptions = ["Response was not in expected JSON format"]
        };
    }
}
