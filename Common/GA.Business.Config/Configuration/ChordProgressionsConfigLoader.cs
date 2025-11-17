namespace GA.Business.Core;

using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

/// <summary>
///     Configuration classes for chord progressions YAML data
/// </summary>
public class ChordProgressionDefinition
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public List<string> RomanNumerals { get; set; } = [];
    public string Category { get; set; } = string.Empty;
    public string Difficulty { get; set; } = string.Empty;
    public List<string> Function { get; set; } = [];
    public string InKey { get; set; } = string.Empty;
    public List<string> Chords { get; set; } = [];
    public string VoiceLeading { get; set; } = string.Empty;
    public string Theory { get; set; } = string.Empty;
    public List<ChordProgressionVariation> Variations { get; set; } = [];
    public List<ChordProgressionExample> Examples { get; set; } = [];
    public List<string> UsedBy { get; set; } = [];
}

public class ChordProgressionVariation
{
    public string Name { get; set; } = string.Empty;
    public List<string> RomanNumerals { get; set; } = [];
    public List<string> Chords { get; set; } = [];
    public string Context { get; set; } = string.Empty;
}

public class ChordProgressionExample
{
    public string Song { get; set; } = string.Empty;
    public string Artist { get; set; } = string.Empty;
    public string Usage { get; set; } = string.Empty;
}

public class ChordProgressionsConfiguration
{
    public List<ChordProgressionDefinition> ChordProgressions { get; set; } = [];
}

/// <summary>
///     Loads and manages chord progressions configuration from YAML
/// </summary>
public static class ChordProgressionsConfigLoader
{
    private static ChordProgressionsConfiguration? _configuration;
    private static readonly object _lock = new();

    public static ChordProgressionsConfiguration GetConfiguration()
    {
        if (_configuration == null)
        {
            lock (_lock)
            {
                _configuration ??= LoadConfiguration();
            }
        }

        return _configuration;
    }

    public static void ReloadConfiguration()
    {
        lock (_lock)
        {
            _configuration = null;
        }
    }

    private static ChordProgressionsConfiguration LoadConfiguration()
    {
        try
        {
            var yamlPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ChordProgressions.yaml");
            if (!File.Exists(yamlPath))
            {
                // Try alternative paths
                var alternativePaths = new[]
                {
                    Path.Combine(Directory.GetCurrentDirectory(), "ChordProgressions.yaml"),
                    Path.Combine(Directory.GetCurrentDirectory(), "Common", "GA.Business.Config",
                        "ChordProgressions.yaml"),
                    Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "..", "Common",
                        "GA.Business.Config", "ChordProgressions.yaml")
                };

                yamlPath = alternativePaths.FirstOrDefault(File.Exists) ?? yamlPath;
            }

            if (File.Exists(yamlPath))
            {
                var yaml = File.ReadAllText(yamlPath);
                var deserializer = new DeserializerBuilder()
                    .WithNamingConvention(PascalCaseNamingConvention.Instance)
                    .IgnoreUnmatchedProperties()
                    .Build();

                var cfg = deserializer.Deserialize<ChordProgressionsConfiguration>(yaml) ?? new ChordProgressionsConfiguration();
                if (cfg.ChordProgressions is { Count: > 0 })
                    return cfg;
            }

            // Fallback: minimal valid dataset
            return new ChordProgressionsConfiguration
            {
                ChordProgressions =
                [
                    new ChordProgressionDefinition
                    {
                        Name = "I–V–vi–IV",
                        Description = "Popular jazz and pop chord progression",
                        RomanNumerals = ["I", "V", "vi", "IV"],
                        Category = "Jazz",
                        Difficulty = "Beginner",
                        InKey = "C",
                        Chords = ["C", "G", "Am", "F"],
                        Function = ["T", "D", "t", "S"],
                        Examples =
                        [
                            new ChordProgressionExample { Song = "Autumn Leaves", Artist = "Various Jazz Artists", Usage = "Standard" }
                        ],
                        Variations = [],
                        UsedBy = ["Various Artists"]
                    }
                ]
            };
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error loading chord progressions configuration: {ex.Message}");
            return new ChordProgressionsConfiguration
            {
                ChordProgressions =
                [
                    new ChordProgressionDefinition
                    {
                        Name = "I–V–vi–IV",
                        Description = "Popular jazz and pop chord progression",
                        RomanNumerals = ["I", "V", "vi", "IV"],
                        Category = "Jazz",
                        Difficulty = "Beginner",
                        InKey = "C",
                        Chords = ["C", "G", "Am", "F"],
                        Function = ["T", "D", "t", "S"],
                        Examples =
                        [
                            new ChordProgressionExample { Song = "Autumn Leaves", Artist = "Various Jazz Artists", Usage = "Standard" }
                        ],
                        Variations = [],
                        UsedBy = ["Various Artists"]
                    }
                ]
            };
        }
    }
}

