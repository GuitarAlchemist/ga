namespace GA.Business.Core.Fretboard.Positions.Grouping;

using Primitives;

public class PositionGroupsGenerator
{
    private readonly Fretboard _fretboard;

    public PositionGroupsGenerator(
        Fretboard fretboard)
    {
        _fretboard = fretboard ?? throw new ArgumentNullException(nameof(fretboard));
    }

    // Work in progress...
    /* 
    private PositionGroup CreateGroup(
        Str str,
        Fret startFret,
        int fretCount = 5)
    {
        var list = new List<Position>
        {
            // _fretboard.Positions.Muted[str],

        };

        var aa = _fretboard.PlayedPositions[str];
        list.AddRange(_fretboard.PlayedPositions.GetRange(str, startFret, fretCount));

        var positions = list.ToImmutableList();

        return new(positions);
    }

    public void GenerateGroups()
    {
        var strCount = _fretboard.StringCount;
        var groups = new ImmutableList<Position>[strCount];
        var groupMemberIndex = new int[strCount];
        foreach (var strIndex in Enumerable.Range(0, strCount))
        {
            groupMemberIndex[strIndex] = 0;
        }

        while (true)
        {

        }
    }
    */

}

