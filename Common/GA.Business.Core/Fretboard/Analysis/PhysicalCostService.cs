namespace GA.Business.Core.Fretboard.Analysis;

using System;
using System.Collections.Generic;
using System.Linq;

using GA.Business.Core.Fretboard.Biomechanics;

using GA.Business.Core.Player;



/// <summary>

/// Calculates physical ergonomic costs for fretboard realizations.

/// Used to rank multiple ways of playing the same notes.

/// </summary>

public class PhysicalCostService

{

    private readonly PlayerProfile _profile;
    private readonly IMlNaturalnessRanker? _naturalnessRanker;



    public PhysicalCostService(PlayerProfile? profile = null, IMlNaturalnessRanker? naturalnessRanker = null)

    {

        _profile = profile ?? new PlayerProfile();
        _naturalnessRanker = naturalnessRanker;

    }



    public record CostResult(double TotalCost, Dictionary<string, double> Breakdown);



    /// <summary>

    /// Calculates the "effort" required to play a specific static shape.

    /// </summary>

    public CostResult CalculateStaticCost(List<FretboardPosition> shape)

    {

        if (shape == null || shape.Count == 0) return new CostResult(0, new());



        var breakdown = new Dictionary<string, double>();

        var nonZeroFrets = shape.Where(p => p.Fret > 0).Select(p => p.Fret).ToList();



        // 1. Stretch Cost (Physical distance based)

        double stretchCost = 0;

        if (nonZeroFrets.Count > 1)

        {

            double physicalSpan = FretboardGeometry.CalculatePhysicalSpan(nonZeroFrets);

            double limit = BiomechanicsConstants.ComfortableReachScaleUnits;

            if (physicalSpan > limit) 

            {

                stretchCost = (physicalSpan - limit) * _profile.StretchWeight;

            }

        }

        

        // Disjointed Open + High penalty

        if (shape.Any(p => p.Fret == 0) && nonZeroFrets.Count > 0)

        {

            var minFingered = nonZeroFrets.Min();

            if (minFingered > 5) stretchCost += (minFingered - 5) * 1.5; 

        }

        breakdown["Stretch"] = stretchCost;



        // 2. Fat Fingers Penalty (Cramped high frets)

        int threshold = BiomechanicsConstants.CrampedFretThreshold;

        double fatFingers = shape.Sum(p => p.Fret > threshold ? (p.Fret - threshold) * _profile.FatFingerWeight : 0);

        breakdown["FatFingers"] = fatFingers;



        // 3. Register Penalty (Based on Profile Preferences)

        double avgFret = shape.Average(p => p.Fret);

        double registerCost = 0;

        double openBonus = shape.Count(p => p.Fret == 0) * -0.5;



        if (avgFret == 0) registerCost = openBonus;

        else if (avgFret < _profile.PreferredMinFret) registerCost = (_profile.PreferredMinFret - avgFret) * 0.1 + openBonus; 

        else if (avgFret <= _profile.PreferredMaxFret) registerCost = openBonus;            

        else 

        {

            // Exponential penalty for high register

            registerCost = Math.Pow(1.2, avgFret - _profile.PreferredMaxFret) * _profile.RegisterWeight + openBonus;

            if (shape.Any(p => p.Fret > 22)) registerCost += 50.0;

        }

        breakdown["Register"] = registerCost;



        // 4. String Skip Cost

        var strings = shape.Select(p => p.StringIndex.Value).OrderBy(s => s).ToList();

        double skipCost = 0;

        for (int i = 0; i < strings.Count - 1; i++)

        {

            var diff = strings[i+1] - strings[i];

            if (diff > 1) skipCost += (diff - 1) * _profile.SkipWeight;

        }

        breakdown["StringSkip"] = skipCost;



        // 5. String Tension (Thick strings heavier)

        double tensionCost = shape.Sum(p => {

            double weight = 1.0 + (p.StringIndex.Value - 3.5) * 0.1;

            return p.Fret > 0 ? _profile.TensionWeight * weight : 0;

        });

        breakdown["Tension"] = tensionCost;



        // Final Score

        double total = stretchCost + fatFingers + registerCost + skipCost + tensionCost;

        total /= Math.Sqrt(shape.Count); 



        return new CostResult(total, breakdown);

    }

    /// <summary>
    /// Calculates the transition cost between two shapes (voice leading effort).
    /// </summary>
    public double CalculateTransitionCost(List<FretboardPosition> from, List<FretboardPosition> to)
    {
        double movement = 0;
        var fromMap = from.ToDictionary(p => p.StringIndex.Value, p => p.Fret);
        var toMap = to.ToDictionary(p => p.StringIndex.Value, p => p.Fret);

        var allStrings = fromMap.Keys.Union(toMap.Keys).Distinct().ToList();
        var movements = new List<int>();

        foreach (var s in allStrings)
        {
            int f1 = fromMap.GetValueOrDefault(s, -1);
            int f2 = toMap.GetValueOrDefault(s, -1);

            if (f1 != -1 && f2 != -1)
            {
                int diff = f2 - f1;
                movement += Math.Abs(diff);
                movements.Add(diff);
            }
            else if (f1 != -1 || f2 != -1) movement += 2.0; 
        }

        // Shift Discount
        if (movements.Count > 1)
        {
            var directions = movements.Select(m => Math.Sign(m)).Distinct().ToList();
            if (directions.Count == 1 && directions[0] != 0) movement *= 0.5;
        }

        // ML Naturalness Adjustment
        if (_naturalnessRanker != null)
        {
            float naturalness = _naturalnessRanker.PredictNaturalness(from, to);
            // Low naturalness (0) -> High penalty; High naturalness (1) -> Low penalty
            double mlPenalty = (1.0 - naturalness) * 5.0;
            movement += mlPenalty;
        }

        return movement;
    }
}
