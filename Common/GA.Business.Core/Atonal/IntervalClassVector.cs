namespace GA.Business.Core.Atonal;

using GA.Core;
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
        if (obj is null) return false;
        if (ReferenceEquals(this, obj)) return true;
        return obj.GetType() == GetType() && Equals((IntervalClassVector) obj);
    }

    public override int GetHashCode() => Value;

    #endregion

    private readonly ImmutableSortedDictionary<IntervalClass, int> _countByIc;

    public IntervalClassVector(ImmutableSortedDictionary<IntervalClass, int> countByIc)
    {
        _countByIc = countByIc ?? throw new ArgumentNullException(nameof(countByIc));
        Value = ToBase12Value(countByIc);
    }

    public static IntervalClassVector CreateFrom<T>(IEnumerable<T> items) 
        where T : IStaticIntervalClassNorm<T>, IValueObject 
            => items.ToIntervalClassVector();

    /// <summary>
    /// Gets the base 12 value
    /// </summary>
    public int Value { get; }
    public int Hemitonia => this[IntervalClass.Hemitone];
    public int Tritonia => this[IntervalClass.Tritone];
    public bool IsHemitonic => Hemitonia > 0;
    public bool IsTritonic => Tritonia > 0;
    /// <summary>
    /// 
    /// </summary>
    /// <remarks>
    /// See https://en.wikipedia.org/wiki/Common_tone_(scale)#Deep_scale_property
    /// </remarks>
    public bool IsDeepScale => _countByIc.Values.Distinct().Count() == _countByIc.Values.Count(); 
    public static implicit operator int(IntervalClassVector vector) => vector.Value;
    public static implicit operator IntervalClassVector(int value) => FromBase12Value(value);

    public override string ToString() => $"<{string.Join(" ", _countByIc.Values)}>";

    private static IntervalClassVector FromBase12Value(int value)
    {
        var dictBuilder = ImmutableSortedDictionary.CreateBuilder<IntervalClass, int>();
        var dividend = value;
        var intervalClasses = IntervalClass.Range(1, 6).Reverse();
        foreach (var intervalClass in intervalClasses)
        {
            var count = dividend % 12;
            dictBuilder.Add(intervalClass, count);
            dividend /= 10;
        }
        return new(dictBuilder.ToImmutable());
    }

    private static int ToBase12Value(ImmutableSortedDictionary<IntervalClass, int> countByIc)
    {
        var weight = 1;
        var value = 0;
        foreach (var ic in countByIc.Keys.OrderBy(ic => ic.Value))
        {
            var count = countByIc[ic];
            value += count * weight;
            weight *= 12;
        }
        return value;
    }
}
