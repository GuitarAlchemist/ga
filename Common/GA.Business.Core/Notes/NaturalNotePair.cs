namespace GA.Business.Core.Notes;

using Primitives;
using System;

public readonly struct NaturalNotePair(NaturalNote note1, NaturalNote note2)
{
    #region Equality members

    public bool Equals(NaturalNotePair other) => Note1.Equals(other.Note1) && Note2.Equals(other.Note2);

    public override bool Equals(object? obj)
    {
        if (obj is null) return false;
        return obj.GetType() == GetType() && Equals((NaturalNotePair) obj);
    }

    public override int GetHashCode() => HashCode.Combine(Note1, Note2);
    
    public static bool operator ==(NaturalNotePair left, NaturalNotePair right) => left.Equals(right);
    public static bool operator !=(NaturalNotePair left, NaturalNotePair right) => !(left == right);

    #endregion

    /// <summary>
    /// Gets the first note of the pair
    /// </summary>
    public NaturalNote Note1 { get; } = note1;
    
    /// <summary>
    /// Gets the second note of the pair
    /// </summary>
    public NaturalNote Note2 { get; } = note2;

    /// <inheritdoc />
    public override string ToString() => $"({Note1},{Note2})";
}