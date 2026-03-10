namespace GA.Business.ML.Agents.Skills;

using Anthropic;
using GA.Business.ML.Agents.Plugins;
using GA.Business.ML.Skills;
using Microsoft.Extensions.AI;

/// <summary>
/// <see cref="IOrchestratorSkill"/> backed by a SKILL.md file.
/// Uses the Anthropic SDK + MEAI <see cref="IChatClient"/> for LLM calls,
/// with GA domain tools supplied by <see cref="IMcpToolsProvider"/>.
/// <see cref="FunctionInvokingChatClientBuilderExtensions.UseFunctionInvocation"/> handles the
/// full multi-turn agentic tool-use loop automatically — no hand-coded loop required.
/// </summary>
public sealed class SkillMdDrivenSkill : IOrchestratorSkill
{
    private const string DefaultModel = "claude-sonnet-4-6";

    private readonly SkillMd _skillMd;
    private readonly IMcpToolsProvider _toolsProvider;
    private readonly ILogger<SkillMdDrivenSkill> _logger;

    // Built once (lazily) and reused for the lifetime of this singleton.
    // LazyThreadSafetyMode.ExecutionAndPublication ensures only one thread builds the client.
    private readonly Lazy<IChatClient> _chatClient;

    public SkillMdDrivenSkill(
        SkillMd skillMd,
        IMcpToolsProvider toolsProvider,
        IConfiguration configuration,
        ILogger<SkillMdDrivenSkill> logger)
    {
        _skillMd = skillMd;
        _toolsProvider = toolsProvider;
        _logger = logger;

        _chatClient = new Lazy<IChatClient>(() =>
        {
            var model = configuration["AnthropicSkills:Model"] ?? DefaultModel;
            var apiKey = configuration["Anthropic:ApiKey"]
                         ?? Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY");

            if (string.IsNullOrWhiteSpace(apiKey))
                throw new InvalidOperationException(
                    "ANTHROPIC_API_KEY environment variable or Anthropic:ApiKey configuration is required " +
                    "to run SKILL.md-driven skills. Set the environment variable and restart the service.");

            return new AnthropicClient { ApiKey = apiKey }
                .AsIChatClient(model)
                .AsBuilder()
                .UseFunctionInvocation()
                .Build();
        }, LazyThreadSafetyMode.ExecutionAndPublication);
    }

    /// <summary>
    /// Creates a <see cref="SkillMdDrivenSkill"/> with an injected <see cref="IChatClient"/>
    /// for unit testing — bypasses <c>AnthropicClient</c> creation and API-key validation.
    /// </summary>
    internal static SkillMdDrivenSkill ForTesting(
        SkillMd skillMd,
        IMcpToolsProvider toolsProvider,
        ILogger<SkillMdDrivenSkill> logger,
        IChatClient chatClient)
    {
        var skill = new SkillMdDrivenSkill(
            skillMd,
            toolsProvider,
            new ConfigurationBuilder().Build(),
            logger);
        // Replace the lazy with one that always returns the test client.
        typeof(SkillMdDrivenSkill)
            .GetField("_chatClient", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
            .SetValue(skill, new Lazy<IChatClient>(() => chatClient));
        return skill;
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

        try
        {
            _logger.LogDebug(
                "SkillMdDrivenSkill [{Skill}] → tools={ToolCount}",
                _skillMd.Name, tools.Count);

            var response = await _chatClient.Value.GetResponseAsync(messages, options, ct);
            var text = response.Text ?? string.Empty;

            _logger.LogDebug("SkillMdDrivenSkill [{Skill}] response length={Len}", _skillMd.Name, text.Length);

            return new AgentResponse
            {
                AgentId    = $"skill.md.{_skillMd.Name.ToLowerInvariant().Replace(' ', '-')}",
                Result     = text,
                // Placeholder confidence: SKILL.md-driven skills do not emit a structured
                // confidence value — the LLM response is free-form text. 0.9f is used as a
                // reasonable default. Revisit if routing decisions start consuming this value.
                Confidence = 0.9f,
            };
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "SkillMdDrivenSkill [{Skill}] failed", _skillMd.Name);
            return new AgentResponse
            {
                AgentId    = $"skill.md.{_skillMd.Name.ToLowerInvariant().Replace(' ', '-')}",
                // Return a generic message — never surface ex.Message to the caller
                // (may contain API keys, endpoint URLs, or internal service details).
                Result     = "I encountered an error processing your request. Please try again.",
                Confidence = 0f,
            };
        }
    }
}
