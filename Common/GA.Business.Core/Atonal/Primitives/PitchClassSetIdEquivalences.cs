namespace GA.Business.Core.Atonal.Primitives;

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;
using static PitchClassSetIdEquivalences.Relationship;

public class PitchClassSetIdEquivalences
{
    public enum RelationshipKind
    {
        Complement,
        Inversion,
        Rotation
    }

    private static readonly Lazy<PitchClassSetIdEquivalences> _lazyInstance = new(Create);
    public static PitchClassSetIdEquivalences Instance => _lazyInstance.Value;

    private static PitchClassSetIdEquivalences Create()
    {
        var idsByIntervalClassVectorId =
            PitchClassSetId.Items.ToLookup(id => id.ToPitchClassSet().IntervalClassVector.Id, id => id);

        var aaa = PitchClassSetId.Items.Where(id => id.ToPitchClassSet().IntervalClassVector.Id == 271296)
            .ToImmutableList();

        var primeFormIds = ImmutableSortedSet.CreateBuilder<PitchClassSetId>();
        foreach (var grouping in idsByIntervalClassVectorId)
        {
            var primeForm = grouping.Order().First();
            primeFormIds.Add(primeForm);
        }

        foreach (var grouping in idsByIntervalClassVectorId)
        {
            if (grouping.Key == 271296)
            {
                Debugger.Break();
            }

            var relationships = new HashSet<Relationship>();
            foreach (var id in grouping)
            {
                // Complement relationship
                relationships.Add(new Complement(new(id, id.Complement)));

                // Inversion relationship
                relationships.Add(new Inversion(new(id, id.Inverse)));

                // Rotation relationship (1:*)
                var idRotations = id.GetRotations().ToImmutableSortedSet();
                var rotationIndex = 0;
                foreach (var rotationId in idRotations)
                {
                    if (!id.Equals(rotationId))
                    {
                        relationships.Add(new Rotation(new(id, rotationId), rotationIndex));
                    }

                    rotationIndex++;
                }
            }

            var relationshipsByKind = relationships.ToLookup(relationship => relationship.Kind);
            var complements = relationshipsByKind[RelationshipKind.Complement].ToImmutableSortedSet();
            var inversions = relationshipsByKind[RelationshipKind.Inversion].ToImmutableSortedSet();
            var rotations = relationshipsByKind[RelationshipKind.Rotation].ToImmutableSortedSet();

            var complementsById = complements.ToDictionary(relationship => relationship.Key.Id1.ToPitchClassSet(),
                relationship => relationship.Key.Id2.ToPitchClassSet());
            var inversionsById = inversions.ToDictionary(relationship => relationship.Key.Id1.ToPitchClassSet(),
                relationship => relationship.Key.Id2.ToPitchClassSet());
            var rotationsById = rotations.ToLookup(relationship => relationship.Key.Id1.ToPitchClassSet(),
                relationship => relationship.Key.Id2.ToPitchClassSet());
        }

        // TODO: Finish implementation

        return new();
    }

    public sealed class SetClassFeatures(PitchClassSetId id)
    {
        public PitchClassSetId Id { get; } = id;

        public ImmutableSortedSet<PitchClassSetId> Complements { get; } = [id, id.Complement];
        public ImmutableSortedSet<PitchClassSetId> Inversions { get; } = [id, id.Inverse];
        public ImmutableSortedSet<PitchClassSetId> Rotations { get; } = [.. id.GetRotations()];

        public override string ToString()
        {
            return Id.ToString();
        }
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

            if (ReferenceEquals(null, other))
            {
                return 1;
            }

            var keyComparison = Key.CompareTo(other.Key);
            return keyComparison != 0 ? keyComparison : Kind.CompareTo(other.Kind);
        }

        public int CompareTo(object? obj)
        {
            if (ReferenceEquals(null, obj))
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

        public static bool operator <(Relationship? left, Relationship? right)
        {
            return Comparer<Relationship>.Default.Compare(left, right) < 0;
        }

        public static bool operator >(Relationship? left, Relationship? right)
        {
            return Comparer<Relationship>.Default.Compare(left, right) > 0;
        }

        public static bool operator <=(Relationship? left, Relationship? right)
        {
            return Comparer<Relationship>.Default.Compare(left, right) <= 0;
        }

        public static bool operator >=(Relationship? left, Relationship? right)
        {
            return Comparer<Relationship>.Default.Compare(left, right) >= 0;
        }

        #endregion
    }

    public readonly record struct UnorderedIdPair(PitchClassSetId Id1, PitchClassSetId Id2)
        : IComparable<UnorderedIdPair>, IComparable
    {
        /// <inheritdoc />
        public override string ToString()
        {
            return $"({Id1}, {Id2})";
        }

        #region Commutative Equality Members

        public bool Equals(UnorderedIdPair? other)
        {
            return other != null
                   &&
                   (
                       EqualityComparer<PitchClassSetId>.Default.Equals(Id1, other.Value.Id1) &&
                       EqualityComparer<PitchClassSetId>.Default.Equals(Id2, other.Value.Id2)
                       ||
                       EqualityComparer<PitchClassSetId>.Default.Equals(Id1, other.Value.Id2) &&
                       EqualityComparer<PitchClassSetId>.Default.Equals(Id2, other.Value.Id1)
                   );
        }

        /// <inheritdoc />
        public override int GetHashCode()
        {
            return unchecked(Id1.GetHashCode() + Id2.GetHashCode());
        }

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
        public int CompareTo(object? obj)
        {
            return obj switch
            {
                null => 1,
                UnorderedIdPair other => CompareTo(other),
                _ => throw new ArgumentException($"Object must be of type {nameof(UnorderedIdPair)}")
            };
        }

        public static bool operator <(UnorderedIdPair left, UnorderedIdPair right)
        {
            return left.CompareTo(right) < 0;
        }

        public static bool operator >(UnorderedIdPair left, UnorderedIdPair right)
        {
            return left.CompareTo(right) > 0;
        }

        public static bool operator <=(UnorderedIdPair left, UnorderedIdPair right)
        {
            return left.CompareTo(right) <= 0;
        }

        public static bool operator >=(UnorderedIdPair left, UnorderedIdPair right)
        {
            return left.CompareTo(right) >= 0;
        }

        #endregion
    }
}
