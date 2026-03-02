namespace GA.Business.ML.Retrieval;

using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Domain.Core.Primitives.Notes;
using Domain.Services.Fretboard.Analysis;
using Embeddings;
using GA.Domain.Core.Instruments;

/// <summary>
/// Suggests "where to go next" based on harmonic smoothness and physical playability.
/// Implements Phase Sphere Neighbor navigation.
/// </summary>
public class NextChordSuggestionService(
    SpectralRetrievalService retrieval,
    PhysicalCostService costService)
{
    private readonly FretboardPositionMapper _mapper = new(Tuning.Default);

    public record SuggestionResult(ChordVoicingRagDocument Doc, double HarmonicScore, double PhysicalCost, double TotalScore);

    /// <summary>
    /// Suggests next voicings based on a current voicing.
    /// </summary>
    public Task<List<SuggestionResult>> SuggestNextAsync(
        ChordVoicingRagDocument currentDoc,
        int topK = 5)
    {
        // 1. Find Harmonic Neighbors (Phase Sphere)
        var neighbors = retrieval.FindSpectralNeighbors(currentDoc.Embedding, topK: 50).ToList();

        // 2. Rank by Physical Smoothness
        var currentShape = MapToPositions(currentDoc);
        var ranked = new List<SuggestionResult>();

        foreach (var (neighbor, harmScore) in neighbors)
        {
            if (neighbor.Id == currentDoc.Id) continue;

            var neighborShape = MapToPositions(neighbor);
            var physicalCost = costService.CalculateTransitionCost(currentShape, neighborShape);
            var totalScore = (harmScore * 10.0) - physicalCost;

            ranked.Add(new SuggestionResult(neighbor, harmScore, physicalCost, totalScore));
        }

        return Task.FromResult<List<SuggestionResult>>([.. ranked
            .OrderByDescending(x => x.TotalScore)
            .Take(topK)]);
    }

    private List<FretboardPosition> MapToPositions(ChordVoicingRagDocument doc)
    {
        var pitches = doc.MidiNotes.Select(m => MidiNote.FromValue(m).ToSharpPitch()).ToList();
        var realizations = _mapper.MapChord(pitches).ToList();
        return realizations.Count > 0 ? realizations.First() : [];
    }
}
