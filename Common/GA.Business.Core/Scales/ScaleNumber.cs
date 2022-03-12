namespace GA.Business.Core.Scales;

using Atonal;
using Notes;

public class ScaleNumber
{
    #region Equality members

    protected bool Equals(ScaleNumber other)
    {
        return Value == other.Value;
    }

    public override bool Equals(object? obj)
    {
        if (ReferenceEquals(null, obj)) return false;
        if (ReferenceEquals(this, obj)) return true;
        if (obj.GetType() != this.GetType()) return false;
        return Equals((ScaleNumber) obj);
    }

    public override int GetHashCode()
    {
        return Value;
    }

    public static bool operator ==(ScaleNumber? left, ScaleNumber? right)
    {
        return Equals(left, right);
    }

    public static bool operator !=(ScaleNumber? left, ScaleNumber? right)
    {
        return !Equals(left, right);
    }

    #endregion


    public static IEnumerable<ScaleNumber>  GetAllValid()
    {
        var count = 1 << 12;
        for (var i = 0; i < count; i++)
        {
            if ((i & 1) == 0) continue; // Does not contain root, invalid
            yield return new(i);
        }
    }

    public ScaleNumber(int value)
    {
        Value = value;

        PitchClassSet = PitchClassSet.FromIdentity(Value);
        Notes = PitchClassSet.GetNotes();
        IntervalVector = new(Value);
        IsValid = PitchClassSet.Contains(0);
    }

    public static implicit operator ScaleNumber(int value) => new(value);
    public static implicit operator int(ScaleNumber scaleNumber) => scaleNumber.Value;

    public int Value { get; }
    public PitchClassSet PitchClassSet { get; }
    public IReadOnlyCollection<Note.Chromatic> Notes { get; }
    public IntervalVector IntervalVector { get; }
    public bool IsValid { get; }
    public string ScaleName => ScaleNameByNumber.Get(this);
    public string ScaleVideoUrl => ScaleVideoUrlByNumber.Get(this);
    public string ScalePageUrl => $"https://ianring.com/musictheory/scales/{Value}";


    public override string ToString()
    {
        var name = ScaleNameByNumber.Get(this);
        ;
        if (string.IsNullOrEmpty(name)) return Value.ToString();
        return $"{Value} ({name})";
    }
}