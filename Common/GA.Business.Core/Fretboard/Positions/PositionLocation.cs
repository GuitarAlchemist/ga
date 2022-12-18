namespace GA.Business.Core.Fretboard.Positions;

using Primitives;

public readonly record struct PositionLocation(Str Str, Fret Fret) : IStr, IFret, IComparable<PositionLocation>
{
    #region RelationalMembers

    public int CompareTo(PositionLocation other)
    {
        var strComparison = Str.CompareTo(other.Str);
        if (strComparison != 0) return strComparison;
        return Fret.CompareTo(other.Fret);
    }

    public static bool operator <(PositionLocation left, PositionLocation right) => left.CompareTo(right) < 0;
    public static bool operator >(PositionLocation left, PositionLocation right) => left.CompareTo(right) > 0;
    public static bool operator <=(PositionLocation left, PositionLocation right) => left.CompareTo(right) <= 0;
    public static bool operator >=(PositionLocation left, PositionLocation right) => left.CompareTo(right) >= 0;

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

    public override string ToString() => $"{Str},{Fret}";
}