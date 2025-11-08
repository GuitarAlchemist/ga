namespace GA.Business.Core;

using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

/// <summary>
///     Configuration classes for specialized tunings YAML data
/// </summary>
public class SpecializedTuningDefinition
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public Dictionary<string, string> Configuration { get; set; } = [];
    public List<int> PitchClasses { get; set; } = [];
    public List<string> StringGauges { get; set; } = [];
    public List<string> TonalCharacteristics { get; set; } = [];
    public List<string> Applications { get; set; } = [];
    public List<string> Artists { get; set; } = [];
    public List<string> RecordingTechniques { get; set; } = [];
    public List<string> PlayingConsiderations { get; set; } = [];
    public List<SpecializedTuningVariation> Variations { get; set; } = [];
    public List<SpecializedTuningExample> Examples { get; set; } = [];
    public string TuningPattern { get; set; } = string.Empty;
    public string Interval { get; set; } = string.Empty;
    public string Range { get; set; } = string.Empty;
    public string AddedString { get; set; } = string.Empty;
    public List<string> AddedStrings { get; set; } = [];
    public List<string> Characteristics { get; set; } = [];
    public List<string> Benefits { get; set; } = [];
    public List<string> Challenges { get; set; } = [];
}

public class SpecializedTuningVariation
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public Dictionary<string, string> Configuration { get; set; } = [];
    public List<int> PitchClasses { get; set; } = [];
    public string Context { get; set; } = string.Empty;
    public string Characteristics { get; set; } = string.Empty;
    public string Interval { get; set; } = string.Empty;
}

public class SpecializedTuningExample
{
    public string Key { get; set; } = string.Empty;
    public string PedalNote { get; set; } = string.Empty;
    public List<string> HarmonyAbove { get; set; } = [];
    public string Analysis { get; set; } = string.Empty;
    public string Dissonances { get; set; } = string.Empty;
    public string Tension { get; set; } = string.Empty;
    public string Chord { get; set; } = string.Empty;
    public List<string> Notes { get; set; } = [];
    public string Function { get; set; } = string.Empty;
    public string Usage { get; set; } = string.Empty;
}

public class SpecializedTuningsConfiguration
{
    public List<SpecializedTuningDefinition> AlternativeStringConfigurations { get; set; } = [];
    public List<SpecializedTuningDefinition> SpecializedTuningSystems { get; set; } = [];
    public List<SpecializedTuningDefinition> ExtendedRangeInstruments { get; set; } = [];
    public List<SpecializedTuningDefinition> MultiscaleInstruments { get; set; } = [];
    public List<SpecializedTuningDefinition> RecordingTunings { get; set; } = [];
    public List<SpecializedTuningDefinition> ExperimentalTunings { get; set; } = [];
}

/// <summary>
///     Loads and manages specialized tunings configuration from YAML
/// </summary>
public static class SpecializedTuningsConfigLoader
{
    private static SpecializedTuningsConfiguration? _configuration;
    private static readonly object _lock = new();

    public static SpecializedTuningsConfiguration GetConfiguration()
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

    private static SpecializedTuningsConfiguration LoadConfiguration()
    {
        try
        {
            var yamlPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "SpecializedTunings.yaml");
            if (!File.Exists(yamlPath))
            {
                // Try alternative paths
                var alternativePaths = new[]
                {
                    Path.Combine(Directory.GetCurrentDirectory(), "SpecializedTunings.yaml"),
                    Path.Combine(Directory.GetCurrentDirectory(), "Common", "GA.Business.Config",
                        "SpecializedTunings.yaml"),
                    Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "..", "Common",
                        "GA.Business.Config", "SpecializedTunings.yaml")
                };

                yamlPath = alternativePaths.FirstOrDefault(File.Exists) ?? yamlPath;
            }

            if (!File.Exists(yamlPath))
            {
                return new SpecializedTuningsConfiguration();
            }

            var yaml = File.ReadAllText(yamlPath);
            var deserializer = new DeserializerBuilder()
                .WithNamingConvention(PascalCaseNamingConvention.Instance)
                .IgnoreUnmatchedProperties()
                .Build();

            return deserializer.Deserialize<SpecializedTuningsConfiguration>(yaml);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error loading specialized tunings configuration: {ex.Message}");
            return new SpecializedTuningsConfiguration();
        }
    }
}

