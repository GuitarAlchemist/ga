namespace GA.Business.Core.Fretboard.Positions;

using Primitives;

[PublicAPI]
public class MutedPositionCollection : PositionCollection<Position.Muted>
{
    private readonly Lazy<ImmutableDictionary<Str, Position.Muted>> _lazyPositionsByStr;

    public MutedPositionCollection(IReadOnlyCollection<Position.Muted> positions)
        : base(positions)
    {
        _lazyPositionsByStr = new(() => positions.ToImmutableDictionary(position => position.Str));
    }

    /// <summary>
    /// Gets the muted position for the given string.
    /// </summary>
    /// <param name="str">The <see cref="Str"/></param>
    /// <returns>The <see cref="Position.Muted"/></returns>
    public Position.Muted this[Str str] => _lazyPositionsByStr.Value[str];
}