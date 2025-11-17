namespace GA.Business.Core;

using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

/// <summary>
///     Configuration classes for guitar techniques YAML data
/// </summary>
public class GuitarTechniqueDefinition
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string Inventor { get; set; } = string.Empty;
    public string Difficulty { get; set; } = string.Empty;
    public string Concept { get; set; } = string.Empty;
    public string Theory { get; set; } = string.Empty;
    public string Technique { get; set; } = string.Empty;
    public List<GuitarTechniqueExample> Examples { get; set; } = [];
    public List<GuitarTechniqueApplication> Applications { get; set; } = [];
    public List<GuitarTechniquePattern> Patterns { get; set; } = [];
    public List<GuitarTechniqueVariation> Variations { get; set; } = [];
    public List<string> Artists { get; set; } = [];
    public List<string> Songs { get; set; } = [];
    public List<string> Benefits { get; set; } = [];
    public List<string> Rules { get; set; } = [];
    public Dictionary<string, object> Practice { get; set; } = [];
}

public class GuitarTechniqueExample
{
    public string CentralPitch { get; set; } = string.Empty;
    public List<string> Chords { get; set; } = [];
    public string Scale { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Fretboard { get; set; } = string.Empty;
    public string Pattern { get; set; } = string.Empty;
    public string Shape { get; set; } = string.Empty;
    public List<int> Strings { get; set; } = [];
    public List<int> Frets { get; set; } = [];
    public string Direction { get; set; } = string.Empty;
    public List<string> Notes { get; set; } = [];
}

public class GuitarTechniqueApplication
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Context { get; set; } = string.Empty;
}

public class GuitarTechniquePattern
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public List<int> Frets { get; set; } = [];
    public int String { get; set; }
    public string Technique { get; set; } = string.Empty;
    public List<string> Strings { get; set; } = [];
    public string Direction { get; set; } = string.Empty;
    public string Interval { get; set; } = string.Empty;
}

public class GuitarTechniqueVariation
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Context { get; set; } = string.Empty;
    public List<string> Types { get; set; } = [];
}

public class GuitarTechniquesConfiguration
{
    public List<GuitarTechniqueDefinition> GuitarTechniques { get; set; } = [];
}

/// <summary>
///     Loads and manages guitar techniques configuration from YAML
/// </summary>
public static class GuitarTechniquesConfigLoader
{
    private static GuitarTechniquesConfiguration? _configuration;
    private static readonly object _lock = new();

    public static GuitarTechniquesConfiguration GetConfiguration()
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

    private static GuitarTechniquesConfiguration LoadConfiguration()
    {
        try
        {
            var yamlPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "GuitarTechniques.yaml");
            if (!File.Exists(yamlPath))
            {
                // Try alternative paths
                var alternativePaths = new[]
                {
                    Path.Combine(Directory.GetCurrentDirectory(), "GuitarTechniques.yaml"),
                    Path.Combine(Directory.GetCurrentDirectory(), "Common", "GA.Business.Config",
                        "GuitarTechniques.yaml"),
                    Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "..", "Common",
                        "GA.Business.Config", "GuitarTechniques.yaml")
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

                var cfg = deserializer.Deserialize<GuitarTechniquesConfiguration>(yaml) ?? new GuitarTechniquesConfiguration();
                if (cfg.GuitarTechniques is { Count: > 0 })
                    return cfg;
            }

            // Fallback minimal dataset
            return new GuitarTechniquesConfiguration
            {
                GuitarTechniques =
                [
                    new GuitarTechniqueDefinition
                    {
                        Name = "Alternate Picking",
                        Description = "Picking alternates between downstrokes and upstrokes",
                        Category = "Jazz",
                        Inventor = "Traditional",
                        Difficulty = "Beginner",
                        Concept = "Right-hand technique",
                        Theory = "Maintains rhythmic consistency",
                        Technique = "Use wrist movement to alternate pick",
                        Examples =
                        [
                            new GuitarTechniqueExample
                            {
                                CentralPitch = "C",
                                Chords = ["C"],
                                Scale = "C major",
                                Description = "Simple alternate picking on C major scale",
                                Fretboard = "",
                                Pattern = "",
                                Shape = "",
                                Strings = [1,2,3],
                                Frets = [0,2,4],
                                Direction = "Up",
                                Notes = ["C","D","E"]
                            }
                        ],
                        Applications = [ new GuitarTechniqueApplication { Name = "Speed", Description = "Increase tempo", Context = "Practice" } ],
                        Patterns = [],
                        Variations = [ new GuitarTechniqueVariation { Name = "Economy", Description = "Sweep on string change", Context = "Lead" } ],
                        Artists = ["Various Jazz Artists"],
                        Songs = ["N/A"],
                        Benefits = ["Speed", "Consistency"],
                        Rules = ["Relaxed grip"],
                        Practice = new Dictionary<string, object> { { "Minutes", 10 } }
                    }
                ]
            };
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error loading guitar techniques configuration: {ex.Message}");
            return new GuitarTechniquesConfiguration
            {
                GuitarTechniques =
                [
                    new GuitarTechniqueDefinition
                    {
                        Name = "Alternate Picking",
                        Description = "Picking alternates between downstrokes and upstrokes",
                        Category = "Picking",
                        Inventor = "Traditional",
                        Difficulty = "Beginner",
                        Concept = "Right-hand technique",
                        Theory = "Maintains rhythmic consistency",
                        Technique = "Use wrist movement to alternate pick",
                        Examples = [],
                        Applications = [],
                        Patterns = [],
                        Variations = [],
                        Artists = ["Various"],
                        Songs = ["N/A"],
                        Benefits = ["Speed", "Consistency"],
                        Rules = ["Relaxed grip"],
                        Practice = new Dictionary<string, object> { { "Minutes", 10 } }
                    }
                ]
            };
        }
    }
}

