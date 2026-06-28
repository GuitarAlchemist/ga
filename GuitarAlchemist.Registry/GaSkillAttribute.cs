namespace GuitarAlchemist.Registry;

/// <summary>
/// Marks a Guitar Alchemist skill as discoverable by <see cref="Registry"/>.
/// Mirrors ix's <c>#[ix_skill]</c> proc-macro pattern; the registry scans
/// loaded assemblies for this attribute and exposes the skills through a
/// unified surface (CLI, MCP server, HTTP API).
/// </summary>
/// <remarks>
/// Two supported targets:
/// <list type="bullet">
///   <item>
///     <b>Method</b> — a <c>public static</c> method accepting a single
///     <see cref="System.Text.Json.Nodes.JsonNode"/> and returning a
///     <see cref="System.Text.Json.Nodes.JsonNode"/>. The registry binds an
///     executable handler to it via reflection. Optionally, a sibling static
///     method named <c>{MethodName}Schema</c> returning
///     <see cref="System.Text.Json.Nodes.JsonObject"/> supplies the JSON
///     schema; otherwise an empty schema is used.
///   </item>
///   <item>
///     <b>Class</b> — a skill class whose body runs through its own pipeline
///     (e.g. a DI-constructed <c>IOrchestratorSkill</c>). A
///     <c>[ModuleInitializer]</c> in the owning assembly reads the attribute and
///     registers a descriptor via <see cref="Registry.RegisterSkill(string, string, string, System.Collections.Generic.IReadOnlyList{string})"/>.
///     The registry holds a descriptor only — dispatch happens through the
///     class's own pipeline, not the registry handler.
///   </item>
/// </list>
/// </remarks>
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class, AllowMultiple = false)]
public sealed class GaSkillAttribute : Attribute
{
    public string Name { get; }
    public string Domain { get; }
    public string Description { get; init; } = "";
    public string[] GovernanceTags { get; init; } = [];

    public GaSkillAttribute(string name, string domain)
    {
        Name = name;
        Domain = domain;
    }
}
