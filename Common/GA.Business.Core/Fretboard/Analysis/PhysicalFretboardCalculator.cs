namespace GA.Business.Core.Fretboard.Analysis;

using Primitives;
using static Primitives.Position;

/// <summary>
///     Calculates physical distances and ergonomic metrics on the fretboard
///     Takes into account the logarithmic decrease in fret spacing
/// </summary>
public static class PhysicalFretboardCalculator
{
    /// <summary>
    ///     Playability difficulty based on physical constraints
    /// </summary>
    public enum PlayabilityDifficulty
    {
        VeryEasy, // < 40mm stretch, comfortable for beginners
        Easy, // 40-60mm stretch, standard open chords
        Moderate, // 60-80mm stretch, standard barre chords
        Challenging, // 80-100mm stretch, requires practice
        Difficult, // 100-120mm stretch, advanced technique
        VeryDifficult, // 120-140mm stretch, expert level
        Extreme, // > 140mm stretch, exceptional hand size/flexibility
        Impossible // Physically unplayable for most humans
    }

    /// <summary>
    ///     Calculate the physical distance from nut to a specific fret
    ///     Uses the equal temperament formula: distance = scaleLength * (1 - 2^(-fret/12))
    /// </summary>
    /// <param name="fretNumber">Fret number (0 = nut)</param>
    /// <param name="scaleLengthMm">Scale length in millimeters</param>
    /// <returns>Distance from nut to fret in millimeters</returns>
    public static double CalculateFretPositionMm(int fretNumber, double scaleLengthMm = ScaleLengths.Default)
    {
        if (fretNumber == 0)
        {
            return 0.0;
        }

        return scaleLengthMm * (1.0 - Math.Pow(2.0, -fretNumber / 12.0));
    }

    /// <summary>
    ///     Calculate the physical distance between two frets
    ///     This accounts for the logarithmic decrease in fret spacing
    /// </summary>
    /// <param name="fret1">First fret number</param>
    /// <param name="fret2">Second fret number</param>
    /// <param name="scaleLengthMm">Scale length in millimeters</param>
    /// <returns>Physical distance between the two frets in millimeters</returns>
    public static double CalculateFretDistanceMm(int fret1, int fret2, double scaleLengthMm = ScaleLengths.Default)
    {
        var pos1 = CalculateFretPositionMm(fret1, scaleLengthMm);
        var pos2 = CalculateFretPositionMm(fret2, scaleLengthMm);
        return Math.Abs(pos2 - pos1);
    }

    /// <summary>
    ///     Calculate the width of a single fret (distance between fret wire and next fret wire)
    /// </summary>
    /// <param name="fretNumber">Fret number</param>
    /// <param name="scaleLengthMm">Scale length in millimeters</param>
    /// <returns>Fret width in millimeters</returns>
    public static double CalculateFretWidthMm(int fretNumber, double scaleLengthMm = ScaleLengths.Default)
    {
        return CalculateFretDistanceMm(fretNumber, fretNumber + 1, scaleLengthMm);
    }

    /// <summary>
    ///     Calculate string spacing at a specific fret
    ///     String spacing increases from nut to bridge
    /// </summary>
    /// <param name="fretNumber">Fret number</param>
    /// <param name="nutWidthMm">Nut width in millimeters (default: 43mm for electric)</param>
    /// <param name="bridgeWidthMm">Bridge width in millimeters (default: 52mm for electric)</param>
    /// <param name="scaleLengthMm">Scale length in millimeters</param>
    /// <returns>String spacing in millimeters</returns>
    public static double CalculateStringSpacingMM(
        int fretNumber,
        double nutWidthMm = 43.0,
        double bridgeWidthMm = 52.0,
        double scaleLengthMm = ScaleLengths.Default)
    {
        var fretPosition = CalculateFretPositionMm(fretNumber, scaleLengthMm);
        var ratio = fretPosition / scaleLengthMm;
        var widthAtFret = nutWidthMm + (bridgeWidthMm - nutWidthMm) * ratio;
        return widthAtFret / 5.0; // 6 strings = 5 gaps
    }

