namespace GA.Domain.Services.Chords.Analysis.Atonal;

using Abstractions;
using Core.Theory.Atonal;
using Core.Theory.Harmony;

/// <summary>
/// Adapter that exposes the existing static <see cref="AtonalChordAnalysisService"/>
/// through the generic <see cref="IChordAnalysisService"/> abstraction.
/// </summary>
public sealed class AtonalChordAnalysisServiceAdapter : IChordAnalysisService
{
    public bool CanAnalyze(ChordTemplate template)
        => AtonalChordAnalysisService.RequiresAtonalAnalysis(template);

    public string GetSuggestedName(ChordTemplate template, PitchClass root)
        => AtonalChordAnalysisService.GenerateAtonalChordName(template, root);

    public string GetDescription(ChordTemplate template, PitchClass root)
        => AtonalChordAnalysisService.GetAtonalDescription(template, root);
}