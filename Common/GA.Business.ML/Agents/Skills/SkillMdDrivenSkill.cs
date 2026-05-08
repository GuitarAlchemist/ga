namespace GA.Business.ML.Agents.Skills;

using System.Diagnostics;
using GA.Business.ML.Agents.Plugins;
using GA.Business.ML.Extensions;
using GA.Business.ML.Skills;
using Microsoft.Extensions.AI;

/// <summary>
/// <see cref="IOrchestratorSkill"/> backed by a SKILL.md file.
/// Uses an <see cref="IChatClient"/> resolved via <see cref="IChatClientFactory"/>
/// for the <c>skill-md</c> purpose (provider chosen by configuration), with GA
/// domain tools supplied by <see cref="IMcpToolsProvider"/>.
/// <see cref="FunctionInvokingChatClientBuilderExtensions.UseFunctionInvocation"/>
/// (applied inside the factory) handles the full multi-turn agentic tool-use loop
/// automatically — no hand-coded loop required.
/// </summary>
/// <remarks>
/// This type intentionally references no provider SDK; vendor specifics live
/// inside <c>AnthropicProvider</c> behind the factory. Tests inject a fake
/// <see cref="IChatClientFactory"/> (or implement <see cref="IChatClient"/> directly)
/// instead of mutating private state.
/// </remarks>
public sealed class SkillMdDrivenSkill : IOrchestratorSkill
{
    private const string SkillMdPurpose = "skill-md";

    private readonly SkillMd _skillMd;
    private readonly IMcpToolsProvider _toolsProvider;
    private readonly ILogger<SkillMdDrivenSkill> _logger;

    // Lazily resolved via the factory and reused for the lifetime of this singleton.
    // LazyThreadSafetyMode.ExecutionAndPublication ensures only one thread builds the client.
    private readonly Lazy<IChatClient> _chatClient;

    public SkillMdDrivenSkill(
        SkillMd skillMd,
        IMcpToolsProvider toolsProvider,
        IChatClientFactory chatClientFactory,
        ILogger<SkillMdDrivenSkill> logger)
    {
        _skillMd = skillMd;
        _toolsProvider = toolsProvider;
        _logger = logger;

        _chatClient = new Lazy<IChatClient>(
            () => chatClientFactory.Create(SkillMdPurpose),
            LazyThreadSafetyMode.ExecutionAndPublication);
    }

    public string Name        => _skillMd.Name;
    public string Description => _skillMd.Description;

    public bool CanHandle(string message)
    {
        if (_skillMd.Triggers.Count == 0) return false;
        var lower = message.ToLowerInvariant();
        return _skillMd.Triggers.Any(t => lower.Contains(t.ToLowerInvariant()));
    }

    public async Task<AgentResponse> ExecuteAsync(string message, CancellationToken ct = default)
    {
        var tools = await _toolsProvider.GetToolsAsync(ct);
        var options = new ChatOptions { Tools = [.. tools] };

        ChatMessage[] messages =
        [
            new(ChatRole.System, _skillMd.Body),
            new(ChatRole.User, message),
        ];

        var agentId = $"skill.md.{_skillMd.Name.ToLowerInvariant().Replace(' ', '-')}";
        var messageHead = message.Length > 200 ? message[..200] + "…" : message;

        try
        {
            _logger.LogDebug(
                "SkillMdDrivenSkill [{Skill}] → tools={ToolCount}",
                _skillMd.Name, tools.Count);

            var response = await _chatClient.Value.GetResponseAsync(messages, options, ct);
            var text = response.Text ?? string.Empty;

            // Walk response.Messages to collect every tool call the LLM
            // actually made during the function-invocation loop. Without this
            // the chatbot can't tell apart "Path B closure executed against
            // the F# registry" from "LLM looked at the closure list and made
            // up an answer" — both produce the same final text. Surfaces as
            // AgentResponse.Evidence so OrchestratorSkillIntent can propagate
            // it into IntentResult.Evidence and downstream traces. Roadmap
            // P0 #1 (docs/plans/2026-05-07-chatbot-roadmap.md).
            var toolCalls = response.Messages
                .SelectMany(m => m.Contents)
                .OfType<FunctionCallContent>()
                .Select(c => c.Name)
                .ToList();

            _logger.LogDebug(
                "SkillMdDrivenSkill [{Skill}] response length={Len}, tool calls={ToolCalls}",
                _skillMd.Name, text.Length,
                toolCalls.Count == 0 ? "<none>" : string.Join(", ", toolCalls));

            // Empty / whitespace text means the LLM either hit the tool-loop
            // iteration cap without converging, returned only tool_use blocks
            // with no text turn, or refused. Surface as a low-confidence
            // failure instead of a blank chatbot bubble (rel-005 / corr-1).
            if (string.IsNullOrWhiteSpace(text))
            {
                Activity.Current?.SetTag(ChatbotActivitySource.TagToolName, "skill-md");
                Activity.Current?.SetTag(ChatbotActivitySource.TagSkillName, _skillMd.Name);
                Activity.Current?.SetTag(
                    ChatbotActivitySource.TagToolFailureReason,
                    ChatbotActivitySource.FailureReasons.EmptyModelResponse);
                _logger.LogWarning(
                    "SkillMdDrivenSkill [{Skill}] returned empty text — tools={ToolCount}, message head=\"{MessageHead}\"",
                    _skillMd.Name, tools.Count, messageHead);
                return new AgentResponse
                {
                    AgentId    = agentId,
                    Result     = "The model returned an empty response. Please rephrase or try again.",
                    Confidence = 0f,
                };
            }

            // Build evidence entries from the actual tool-call sequence so
            // callers can audit deterministic-compute provenance instead of
            // trusting static "via ga_dsl_eval" string tags.
            var evidence = toolCalls.Count == 0
                ? new List<string> { "tools.invoked: <none>" }
                : toolCalls.Select(name => $"tools.invoked: {name}").ToList();

            return new AgentResponse
            {
                AgentId    = agentId,
                Result     = text,
                // Placeholder confidence: SKILL.md-driven skills do not emit a structured
                // confidence value — the LLM response is free-form text. 0.9f is used as a
                // reasonable default. Revisit if routing decisions start consuming this value.
                Confidence = 0.9f,
                Evidence   = evidence,
            };
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Operator-visible context: skill, tool count, message head — without
            // leaking ex.Message to the caller (may contain API keys, endpoint
            // URLs, or internal service details). Trace tag uses the structured
            // failure-reason taxonomy in ChatbotActivitySource.FailureReasons so
            // dashboards can aggregate by reason rather than parsing log strings.
            Activity.Current?.SetTag(ChatbotActivitySource.TagToolName, "skill-md");
            Activity.Current?.SetTag(ChatbotActivitySource.TagSkillName, _skillMd.Name);
            Activity.Current?.SetTag(ChatbotActivitySource.TagExceptionType, ex.GetType().Name);
            Activity.Current?.SetTag(
                ChatbotActivitySource.TagToolFailureReason,
                ChatbotActivitySource.FailureReasons.SkillMdException);
            _logger.LogError(ex,
                "SkillMdDrivenSkill [{Skill}] failed — tools={ToolCount}, message head=\"{MessageHead}\"",
                _skillMd.Name, tools.Count, messageHead);
            return new AgentResponse
            {
                AgentId    = agentId,
                Result     = "I encountered an error processing your request. Please try again.",
                Confidence = 0f,
            };
        }
    }
}
