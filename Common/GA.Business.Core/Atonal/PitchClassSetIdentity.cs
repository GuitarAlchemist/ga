namespace GA.Business.Core.Atonal;

using Notes.Extensions;
using Notes;
using Scales;

/// <summary>
/// Unique identifier for a pitch class set.
/// </summary>
/// <remarks>
/// See https://ianring.com/musictheory/scales/
///
/// Examples:
/// Dorian https://ianring.com/musictheory/scales/1709
/// Phrygian https://ianring.com/musictheory/scales/1451
/// </remarks>
public class PitchClassSetIdentity
{
    #region Equality members

    protected bool Equals(PitchClassSetIdentity other) => Value == other.Value;
    public override bool Equals(object? obj)
    {
        if (ReferenceEquals(null, obj)) return false;
        if (ReferenceEquals(this, obj)) return true;
        if (obj.GetType() != GetType()) return false;
        return Equals((PitchClassSetIdentity)obj);
    }

    public override int GetHashCode() => Value;
    public static bool operator ==(PitchClassSetIdentity? left, PitchClassSetIdentity? right) => Equals(left, right);
    public static bool operator !=(PitchClassSetIdentity? left, PitchClassSetIdentity? right) => !Equals(left, right);

    #endregion

    public static PitchClassSetIdentity FromNotes(IEnumerable<Note> notes)
    {
        var pitchClassSet = notes.ToPitchClassSet();
        return pitchClassSet.Identity;
    }

    public static bool IsValid(int value) => (value & 1) == 1; // least significant bit represents the root, which must be present for the Pitch Class Set Identity to be valid

    /// <summary>
    /// Gets all valid pitch class set identities.
    /// </summary>
    /// <returns>The <see cref="IEnumerable{PitchClassSetIdentity}"/></returns>
    public static IEnumerable<PitchClassSetIdentity> ValidIdentities()
    {
        const int count = 1 << 12;
        for (var value = 0; value < count; value++)
        {
            if (!IsValid(value)) continue;
            yield return new(value);
        }
    }

    public PitchClassSetIdentity(int value)
    {
        if (!IsValid(value)) throw new ArgumentException("Value must be odd");

        Value = value;
    }

    public static implicit operator PitchClassSetIdentity(int value) => new(value);
    public static implicit operator int(PitchClassSetIdentity identity) => identity.Value;

    public int Value { get; }
    public PitchClassSet PitchClassSet => PitchClassSet.FromIdentity(this);
    public IReadOnlyCollection<Note.Chromatic> Notes => PitchClassSet.Notes;
    public IntervalVector IntervalVector => new(Notes);
    public string ScaleName => ScaleNameByIdentity.Get(this);
    public string ScaleVideoUrl => ScaleVideoUrlByIdentity.Get(this);
    public string ScalePageUrl => $"https://ianring.com/musictheory/scales/{Value}";

    public override string ToString() => $"{Value} ({PitchClassSet})";
}