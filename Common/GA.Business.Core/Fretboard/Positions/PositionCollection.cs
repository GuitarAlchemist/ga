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
    private readonly Lazy<PlayedPositionCollection> _lazyOpenPositions;

    public PositionCollection(IReadOnlyCollection<T> positions) : base(positions)
    {
        _lazyMutedPositions = new(() => new(positions.OfType<Position.Muted>().ToImmutableArray()));
        _lazyPlayedPositions = new(() => new(positions.OfType<Position.Played>().ToImmutableArray()));
        _lazyOpenPositions = new(() => new(Played[Fret.Open].ToImmutableArray()));
    }

    /// <summary>
    /// Gets muted positions
    /// </summary>
    /// <remarks>
    /// <see cref="MutedPositionCollection"/>
    /// </remarks>
    public MutedPositionCollection Muted => _lazyMutedPositions.Value;

    /// <summary>
    /// Gets played positions (All)
    /// </summary>
    /// <remarks>
    /// <see cref="PlayedPositionCollection"/>
    /// </remarks>
    public PlayedPositionCollection Played => _lazyPlayedPositions.Value;

    /// <summary>
    /// Gets played positions (Open only)
    /// </summary>
    /// <remarks>
    /// <see cref="PlayedPositionCollection"/>
    /// </remarks>
    public PlayedPositionCollection Open => _lazyOpenPositions.Value;
}

[PublicAPI]
public class PositionCollection : PositionCollection<Position>
{
    public PositionCollection(IReadOnlyCollection<Position> positions)
        : base(positions)
    {
    }
}