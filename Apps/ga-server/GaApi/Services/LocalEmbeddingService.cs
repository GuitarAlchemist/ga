namespace GaApi.Services;

using Microsoft.DeepDev;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;

public class LocalEmbeddingService : IDisposable
{
    private const int _embeddingDimensions = 384;
    private const string _modelPath = "all-MiniLM-L6-v2.onnx";
    private const string _tokenizerPath = "tokenizer.json";
    private readonly InferenceSession? _session;
    private readonly ITokenizer? _tokenizer;

    public LocalEmbeddingService(ILogger<LocalEmbeddingService> logger)
    {
        try
        {
            if (File.Exists(_modelPath) && File.Exists(_tokenizerPath))
            {
                _session = new InferenceSession(_modelPath);
                _tokenizer = TokenizerBuilder.CreateByModelNameAsync("sentence-transformers/all-MiniLM-L6-v2")
                    .GetAwaiter().GetResult();
                logger.LogInformation("Local embedding model loaded successfully");
            }
            else
            {
                logger.LogWarning("Local embedding model files not found. Run LocalEmbedding tool first.");
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to load local embedding model");
        }
    }

    public bool IsAvailable => _session != null && _tokenizer != null;

    public void Dispose()
    {
        _session?.Dispose();

        GC.SuppressFinalize(this);
    }

    public float[] GenerateEmbedding(string text)
    {
        if (!IsAvailable)
        {
            throw new InvalidOperationException("Local embedding model not available");
        }

        // Tokenize
        var encoded = _tokenizer!.Encode(text, []);
        var inputIds = encoded.Select(id => (long)id).ToArray();
        var attentionMask = Enumerable.Repeat(1L, inputIds.Length).ToArray();

        // Create tensors
        var inputIdsTensor = new DenseTensor<long>(inputIds, new[] { 1, inputIds.Length });
        var attentionMaskTensor = new DenseTensor<long>(attentionMask, new[] { 1, attentionMask.Length });

        // Run inference
        var inputs = new List<NamedOnnxValue>
        {
            NamedOnnxValue.CreateFromTensor("input_ids", inputIdsTensor),
            NamedOnnxValue.CreateFromTensor("attention_mask", attentionMaskTensor)
        };

        using var results = _session!.Run(inputs);
        var output = results[0].AsEnumerable<float>().ToArray();

        // Mean pooling
        var embeddings = new float[_embeddingDimensions];
        var tokenCount = inputIds.Length;

        for (var i = 0; i < _embeddingDimensions; i++)
        {
            float sum = 0;
            for (var j = 0; j < tokenCount; j++)
            {
                sum += output[j * _embeddingDimensions + i];
            }

            embeddings[i] = sum / tokenCount;
        }

        // Normalize
        var norm = Math.Sqrt(embeddings.Sum(x => x * x));
        for (var i = 0; i < embeddings.Length; i++)
        {
            embeddings[i] /= (float)norm;
        }

        return embeddings;
    }
}
