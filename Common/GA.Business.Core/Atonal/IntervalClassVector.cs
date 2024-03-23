using CollectionExtensions = System.Collections.Generic.CollectionExtensions;

namespace GA.Business.Core.Atonal;

using Primitives;

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
/// Items major scale modes share the same interval vector - Example:
/// - Major scale => 254361
/// - Dorian      => 254361
/// </remarks>
[PublicAPI]
public sealed class IntervalClassVector : IIndexer<IntervalClass, int>,
                                          IReadOnlyCollection<int>, 
                                          IComparable<IntervalClassVector>
{
    #region Indexer members

    /// <summary>
    /// Gets the occurence count for the interval class
    /// </summary>
    /// <param name="intervalClass">The <see cref="IntervalClass"/></param>
    /// <returns>The occurence count.</returns>
    public int this[IntervalClass intervalClass] => CollectionExtensions.GetValueOrDefault(_countByIntervalClass, intervalClass, 0);

    #endregion

    #region IReadOnlyCollection<int> members

    public IEnumerator<int> GetEnumerator() => _countByIntervalClass.Values.GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => _countByIntervalClass.Values.GetEnumerator();
    public int Count => _countByIntervalClass.Count;

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

    private readonly ImmutableSortedDictionary<IntervalClass, int> _countByIntervalClass;

    public IntervalClassVector(IReadOnlyDictionary<IntervalClass, int> countByIntervalClass)
    {
        ArgumentNullException.ThrowIfNull(countByIntervalClass);

        var dictBuilder = ImmutableSortedDictionary.CreateBuilder<IntervalClass, int>();
        foreach (var intervalClass in IntervalClass.Items)
        {
            dictBuilder[intervalClass] = countByIntervalClass.GetValueOrDefault(intervalClass, 0);
        }

        var dict = dictBuilder.ToImmutable();
        _countByIntervalClass = dict;
        Value = ToBase12Value(dict);
    }

    public static IntervalClassVector CreateFrom<T>(IEnumerable<T> items) 
        where T : IStaticNorm<T, IntervalClass>, IValueObject
            => items.ToIntervalClassVector();

    /// <summary>
    /// Gets the base 12 value (Sum of ordered)
    /// </summary>
    public int Value { get; }
    public int Hemitonia => this[IntervalClass.Hemitone];
    public int Tritonia => this[IntervalClass.Tritone];
    public bool IsHemitonic => Hemitonia > 0;
    public bool IsTritonic => Tritonia > 0;
    /// <summary>
    /// The deep scale property has important implications is the tone commonality and modulation of the diatonic scale.
    /// </summary>
    /// <remarks>
    /// See https://www.wikiwand.com/en/Common_tone_(scale) - See common tone theorem
    /// See https://ftp.isdi.co.cu/Biblioteca/BIBLIOTECA%20UNIVERSITARIA%20DEL%20ISDI/COLECCION%20DE%20LIBROS%20ELECTRONICOS/LE-1433/LE-1433.pdf - Page 42 (Modulation, Common Tones, and the Deep Scale)
    /// See https://en.wikipedia.org/wiki/Common_tone_(scale)#Deep_scale_property
    /// </remarks>
    public bool IsDeepScale => _countByIntervalClass.Values.Distinct().Count() == _countByIntervalClass.Values.Count(); 
    public static implicit operator int(IntervalClassVector vector) => vector.Value;
    public static implicit operator IntervalClassVector(int value) => FromBase12Value(value);

    public override string ToString() => $"<{string.Join(" ", _countByIntervalClass.Values)}>";

    private static IntervalClassVector FromBase12Value(int value)
    {
        var dictBuilder = ImmutableSortedDictionary.CreateBuilder<IntervalClass, int>();
        var dividend = value;
        var intervalClasses = IntervalClass.Range(1, 6).Reverse();
        foreach (var intervalClass in intervalClasses)
        {
            var count = dividend % 12;
            dictBuilder.Add(intervalClass, count);
            dividend /= 12;
        }
        return new(dictBuilder.ToImmutable());
    }

    private static int ToBase12Value(IReadOnlyDictionary<IntervalClass, int> countByIc)
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
