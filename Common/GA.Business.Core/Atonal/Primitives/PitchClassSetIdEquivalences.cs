namespace GA.Business.Core.Atonal.Primitives;

public class PitchClassSetIdEquivalences
{
    public static PitchClassSetIdEquivalences Create()
    {
        // var idsByIntervalClassVectorId = PitchClassSetId.Items.ToLookup(id => id.PitchClassSet.IntervalClassVector.Id, id => id);
        
        var relationships = new HashSet<Relationship>();
        foreach (var id in PitchClassSetId.Items)
        {
            // Complement relationship
            relationships.Add(
                new Relationship(
                    new(id, id.Complement),
                    RelationshipKind.Complement)
            );

            // Inversion relationship
            relationships.Add(
                new Relationship(
                    new(id, id.Inverse), 
                    RelationshipKind.Inversion)
            );
            
            // Rotation relationship (1:*)
            foreach (var rotationId in id.GetRotations())
            {
                relationships.Add(
                    new Relationship(
                        new(id, rotationId),
                        RelationshipKind.Rotation)
                );
            }
        }

        var relationshipsByKind = relationships.ToLookup(relationship => relationship.Kind);
        var complements = relationshipsByKind[RelationshipKind.Complement].ToImmutableList();
        var inversions = relationshipsByKind[RelationshipKind.Inversion].ToImmutableList();
        var rotations = relationshipsByKind[RelationshipKind.Rotation].ToImmutableList();
        
        // TODO: Finish implementation

        return new PitchClassSetIdEquivalences();
    }

    public sealed class SetClassFeatures(PitchClassSetId id)
    {
        public PitchClassSetId Id { get; } = id;

        public ImmutableSortedSet<PitchClassSetId> Complements { get; } = [id, id.Complement];
        public ImmutableSortedSet<PitchClassSetId> Inversions { get; } = [id, id.Inverse];
        public ImmutableSortedSet<PitchClassSetId> Rotations { get; } = id.GetRotations().ToImmutableSortedSet();

        public override string ToString() => Id.ToString();
    }

    public sealed record Relationship(UnorderedIdPair Key, RelationshipKind Kind);
    
    public readonly record struct UnorderedIdPair(PitchClassSetId Id1, PitchClassSetId Id2) : IComparable<UnorderedIdPair>, IComparable
    {
        #region Commutative Equality Members
        
        public bool Equals(UnorderedIdPair? other) =>
            other != null
            &&
            (
                (EqualityComparer<PitchClassSetId>.Default.Equals(Id1, other.Value.Id1) &&
                 EqualityComparer<PitchClassSetId>.Default.Equals(Id2, other.Value.Id2))
                ||
                (EqualityComparer<PitchClassSetId>.Default.Equals(Id1, other.Value.Id2) &&
                 EqualityComparer<PitchClassSetId>.Default.Equals(Id2, other.Value.Id1))
            );

        /// <inheritdoc />
        public override int GetHashCode() => unchecked(Id1.GetHashCode() + Id2.GetHashCode());
        
        #endregion

        #region Commutative Relational Members

        /// <inheritdoc />
        public int CompareTo(UnorderedIdPair other)
        {
            // Sort Ids for this instance
            var (minId, maxId) = Id1.CompareTo(Id2) <= 0 
                ? (Id1, Id2) 
                : (Id2, Id1);
            
            // Sort Ids for the other instance
            var (otherMinId, otherMaxId) = 
                other.Id1.CompareTo(other.Id2) <= 0 
                    ? (other.Id1, other.Id2) 
                    : (other.Id2, other.Id1);

            // Compare the minimum IDs first
            var minIdComparison = minId.CompareTo(otherMinId);
            return minIdComparison != 0 
                ? minIdComparison 
                : maxId.CompareTo(otherMaxId); // If equal, compare the maximum IDs
        }

        /// <inheritdoc />
        public int CompareTo(object? obj) => obj switch
        {
            null => 1,
            UnorderedIdPair other => CompareTo(other),
            _ => throw new ArgumentException($"Object must be of type {nameof(UnorderedIdPair)}")
        };
        
        public static bool operator <(UnorderedIdPair left, UnorderedIdPair right) => left.CompareTo(right) < 0;
        public static bool operator >(UnorderedIdPair left, UnorderedIdPair right) => left.CompareTo(right) > 0;
        public static bool operator <=(UnorderedIdPair left, UnorderedIdPair right) => left.CompareTo(right) <= 0;
        public static bool operator >=(UnorderedIdPair left, UnorderedIdPair right) => left.CompareTo(right) >= 0;

        #endregion

        /// <inheritdoc />
        public override string ToString() => $"({Id1}, {Id2})";
    }    

    public enum RelationshipKind
    {
        IntervalClassVector,
        Complement,
        Inversion,
        Rotation
    }
}