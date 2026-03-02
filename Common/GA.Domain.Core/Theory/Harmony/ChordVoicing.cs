namespace GA.Domain.Core.Theory.Harmony;

using Atonal;

/// <summary>
///     Represents a specific voicing of a chord
/// </summary>
/// <remarks>
///     Initializes a new instance of the ChordVoicing class
/// </remarks>
public class ChordVoicing(ChordTemplate chordTemplate, IEnumerable<ChordTone> chordTones, PitchClass bass)
{

    /// <summary>Gets the chord template</summary>
    public ChordTemplate ChordTemplate { get; } = chordTemplate ?? throw new ArgumentNullException(nameof(chordTemplate));

    /// <summary>Gets the chord tones in this voicing</summary>
    public IReadOnlyList<ChordTone> ChordTones { get; } = chordTones?.ToList().AsReadOnly() ?? throw new ArgumentNullException(nameof(chordTones));

    /// <summary>Gets the bass note</summary>
    public PitchClass Bass { get; } = bass;

    /// <summary>Gets whether this is an inverted voicing</summary>
    public bool IsInverted => Bass != ChordTones[0].PitchClass;

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

    public override string ToString() => $"{ChordTemplate.Name}{(IsInverted ? $"/{Bass}" : "")}";
}
