namespace GA.Business.ML.Naturalness;

using Domain.Services.Fretboard.Analysis;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;

/// <summary>
///     ONNX-based naturalness ranker using the trained FastTree model.
/// </summary>
public class MlNaturalnessRanker : IMlNaturalnessRanker, IDisposable
{
    private readonly bool _isAvailable;
    private readonly InferenceSession? _session;

    public MlNaturalnessRanker(string? modelPath = null)
    {
        modelPath ??= Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "models", "naturalness_ranker.onnx");

        if (File.Exists(modelPath))
        {
            try
            {
                _session = new(modelPath);
                _isAvailable = true;
            }
            catch
            {
                _isAvailable = false;
            }
        }
        else
        {
            _isAvailable = false;
        }
    }

    public void Dispose() => _session?.Dispose();

    public float PredictNaturalness(List<FretboardPosition> from, List<FretboardPosition> to)
    {
        if (!_isAvailable || _session == null)
        {
            return 0.5f; // Neutral fallback
        }

        // Extract features (same as training data generator)
        var features = ExtractFeatures(from, to);

        // Create input tensor
        var inputTensor = new DenseTensor<float>(features, [1, features.Length]);
        var inputs = new List<NamedOnnxValue>
        {
            NamedOnnxValue.CreateFromTensor("Features", inputTensor)
        };

        try
        {
            using var results = _session.Run(inputs);
            var output = results[0].AsTensor<float>();
            return Math.Clamp(output[0], 0f, 1f);
        }
        catch
        {
            return 0.5f;
        }
    }

    private float[] ExtractFeatures(List<FretboardPosition> a, List<FretboardPosition> b)
    {
        if (a.Count == 0 || b.Count == 0)
        {
            return new float[5];
        }

        var avgA = (float)a.Average(p => p.Fret);
        var avgB = (float)b.Average(p => p.Fret);
        var deltaAvg = Math.Abs(avgA - avgB);

        var spreadA = a.Max(p => p.Fret) - a.Min(p => p.Fret);
        var spreadB = b.Max(p => p.Fret) - b.Min(p => p.Fret);
        float deltaStretch = Math.Abs(spreadA - spreadB);

        var sharedStrings = a.Select(p => p.StringIndex.Value)
            .Intersect(b.Select(p => p.StringIndex.Value)).Count();
        var changedStrings = a.Count + b.Count - 2 * sharedStrings;

        var maxFingerDisp = 0f;
        foreach (var pB in b)
        {
            var pA = a.FirstOrDefault(p => p.StringIndex.Value == pB.StringIndex.Value);
            if (pA != null)
            {
                var disp = Math.Abs(pB.Fret - pA.Fret);
                if (disp > maxFingerDisp) maxFingerDisp = disp;
            }
        }

        return
        [
            deltaAvg, // DeltaAvgFret
            maxFingerDisp, // MaxFingerDisp
            changedStrings, // StringCrossingCount
            deltaStretch, // HandStretchDelta
            sharedStrings // CommonStrings
        ];
    }
}
