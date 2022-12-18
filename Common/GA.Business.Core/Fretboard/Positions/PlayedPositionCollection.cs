namespace GA.Business.Core.Fretboard.Positions;

using Primitives;

[PublicAPI]
public class PlayedPositionCollection : PositionCollection<Position.Played>
{
    private readonly Lazy<ILookup<Str, Position.Played>> _lazyPositionsByStr;
    private readonly Lazy<ILookup<Fret, Position.Played>> _lazyPositionsByFret;
    private readonly Lazy<ImmutableDictionary<PositionLocation, Position.Played>> _lazyPositionsByLocation;

    public PlayedPositionCollection(IReadOnlyCollection<Position.Played> positions)
        : base(positions)
    {
        _lazyPositionsByStr = new(() => positions.ToLookup(position => position.Location.Str));
        _lazyPositionsByFret = new(() => positions.ToLookup(position => position.Location.Fret));
        _lazyPositionsByLocation = new(() => positions.ToImmutableDictionary(position => position.Location));
    }

    /// <summary>
    /// Gets played positions by string
    /// </summary>
    /// <param name="str">The <see cref="Str"/></param>
    /// <returns>The collection of <see cref="Position.Played"/> positions</returns>
    public IEnumerable<Position.Played> this[Str str] => _lazyPositionsByStr.Value[str];

    /// <summary>
    /// Gets played positions by fret
    /// </summary>
    /// <param name="fret">The <see cref="Fret"/></param>
    /// <returns>The collection of <see cref="Position.Played"/> positions</returns>
    public IEnumerable<Position.Played> this[Fret fret] => _lazyPositionsByFret.Value[fret];

    /// <summary>
    /// Gets played position by location
    /// </summary>
    /// <param name="location">The <see cref="PositionLocation"/></param>
    /// <returns>The collection of <see cref="Position.Played"/> positions</returns>
    public Position.Played this[PositionLocation location] => FromLocation(location);

    /// <summary>
    /// Gets all position locations
    /// </summary>
    /// <returns>The <see cref="IEnumerable{PositionLocation}"/></returns>
    public IEnumerable<PositionLocation> Locations => _lazyPositionsByLocation.Value.Select(grouping => grouping.Key);

    public PlayedPositionCollection GetRange(Str str, Fret startFret, int fretCount) =>
        new(this[str].Where(played => played.Location.Fret >= startFret)
            .Take(fretCount)
            .ToImmutableList()
        );

    /// <summary>
    /// Creates a new played position collection for the given locations.
    /// </summary>
    /// <param name="positionLocations">The <see cref="IEnumerable{PositionLocation}"/>.</param>
    /// <returns>The resulting <see cref="PlayedPositionCollection"/>.</returns>
    public PlayedPositionCollection FromLocations(IEnumerable<PositionLocation> positionLocations) =>
        new(positionLocations.Select(FromLocation).OrderBy(played => played).ToImmutableArray());

    private Position.Played FromLocation(PositionLocation location) => _lazyPositionsByLocation.Value[location];
}