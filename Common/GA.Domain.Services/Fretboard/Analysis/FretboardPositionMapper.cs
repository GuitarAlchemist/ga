namespace GA.Domain.Services.Fretboard.Analysis;

using Core.Instruments;
using Core.Instruments.Primitives;
using Core.Primitives.Notes;

/// <summary>
///     Service for mapping abstract pitches (MIDI) to physical fretboard locations.
///     Core engine for generative realization (MIDI to Tab).
/// </summary>
public class FretboardPositionMapper(Tuning tuning, int maxFret = 24)
{
    private readonly Tuning _tuning = tuning ?? throw new ArgumentNullException(nameof(tuning));

    /// <summary>
    ///     Finds all possible (String, Fret) positions for a given pitch.
    /// </summary>
    public IEnumerable<FretboardPosition> MapPitch(Pitch targetPitch)
    {
        var positions = new List<FretboardPosition>();

        for (var s = 1; s <= _tuning.StringCount; s++)
        {
            var stringIndex = Str.FromValue(s);
            var openPitch = _tuning[stringIndex];

            // Calculate fret required to reach targetPitch from openPitch
            // Formula: Fret = targetPitch.MidiNote.Value - openPitch.MidiNote.Value
            var fret = targetPitch.MidiNote.Value - openPitch.MidiNote.Value;

            if (fret >= 0 && fret <= maxFret)
            {
                positions.Add(new(stringIndex, fret, targetPitch));
            }
        }

        return positions;
    }

    /// <summary>
    ///     Maps a set of pitches to all valid chord shapes (combinations of positions).
    ///     WARNING: Combinatorial complexity can be high.
    /// </summary>
    public IEnumerable<List<FretboardPosition>> MapChord(IEnumerable<Pitch> chordPitches)
    {
        var pitches = chordPitches.ToList();
        var optionsPerNote = pitches.Select(p => MapPitch(p).ToList()).ToList();

        return GenerateCombinations(optionsPerNote);
    }

    private IEnumerable<List<FretboardPosition>> GenerateCombinations(List<List<FretboardPosition>> options) =>
        // Recursively generate all possible ways to play the chord
        // Filter out shapes that use the same string for multiple notes
        CartesianProduct(options)
            .Where(combination => IsPhysicallyPossible(combination));

    private static bool IsPhysicallyPossible(List<FretboardPosition> combination)
    {
        // 1. Unique Strings constraint
        var stringIndices = combination.Select(p => p.StringIndex.Value).ToList();
        if (stringIndices.Distinct().Count() != combination.Count)
        {
            return false;
        }

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
