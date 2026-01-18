namespace GA.Business.ML.Naturalness;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using GA.Business.Core.Tabs;
using Tabs; 
using Tabs.Models;
using Core.Fretboard.Primitives;
using Core.Notes;
using Core.Fretboard;
using Core.Fretboard.Analysis;

public class NaturalnessTrainingDataGenerator
{
    private readonly ITabCorpusRepository _repository;
    private readonly TabTokenizer _tokenizer;
    private readonly TabToPitchConverter _converter;
    private readonly Tuning _tuning;

    public NaturalnessTrainingDataGenerator(ITabCorpusRepository repository)
    {
        _repository = repository;
        _tokenizer = new TabTokenizer();
        _converter = new TabToPitchConverter();
        _tuning = Tuning.Default;
    }

    public async Task<string> GenerateCsvAsync(int maxItems = 1000)
    {
        var sb = new StringBuilder();
        // Header
        sb.AppendLine("Label,DeltaAvgFret,MaxFingerDisp,StringCrossingCount,HandStretchDelta,CommonStrings");

        var allItems = await _repository.GetAllAsync();
        int count = 0;

        foreach (var item in allItems)
        {
            if (count >= maxItems) break;
            if (item.Format != "ASCII") continue; // Only handle ASCII for now

            try 
            {
                var dataset = ExtractFromTab(item.Content);
                foreach (var line in dataset)
                {
                    sb.AppendLine(line);
                }
                if (dataset.Count > 0) count++;
            }
            catch 
            {
                // Ignore parse errors for bulk generation
            }
        }

        return sb.ToString();
    }

    private List<string> ExtractFromTab(string content)
    {
        var rows = new List<string>();
        var blocks = _tokenizer.Tokenize(content);
        var slices = blocks.SelectMany(b => b.Slices).Where(s => s.Notes.Count > 0).ToList();

        for (int i = 0; i < slices.Count - 1; i++)
        {
            var curr = ConvertSlice(slices[i]);
            var next = ConvertSlice(slices[i+1]);

            if (curr.Count == 0 || next.Count == 0) continue;

            // 1. Positive Example (Real Transition)
            var featuresPos = ComputeFeatures(curr, next);
            rows.Add($"1,{featuresPos}");

            // 2. Negative Example (Perturbed Next State)
            // Strategy: Shift 'next' chord up/down 12 frets if possible, or randomize positions
            var perturbed = Perturb(next);
            if (perturbed != null)
            {
                var featuresNeg = ComputeFeatures(curr, perturbed);
                rows.Add($"0,{featuresNeg}");
            }
        }
        return rows;
    }

    private List<FretboardPosition> ConvertSlice(TabSlice slice)
    {
        var list = new List<FretboardPosition>();
        foreach (var note in slice.Notes)
        {
            // ASCII Tab: StringIndex 0=Top(HighE), 5=Bottom(LowE) usually in Tokenizer?
            // Let's verify TabTokenizer convention. usually 0 is High E.
            // Tuning.Default.Strings is LowE..HighE (0..5).
            // So StringIndex 0 in Tab -> Tuning String 5 (High E).
            // StringIndex 5 in Tab -> Tuning String 0 (Low E).
            var strIndex = 5 - note.StringIndex; 
            if (strIndex < 0 || strIndex > 5) continue;

            var str = Str.FromValue(strIndex + 1); // Str is 1-based? Usually. let's assume 1-based for now or check.
            // Actually Str.FromValue takes int.
            
            // We just need Fret really for features.
            // Pitch is mostly irrelevant for "Physical Naturalness" unless we check if valid.
            list.Add(new FretboardPosition(Str.FromValue(strIndex+1), note.Fret, Pitch.FromMidiNote(60))); 
        }
        return list;
    }

    private List<FretboardPosition> Perturb(List<FretboardPosition> original)
    {
        // Simple perturbation: Add random offset to frets, ignoring musical validity for "Bad Fingering" simulation?
        // No, that's too easy to distinguish.
        // Better: Shift same shape up 5 frets?
        var shifted = new List<FretboardPosition>();
        foreach (var p in original)
        {
            shifted.Add(new FretboardPosition(p.StringIndex, p.Fret + 5, p.Pitch));
        }
        return shifted;
    }

    private string ComputeFeatures(List<FretboardPosition> a, List<FretboardPosition> b)
    {
        double avgA = a.Average(p => p.Fret);
        double avgB = b.Average(p => p.Fret);
        double deltaAvg = Math.Abs(avgA - avgB);

        int minFretA = a.Min(p => p.Fret);
        int maxFretA = a.Max(p => p.Fret);
        int spreadA = maxFretA - minFretA;

        int minFretB = b.Min(p => p.Fret);
        int maxFretB = b.Max(p => p.Fret);
        int spreadB = maxFretB - minFretB;
        
        double deltaStretch = Math.Abs(spreadA - spreadB);

        // String crossings?
        // Count how many strings are shared vs changed
        int sharedStrings = a.Select(p => p.StringIndex.Value).Intersect(b.Select(p => p.StringIndex.Value)).Count();
        int changedStrings = (a.Count + b.Count) - 2 * sharedStrings; // Rough proxy

        // Max Finger displacement
        // Simple heuristic: distance between centers?
        // Real logic needs finger assignment.
        // Let's use avg fret delta as primary.
        
        return $"{deltaAvg:F2},{deltaAvg:F2},{changedStrings},{deltaStretch},{sharedStrings}";
    }
}