/// <summary>
///     Service for querying specialized tunings
/// </summary>
public static class SpecializedTuningsService
{
    public static IEnumerable<SpecializedTuningDefinition> GetAllTunings()
    {
        var config = SpecializedTuningsConfigLoader.GetConfiguration();
        return config.AlternativeStringConfigurations
            .Concat(config.SpecializedTuningSystems)
            .Concat(config.ExtendedRangeInstruments)
            .Concat(config.MultiscaleInstruments)
            .Concat(config.RecordingTunings)
            .Concat(config.ExperimentalTunings);
    }

    public static IEnumerable<SpecializedTuningDefinition> GetAlternativeStringConfigurations()
    {
        return SpecializedTuningsConfigLoader.GetConfiguration().AlternativeStringConfigurations;
    }

    public static IEnumerable<SpecializedTuningDefinition> GetSpecializedTuningSystems()
    {
        return SpecializedTuningsConfigLoader.GetConfiguration().SpecializedTuningSystems;
    }

    public static IEnumerable<SpecializedTuningDefinition> GetExtendedRangeInstruments()
    {
        return SpecializedTuningsConfigLoader.GetConfiguration().ExtendedRangeInstruments;
    }

    public static IEnumerable<SpecializedTuningDefinition> GetRecordingTunings()
    {
        return SpecializedTuningsConfigLoader.GetConfiguration().RecordingTunings;
    }

    public static SpecializedTuningDefinition? FindTuningByName(string name)
    {
        return GetAllTunings().FirstOrDefault(t =>
            string.Equals(t.Name, name, StringComparison.OrdinalIgnoreCase));
    }

    public static IEnumerable<SpecializedTuningDefinition> FindTuningsByCategory(string category)
    {
        return GetAllTunings().Where(t =>
            string.Equals(t.Category, category, StringComparison.OrdinalIgnoreCase));
    }

    public static IEnumerable<SpecializedTuningDefinition> FindTuningsByApplication(string application)
    {
        return GetAllTunings().Where(t =>
            t.Applications.Any(a => a.Contains(application, StringComparison.OrdinalIgnoreCase)));
    }

    public static IEnumerable<SpecializedTuningDefinition> FindTuningsByArtist(string artist)
    {
        return GetAllTunings().Where(t =>
            t.Artists.Any(a => a.Contains(artist, StringComparison.OrdinalIgnoreCase)));
    }

    public static IEnumerable<SpecializedTuningDefinition> FindTuningsByPitchClasses(IEnumerable<int> pitchClasses)
    {
        var targetClasses = pitchClasses.ToList();
        return GetAllTunings().Where(t =>
            t.PitchClasses.SequenceEqual(targetClasses));
    }

    public static IEnumerable<string> GetAllCategories()
    {
        return GetAllTunings()
            .Select(t => t.Category)
            .Where(c => !string.IsNullOrEmpty(c))
            .Distinct()
            .OrderBy(c => c);
    }

    public static IEnumerable<string> GetAllApplications()
    {
        return GetAllTunings()
            .SelectMany(t => t.Applications)
            .Where(a => !string.IsNullOrEmpty(a))
            .Distinct()
            .OrderBy(a => a);
    }

    public static IEnumerable<string> GetAllArtists()
    {
        return GetAllTunings()
            .SelectMany(t => t.Artists)
            .Where(a => !string.IsNullOrEmpty(a))
            .Distinct()
            .OrderBy(a => a);
    }

    public static (bool IsValid, List<string> Errors) ValidateConfiguration()
    {
        var errors = new List<string>();
        var tunings = GetAllTunings().ToList();

        if (!tunings.Any())
        {
            errors.Add("No specialized tunings found in configuration");
            return (false, errors);
        }

        foreach (var tuning in tunings)
        {
            if (string.IsNullOrEmpty(tuning.Name))
            {
                errors.Add("Specialized tuning found with empty name");
            }

            if (string.IsNullOrEmpty(tuning.Description))
            {
                errors.Add($"Specialized tuning '{tuning.Name}' has empty description");
            }

            if (string.IsNullOrEmpty(tuning.Category))
            {
                errors.Add($"Specialized tuning '{tuning.Name}' has no category");
            }

            if (!tuning.Configuration.Any() && !tuning.PitchClasses.Any())
            {
                errors.Add($"Specialized tuning '{tuning.Name}' has no configuration or pitch classes");
            }
        }

        return (errors.Count == 0, errors);
    }
}
