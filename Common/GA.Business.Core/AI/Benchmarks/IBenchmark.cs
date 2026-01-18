namespace GA.Business.Core.AI.Benchmarks;

using System.Threading.Tasks;

public interface IBenchmark
{
    string Name { get; }
    string Description { get; }
    Task<BenchmarkResult> RunAsync();
}
