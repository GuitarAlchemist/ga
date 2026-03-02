namespace GA.Domain.Services.Chords;

using Abstractions;
using ServicesChordTemplate = ChordTemplate;
using CoreChordTemplate = ChordTemplate;

/// <summary>
///     Facade that orchestrates chord analysis strategies and falls back to the
///     existing naming pipeline when no specialized analyzer applies.
/// </summary>
public sealed class ChordDescriptorService(IEnumerable<IChordAnalysisService> analyzers)
{
    private readonly IReadOnlyList<IChordAnalysisService> _analyzers = analyzers?.ToList() ?? [];

    /// <summary>
    ///     Returns the best available chord name. Uses the first analyzer whose
    ///     <see cref="IChordAnalysisService.CanAnalyze" /> returns true; otherwise
    ///     falls back to the unified static naming service.
    /// </summary>
    public string GetName(ServicesChordTemplate template, PitchClass root, PitchClass? bassNote = null)
    {
        var coreTemplate = ConvertToCore(template);
        var analyzer = _analyzers.FirstOrDefault(a => a.CanAnalyze(coreTemplate));
        if (analyzer is not null)
        {
            return analyzer.GetSuggestedName(coreTemplate, root);
        }

        // Fallback to existing naming flow
        return HybridChordNamingService.GetBestChordName(template, root, bassNote);
    }

    /// <summary>
    ///     Returns a description from the first matching analyzer, otherwise a minimal
    ///     description based on the fallback name.
    /// </summary>
    public string GetDescription(ServicesChordTemplate template, PitchClass root, PitchClass? bassNote = null)
    {
        var coreTemplate = ConvertToCore(template);
        var analyzer = _analyzers.FirstOrDefault(a => a.CanAnalyze(coreTemplate));
        if (analyzer is not null)
        {
            return analyzer.GetDescription(coreTemplate, root);
        }

        var name = ChordTemplateNamingService.GetBestChordName(coreTemplate, root, bassNote);
        return $"{name}";
    }

    private static CoreChordTemplate ConvertToCore(ServicesChordTemplate template) =>
        new ChordTemplate.Analytical(
            new(template.PitchClassSet.Select(pc => pc)),
            template.Formula,
            template.Name);
}
