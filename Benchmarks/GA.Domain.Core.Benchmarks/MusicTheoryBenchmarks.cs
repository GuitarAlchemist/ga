using BenchmarkDotNet.Attributes;
using GA.Business.ML.Embeddings;
using System.Numerics.Tensors;

namespace GA.Benchmarks;

[MemoryDiagnoser]
[SimpleJob]
public class MusicTheoryBenchmarks
{
    private float[] _vecA;
    private float[] _vecB;
    private const int Dim = 228;

    [GlobalSetup]
    public void Setup()
    {
        var random = new Random(42);
        _vecA = new float[Dim];
        _vecB = new float[Dim];
        
        for (int i = 0; i < Dim; i++)
        {
            _vecA[i] = (float)random.NextDouble();
            _vecB[i] = (float)random.NextDouble();
        }
    }

    [Benchmark]
    public double CosineSimilarity_TensorPrimitives()
    {
        return TensorPrimitives.CosineSimilarity(_vecA, _vecB);
    }

    [Benchmark]
    public double SpectralWeightedSimilarity_Tonal()
    {
        return SpectralRetrievalService.CalculateWeightedSimilarity(_vecA, _vecB, SpectralRetrievalService.SearchPreset.Tonal);
    }

    [Benchmark]
    public double SpectralWeightedSimilarity_Spectral()
    {
        return SpectralRetrievalService.CalculateWeightedSimilarity(_vecA, _vecB, SpectralRetrievalService.SearchPreset.Spectral);
    }
}
