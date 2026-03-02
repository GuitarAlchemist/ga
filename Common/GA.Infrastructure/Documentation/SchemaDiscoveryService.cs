namespace GA.Infrastructure.Documentation;

using System.Reflection;
using Domain.Core.Design.Attributes;
using Domain.Core.Design.Schema;

/// <summary>
///     Service for discovering domain schema and relationships via reflection.
/// </summary>
public class SchemaDiscoveryService
{
    /// <summary>
    ///     Discovers all types in the domain that have relationship or invariant annotations.
    /// </summary>
    /// <param name="assemblies">Assemblies to scan.</param>
    /// <returns>Schema information for discovered types.</returns>
    public IEnumerable<TypeSchemaInfo> DiscoverSchema(params Assembly[] assemblies)
    {
        var discoveredTypes = assemblies
            .SelectMany(a => a.GetTypes())
            .Where(t => t.GetCustomAttributes<DomainRelationshipAttribute>().Any() ||
                        t.GetCustomAttributes<DomainInvariantAttribute>().Any());

        return discoveredTypes.Select(GetTypeSchema);
    }

    /// <summary>
    ///     Gets schema information for a specific type.
    /// </summary>
    public TypeSchemaInfo GetTypeSchema(Type type)
    {
        var relationships = type.GetCustomAttributes<DomainRelationshipAttribute>()
            .Select(a => new RelationshipInfo(a.TargetType, a.Type, a.Description));

        var invariants = type.GetCustomAttributes<DomainInvariantAttribute>()
            .Select(a => new InvariantInfo(a.Description, a.Expression));

        // Also check properties for invariants
        var propertyInvariants = type.GetProperties()
            .SelectMany(p => p.GetCustomAttributes<DomainInvariantAttribute>()
                .Select(a => new InvariantInfo($"{p.Name}: {a.Description}", a.Expression)));

        return new(
            type.Name,
            type.FullName ?? type.Name,
            [.. relationships],
            [.. invariants, .. propertyInvariants]);
    }

    /// <summary>
    ///     Gets the controlled vocabulary for domain concepts (Qualities, Extensions, etc.)
    /// </summary>
    public DomainVocabulary GetDomainVocabulary()
    {
        // 1. Discover Chord Qualities from ChordFormula or static lists? 
        // For now, we'll hardcode common ones to match the Vector Index patterns, 
        // but ideally this comes from GA.Domain.Core.Theory.Harmony.ChordQuality
        List<string> qualities = ["Major", "Minor", "Diminished", "Augmented", "Dominant", "Sus2", "Sus4"];

        // 2. Discover Extensions
        // In the future: Scan ChordDefinition or similar
        List<string> extensions = ["7", "maj7", "m7", "9", "11", "13", "6", "add9"];

        // 3. Discover Stacking Types (Voicings)
        List<string> stackingTypes =
            ["Tertial", "Quartal", "Secundal", "Quintal", "Cluster", "Drop2", "Drop3", "Shell"];

        return new(qualities, extensions, stackingTypes);
    }
}
