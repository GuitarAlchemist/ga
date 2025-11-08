namespace GuitarChordProgressionMCTS;

public class Individual(List<int[]> voicings)
{
    public List<int[]> Voicings { get; set; } = voicings; // List of voicings for each chord
    public double Fitness { get; set; }

    // Constructor
}
