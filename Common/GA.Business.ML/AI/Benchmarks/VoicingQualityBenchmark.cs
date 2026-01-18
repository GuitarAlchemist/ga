namespace GA.Business.ML.AI.Benchmarks;

using GA.Business.Core.AI.Benchmarks;
using GA.Data.MongoDB.Services;
using GA.Data.MongoDB.Models;
using MongoDB.Driver;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

public class VoicingQualityBenchmark(MongoDbService mongoDbService) : IBenchmark
{
    public string Name => "Voicing Quality";
    public string Description => "Validates voicing search results against ground truth and semantic expectations.";

    public async Task<BenchmarkResult> RunAsync()
    {
        var result = new BenchmarkResult
        {
            BenchmarkId = "voicing-quality",
            Name = Name,
            Timestamp = DateTime.UtcNow
        };

        var sw = Stopwatch.StartNew();
        
        // Define base cases (reused from BenchmarkQualityCommand)
        var groundTruth = GetGroundTruth();
        var collection = mongoDbService.Voicings;

        foreach (var (diagram, expectedName, desc) in groundTruth)
        {
            var step = new BenchmarkStep { Name = $"Identify {expectedName}", Input = diagram, Expected = expectedName };
            
            var entity = await collection.Find(v => v.Diagram == diagram).FirstOrDefaultAsync();
            if (entity == null)
            {
                step.Actual = "Not found";
                step.Score = 0;
                step.Passed = false;
                step.Notes = "Chord diagram not indexed.";
            }
            else
            {
                step.Actual = entity.ChordName ?? "Unknown";
                bool matches = step.Actual.Equals(expectedName, StringComparison.OrdinalIgnoreCase) ||
                              step.Actual.Replace(" Major", "").Replace(" Minor", "m").Equals(expectedName, StringComparison.OrdinalIgnoreCase);
                
                step.Score = matches ? 1.0 : 0.0;
                step.Passed = matches;
            }
            result.Steps.Add(step);
        }

        sw.Stop();
        result.Duration = sw.Elapsed;
        result.Score = result.Steps.Any() ? result.Steps.Average(s => s.Score) : 0;
        
        return result;
    }

    private List<(string Diagram, string ExpectedName, string Description)> GetGroundTruth()
    {
        return new List<(string, string, string)>
        {
            ("x-3-2-0-1-0", "C", "Standard open C major"),
            ("x-0-2-2-2-0", "A", "Standard open A major"),
            ("x-0-2-2-1-0", "Am", "Standard open A minor"),
            ("0-2-2-1-0-0", "E", "Standard open E major"),
            ("0-2-2-0-0-0", "Em", "Standard open E minor"),
            ("3-2-0-0-0-3", "G", "Standard open G major"),
            ("x-x-0-2-3-2", "D", "Standard open D major"),
            ("1-3-3-2-1-1", "F", "F barre chord"),
            ("0-7-6-7-8-0", "E7#9", "Hendrix chord"),
        };
    }
}
