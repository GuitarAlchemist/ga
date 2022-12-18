namespace GA.Business.Core.Fretboard.Primitives;

using Positions;
using GA.Core.Collections;

[PublicAPI]
public class FretVector : IReadOnlyCollection<Fret>,
                          IIndexer<Str, Fret>
{
    #region IIndexer<Str, RelativeFret> Members

    public Fret this[Str key] => _fretByStr[key];

    #endregion

    #region IReadOnlyCollection<Fret> Members

    public IEnumerator<Fret> GetEnumerator() => _fretByStr.Values.GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    public int Count => _fretByStr.Count;

    #endregion

    /// <summary>
    /// Gets the <see cref="ImmutableHashSet{PositionLocation}"/>.
    /// </summary>
    public ImmutableHashSet<PositionLocation> PositionLocations => _lazyPositionLocationsSet.Value;

    private readonly ImmutableSortedDictionary<Str, Fret> _fretByStr;
    private readonly Lazy<ImmutableHashSet<PositionLocation>> _lazyPositionLocationsSet;

    public FretVector(IEnumerable<Fret> frets)
    {
        if (frets == null) throw new ArgumentNullException(nameof(frets));

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

    public override string ToString() => "fret: " + string.Join(" ", _fretByStr.Values);

    private ImmutableHashSet<PositionLocation> GetPositionLocations()
        => _fretByStr.Select(pair => new PositionLocation(pair.Key, pair.Value)).ToImmutableHashSet();

}