namespace GA.Business.Core.SetTheory;

using Notes;
using GA.Business.Core.Notes.Extensions;
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
public sealed class PitchClassSetIdentity : IMusicObjectCollection<PitchClassSetIdentity>, 
                                            IComparable<PitchClassSetIdentity>
{
    #region Relational Members

    public int CompareTo(PitchClassSetIdentity? other)
    {
        if (ReferenceEquals(this, other)) return 0;
        if (ReferenceEquals(null, other)) return 1;
        return Value.CompareTo(other.Value);
    }

    public static bool operator <(PitchClassSetIdentity? left, PitchClassSetIdentity? right) => Comparer<PitchClassSetIdentity>.Default.Compare(left, right) < 0;
    public static bool operator >(PitchClassSetIdentity? left, PitchClassSetIdentity? right) => Comparer<PitchClassSetIdentity>.Default.Compare(left, right) > 0;
    public static bool operator <=(PitchClassSetIdentity? left, PitchClassSetIdentity? right) => Comparer<PitchClassSetIdentity>.Default.Compare(left, right) <= 0;
    public static bool operator >=(PitchClassSetIdentity? left, PitchClassSetIdentity? right) => Comparer<PitchClassSetIdentity>.Default.Compare(left, right) >= 0;

    #endregion

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

    public static PitchClassSetIdentity FromNotes(IEnumerable<Note> notes) => notes.ToPitchClassSet().Identity;
    public static PitchClassSetIdentity FromNotes(params Note[] notes) => FromNotes(notes.ToImmutableArray());

    public static bool ContainsRoot(int value) => (value & 1) == 1; // least significant bit represents the root, which must be present for the Pitch Class Set Identity to be a valid scale

    /// <summary>
    /// Gets all valid pitch class set identities.
    /// </summary>
    /// <returns>The <see cref="IEnumerable{PitchClassSetIdentity}"/></returns>
    // ReSharper disable once InconsistentNaming
    public static IEnumerable<PitchClassSetIdentity> Objects
    {
        get
        {
            const int count = 1 << 12; // 4096 combinations
            for (var value = 0; value < count; value++)
            {
                yield return new(value);
            }
        }
    }

    public PitchClassSetIdentity(int value)
    {
        Value = value;
    }

    public static implicit operator PitchClassSetIdentity(int value) => new(value);
    public static implicit operator int(PitchClassSetIdentity identity) => identity.Value;

    public int Value { get; }
    public PitchClassSet PitchClassSet => PitchClassSet.FromIdentity(this);
    public string ScaleName => ScaleNameByIdentity.Get(this);
    public string ScaleVideoUrl => ScaleVideoUrlByIdentity.Get(this);
    public string ScalePageUrl => $"https://ianring.com/musictheory/scales/{Value}";

    public override string ToString()
    {
        if (string.IsNullOrEmpty(ScaleName)) return $"{Value}";
        return  $"{Value} ({ScaleName})";
    }
}