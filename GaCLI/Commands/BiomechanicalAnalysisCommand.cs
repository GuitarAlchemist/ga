namespace GaCLI.Commands;

using GA.Business.Core.Fretboard.Biomechanics;
using static Console;

/// <summary>
///     CLI command for biomechanical analysis of chord fingerings
/// </summary>
public class BiomechanicalAnalysisCommand
{
    private readonly BiomechanicalAnalyzer _analyzer = new();

    /// <summary>
    ///     Analyze a single chord fingering
    /// </summary>
    /// <param name="chordName">Chord name (e.g., "Cmaj7", "Dm", "G7")</param>
    /// <param name="handSize">Hand size (Small, Medium, Large, XL)</param>
    /// <param name="verbose">Show detailed analysis</param>
    public async Task<int> AnalyzeChordAsync(string chordName, string handSize = "Medium", bool verbose = false)
    {
        try
        {
            WriteLine($"\n?? Analyzing chord: {chordName}");
            WriteLine($"   Hand size: {handSize}");
            WriteLine();

            // Parse hand size
            if (!Enum.TryParse<HandSize>(handSize, true, out var parsedHandSize))
            {
                WriteLine($"? Invalid hand size: {handSize}. Valid options: Small, Medium, Large, XL");
                return 1;
            }

            // TODO: Generate chord positions when FretboardChordsGenerator is available
            // var fretboard = Fretboard.Default;
            // var generator = new FretboardChordsGenerator(fretboard);
            // var positions = generator.GetChordPositions(null).Take(1).ToList();

            WriteLine("? Generating chord positions...");
            var positions = new List<object>(); // Stub implementation

            if (!positions.Any())
            {
                WriteLine($"? No positions found for chord: {chordName}");
                return 1;
            }

            var position = positions.First();

            // Perform biomechanical analysis
            WriteLine("?? Performing biomechanical analysis...");
            // TODO: Fix when FretboardChordsGenerator is available
            // var analysis = _analyzer.AnalyzeChordPlayability(position.ToImmutableList());
            var analysis = new BiomechanicalPlayabilityAnalysis(
                OverallScore: 0.75,
                Difficulty: "Moderate",
                IsPlayable: true,
                Reachability: 0.8,
                Comfort: 0.7,
                Naturalness: 0.75,
                Efficiency: 0.7,
                Stability: 0.8,
                StretchAnalysis: null,
                FingeringEfficiencyAnalysis: null,
                WristPostureAnalysis: null,
                MutingAnalysis: null,
                SlideLegatoAnalysis: null,
                BestPose: null);

            // Display results
            DisplayAnalysisResults(analysis, verbose);

            return 0;
        }
        catch (Exception ex)
        {
            WriteLine($"? Error analyzing chord: {ex.Message}");
            if (verbose)
            {
                WriteLine($"   Stack trace: {ex.StackTrace}");
            }

            return 1;
        }
    }

    /// <summary>
    ///     Analyze a chord progression
    /// </summary>
    /// <param name="chords">Comma-separated chord names (e.g., "C,Am,F,G")</param>
    /// <param name="handSize">Hand size (Small, Medium, Large, XL)</param>
    /// <param name="verbose">Show detailed analysis</param>
    public async Task<int> AnalyzeProgressionAsync(string chords, string handSize = "Medium", bool verbose = false)
    {
        try
        {
            var chordNames = chords.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

            if (chordNames.Length == 0)
            {
                WriteLine("? No chords specified. Use comma-separated chord names (e.g., 'C,Am,F,G')");
                return 1;
            }

            WriteLine($"\n?? Analyzing progression: {string.Join(" ? ", chordNames)}");
            WriteLine($"   Hand size: {handSize}");
            WriteLine();

            // Parse hand size
            if (!Enum.TryParse<HandSize>(handSize, true, out var parsedHandSize))
            {
                WriteLine($"? Invalid hand size: {handSize}. Valid options: Small, Medium, Large, XL");
                return 1;
            }

            var analyses = new List<BiomechanicalPlayabilityAnalysis>();

            foreach (var chordName in chordNames)
            {
                WriteLine($"?? Analyzing: {chordName}");

                // TODO: Generate chord positions when FretboardChordsGenerator is available
                // var fretboard = Fretboard.Default;
                // var generator = new FretboardChordsGenerator(fretboard);
                var positions = new List<object>(); // Stub implementation

                if (!positions.Any())
                {
                    WriteLine($"   ??  No positions found for: {chordName}");
                    continue;
                }

                var position = positions.First();

                // TODO: Fix when FretboardChordsGenerator is available
                // var analysis = _analyzer.AnalyzeChordPlayability(position.ToImmutableList());
                var analysis = new BiomechanicalPlayabilityAnalysis(
                    OverallScore: 0.75,
                    Difficulty: "Moderate",
                    IsPlayable: true,
                    Reachability: 0.8,
                    Comfort: 0.7,
                    Naturalness: 0.75,
                    Efficiency: 0.7,
                    Stability: 0.8,
                    StretchAnalysis: null,
                    FingeringEfficiencyAnalysis: null,
                    WristPostureAnalysis: null,
                    MutingAnalysis: null,
                    SlideLegatoAnalysis: null,
                    BestPose: null);
                analyses.Add(analysis);

                WriteLine($"   ? Playability: {analysis.OverallScore:F2}");
                WriteLine();
            }

            // Display progression summary
            if (analyses.Any())
            {
                DisplayProgressionSummary(analyses, chordNames, verbose);
            }

            return 0;
        }
        catch (Exception ex)
        {
            WriteLine($"? Error analyzing progression: {ex.Message}");
            if (verbose)
            {
                WriteLine($"   Stack trace: {ex.StackTrace}");
            }

            return 1;
        }
    }

