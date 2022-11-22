namespace GA.Business.Core.Fretboard.Positions;

using Primitives;

public readonly record struct PositionLocation(Str Str, Fret Fret)
{
    #region RelationalMembers

    public static IComparer<PositionLocation> StrComparer { get; } = new StrRelationalComparer();
    private sealed class StrRelationalComparer : IComparer<PositionLocation>
    {
        public int Compare(PositionLocation x, PositionLocation y) => x.Str.CompareTo(y.Str);
    }

    #endregion

    public override string ToString() => $"str: {Str}; fret: {Fret}";
}