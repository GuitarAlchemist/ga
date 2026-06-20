namespace GA.Domain.Core.Theory.Atonal;

/// <summary>
///     A transposition class ("Tn-type") — an equivalence class of pitch-class sets related by
///     transposition ONLY, the OPTC rung of the Callender–Quinn–Tymoczko OPTIC hierarchy.
/// </summary>
/// <remarks>
///     This sits between <see cref="PitchClassSet" /> (the OPC rung, 4096 sets) and <see cref="SetClass" />
///     (the OPTIC rung, 224 set classes). Unlike a set class, a transposition class does NOT fold in
///     inversion, so a major triad and a minor triad are <em>different</em> transposition classes that
///     share one set class. See <see href="https://harmoniousapp.net/p/ec/Equivalence-Groups" />.
///     The canonical representative is the transposition-only prime form: the smallest pitch-class-set id
///     among the twelve transpositions (the binary-necklace representative, consistent with how
///     <see cref="SetClass.PrimeForm" /> picks the smallest of the twenty-four transposition/inversion forms).
/// </remarks>
[PublicAPI]
public sealed class TranspositionClass : IEquatable<TranspositionClass>
{
    private static readonly Lazy<IReadOnlyList<TranspositionClass>> _lazyItems = new(Build);

    public TranspositionClass(PitchClassSet pitchClassSet)
    {
        ArgumentNullException.ThrowIfNull(pitchClassSet);
        PrimeForm = TranspositionPrimeForm(pitchClassSet);
    }

    /// <summary>
    ///     Gets the transposition-only prime form (smallest id among the twelve transpositions).
    /// </summary>
    public PitchClassSet PrimeForm { get; }

    /// <summary>
    ///     Gets the <see cref="Cardinality" />.
    /// </summary>
    public Cardinality Cardinality => PrimeForm.Cardinality;

    /// <summary>
    ///     Gets the <see cref="IntervalClassVector" />.
    /// </summary>
    public IntervalClassVector IntervalClassVector => PrimeForm.IntervalClassVector;

    /// <summary>
    ///     Gets the <see cref="SetClass" /> (OPTIC) this transposition class belongs to — folding in inversion.
    /// </summary>
    public SetClass SetClass => new(PrimeForm);

    /// <summary>
    ///     Gets a flag indicating whether this transposition class is inversionally symmetric, i.e. equal to
    ///     its own inversion's transposition class (e.g. an augmented triad). Asymmetric classes (e.g. a major
    ///     triad) pair with a distinct inverted transposition class inside the same <see cref="SetClass" />.
    /// </summary>
    public bool IsInversionallySymmetric =>
        PrimeForm.Id.Value == TranspositionPrimeForm(PrimeForm.Inverse).Id.Value;

    /// <summary>
    ///     Gets all transposition classes, ordered by prime-form id.
    /// </summary>
    public static IReadOnlyList<TranspositionClass> Items => _lazyItems.Value;

    private static PitchClassSet TranspositionPrimeForm(PitchClassSet pitchClassSet)
    {
        var id = pitchClassSet.Id;
        var min = id.Value;
        for (var i = 1; i < 12; i++)
        {
            var rotated = id.Transpose(i).Value;
            if (rotated < min)
            {
                min = rotated;
            }
        }

        return ((PitchClassSetId)min).ToPitchClassSet();
    }

    private static IReadOnlyList<TranspositionClass> Build()
    {
        var seen = new HashSet<int>();
        var items = new List<TranspositionClass>();

        foreach (var pcs in PitchClassSet.Items)
        {
            var tc = new TranspositionClass(pcs);
            if (seen.Add(tc.PrimeForm.Id.Value))
            {
                items.Add(tc);
            }
        }

        items.Sort((a, b) => a.PrimeForm.Id.Value.CompareTo(b.PrimeForm.Id.Value));
        return items.AsReadOnly();
    }

    #region Equality Members

    public bool Equals(TranspositionClass? other) =>
        other is not null && (ReferenceEquals(this, other) || PrimeForm.Equals(other.PrimeForm));

    public override bool Equals(object? obj) => obj is TranspositionClass other && Equals(other);

    public override int GetHashCode() => PrimeForm.GetHashCode();

    public static bool operator ==(TranspositionClass? left, TranspositionClass? right) => Equals(left, right);

    public static bool operator !=(TranspositionClass? left, TranspositionClass? right) => !Equals(left, right);

    #endregion

    public override string ToString() => $"TnType[{Cardinality}-{PrimeForm.Id.Value}]";
}