    private void DisplayAnalysisResults(BiomechanicalPlayabilityAnalysis analysis, bool verbose)
    {
        WriteLine("-----------------------------------------------------------");
        WriteLine("                  BIOMECHANICAL ANALYSIS                   ");
        WriteLine("-----------------------------------------------------------");
        WriteLine();

        WriteLine($"?? Overall Score: {analysis.OverallScore:F2}");
        WriteLine($"   {GetPlayabilityRating(analysis.OverallScore)}");
        WriteLine($"   Difficulty: {analysis.Difficulty}");
        WriteLine($"   Playable: {(analysis.IsPlayable ? "? Yes" : "? No")}");
        WriteLine();

        WriteLine("?? Component Scores:");
        WriteLine($"   Reachability: {analysis.Reachability:F2}");
        WriteLine($"   Comfort: {analysis.Comfort:F2}");
        WriteLine($"   Naturalness: {analysis.Naturalness:F2}");
        WriteLine($"   Efficiency: {analysis.Efficiency:F2}");
        WriteLine($"   Stability: {analysis.Stability:F2}");
        WriteLine();

        if (analysis.StretchAnalysis != null)
        {
            WriteLine($"?? Finger Stretch: {analysis.StretchAnalysis.MaxStretchDistance:F2}mm");
            WriteLine($"   Max Fret Span: {analysis.StretchAnalysis.MaxFretSpan} frets");
            WriteLine($"   Description: {analysis.StretchAnalysis.StretchDescription}");
            if (verbose && analysis.StretchAnalysis.HasWideStretches)
            {
                WriteLine($"   Wide Stretches: {analysis.StretchAnalysis.WideStretchCount}");
            }

            WriteLine();
        }

        if (analysis.FingeringEfficiencyAnalysis != null)
        {
            WriteLine($"? Fingering Efficiency: {analysis.FingeringEfficiencyAnalysis.EfficiencyScore:F2}");
            WriteLine($"   Finger Span: {analysis.FingeringEfficiencyAnalysis.FingerSpan} frets");
            WriteLine($"   Pinky Usage: {analysis.FingeringEfficiencyAnalysis.PinkyUsagePercentage:F1}%");
            if (verbose && analysis.FingeringEfficiencyAnalysis.Recommendations.Any())
            {
                WriteLine("   Recommendations:");
                foreach (var rec in analysis.FingeringEfficiencyAnalysis.Recommendations)
                {
                    WriteLine($"     � {rec}");
                }
            }

            WriteLine();
        }

        if (analysis.WristPostureAnalysis != null)
        {
            WriteLine($"???  Wrist Posture: {analysis.WristPostureAnalysis.WristAngleDegrees:F1}�");
            WriteLine($"   Type: {analysis.WristPostureAnalysis.PostureType}");
            WriteLine($"   Ergonomic: {(analysis.WristPostureAnalysis.IsErgonomic ? "? Yes" : "??  No")}");
            WriteLine();
        }

        if (verbose)
        {
            if (analysis.MutingAnalysis != null)
            {
                WriteLine($"?? Muting: {analysis.MutingAnalysis.Reason}");
                WriteLine();
            }

            if (analysis.SlideLegatoAnalysis != null)
            {
                WriteLine($"?? Slide/Legato: {analysis.SlideLegatoAnalysis.Technique}");
                WriteLine();
            }
        }

        WriteLine("-----------------------------------------------------------");
    }

    private void DisplayProgressionSummary(List<BiomechanicalPlayabilityAnalysis> analyses, string[] chordNames,
        bool verbose)
    {
        WriteLine("-----------------------------------------------------------");
        WriteLine("                 PROGRESSION SUMMARY                       ");
        WriteLine("-----------------------------------------------------------");
        WriteLine();

        var avgPlayability = analyses.Average(a => a.OverallScore);
        WriteLine($"?? Average Playability: {avgPlayability:F2}");
        WriteLine($"   {GetPlayabilityRating(avgPlayability)}");
        WriteLine();

        WriteLine("?? Individual Chord Scores:");
        for (var i = 0; i < analyses.Count && i < chordNames.Length; i++)
        {
            WriteLine($"   {chordNames[i],-10} {analyses[i].OverallScore:F2}");
        }

        WriteLine();

        WriteLine("-----------------------------------------------------------");
    }

    private string GetPlayabilityRating(double score)
    {
        return score switch
        {
            >= 0.9 => "? Excellent - Very easy to play",
            >= 0.7 => "?? Good - Comfortable for most players",
            >= 0.5 => "??  Moderate - May require practice",
            >= 0.3 => "??  Challenging - Difficult for beginners",
            _ => "? Very Difficult - Advanced technique required"
        };
    }
}
