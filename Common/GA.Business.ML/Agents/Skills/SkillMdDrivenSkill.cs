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
public sealed class SkillMdDrivenSkill(
    SkillMd skillMd,
    IMcpToolsProvider toolsProvider,
    IConfiguration configuration,
    ILogger<SkillMdDrivenSkill> logger) : IOrchestratorSkill
{
    private const string DefaultModel = "claude-sonnet-4-6";

    public string Name        => skillMd.Name;
    public string Description => skillMd.Description;

    public bool CanHandle(string message)
    {
        if (skillMd.Triggers.Count == 0) return false;
        var lower = message.ToLowerInvariant();
        return skillMd.Triggers.Any(t => lower.Contains(t.ToLowerInvariant()));
    }

    public async Task<AgentResponse> ExecuteAsync(string message, CancellationToken ct = default)
    {
        var model = configuration["AnthropicSkills:Model"] ?? DefaultModel;

        var apiKey = configuration["Anthropic:ApiKey"]
                     ?? Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY");

        if (string.IsNullOrWhiteSpace(apiKey))
            throw new InvalidOperationException(
                "ANTHROPIC_API_KEY environment variable or Anthropic:ApiKey configuration is required " +
                "to run SKILL.md-driven skills. Set the environment variable and restart the service.");

        var anthropicClient = new AnthropicClient { ApiKey = apiKey };

        // AsIChatClient() from Microsoft.Extensions.AI.AnthropicClientExtensions
        // UseFunctionInvocation() intercepts tool_use stops, dispatches AIFunction,
        // feeds results back, and loops until a final text response.
        IChatClient chatClient = anthropicClient
            .AsIChatClient(model)
            .AsBuilder()
            .UseFunctionInvocation()
            .Build();

        ChatMessage[] messages =
        [
            new(ChatRole.System, skillMd.Body),
            new(ChatRole.User, message),
        ];

        var tools = await toolsProvider.GetToolsAsync(ct);
        var options = new ChatOptions { Tools = [.. tools] };

        try
        {
            logger.LogDebug(
                "SkillMdDrivenSkill [{Skill}] → Anthropic model={Model}, tools={ToolCount}",
                skillMd.Name, model, tools.Count);

            var response = await chatClient.GetResponseAsync(messages, options, ct);
            var text = response.Text ?? string.Empty;

            logger.LogDebug("SkillMdDrivenSkill [{Skill}] response length={Len}", skillMd.Name, text.Length);

            return new AgentResponse
            {
                AgentId    = $"skill.md.{skillMd.Name.ToLowerInvariant().Replace(' ', '-')}",
                Result     = text,
                Confidence = 0.9f,
            };
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "SkillMdDrivenSkill [{Skill}] failed", skillMd.Name);
            return new AgentResponse
            {
                AgentId    = $"skill.md.{skillMd.Name.ToLowerInvariant().Replace(' ', '-')}",
                Result     = $"I encountered an error processing your request: {ex.Message}",
                Confidence = 0f,
            };
        }
    }
}
