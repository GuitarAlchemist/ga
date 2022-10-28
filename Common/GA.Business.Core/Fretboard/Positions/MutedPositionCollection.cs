namespace GA.Business.Core.Fretboard.Positions;

using Primitives;

[PublicAPI]
public class MutedPositionCollection : PositionCollection<Position.Muted>
{
    private readonly Lazy<ILookup<Str, Position.Muted>> _lazyPositionsByStr;

    public MutedPositionCollection(IReadOnlyCollection<Position.Muted> positions)
        : base(positions)
    {
        _lazyPositionsByStr = new(() => positions.ToLookup(position => position.Str));
    }

    public IEnumerable<Position.Muted> this[Str str] => _lazyPositionsByStr.Value[str];
}