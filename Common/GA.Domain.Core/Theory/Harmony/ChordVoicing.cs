namespace GA.Domain.Core.Theory.Harmony;

using Atonal;

/// <summary>
///     Represents a specific voicing of a chord
/// </summary>
public class ChordVoicing
{
    /// <summary>
    ///     Initializes a new instance of the ChordVoicing class
    /// </summary>
    public ChordVoicing(ChordTemplate chordTemplate, IEnumerable<ChordTone> chordTones, PitchClass bass)
    {
        ChordTemplate = chordTemplate ?? throw new ArgumentNullException(nameof(chordTemplate));
        ChordTones = chordTones?.ToList().AsReadOnly() ?? throw new ArgumentNullException(nameof(chordTones));
        Bass = bass;
    }

    /// <summary>Gets the chord template</summary>
    public ChordTemplate ChordTemplate { get; }

    /// <summary>Gets the chord tones in this voicing</summary>
    public IReadOnlyList<ChordTone> ChordTones { get; }

    /// <summary>Gets the bass note</summary>
    public PitchClass Bass { get; }

    /// <summary>Gets whether this is an inverted voicing</summary>
    public bool IsInverted => Bass != ChordTones.First().PitchClass;

    /// <summary>Gets the inversion number (0 = root position, 1 = first inversion, etc.)</summary>
    public int GetInversion()
    {
        if (!IsInverted)
        {
            return 0;
        }

        var bassIndex = ChordTones.ToList().FindIndex(ct => ct.PitchClass == Bass);
        return bassIndex == -1 ? 0 : bassIndex;
    }

    public override string ToString()
    {
        return $"{ChordTemplate.Name}{(IsInverted ? $"/{Bass}" : "")}";
    }
}