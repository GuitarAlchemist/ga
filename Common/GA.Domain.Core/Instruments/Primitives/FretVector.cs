namespace GA.Domain.Core.Instruments.Primitives;

using GA.Core.Collections.Abstractions;
using Positions;

[PublicAPI]
public sealed class FretVector : IReadOnlyCollection<Fret>,
    IIndexer<Str, Fret>
{
    private readonly ImmutableSortedDictionary<Str, Fret> _fretByStr;
    private readonly Lazy<ImmutableHashSet<PositionLocation>> _lazyPositionLocationsSet;

    public FretVector(IEnumerable<Fret> frets)
    {
        ArgumentNullException.ThrowIfNull(frets);

        var str = Str.Min;
        var fretByStrBuilder = ImmutableSortedDictionary.CreateBuilder<Str, Fret>();
        foreach (var fret in frets)
        {
            fretByStrBuilder.Add(str, fret);
            str++;
        }

        _fretByStr = fretByStrBuilder.ToImmutable();

        _lazyPositionLocationsSet = new(GetPositionLocations);
    }

    /// <summary>
    ///     Gets the <see cref="ImmutableHashSet{PositionLocation}" />.
    /// </summary>
    public ImmutableHashSet<PositionLocation> PositionLocations => _lazyPositionLocationsSet.Value;

    #region IIndexer<Str, RelativeFret> Members

    public Fret this[Str key] => _fretByStr[key];

    #endregion

    public override string ToString() => "fret: " + string.Join(" ", _fretByStr.Values);

    private ImmutableHashSet<PositionLocation> GetPositionLocations() =>
        [.. _fretByStr.Select(pair => new PositionLocation(pair.Key, pair.Value))];

    #region IReadOnlyCollection<Fret> Members

    public IEnumerator<Fret> GetEnumerator() => _fretByStr.Values.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    public int Count => _fretByStr.Count;

    #endregion
}