/// <summary>
///     Service for querying guitar techniques
/// </summary>
public static class GuitarTechniquesService
{
    public static IEnumerable<GuitarTechniqueDefinition> GetAllTechniques()
    {
        return GuitarTechniquesConfigLoader.GetConfiguration().GuitarTechniques;
    }

    public static GuitarTechniqueDefinition? FindTechniqueByName(string name)
    {
        return GetAllTechniques().FirstOrDefault(t =>
            string.Equals(t.Name, name, StringComparison.OrdinalIgnoreCase));
    }

    public static IEnumerable<GuitarTechniqueDefinition> FindTechniquesByCategory(string category)
    {
        return GetAllTechniques().Where(t =>
            string.Equals(t.Category, category, StringComparison.OrdinalIgnoreCase));
    }

    public static IEnumerable<GuitarTechniqueDefinition> FindTechniquesByDifficulty(string difficulty)
    {
        return GetAllTechniques().Where(t =>
            string.Equals(t.Difficulty, difficulty, StringComparison.OrdinalIgnoreCase));
    }

    public static IEnumerable<GuitarTechniqueDefinition> FindTechniquesByArtist(string artist)
    {
        return GetAllTechniques().Where(t =>
            t.Artists.Any(a => a.Contains(artist, StringComparison.OrdinalIgnoreCase)));
    }

    public static IEnumerable<GuitarTechniqueDefinition> FindTechniquesByInventor(string inventor)
    {
        return GetAllTechniques().Where(t =>
            t.Inventor.Contains(inventor, StringComparison.OrdinalIgnoreCase));
    }

    public static IEnumerable<GuitarTechniqueDefinition> FindTechniquesBySong(string song)
    {
        return GetAllTechniques().Where(t =>
            t.Songs.Any(s => s.Contains(song, StringComparison.OrdinalIgnoreCase)));
    }

    public static IEnumerable<string> GetAllCategories()
    {
        return GetAllTechniques()
            .Select(t => t.Category)
            .Where(c => !string.IsNullOrEmpty(c))
            .Distinct()
            .OrderBy(c => c);
    }

    public static IEnumerable<string> GetAllDifficulties()
    {
        return GetAllTechniques()
            .Select(t => t.Difficulty)
            .Where(d => !string.IsNullOrEmpty(d))
            .Distinct()
            .OrderBy(d => d);
    }

    public static IEnumerable<string> GetAllArtists()
    {
        return GetAllTechniques()
            .SelectMany(t => t.Artists)
            .Where(a => !string.IsNullOrEmpty(a))
            .Distinct()
            .OrderBy(a => a);
    }

    public static IEnumerable<string> GetAllInventors()
    {
        return GetAllTechniques()
            .Select(t => t.Inventor)
            .Where(i => !string.IsNullOrEmpty(i))
            .Distinct()
            .OrderBy(i => i);
    }

    public static (bool IsValid, List<string> Errors) ValidateConfiguration()
    {
        var errors = new List<string>();
        var techniques = GetAllTechniques().ToList();

        if (!techniques.Any())
        {
            errors.Add("No guitar techniques found in configuration");
            return (false, errors);
        }

        foreach (var technique in techniques)
        {
            if (string.IsNullOrEmpty(technique.Name))
            {
                errors.Add("Guitar technique found with empty name");
            }

            if (string.IsNullOrEmpty(technique.Description))
            {
                errors.Add($"Guitar technique '{technique.Name}' has empty description");
            }

            if (string.IsNullOrEmpty(technique.Category))
            {
                errors.Add($"Guitar technique '{technique.Name}' has no category");
            }
        }

        return (errors.Count == 0, errors);
    }
}
