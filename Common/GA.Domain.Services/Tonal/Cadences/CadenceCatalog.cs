namespace GA.Domain.Services.Tonal.Cadences;

using System.IO;
using System.Linq;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;
using GA.Domain.Core.Theory.Tonal.Cadences;

public static class CadenceCatalog
{
    private static readonly Lazy<List<CadenceDefinition>> _cadences = new(LoadCadences);

    public static IReadOnlyList<CadenceDefinition> Items => _cadences.Value;

    private static List<CadenceDefinition> LoadCadences()
    {
        try
        {
            var path = FindConfigFile("Cadences.yaml");
            if (string.IsNullOrEmpty(path)) return [];

            var yaml = File.ReadAllText(path);
            var deserializer = new DeserializerBuilder()
                .WithNamingConvention(PascalCaseNamingConvention.Instance)
                .IgnoreUnmatchedProperties()
                .Build();

            var root = deserializer.Deserialize<CadenceRootYaml>(yaml);
            return root.Cadences?.Select(MapToDomain).ToList() ?? [];
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[CadenceCatalog] Error loading cadences: {ex.Message}");
            return [];
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

    private static CadenceDefinition MapToDomain(CadenceDefinitionYaml source) =>
        new()
        {
            Name = source.Name ?? string.Empty,
            Category = source.Category ?? string.Empty,
            Description = source.Description ?? string.Empty,
            RomanNumerals = source.RomanNumerals ?? [],
            InKey = source.InKey ?? string.Empty,
            Chords = source.Chords ?? [],
            Function = source.Function,
            VoiceLeading = source.VoiceLeading
        };

    private sealed class CadenceRootYaml
    {
        public List<CadenceDefinitionYaml>? Cadences { get; set; }
    }

    private sealed class CadenceDefinitionYaml
    {
        public string? Name { get; set; }
        public string? Category { get; set; }
        public string? Description { get; set; }
        public List<string>? RomanNumerals { get; set; }
        public string? InKey { get; set; }
        public List<string>? Chords { get; set; }
        public string? Function { get; set; }
        public string? VoiceLeading { get; set; }
    }
}
