namespace GA.Domain.Services.Fretboard.Voicings.Analysis;

/// <summary>
///     Inputs to <see cref="ChordClassificationEngine" />. Carries everything any caller can
///     supply about a voiced chord; the caller fills what it has and the engine emits only the
///     mood / style / genre / technique tags the available inputs justify. This decouples
///     classification from the layer-specific carrier types (<c>VoicingCharacteristics</c> at
///     runtime, the OPTIC-K corpus document at index time) so a single rule set governs both.
/// </summary>
public sealed record ChordClassificationContext
{
    /// <summary>Chord-quality family, e.g. <c>"major"</c>, <c>"m"</c>, <c>"diminished"</c>.</summary>
    public required string Quality { get; init; }

    /// <summary>True when the chord carries a 7th-or-beyond extension (jazz / neo-soul / dreamy flavour).</summary>
    public bool HasSeventhOrBeyond { get; init; }

    public double Consonance { get; init; }
    public double DissonanceScore { get; init; }
    public bool IsRootless { get; init; }
    public bool IsOpenVoicing { get; init; }
    public string? DropVoicing { get; init; }
    public int NoteCount { get; init; }
    public int IntervalSpread { get; init; }

    /// <summary>
    ///     Mean MIDI pitch of the voicing. <c>null</c> when no notes are available — the register
    ///     tag is then suppressed and mood rules treat the mean as 0.
    /// </summary>
    public double? MeanMidi { get; init; }
}
