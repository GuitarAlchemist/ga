namespace GA.Infrastructure.Documentation;

using System.Reflection;
using System.Text;
using Domain.Core.Design.Schema;

/// <summary>
///     Generates Markdown documentation for the domain schema.
/// </summary>
public class SchemaDocumentationGenerator
{
    private readonly SchemaDiscoveryService _discoveryService = new();

    public string GenerateMarkdown(params Assembly[] assemblies)
    {
        var schema = _discoveryService.DiscoverSchema(assemblies).ToList();
        var sb = new StringBuilder();

        sb.AppendLine("# GA Domain Schema");
        sb.AppendLine();
        sb.AppendLine("## Entity Relationship Diagram");
        sb.AppendLine();
        sb.AppendLine("```mermaid");
        sb.AppendLine("classDiagram");
        foreach (var type in schema)
        {
            foreach (var rel in type.Relationships)
            {
                var arrow = rel.Type switch
                {
                    RelationshipType.IsParentOf => " --> ",
                    RelationshipType.IsChildOf => " --* ",
                    RelationshipType.Groups => " ..> ",
                    RelationshipType.IsRealizationOf => " ..|> ",
                    RelationshipType.IsDerivedFrom => " ..|> ",
                    _ => " -- "
                };
                sb.AppendLine($"    {type.Name}{arrow}{rel.TargetType.Name} : {rel.Type}");
            }
        }

        sb.AppendLine("```");
        sb.AppendLine();

        sb.AppendLine("## Entities");
        foreach (var type in schema.OrderBy(t => t.Name))
        {
            sb.AppendLine($"### {type.Name}");
            sb.AppendLine(type.FullName);
            sb.AppendLine();

            if (type.Invariants.Any())
            {
                sb.AppendLine("#### Invariants");
                foreach (var inv in type.Invariants)
                {
                    sb.AppendLine($"- {inv.Description} `{inv.Expression}`");
                }

                sb.AppendLine();
            }

            if (type.Relationships.Any())
            {
                sb.AppendLine("#### Relationships");
                foreach (var rel in type.Relationships)
                {
                    sb.AppendLine($"- **{rel.Type}** {rel.TargetType.Name}: {rel.Description}");
                }

                sb.AppendLine();
            }
        }

        return sb.ToString();
    }
}
