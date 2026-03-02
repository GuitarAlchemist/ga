namespace GA.Domain.Core.Theory.Harmony;

using Atonal;
using Tonal;
using Tonal.Modes;

/// <summary>
/// Represents a theoretical template for a chord, which can be scale-derived or analytical.
/// A template defines the "shape" of a chord before it is realized as a specific Chord instance with a root.
/// </summary>
/// <remarks>
/// Example: Cmaj7 is a chord template. Cmaj7, Dmaj7, Emaj7 are all chords derived from that template.
/// </remarks>
public abstract record ChordTemplate
{
    /// <summary>Specific name of the chord template.</summary>
    public abstract string Name { get; }

    /// <summary>The underlying tonal realization of the chord's pitch classes.</summary>
    public abstract PitchClassSet PitchClassSet { get; }

    /// <summary>The intervals defining this chord from its theoretical root.</summary>
    public abstract ChordFormula Formula { get; }

    /// <summary>The collection of intervals that define this chord.</summary>
    public IReadOnlyList<ChordFormulaInterval> Intervals => Formula.Intervals;

    /// <summary>Number of notes in the template.</summary>
    public int NoteCount => PitchClassSet.Count;

    /// <summary>The triad quality (Major, Minor, etc.).</summary>
    public ChordQuality Quality => Formula.Quality;

    /// <summary>The upper-structure extension (7, 9, 11, 13).</summary>
    public ChordExtension Extension => Formula.Extension;

    /// <summary>The stacking logic (Tertian, Quartal, etc.).</summary>
    public ChordStackingType StackingType => Formula.StackingType;

    /// <summary>
    /// Represents a chord derived from a specific degree of a scale mode.
    /// </summary>
    public record TonalModal(ChordFormula ChordFormula, ScaleMode ParentScale, int ScaleDegree) : ChordTemplate
    {
        public override string Name => ChordFormula.Name;
        public override PitchClassSet PitchClassSet { get; } = new(ChordFormula.Intervals.Select(i => (PitchClass)i.Interval.Semitones.Value).Concat([(PitchClass)0]));
        public override ChordFormula Formula => ChordFormula;
        public string Description => $"{ParentScale.Name} degree {ScaleDegree} ({ChordFormula.Name})";

        /// <summary>Inferred harmonic function based on the scale degree (e.g. Tonic, Dominant).</summary>
        public HarmonicFunction Function => HarmonicFunctionExtensions.FromDegree(ScaleDegree);
    }

    /// <summary>
    /// Represents a chord derived from arbitrary pitch class set analysis.
    /// </summary>
    public record Analytical(PitchClassSet InnerPitchClassSet, ChordFormula ChordFormula, string? AnalyticalName = null) : ChordTemplate
    {
        public override string Name => AnalyticalName ?? ChordFormula.Name;
        public override PitchClassSet PitchClassSet => InnerPitchClassSet;
        public override ChordFormula Formula => ChordFormula;
        public string Description => $"Analytical: {Name}";
        public Dictionary<string, object> AnalysisData { get; init; } = [];

        public static Analytical FromPitchClassSet(PitchClassSet pcs, string name) =>
            new(pcs, ChordFormula.FromSemitones(name, [.. pcs.Where(p => p.Value != 0).Select(p => (int)p.Value)]), name);

        public static Analytical FromPitchClassSet(PitchClassSet pcs, PitchClass root, string name) =>
            new(new(pcs.Select(p => p - root)), ChordFormula.FromSemitones(name, [.. pcs.Select(p => p - root).Where(p => p.Value != 0).Select(p => (int)p.Value)]), name);

        public static Analytical FromSetTheory(ChordFormula formula, string name) =>
            new(new PitchClassSet(formula.Intervals.Select(i => (PitchClass)i.Interval.Semitones.Value).Concat([(PitchClass)0])), formula, name);
    }

    public override string ToString() => Name;
}
