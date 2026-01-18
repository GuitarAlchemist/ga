namespace GA.Business.ML.Tabs;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Core.Fretboard.Voicings.Search;
using Core.Fretboard.Analysis;
using Core.Notes;
using Embeddings;

using Core.Notes.Primitives;

/// <summary>
/// Optimizes an existing progression by suggesting alternative voicings that minimize voice-leading cost.
/// </summary>
public class VoiceLeadingOptimizer
{
    private readonly AdvancedTabSolver _solver;
    private readonly IVectorIndex _index;

    public VoiceLeadingOptimizer(AdvancedTabSolver solver, IVectorIndex index)
    {
        _solver = solver;
        _index = index;
    }

    /// <summary>
    /// Given a progression of VoicingDocuments, returns an optimized sequence of voicings.
    /// </summary>
    public async Task<List<FretboardPosition[]>> OptimizeAsync(
        List<VoicingDocument> currentProgression, 
        string style = "Jazz")
    {
        if (currentProgression.Count == 0) return new();

        // 1. Extract the "Score" (Pitches per step)
        var score = currentProgression.Select(d => 
            d.MidiNotes.Select(m => MidiNote.FromValue(m).ToSharpPitch()).Cast<Pitch>().ToList()
        ).ToList();

        // 2. Run the Advanced Solver to find the optimal physical path
        var allPaths = await _solver.SolveAsync(score, style, k: 1);
        if (allPaths.Count == 0) return new();

        var optimized = allPaths[0];

        return optimized.Select(o => o.ToArray()).ToList();
    }
}
