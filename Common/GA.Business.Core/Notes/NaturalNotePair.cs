using GA.Business.Core.Notes.Primitives;

namespace GA.Business.Core.Notes;

public class NaturalNotePair
{
    #region Equality members

    protected bool Equals(NaturalNotePair other) => Note1.Equals(other.Note1) && Note2.Equals(other.Note2);

    public override bool Equals(object? obj)
    {
        if (ReferenceEquals(null, obj)) return false;
        if (ReferenceEquals(this, obj)) return true;
        return obj.GetType() == GetType() && Equals((NaturalNotePair) obj);
    }

    public override int GetHashCode() => HashCode.Combine(Note1, Note2);

    #endregion

    public NaturalNotePair(
        NaturalNote note1, 
        NaturalNote note2)
    {
        Note1 = note1;
        Note2 = note2;
    }

    public NaturalNote Note1 { get; }
    public NaturalNote Note2 { get; }

    public override string ToString() => $"({Note1},{Note2})";
}