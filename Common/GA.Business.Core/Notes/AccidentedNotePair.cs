namespace GA.Business.Core.Notes;

public class AccidentedNotePair(
    Note.Accidented note1, 
    Note.Accidented note2)
{
    #region Equality members

    protected bool Equals(AccidentedNotePair other) => Note1.Equals(other.Note1) && Note2.Equals(other.Note2);

    public override bool Equals(object? obj)
    {
        if (ReferenceEquals(null, obj)) return false;
        if (ReferenceEquals(this, obj)) return true;
        return obj.GetType() == GetType() && Equals((AccidentedNotePair) obj);
    }

    public override int GetHashCode() => HashCode.Combine(Note1, Note2);

    #endregion

    public Note.Accidented Note1 { get; } = note1;
    public Note.Accidented Note2 { get; } = note2;

    public override string ToString() => $"({Note1},{Note2})";
}