    /// <summary>
    ///     Analyze the physical playability of a chord voicing
    /// </summary>
    public static PhysicalPlayabilityAnalysis AnalyzePlayability(
        ImmutableList<Position> positions,
        double scaleLengthMm = ScaleLengths.Default,
        double nutWidthMm = 43.0,
        double bridgeWidthMm = 52.0)
    {
        var playedPositions = positions.OfType<Played>().ToList();

        if (playedPositions.Count == 0)
        {
            return new PhysicalPlayabilityAnalysis(
                0, 0, 0, 0, 0,
                PlayabilityDifficulty.VeryEasy,
                true,
                "No notes played",
                []);
        }

        // Extract fret numbers (excluding open strings for stretch calculation)
        var frettedPositions = playedPositions.Where(p => p.Location.Fret.Value > 0).ToList();

        if (frettedPositions.Count == 0)
        {
            // All open strings
            return new PhysicalPlayabilityAnalysis(
                0, 0, 0, 0, 0,
                PlayabilityDifficulty.VeryEasy,
                true,
                "Open chord - no finger stretch required",
                []);
        }

        var frets = frettedPositions.Select(p => p.Location.Fret.Value).ToList();
        var minFret = frets.Min();
        var maxFret = frets.Max();
        var fretSpan = maxFret - minFret;

        // Calculate physical fret span in millimeters
        var fretSpanMm = CalculateFretDistanceMm(minFret, maxFret, scaleLengthMm);

        // Calculate finger stretches
        var stretches = new List<double>();
        for (var i = 0; i < frettedPositions.Count - 1; i++)
        {
            for (var j = i + 1; j < frettedPositions.Count; j++)
            {
                var fret1 = frettedPositions[i].Location.Fret.Value;
                var fret2 = frettedPositions[j].Location.Fret.Value;
                var stretch = CalculateFretDistanceMm(fret1, fret2, scaleLengthMm);
                stretches.Add(stretch);
            }
        }

        var maxStretch = stretches.Count != 0 ? stretches.Max() : 0.0;
        var avgStretch = stretches.Count != 0 ? stretches.Average() : 0.0;

        // Calculate vertical span (string-to-string distance)
        var strings = playedPositions.Select(p => p.Location.Str.Value).ToList();
        var minString = strings.Min();
        var maxString = strings.Max();
        var stringSpan = maxString - minString;

        // Use average fret position for string spacing calculation
        var avgFret = (int)Math.Round(frets.Average());
        var stringSpacing = CalculateStringSpacingMM(avgFret, nutWidthMm, bridgeWidthMm, scaleLengthMm);
        var verticalSpanMm = stringSpan * stringSpacing;

        // Calculate diagonal stretch (worst case: max fret span + max string span)
        var diagonalStretchMm = Math.Sqrt(fretSpanMm * fretSpanMm + verticalSpanMm * verticalSpanMm);

        // Determine difficulty and playability
        var (difficulty, isPlayable, reason) = ClassifyDifficulty(
            fretSpanMm, maxStretch, diagonalStretchMm, fretSpan, playedPositions.Count, minFret);

        // Generate suggested fingering (simplified - could be enhanced)
        var suggestedFingering = GenerateSuggestedFingering(playedPositions, fretSpan);

        return new PhysicalPlayabilityAnalysis(
            fretSpanMm, maxStretch, avgStretch, verticalSpanMm, diagonalStretchMm,
            difficulty, isPlayable, reason, suggestedFingering);
    }

    /// <summary>
    ///     Classify difficulty based on physical measurements
    /// </summary>
    private static (PlayabilityDifficulty difficulty, bool isPlayable, string reason) ClassifyDifficulty(
        double fretSpanMm,
        double maxStretchMm,
        double diagonalStretchMm,
        int fretSpan,
        int noteCount,
        int minFret)
    {
        // Human hand ergonomics (based on research and practical experience)
        // Average adult hand can comfortably stretch 80-100mm
        // Expert players can stretch 120-140mm
        // Anything beyond 140mm is exceptional

        // Adjust difficulty based on fret position (higher frets are easier due to smaller spacing)
        var positionFactor = minFret switch
        {
            <= 3 => 1.0, // Low frets - full difficulty
            <= 7 => 0.9, // Mid-low frets - slightly easier
            <= 12 => 0.8, // Mid frets - easier
            <= 17 => 0.7, // Mid-high frets - much easier
            _ => 0.6 // High frets - significantly easier
        };

        var adjustedStretch = maxStretchMm * positionFactor;
        var adjustedDiagonal = diagonalStretchMm * positionFactor;
        var adjustedFretSpan = fretSpanMm * positionFactor;

        // Check for impossible conditions
        if (fretSpan > 6)
        {
            return (PlayabilityDifficulty.Impossible, false,
                $"Fret span of {fretSpan} exceeds maximum finger reach (6 frets)");
        }

        if (adjustedDiagonal > 175)
        {
            return (PlayabilityDifficulty.Impossible, false,
                $"Diagonal stretch of {adjustedDiagonal:F1}mm exceeds human capability");
        }

        if (adjustedStretch > 160)
        {
            return (PlayabilityDifficulty.Impossible, false,
                $"Finger stretch of {adjustedStretch:F1}mm exceeds human capability");
        }

        if (noteCount > 6)
        {
            return (PlayabilityDifficulty.Impossible, false, $"Cannot play {noteCount} notes on a 6-string guitar");
        }

        var effectiveStretch = Math.Max(adjustedStretch, Math.Max(adjustedDiagonal * 0.9, adjustedFretSpan));

        // Classify based on dominant stretch metric
        var difficulty = effectiveStretch switch
        {
            < 30 => PlayabilityDifficulty.VeryEasy,
            < 50 => PlayabilityDifficulty.Easy,
            < 70 => PlayabilityDifficulty.Moderate,
            < 90 => PlayabilityDifficulty.Challenging,
            < 110 => PlayabilityDifficulty.Difficult,
            < 140 => PlayabilityDifficulty.VeryDifficult,
            _ => PlayabilityDifficulty.Extreme
        };

        var dominantMetric = effectiveStretch == adjustedStretch
            ? $"Maximum stretch of {adjustedStretch:F1}mm"
            : effectiveStretch == adjustedFretSpan
                ? $"Fret span of {adjustedFretSpan:F1}mm"
                : $"Diagonal reach of {adjustedDiagonal:F1}mm";

        var reason = difficulty switch
        {
            PlayabilityDifficulty.VeryEasy => $"Comfortable for all skill levels ({dominantMetric})",
            PlayabilityDifficulty.Easy => $"Suitable for beginners ({dominantMetric})",
            PlayabilityDifficulty.Moderate => $"Standard chord voicing ({dominantMetric})",
            PlayabilityDifficulty.Challenging => $"Requires practice and finger strength ({dominantMetric})",
            PlayabilityDifficulty.Difficult => $"Advanced technique required ({dominantMetric})",
            PlayabilityDifficulty.VeryDifficult =>
                $"Expert level - exceptional hand flexibility needed ({dominantMetric})",
            PlayabilityDifficulty.Extreme => $"Extreme stretch - only for players with large hands ({dominantMetric})",
            _ => "Unknown difficulty"
        };

        return (difficulty, true, reason);
    }

