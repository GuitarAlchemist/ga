namespace GA.Domain.Core.Theory.Tonal.Cadences;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

public static class CadenceCatalog
{
    private static readonly Lazy<List<CadenceDefinition>> _cadences = new(LoadCadences);

    public static IReadOnlyList<CadenceDefinition> Items => _cadences.Value;

    private static List<CadenceDefinition> LoadCadences()
    {
        try
        {
            var path = FindConfigFile("Cadences.yaml");
            if (string.IsNullOrEmpty(path)) return new List<CadenceDefinition>();

            var yaml = File.ReadAllText(path);
            var deserializer = new DeserializerBuilder()
                .WithNamingConvention(PascalCaseNamingConvention.Instance)
                .IgnoreUnmatchedProperties()
                .Build();

            var root = deserializer.Deserialize<CadenceRoot>(yaml);
            return root.Cadences ?? new List<CadenceDefinition>();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[CadenceCatalog] Error loading cadences: {ex.Message}");
            return new List<CadenceDefinition>();
        }
    }

    private static string? FindConfigFile(string fileName)
    {
        var baseDir = AppDomain.CurrentDomain.BaseDirectory;
        
        var candidates = new[]
        {
            Path.Combine(baseDir, fileName),
            Path.Combine(baseDir, "Configuration", fileName),
            // Dev environment fallbacks
            Path.Combine(baseDir, "..", "..", "..", "..", "..", "Common", "GA.Business.Config", fileName),
            Path.Combine(baseDir, "..", "..", "..", "Common", "GA.Business.Config", fileName)
        };

        return candidates.FirstOrDefault(File.Exists);
    }

    public class CadenceRoot
    {
        public List<CadenceDefinition>? Cadences { get; set; }
    }
}