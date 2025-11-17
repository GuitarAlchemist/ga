namespace GA.Business.Core.Chords;

using System;
using GA.Business.Core.Atonal;
using AtonalAnalysisNew = GA.Business.Core.Chords.Analysis.Atonal.AtonalChordAnalysisService;
using AtonalAnalysis = GA.Business.Core.Chords.Analysis.Atonal.AtonalChordAnalysisService.AtonalAnalysis;

/// <summary>
/// Backward-compatible forwarding shim. The implementation now lives under
/// GA.Business.Core.Chords.Analysis.Atonal.AtonalChordAnalysisService.
/// </summary>
[Obsolete("Use GA.Business.Core.Chords.Analysis.Atonal.AtonalChordAnalysisService instead.")]
public static class AtonalChordAnalysisService
{
    public static bool RequiresAtonalAnalysis(ChordTemplate template)
        => AtonalAnalysisNew.RequiresAtonalAnalysis(template);

    public static AtonalAnalysis AnalyzeAtonally(ChordTemplate template, PitchClass root)
        => AtonalAnalysisNew.AnalyzeAtonally(template, root);

    public static string GenerateAtonalChordName(ChordTemplate template, PitchClass root)
        => AtonalAnalysisNew.GenerateAtonalChordName(template, root);

    public static string GetAtonalDescription(ChordTemplate template, PitchClass root)
        => AtonalAnalysisNew.GetAtonalDescription(template, root);
}
