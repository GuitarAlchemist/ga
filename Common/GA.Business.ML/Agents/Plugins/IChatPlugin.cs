namespace GA.Business.ML.Agents.Plugins;

using Hooks;
using Microsoft.Extensions.DependencyInjection;

/// <summary>
/// A named bundle of chatbot skills, hooks, and MCP tool types that registers itself
/// into the DI container. Discovered at startup by <see cref="ChatPluginHost"/>.
/// </summary>
/// <remarks>
/// Mirrors Claude Code's plugin concept: a plugin is a cohesive unit of functionality
/// (e.g., "GA", "SkillMd") that contributes <see cref="IOrchestratorSkill"/> instances,
/// <see cref="IChatHook"/> implementations, and/or in-process MCP tool types.
/// <para>
/// Implementations must also carry <see cref="ChatPluginAttribute"/> to be auto-discovered.
/// </para>
/// </remarks>
public interface IChatPlugin
{
    /// <summary>Human-readable plugin identifier used in logging.</summary>
    string Name { get; }

    /// <summary>Semantic version string (informational).</summary>
    string Version => "1.0";

    /// <summary>
    /// Registers all plugin contributions (skills, hooks) into <paramref name="services"/>.
    /// The plugin controls DI lifetimes — Singleton for pure-domain skills,
    /// Scoped for LLM-dependent skills.
    /// </summary>
    void Register(IServiceCollection services);

    /// <summary>
    /// MCP tool types whose <c>[McpServerTool]</c>-decorated methods should be
    /// included in the in-process MCP server assembled by <see cref="ChatPluginHost"/>.
    /// </summary>
    IReadOnlyList<Type> McpToolTypes => [];
}
