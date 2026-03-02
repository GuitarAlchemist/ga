namespace GA.Business.Core.Analysis.Voicings;

public record ChordIdentification(
    string ChordName,
    string RootPitchClass, 
    string HarmonicFunction, 
    bool IsNaturallyOccurring, 
    string FunctionalDescription,
    string Quality,
    object? SlashChordInfo,
    object? ExtensionInfo,
    string? ClosestKey = null
)
{
    public string? AlternateName { get; init; }
}
