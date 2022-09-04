namespace GA.Business.Core.Fretboard;

using Primitives;


public class FretPositions : Positions<Position.Fretted>
{
    public FretPositions(
        IReadOnlyCollection<Position.Fretted> positions,
        Fret fret) 
            : base(GetPositions(positions, fret))
    {
        Fret = fret;
    }

    private static IEnumerable<Position.Fretted> GetPositions(
        IReadOnlyCollection<Position.Fretted> positions,
        Fret fret)
    {
        var result = positions
            .Where(fretted => fretted.Fret == fret)
            .ToImmutableList();

        return result;
    }

    public Fret Fret { get; }
}