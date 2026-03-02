namespace GA.Business.ML.Tabs;


using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Domain.Core.Primitives.Notes;
using Domain.Services.Fretboard.Analysis;
using Embeddings;

/// <summary>
/// Optimizes an existing progression by suggesting alternative voicings that minimize voice-leading cost.
/// </summary>
public class VoiceLeadingOptimizer(AdvancedTabSolver solver, IVectorIndex index)
{
    private readonly IVectorIndex _index = index;

    /// <summary>
    /// Given a progression of VoicingDocuments, returns an optimized sequence of voicings.
    /// </summary>
    public async Task<List<FretboardPosition[]>> OptimizeAsync(
        List<ChordVoicingRagDocument> currentProgression, 
        string style = "Jazz")
    {
        if (currentProgression.Count == 0) return [];

        // 1. Extract the "Score" (Pitches per step)
        var score = currentProgression.Select(d => 
            d.MidiNotes.Select(m => MidiNote.FromValue(m).ToSharpPitch()).Cast<Pitch>().ToList()
        ).ToList();

        // 2. Run the Advanced Solver to find the optimal physical path
        var allPaths = await solver.SolveAsync(score, style, k: 1);
        if (allPaths.Count == 0) return [];

        var optimized = allPaths[0];

        return [.. optimized.Select(o => o.ToArray())];
    }
}
