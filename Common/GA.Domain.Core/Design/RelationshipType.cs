namespace GA.Domain.Core.Design;

/// <summary>
/// Specifies the type of relationship between domain entities.
/// </summary>
public enum RelationshipType
{
    /// <summary>The current type is a parent of the target type (e.g., Scale is a parent of PitchClassSet).</summary>
    IsParentOf,
    /// <summary>The current type is a child of the target type.</summary>
    IsChildOf,
    /// <summary>The current type is a peer of the target type (horizontal relationship).</summary>
    IsPeerOf,
    /// <summary>The current type provides metadata or descriptive information for the target type.</summary>
    IsMetadataFor,
    /// <summary>The current type groups multiple instances of the target type.</summary>
    Groups,
    /// <summary>The current type transforms into the target type (e.g., C Major to C Minor).</summary>
    TransformsTo,
    /// <summary>The current type is a parallel version of the target type (same root, different quality).</summary>
    IsParallelTo,
    /// <summary>The current type is derived from the target type (e.g., Mode derived from Scale).</summary>
    IsDerivedFrom,
    /// <summary>The current type represents a physical realization of the target type (e.g., FretboardShape is a realization of a Chord).</summary>
    IsRealizationOf
}