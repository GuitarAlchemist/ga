namespace GA.Business.Core.Fretboard.Analysis;

using System;
using System.Collections.Generic;
using GA.Business.Core.Notes;
using GA.Business.Core.Fretboard.Primitives;

/// <summary>
/// Service for mapping abstract pitches (MIDI) to physical fretboard locations.
/// Core engine for generative realization (MIDI to Tab).
/// </summary>
public class FretboardPositionMapper
{
    private readonly Tuning _tuning;
    private readonly int _maxFret;

    public FretboardPositionMapper(Tuning tuning, int maxFret = 24)
    {
        _tuning = tuning ?? throw new ArgumentNullException(nameof(tuning));
        _maxFret = maxFret;
    }

    /// <summary>
    /// Finds all possible (String, Fret) positions for a given pitch.
    /// </summary>
    public IEnumerable<FretboardPosition> MapPitch(Pitch targetPitch)
    {
        var positions = new List<FretboardPosition>();

        for (int s = 1; s <= _tuning.StringCount; s++)
        {
            var stringIndex = Str.FromValue(s);
            var openPitch = _tuning[stringIndex];

            // Calculate fret required to reach targetPitch from openPitch
            // Formula: Fret = targetPitch.MidiNote.Value - openPitch.MidiNote.Value
            int fret = targetPitch.MidiNote.Value - openPitch.MidiNote.Value;

            if (fret >= 0 && fret <= _maxFret)
            {
                positions.Add(new FretboardPosition(stringIndex, fret, targetPitch));
            }
        }

        return positions;
    }

    /// <summary>
    /// Maps a set of pitches to all valid chord shapes (combinations of positions).
    /// WARNING: Combinatorial complexity can be high.
    /// </summary>
    public IEnumerable<List<FretboardPosition>> MapChord(IEnumerable<Pitch> chordPitches)
    {
        var pitches = chordPitches.ToList();
        var optionsPerNote = pitches.Select(p => MapPitch(p).ToList()).ToList();

        return GenerateCombinations(optionsPerNote);
    }

    private IEnumerable<List<FretboardPosition>> GenerateCombinations(List<List<FretboardPosition>> options)
    {
        // Recursively generate all possible ways to play the chord
        // Filter out shapes that use the same string for multiple notes
        return CartesianProduct(options)
            .Where(combination => IsPhysicallyPossible(combination));
    }

    private static bool IsPhysicallyPossible(List<FretboardPosition> combination)
    {
        // 1. Unique Strings constraint
        var stringIndices = combination.Select(p => p.StringIndex.Value).ToList();
        if (stringIndices.Distinct().Count() != combination.Count) return false;

        // 2. Future: Add stretch constraints? (Handled by PhysicalCostService later)
        return true;
    }

    private static IEnumerable<List<T>> CartesianProduct<T>(List<List<T>> sequences)
    {
        IEnumerable<List<T>> result = new[] { new List<T>() };
        foreach (var sequence in sequences)
        {
            var seq = sequence; // closure
            result = result.SelectMany(_ => seq, (list, item) => 
            {
                var newList = new List<T>(list) { item };
                return newList;
            });
        }
        return result;
    }
}

public record FretboardPosition(Str StringIndex, int Fret, Pitch Pitch);
