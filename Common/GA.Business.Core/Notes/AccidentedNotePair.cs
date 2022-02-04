namespace GA.Business.Core.Notes;

public class AccidentedNotePair
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

    public AccidentedNotePair(
        Note.AccidentedNote note1, 
        Note.AccidentedNote note2)
    {
        Note1 = note1;
        Note2 = note2;
    }

    public Note.AccidentedNote Note1 { get; }
    public Note.AccidentedNote Note2 { get; }

    public override string ToString() => $"({Note1},{Note2})";
}