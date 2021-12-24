using System.Collections;
using GA.Business.Core.Fretboard.Config;
using GA.Business.Core.Fretboard.Primitives;

namespace GA.Business.Core.Fretboard;

public class FretboardPositions : IReadOnlyCollection<Position>
{
    private readonly IEnumerable<Position> _positions;

    public FretboardPositions(PositionsRange range)
    {
        _positions = GetPositions(range);
        Count = GetPositionCount(range);
    }

    private static IEnumerable<Position> GetPositions(PositionsRange range)
    {
        range.Deconstruct(
            out var stringCount, 
            out var fretCount);
        foreach (var str in Str.GetCollection(stringCount))
        {
            yield return new Position.Muted(str);
            yield return new Position.Open(str);
            foreach (var fret in Fret.GetCollection(1, fretCount - 1))
            {
                yield return new Position.Fretted(str, fret);
            }
        }
    }

    private static int GetPositionCount(PositionsRange range)
    {
        range.Deconstruct(
            out var stringCount, 
            out var fretCount);

        return (fretCount + 1) * stringCount;
    }

    public IEnumerator<Position> GetEnumerator()
    {
        return _positions.GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }

    public int Count { get; }
}