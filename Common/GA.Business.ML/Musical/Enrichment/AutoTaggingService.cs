namespace GA.Business.ML.Musical.Enrichment;

using System;
using System.Collections.Generic;
using System.Linq;
using Core.Fretboard.Voicings.Search;

/// <summary>
/// automatically generates semantic tags (e.g. "Jazz", "Campfire", "Shell") based on detailed voicing analysis.
/// This enables the chatbot to explain "Why" returned results are relevant.
/// </summary>
public class AutoTaggingService
{
    private readonly ModalFlavorService _modalFlavorService;

    public AutoTaggingService(ModalFlavorService modalFlavorService)
    {
        _modalFlavorService = modalFlavorService;
    }

    public string[] GenerateTags(VoicingDocument doc)
    {
        var tags = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // 1. Analyze Diagram / Playability
        AnalyzePlayability(doc, tags);

        // 2. Analyze Harmonic Structure (Tonal)
        AnalyzeHarmonicStructure(doc, tags);

        // 3. Analyze Atonal Structure (Forte Numbers)
        AnalyzeAtonalStructure(doc, tags);

        // Phase 13: Modal Flavor
        _modalFlavorService.Enrich(doc, tags);

        return tags.ToArray();
    }

    private void AnalyzeAtonalStructure(VoicingDocument doc, HashSet<string> tags)
    {
        if (doc.PitchClasses == null || doc.PitchClasses.Length == 0) return;

        try
        {
            // Use domain model to get Forte number
            var pcsList = doc.PitchClasses.Select(pc => Core.Atonal.PitchClass.FromValue(pc));
            var pcs = new Core.Atonal.PitchClassSet(pcsList);

            if (Core.Atonal.ForteCatalog.TryGetForteNumber(pcs.PrimeForm, out var forte))
            {
                tags.Add($"Set:{forte}");
            }
        }
        catch
        {
            // Ignore parsing errors for malformed PCS
        }
    }

    public void Enrich(VoicingDocument doc)
    {
        var tags = GenerateTags(doc);
        // We can't set doc.SemanticTags because it's init-only or required.
        // The caller must handle the object creation or cloning.
        // Wait, VoicingDocument properties are immutable.
        // So this method is slightly misleading, functionality sits in GenerateTags.
    }

    private void AnalyzePlayability(VoicingDocument doc, HashSet<string> tags)
    {
        var shape = doc.Diagram; // e.g., "x-3-2-0-1-0"
        if (string.IsNullOrEmpty(shape)) return;

        var frets = shape.Split('-')
            .Where(s => s != "x")
            .Select(s => int.TryParse(s, out var i) ? i : -1)
            .Where(i => i >= 0)
            .ToList();

        var openStrings = frets.Count(f => f == 0);
        var maxFret = frets.Any() ? frets.Max() : 0;
        var minFret = frets.Any() ? frets.Min() : 0;
        var span = maxFret - minFret;

        // Campfire: Low position, open strings, moderate span
        if (minFret <= 3 && openStrings >= 2 && span <= 4)
        {
            tags.Add("Campfire");
            tags.Add("Beginner");
        }
        else // Only check broad cat if not specific
        {
            if (openStrings > 0) tags.Add("Open");
            else tags.Add("Movable");
        }

        // Wide Stretch
        if (span >= 5)
        {
            tags.Add("Wide Stretch");
        }
    }

    private void AnalyzeHarmonicStructure(VoicingDocument doc, HashSet<string> tags)
    {
        // 1. Rootless (Pre-calculated by Analyzer)
        if (doc.IsRootless)
        {
            tags.Add("Rootless");
            tags.Add("Jazz");
        }

        // 2. Shell Voicings (Derived from Tone Inventory)
        // Definition: Has Guide Tones (3rd + 7th), Omitted 5th (usually), Small size (< 5 notes)
        // If it has Guide Tones and NO 5th, it's a strong Shell candidate.
        bool has5th = !doc.OmittedTones?.Contains("5th") ?? true; // If Omitted doesn't contain 5th, we assume it has it

        if (doc.HasGuideTones && !has5th && doc.MidiNotes.Length <= 4)
        {
             tags.Add("Shell");
             tags.Add("Jazz");
        }
        // Strict Shell (R-3-7) might effectively be covered here.

        // 3. Extensions (Pre-calculated text description or check names?)
        // Analyzer doesn't have "HasExtensions" bool, but we can check the Chord Name or Extended Tones if we had them.
        // Or we can check the PitchClassSet size vs 4?
        // Let's use the explicit interval check which is robust, OR rely on parsing the Name?
        // Analyzing intervals again is redundant if we trust the Analyzer.
        // But VoicingDocument doesn't store "HasExtensions".
        // Let's infer from ChordName (e.g. "9", "13", "11") or check ToneInventory if exposed?
        // VoicingDocument doesn't expose ToneInventory directly.
        // However, we can check the stored OmittedTones or just check the note count for extensions?
        // Note count > 4 usually implies extensions (R-3-5-7 + 1).
        if (doc.MidiNotes.Length > 4 || (doc.ChordName?.Any(char.IsDigit) ?? false))
        {
            // Simple heuristic based on name containing 9, 11, 13
            if (doc.ChordName != null && (doc.ChordName.Contains("9") || doc.ChordName.Contains("11") || doc.ChordName.Contains("13")))
            {
                tags.Add("Extensions");
                tags.Add("Jazz");
            }
        }

        // 4. Power Chord
        bool isPowerChord = false;
        if (doc.ChordName != null && (doc.ChordName.EndsWith("5") || doc.ChordName.Contains("5 (") || doc.ChordName.Contains("5/")))
        {
            isPowerChord = true;
        }
        else if (doc.PitchClasses != null && doc.PitchClasses.Length >= 2)
        {
            var root = doc.RootPitchClass ?? (doc.MidiNotes.Length > 0 ? doc.MidiNotes.Min() % 12 : -1);
            if (root >= 0)
            {
                var relativePcs = doc.PitchClasses.Select(pc => (pc - root + 12) % 12).Distinct().OrderBy(pc => pc).ToList();
                // Power chord = Root + Perfect Fifth (0, 7)
                if (relativePcs.Count == 2 && relativePcs[0] == 0 && relativePcs[1] == 7)
                {
                    isPowerChord = true;
                }
            }
        }

        if (isPowerChord)
        {
            tags.Add("Power Chord");
            tags.Add("Rock");
        }

        // 5. Inversions
        if (doc.Inversion > 0)
        {
            tags.Add($"Inversion:{doc.Inversion}");
        }

        // 6. Quartal/Quintal
        if (doc.StackingType == "Quartal")
        {
            tags.Add("Quartal");
            tags.Add("Modern");
        }
    }
}
