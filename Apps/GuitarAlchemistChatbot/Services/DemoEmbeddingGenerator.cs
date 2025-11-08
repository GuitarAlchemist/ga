namespace GuitarAlchemistChatbot.Services;

/// <summary>
///     Demo embedding generator that provides mock embeddings when OpenAI API is not available
/// </summary>
public class DemoEmbeddingGenerator : IEmbeddingGenerator<string, Embedding<float>>
{
    private readonly Random _random = new(42); // Fixed seed for consistent results

    public EmbeddingGeneratorMetadata Metadata => new("Demo Embedding Generator", new Uri("https://localhost"), "demo");

    public async Task<GeneratedEmbeddings<Embedding<float>>> GenerateAsync(
        IEnumerable<string> values,
        EmbeddingGenerationOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        await Task.Delay(100, cancellationToken); // Simulate processing time

        var embeddings = new List<Embedding<float>>();

        foreach (var value in values)
        {
            // Generate a mock embedding based on the input string
            var embedding = GenerateMockEmbedding(value);
            embeddings.Add(embedding);
        }

        return new GeneratedEmbeddings<Embedding<float>>(embeddings)
        {
            Usage = new UsageDetails
            {
                InputTokenCount = values.Sum(v => v.Length / 4), // Rough token estimate
                TotalTokenCount = values.Sum(v => v.Length / 4)
            }
        };
    }

    public void Dispose()
    {
        // Nothing to dispose in demo mode
    }

    public object? GetService(Type serviceType, object? serviceKey = null)
    {
        return null;
    }

    public TService? GetService<TService>(object? serviceKey = null)
    {
        return default;
    }

    private Embedding<float> GenerateMockEmbedding(string input)
    {
        // Create a deterministic but varied embedding based on the input
        var hash = input.GetHashCode();
        var localRandom = new Random(hash);

        // Generate a 1536-dimensional embedding (same as text-embedding-3-small)
        var vector = new float[1536];
        for (var i = 0; i < vector.Length; i++)
        {
            vector[i] = (float)(localRandom.NextDouble() * 2.0 - 1.0); // Range [-1, 1]
        }

        // Normalize the vector
        var magnitude = Math.Sqrt(vector.Sum(x => x * x));
        for (var i = 0; i < vector.Length; i++)
        {
            vector[i] = (float)(vector[i] / magnitude);
        }

        return new Embedding<float>(vector);
    }
}
