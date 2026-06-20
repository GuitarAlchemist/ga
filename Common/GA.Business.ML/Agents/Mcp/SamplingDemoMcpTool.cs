namespace GA.Business.ML.Agents.Mcp;

using System.ComponentModel;
using Microsoft.Extensions.AI;
using ModelContextProtocol.Server;

/// <summary>
/// Spike for the MCP-sampling pattern (task #79 of the chatbot ↔ Claude
/// Code parity plan). Exposes <c>ga_sampling_demo</c> — a single MCP
/// tool whose implementation asks the *connected client* to do an LLM
/// completion via <see cref="McpServer.SampleAsync(IEnumerable{ChatMessage}, ChatOptions, System.Text.Json.JsonSerializerOptions, System.Threading.CancellationToken)"/>
/// instead of opening its own <see cref="IChatClient"/>.
/// </summary>
/// <remarks>
/// Why this matters for parity: today every <c>SkillMdDrivenSkill</c>
/// builds an Anthropic <see cref="IChatClient"/> on first call, even
/// when invoked from a context that already has one (Claude Code's
/// session, the chatbot orchestrator's own LLM). With sampling the tool
/// asks whatever client connected — Claude Code's session for dev
/// iteration, the chatbot's own client for production — to do the
/// reasoning. One LLM, no per-skill credentials, no
/// <c>IChatClientFactory</c> drift.
///
/// Sampling requires the connecting client to declare the
/// <see cref="ModelContextProtocol.Protocol.SamplingCapability"/>; the
/// chatbot's in-process MCP path bypasses MCP entirely
/// (reflection-based <c>AIFunctionFactory</c>) so sampling there is a
/// follow-up — for now, this tool is callable end-to-end from Claude
/// Code via GaApi's <c>/mcp</c> endpoint.
/// </remarks>
[McpServerToolType]
public sealed class SamplingDemoMcpTool
{
    private const int MaxQuestionLength = 1_000;

    [McpServerTool(Name = "ga_sampling_demo"), Description(
        "Asks the connected MCP client (Claude Code session, chatbot orchestrator, etc.) " +
        "to answer the supplied question via MCP sampling. The server itself does NOT " +
        "open an LLM connection; the client's LLM does the reasoning and returns the text. " +
        "Spike for the chatbot ↔ Claude Code parity plan — proves a Guitar Alchemist " +
        "tool can use the caller's LLM without provisioning its own.")]
    public static async Task<SamplingDemoResult> SampleAsync(
        McpServer server,
        [Description("The question to send to the connected client's LLM. Max 1,000 chars.")]
        string question,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(question))
            return SamplingDemoResult.Failure("question is required.");
        if (question.Length > MaxQuestionLength)
            return SamplingDemoResult.Failure($"question exceeds {MaxQuestionLength} chars.");

        ChatMessage[] messages =
        [
            new(ChatRole.System,
                "You are answering a music-theory question for the Guitar Alchemist. " +
                "Be concise, accurate, and prefer a one-sentence answer when the question allows."),
            new(ChatRole.User, question),
        ];

        try
        {
            var response = await server.SampleAsync(messages, new ChatOptions(), null, cancellationToken);
            var text = response.Text ?? string.Empty;
            return new SamplingDemoResult { Answer = text, Error = null };
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("sampling", StringComparison.OrdinalIgnoreCase))
        {
            return SamplingDemoResult.Failure(
                "Connected MCP client does not declare the sampling capability. " +
                "Use Claude Code (or another sampling-capable client) to exercise this tool.");
        }
        catch (Exception ex)
        {
            return SamplingDemoResult.Failure($"sampling-failed: {ex.GetType().Name}: {ex.Message}");
        }
    }
}

/// <summary>Result envelope for <see cref="SamplingDemoMcpTool.SampleAsync"/>.</summary>
public sealed record SamplingDemoResult
{
    public string? Answer { get; init; }
    public string? Error { get; init; }

    public static SamplingDemoResult Failure(string message) => new() { Error = message };
}
