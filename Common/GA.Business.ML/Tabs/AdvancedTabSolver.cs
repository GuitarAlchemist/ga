namespace GA.Business.ML.Tabs;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Core.Fretboard.Analysis;
using Core.Notes;
using Embeddings;
using Retrieval;

using Core.Player;

/// <summary>
/// Modern Style-Aware Tab Realization Solver.
/// Uses OPTIC-K Embeddings and Hand-Position modeling to generate idiomatic tablature.
/// </summary>
public class AdvancedTabSolver
{
    private readonly FretboardPositionMapper _mapper;
    private readonly PhysicalCostService _costService;
    private readonly StyleProfileService _styleService;
    private readonly IEmbeddingGenerator _generator;
    private readonly PlayerProfile _playerProfile;

    public AdvancedTabSolver(
        FretboardPositionMapper mapper,
        PhysicalCostService costService,
        StyleProfileService styleService,
        IEmbeddingGenerator generator,
        PlayerProfile? playerProfile = null)
    {
        _mapper = mapper;
        _costService = costService;
        _styleService = styleService;
        _generator = generator;
        _playerProfile = playerProfile ?? new PlayerProfile();
    }

    public record TabSolution(string TabContent, double TotalPhysicalCost);

    /// <summary>
    /// Higher-level entry point that returns a formatted solution.
    /// </summary>
    public async Task<TabSolution> SolveOptimalPathAsync(IEnumerable<VoicingDocument> documents)
    {
        var pitches = documents.Select(d => d.MidiNotes.Select(m => Pitch.FromMidiNote(m))).ToList();
        var allPaths = await SolveAsync(pitches, k: 1);
        
        if (allPaths.Count == 0) return new TabSolution("", 0);
        
        var bestPath = allPaths[0];
        
        var renderer = new TabRenderer(); // Use existing renderer if available
        string content = renderer.Render(bestPath);
        
        // Calculate final cost
        double totalCost = 0;
        for (int i = 0; i < bestPath.Count - 1; i++)
            totalCost += _costService.CalculateTransitionCost(bestPath[i], bestPath[i+1]);

        return new TabSolution(content, totalCost);
    }

    /// <summary>
    /// Solves for the optimal sequence using stylistic bias and hand-position modeling.
    /// Returns Top-K paths.
    /// </summary>
    public virtual async Task<List<List<List<FretboardPosition>>>> SolveAsync(
        IEnumerable<IEnumerable<Pitch>> score, 
        string styleTag = "Jazz",
        int k = 1)
    {
        var steps = score.ToList();
        if (steps.Count == 0) return new();

        // 1. Get Style Centroid
        var styleCentroid = _styleService.GetStyleCentroid(styleTag);

        // 2. Pre-calculate all candidate embeddings 
        var stepStates = new List<List<CandidateState>>();
        
        foreach (var pitches in steps)
        {
            var realizations = _mapper.MapChord(pitches).ToList();
            var candidates = new List<CandidateState>();

            foreach (var r in realizations)
            {
                var doc = CreateTempDoc(r);
                var emb = await _generator.GenerateEmbeddingAsync(doc);
                doc = doc with { Embedding = emb }; // Ensure ranker sees it
                
                double centroidNaturalness = styleCentroid != null 
                    ? _styleService.CalculateNaturalness(emb, styleCentroid)
                    : 1.0;

                // Note: Transition-based ML naturalness is now applied in PhysicalCostService.CalculateTransitionCost
                double naturalness = centroidNaturalness;

                candidates.Add(new CandidateState(r, emb, naturalness));
            }
            stepStates.Add(candidates);
        }

        return PerformAdvancedViterbi(stepStates, k);
    }

