namespace GA.Business.ML.Agents.Intents;

using System.Text.Json;
using System.Text.Json.Serialization;

/// <summary>
/// SHADOW evaluator for the Hermes Spike-A learned router head. Loads
/// <c>learned-head.json</c> (an L2-regularized softmax classifier over the SAME
/// nomic-embed query vector <see cref="SemanticIntentRouter"/> already computes)
/// and, when enabled, logs the head's routing decision ALONGSIDE production's —
/// WITHOUT ever changing the routing result.
/// <para>
/// Default OFF. Active only when <c>GA_ROUTER_SHADOW=1</c> AND
/// <c>GA_LEARNED_HEAD_PATH</c> points at a readable head file. Any load/eval
/// failure disables it (no-op), so the production routing path is never at risk.
/// </para>
/// <para>
/// Apply contract (must match training): the input vector is the embedding of
/// the lowercase+trimmed query; the head L2-normalizes it, computes
/// <c>logits[c] = Σ_f x[f]·W[f][c] + b[c]</c>, softmaxes, and picks argmax if the
/// winner's probability ≥ <c>tau</c> else declines. PCA heads are not supported
/// in shadow (disabled if <c>pca</c> is present).
/// </para>
/// </summary>
public sealed class LearnedHeadShadow
{
    private static readonly Lazy<LearnedHeadShadow?> LazyInstance = new(TryLoad);

    /// <summary>The shadow evaluator, or <c>null</c> when disabled / unloadable.</summary>
    public static LearnedHeadShadow? Instance =>
        Environment.GetEnvironmentVariable("GA_ROUTER_SHADOW") == "1" ? LazyInstance.Value : null;

    private readonly string[] _labels;
    private readonly float[][] _w; // [feature][class]
    private readonly float[] _b;
    private readonly double _tau;
    private readonly int _dim;

    private LearnedHeadShadow(string[] labels, float[][] w, float[] b, double tau, int dim)
    {
        _labels = labels;
        _w = w;
        _b = b;
        _tau = tau;
        _dim = dim;
    }

    private static LearnedHeadShadow? TryLoad()
    {
        try
        {
            var path = Environment.GetEnvironmentVariable("GA_LEARNED_HEAD_PATH");
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path)) return null;

            var dto = JsonSerializer.Deserialize<HeadDto>(File.ReadAllText(path));
            if (dto?.Labels is null || dto.Weights is null || dto.Bias is null) return null;
            // Shadow supports the no-PCA head only; disable if a PCA head ships
            // (the inference path here doesn't apply the PCA projection).
            if (dto.Pca is { ValueKind: JsonValueKind.Object }) return null;
            if (dto.Weights.Length != dto.Dim || dto.Bias.Length != dto.Labels.Length) return null;

            return new LearnedHeadShadow(dto.Labels, dto.Weights, dto.Bias, dto.Tau, dto.Dim);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Evaluate the head on a query embedding. Returns the chosen intent id
    /// (<c>null</c> = declined), the winner's softmax probability, and whether it
    /// was declined (max probability below <c>tau</c>).
    /// </summary>
    public (string? Chosen, double Confidence, bool Declined) Evaluate(float[] vec)
    {
        if (vec.Length != _dim) return (null, 0.0, true);

        // L2-normalize (matches training-time normalization).
        double norm = 0.0;
        for (var i = 0; i < vec.Length; i++) norm += (double)vec[i] * vec[i];
        norm = Math.Sqrt(norm);
        if (norm <= 0.0) return (null, 0.0, true);

        var k = _labels.Length;
        var logits = new double[k];
        for (var c = 0; c < k; c++) logits[c] = _b[c];
        for (var f = 0; f < _dim; f++)
        {
            var x = vec[f] / norm;
            var wf = _w[f];
            for (var c = 0; c < k; c++) logits[c] += x * wf[c];
        }

        // softmax + argmax
        var max = double.NegativeInfinity;
        for (var c = 0; c < k; c++)
        {
            if (logits[c] > max) max = logits[c];
        }
        double sum = 0.0;
        for (var c = 0; c < k; c++)
        {
            logits[c] = Math.Exp(logits[c] - max);
            sum += logits[c];
        }
        var best = 0;
        var bestP = double.NegativeInfinity;
        for (var c = 0; c < k; c++)
        {
            var p = logits[c] / sum;
            if (p > bestP)
            {
                bestP = p;
                best = c;
            }
        }

        var declined = bestP < _tau;
        return (declined ? null : _labels[best], bestP, declined);
    }

    /// <summary>Evaluate + append a shadow telemetry record. Fully error-swallowed —
    /// shadow logging must never affect the routing caller.</summary>
    public void LogShadow(string query, float[] queryVec, string? prodChosen)
    {
        try
        {
            var (chosen, conf, declined) = Evaluate(queryVec);
            RoutingShadowLog.Append(new RoutingShadowRecord
            {
                Timestamp = DateTime.UtcNow.ToString("o"),
                Query = query,
                ProdChosen = prodChosen,
                HeadChosen = chosen,
                HeadConfidence = conf,
                HeadDeclined = declined,
                Agree = string.Equals(prodChosen, chosen, StringComparison.Ordinal),
            });
        }
        catch
        {
            // shadow must never affect routing
        }
    }

    private sealed record HeadDto
    {
        [JsonPropertyName("dim")] public int Dim { get; init; }
        [JsonPropertyName("labels")] public string[]? Labels { get; init; }
        [JsonPropertyName("weights")] public float[][]? Weights { get; init; }
        [JsonPropertyName("bias")] public float[]? Bias { get; init; }
        [JsonPropertyName("tau")] public double Tau { get; init; }
        [JsonPropertyName("pca")] public JsonElement? Pca { get; init; }
    }
}
