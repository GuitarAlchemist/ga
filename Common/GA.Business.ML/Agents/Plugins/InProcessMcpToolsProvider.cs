namespace GA.Business.ML.Agents.Plugins;

using System.ComponentModel;
using System.Reflection;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using ModelContextProtocol.Server;

/// <summary>
/// Surfaces MCP tools from registered <see cref="IChatPlugin.McpToolTypes"/>
/// as <see cref="AIFunction"/> instances suitable for
/// <see cref="ChatOptions.Tools"/>. The previous implementation booted an
/// in-process MCP server and round-tripped tools/list through a Pipe pair;
/// that path tripped over the
/// <c>WithTools(Type)</c> / <c>WithTools(IEnumerable&lt;Type&gt;)</c>
/// overload-resolution gotcha and left the server with no
/// <c>tools/list</c> handler — every <see cref="Skills.SkillMdDrivenSkill"/> call
/// 500'd with <c>Method 'tools/list' is not available</c>.
/// </summary>
/// <remarks>
/// Direct reflection-based <see cref="AIFunctionFactory.Create(MethodInfo, object?, AIFunctionFactoryOptions?)"/>
/// is faster (no JSON-RPC, no pipe IO), simpler (no MCP capability
/// negotiation), and bypasses the registration bug entirely. Wire-name,
/// description, and parameter binding still come from
/// <see cref="McpServerToolAttribute"/> + <see cref="DescriptionAttribute"/>
/// so the contract LLMs see is unchanged.
/// </remarks>
public sealed class InProcessMcpToolsProvider(
    IReadOnlyList<Type> toolTypes,
    IServiceProvider hostServices,
    ILogger<InProcessMcpToolsProvider> logger) : IMcpToolsProvider, IAsyncDisposable
{
    private readonly SemaphoreSlim _initLock = new(1, 1);
    private IReadOnlyList<AIFunction>? _tools;

    public async ValueTask<IReadOnlyList<AIFunction>> GetToolsAsync(CancellationToken ct = default)
    {
        if (_tools is not null)
            return _tools;

        await _initLock.WaitAsync(ct);
        try
        {
            if (_tools is not null)
                return _tools;

            _tools = BuildTools();
            return _tools;
        }
        finally
        {
            _initLock.Release();
        }
    }

    private IReadOnlyList<AIFunction> BuildTools()
    {
        if (toolTypes.Count == 0)
        {
            logger.LogDebug("InProcessMcpToolsProvider: no McpToolTypes registered — returning empty tool list");
            return [];
        }

        var functions = new List<AIFunction>();
        var seenNames = new HashSet<string>(StringComparer.Ordinal);

        foreach (var toolType in toolTypes)
        {
            // Some tools are static helpers, others have ctors taking domain
            // services from hostServices. Activate lazily — only spend the
            // ActivatorUtilities call if at least one method is an instance
            // method, and only activate once per type.
            object? lazyTarget = null;
            object GetTarget() => lazyTarget ??= ActivatorUtilities.CreateInstance(hostServices, toolType);

            var methods = toolType.GetMethods(
                BindingFlags.Public | BindingFlags.NonPublic |
                BindingFlags.Instance | BindingFlags.Static);

            foreach (var method in methods)
            {
                var toolAttr = method.GetCustomAttribute<McpServerToolAttribute>();
                if (toolAttr is null) continue;

                var descAttr = method.GetCustomAttribute<DescriptionAttribute>();
                var name = !string.IsNullOrEmpty(toolAttr.Name) ? toolAttr.Name : method.Name;
                var description = descAttr?.Description ?? string.Empty;

                if (!seenNames.Add(name))
                {
                    throw new InvalidOperationException(
                        $"InProcessMcpToolsProvider: duplicate MCP tool name '{name}'. " +
                        $"Each [McpServerTool] must have a unique wire name " +
                        $"(e.g. via [McpServerTool(Name = \"ga_<topic>_<verb>\")]).");
                }

                var target = method.IsStatic ? null : GetTarget();
                var fn = AIFunctionFactory.Create(method, target, name, description);
                functions.Add(fn);
            }
        }

        logger.LogInformation(
            "InProcessMcpToolsProvider: built {Count} AIFunctions ({Names})",
            functions.Count,
            string.Join(", ", functions.Select(f => f.Name)));

        return functions.AsReadOnly();
    }

    public ValueTask DisposeAsync()
    {
        _initLock.Dispose();
        return ValueTask.CompletedTask;
    }
}
