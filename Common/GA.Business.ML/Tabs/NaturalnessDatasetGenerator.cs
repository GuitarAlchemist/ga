namespace GA.Business.ML.Tabs;

using System;
using System.Collections.Generic;
using System.Linq;
using Core.Fretboard.Voicings.Search;
using Core.Fretboard.Analysis;
using Core.Fretboard;
using Core.Notes;
using Core.Notes.Primitives;

/// <summary>
/// Generates synthetic training data for the Naturalness Ranker.
/// Pairs human "good" voicings with procedurally generated "awkward" equivalents.
/// </summary>
public class NaturalnessDatasetGenerator
{
    private readonly FretboardPositionMapper _mapper = new(Tuning.Default);
    private readonly PhysicalCostService _costService = new();

    public record TrainingPair(VoicingDocument Positive, VoicingDocument Negative);

    /// <summary>
    /// Generates training pairs for a given set of human voicings.
    /// </summary>
    public IEnumerable<TrainingPair> GeneratePairs(IEnumerable<VoicingDocument> humanVoicings)
    {
        foreach (var human in humanVoicings)
        {
            var pitches = human.MidiNotes.Select(m => MidiNote.FromValue(m).ToSharpPitch()).ToList();
            
            // Find ALL possible ways to play these exact pitches
            var allRealizations = _mapper.MapChord(pitches).ToList();
            
            if (allRealizations.Count < 2) continue;

            // Find an "Awkward" realization: 
            // High physical cost but technically playable
            var candidates = allRealizations
                .Select(r => new { Shape = r, Cost = _costService.CalculateStaticCost(r) })
                .Where(x => x.Cost.TotalCost > 5.0 && x.Cost.TotalCost < 15.0) // Not impossible, just bad
                .OrderByDescending(x => x.Cost.TotalCost)
                .ToList();

            if (candidates.Count > 0)
            {
                var awkwardShape = candidates.First().Shape;
                var awkwardDoc = human with {
                    Id = Guid.NewGuid().ToString(),
                    MinFret = awkwardShape.Min(p => p.Fret),
                    MaxFret = awkwardShape.Max(p => p.Fret),
                    HandStretch = awkwardShape.Max(p => p.Fret) - awkwardShape.Min(p => p.Fret),
                    SemanticTags = human.SemanticTags.Append("synthetic-awkward").ToArray()
                };

                yield return new TrainingPair(human, awkwardDoc);
            }
        }
    }
}
