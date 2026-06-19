namespace GuitarAlchemist.Registry;

using System.Text.Json.Nodes;

/// <summary>
/// A discovered skill — the runtime view of a method annotated with
/// <see cref="GaSkillAttribute"/>. Materialised by <see cref="Registry"/>.
/// </summary>
public sealed record GaSkill(
    string Name,
    string Domain,
    string Description,
    Func<JsonNode, JsonNode> Handler,
    Func<JsonObject> Schema,
    IReadOnlyList<string> GovernanceTags);
