namespace GA.Business.Core.Atonal;

using Primitives;

/// <summary>
/// Represents a set class in post-tonal music theory.
/// </summary>
/// <remarks>
/// A set class is an equivalence class of pitch class sets related by transposition or inversion.
/// It is characterized by its prime form, which is the most compact representation of the set.
/// Set classes are fundamental to analyzing atonal and twelve-tone music, as they allow for
/// identifying structural relationships between different pitch collections regardless of their
/// specific pitch content (<see href="https://harmoniousapp.net/p/71/Set-Classes"/>)
/// 
/// This class provides properties for accessing the prime form, cardinality, and interval class vector
/// of the set class, which are essential characteristics for set class analysis.
///
/// Implement <see cref="IEquatable{SetClass}"/>
/// </remarks>
[PublicAPI]
public sealed class SetClass(PitchClassSet pitchClassSet) : IEquatable<SetClass>, IStaticReadonlyCollection<SetClass>
{
    #region IStaticReadonlyCollection Members

    /// <summary>
    /// Gets all set classes
    /// <br/><see cref="IReadOnlyCollection{PitchClassSet}"/>
    /// </summary>
    public static IReadOnlyCollection<SetClass> Items => AllSetClasses.Instance;

    #endregion

    /// <summary>
    /// Gets all modal set classes
    /// </summary>
    public static IReadOnlyCollection<SetClass> ModalItems => [..Items.Where(@class => @class.IsModal)];
    
    #region Equality Members
    
    public bool Equals(SetClass? other)
    {
        if (ReferenceEquals(null, other)) return false;
        return ReferenceEquals(this, other) || PrimeForm.Equals(other.PrimeForm);
    }

    public override bool Equals(object? obj)
    {
        if (ReferenceEquals(null, obj)) return false;
        if (ReferenceEquals(this, obj)) return true;
        return obj.GetType() == GetType() && Equals((SetClass)obj);
    }

    public override int GetHashCode() => PrimeForm.GetHashCode();
    public static bool operator ==(SetClass? left, SetClass? right) => Equals(left, right);
    public static bool operator !=(SetClass? left, SetClass? right) => !Equals(left, right);

    #endregion    

    /// <summary>
    /// Gets the <see cref="Cardinality"/>
    /// </summary>
    public Cardinality Cardinality => PrimeForm.Cardinality;
    
    /// <summary>
    /// Gets the <see cref="IntervalClassVector"/>
    /// </summary>
    public IntervalClassVector IntervalClassVector => PrimeForm.IntervalClassVector;
    
    /// <summary>
    /// Gets the <see cref="PitchClassSet"/> prime form
    /// </summary>
    public PitchClassSet PrimeForm { get; } = pitchClassSet.PrimeForm ?? throw new ArgumentException("Invalid pitch class set", nameof(pitchClassSet));

    /// <summary>
    /// Gets the <see cref="ModalFamily"/> of the set class, if it exists
    /// </summary>
    public ModalFamily? ModalFamily => ModalFamily.TryGetValue(IntervalClassVector, out var modalFamily) ? modalFamily : null;

    /// <summary>
    /// Determines whether this set class represents a modal scale
    /// </summary>
    public bool IsModal => ModalFamily != null;
    
    /// <inheritdoc />
    public override string ToString() => $"SetClass[{Cardinality}-{IntervalClassVector.Id}]";
   
    #region Innner Classes
    
    private class AllSetClasses : LazyCollectionBase<SetClass>
    {
        public static readonly AllSetClasses Instance = new();
        private AllSetClasses() : base(Collection, separator: ", ") { }

        private static IEnumerable<SetClass> Collection =>
            PitchClassSet.Items
                .Select(pcs => pcs.PrimeForm)
                .Where(pf => pf != null)
                .Distinct()
                .Select(pf => new SetClass(pf!))
                .OrderBy(setClass => setClass.Cardinality)
                .ThenBy(setClass => setClass.PrimeForm.Id);
    }    
    
    #endregion
}