    /// <summary>
    ///     Generate suggested fingering for a chord voicing
    /// </summary>
    private static IReadOnlyList<FingerPosition> GenerateSuggestedFingering(
        IList<Played> playedPositions,
        int fretSpan)
    {
        // Simplified fingering suggestion
        // In a real implementation, this would use sophisticated algorithms
        // considering barre chords, finger independence, etc.

        var fingering = new List<FingerPosition>();
        var sortedPositions = playedPositions.OrderBy(p => p.Location.Fret.Value).ToList();

        // Check for barre chord pattern (same fret on multiple strings)
        var fretGroups = sortedPositions.GroupBy(p => p.Location.Fret.Value).ToList();
        var barreCandidate = fretGroups.FirstOrDefault(g => g.Count() >= 2);

        if (barreCandidate != null && fretSpan <= 3)
        {
            // Suggest barre chord fingering
            foreach (var pos in barreCandidate)
            {
                fingering.Add(new FingerPosition(
                    pos.Location.Str,
                    pos.Location.Fret,
                    -1, // Barre
                    "barre"));
            }

            // Assign remaining fingers
            var remainingPositions = sortedPositions.Except(barreCandidate).ToList();
            var finger = 2; // Start with middle finger after barre (index)
            foreach (var pos in remainingPositions.OrderBy(p => p.Location.Fret.Value))
            {
                fingering.Add(new FingerPosition(
                    pos.Location.Str,
                    pos.Location.Fret,
                    Math.Min(finger++, 4),
                    "normal"));
            }
        }
        else
        {
            // Standard fingering
            var finger = 1;
            foreach (var pos in sortedPositions)
            {
                fingering.Add(new FingerPosition(
                    pos.Location.Str,
                    pos.Location.Fret,
                    Math.Min(finger++, 4),
                    "normal"));
            }
        }

        return fingering.AsReadOnly();
    }

    /// <summary>
    ///     Standard guitar scale lengths in millimeters
    /// </summary>
    public static class ScaleLengths
    {
        public const double Classical = 650.0; // Classical guitar
        public const double Acoustic = 645.0; // Acoustic guitar
        public const double Electric = 648.0; // Electric guitar (Fender)
        public const double GibsonElectric = 628.0; // Gibson Les Paul
        public const double Bass = 864.0; // Bass guitar (34")
        public const double Default = Electric; // Default to electric
    }

    /// <summary>
    ///     Physical playability analysis result
    /// </summary>
    public record PhysicalPlayabilityAnalysis(
        double FretSpanMm,
        double MaxFingerStretchMm,
        double AverageFingerStretchMm,
        double VerticalSpanMm,
        double DiagonalStretchMm,
        PlayabilityDifficulty Difficulty,
        bool IsPlayable,
        string DifficultyReason,
        IReadOnlyList<FingerPosition> SuggestedFingering);

    /// <summary>
    ///     Suggested finger position
    /// </summary>
    public record FingerPosition(
        Str String,
        Fret Fret,
        int FingerNumber, // 1=index, 2=middle, 3=ring, 4=pinky, 0=thumb (rare), -1=barre
        string Technique); // "normal", "barre", "stretch", "thumb"
}
