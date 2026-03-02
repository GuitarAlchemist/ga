namespace GA.Business.ML.Rag;

using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

/// <summary>
/// Service for benchmarking and evaluating the performance of the Partitioned RAG framework.
/// </summary>
public class RagEvaluationService(
    IPartitionedRagService ragService,
    ILogger<RagEvaluationService> logger)
{
    private readonly IPartitionedRagService _ragService = ragService;
    private readonly ILogger<RagEvaluationService> _logger = logger;

    /// <summary>
    /// Executes a benchmark run against a set of test cases.
    /// </summary>
    public async Task<RagBenchmarkResult> RunBenchmarkAsync(string name, IEnumerable<RagTestCase> testCases)
    {
        _logger.LogInformation("Starting RAG Benchmark: {Name}", name);
        
        var evaluations = new List<RagQueryEvaluation>();
        var stopwatch = Stopwatch.StartNew();

        foreach (var testCase in testCases)
        {
            var evalStopwatch = Stopwatch.StartNew();
            var response = await _ragService.QueryAsync(testCase.Query, testCase.ExpectedPartitions);
            evalStopwatch.Stop();

            var evaluation = new RagQueryEvaluation(
                testCase.Query,
                testCase.ExpectedPartitions,
                response.Results.Count,
                response.Results.Any() ? response.Results.Average(r => r.Score) : 0f,
                evalStopwatch.Elapsed,
                response.Results);
            
            evaluations.Add(evaluation);
        }

        stopwatch.Stop();

        var totalQueries = evaluations.Count;
        var avgLatency = evaluations.Any() ? (float)evaluations.Average(e => e.Latency.TotalMilliseconds) : 0f;
        var avgResults = evaluations.Any() ? (float)evaluations.Average(e => e.TotalResults) : 0f;
        
        var partitionDist = evaluations.SelectMany(e => e.Results)
            .GroupBy(r => r.Type)
            .ToDictionary(g => g.Key, g => g.Count());

        var result = new RagBenchmarkResult(
            name,
            totalQueries,
            stopwatch.Elapsed,
            avgLatency,
            avgResults,
            partitionDist);

        _logger.LogInformation("Benchmark {Name} complete. Avg Latency: {Latency:F2}ms, Avg Results: {Results:F1}", 
            name, avgLatency, avgResults);

        return result;
    }

    /// <summary>
    /// Validates the quality of a specific query based on expected keywords in results.
    /// </summary>
    public float CalculateRetrievalQuality(RagQueryEvaluation evaluation, IEnumerable<string> expectedKeywords)
    {
        if (!evaluation.Results.Any()) return 0f;

        var keywordList = expectedKeywords.ToList();
        if (!keywordList.Any()) return 1.0f;

        var matches = 0;
        var combinedText = string.Join(" ", evaluation.Results.Select(r => r.Content)).ToLowerInvariant();

        foreach (var keyword in keywordList)
        {
            if (combinedText.Contains(keyword.ToLowerInvariant()))
            {
                matches++;
            }
        }

        return (float)matches / keywordList.Count;
    }
}
