namespace GA.Business.Core.Fretboard;

using System.Collections;
using System.Collections.Immutable;
using Primitives;
using GA.Core;

public class Positions<T> : IReadOnlyCollection<T> 
    where T: Position
{
    private readonly IReadOnlyCollection<T> _positions;
    private readonly ILookup<Str, T> _positionsByStr;

    public Positions(IEnumerable<T> positions)
    {
        _positions = positions.ToImmutableList().AsPrintable();

        _positionsByStr = _positions.ToLookup(open => open.Str);
    }

    public IReadOnlyCollection<T> this[Str str] => _positionsByStr[str].ToImmutableList();
    public IEnumerator<T> GetEnumerator() => _positions.GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    public int Count => _positions.Count;
    public override string ToString() => _positions.ToString() ?? string.Empty;
}