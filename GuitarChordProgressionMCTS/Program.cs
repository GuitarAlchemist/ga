namespace GuitarChordProgressionMCTS;

public class Program
{
    public static void Main(string[] args)
    {
        // Define a more complex melody as a list of MIDI note numbers
        var melody = new List<int>
        {
            60, // C4
            62, // D4
            64, // E4
            67, // G4
            65, // F4
            72, // C5 (octave up)
            71, // B4
            69, // A4
            67, // G4
            69, // A4
            76, // E5
            74, // D5
            72, // C5
            71, // B4
            69, // A4
            65, // F4
            64, // E4
            62, // D4
            60 // C4
        };

        var maxSequenceLength = melody.Count; // The chord progression should match the melody length
        const string key = "C Major"; // Specify the key

        // Start with an empty sequence
        var initialState = new State(new List<MusicalElement>(), maxSequenceLength, key, melody);

        var maxIterations = 1000; // Define how many iterations the MCTS should run
        var mcts = new Mcts(initialState, maxIterations);

        var bestState = mcts.Run();

        Console.WriteLine("Best Chord Progression Harmonizing the Melody:");
        for (var i = 0; i < bestState.Sequence.Count; i++)
        {
            var element = bestState.Sequence[i];
            Console.WriteLine($"Melody Note: {GetNoteName(bestState.MelodyNotes[i])} - Chord: {element.Name}");
            PrintVoicing(element.Voicings.First());
            Console.WriteLine();
        }
    }

    // Helper method to print the voicing
    private static void PrintVoicing(int[] voicing)
    {
        Console.WriteLine("Voicing (String 6 to 1): " +
                          string.Join("-", voicing.Select(fret => fret >= 0 ? fret.ToString() : "X")));
    }

    // Helper method to get note names from MIDI numbers
    private static string GetNoteName(int midiNumber)
    {
        string[] noteNames = ["C", "C#", "D", "D#", "E", "F", "F#", "G", "G#", "A", "A#", "B"];
        var octave = midiNumber / 12 - 1;
        var noteIndex = midiNumber % 12;
        return noteNames[noteIndex] + octave;
    }
}
