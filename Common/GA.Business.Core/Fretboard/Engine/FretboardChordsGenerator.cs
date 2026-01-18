namespace GA.Business.Core.Fretboard.Engine;

using System.Collections.Immutable;
using Primitives;
using Positions; // Added for PositionLocation
using GA.Business.Core.Notes.Primitives;
using Atonal;

using FretboardClass = GA.Business.Core.Fretboard.Primitives.Fretboard;

/// <summary>
/// Generates chord voicings for specific pitch class sets.
/// </summary>
public class FretboardChordsGenerator
{
    private readonly FretboardClass _fretboard;

    public FretboardChordsGenerator(FretboardClass fretboard)
    {
        _fretboard = fretboard;
    }

    /// <summary>
    /// Gets all valid chord positions (voicings) for the given set of pitch classes.
    /// </summary>
    /// <param name="notes">The set of pitch classes to form the chord.</param>
    /// <param name="minNotes">Minimum number of notes to play.</param>
    /// <param name="span">Maximum fret span (default 4).</param>
    /// <returns>A collection of voicings, each represented as a list of positions.</returns>
    public IEnumerable<ImmutableList<Position>> GetChordPositions(
        PitchClassSet notes, 
        int minNotes = 3, 
        int span = 4)
    {
        // Naive implementation: 
        // 1. Find all locations for each pitch class on the fretboard.
        // 2. Generate combinations.
        // 3. Filter by span and strict match (subset/superset).

        // Get all locations for relevant PCs
        var locationsPerString = new List<Position>[_fretboard.StringCount];
        for (int s = 0; s < _fretboard.StringCount; s++)
        {
            var str = _fretboard.Tuning[new Str(s + 1)]; // Tuning uses 1-based indexing for strings? 
            // Correct: Tuning indexer takes Str. Str is value object.
            // Tuning.cs: public Pitch this[Str str] ... var index = str.Value - 1;
            // So new Str(s+1) is correct.
            
            var positions = new List<Position>();
            
            // Allow muted string
            positions.Add(new Position.Muted(new Str(s + 1)));

            // Check each fret
            for (int f = 0; f <= _fretboard.FretCount; f++)
            {
                // Calculate MidiNote
                MidiNote midiNote = (MidiNote)str + f; // Assuming MidiNote supports + int
                
                if (notes.Contains(midiNote.PitchClass))
                {
                    positions.Add(new Position.Played(new PositionLocation(new Str(s + 1), Fret.FromValue(f)), midiNote));
                }
            }
            locationsPerString[s] = positions;
        }

        return GenerateCombinations(locationsPerString, minNotes, span);
    }
    
    /// <summary>
    /// Generates all chord positions (no arguments version for API compatibility if needed).
    /// But typically it needs PCSet. The call site calls it with PCSet.
    /// Explicitly adding parameterless one if semantic service used it? 
    /// Semantic service used: generator.GetChordPositions().ToList(); 
    /// </summary>
    public IEnumerable<ImmutableList<Position>> GetChordPositions()
    {
         // If called without arguments, it might imply generating "all chords"? 
         // Or maybe it was a mistake in my reading of SemanticService?
         // SemanticService: generator.GetChordPositions().ToList();
         // It instantiated: var generator = new FretboardChordsGenerator(fretboard);
         // If it generates ALL voicings, that's what VoicingGenerator does.
         // Maybe FretboardChordsGenerator was just a wrapper around VoicingGenerator?
         // I'll implement this to delegate to VoicingGenerator if no args.
         
         return Voicings.Generation.VoicingGenerator
             .GenerateAllVoicings(_fretboard)
             .Select(v => v.Positions.ToImmutableList());
    }

    private IEnumerable<ImmutableList<Position>> GenerateCombinations(
        List<Position>[] locationsPerString, 
        int minNotes, 
        int span)
    {
        // Stack-based or recursive combination
        // Since string count is low (6-8), recursion is fine.
        
        foreach (var combination in Recurse(0, locationsPerString))
        {
            // Validate Span & Note Count
            int playedCount = 0;
            int minFret = int.MaxValue;
            int maxFret = int.MinValue;
            
            foreach (var pos in combination)
            {
                if (pos is Position.Played p)
                {
                    playedCount++;
                    int f = p.Location.Fret.Value;
                    if (f > 0) // Ignore open strings for span? Usually yes.
                    {
                        if (f < minFret) minFret = f;
                        if (f > maxFret) maxFret = f;
                    }
                }
            }

            if (playedCount < minNotes) continue;
            
            if (minFret != int.MaxValue && (maxFret - minFret) > span) continue;

            yield return combination.ToImmutableList();
        }
    }

    private IEnumerable<List<Position>> Recurse(int stringIndex, List<Position>[] locations)
    {
        if (stringIndex >= locations.Length)
        {
            yield return new List<Position>();
            yield break;
        }

        foreach (var pos in locations[stringIndex])
        {
            foreach (var rest in Recurse(stringIndex + 1, locations))
            {
                var list = new List<Position> { pos };
                list.AddRange(rest);
                yield return list;
            }
        }
    }
}
