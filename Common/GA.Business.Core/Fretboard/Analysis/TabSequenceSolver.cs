namespace GA.Business.Core.Fretboard.Analysis;

using System;
using System.Collections.Generic;
using System.Linq;
using GA.Business.Core.Notes;

/// <summary>
/// Solves for the optimal sequence of fretboard realizations for a score.
/// Uses dynamic programming (Viterbi-like) to minimize total physical cost.
/// </summary>
public class TabSequenceSolver
{
    private readonly FretboardPositionMapper _mapper;
    private readonly PhysicalCostService _costService;

    public TabSequenceSolver(FretboardPositionMapper mapper, PhysicalCostService costService)
    {
        _mapper = mapper;
        _costService = costService;
    }

    /// <summary>
    /// Finds the most ergonomic path for a sequence of pitch sets.
    /// </summary>
    public List<List<FretboardPosition>> Solve(IEnumerable<IEnumerable<Pitch>> score)
    {
        var steps = score.ToList();
        if (steps.Count == 0) return new();

        // states[i] = all valid realizations for step i
        var states = steps.Select(pitches => _mapper.MapChord(pitches).ToList()).ToList();

        return PerformViterbi(states);
    }

    private List<List<FretboardPosition>> PerformViterbi(List<List<List<FretboardPosition>>> states)
    {
        int n = states.Count;
        var dp = new (double Cost, int PrevIndex)[n][];

        // 1. Initialize first step
        dp[0] = new (double, int)[states[0].Count];
        for (int j = 0; j < states[0].Count; j++)
        {
            var staticCost = _costService.CalculateStaticCost(states[0][j]).TotalCost;
            dp[0][j] = (staticCost, -1);
        }

        // 2. Fill DP table for subsequent steps
        for (int i = 1; i < n; i++)
        {
            dp[i] = new (double, int)[states[i].Count];
            for (int curr = 0; curr < states[i].Count; curr++)
            {
                double minPrevCost = double.MaxValue;
                int bestPrev = -1;

                var currShape = states[i][curr];
                var currStaticCost = _costService.CalculateStaticCost(currShape).TotalCost;

                for (int prev = 0; prev < states[i - 1].Count; prev++)
                {
                    var prevShape = states[i - 1][prev];
                    var transCost = _costService.CalculateTransitionCost(prevShape, currShape);
                    
                    double total = dp[i - 1][prev].Cost + transCost + currStaticCost;
                    
                    if (total < minPrevCost)
                    {
                        minPrevCost = total;
                        bestPrev = prev;
                    }
                }

                dp[i][curr] = (minPrevCost, bestPrev);
            }
        }

        // 3. Backtrack to find optimal sequence
        var result = new List<List<FretboardPosition>>();
        double minFinalCost = double.MaxValue;
        int lastBest = -1;

        for (int j = 0; j < states[n - 1].Count; j++)
        {
            if (dp[n - 1][j].Cost < minFinalCost)
            {
                minFinalCost = dp[n - 1][j].Cost;
                lastBest = j;
            }
        }

        if (lastBest == -1) return new();

        int currentIdx = lastBest;
        for (int i = n - 1; i >= 0; i--)
        {
            result.Add(states[i][currentIdx]);
            currentIdx = dp[i][currentIdx].PrevIndex;
        }

        result.Reverse();
        return result;
    }
}