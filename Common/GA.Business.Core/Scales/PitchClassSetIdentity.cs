namespace GA.Business.Core.Scales;

using Notes.Extensions;

using Atonal;
using Notes;

/// <summary>
/// Unique identifier for a pitch class set.
/// </summary>
/// <remarks>
/// See https://ianring.com/musictheory/scales/
/// </remarks>
public class PitchClassSetIdentity
{
    #region Equality members

    protected bool Equals(PitchClassSetIdentity other)
    {
        return Value == other.Value;
    }

    public override bool Equals(object? obj)
    {
        if (ReferenceEquals(null, obj)) return false;
        if (ReferenceEquals(this, obj)) return true;
        if (obj.GetType() != GetType()) return false;
        return Equals((PitchClassSetIdentity) obj);
    }

    public override int GetHashCode()
    {
        return Value;
    }

    public static bool operator ==(PitchClassSetIdentity? left, PitchClassSetIdentity? right)
    {
        return Equals(left, right);
    }

    public static bool operator !=(PitchClassSetIdentity? left, PitchClassSetIdentity? right)
    {
        return !Equals(left, right);
    }

    #endregion

    public static PitchClassSetIdentity FromNotes(IEnumerable<Note> notes) => new(notes.ToPitchClassSet().GetIdentity());

    public static IEnumerable<PitchClassSetIdentity> GetAllValid()
    {
        var count = 1 << 12;
        for (var i = 0; i < count; i++)
        {
            if ((i & 1) == 0) continue; // Does not contain root, invalid
            yield return new(i);
        }
    }

    public PitchClassSetIdentity(int value)
    {
        Value = value;

        PitchClassSet = PitchClassSet.FromIdentity(Value);
        Notes = PitchClassSet.GetNotes();
        IntervalVector = new(Notes);
        IsValid = PitchClassSet.Contains(0);
    }

    public static implicit operator PitchClassSetIdentity(int value) => new(value);
    public static implicit operator int(PitchClassSetIdentity pitchClassSetIdentity) => pitchClassSetIdentity.Value;

    public int Value { get; }
    public PitchClassSet PitchClassSet { get; }
    public IReadOnlyCollection<Note.Chromatic> Notes { get; }
    public IntervalVector IntervalVector { get; }
    public bool IsValid { get; }
    public string ScaleName => ScaleNameByIdentity.Get(this);
    public string ScaleVideoUrl => ScaleVideoUrlByIdentity.Get(this);
    public string ScalePageUrl => $"https://ianring.com/musictheory/scales/{Value}";

    public override string ToString()
    {
        var name = ScaleNameByIdentity.Get(this);
        ;
        if (string.IsNullOrEmpty(name)) return Value.ToString();
        return $"{Value} ({name})";
    }
}