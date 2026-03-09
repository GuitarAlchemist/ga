namespace GA.Business.ML.Agents.Plugins;

/// <summary>
/// Marks a class as a GA chat plugin discoverable by <see cref="ChatPluginHost"/>.
/// The class must also implement <see cref="IChatPlugin"/>.
/// </summary>
/// <remarks>
/// Mirrors Claude Code's plugin model: a plugin bundles related skills, hooks, and MCP tools
/// as a named, self-registering unit. The host scans loaded assemblies at startup.
/// </remarks>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public sealed class ChatPluginAttribute : Attribute;
