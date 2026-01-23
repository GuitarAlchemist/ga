namespace GA.Domain.Core.Design;

using System;

/// <summary>
/// Defines a domain relationship between types for schema documentation.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Interface, AllowMultiple = true)]
public class DomainRelationshipAttribute(Type targetType, RelationshipType type, string description = "") : Attribute
{
    public Type TargetType { get; } = targetType;
    public RelationshipType Type { get; } = type;
    public string Description { get; } = description;
}
