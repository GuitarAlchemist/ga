namespace GA.Business.Core.Fretboard.Primitives;

using Positions;

[PublicAPI]
public class FretVector : IReadOnlyCollection<Fret>,
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

    public override string ToString()
    {
        return "fret: " + string.Join(" ", _fretByStr.Values);
    }

    private ImmutableHashSet<PositionLocation> GetPositionLocations()
    {
        return _fretByStr.Select(pair => new PositionLocation(pair.Key, pair.Value)).ToImmutableHashSet();
    }

    #region IReadOnlyCollection<Fret> Members

    public IEnumerator<Fret> GetEnumerator()
    {
        return _fretByStr.Values.GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }

    public int Count => _fretByStr.Count;

    #endregion
}
