namespace GA.Business.ML.Agents.Plugins;

using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

/// <summary>
/// Discovers all <see cref="IChatPlugin"/> implementations marked with
/// <see cref="ChatPluginAttribute"/> across loaded assemblies and registers
/// their contributions in the DI container.
/// </summary>
/// <remarks>
/// Call <see cref="AddChatPluginHost"/> once in application startup — it replaces
/// the manual skill and hook registrations in <c>ChatbotOrchestrationExtensions</c>.
/// </remarks>
public static class ChatPluginHost
{
    /// <summary>
    /// Scans all loaded assemblies for <see cref="ChatPluginAttribute"/>-marked
    /// <see cref="IChatPlugin"/> implementations and calls <see cref="IChatPlugin.Register"/>
    /// on each discovered plugin.
    /// </summary>
    /// <remarks>
    /// Discovery is intentionally eager (at startup) so plugin registration errors surface
    /// immediately rather than at first request.
    /// </remarks>
    public static IServiceCollection AddChatPluginHost(
        this IServiceCollection services,
        ILogger? logger = null)
    {
        var pluginTypes = AppDomain.CurrentDomain
            .GetAssemblies()
            .Where(a => !a.IsDynamic)
            .SelectMany(a =>
            {
                try { return a.GetExportedTypes(); }
                catch { return []; }
            })
            .Where(t => t is { IsClass: true, IsAbstract: false }
                     && t.GetCustomAttributes(typeof(ChatPluginAttribute), inherit: false).Length > 0
                     && typeof(IChatPlugin).IsAssignableFrom(t))
            .ToList();

        var allMcpToolTypes = new List<Type>();

        foreach (var pluginType in pluginTypes)
        {
            IChatPlugin plugin;
            try
            {
                plugin = (IChatPlugin)Activator.CreateInstance(pluginType)!;
            }
            catch (Exception ex)
            {
                logger?.LogWarning(ex,
                    "ChatPluginHost: failed to instantiate plugin {Type} — skipping",
                    pluginType.FullName);
                continue;
            }

            logger?.LogInformation(
                "ChatPluginHost: registering plugin '{Name}' v{Version} ({Type})",
                plugin.Name, plugin.Version, pluginType.FullName);

            plugin.Register(services);
            allMcpToolTypes.AddRange(plugin.McpToolTypes);
        }

        if (pluginTypes.Count == 0)
        {
            logger?.LogWarning(
                "ChatPluginHost: no [ChatPlugin] implementations found in loaded assemblies");
        }

        // Only register the in-process MCP tools provider when there are actual tool types.
        // Registering an empty provider adds startup cost for no benefit (Phase 3 work).
        var capturedToolTypes = allMcpToolTypes.Distinct().ToList().AsReadOnly();
        if (capturedToolTypes.Count > 0)
        {
            services.AddSingleton<IMcpToolsProvider>(sp =>
                new InProcessMcpToolsProvider(
                    capturedToolTypes,
                    sp,
                    sp.GetRequiredService<ILogger<InProcessMcpToolsProvider>>()));

            // Build the AIFunction list eagerly at startup — duplicate
            // [McpServerTool] names, missing tool dependencies, and reflection
            // failures otherwise surface only on first chat request, where
            // SkillMdDrivenSkill's catch turns them into a generic "I
            // encountered an error" reply on every subsequent call (the cached
            // failure poisons the chatbot until restart, with no operator
            // alarm). See PR #151 review (reliability rel-004).
            services.AddHostedService<McpToolsProviderStartupCheck>();
        }

        // Make the same tool types available to the host so it can also expose
        // them via MCP over HTTP (parity surface for Claude Code + remote
        // clients). Hosts that don't add the AspNetCore MCP package can
        // ignore this — the marker singleton has no effect on its own.
        services.AddSingleton(new ChatPluginMcpToolTypes(capturedToolTypes));

        return services;
    }
}

/// <summary>
/// Eagerly resolves <see cref="IMcpToolsProvider"/> at host startup so MCP
/// tool registration failures (duplicate <c>[McpServerTool]</c> names,
/// missing constructor dependencies, reflection issues) surface as a
/// boot failure rather than a silently-broken chatbot. The hosted
/// service is registered by <see cref="ChatPluginHost.AddChatPluginHost"/>
/// only when at least one plugin contributed tool types.
/// </summary>
internal sealed class McpToolsProviderStartupCheck(
    IMcpToolsProvider provider,
    ILogger<McpToolsProviderStartupCheck> logger) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        try
        {
            var tools = await provider.GetToolsAsync(cancellationToken);
            logger.LogInformation(
                "McpToolsProviderStartupCheck: {Count} AIFunction(s) ready ({Names})",
                tools.Count,
                string.Join(", ", tools.Select(t => t.Name)));
        }
        catch (Exception ex)
        {
            // Re-throw so host startup fails loudly. The alternative — log and
            // continue — produces an app that boots green but fails every
            // chat call, which is the failure mode rel-004 flagged.
            logger.LogCritical(ex,
                "McpToolsProviderStartupCheck: tool registration failed at startup — refusing to start");
            throw;
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}

/// <summary>
/// Marker singleton that surfaces the union of <c>McpToolTypes</c> contributed
/// by every loaded <see cref="IChatPlugin"/>. Consumed by
/// <c>AddChatPluginMcpHttpServer</c> in <c>GaApi</c> to wire the same tool
/// surface into MCP-over-HTTP so Claude Code and the chatbot share one
/// canonical tool definition.
/// </summary>
public sealed record ChatPluginMcpToolTypes(IReadOnlyList<Type> Types);
