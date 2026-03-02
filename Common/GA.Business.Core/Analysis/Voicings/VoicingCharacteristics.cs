namespace GA.Business.Core.Analysis.Voicings;

public record VoicingCharacteristics(
    ChordIdentification ChordId,
    double DissonanceScore,
    double Consonance,
    int IntervalSpread,
    int NoteCount,
    string IntervalClassVector,
    bool IsRootless,
    string? DropVoicing,
    bool IsOpenVoicing,
    List<string> Features,
    string[] SemanticTags
);
