namespace GA.Business.ML.Tabs;

using Domain.Core.Primitives.Notes;
using Domain.Services.Fretboard.Analysis;

/// <summary>
///     Service to generate accessible alternative fingerings for a given chord progression.
///     Categorizes options into styles like "Campfire", "Jazz", "Shred", etc.
/// </summary>
public class AlternativeFingeringService(AdvancedTabSolver solver)
{
    private readonly TabToPitchConverter _converter = new();

    public async Task<List<FingeringOption>> GetAlternativesAsync(IEnumerable<ChordVoicingRagDocument> progression)
    {
        // 1. Convert to Pitch Matrix
        var score = progression.Select(d =>
            d.MidiNotes.Select(m => Pitch.FromMidiNote(m)).ToList()
        ).ToList();

        // 2. Get Top-K raw paths from Solver
        // We request a larger K (e.g. 20) to ensure we find enough diverse clusters
        var rawPaths = await solver.SolveAsync(score, "Jazz", 20);

        // 3. Cluster/Filter based on heuristics
        var options = new List<FingeringOption>();

        // A. "Campfire" / Open Position
        // Heuristic: Low avg fret, high open string count.
        var openPath = rawPaths
            .OrderByDescending(p => CalculateOpenStringScore(p))
            .ThenBy(p => CalculateAverageFret(p))
            .FirstOrDefault();

        if (openPath != null)
        {
            options.Add(new(
                "Open / Campfire",
                "Uses open strings and lower frets where possible.",
                [.. openPath.Select(x => x.ToArray())],
                CalculateDifficulty(openPath)
            ));
        }

        // B. "Jazz" / Shell Voicings
        // Heuristic: No open strings, frets 3-10 usually (center), consistent string sets.
        // We already biased the solver for "Jazz" so the #1 result is likely this, but let's be specific.
        var jazzPath = rawPaths
            .Where(p => p != openPath) // Don't duplicate
            .OrderBy(p => CalculateStandardDeviationFret(p)) // Compact hand shapes
            .FirstOrDefault();

        if (jazzPath != null)
        {
            options.Add(new(
                "Jazz / Compact",
                "Tight voice leading, minimizes hand movement. Good for electric.",
                [.. jazzPath.Select(x => x.ToArray())],
                CalculateDifficulty(jazzPath)
            ));
        }

        // C. "High / Inversions"
        // Heuristic: High average fret (> 7).
        var highPath = rawPaths
            .Where(p => p != openPath && p != jazzPath)
            .Where(p => CalculateAverageFret(p) > 7)
            .OrderBy(p => CalculateAverageFret(p)) // Lowest of the high ones? Or highest?
            .FirstOrDefault();

        if (highPath != null)
        {
            options.Add(new(
                "Higher Inversions",
                "Played up the neck for a brighter timbre.",
                [.. highPath.Select(x => x.ToArray())],
                CalculateDifficulty(highPath)
            ));
        }

        return options;
    }

    private double CalculateOpenStringScore(List<List<FretboardPosition>> path)
    {
        var openCount = path.Sum(chord => chord.Count(n => n.Fret == 0));
        return openCount;
    }

    private double CalculateAverageFret(List<List<FretboardPosition>> path)
    {
        var frets = path.SelectMany(c => c.Select(n => n.Fret)).Where(f => f > 0).ToList();
        return frets.Count > 0 ? frets.Average() : 0;
    }

    private double CalculateStandardDeviationFret(List<List<FretboardPosition>> path)
    {
        var frets = path.SelectMany(c => c.Select(n => n.Fret)).Where(f => f > 0).ToList();
        if (frets.Count < 2)
        {
            return 0;
        }

        var avg = frets.Average();
        var sum = frets.Sum(d => Math.Pow(d - avg, 2));
        return Math.Sqrt(sum / (frets.Count - 1));
    }

    private double CalculateDifficulty(List<List<FretboardPosition>> path) =>
        // Placeholder for correct PhysicalCost usage
        0.5;

    public record FingeringOption(
        string Label,
        string Description,
        List<FretboardPosition[]> Tab, // Array for easier JSON serialization usually
        double DifficultyScore
    );
}
