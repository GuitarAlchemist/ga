namespace GA.Business.ML.Musical.Enrichment;

using System;
using System.Collections.Generic;
using System.Linq;
using Core.Intervals;
using Core.Tonal.Modes;
using Core.Tonal.Modes.Diatonic;
using Core.Tonal.Modes.Exotic;
using Core.Tonal.Modes.Pentatonic;
using Core.Tonal.Modes.Symmetric;
using Embeddings;

/// <summary>
/// Computes characteristic intervals for modes using the domain model.
/// Uses embedding index as primary key (e.g., 109 = Ionian, 110 = Dorian).
/// </summary>
public class ModalCharacteristicIntervalService
{
    private static ModalCharacteristicIntervalService? _instance;

        // Primary storage: embedding index → intervals

        private readonly Dictionary<int, HashSet<int>> _intervalsByIndex = new();

        private readonly Dictionary<int, HashSet<int>> _fullIntervalsByIndex = new();



        // Secondary: name → index (for backward compatibility)

        private readonly Dictionary<string, int> _indexByName = new(StringComparer.OrdinalIgnoreCase);



        public static ModalCharacteristicIntervalService Instance => _instance ??= new ModalCharacteristicIntervalService();



        private ModalCharacteristicIntervalService()

        {

            LoadFromDomainModel();

        }



        private void LoadFromDomainModel()

        {

            // ... (Calls to LoadModes remain same, logic inside LoadModes changes)



            // ═══════════════════════════════════════════════════════════════════════

            // DIATONIC MODES (7-note per family, 1-based degree)

            // ═══════════════════════════════════════════════════════════════════════



            // Major Scale: indices 109-115 (offset 109, degree 1-7)

            LoadModes(MajorScaleMode.Items, baseIndex: EmbeddingSchema.ModalOffset);



            // Harmonic Minor: indices 116-122 (offset 116)

            LoadModes(HarmonicMinorMode.Items, baseIndex: 116);



            // Melodic Minor: indices 123-129 (offset 123)

            LoadModes(MelodicMinorMode.Items, baseIndex: 123);



            // Harmonic Major: indices 130-136 (offset 130)

            LoadModes(HarmonicMajorScaleMode.Items, baseIndex: 130);



            // ═══════════════════════════════════════════════════════════════════════

            // EXOTIC MODES (7-note per family)

            // ═══════════════════════════════════════════════════════════════════════



            LoadModes(DoubleHarmonicScaleMode.Items, baseIndex: 137);

            LoadModes(NeapolitanMajorScaleMode.Items, baseIndex: 144);

            LoadModes(NeapolitanMinorScaleMode.Items, baseIndex: 151);

            LoadModes(EnigmaticScaleMode.Items, baseIndex: 158);

            LoadModes(BebopScaleMode.Items, baseIndex: 165);

            LoadModes(BluesScaleMode.Items, baseIndex: 173);

            LoadModes(PrometheusScaleMode.Items, baseIndex: 179);

            LoadModes(TritoneScaleMode.Items, baseIndex: 185);



            // ═══════════════════════════════════════════════════════════════════════

            // PENTATONIC MODES (5-note scales)

            // ═══════════════════════════════════════════════════════════════════════



            LoadModes(MajorPentatonicMode.Items, baseIndex: 191);

            LoadModes(HirajoshiScaleMode.Items, baseIndex: 196);

            LoadModes(InSenScaleMode.Items, baseIndex: 201);



            // ═══════════════════════════════════════════════════════════════════════

            // SYMMETRIC MODES (variable modes)

            // ═══════════════════════════════════════════════════════════════════════



            LoadModes(WholeToneScaleMode.Items, baseIndex: 206);

            LoadModes(DiminishedScaleMode.Items, baseIndex: 208);

            LoadModes(AugmentedScaleMode.Items, baseIndex: 212);



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

            if (prop == null) return 1;



            var degree = prop.GetValue(mode);

            var valueProp = degree?.GetType().GetProperty("Value");

            return (int?)valueProp?.GetValue(degree) ?? 1;

        }



        private static HashSet<int> GetSemitonesFromFormula(ModeFormula formula)

        {

            return formula.CharacteristicIntervals

                .Select(interval => (int)interval.ToSemitones().Value)

                .ToHashSet();

        }



        private static HashSet<int> GetFullSemitonesFromFormula(ModeFormula formula)

        {

            return formula.Intervals

                .Select(interval => (int)interval.ToSemitones().Value % 12)

                .ToHashSet();

        }



        /// <summary>

        /// Gets characteristic interval semitones by embedding index (e.g., 109 = Ionian).

        /// </summary>

        public HashSet<int>? GetCharacteristicSemitones(int embeddingIndex)

        {

            return _intervalsByIndex.TryGetValue(embeddingIndex, out var intervals) ? intervals : null;

        }



        /// <summary>

        /// Gets characteristic interval semitones by mode name (backward compatibility).

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

        /// Gets ALL intervals in the mode (not just characteristic).

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

        /// Gets all embedding indices with registered modes.

        /// </summary>

        public IEnumerable<int> GetAllModeIndices() => _intervalsByIndex.Keys;



        /// <summary>

        /// Gets all mode names (backward compatibility).

        /// </summary>

        public IEnumerable<string> GetAllModeNames() => _indexByName.Keys;

    }
