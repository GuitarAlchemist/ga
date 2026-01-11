namespace GA.Testing.Semantic;

using GA.Business.Core.AI.Services.Embeddings;
using NUnit.Framework;
using System.Collections.Concurrent;
using System.IO;
using System.Security.Cryptography;

/// <summary>
/// Level 0 Semantic Assertions.
/// Optimized for speed using cached local embeddings.
/// </summary>
public static class AssertAi
{
    private static IEmbeddingService? _embeddingService;
    private static IJudgeService? _judgeService;
    private static readonly ConcurrentDictionary<string, float[]> EmbeddingCache = new();

    // Persistent cache directory
    private static readonly string CachePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".gemini", "ga_semantic_cache");

    /// <summary>
    /// Configures the global embedding service for AI assertions.
    /// Recommended: Use a local ONNX-based service for Level 0 speed.
    /// </summary>
    public static void Configure(IEmbeddingService service)
    {
        _embeddingService = service;
        EmbeddingCache.Clear();
        if (Directory.Exists(CachePath))
        {
            foreach (var file in Directory.GetFiles(CachePath))
            {
                try { File.Delete(file); } catch { /* ignore */ }
            }
        }
        else
        {
            Directory.CreateDirectory(CachePath);
        }
    }

    /// <summary>
    /// Configures the optional judge service for reasoning/rational tests (Level 1).
    /// </summary>
    public static void ConfigureJudge(IJudgeService service)
    {
        _judgeService = service;
    }

    /// <summary>
    /// Automatically configures the Judge service if environment variables are set.
    /// GA_OLLAMA_BASE_URL (e.g. http://localhost:11434)
    /// GA_OLLAMA_MODEL (e.g. mistral)
    /// </summary>
    public static void AutoConfigureJudge()
    {
        var baseUrl = Environment.GetEnvironmentVariable("GA_OLLAMA_BASE_URL");
        if (!string.IsNullOrWhiteSpace(baseUrl))
        {
            var model = Environment.GetEnvironmentVariable("GA_OLLAMA_MODEL") ?? "mistral";
            var client = new HttpClient { BaseAddress = new Uri(baseUrl) };
            _judgeService = new OllamaJudgeService(client, model);
        }
    }

    public static class Judges
    {
        /// <summary>
        /// Expert Reasoning Assertion (Level 1).
        /// Asks a Judge (LLM) to evaluate if the text passes a specific qualitative rubric.
        /// </summary>
        public static void PassesRubric(string text, string rubric)
        {
            if (_judgeService == null)
            {
                throw new InvalidOperationException("Judge service is not configured. Call AssertAi.ConfigureJudge(service).");
            }

            var result = _judgeService.EvaluateAsync(text, "Evaluate the following guitar instruction response against the persona rubric.", rubric).GetAwaiter().GetResult();

            Assert.That(result.IsPassing, Is.True,
                $"Judge Evaluation FAILED!\n" +
                $"Rationale: {result.Rationale}\n" +
                $"Confidence: {result.Confidence:P}\n" +
                $"Text: \"{text}\"");
        }

        /// <summary>
        /// Returns a numeric score (0.0 - 1.0) for how well text matches a rubric.
        /// Useful for Level 2 Directional tests.
        /// </summary>
        public static double GetRubricScore(string text, string rubric)
        {
            if (_judgeService == null) return 0;
            var result = _judgeService.EvaluateAsync(text, "Score the following text against the rubric.", rubric).GetAwaiter().GetResult();
            return result.IsPassing ? 1.0 : 0.0; // Simple for now, can be improved if IJudgeService returns scores
        }
    }

    /// <summary>
    /// Asserts that two pieces of text are semantically similar.
    /// Hits the cache first, then the embedding service.
    /// </summary>
    public static void Similar(string actual, string expected, double threshold = 0.85)
    {
        var actualVector = GetEmbedding(actual);
        var expectedVector = GetEmbedding(expected);

        var similarity = CosineSimilarity(actualVector, expectedVector);

        Assert.That(similarity, Is.GreaterThanOrEqualTo(threshold),
            $"Semantic Drift Detected!\nExpected meaning: \"{expected}\"\nActual output: \"{actual}\"\nSimilarity: {similarity:P2} (Threshold: {threshold:P2})");
    }

    /// <summary>
    /// Level 0 Property: Concept Probe.
    /// Asserts that the text "resides" within the semantic basin of a specific concept.
    /// </summary>
    public static void InBasin(string text, string concept, double threshold = 0.70)
    {
        var textVector = GetEmbedding(text);
        var conceptVector = GetEmbedding(concept);

        var similarity = CosineSimilarity(textVector, conceptVector);

        Assert.That(similarity, Is.GreaterThanOrEqualTo(threshold),
            $"Concept Probe Failed!\nText: \"{text}\"\nDid not sufficiently resonate with concept: \"{concept}\"\nSimilarity: {similarity:P2}");
    }

    /// <summary>
    /// Asserts that the text does NOT contain a specific concept (e.g., PII, Safety, or unwanted tone).
    /// </summary>
    public static void NotInBasin(string text, string forbiddenConcept, double threshold = 0.60)
    {
        var textVector = GetEmbedding(text);
        var conceptVector = GetEmbedding(forbiddenConcept);

        var similarity = CosineSimilarity(textVector, conceptVector);

        Assert.That(similarity, Is.LessThan(threshold),
            $"Safety/Constraint Failure!\nText: \"{text}\"\nResonated too strongly with forbidden concept: \"{forbiddenConcept}\"\nSimilarity: {similarity:P2}");
    }

    /// <summary>
    /// Returns the raw similarity score between two pieces of text.
    /// Useful for Level 2 Directional/Monotonicity tests.
    /// </summary>
    public static double GetSimilarity(string text, string concept)
    {
        var textVector = GetEmbedding(text);
        var conceptVector = GetEmbedding(concept);
        return CosineSimilarity(textVector, conceptVector);
    }

    private static float[] GetEmbedding(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return Array.Empty<float>();

        if (EmbeddingCache.TryGetValue(text, out var cached)) return cached;

        // Try file cache
        var hash = GetSha256(text);
        var filePath = Path.Combine(CachePath, $"{hash}.json");
        if (File.Exists(filePath))
        {
            var json = File.ReadAllText(filePath);
            var vector = System.Text.Json.JsonSerializer.Deserialize<float[]>(json);
            if (vector != null)
            {
                EmbeddingCache.TryAdd(text, vector);
                return vector;
            }
        }

        if (_embeddingService == null)
        {
            throw new InvalidOperationException("AssertAi is not configured. Call AssertAi.Configure(service) in your GlobalSetup.");
        }

        // Generate and store
        var generated = _embeddingService.GenerateEmbeddingAsync(text).GetAwaiter().GetResult();
        EmbeddingCache.TryAdd(text, generated);

        File.WriteAllText(filePath, System.Text.Json.JsonSerializer.Serialize(generated));

        return generated;
    }

    private static string GetSha256(string input)
    {
        using var sha256Hash = System.Security.Cryptography.SHA256.Create();
        byte[] bytes = sha256Hash.ComputeHash(System.Text.Encoding.UTF8.GetBytes(input));
        return BitConverter.ToString(bytes).Replace("-", "").ToLowerInvariant();
    }

    private static double CosineSimilarity(float[] v1, float[] v2)
    {
        if (v1.Length == 0 || v2.Length == 0 || v1.Length != v2.Length) return 0;

        double dotProduct = 0;
        double l2Actual = 0;
        double l2Expected = 0;

        for (int i = 0; i < v1.Length; i++)
        {
            dotProduct += v1[i] * v2[i];
            l2Actual += v1[i] * v1[i];
            l2Expected += v2[i] * v2[i];
        }

        double denominator = Math.Sqrt(l2Actual) * Math.Sqrt(l2Expected);
        return denominator <= 0 ? 0 : dotProduct / denominator;
    }
}
