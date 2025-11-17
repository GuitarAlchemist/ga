namespace GA.Business.Core.Chords;

using Atonal;
using Unified;

/// <summary>
/// Concrete DI-friendly service that forwards to the static
/// <see cref="ChordTemplateNamingService"/> entry points.
/// </summary>
public sealed class ChordNamingService : IChordNamingService
{
    // Unified modal naming (Roman numerals)
    public string GenerateModalChordName(
        UnifiedModeInstance mode,
        int degree,
        ChordExtension extension,
        ChordStackingType stacking = ChordStackingType.Tertian)
        => ChordTemplateNamingService.GenerateModalChordName(mode, degree, extension, stacking);

    // ChordTemplate overloads
    public string GetBestChordName(ChordTemplate template, PitchClass root, PitchClass? bassNote = null)
        => ChordTemplateNamingService.GetBestChordName(template, root, bassNote);

    public IEnumerable<string> GetAllNamingOptions(ChordTemplate template, PitchClass root, PitchClass? bassNote = null)
        => ChordTemplateNamingService.GetAllNamingOptions(template, root, bassNote);

    public ChordTemplateNamingService.ComprehensiveChordName GenerateComprehensiveNames(
        ChordTemplate template,
        PitchClass root,
        PitchClass? bassNote = null)
        => ChordTemplateNamingService.GenerateComprehensiveNames(template, root, bassNote);

    // ChordFormula overloads
    public string GetBestChordName(ChordFormula formula, PitchClass root, PitchClass? bassNote = null)
        => ChordTemplateNamingService.GetBestChordName(formula, root, bassNote);

    public IEnumerable<string> GetAllNamingOptions(ChordFormula formula, PitchClass root, PitchClass? bassNote = null)
        => ChordTemplateNamingService.GetAllNamingOptions(formula, root, bassNote);

    public ChordTemplateNamingService.ComprehensiveChordName GenerateComprehensiveNames(
        ChordFormula formula,
        PitchClass root,
        PitchClass? bassNote = null)
        => ChordTemplateNamingService.GenerateComprehensiveNames(formula, root, bassNote);

    // Interval list overloads
    public string GetBestChordName(IEnumerable<ChordFormulaInterval> intervals, string formulaName, PitchClass root, PitchClass? bassNote = null)
        => ChordTemplateNamingService.GetBestChordName(intervals, formulaName, root, bassNote);

    public IEnumerable<string> GetAllNamingOptions(IEnumerable<ChordFormulaInterval> intervals, string formulaName, PitchClass root, PitchClass? bassNote = null)
        => ChordTemplateNamingService.GetAllNamingOptions(intervals, formulaName, root, bassNote);

    public ChordTemplateNamingService.ComprehensiveChordName GenerateComprehensiveNames(
        IEnumerable<ChordFormulaInterval> intervals,
        string formulaName,
        PitchClass root,
        PitchClass? bassNote = null)
        => ChordTemplateNamingService.GenerateComprehensiveNames(intervals, formulaName, root, bassNote);
}
