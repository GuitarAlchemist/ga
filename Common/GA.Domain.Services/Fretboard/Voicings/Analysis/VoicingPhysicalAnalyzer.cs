namespace GA.Domain.Services.Fretboard.Voicings.Analysis;

using System;
using System.Collections.Generic;
using System.Linq;
using GA.Business.Core.Analysis.Voicings;
using GA.Domain.Core.Instruments.Fretboard.Voicings.Core;
using GA.Domain.Core.Instruments.Primitives;

/// <summary>
/// Specialized analyzer for physical, ergonomic, and fretboard-specific properties of voicings
/// </summary>
public static class VoicingPhysicalAnalyzer
{
    /// <summary>Fingers available to the fretting hand (index to little finger; the thumb is not modelled).</summary>
    public const int MaxFrettingFingers = 4;

    /// <summary>Score given to a voicing no fingering can hold: the top of the 1-10 scale.</summary>
    public const double UnplayableDifficultyScore = 10.0;

    /// <summary>Scores below this are labelled "Beginner".</summary>
    public const double BeginnerScoreLimit = 4.0;

    /// <summary>Scores at or above this are labelled "Advanced".</summary>
    public const double AdvancedScoreThreshold = 7.0;

    public static PhysicalLayout ExtractPhysicalLayout(Voicing voicing)
    {
        var positions = voicing.Positions;
        var fretPositions = new int[positions.Length];
        var stringsUsed = new List<int>();
        var mutedStrings = new List<int>();
        var openStrings = new List<int>();
        var minFret = int.MaxValue;
        var maxFret = 0;

        for (var i = 0; i < positions.Length; i++)
        {
            var stringNum = i + 1; // 1-based string numbering

            switch (positions[i])
            {
                case Position.Played played:
                    var fret = played.Location.Fret.Value;
                    fretPositions[i] = fret;
                    stringsUsed.Add(stringNum);

                    if (fret == 0)
                    {
                        openStrings.Add(stringNum);
                    }
                    else
                    {
                        if (fret < minFret) minFret = fret;
                        if (fret > maxFret) maxFret = fret;
                    }
                    break;

                case Position.Muted:
                    fretPositions[i] = -1;
                    mutedStrings.Add(stringNum);
                    break;
            }
        }

        // Determine hand position
        var handPosition = maxFret switch
        {
            <= 4 => "Open Position",
            <= 7 => "Low Position",
            <= 12 => "Middle Position",
            _ => "Upper Position"
        };

        // Handle case where all strings are open or muted
        if (minFret == int.MaxValue) minFret = 0;

        // Determine string set
        var stringSet = stringsUsed.Count switch
        {
            <= 3 => stringsUsed.All(s => s <= 3) ? "Top 3" : stringsUsed.All(s => s >= 4) ? "Bottom 3" : "Mixed",
            4 => stringsUsed.All(s => s <= 4) ? "Top 4" : stringsUsed.All(s => s >= 3) ? "Bottom 4" : "Inner 4",
            _ => "Full"
        };

        return new(
            fretPositions,
            [.. stringsUsed],
            [.. mutedStrings],
            [.. openStrings],
            minFret,
            maxFret,
            handPosition,
            stringSet
        );
    }

    public static PlayabilityInfo CalculatePlayability(PhysicalLayout layout)
    {
        // Use Physical distance (logarithmic) for accurate stretch measurement
        var playedFrets = layout.FretPositions.Where(f => f > 0).ToList();
        var physicalSpan = playedFrets.Any() 
            ? GA.Domain.Services.Fretboard.Analysis.PhysicalFretboardCalculator.CalculateFretDistanceMm(playedFrets.Min(), playedFrets.Max())
            : 0.0;
        var spanScore = physicalSpan / 80.0;

        // Calculate hand stretch (legacy field remains raw count for compatibility)
        var handStretch = layout.MaxFret - layout.MinFret;

        // Detect barre requirement (the fret the index finger lies across, if any)
        var barreFret = DetectBarreFret(layout.FretPositions);
        var barreRequired = barreFret.HasValue;

        // Minimum fingers needed: a finger lying across several notes of one fret counts once.
        // Deliberately not capped: more than MaxFrettingFingers means no fingering exists.
        var minimumFingers = CountMinimumFingers(layout.FretPositions);

        // Detect CAGED shape
        var cagedShape = DetectCagedShape(layout);

        // Generate barre info
        var barreInfo = barreRequired ? $"Fret {barreFret} barre" : null;

        // Detect shell voicing family
        string? shellFamily = null;
        if (layout.StringsUsed.Length <= 4 && layout.StringsUsed.Length >= 3)
        {
            shellFamily = "3-4 note shell";
        }

        // Compute numeric difficulty score (1-10) using spanScore
        var difficultyScore = 1.0;
        if (barreRequired) difficultyScore += 2.0;

        // Use logarithmic span score instead of raw fret count
        difficultyScore += spanScore * 3.0; // max ~4.0 extra for wide stretch

        if (layout.OpenStrings.Length == 0) difficultyScore += 1.0;
        if (minimumFingers == 4) difficultyScore += 1.0;
        difficultyScore = Math.Min(10.0, difficultyScore);

        // A voicing that needs more fingers than a hand has cannot be played, however narrow it is
        if (minimumFingers > MaxFrettingFingers) difficultyScore = UnplayableDifficultyScore;

        // The label is a banding of the score, so the two cannot disagree
        var difficulty = DifficultyLabel(difficultyScore);

        return new(
            difficulty,
            handStretch,
            barreRequired,
            barreInfo,
            minimumFingers,
            cagedShape,
            shellFamily,
            difficultyScore
        );
    }

