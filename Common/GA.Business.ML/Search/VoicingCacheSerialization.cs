namespace GA.Business.ML.Search;

using System.Text.Json;
using System.Text.Json.Serialization;
using Rag.Models;

public record VoicingCacheItem(
    ChordVoicingRagDocument Document,
    double[] Embedding);

public static class VoicingCacheSerialization
{
    private static readonly JsonSerializerOptions _options = new()
    {
        WriteIndented = false,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        NumberHandling = JsonNumberHandling.AllowNamedFloatingPointLiterals
    };

    public static List<ChordVoicingRagDocument> LoadFromCache(string filePath)
    {
        if (!File.Exists(filePath))
        {
            return [];
        }

        using var fs = File.OpenRead(filePath);
        return JsonSerializer.Deserialize<List<ChordVoicingRagDocument>>(fs, _options) ?? [];
    }

    public static void SaveToCache(string filePath, IEnumerable<ChordVoicingRagDocument> documents)
    {
        var dir = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrEmpty(dir))
        {
            Directory.CreateDirectory(dir);
        }

        using var fs = File.Create(filePath);
        JsonSerializer.Serialize(fs, documents, _options);
    }
}
