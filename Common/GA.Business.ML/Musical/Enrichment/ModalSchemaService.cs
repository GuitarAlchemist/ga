namespace GA.Business.ML.Musical.Enrichment;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

/// <summary>
/// Loads modal flavor embedding indices from ModalEmbedding.yaml.
/// Provides lookup for mode names to embedding indices and characteristic intervals.
/// </summary>
public class ModalSchemaService
{
    private static ModalSchemaService? _instance;
    private readonly Dictionary<string, ModalEntry> _modesByName = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<int, ModalEntry> _modesByIndex = new();
    private int _totalModes;

    public static ModalSchemaService Instance => _instance ??= new();

    public int TotalModes => _totalModes;

    private ModalSchemaService()
    {
        LoadSchema();
    }

    private void LoadSchema()
    {
        try
        {
            var possiblePaths = new[]
            {
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ModalEmbedding.yaml"),
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "..", "..", "Common", "GA.Business.Config", "ModalEmbedding.yaml"),
                "ModalEmbedding.yaml"
            };

            string? path = possiblePaths.FirstOrDefault(File.Exists);
            if (path == null)
            {
                Console.WriteLine("[ModalSchemaService] Warning: ModalEmbedding.yaml not found.");
                return;
            }

            var yaml = File.ReadAllText(path);
            var deserializer = new DeserializerBuilder()
                .WithNamingConvention(PascalCaseNamingConvention.Instance)
                .IgnoreUnmatchedProperties()
                .Build();

            var schema = deserializer.Deserialize<ModalEmbeddingSchema>(yaml);

            // Flatten all mode families into lookup dictionaries
            ProcessFamily(schema.MajorScaleModes);
            ProcessFamily(schema.HarmonicMinorModes);
            ProcessFamily(schema.MelodicMinorModes);
            ProcessFamily(schema.HarmonicMajorModes);
            ProcessFamily(schema.DoubleHarmonicModes);
            ProcessFamily(schema.SymmetricScales);
            ProcessFamily(schema.PentatonicAndBlues);
            ProcessFamily(schema.BebopScales);
            ProcessFamily(schema.ExoticScales);

            _totalModes = _modesByName.Count;
            Console.WriteLine($"[ModalSchemaService] Loaded {_totalModes} modes from YAML.");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ModalSchemaService] Error loading schema: {ex.Message}");
        }
    }

    private void ProcessFamily(List<ModalModeEntry>? modes)
    {
        if (modes == null) return;
        
        foreach (var mode in modes)
        {
            var entry = new ModalEntry
            {
                Name = mode.Name,
                Index = mode.Index,
                CharacteristicIntervals = mode.CharacteristicIntervals ?? []
            };

            _modesByName[mode.Name] = entry;
            _modesByIndex[mode.Index] = entry;

            // Register alternate names
            if (mode.AlternateNames != null)
            {
                foreach (var alt in mode.AlternateNames)
                {
                    _modesByName[alt] = entry;
                }
            }
        }
    }

    /// <summary>
    /// Gets the embedding index for a mode by name.
    /// </summary>
    public int? GetIndex(string modeName)
    {
        return _modesByName.TryGetValue(modeName, out var entry) ? entry.Index : null;
    }

    /// <summary>
    /// Gets the characteristic intervals for a mode by name.
    /// </summary>
    public IReadOnlyList<string> GetCharacteristicIntervals(string modeName)
    {
        return _modesByName.TryGetValue(modeName, out var entry) 
            ? entry.CharacteristicIntervals 
            : [];
    }

    /// <summary>
    /// Gets all registered mode names.
    /// </summary>
    public IEnumerable<string> GetAllModeNames() => _modesByName.Keys;

    /// <summary>
    /// Gets all modal entries for iteration.
    /// </summary>
    public IEnumerable<ModalEntry> GetAllModes() => _modesByIndex.Values;

    // --- Internal Types ---
    
    public class ModalEntry
    {
        public string Name { get; set; } = "";
        public int Index { get; set; }
        public List<string> CharacteristicIntervals { get; set; } = [];
    }

    private class ModalEmbeddingSchema
    {
        public string SchemaVersion { get; set; } = "";
        public int ModalPartitionStart { get; set; }
        public int TotalModes { get; set; }
        public List<ModalModeEntry>? MajorScaleModes { get; set; }
        public List<ModalModeEntry>? HarmonicMinorModes { get; set; }
        public List<ModalModeEntry>? MelodicMinorModes { get; set; }
        public List<ModalModeEntry>? HarmonicMajorModes { get; set; }
        public List<ModalModeEntry>? DoubleHarmonicModes { get; set; }
        public List<ModalModeEntry>? SymmetricScales { get; set; }
        public List<ModalModeEntry>? PentatonicAndBlues { get; set; }
        public List<ModalModeEntry>? BebopScales { get; set; }
        public List<ModalModeEntry>? ExoticScales { get; set; }
    }

    private class ModalModeEntry
    {
        public string Name { get; set; } = "";
        public int Index { get; set; }
        public List<string>? CharacteristicIntervals { get; set; }
        public List<string>? AlternateNames { get; set; }
    }
}