    public static ErgonomicsInfo AnalyzeErgonomics(PhysicalLayout layout, PlayabilityInfo playability)
    {
        // Count string skips (non-adjacent played strings)
        var playedStrings = layout.StringsUsed.Where(s => s > 0).OrderBy(s => s).ToList();
        var stringSkips = 0;
        for (var i = 1; i < playedStrings.Count; i++)
        {
            var gap = playedStrings[i] - playedStrings[i - 1];
            if (gap > 1) stringSkips += gap - 1;
        }

        // Simple finger assignment heuristic (would need more sophisticated analysis)
        string? fingerAssignment = null;
        if (playability.BarreRequired)
        {
            fingerAssignment = "1:barre";
        }

        // Check if thumb is likely required (low bass note on string 6 with spread voicing)
        var requiresThumb = playability.HandStretch >= 5 && layout.FretPositions[0] > 0;

        // Check for physically impossible voicings
        var isImpossible = playability.HandStretch > 6 || playability.MinimumFingers > 4;

        string? notes = null;
        if (stringSkips > 2) notes = "Difficult arpeggiation due to string skips";
        if (isImpossible) notes = "May be physically challenging or impossible for most players";

        return new(stringSkips, fingerAssignment, requiresThumb, isImpossible, notes);
    }

    public static string[] GeneratePhysicalTags(PhysicalLayout layout, PlayabilityInfo playability, ErgonomicsInfo ergonomics)
    {
        var tags = new List<string>();

        // Position Tags
        tags.Add(layout.HandPosition.ToLowerInvariant().Replace(" ", "-"));
        
        // Difficulty Tags
        tags.Add(playability.Difficulty.ToLowerInvariant());
        if (playability.Difficulty == "Beginner") tags.Add("easy-to-play");
        if (playability.Difficulty == "Advanced") tags.Add("technical");

        // Construction Tags
        if (playability.BarreRequired) tags.Add("barre-chord");
        if (layout.OpenStrings.Length > 0) tags.Add("open-strings");
        if (layout.OpenStrings.Length >= 3) tags.Add("ambient-open");
        if (playability.CagedShape != null) tags.Add(playability.CagedShape.ToLowerInvariant());
        if (playability.ShellFamily != null) tags.Add("shell-voicing");

        // Ergonomic Tags
        if (ergonomics.RequiresThumb) tags.Add("thumb-chord");
        if (ergonomics.StringSkips > 0) tags.Add("string-skips");
        if (ergonomics.IsImpossible) tags.Add("physically-challenging");

        // String Set Tags
        tags.Add(layout.StringSet.ToLowerInvariant().Replace(" ", "-"));

        return [.. tags.Distinct()];
    }

    // ================== HELPERS ==================

    /// <summary>
    ///     The fret of the barre the index finger has to make, or null when the voicing needs none.
    ///     A barre is needed only when there are more fretted notes than <see cref="MaxFrettingFingers"/>:
    ///     up to that, every note can have its own finger, as in the sparse grip <c>1x2x1x</c>. The barre
    ///     is then the lowest fretted fret held on both outermost played strings, across at least three
    ///     strings, with no open string in between (strings in between are fretted at that fret or higher,
    ///     or muted and damped by the finger). This counts the E-shape <c>133211</c> as well as the A-shape
    ///     <c>x13331</c>, and no open chord, whose outermost strings are open. A partial barre on a higher
    ///     fret, like the ring finger at fret 3 in <c>x12333</c>, is not detected.
    /// </summary>
    internal static int? DetectBarreFret(int[] fretPositions)
    {
        if (fretPositions.Count(f => f > 0) <= MaxFrettingFingers) return null;

        var first = Array.FindIndex(fretPositions, f => f >= 0);
        var last = Array.FindLastIndex(fretPositions, f => f >= 0);
        if (first < 0 || last - first < 2) return null;

        var lowestFret = fretPositions.Where(f => f > 0).DefaultIfEmpty(0).Min();
        if (lowestFret == 0 || fretPositions[first] != lowestFret || fretPositions[last] != lowestFret) return null;

        for (var i = first + 1; i < last; i++)
        {
            if (fretPositions[i] == 0) return null; // an open string cannot ring under a barre
        }

        return lowestFret;
    }

