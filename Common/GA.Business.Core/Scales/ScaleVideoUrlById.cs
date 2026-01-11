namespace GA.Business.Core.Scales;

using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text.Json;
using Atonal.Primitives;
using GA.Core.Collections;

/// <summary>
/// Provides YouTube video URLs for scale demonstrations, indexed by PitchClassSetId.
/// Data is loaded from embedded JSON resource for maintainability.
/// </summary>
public class ScaleVideoUrlById() : LazyIndexerBase<PitchClassSetId, Uri>(LoadVideoUrls())
{
    private static readonly Lazy<ScaleVideoUrlById> LazyInstance = new(() => new ScaleVideoUrlById());
    internal static ScaleVideoUrlById Instance => LazyInstance.Value;
    
    public static IReadOnlyList<PitchClassSetId> ValidScaleNumbers => [.. Instance.Dictionary.Keys];

    public static bool IsValidScaleNumber(PitchClassSetId pitchClassSetIdentity)
    {
        return Instance.Dictionary.ContainsKey(pitchClassSetIdentity);
    }

    public static Uri? Get(PitchClassSetId pitchClassSetId)
    {
        return IsValidScaleNumber(pitchClassSetId) ? Instance[pitchClassSetId] : null;
    }

    private static IReadOnlyDictionary<PitchClassSetId, Uri> LoadVideoUrls()
    {
        var assembly = Assembly.GetExecutingAssembly();
        var resourceName = "GA.Business.Core.Scales.Data.scale_video_urls.json";
        
        using var stream = assembly.GetManifestResourceStream(resourceName);
        if (stream == null)
        {
            // Fallback: try loading from file path during development
            var filePath = Path.Combine(
                Path.GetDirectoryName(assembly.Location) ?? "",
                "Scales", "Data", "scale_video_urls.json");
            
            if (File.Exists(filePath))
            {
                var json = File.ReadAllText(filePath);
                return ParseJson(json);
            }
            
            return new Dictionary<PitchClassSetId, Uri>();
        }

        using var reader = new StreamReader(stream);
        var content = reader.ReadToEnd();
        return ParseJson(content);
    }

    private static IReadOnlyDictionary<PitchClassSetId, Uri> ParseJson(string json)
    {
        var dict = new Dictionary<PitchClassSetId, Uri>();
        var data = JsonSerializer.Deserialize<Dictionary<string, string>>(json);
        
        if (data == null) return dict;
        
        foreach (var (key, value) in data)
        {
            if (int.TryParse(key, out var id))
            {
                dict[PitchClassSetId.FromValue(id)] = new Uri(value);
            }
        }
        
        return dict;
    }
}
