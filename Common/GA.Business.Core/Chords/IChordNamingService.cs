namespace GA.Business.Core.Chords;

using Atonal;
using Unified;

/// <summary>
/// Application-friendly façade for chord naming that delegates to
/// the unified static <see cref="ChordTemplateNamingService"/>.
/// Provides DI-friendly access and easier testing/mocking.
/// </summary>
public interface IChordNamingService
{
    // Unified modal naming (Roman numerals)
    string GenerateModalChordName(
        UnifiedModeInstance mode,
        int degree,
        ChordExtension extension,
        ChordStackingType stacking = ChordStackingType.Tertian);

    // ChordTemplate overloads
    string GetBestChordName(ChordTemplate template, PitchClass root, PitchClass? bassNote = null);
    IEnumerable<string> GetAllNamingOptions(ChordTemplate template, PitchClass root, PitchClass? bassNote = null);
    ChordTemplateNamingService.ComprehensiveChordName GenerateComprehensiveNames(
        ChordTemplate template,
        PitchClass root,
        PitchClass? bassNote = null);

    // ChordFormula overloads
    string GetBestChordName(ChordFormula formula, PitchClass root, PitchClass? bassNote = null);
    IEnumerable<string> GetAllNamingOptions(ChordFormula formula, PitchClass root, PitchClass? bassNote = null);
    ChordTemplateNamingService.ComprehensiveChordName GenerateComprehensiveNames(
        ChordFormula formula,
        PitchClass root,
        PitchClass? bassNote = null);

    // Interval list overloads
    string GetBestChordName(IEnumerable<ChordFormulaInterval> intervals, string formulaName, PitchClass root, PitchClass? bassNote = null);
    IEnumerable<string> GetAllNamingOptions(IEnumerable<ChordFormulaInterval> intervals, string formulaName, PitchClass root, PitchClass? bassNote = null);
    ChordTemplateNamingService.ComprehensiveChordName GenerateComprehensiveNames(
        IEnumerable<ChordFormulaInterval> intervals,
        string formulaName,
        PitchClass root,
        PitchClass? bassNote = null);
}
