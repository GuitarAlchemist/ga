namespace GA.Business.Core.Chords;

using System.Collections.Generic;
using System.Linq;
using Atonal;
using Abstractions;

/// <summary>
/// Facade that orchestrates chord analysis strategies and falls back to the
/// existing naming pipeline when no specialized analyzer applies.
/// </summary>
public sealed class ChordDescriptorService
{
    private readonly IReadOnlyList<IChordAnalysisService> _analyzers;

    public ChordDescriptorService(IEnumerable<IChordAnalysisService> analyzers)
    {
        _analyzers = analyzers?.ToList() ?? new List<IChordAnalysisService>();
    }

    /// <summary>
    /// Returns the best available chord name. Uses the first analyzer whose
    /// <see cref="IChordAnalysisService.CanAnalyze"/> returns true; otherwise
    /// falls back to the unified static naming service.
    /// </summary>
    public string GetName(ChordTemplate template, PitchClass root, PitchClass? bassNote = null)
    {
        var analyzer = _analyzers.FirstOrDefault(a => a.CanAnalyze(template));
        if (analyzer is not null)
        {
            return analyzer.GetSuggestedName(template, root);
        }

        // Fallback to existing naming flow
        return ChordTemplateNamingService.GetBestChordName(template, root, bassNote);
    }

    /// <summary>
    /// Returns a description from the first matching analyzer, otherwise a minimal
    /// description based on the fallback name.
    /// </summary>
    public string GetDescription(ChordTemplate template, PitchClass root, PitchClass? bassNote = null)
    {
        var analyzer = _analyzers.FirstOrDefault(a => a.CanAnalyze(template));
        if (analyzer is not null)
        {
            return analyzer.GetDescription(template, root);
        }

        var name = ChordTemplateNamingService.GetBestChordName(template, root, bassNote);
        return $"{name}";
    }
}
