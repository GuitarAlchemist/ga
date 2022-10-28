namespace GA.Business.Core.Atonal;

using Primitives;
using Notes;
using GA.Core.Collections;

/// <summary>
/// An interval vector represents the ordered occurence for each interval class (e.g. Major Scale => 2, 5, 4, 3, 6, 1)
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
                                          IReadOnlyCollection<int>, IComparable<IntervalClassVector>
{
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
        if (obj.GetType() != GetType()) return false;
        return Equals((IntervalClassVector) obj);
    }

    public override int GetHashCode() => Value;

    #endregion

    #region Indexer members

    public int this[IntervalClass ic] => _countByIc.TryGetValue(ic, out var count) ? count : 0;

    #endregion

    #region Enumerable members

    public IEnumerator<int> GetEnumerator() => _orderedIcCounts.GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => ((IEnumerable) _orderedIcCounts).GetEnumerator();
    public int Count => _orderedIcCounts.Count;

    #endregion

    private readonly ImmutableDictionary<IntervalClass, int> _countByIc;
    private readonly ImmutableList<int> _orderedIcCounts;

    public IntervalClassVector(IReadOnlyCollection<Note> notes)
    {
        if (notes == null) throw new ArgumentNullException(nameof(notes));

        var countByIc = GetCountByIntervalClass(notes);
        var orderedIcCounts = GetOrderedIcCounts(countByIc);

        _orderedIcCounts = orderedIcCounts;
        _countByIc = countByIc;
        Value = GetValue(orderedIcCounts);

        static ImmutableDictionary<IntervalClass, int> GetCountByIntervalClass(IReadOnlyCollection<Note> notes)
        {
            var intervalClasses =
                from n1 in notes
                from n2 in notes
                where n1 < n2
                select n1.GetIntervalClass(n2);
                
            var result = intervalClasses
                .ToLookup(ic => ic)
                .Select(grouping => (Ic: grouping.Key, Count: grouping.Count()))
                .ToImmutableDictionary(tuple => tuple.Ic, tuple => tuple.Count);

            return result;
        }

        static ImmutableList<int> GetOrderedIcCounts(ImmutableDictionary<IntervalClass, int> countByIc) =>
            IntervalClass.Items.Where(ic => ic.Value > 0) // Omit the unison
                .Select(ic => countByIc.TryGetValue(ic, out var count) ? count : 0)
                .ToImmutableList();

        static int GetValue(IEnumerable<int> orderedIcCounts)
        {
            var result = 0;
            var weight = 1;
            foreach (var count in orderedIcCounts)
            {
                result += weight * count;
                weight *= 12;
            }

            return result;
        }
    }

    public int Value { get; }
    public int Hemitonia => this[IntervalClass.Hemitone];
    public int Tritonia => this[IntervalClass.Tritone];
    public bool IsHemitonic => Hemitonia > 0;
    public bool IsTritonic => Tritonia > 0;
    public static implicit operator int(IntervalClassVector vector) => vector.Value;

    public override string ToString() => Description();

    public string Description()
    {
        var sb = new StringBuilder();
        sb.Append("<");
        sb.Append(string.Join(" ", _orderedIcCounts));
        sb.Append(">");
        return sb.ToString();
    }
}
