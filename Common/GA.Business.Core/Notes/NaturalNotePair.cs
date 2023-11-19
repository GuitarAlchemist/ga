namespace GA.Business.Core.Notes;

using Primitives;


public class NaturalNotePair(NaturalNote note1, 
    NaturalNote note2)
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

    public NaturalNote Note1 { get; } = note1;
    public NaturalNote Note2 { get; } = note2;

    public override string ToString() => $"({Note1},{Note2})";
}