    private List<List<List<FretboardPosition>>> PerformAdvancedViterbi(List<List<CandidateState>> states, int k)
    {
        int n = states.Count;
        // dp[step][stateIndex] -> List of Top-K (Cost, PrevStateIndex, PrevRank)
        var dp = new List<(double Cost, int PrevStateIndex, int PrevRank)>[n][];

        // 1. Initialize first step
        dp[0] = new List<(double Cost, int PrevStateIndex, int PrevRank)>[states[0].Count];
        for (int j = 0; j < states[0].Count; j++)
        {
            var state = states[0][j];
            var physical = _costService.CalculateStaticCost(state.Shape).TotalCost;
            
            // Modern Addition: Style Bias
            double stylePenalty = (1.0 - state.Naturalness) * 10.0; 
            
            // Initial states have no previous state (-1, -1)
            dp[0][j] = new List<(double, int, int)> { (physical + stylePenalty, -1, -1) };
        }

        // 2. Fill DP table
        for (int i = 1; i < n; i++)
        {
            dp[i] = new List<(double, int, int)>[states[i].Count];
            for (int curr = 0; curr < states[i].Count; curr++)
            {
                var candidates = new List<(double TotalCost, int PrevState, int PrevRank)>();

                var currState = states[i][curr];
                var currStaticPhys = _costService.CalculateStaticCost(currState.Shape).TotalCost;
                double stylePenalty = (1.0 - currState.Naturalness) * 10.0;

                // Iterate over all previous states
                for (int prev = 0; prev < states[i - 1].Count; prev++)
                {
                    // Iterate over all K best paths reaching that previous state
                    var prevPaths = dp[i - 1][prev];
                    if (prevPaths == null) continue;

                    for (int rank = 0; rank < prevPaths.Count; rank++)
                    {
                        var (prevCost, _, _) = prevPaths[rank];
                        var prevStateShape = states[i - 1][prev].Shape;

                        // Physical Transition Cost
                        var transCost = _costService.CalculateTransitionCost(prevStateShape, currState.Shape);
                        
                        // --- POSITION INERTIA (Modern addition) ---
                        int pos1 = GetHandPosition(prevStateShape);
                        int pos2 = GetHandPosition(currState.Shape);
                        double inertia = Math.Abs(pos1 - pos2) > 2 ? 2.0 : 0.0;

                        double total = prevCost + transCost + currStaticPhys + stylePenalty + inertia;
                        candidates.Add((total, prev, rank));
                    }
                }

                // Keep only Top-K best paths for this state
                dp[i][curr] = candidates.OrderBy(x => x.TotalCost).Take(k).ToList();
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
        int n = states.Count;
        var result = new List<List<List<FretboardPosition>>>();

        // Collect all final states from the last step
        var finalCandidates = new List<(double Cost, int StateIndex, int Rank)>();
        for (int j = 0; j < states[n - 1].Count; j++)
        {
            var paths = dp[n - 1][j];
            if (paths != null)
            {
                for (int r = 0; r < paths.Count; r++)
                {
                    finalCandidates.Add((paths[r].Cost, j, r));
                }
            }
        }

        // Take global Top-K best final states
        var bestFinals = finalCandidates.OrderBy(x => x.Cost).Take(k).ToList();

        foreach (var (finalCost, lastStateIdx, lastRank) in bestFinals)
        {
            var path = new List<FretboardPosition[]>(); // Use array for easier reconstruction? Keeping List for compatibility logic.
            // Actually existing logic uses List<List<FretboardPosition>> which is List<Chord>. 
            // My Backtrack return type is List<List<List<FretboardPosition>>> -> List<Path> -> Path=List<Chord>.
            
            var currentPath = new List<List<FretboardPosition>>();
            int currentStep = n - 1;
            int currentStateIdx = lastStateIdx;
            int currentRank = lastRank;

            while (currentStep >= 0)
            {
                currentPath.Add(states[currentStep][currentStateIdx].Shape);
                
                var node = dp[currentStep][currentStateIdx][currentRank];
                if (node.PrevStateIndex == -1) break; // Start of path

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

    private VoicingDocument CreateTempDoc(List<FretboardPosition> shape)
    {
        var pcs = shape.Select(p => p.Pitch.PitchClass.Value).ToArray();
        var midi = shape.Select(p => p.Pitch.MidiNote.Value).ToArray();
        var nonZeroFrets = shape.Where(p => p.Fret > 0).Select(p => p.Fret).ToList();
        
        int minFret = nonZeroFrets.Count > 0 ? nonZeroFrets.Min() : 0;
        int maxFret = nonZeroFrets.Count > 0 ? nonZeroFrets.Max() : 0;
        
        // Use physical distance for normalization if needed, or keep raw for internal doc
        int stretch = nonZeroFrets.Count > 1 ? maxFret - minFret : 0;
        double physicalStretch = FretboardGeometry.CalculatePhysicalSpan(nonZeroFrets);

        return new VoicingDocument
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
            PossibleKeys = [], YamlAnalysis = "{}", PitchClassSet = string.Join(",", pcs), IntervalClassVector = "000000", AnalysisEngine = "Temp", AnalysisVersion = "1.0", Jobs = [], TuningId = "Standard", PitchClassSetId = "0", Diagram = ""
        };
    }

    private record CandidateState(List<FretboardPosition> Shape, double[] Embedding, double Naturalness);
}
