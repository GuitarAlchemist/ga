namespace GaCLI.Commands;

using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.ML;
using Microsoft.ML.Data;

/// <summary>
/// Input row for naturalness training.
/// </summary>
public class NaturalnessInput
{
    [LoadColumn(0)]
    public float Label { get; set; }
    
    [LoadColumn(1)]
    public float DeltaAvgFret { get; set; }
    
    [LoadColumn(2)]
    public float MaxFingerDisp { get; set; }
    
    [LoadColumn(3)]
    public float StringCrossingCount { get; set; }
    
    [LoadColumn(4)]
    public float HandStretchDelta { get; set; }
    
    [LoadColumn(5)]
    public float CommonStrings { get; set; }
}

/// <summary>
/// Prediction output from the naturalness model.
/// </summary>
public class NaturalnessPrediction
{
    [ColumnName("Score")]
    public float Score { get; set; }
}

public class TrainNaturalnessModelCommand
{
    public Task ExecuteAsync(string dataPath = "naturalness_data.csv", string outputPath = "models/naturalness_ranker.onnx")
    {
        Console.WriteLine($"Loading data from: {dataPath}");
        
        var mlContext = new MLContext(seed: 42);
        
        // 1. Load Data
        var dataView = mlContext.Data.LoadFromTextFile<NaturalnessInput>(
            dataPath, 
            hasHeader: true, 
            separatorChar: ',');
        
        // 2. Split for validation
        var split = mlContext.Data.TrainTestSplit(dataView, testFraction: 0.2);
        
        // 3. Define Pipeline
        var featureColumns = new[] { "DeltaAvgFret", "MaxFingerDisp", "StringCrossingCount", "HandStretchDelta", "CommonStrings" };
        
        var pipeline = mlContext.Transforms.Concatenate("Features", featureColumns)
            .Append(mlContext.Regression.Trainers.FastTree(
                labelColumnName: "Label",
                featureColumnName: "Features",
                numberOfLeaves: 20,
                numberOfTrees: 100,
                minimumExampleCountPerLeaf: 5));
        
        // 4. Train
        Console.WriteLine("Training FastTree regression model...");
        var model = pipeline.Fit(split.TrainSet);
        
        // 5. Evaluate
        Console.WriteLine("Evaluating...");
        var predictions = model.Transform(split.TestSet);
        var metrics = mlContext.Regression.Evaluate(predictions, labelColumnName: "Label", scoreColumnName: "Score");
        
        Console.WriteLine($"  R² Score: {metrics.RSquared:F4}");
        Console.WriteLine($"  MAE:      {metrics.MeanAbsoluteError:F4}");
        Console.WriteLine($"  RMSE:     {metrics.RootMeanSquaredError:F4}");
        
        // 6. Export to ONNX
        Console.WriteLine($"Exporting model to: {outputPath}");
        Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
        
        using var stream = File.Create(outputPath);
        mlContext.Model.ConvertToOnnx(model, dataView, stream);
        
        Console.WriteLine("Model training complete!");
        return Task.CompletedTask;
    }
}
