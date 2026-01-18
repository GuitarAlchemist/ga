namespace GA.Business.ML.Musical.Enrichment;

using System;
using System.Collections.Generic;
using System.Linq;
using Core.Fretboard.Voicings.Search;

/// <summary>
/// Service to identify and tag "Modal Flavors" in voicings based on characteristic intervals.
/// Uses ModalCharacteristicIntervalService (Domain Model) as the source of truth.
/// </summary>
public class ModalFlavorService
{
    private List<ModeFlavorDefinition> _flavors = new();
    private readonly ModalCharacteristicIntervalService _intervalService;

    public ModalFlavorService()
    {
        _intervalService = ModalCharacteristicIntervalService.Instance;
        LoadConfiguration();
    }

    private void LoadConfiguration()
    {
        // Load flavors from the programmatic domain model
        var modeNames = _intervalService.GetAllModeNames();
        
        foreach (var name in modeNames)
        {
            var intervals = _intervalService.GetCharacteristicSemitones(name);
            var fullIntervals = _intervalService.GetModeIntervals(name);
            
            if (name == "Ionian")
            {
                // Debug output removed for cleaner console
            }

            if (fullIntervals != null && fullIntervals.Count > 0)
            {
                // Fallback: If no characteristic intervals are defined (e.g. Ionian), 
                // assume the entire set is characteristic.
                var effectiveCharacteristics = (intervals != null && intervals.Count > 0) 
                    ? intervals 
                    : fullIntervals;

                var priority = GetModePriority(name);
                _flavors.Add(new ModeFlavorDefinition(
                    name, 
                    effectiveCharacteristics, 
                    fullIntervals,
                    $"Auto-generated flavor for {name}",
                    priority
                ));
            }
        }
        
        // Debug: Console.WriteLine($"[ModalFlavorService] Loaded {_flavors.Count} modal flavors from domain model.");
    }

    private int GetModePriority(string name)
    {
        // High Priority: Church Modes + Common Variations
        var standard = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "Ionian", "Dorian", "Phrygian", "Lydian", "Mixolydian", "Aeolian", "Locrian",
            "Harmonic minor", "Melodic minor", "Phrygian dominant", "Lydian dominant", "Altered", "Diminished", "Whole-tone", "Blues"
        };
        
        if (standard.Contains(name)) return 10;
        
        // Medium Priority: Common Jazz/Fusion
        if (name.Contains("Pentatonic", StringComparison.OrdinalIgnoreCase) || 
            name.Contains("Bebop", StringComparison.OrdinalIgnoreCase)) return 5;

        // Low Priority: Exotic/Generated
        return 1;
    }

    public void Enrich(VoicingDocument doc, HashSet<string> tags)
    {
        if (doc.RootPitchClass == null || doc.PitchClasses == null || doc.PitchClasses.Length == 0) return;

        var root = doc.RootPitchClass.Value;
        var pcs = doc.PitchClasses;
        
        // Convert voicing to Interval Set relative to Root (semitones, normalized to octave)
        var voicingIntervals = new HashSet<int>();
        foreach (var pc in pcs)
        {
            int interval = (pc - root + 12) % 12;
            voicingIntervals.Add(interval);
        }

        var matches = new List<(string Name, int Score, int Priority, int MatchCount)>();

        foreach (var flavor in _flavors)
        {
            // 1. Check Characteristic Matches (Hits)
            int matchCount = flavor.CharacteristicSemitones.Count(s => voicingIntervals.Contains(s));
            
            if (matchCount > 0)
            {
                // 2. Check Conflicts (Voicing notes that are NOT in the mode)
                int conflictCount = 0;
                foreach (var interval in voicingIntervals)
                {
                    if (!flavor.FullIntervals.Contains(interval))
                    {
                        conflictCount++;
                    }
                }

                // Score: Matches - (Conflicts * 2) + Saturation
                // 1. Base Score: Matches
                int score = matchCount;
                
                // 2. Conflict Penalty (Heavy: 2x)
                score -= (conflictCount * 2);

                // 3. Saturation Bonus (If we cover the ENTIRE mode definition)
                int fullMatchCount = flavor.FullIntervals.Count(s => voicingIntervals.Contains(s));
                if (fullMatchCount == flavor.FullIntervals.Count)
                {
                    score += 15;
                }

                if (score > 0) // Only consider positive scores
                {
                    matches.Add((flavor.Name, score, flavor.Priority, matchCount));
                }
            }
        }

        if (matches.Count > 0)
        {
            // Tiered Selection Logic:
            
            // 1. Identify "Standard" matches that are conflict-free.
            // These are the "Gold Standard" (e.g. it's exactly Mixolydian, no weird notes).
            var perfectStandard = matches
                .Where(x => x.Priority >= 10 && x.Score == x.MatchCount) // Score == MatchCount implies Conflict == 0
                .ToList();

            if (perfectStandard.Any())
            {
                // If we have perfect standard matches, we ONLY report these.
                // We prefer the one with the highest MatchCount (most characteristic intervals matched).
                var maxStandardScore = perfectStandard.Max(x => x.Score);
                var winners = perfectStandard.Where(x => x.Score == maxStandardScore);
                foreach (var match in winners) tags.Add($"Flavor:{match.Name}");
                return;
            }

            // 2. If no perfect standard matches, we look at everything.
            // But we still give a massive boost to Priority in the final sorting.
            // Let's effectively add Priority to Score for sorting purposes.
            // Score ranges ~1-6. Priority is 1, 5, 10.
            // AdjustedScore = Score + Priority.
            
            var maxAdjustedScore = matches.Max(x => x.Score + x.Priority);
            
            // We might have a tie.
            var bestCandidates = matches
                .Where(x => (x.Score + x.Priority) == maxAdjustedScore)
                .ToList();

            foreach (var match in bestCandidates)
            {
                tags.Add($"Flavor:{match.Name}");
            }
        }
    }

    private record ModeFlavorDefinition(
        string Name,
        HashSet<int> CharacteristicSemitones,
        HashSet<int> FullIntervals,
        string? Description,
        int Priority)
    {
        // Deconstruct for easier usage in loop if needed
        public int MatchCount(HashSet<int> voicingIntervals) => CharacteristicSemitones.Count(s => voicingIntervals.Contains(s));
    };
}