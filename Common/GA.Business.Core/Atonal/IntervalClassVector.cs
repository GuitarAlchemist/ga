namespace GA.Business.Core.Atonal;

using Primitives;
using SystemCollectionExtensions = System.Collections.Generic.CollectionExtensions;

/// <summary>
/// Represents ordered occurence for each interval class, (e.g. Major Scale => 2, 5, 4, 3, 6, 1)
/// </summary>
/// <remarks>
/// <see href="https://musictheory.pugetsound.edu/mt21c/IntervalVector.html"/><br/>
/// <see href="https://viva.pressbooks.pub/openmusictheory/chapter/interval-class-vectors/"/><br/>
/// <see href="https://en.wikipedia.org/wiki/Interval_vector"/><br/>
/// <see href="https://harmoniousapp.net/p/d0/Glossary-Atonal-Theory"/><br/>
/// <see href="http://www.jaytomlin.com/music/settheory/help.html"/><br/>
/// <see href="https://viva.pressbooks.pub/openmusictheory/chapter/interval-class-vectors/"/><br/>
/// <see href="https://www.youtube.com/watch?v=KFKMvFzobbw">Prime Form</see><br/>
/// <br/>
/// Modes from a scale share the same Interval Class Vector - Example:<br/>
/// <bt/>
/// Major scale => 254361 | Dorian mode => 254361 | etc...
/// </remarks>
[PublicAPI]
public sealed class IntervalClassVector(IntervalClassVectorId id) :
    IParsable<IntervalClassVector>,
    IIndexer<IntervalClass, int>,
    IReadOnlyCollection<int>,
    IComparable<IntervalClassVector>,
    IEquatable<IntervalClassVector>
{
    #region IParsable<IntervalClassVector> Members

    /// <inheritdoc />
    public static IntervalClassVector Parse(string s, IFormatProvider? provider)
    {
        if (!TryParse(s, provider, out var result)) throw new ArgumentException($"Failed parsing '{s}'", nameof(s));
        return result;
    }

    /// <inheritdoc />
    public static bool TryParse(string? s, IFormatProvider? provider, out IntervalClassVector result)
    {
        result = null!;
        if (string.IsNullOrWhiteSpace(s)) return false;

        // Remove angle brackets and whitespace if present
        var cleaned = s.Trim().Trim('<', '>', ' ');

        // Split into individual numbers
        var segments = cleaned.Split(new[] { ' ', ',' }, StringSplitOptions.RemoveEmptyEntries);

        if (segments.Length != 6) return false; // Must have exactly 6 interval class counts

        var countByIntervalClass = new Dictionary<IntervalClass, int>();
        for (var i = 0; i < segments.Length; i++)
        {
            if (!int.TryParse(segments[i], out var count)) return false;
            countByIntervalClass[IntervalClass.FromValue(i + 1)] = count;
        }

        // Success
        result = new(countByIntervalClass);
        return true;
    }

    #endregion

    #region Indexer members

    /// <summary>
    /// Gets the occurrence count for the interval class
    /// </summary>
    /// <param name="intervalClass">The <see cref="IntervalClass"/></param>
    /// <returns>The occurrence count.</returns>
    public int this[IntervalClass intervalClass] =>
        SystemCollectionExtensions.GetValueOrDefault(Vector, intervalClass, 0);

    #endregion

    #region IReadOnlyCollection<int> members

    public IEnumerator<int> GetEnumerator() => Vector.Values.GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => Vector.Values.GetEnumerator();
    public int Count => Vector.Count;

    #endregion

    #region Relational Members

    public static bool operator <(IntervalClassVector? left, IntervalClassVector? right) =>
        Comparer<IntervalClassVector>.Default.Compare(left, right) < 0;

    public static bool operator >(IntervalClassVector? left, IntervalClassVector? right) =>
        Comparer<IntervalClassVector>.Default.Compare(left, right) > 0;

    public static bool operator <=(IntervalClassVector? left, IntervalClassVector? right) =>
        Comparer<IntervalClassVector>.Default.Compare(left, right) <= 0;

    public static bool operator >=(IntervalClassVector? left, IntervalClassVector? right) =>
        Comparer<IntervalClassVector>.Default.Compare(left, right) >= 0;

    /// <inheritdoc />
    public int CompareTo(IntervalClassVector? other)
    {
        if (ReferenceEquals(this, other)) return 0;
        return other is null ? 1 : Id.CompareTo(other.Id);
    }

    #endregion

    #region Equality members

    public static bool operator ==(IntervalClassVector? left, IntervalClassVector? right) => Equals(left, right);
    public static bool operator !=(IntervalClassVector? left, IntervalClassVector? right) => !Equals(left, right);

    /// <inheritdoc />
    public bool Equals(IntervalClassVector? other) => Id.Equals(other?.Id);

    /// <inheritdoc />
    public override bool Equals(object? obj) =>
        ReferenceEquals(this, obj) || obj is IntervalClassVector other && Equals(other);

    /// <inheritdoc />
    public override int GetHashCode() => id.GetHashCode();

    #endregion

    /// <summary>
    /// Constructs an Interval Class Vector from the number of occurrences for each Interval Class
    /// </summary>
    /// <param name="countByIntervalClass">The <see cref="IReadOnlyDictionary{IntervalClass,Int32}"/></param>
    public IntervalClassVector(IReadOnlyDictionary<IntervalClass, int> countByIntervalClass)
        : this(IntervalClassVectorId.CreateFrom(countByIntervalClass))
    {
    }

    /// <summary>
    /// Gets the <see cref="IntervalClassVectorId"/>
    /// </summary>
    public IntervalClassVectorId Id => id;

    /// <summary>
    /// Gets the <see cref="ImmutableSortedDictionary{IntervalClass, Int32}"/>
    /// </summary>
    public ImmutableSortedDictionary<IntervalClass, int> Vector => Id.Vector;

    /// <summary>
    /// Number of semitone intervals in the vector (See https://ianring.com/musictheory/scales/#hemitonia)
    /// </summary>
    public int Hemitonia => this[IntervalClass.Hemitone];

    /// <summary>
    /// Number of tritone intervals in the vector (See https://ianring.com/musictheory/scales/#hemitonia)
    /// </summary>
    public int Tritonia => this[IntervalClass.Tritone];

    /// <summary>
    /// True if the vector has one or more tritone intervals
    /// </summary>
    public bool IsHemitonic => Hemitonia > 0;

    /// <summary>
    /// True if the vector has one or more tritone intervals
    /// </summary>
    public bool IsTritonic => Tritonia > 0;

    /// <summary>
    /// The deep scale property has important implications is the tone commonality and modulation of the diatonic scale.
    /// </summary>
    /// <remarks>
    /// See https://www.wikiwand.com/en/Common_tone_(scale) - See common tone theorem
    /// See https://ftp.isdi.co.cu/Biblioteca/BIBLIOTECA%20UNIVERSITARIA%20DEL%20ISDI/COLECCION%20DE%20LIBROS%20ELECTRONICOS/LE-1433/LE-1433.pdf - Page 42 (Modulation, Common Tones, and the Deep Scale)
    /// See https://en.wikipedia.org/wiki/Common_tone_(scale)#Deep_scale_property
    /// </remarks>
    public bool IsDeepScale => Vector.Values.Distinct().Count() == Vector.Values.Count();

    /// <inheritdoc />
    /// <remarks>
    /// e.g.
    /// <code>
    /// &lt;2, 5, 4, 3, 6, 1&gt;
    /// </code>
    /// </remarks>
    public override string ToString() => Id.ToString();
}