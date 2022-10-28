namespace GA.Business.Core.Fretboard.Positions;

using System.Linq;

using GA.Core.Collections;
using Primitives;

[PublicAPI]
public class PositionCollection<T> : LazyPrintableCollectionBase<T>
    where T : Position
{
    private readonly Lazy<MutedPositionCollection> _lazyMutedPositions;
    private readonly Lazy<PlayedPositionCollection> _lazyPlayedPositions;

    public PositionCollection(IReadOnlyCollection<T> positions) : base(positions)
    {
        _lazyMutedPositions = new(() => new(positions.OfType<Position.Muted>().ToImmutableArray()));
        _lazyPlayedPositions = new(() => new(positions.OfType<Position.Played>().ToImmutableArray()));
    }

    /// <summary>
    /// Gets the <see cref="MutedPositionCollection"/>
    /// </summary>
    public MutedPositionCollection Muted => _lazyMutedPositions.Value;

    /// <summary>
    /// Gets the <see cref="PlayedPositionCollection"/>
    /// </summary>
    public PlayedPositionCollection Played => _lazyPlayedPositions.Value;
}

[PublicAPI]
public class PositionCollection : PositionCollection<Position>
{
    public PositionCollection(IReadOnlyCollection<Position> positions)
        : base(positions)
    {
    }
}