namespace GA.Business.Core.Notes;

using System;

public class AccidentedNotePair(
    Note.Accidented note1,
    Note.Accidented note2)
{
    public Note.Accidented Note1 { get; } = note1;
    public Note.Accidented Note2 { get; } = note2;

    public override string ToString()
    {
        return $"({Note1},{Note2})";
    }

    #region Equality members

    protected bool Equals(AccidentedNotePair other)
    {
        return Note1.Equals(other.Note1) && Note2.Equals(other.Note2);
    }

    public override bool Equals(object? obj)
    {
        if (ReferenceEquals(null, obj))
        {
            return false;
        }

        if (ReferenceEquals(this, obj))
        {
            return true;
        }

        return obj.GetType() == GetType() && Equals((AccidentedNotePair)obj);
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(Note1, Note2);
    }

    #endregion
}
