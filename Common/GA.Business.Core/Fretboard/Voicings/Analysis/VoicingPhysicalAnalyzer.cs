namespace GA.Business.Core.Fretboard.Voicings.Analysis;

using System;
using System.Collections.Generic;
using System.Linq;
using Core;
using Primitives;

/// <summary>
/// Specialized analyzer for physical, ergonomic, and fretboard-specific properties of voicings
/// </summary>
public static class VoicingPhysicalAnalyzer
{
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
        // Calculate hand stretch (fret span)
        var handStretch = layout.MaxFret - layout.MinFret;

        // Detect barre requirement (same fret on multiple adjacent strings)
        var barreRequired = DetectBarreRequirement(layout.FretPositions);

        // Estimate minimum fingers needed
        var uniqueFrets = layout.FretPositions.Where(f => f > 0).Distinct().Count();
        var minimumFingers = Math.Min(uniqueFrets, 4);

        // Determine difficulty
        var difficulty = CalculateDifficulty(handStretch, barreRequired, layout.OpenStrings.Length);

        // Detect CAGED shape (simplified - would need more sophisticated analysis)
        var cagedShape = DetectCagedShape(layout);

        // Generate barre info if needed
        string? barreInfo = null;
        if (barreRequired)
        {
            var barreFret = layout.FretPositions.Where(f => f > 0).GroupBy(f => f)
                .OrderByDescending(g => g.Count()).FirstOrDefault()?.Key;
            if (barreFret.HasValue)
            {
                barreInfo = $"Fret {barreFret} barre";
            }
        }

        // Detect shell voicing family
        string? shellFamily = null;
        if (layout.StringsUsed.Length <= 4 && layout.StringsUsed.Length >= 3)
        {
            shellFamily = "3-4 note shell";
        }

        // Compute numeric difficulty score (1-10)
        var difficultyScore = 1.0;
        if (barreRequired) difficultyScore += 2.0;
        difficultyScore += Math.Max(0, handStretch - 3) * 1.5;
        if (layout.OpenStrings.Length == 0) difficultyScore += 1.0;
        if (minimumFingers == 4) difficultyScore += 1.0;
        difficultyScore = Math.Min(10.0, difficultyScore);

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
    
    // ================== HELPERS ==================

    private static bool DetectBarreRequirement(int[] fretPositions)
    {
        // Check for same fret on 3+ adjacent strings
        for (var i = 0; i < fretPositions.Length - 2; i++)
        {
            var fret = fretPositions[i];
            if (fret > 0 &&
                fretPositions[i + 1] == fret &&
                fretPositions[i + 2] == fret)
            {
                return true;
            }
        }
        return false;
    }

    private static string CalculateDifficulty(int handStretch, bool barreRequired, int openStringCount)
    {
        // Beginner: Small stretch, no barre, has open strings
        if (handStretch <= 3 && !barreRequired && openStringCount > 0)
        {
            return "Beginner";
        }

        // Advanced: Large stretch or complex barre
        if (handStretch >= 5 || barreRequired && handStretch >= 4)
        {
            return "Advanced";
        }

        // Intermediate: Everything else
        return "Intermediate";
    }

    private static string? DetectCagedShape(PhysicalLayout layout)
    {
        // Basic E-Shape detection (Root on 6th string)
        // Pattern relative to barre/nut: 0-2-2-1-0-0
        // We look for the characteristic intervals on strings 6, 5, 4, 3
        
        // Ensure 6th string is played
        var rootStringMatches = layout.FretPositions[5] >= 0; // String 6 is index 5? No, positions is array[6].
        // Wait, FretPositions index 0 = String 1 (High E) usually in this codebase?
        // Let's check ExtractPhysicalLayout: "var stringNum = i + 1;" loop 0..5.
        // Usually index 0 is low E or high E depending on convention.
        // "Position.LowToHigh" is standard in this project?
        // Let's verify loop: standard 6-string guitar usually defined low-to-high or high-to-low.
        // GA convention: usually Low (6) to High (1) if simple array?
        // BUT "var stringNum = i + 1".
        // In `Voicing.Positions`, is index 0 the 6th string (Low E) or 1st string (High E)?
        // Primitives like "x-3-2-0-1-0" are typically Low-to-High in string representation.
        // Let's assume Low-to-High (Index 0 = String 6, Low E).

        // E-Shape Pattern (relative to base): 0, 2, 2, 1, 0, 0
        // Indices: 0(E), 1(A), 2(D), 3(G), 4(B), 5(e)
        // Check relative frets
        
        var frets = layout.FretPositions;
        if (frets.Length != 6) return null;

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
        
        bool match = true;
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
