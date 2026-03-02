namespace GA.Business.ML.Tabs;

using Domain.Core.Primitives.Notes;
using Abstractions;
using GA.Business.ML.Rag.Models;
using Domain.Services.Fretboard.Analysis;
using Retrieval;

/// <summary>
///     Modern Style-Aware Tab Realization Solver.
///     Uses OPTIC-K Embeddings and Hand-Position modeling to generate idiomatic tablature.
/// </summary>
public class AdvancedTabSolver(
    FretboardPositionMapper mapper,
    PhysicalCostService costService,
    StyleProfileService styleService,
    IEmbeddingGenerator generator,
    IMlNaturalnessRanker naturalnessRanker)
{
    /// <summary>
    ///     Higher-level entry point that returns a formatted solution.
    /// </summary>
    public async Task<TabSolution> SolveOptimalPathAsync(IEnumerable<ChordVoicingRagDocument> documents)
    {
        var pitches = documents.Select(d => d.MidiNotes.Select(m => Pitch.FromMidiNote(m))).ToList();
        var allPaths = await SolveAsync(pitches, k: 1);

        if (allPaths.Count == 0)
        {
            return new("", 0);
        }

        var bestPath = allPaths[0];

        var renderer = new TabRenderer(); // Use existing renderer if available
        var content = renderer.Render(bestPath);

        // Calculate final cost
        double totalCost = 0;
        for (var i = 0; i < bestPath.Count - 1; i++)
        {
            totalCost += costService.CalculateTransitionCost(bestPath[i], bestPath[i + 1]);
        }

        return new(content, totalCost);
    }

    /// <summary>
    ///     Solves for the optimal sequence using stylistic bias and hand-position modeling.
    ///     Returns Top-K paths.
    /// </summary>
    public virtual async Task<List<List<List<FretboardPosition>>>> SolveAsync(
        IEnumerable<IEnumerable<Pitch>> score,
        string styleTag = "Jazz",
        int k = 1)
    {
        var steps = score.ToList();
        if (steps.Count == 0)
        {
            return [];
        }

        // 1. Get Style Centroid
        var styleCentroid = styleService.GetStyleCentroid(styleTag);

        // 2. Pre-calculate all candidate embeddings 
        var stepStates = new List<List<CandidateState>>();

        foreach (var pitches in steps)
        {
            var realizations = mapper.MapChord(pitches).ToList();
            var candidates = new List<CandidateState>();

            foreach (var r in realizations)
            {
                // Pruning: fast static physical cost check before expensive embedding.
                // We'll calculate it fully later, but we can do a quick check if needed.
                var staticPhys = costService.CalculateStaticCost(r).TotalCost;
                
                // Keep only reasonable shapes before generating embeddings to save time
                if (staticPhys > 200.0) continue; 

                var doc = CreateTempDoc(r);
                var emb = await generator.GenerateEmbeddingAsync(doc);

                var centroidNaturalness = styleCentroid != null
                    ? styleService.CalculateNaturalness(emb, styleCentroid)
                    : 1.0;

                var naturalness = centroidNaturalness;

                candidates.Add(new(r, naturalness, staticPhys));
            }

            // Pruning: Keep only top 40 candidates per step to prevent combinatorial explosion
            var prunedCandidates = candidates
                .OrderBy(c => c.StaticCost + (1.0 - c.Naturalness) * 10.0)
                .Take(40)
                .ToList();

            if (prunedCandidates.Count == 0 && candidates.Count > 0)
            {
                // Fallback if all were pruned
                prunedCandidates = [.. candidates.Take(40)];
            }

            stepStates.Add(prunedCandidates);
        }

        return PerformAdvancedViterbi(stepStates, k);
    }

    private List<List<List<FretboardPosition>>> PerformAdvancedViterbi(List<List<CandidateState>> states, int k)
    {
        var n = states.Count;
        // dp[step][stateIndex] -> List of Top-K (Cost, PrevStateIndex, PrevRank)
        var dp = new List<(double Cost, int PrevStateIndex, int PrevRank)>[n][];
        
        // Memoization cache for transition costs
        var transitionCache = new Dictionary<(CandidateState, CandidateState), double>();

        // 1. Initialize first step
        dp[0] = new List<(double Cost, int PrevStateIndex, int PrevRank)>[states[0].Count];
        for (var j = 0; j < states[0].Count; j++)
        {
            var state = states[0][j];
            var physical = state.StaticCost;

            // Modern Addition: Style Bias
            var stylePenalty = (1.0 - state.Naturalness) * 10.0;

            // Initial states have no previous state (-1, -1)
            dp[0][j] = [(physical + stylePenalty, -1, -1)];
        }

        // 2. Fill DP table
        for (var i = 1; i < n; i++)
        {
            dp[i] = new List<(double, int, int)>[states[i].Count];
            for (var curr = 0; curr < states[i].Count; curr++)
            {
                var candidates = new List<(double TotalCost, int PrevState, int PrevRank)>();

                var currState = states[i][curr];
                var currStaticPhys = currState.StaticCost;
                var stylePenalty = (1.0 - currState.Naturalness) * 10.0;

                // Iterate over all previous states
                for (var prev = 0; prev < states[i - 1].Count; prev++)
                {
                    var prevState = states[i - 1][prev];
                    var prevPaths = dp[i - 1][prev];

                    // Calculate or get memoized transition cost
                    if (!transitionCache.TryGetValue((prevState, currState), out var transCost))
                    {
                        transCost = costService.CalculateTransitionCost(prevState.Shape, currState.Shape);
                        transitionCache[(prevState, currState)] = transCost;
                    }

                    for (var rank = 0; rank < prevPaths.Count; rank++)
                    {
                        var (prevCost, _, _) = prevPaths[rank];

                        // --- POSITION INERTIA (Modern addition) ---
                        var pos1 = GetHandPosition(prevState.Shape);
                        var pos2 = GetHandPosition(currState.Shape);
                        var inertia = Math.Abs(pos1 - pos2) > 2 ? 2.0 : 0.0;

                        // --- ML NATURALNESS (Integration addition) ---
                        var mlNaturalness = naturalnessRanker.PredictNaturalness(prevState.Shape, currState.Shape);
                        
                        // We use a small penalty modifier from naturalness to break ties, 
                        // but not overwhelm pure physical transition cost
                        var mlPenalty = (1.0 - mlNaturalness) * 2.0;

                        var total = prevCost + transCost + currStaticPhys + stylePenalty + inertia + mlPenalty;
                        candidates.Add((total, prev, rank));
                    }
                }

                // Keep only Top-K best paths for this state
                dp[i][curr] = [.. candidates.OrderBy(x => x.TotalCost).Take(k)];
            }
        }

        // 3. Backtrack K Best Paths
        return Backtrack(states, dp, k);
    }

    private List<List<List<FretboardPosition>>> Backtrack(
        List<List<CandidateState>> states,
        List<(double Cost, int PrevStateIndex, int PrevRank)>[][] dp,
        int k)
    {
        var n = states.Count;
        var result = new List<List<List<FretboardPosition>>>();

        // Collect all final states from the last step
        var finalCandidates = new List<(double Cost, int StateIndex, int Rank)>();
        for (var j = 0; j < states[n - 1].Count; j++)
        {
            var paths = dp[n - 1][j];
            for (var r = 0; r < paths.Count; r++)
            {
                finalCandidates.Add((paths[r].Cost, j, r));
            }
        }

        // Take global Top-K best final states
        var bestFinals = finalCandidates.OrderBy(x => x.Cost).Take(k).ToList();

        foreach (var (_, lastStateIdx, lastRank) in bestFinals)
        {
            var currentPath = new List<List<FretboardPosition>>();
            var currentStep = n - 1;
            var currentStateIdx = lastStateIdx;
            var currentRank = lastRank;

            while (currentStep >= 0)
            {
                currentPath.Add(states[currentStep][currentStateIdx].Shape);

                var node = dp[currentStep][currentStateIdx][currentRank];
                if (node.PrevStateIndex == -1)
                {
                    break; // Start of path
                }

                currentStateIdx = node.PrevStateIndex;
                currentRank = node.PrevRank;
                currentStep--;
            }

            currentPath.Reverse();
            result.Add(currentPath);
        }

        return result;
    }

    private int GetHandPosition(List<FretboardPosition> shape)
    {
        // Simplification: index finger usually at min fret (if not open)
        var nonZero = shape.Where(p => p.Fret > 0).Select(p => p.Fret).ToList();
        return nonZero.Count > 0 ? nonZero.Min() : 0;
    }

    private ChordVoicingRagDocument CreateTempDoc(List<FretboardPosition> shape)
    {
        var pcs = shape.Select(p => p.Pitch.PitchClass.Value).ToArray();
        var midi = shape.Select(p => p.Pitch.MidiNote.Value).ToArray();
        var nonZeroFrets = shape.Where(p => p.Fret > 0).Select(p => p.Fret).ToList();

        var minFret = nonZeroFrets.Count > 0 ? nonZeroFrets.Min() : 0;
        var maxFret = nonZeroFrets.Count > 0 ? nonZeroFrets.Max() : 0;

        // Use physical distance for normalization if needed, or keep raw for internal doc
        var stretch = nonZeroFrets.Count > 1 ? maxFret - minFret : 0;

        return new ChordVoicingRagDocument
        {
            Id = "temp",
            ChordName = "Temp",
            PitchClasses = pcs,
            MidiNotes = midi,
            SearchableText = "temp",
            RootPitchClass = pcs[0],
            SemanticTags = [],

            // Physical Properties
            MinFret = minFret,
            MaxFret = maxFret,
            HandStretch = stretch, // We'll keep raw fret count for the document field but use physical for analysis
            BarreRequired = shape.GroupBy(p => p.Fret).Any(g => g.Key > 0 && g.Count() >= 3),

            // Required placeholders
            PossibleKeys = [], YamlAnalysis = "{}", PitchClassSet = string.Join(",", pcs),
            IntervalClassVector = "000000", AnalysisEngine = "Temp", AnalysisVersion = "1.0", Jobs = [],
            TuningId = "Standard", PitchClassSetId = "0", Diagram = ""
        };
    }

    public record TabSolution(string TabContent, double TotalPhysicalCost);

    private class CandidateState(List<FretboardPosition> shape, double naturalness, double staticCost)
    {
        public List<FretboardPosition> Shape { get; } = shape;
        public double Naturalness { get; } = naturalness;
        public double StaticCost { get; } = staticCost;
    }
}
