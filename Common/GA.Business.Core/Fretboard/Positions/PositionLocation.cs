namespace GA.Business.Core.Fretboard.Positions;

using Primitives;

public readonly record struct PositionLocation(Str Str, Fret Fret) : IStr, IFret, IComparable<PositionLocation>
{
    public bool IsMuted => Fret == Fret.Muted;
    public bool IsOpen => Fret == Fret.Open;

    public static PositionLocation Muted(Str str)
    {
        return new PositionLocation(str, Fret.Muted);
    }

    public static PositionLocation Open(Str str)
    {
        return new PositionLocation(str, Fret.Open);
    }

    public override string ToString()
    {
        return $"{Str},{Fret}";
    }

    #region RelationalMembers

    public int CompareTo(PositionLocation other)
    {
        var strComparison = Str.CompareTo(other.Str);
        if (strComparison != 0)
        {
            return strComparison;
        }

        return Fret.CompareTo(other.Fret);
    }

    public static bool operator <(PositionLocation left, PositionLocation right)
    {
        return left.CompareTo(right) < 0;
    }

    public static bool operator >(PositionLocation left, PositionLocation right)
    {
        return left.CompareTo(right) > 0;
    }

    public static bool operator <=(PositionLocation left, PositionLocation right)
    {
        return left.CompareTo(right) <= 0;
    }

    public static bool operator >=(PositionLocation left, PositionLocation right)
    {
        return left.CompareTo(right) >= 0;
    }

    public static IComparer<PositionLocation> StrComparer { get; } = new StrRelationalComparer();

    private sealed class StrRelationalComparer : IComparer<PositionLocation>
    {
        public int Compare(PositionLocation x, PositionLocation y)
        {
            return x.Str.CompareTo(y.Str);
        }
    }

    #endregion
}
