namespace GA.Business.Core.Atonal;

using Primitives;
using GA.Core.Collections;

/// <summary>
/// Ordered occurence for each interval class,  (e.g. Major Scale => 2, 5, 4, 3, 6, 1)
/// </summary>
/// <remarks>
/// https://musictheory.pugetsound.edu/mt21c/IntervalVector.html
/// https://viva.pressbooks.pub/openmusictheory/chapter/interval-class-vectors/
/// https://en.wikipedia.org/wiki/Interval_vector
/// https://harmoniousapp.net/p/d0/Glossary-Atonal-Theory
/// http://www.jaytomlin.com/music/settheory/help.html
/// https://viva.pressbooks.pub/openmusictheory/chapter/interval-class-vectors/
/// See Prime Form: https://www.youtube.com/watch?v=KFKMvFzobbw
/// 
/// All major scale modes share the same interval vector - Example:
/// - Major scale => 254361
/// - Dorian      => 254361
/// </remarks>
[PublicAPI]
public sealed class IntervalClassVector : IIndexer<IntervalClass, int>,
                                          IReadOnlyCollection<int>, 
                                          IComparable<IntervalClassVector>
{
    #region Indexer members

    public int this[IntervalClass ic] => _countByIc.TryGetValue(ic, out var count) ? count : 0;

    #endregion

    #region IReadOnlyCollection<int> members

    public IEnumerator<int> GetEnumerator() => _countByIc.Values.GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => _countByIc.Values.GetEnumerator();
    public int Count => _countByIc.Count;

    #endregion

    #region Relational Members

    public int CompareTo(IntervalClassVector? other)
    {
        if (ReferenceEquals(this, other)) return 0;
        return other is null ? 1 : Value.CompareTo(other.Value);
    }

    public static bool operator <(IntervalClassVector? left, IntervalClassVector? right) => Comparer<IntervalClassVector>.Default.Compare(left, right) < 0;
    public static bool operator >(IntervalClassVector? left, IntervalClassVector? right) => Comparer<IntervalClassVector>.Default.Compare(left, right) > 0;
    public static bool operator <=(IntervalClassVector? left, IntervalClassVector? right) => Comparer<IntervalClassVector>.Default.Compare(left, right) <= 0;
    public static bool operator >=(IntervalClassVector? left, IntervalClassVector? right) => Comparer<IntervalClassVector>.Default.Compare(left, right) >= 0;

    #endregion

    #region Equality members

    public static bool operator ==(IntervalClassVector? left, IntervalClassVector? right) => Equals(left, right);
    public static bool operator !=(IntervalClassVector? left, IntervalClassVector? right) => !Equals(left, right);
    public bool Equals(IntervalClassVector other) => Value == other.Value;

    public override bool Equals(object? obj)
    {
        if (ReferenceEquals(null, obj)) return false;
        if (ReferenceEquals(this, obj)) return true;
        return obj.GetType() == GetType() && Equals((IntervalClassVector) obj);
    }

    public override int GetHashCode() => Value;

    #endregion

    private readonly ImmutableSortedDictionary<IntervalClass, int> _countByIc;
    public IntervalClassVector(ImmutableSortedDictionary<IntervalClass, int> countByIc) => _countByIc = countByIc ?? throw new ArgumentNullException(nameof(countByIc));
    public static IntervalClassVector CreateFrom<T>(IEnumerable<T> items) where T : IStaticIntervalClassNorm<T> => items.ToIntervalClassVector();

    public int Value { get; }
    public int Hemitonia => this[IntervalClass.Hemitone];
    public int Tritonia => this[IntervalClass.Tritone];
    public bool IsHemitonic => Hemitonia > 0;
    public bool IsTritonic => Tritonia > 0;
    public static implicit operator int(IntervalClassVector vector) => vector.Value;

    public override string ToString() => $"<{string.Join(" ", _countByIc.Values)}>";
}
