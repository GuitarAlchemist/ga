namespace GA.Business.ML.AI.Benchmarks;

using GA.Business.Core.AI.Benchmarks;
using System.Collections.Generic;
using System.Threading.Tasks;

public class BenchmarkRunner(IEnumerable<IBenchmark> benchmarks)
{
    private readonly Dictionary<string, BenchmarkResult> _cache = [];

    public async Task<List<BenchmarkResult>> RunAllAsync()
    {
        var results = new List<BenchmarkResult>();
        foreach (var benchmark in benchmarks)
        {
            try 
            {
                var result = await benchmark.RunAsync();
                _cache[benchmark.Name] = result;
                results.Add(result);
            }
            catch (Exception ex)
            {
                results.Add(new BenchmarkResult 
                { 
                    Name = benchmark.Name, // Assuming we can get name from benchmark interface
                    Score = 0,
                    Timestamp = DateTime.UtcNow,
                    RawOutput = $"Error: {ex.Message}"
                });
            }
        }
        return results;
    }

    public async Task<BenchmarkResult?> GetByNameAsync(string name, bool runIfMissing = true)
    {
        if (_cache.TryGetValue(name, out var cachedResult))
        {
            return cachedResult;
        }

        if (!runIfMissing) return null;

        foreach (var benchmark in benchmarks)
        {
            if (benchmark.Name.Equals(name, StringComparison.OrdinalIgnoreCase))
            {
                var result = await benchmark.RunAsync();
                _cache[benchmark.Name] = result;
                return result;
            }
        }
        return null;
    }

    public async Task<BenchmarkResult?> RunByNameAsync(string name)
    {
        foreach (var benchmark in benchmarks)
        {
            if (benchmark.Name.Equals(name, StringComparison.OrdinalIgnoreCase))
            {
                var result = await benchmark.RunAsync();
                _cache[benchmark.Name] = result;
                return result;
            }
        }
        return null;
    }

    public void ReportResult(BenchmarkResult result)
    {
        _cache[result.Name] = result;
    }
}
