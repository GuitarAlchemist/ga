namespace GA.Business.Core.Chords.Analysis.Atonal;

using GA.Business.Core.Atonal;
using Abstractions;

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
