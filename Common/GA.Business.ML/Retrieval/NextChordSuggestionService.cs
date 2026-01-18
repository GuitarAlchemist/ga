namespace GA.Business.ML.Retrieval;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Core.Fretboard.Voicings.Search;
using Embeddings;
using Core.Fretboard.Analysis;
using Core.Notes;
using Core.Fretboard;
using Core.Notes.Primitives;

/// <summary>
/// Suggests "where to go next" based on harmonic smoothness and physical playability.
/// Implements Phase Sphere Neighbor navigation.
/// </summary>
public class NextChordSuggestionService
{
    private readonly SpectralRetrievalService _retrieval;
    private readonly PhysicalCostService _costService;
    private readonly FretboardPositionMapper _mapper = new(Tuning.Default);

    public NextChordSuggestionService(
        SpectralRetrievalService retrieval,
        PhysicalCostService costService)
    {
        _retrieval = retrieval;
        _costService = costService;
    }

    public record SuggestionResult(VoicingDocument Doc, double HarmonicScore, double PhysicalCost, double TotalScore);

    /// <summary>
    /// Suggests next voicings based on a current voicing.
    /// </summary>
    public async Task<List<SuggestionResult>> SuggestNextAsync(
        VoicingDocument currentDoc,
        int topK = 5)
    {
        if (currentDoc.Embedding == null) return new();

        // 1. Find Harmonic Neighbors (Phase Sphere)
        var neighbors = _retrieval.FindSpectralNeighbors(currentDoc.Embedding, topK: 50).ToList();

        // 2. Rank by Physical Smoothness
        var currentShape = MapToPositions(currentDoc);
        var ranked = new List<SuggestionResult>();

        foreach (var (neighbor, harmScore) in neighbors)
        {
            if (neighbor.Id == currentDoc.Id) continue;

            var neighborShape = MapToPositions(neighbor);
            double physicalCost = _costService.CalculateTransitionCost(currentShape, neighborShape);
            double totalScore = (harmScore * 10.0) - physicalCost;

            ranked.Add(new SuggestionResult(neighbor, harmScore, physicalCost, totalScore));
        }

        return ranked
            .OrderByDescending(x => x.TotalScore)
            .Take(topK)
            .ToList();
    }

    private List<FretboardPosition> MapToPositions(VoicingDocument doc)
    {
        var pitches = doc.MidiNotes.Select(m => MidiNote.FromValue(m).ToSharpPitch()).ToList();
        var realizations = _mapper.MapChord(pitches).ToList();
        return realizations.Count > 0 ? realizations.First() : new List<FretboardPosition>();
    }
}