    /// <summary>
    ///     The fewest fingers that can hold every fretted note: each finger stays on one fret and may lie
    ///     across several notes of that fret, unless an open string or a note on a lower fret lies between
    ///     them (a muted string between them is damped by the finger). Fingers are ordered along the neck,
    ///     so any count up to <see cref="MaxFrettingFingers"/> has a legal fingering, and a larger count has none.
    /// </summary>
    internal static int CountMinimumFingers(int[] fretPositions)
    {
        var fingers = 0;
        foreach (var fret in fretPositions.Where(f => f > 0).Distinct())
        {
            var previous = -1;
            for (var i = 0; i < fretPositions.Length; i++)
            {
                if (fretPositions[i] != fret) continue;

                var sharesFinger = previous >= 0;
                for (var between = previous + 1; sharesFinger && between < i; between++)
                {
                    var f = fretPositions[between];
                    if (f == 0 || (f > 0 && f < fret)) sharesFinger = false;
                }

                if (!sharesFinger) fingers++;
                previous = i;
            }
        }

        return fingers;
    }

    private static string DifficultyLabel(double difficultyScore) => difficultyScore switch
    {
        < BeginnerScoreLimit => "Beginner",
        >= AdvancedScoreThreshold => "Advanced",
        _ => "Intermediate"
    };

    private static string? DetectCagedShape(PhysicalLayout layout)
    {
        var frets = layout.FretPositions;
        if (frets.Length != 6) return null;

        // Basic E-Shape detection (Root on 6th string)
        // Pattern relative to barre/nut: 0-2-2-1-0-0
        // We look for the characteristic intervals on strings 6, 5, 4, 3

        // Find the "base" fret (min played fret, treated as 0 or barre)
        // For E-shape, base is the fret on string 6 (Index 0).
        var baseFret = frets[0];
        if (baseFret < 0) return null; // Root must be on string 6

        // Check relative pattern: 2, 2, 1, 0, 0
        // S6: base
        // S5: base + 2
        // S4: base + 2
        // S3: base + 1
        // S2: base
        // S1: base

        // Allow muted high strings for "power chord" variations or variations
        // But strictly, full E-shape:

        var match = true;
        if (frets[1] != baseFret + 2) match = false;
        if (match && frets[2] != baseFret + 2) match = false;
        if (match && frets[3] != baseFret + 1) match = false;

        // Top strings can be base or muted or omitted?
        // Standard E shape includes them.
        if (match)
        {
             // Check S2 and S1 if played
             if (frets[4] >= 0 && frets[4] != baseFret) match = false;
             if (frets[5] >= 0 && frets[5] != baseFret) match = false;
        }

        if (match) return "E-Shape";

        // A-Shape detection (Root on 5th string)
        // Pattern: x, 0, 2, 2, 2, 0 (or similar)
        // Base on S5 (Index 1)
        if (frets[0] == -1 && frets[1] >= 0)
        {
            var baseA = frets[1];
            // S4, S3, S2 should be baseA + 2
            if (frets[2] == baseA + 2 && frets[3] == baseA + 2 && frets[4] == baseA + 2)
            {
                // S1 should be baseA
                if (frets[5] == -1 || frets[5] == baseA)
                    return "A-Shape";
            }
        }

        // C-Shape detection (Root on 5th string)
        // Pattern: x, 3, 2, 0, 1, 0 (Base C is 3)
        // Relative: x, R, R-1, R-3, R-2, R-3 ? No.
        // Let's simply check Open C: x, 3, 2, 0, 1, 0
        // R=3. S5=3. S4=2(R-1). S3=0(R-3). S2=1(R-2). S1=0(R-3).
        // Check relative pattern from Root on S5 (Index 1).
        // Standard C Shape:
        // S5: R
        // S4: R - 1
        // S3: R - 3
        // S2: R - 2
        // S1: R - 3 (or omitted)
        // Note: This requires R >= 3.

        if (frets[0] == -1 && frets[1] >= 3)
        {
            var r = frets[1];
            if (frets[2] == r - 1 &&
                frets[3] == r - 3 &&
                frets[4] == r - 2)
            {
                return "C-Shape";
            }
        }
        else if (frets[0] == -1 && frets[1] == 3) // Open C
        {
             // 3, 2, 0, 1, 0
             // frets[2] == 2
             // frets[3] == 0
             // frets[4] == 1
             if(frets[2] == 2 && frets[3] == 0 && frets[4] == 1) return "C-Shape";
        }

        return null;
    }
}
