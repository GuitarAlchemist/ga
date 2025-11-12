namespace GA.Business.Core.Fretboard.Voicings.Filtering;

/// <summary>
/// Filtering criteria for voicing analysis
/// </summary>
public class VoicingFilterCriteria
{
    public ChordTypeFilter? ChordType { get; set; }
    public VoicingTypeFilter? VoicingType { get; set; }
    public VoicingCharacteristicFilter? Characteristics { get; set; }
    public KeyContextFilter? KeyContext { get; set; }
    public FretRangeFilter? FretRange { get; set; }
    public NoteCountFilter? NoteCount { get; set; }
    public int MaxResults { get; set; } = 50;
}

public enum ChordTypeFilter
{
    All,
    Triads,
    SeventhChords,
    ExtendedChords, // 9th, 11th, 13th
    MajorChords,
    MinorChords,
    DominantChords,
    DiminishedChords,
    AugmentedChords,
    SuspendedChords
}

public enum VoicingTypeFilter
{
    All,
    Drop2,
    Drop3,
    Drop2And4,
    Rootless,
    ShellVoicings, // Root, 3rd, 7th only
    ClosedPosition,
    OpenPosition
}

public enum VoicingCharacteristicFilter
{
    All,
    OpenVoicingsOnly,
    ClosedVoicingsOnly,
    RootlessOnly,
    WithRootOnly,
    QuartalHarmony,
    SuspendedChords,
    AddedToneChords
}

public enum KeyContextFilter
{
    All,
    DiatonicOnly,
    ChromaticOnly,
    InKeyOfC,
    InKeyOfG,
    InKeyOfD,
    InKeyOfA,
    InKeyOfE,
    InKeyOfF,
    InKeyOfBb,
    InKeyOfEb
}

public enum FretRangeFilter
{
    All,
    OpenPosition,    // 0-4
    MiddlePosition,  // 5-12
    UpperPosition    // 12+
}

public enum NoteCountFilter
{
    All,
    TwoNotes,
    ThreeNotes,  // Triads
    FourNotes,   // Seventh chords
    FiveOrMore   // Extended chords
}

