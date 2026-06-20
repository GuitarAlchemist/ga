namespace GA.Domain.Core.Theory.Atonal;

public sealed class PitchClassSetIdEquivalences
{
    public enum RelationshipKind
    {
        Complement,
        Inversion,
        Rotation
    }

    private static readonly Lazy<PitchClassSetIdEquivalences> _lazyInstance = new(() => new PitchClassSetIdEquivalences());
    public static PitchClassSetIdEquivalences Instance => _lazyInstance.Value;

    /// <summary>
    ///     Gets the complement / inversion / rotation relationships for a pitch-class-set id.
    /// </summary>
    /// <remarks>
    ///     The id-level view of the equivalence relations. For the OPTIC-K (complement) grouping over
    ///     set classes, see <see cref="OpticKClass" />.
    /// </remarks>
    public SetClassFeatures GetFeatures(PitchClassSetId id) => new(id);

    public sealed class SetClassFeatures(PitchClassSetId id)
    {
        public PitchClassSetId Id { get; } = id;

        public ImmutableSortedSet<PitchClassSetId> Complements { get; } = [id, id.Complement];
        public ImmutableSortedSet<PitchClassSetId> Inversions { get; } = [id, id.Inverse];
        public ImmutableSortedSet<PitchClassSetId> Rotations { get; } = [.. id.GetRotations()];

        public override string ToString() => Id.ToString();
    }

    public abstract record Relationship(UnorderedIdPair Key, RelationshipKind Kind)
        : IComparable<Relationship>, IComparable
    {
        public sealed record Complement(UnorderedIdPair Key) : Relationship(Key, RelationshipKind.Complement);

        public sealed record Inversion(UnorderedIdPair Key) : Relationship(Key, RelationshipKind.Inversion);

        public sealed record Rotation(UnorderedIdPair Key, int Distance) : Relationship(Key, RelationshipKind.Rotation);

        #region Relational Members

        public int CompareTo(Relationship? other)
        {
            if (ReferenceEquals(this, other))
            {
                return 0;
            }

            if (other is null)
            {
                return 1;
            }

            var keyComparison = Key.CompareTo(other.Key);
            return keyComparison != 0 ? keyComparison : Kind.CompareTo(other.Kind);
        }

        public int CompareTo(object? obj)
        {
            if (obj is null)
            {
                return 1;
            }

            if (ReferenceEquals(this, obj))
            {
                return 0;
            }

            return obj is Relationship other
                ? CompareTo(other)
                : throw new ArgumentException($"Object must be of type {nameof(Relationship)}");
        }

        public static bool operator <(Relationship? left, Relationship? right) =>
            Comparer<Relationship>.Default.Compare(left, right) < 0;

        public static bool operator >(Relationship? left, Relationship? right) =>
            Comparer<Relationship>.Default.Compare(left, right) > 0;

        public static bool operator <=(Relationship? left, Relationship? right) =>
            Comparer<Relationship>.Default.Compare(left, right) <= 0;

        public static bool operator >=(Relationship? left, Relationship? right) =>
            Comparer<Relationship>.Default.Compare(left, right) >= 0;

        #endregion
    }

    public readonly record struct UnorderedIdPair(PitchClassSetId Id1, PitchClassSetId Id2)
        : IComparable<UnorderedIdPair>, IComparable
    {
        /// <inheritdoc />
        public override string ToString() => $"({Id1}, {Id2})";

        #region Commutative Equality Members

        public bool Equals(UnorderedIdPair? other) => other != null
                                                      &&
                                                      (
                                                          (EqualityComparer<PitchClassSetId>.Default.Equals(Id1,
                                                               other.Value.Id1) &&
                                                           EqualityComparer<PitchClassSetId>.Default.Equals(Id2,
                                                               other.Value.Id2))
                                                          ||
                                                          (EqualityComparer<PitchClassSetId>.Default.Equals(Id1,
                                                               other.Value.Id2) &&
                                                           EqualityComparer<PitchClassSetId>.Default.Equals(Id2,
                                                               other.Value.Id1))
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
    }
}
