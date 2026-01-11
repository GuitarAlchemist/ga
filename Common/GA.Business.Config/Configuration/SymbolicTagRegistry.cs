namespace GA.Business.Config.Configuration;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using GA.Business.Core;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

/// <summary>
/// Aggregates symbolic tags from multiple YAML files and provides mapping to embedding subspaces.
/// </summary>
public class SymbolicTagRegistry
    {
        private static readonly Lazy<SymbolicTagRegistry> _instance = new(() => new SymbolicTagRegistry());
        public static SymbolicTagRegistry Instance => _instance.Value;

        private readonly Dictionary<string, int> _tagToBitMap = new(StringComparer.OrdinalIgnoreCase);
        private readonly string _configDir;

        private SymbolicTagRegistry()
        {
            _configDir = FindConfigDir();
            Initialize();
        }

        private string FindConfigDir()
        {
            var possiblePaths = new[]
            {
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Common", "GA.Business.Config"),
                Path.Combine(Directory.GetCurrentDirectory(), "Common", "GA.Business.Config"),
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory),
                Directory.GetCurrentDirectory()
            };

            foreach (var p in possiblePaths)
            {
                if (Directory.Exists(p) && File.Exists(Path.Combine(p, "SemanticNomenclature.yaml")))
                    return p;
            }

            return AppDomain.CurrentDomain.BaseDirectory;
        }

        private void Initialize()
        {
            // --- Technique Partition (0-5) ---
            
            // 1. Semantic Nomenclature (Structure, Register, Playability)
            var semanticTags = SemanticConfig.GetAllTags();
            foreach (var tag in semanticTags)
            {
                var category = SemanticConfig.TryGetCategoryByTagIdManaged(tag.Id);
                if (category == "Structure") RegisterTag(tag.Id, 0);
                else if (category == "Register") RegisterTag(tag.Id, 1);
                else if (category == "Playability") RegisterTag(tag.Id, 2);
                else if (category == "CAGED") RegisterTag(tag.Id, 5);
            }

            // 2. Guitar Techniques
            LoadGenericYamlTags("GuitarTechniques.yaml", 3);
            LoadGenericYamlTags("ArticulationTechniques.yaml", 4);

            // --- Style/Lineage Partition (6-11) ---

            // 3. Semantic Nomenclature (Mood, Genre)
            foreach (var tag in semanticTags)
            {
                var category = SemanticConfig.TryGetCategoryByTagIdManaged(tag.Id);
                if (category == "Mood") RegisterTag(tag.Id, 6);
                else if (category == "Genre") RegisterTag(tag.Id, 7);
            }

            // 4. Advanced Music Theory
            LoadGenericYamlTags("AdvancedHarmony.yaml", 8);
            LoadGenericYamlTags("VoiceLeading.yaml", 9);
            LoadGenericYamlTags("AtonalTechniques.yaml", 10);
            LoadGenericYamlTags("KeyModulationTechniques.yaml", 10);

            // 5. Iconic Chords (Famous/Lineage)
            foreach (var chord in IconicChordsService.GetAllChords())
            {
                RegisterTag(chord.Name, 11);
                if (chord.AlternateNames != null)
                {
                    foreach (var alt in chord.AlternateNames) RegisterTag(alt, 11);
                }
            }
        }

        private void LoadGenericYamlTags(string fileName, int bitIndex)
        {
            try
            {
                var path = Path.Combine(_configDir, fileName);
                if (!File.Exists(path)) return;

                var yaml = File.ReadAllText(path);
                var deserializer = new DeserializerBuilder()
                    .WithNamingConvention(PascalCaseNamingConvention.Instance)
                    .IgnoreUnmatchedProperties()
                    .Build();

                var root = deserializer.Deserialize<object>(yaml);
                ProcessYamlNode(root, bitIndex);
            }
            catch
            {
                // Ignore loading errors for specific files
            }
        }

        private void ProcessYamlNode(object node, int bitIndex)
        {
            if (node is IDictionary<object, object> dict)
            {
                if (dict.TryGetValue("Name", out var name) && name is string nameStr) RegisterTag(nameStr, bitIndex);
                if (dict.TryGetValue("Id", out var id) && id is string idStr) RegisterTag(idStr, bitIndex);

                foreach (var value in dict.Values) ProcessYamlNode(value, bitIndex);
            }
            else if (node is IList<object> list)
            {
                foreach (var item in list) ProcessYamlNode(item, bitIndex);
            }
        }

        private void RegisterTag(string tag, int bitIndex)
        {
            if (string.IsNullOrWhiteSpace(tag)) return;
            var normalized = tag.ToLowerInvariant().Trim().Replace(" ", "-").Replace("_", "-");
            _tagToBitMap[normalized] = bitIndex;
        }

        public int? GetBitIndex(string tag)
        {
            if (string.IsNullOrWhiteSpace(tag)) return null;
            var normalized = tag.ToLowerInvariant().Trim().Replace(" ", "-").Replace("_", "-");
            
            if (_tagToBitMap.TryGetValue(normalized, out var bit)) return bit;
            
            // Partial match fallback (e.g. "sweep" matches "sweep-picking")
            foreach (var kvp in _tagToBitMap)
            {
                if (normalized.Contains(kvp.Key) || kvp.Key.Contains(normalized))
                    return kvp.Value;
            }

            return null;
        }

        public IEnumerable<string> GetAllKnownTags() => _tagToBitMap.Keys;
    }
