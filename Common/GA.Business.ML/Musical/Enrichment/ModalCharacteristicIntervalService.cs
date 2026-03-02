namespace GA.Business.ML.Musical.Enrichment;

using Domain.Core.Primitives.Formulas;
using Domain.Core.Theory.Tonal.Modes;
using Domain.Core.Theory.Tonal.Modes.Diatonic;
using Domain.Core.Theory.Tonal.Modes.Exotic;
using Domain.Core.Theory.Tonal.Modes.Pentatonic;
using Domain.Core.Theory.Tonal.Modes.Symmetric;

/// <summary>
///     Computes characteristic intervals for modes using the domain model.
///     Uses embedding index as primary key (e.g., 109 = Ionian, 110 = Dorian).
/// </summary>
public class ModalCharacteristicIntervalService
{
    private static ModalCharacteristicIntervalService? _instance;

    private readonly Dictionary<int, HashSet<int>> _fullIntervalsByIndex = [];


    // Secondary: name → index (for backward compatibility)

    private readonly Dictionary<string, int> _indexByName = new(StringComparer.OrdinalIgnoreCase);

    // Primary storage: embedding index → intervals

    private readonly Dictionary<int, HashSet<int>> _intervalsByIndex = [];


    private ModalCharacteristicIntervalService() => LoadFromDomainModel();


    public static ModalCharacteristicIntervalService Instance => _instance ??= new();


    private void LoadFromDomainModel()

    {
        // ... (Calls to LoadModes remain same, logic inside LoadModes changes)


        // ═══════════════════════════════════════════════════════════════════════

        // DIATONIC MODES (7-note per family, 1-based degree)

        // ═══════════════════════════════════════════════════════════════════════


        // Major Scale: indices 109-115 (offset 109, degree 1-7)

        LoadModes(MajorScaleMode.Items, EmbeddingSchema.ModalOffset);


        // Harmonic Minor: indices 116-122 (offset 116)

        LoadModes(HarmonicMinorMode.Items, 116);


        // Melodic Minor: indices 123-129 (offset 123)

        LoadModes(MelodicMinorMode.Items, 123);


        // Harmonic Major: indices 130-136 (offset 130)

        LoadModes(HarmonicMajorScaleMode.Items, 130);


        // ═══════════════════════════════════════════════════════════════════════

        // EXOTIC MODES (7-note per family)

        // ═══════════════════════════════════════════════════════════════════════


        LoadModes(DoubleHarmonicScaleMode.Items, 137);

        LoadModes(NeapolitanMajorScaleMode.Items, 144);

        LoadModes(NeapolitanMinorScaleMode.Items, 151);

        LoadModes(EnigmaticScaleMode.Items, 158);

        LoadModes(BebopScaleMode.Items, 165);

        LoadModes(BluesScaleMode.Items, 173);

        LoadModes(PrometheusScaleMode.Items, 179);

        LoadModes(TritoneScaleMode.Items, 185);


        // ═══════════════════════════════════════════════════════════════════════

        // PENTATONIC MODES (5-note scales)

        // ═══════════════════════════════════════════════════════════════════════


        LoadModes(MajorPentatonicMode.Items, 191);

        LoadModes(HirajoshiScaleMode.Items, 196);

        LoadModes(InSenScaleMode.Items, 201);


        // ═══════════════════════════════════════════════════════════════════════

        // SYMMETRIC MODES (variable modes)

        // ═══════════════════════════════════════════════════════════════════════


        LoadModes(WholeToneScaleMode.Items, 206);

        LoadModes(DiminishedScaleMode.Items, 208);

        LoadModes(AugmentedScaleMode.Items, 212);


        // Debug: Console.WriteLine($"[ModalCharacteristicIntervalService] Loaded {_intervalsByIndex.Count} modes by index.");
    }


    private void LoadModes<T>(IEnumerable<T> modes, int baseIndex) where T : ScaleMode

    {
        foreach (var mode in modes)

        {
            // Get 1-based degree, convert to 0-based offset for embedding index

            var degree = GetDegreeValue(mode);

            var embeddingIndex = baseIndex + (degree - 1); // degree 1 → index 0 offset


            var intervals = GetSemitonesFromFormula(mode.Formula);

            var fullIntervals = GetFullSemitonesFromFormula(mode.Formula);


            _intervalsByIndex[embeddingIndex] = intervals;

            _fullIntervalsByIndex[embeddingIndex] = fullIntervals;

            _indexByName[mode.Name] = embeddingIndex;
        }
    }


    private static int GetDegreeValue<T>(T mode) where T : ScaleMode

    {
        // Use reflection to get ParentScaleDegree.Value for typed modes

        var prop = mode.GetType().GetProperty("ParentScaleDegree");

        if (prop == null)
        {
            return 1;
        }


        var degree = prop.GetValue(mode);

        var valueProp = degree?.GetType().GetProperty("Value");

        return (int?)valueProp?.GetValue(degree) ?? 1;
    }


    private static HashSet<int> GetSemitonesFromFormula(ModeFormula formula) =>
    [
        .. formula.CharacteristicIntervals
            .Select(interval => interval.ToSemitones().Value)
    ];


    private static HashSet<int> GetFullSemitonesFromFormula(ModeFormula formula) =>
    [
        .. formula.Intervals
            .Select(interval => interval.ToSemitones().Value % 12)
    ];


    /// <summary>
    ///     Gets characteristic interval semitones by embedding index (e.g., 109 = Ionian).
    /// </summary>
    public HashSet<int>? GetCharacteristicSemitones(int embeddingIndex) =>
        _intervalsByIndex.TryGetValue(embeddingIndex, out var intervals) ? intervals : null;


    /// <summary>
    ///     Gets characteristic interval semitones by mode name (backward compatibility).
    /// </summary>
    public HashSet<int>? GetCharacteristicSemitones(string modeName)

    {
        if (_indexByName.TryGetValue(modeName, out var index))

        {
            return _intervalsByIndex.TryGetValue(index, out var intervals) ? intervals : null;
        }

        return null;
    }


    /// <summary>
    ///     Gets ALL intervals in the mode (not just characteristic).
    /// </summary>
    public HashSet<int>? GetModeIntervals(string modeName)

    {
        if (_indexByName.TryGetValue(modeName, out var index))

        {
            return _fullIntervalsByIndex.TryGetValue(index, out var intervals) ? intervals : null;
        }

        return null;
    }


    /// <summary>
    ///     Gets all embedding indices with registered modes.
    /// </summary>
    public IEnumerable<int> GetAllModeIndices() => _intervalsByIndex.Keys;


    /// <summary>
    ///     Gets all mode names (backward compatibility).
    /// </summary>
    public IEnumerable<string> GetAllModeNames() => _indexByName.Keys;
}
