namespace GA.Domain.Core.Instruments.Fretboard.Voicings.Analysis;

public record VoicingCharacteristics(
    ChordIdentification ChordId,
    double DissonanceScore,
    int IntervalSpread,
    int NoteCount,
    string IntervalClassVector,
    bool IsRootless,
    string? DropVoicing,
    bool IsOpenVoicing,
    List<string> Features
);