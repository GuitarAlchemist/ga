namespace GA.Business.ML.Naturalness;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using Core.Fretboard.Analysis;

/// <summary>
/// ONNX-based naturalness ranker using the trained FastTree model.
/// </summary>
public class MlNaturalnessRanker : IMlNaturalnessRanker, IDisposable
{
    private readonly InferenceSession? _session;
    private readonly bool _isAvailable;

    public MlNaturalnessRanker(string? modelPath = null)
    {
        modelPath ??= Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "models", "naturalness_ranker.onnx");
        
        if (File.Exists(modelPath))
        {
            try
            {
                _session = new InferenceSession(modelPath);
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

    public float PredictNaturalness(List<FretboardPosition> from, List<FretboardPosition> to)
    {
        if (!_isAvailable || _session == null) return 0.5f; // Neutral fallback

        // Extract features (same as training data generator)
        var features = ExtractFeatures(from, to);
        
        // Create input tensor
        var inputTensor = new DenseTensor<float>(features, new[] { 1, features.Length });
        var inputs = new List<NamedOnnxValue>
        {
            NamedOnnxValue.CreateFromTensor("Features", inputTensor)
        };

        try
        {
            using var results = _session.Run(inputs);
            var output = results.First().AsTensor<float>();
            return Math.Clamp(output[0], 0f, 1f);
        }
        catch
        {
            return 0.5f;
        }
    }

    private float[] ExtractFeatures(List<FretboardPosition> a, List<FretboardPosition> b)
    {
        if (a.Count == 0 || b.Count == 0) return new float[5];

        float avgA = (float)a.Average(p => p.Fret);
        float avgB = (float)b.Average(p => p.Fret);
        float deltaAvg = Math.Abs(avgA - avgB);

        int spreadA = a.Max(p => p.Fret) - a.Min(p => p.Fret);
        int spreadB = b.Max(p => p.Fret) - b.Min(p => p.Fret);
        float deltaStretch = Math.Abs(spreadA - spreadB);

        int sharedStrings = a.Select(p => p.StringIndex.Value)
            .Intersect(b.Select(p => p.StringIndex.Value)).Count();
        int changedStrings = (a.Count + b.Count) - 2 * sharedStrings;

        return new float[]
        {
            deltaAvg,           // DeltaAvgFret
            deltaAvg,           // MaxFingerDisp (simplified)
            changedStrings,     // StringCrossingCount
            deltaStretch,       // HandStretchDelta
            sharedStrings       // CommonStrings
        };
    }

    public void Dispose()
    {
        _session?.Dispose();
    }
}
