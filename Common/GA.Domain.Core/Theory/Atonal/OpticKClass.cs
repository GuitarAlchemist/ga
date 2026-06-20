namespace GA.Domain.Core.Theory.Atonal;

/// <summary>
///     An OPTIC-K equivalence class — a <see cref="SetClass" /> grouped with its complement set class.
/// </summary>
/// <remarks>
///     K (complementation) is the top rung of the Callender–Quinn–Tymoczko OPTIC hierarchy
///     (<see href="https://harmoniousapp.net/p/ec/Equivalence-Groups" />):
///     O ⊂ OP ⊂ OPC ⊂ OPTC ⊂ OPTIC ⊂ OPTIC-K, where each rung folds in one more equivalence
///     (Octave, Permutation, Cardinality, Transposition, Involution/inversion, K/complementation).
///     <see cref="PitchClassSet" /> reifies the OPC rung (4096 sets) and <see cref="SetClass" /> the
///     OPTIC rung (224 set classes); this type reifies the OPTIC-K rung — the namesake of the OPTIC-K
///     embedding. Each class contains a set class and its complement, collapsing to a single member when
///     the set class is self-complementary (a self-complementary hexachord).
/// </remarks>
[PublicAPI]
public sealed class OpticKClass : IEquatable<OpticKClass>
{
    private static readonly Lazy<IReadOnlyList<OpticKClass>> _lazyItems = new(Build);

    private OpticKClass(SetClass representative, ImmutableArray<SetClass> members)
    {
        Representative = representative;
        Members = members;
    }

    /// <summary>
    ///     Gets the canonical representative — the member with the lowest prime-form id.
    /// </summary>
    public SetClass Representative { get; }

    /// <summary>
    ///     Gets the set class and its complement, ordered by prime-form id (a single member when
    ///     <see cref="IsSelfComplementary" />).
    /// </summary>
    public ImmutableArray<SetClass> Members { get; }

    /// <summary>
    ///     Gets a flag indicating whether the set class is its own complement (a self-complementary hexachord).
    /// </summary>
    public bool IsSelfComplementary => Members.Length == 1;

    /// <summary>
    ///     Gets all OPTIC-K classes, ordered by representative prime-form id.
    /// </summary>
    public static IReadOnlyList<OpticKClass> Items => _lazyItems.Value;

    private static IReadOnlyList<OpticKClass> Build()
    {
        var seen = new HashSet<int>();
        var groups = new List<OpticKClass>();

        foreach (var setClass in SetClass.Items.OrderBy(sc => sc.PrimeForm.Id.Value))
        {
            var id = setClass.PrimeForm.Id.Value;
            if (!seen.Add(id))
            {
                continue; // already grouped as some earlier set class's complement
            }

            var complement = setClass.Complement;
            var complementId = complement.PrimeForm.Id.Value;

            ImmutableArray<SetClass> members;
            if (complementId == id)
            {
                members = [setClass];
            }
            else
            {
                seen.Add(complementId);
                // setClass has the lowest ungrouped id, so it is always the representative.
                members = [setClass, complement];
            }

            groups.Add(new OpticKClass(setClass, members));
        }

        return groups.AsReadOnly();
    }

    #region Equality Members

    public bool Equals(OpticKClass? other) =>
        other is not null && (ReferenceEquals(this, other) || Representative.Equals(other.Representative));

    public override bool Equals(object? obj) => obj is OpticKClass other && Equals(other);

    public override int GetHashCode() => Representative.GetHashCode();

    public static bool operator ==(OpticKClass? left, OpticKClass? right) => Equals(left, right);

    public static bool operator !=(OpticKClass? left, OpticKClass? right) => !Equals(left, right);

    #endregion

    public override string ToString() =>
        IsSelfComplementary
            ? $"OpticK[{Representative} self-complementary]"
            : $"OpticK[{string.Join(" <-> ", Members)}]";
}
