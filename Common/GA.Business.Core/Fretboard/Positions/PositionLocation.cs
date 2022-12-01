namespace GA.Business.Core.Fretboard.Positions;

using Primitives;

public readonly record struct PositionLocation(Str Str, Fret Fret) : IStr, IFret
{
    #region RelationalMembers

    public static IComparer<PositionLocation> StrComparer { get; } = new StrRelationalComparer();
    private sealed class StrRelationalComparer : IComparer<PositionLocation>
    {
        public int Compare(PositionLocation x, PositionLocation y) => x.Str.CompareTo(y.Str);
    }

    #endregion

    public static PositionLocation Muted(Str str) => new(str, Fret.Muted);
    public static PositionLocation Open(Str str) => new(str, Fret.Open);

    public bool IsMuted => Fret == Fret.Muted;
    public bool IsOpen => Fret == Fret.Open;

    public override string ToString() => $"str: {Str}; fret: {Fret}";
}