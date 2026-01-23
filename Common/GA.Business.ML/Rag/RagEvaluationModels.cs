namespace GA.Business.ML.Rag;

using System;
using System.Collections.Generic;

/// <summary>
/// Metric results for a single RAG query evaluation.
/// </summary>
public record RagQueryEvaluation(
    string Query,
    KnowledgeType[] TargetPartitions,
    int TotalResults,
    float AverageScore,
    TimeSpan Latency,
    IReadOnlyList<RagResult> Results);

/// <summary>
/// Aggregated benchmark results for a RAG performance run.
/// </summary>
public record RagBenchmarkResult(
    string Name,
    int TotalQueries,
    TimeSpan TotalTime,
    float AverageLatencyMs,
    float AverageResultsPerQuery,
    IReadOnlyDictionary<KnowledgeType, int> PartitionDistribution);

/// <summary>
/// A gold-standard test case for RAG evaluation.
/// </summary>
public record RagTestCase(
    string Query,
    KnowledgeType[] ExpectedPartitions,
    IReadOnlyList<string> ExpectedKeywords);
