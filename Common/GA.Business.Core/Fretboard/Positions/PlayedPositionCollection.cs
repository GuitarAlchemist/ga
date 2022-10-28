namespace GA.Business.Core.Fretboard.Positions;

using Primitives;

[PublicAPI]
public class PlayedPositionCollection : PositionCollection<Position.Played>
{
    private readonly Lazy<ILookup<Str, Position.Played>> _lazyPositionsByStr;
    private readonly Lazy<ILookup<Fret, Position.Played>> _lazyPositionsByFret;
    private readonly Lazy<ILookup<PositionLocation, Position.Played>> _lazyPositionsByLocation;

    public PlayedPositionCollection(IReadOnlyCollection<Position.Played> positions)
        : base(positions)
    {
        _lazyPositionsByStr = new(() => positions.ToLookup(position => position.Location.Str));
        _lazyPositionsByFret = new(() => positions.ToLookup(position => position.Location.Fret));
        _lazyPositionsByLocation = new(() => positions.ToLookup(position => position.Location));
    }

    public IEnumerable<Position.Played> this[Str str] => _lazyPositionsByStr.Value[str];
    public IEnumerable<Position.Played> this[Fret fret] => _lazyPositionsByFret.Value[fret];
    public IEnumerable<Position.Played> this[PositionLocation location] => _lazyPositionsByLocation.Value[location];
    public IEnumerable<PositionLocation> Locations => _lazyPositionsByLocation.Value.Select(grouping => grouping.Key);

    public PlayedPositionCollection GetRange(Str str, Fret startFret, int fretCount) =>
        new(this[str].Where(played => played.Location.Fret >= startFret)
            .Take(fretCount)
            .ToImmutableList()
        );
}