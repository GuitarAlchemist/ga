namespace GuitarAlchemist.Registry;

/// <summary>
/// Marks a public static method as a discoverable Guitar Alchemist skill.
/// Mirrors ix's <c>#[ix_skill]</c> proc-macro pattern; the registry scans
/// loaded assemblies for this attribute and exposes the methods through a
/// unified surface (CLI, MCP server, HTTP API).
/// </summary>
/// <remarks>
/// The annotated method must be <c>public static</c> and accept a single
/// <see cref="System.Text.Json.Nodes.JsonNode"/> argument, returning a
/// <see cref="System.Text.Json.Nodes.JsonNode"/>. Optionally, a sibling
/// static method named <c>{MethodName}Schema</c> returning
/// <see cref="System.Text.Json.Nodes.JsonObject"/> may supply the JSON
/// schema; otherwise an empty schema is used.
/// </remarks>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
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
