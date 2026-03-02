namespace GA.Domain.Services.Chords.Analysis.Atonal;

using Abstractions;
using ServicesChordTemplate = ChordTemplate;

/// <summary>
///     Adapter that exposes the existing static <see cref="AtonalChordAnalysisService" />
///     through the generic <see cref="IChordAnalysisService" /> abstraction.
/// </summary>
public sealed class AtonalChordAnalysisServiceAdapter : IChordAnalysisService
{
    public bool CanAnalyze(ChordTemplate template)
        => AtonalChordAnalysisService.RequiresAtonalAnalysis(ConvertToServices(template));

    public string GetSuggestedName(ChordTemplate template, PitchClass root)
        => AtonalChordAnalysisService.GenerateAtonalChordName(ConvertToServices(template), root);

    public string GetDescription(ChordTemplate template, PitchClass root)
        => AtonalChordAnalysisService.GetAtonalDescription(ConvertToServices(template), root);

    private static ServicesChordTemplate ConvertToServices(ChordTemplate template)
        => template;
}