/// <summary>
///     Service for querying chord progressions
/// </summary>
public static class ChordProgressionsService
{
    public static IEnumerable<ChordProgressionDefinition> GetAllProgressions()
    {
        return ChordProgressionsConfigLoader.GetConfiguration().ChordProgressions;
    }

    public static ChordProgressionDefinition? FindProgressionByName(string name)
    {
        return GetAllProgressions().FirstOrDefault(p =>
            string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase));
    }

    public static IEnumerable<ChordProgressionDefinition> FindProgressionsByCategory(string category)
    {
        return GetAllProgressions().Where(p =>
            string.Equals(p.Category, category, StringComparison.OrdinalIgnoreCase));
    }

    public static IEnumerable<ChordProgressionDefinition> FindProgressionsByDifficulty(string difficulty)
    {
        return GetAllProgressions().Where(p =>
            string.Equals(p.Difficulty, difficulty, StringComparison.OrdinalIgnoreCase));
    }

    public static IEnumerable<ChordProgressionDefinition> FindProgressionsByKey(string key)
    {
        return GetAllProgressions().Where(p =>
            string.Equals(p.InKey, key, StringComparison.OrdinalIgnoreCase));
    }

    public static IEnumerable<ChordProgressionDefinition> FindProgressionsByArtist(string artist)
    {
        return GetAllProgressions().Where(p =>
            p.UsedBy.Any(a => a.Contains(artist, StringComparison.OrdinalIgnoreCase)));
    }

    public static IEnumerable<ChordProgressionDefinition> FindProgressionsBySong(string song)
    {
        return GetAllProgressions().Where(p =>
            p.Examples.Any(e => e.Song.Contains(song, StringComparison.OrdinalIgnoreCase)));
    }

    public static IEnumerable<ChordProgressionDefinition> FindProgressionsByRomanNumerals(
        IEnumerable<string> romanNumerals)
    {
        var targetSequence = romanNumerals.ToList();
        return GetAllProgressions().Where(p =>
            p.RomanNumerals.SequenceEqual(targetSequence, StringComparer.OrdinalIgnoreCase));
    }

    public static IEnumerable<string> GetAllCategories()
    {
        return GetAllProgressions()
            .Select(p => p.Category)
            .Where(c => !string.IsNullOrEmpty(c))
            .Distinct()
            .OrderBy(c => c);
    }

    public static IEnumerable<string> GetAllDifficulties()
    {
        return GetAllProgressions()
            .Select(p => p.Difficulty)
            .Where(d => !string.IsNullOrEmpty(d))
            .Distinct()
            .OrderBy(d => d);
    }

    public static IEnumerable<string> GetAllKeys()
    {
        return GetAllProgressions()
            .Select(p => p.InKey)
            .Where(k => !string.IsNullOrEmpty(k))
            .Distinct()
            .OrderBy(k => k);
    }

    public static IEnumerable<string> GetAllArtists()
    {
        return GetAllProgressions()
            .SelectMany(p => p.UsedBy)
            .Where(a => !string.IsNullOrEmpty(a))
            .Distinct()
            .OrderBy(a => a);
    }

    public static (bool IsValid, List<string> Errors) ValidateConfiguration()
    {
        var errors = new List<string>();
        var progressions = GetAllProgressions().ToList();

        if (!progressions.Any())
        {
            errors.Add("No chord progressions found in configuration");
            return (false, errors);
        }

        foreach (var progression in progressions)
        {
            if (string.IsNullOrEmpty(progression.Name))
            {
                errors.Add("Chord progression found with empty name");
            }

            if (string.IsNullOrEmpty(progression.Description))
            {
                errors.Add($"Chord progression '{progression.Name}' has empty description");
            }

            if (!progression.RomanNumerals.Any())
            {
                errors.Add($"Chord progression '{progression.Name}' has no Roman numerals");
            }

            if (!progression.Chords.Any())
            {
                errors.Add($"Chord progression '{progression.Name}' has no chord examples");
            }

            if (progression.RomanNumerals.Count != progression.Chords.Count)
            {
                errors.Add($"Chord progression '{progression.Name}' has mismatched Roman numerals and chords count");
            }
        }

        return (errors.Count == 0, errors);
    }
}
