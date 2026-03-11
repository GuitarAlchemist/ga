namespace GA.Business.ML.Agents.Plugins;

using Microsoft.Extensions.DependencyInjection;
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
        }

        return services;
    }
}
