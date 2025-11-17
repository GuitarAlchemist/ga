namespace GA.Business.Core;

using Invariants;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

/// <summary>
///     Loads invariant configurations from YAML files
/// </summary>
public class InvariantConfigurationLoader(
    ILogger<InvariantConfigurationLoader> logger,
    IOptions<InvariantConfigurationOptions> options)
{
    private readonly string _configurationPath = options.Value.ConfigurationPath;

    private readonly IDeserializer _yamlDeserializer = new DeserializerBuilder()
        .WithNamingConvention(UnderscoredNamingConvention.Instance)
        .IgnoreUnmatchedProperties()
        .Build();

    /// <summary>
    ///     Load invariant configuration from YAML file
    /// </summary>
    public async Task<InvariantConfiguration> LoadConfigurationAsync()
    {
        try
        {
            logger.LogInformation("Loading invariant configuration from: {ConfigurationPath}", _configurationPath);

            if (!File.Exists(_configurationPath))
            {
                logger.LogWarning(
                    "Invariant configuration file not found: {ConfigurationPath}. Using default configuration.",
                    _configurationPath);
                return CreateDefaultConfiguration();
            }

            var yamlContent = await File.ReadAllTextAsync(_configurationPath);
            var configuration = _yamlDeserializer.Deserialize<InvariantConfiguration>(yamlContent);

            logger.LogInformation("Successfully loaded invariant configuration with {GroupCount} groups",
                configuration.InvariantGroups.Count);

            ValidateConfiguration(configuration);
            return configuration;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error loading invariant configuration from: {ConfigurationPath}", _configurationPath);
            throw new InvalidOperationException($"Failed to load invariant configuration: {ex.Message}", ex);
        }
    }

    /// <summary>
    ///     Load configuration for specific environment
    /// </summary>
    public async Task<InvariantConfiguration> LoadConfigurationAsync(string environment)
    {
        var configuration = await LoadConfigurationAsync();

        if (configuration.Environments.TryGetValue(environment, out var environmentSettings))
        {
            logger.LogInformation("Applying environment-specific settings for: {Environment}", environment);
            configuration.Settings = MergeSettings(configuration.Settings, environmentSettings);
        }
        else
        {
            logger.LogWarning("No environment-specific settings found for: {Environment}", environment);
        }

        return configuration;
    }

    /// <summary>
    ///     Watch for configuration file changes
    /// </summary>
    public IDisposable WatchConfiguration(Action<InvariantConfiguration> onConfigurationChanged)
    {
        var directory = Path.GetDirectoryName(_configurationPath);
        var fileName = Path.GetFileName(_configurationPath);

        if (string.IsNullOrEmpty(directory) || string.IsNullOrEmpty(fileName))
        {
            throw new InvalidOperationException("Invalid configuration path");
        }

        var watcher = new FileSystemWatcher(directory, fileName)
        {
            NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size,
            EnableRaisingEvents = true
        };

        watcher.Changed += async (_, _) =>
        {
            try
            {
                logger.LogInformation("Invariant configuration file changed, reloading...");

                // Add a small delay to ensure file write is complete
                await Task.Delay(500);

                var newConfiguration = await LoadConfigurationAsync();
                onConfigurationChanged(newConfiguration);

                logger.LogInformation("Invariant configuration reloaded successfully");
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error reloading invariant configuration");
            }
        };

        return watcher;
    }

    /// <summary>
    ///     Validate configuration for common issues
    /// </summary>
    private void ValidateConfiguration(InvariantConfiguration configuration)
    {
        var issues = new List<string>();

        foreach (var (groupName, group) in configuration.InvariantGroups)
        {
            if (string.IsNullOrEmpty(group.TargetType))
            {
                issues.Add($"Group '{groupName}' has no target type specified");
            }

            foreach (var invariant in group.Invariants)
            {
                if (string.IsNullOrEmpty(invariant.Name))
                {
                    issues.Add($"Invariant in group '{groupName}' has no name");
                }

                if (string.IsNullOrEmpty(invariant.RuleType))
                {
                    issues.Add($"Invariant '{invariant.Name}' has no rule type");
                }

                if (string.IsNullOrEmpty(invariant.TargetProperty))
                {
                    issues.Add($"Invariant '{invariant.Name}' has no target property");
                }

                if (string.IsNullOrEmpty(invariant.ErrorMessage))
                {
                    issues.Add($"Invariant '{invariant.Name}' has no error message");
                }

                // Validate rule-specific requirements
                ValidateRuleSpecificRequirements(invariant, issues);
            }
        }

        if (issues.Any())
        {
            var issueList = string.Join("\n- ", issues);
            logger.LogWarning("Configuration validation issues found:\n- {Issues}", issueList);
        }
    }

    private static void ValidateRuleSpecificRequirements(InvariantDefinition invariant, List<string> issues)
    {
        switch (invariant.RuleType)
        {
            case "Regex":
                if (string.IsNullOrEmpty(invariant.Pattern))
                {
                    issues.Add($"Regex invariant '{invariant.Name}' has no pattern specified");
                }

                break;

            case "Enum":
                if (invariant.AllowedValues == null || !invariant.AllowedValues.Any())
                {
                    issues.Add($"Enum invariant '{invariant.Name}' has no allowed values specified");
                }

                break;

            case "Range":
                if (invariant.Min >= invariant.Max)
                {
                    issues.Add($"Range invariant '{invariant.Name}' has invalid min/max values");
                }

                break;

            case "Collection":
                if (invariant.CollectionRules == null || !invariant.CollectionRules.Any())
                {
                    issues.Add($"Collection invariant '{invariant.Name}' has no collection rules specified");
                }

                break;

            case "String":
                if (invariant.StringRules == null || !invariant.StringRules.Any())
                {
                    issues.Add($"String invariant '{invariant.Name}' has no string rules specified");
                }

                break;

            case "Custom":
                if (string.IsNullOrEmpty(invariant.Rule))
                {
                    issues.Add($"Custom invariant '{invariant.Name}' has no rule specified");
                }

                break;
        }
    }

    private static InvariantSettings MergeSettings(InvariantSettings baseSettings,
        InvariantSettings environmentSettings)
    {
        return new InvariantSettings
        {
            CacheEnabled = environmentSettings.CacheEnabled,
            CacheDurationMinutes = environmentSettings.CacheDurationMinutes != 0
                ? environmentSettings.CacheDurationMinutes
                : baseSettings.CacheDurationMinutes,
            PerformanceMonitoring = environmentSettings.PerformanceMonitoring,
            AsyncValidation = environmentSettings.AsyncValidation,
            MaxConcurrentValidations = environmentSettings.MaxConcurrentValidations != 0
                ? environmentSettings.MaxConcurrentValidations
                : baseSettings.MaxConcurrentValidations,
            ValidationTimeoutSeconds = environmentSettings.ValidationTimeoutSeconds != 0
                ? environmentSettings.ValidationTimeoutSeconds
                : baseSettings.ValidationTimeoutSeconds,
            SeverityFilter = environmentSettings.SeverityFilter ?? baseSettings.SeverityFilter
        };
    }

    private static InvariantConfiguration CreateDefaultConfiguration()
    {
        return new InvariantConfiguration
        {
            InvariantGroups = new Dictionary<string, InvariantGroupDefinition>(),
            Settings = new InvariantSettings()
        };
    }
}

/// <summary>
///     Configuration options for invariant loading
/// </summary>
public class InvariantConfigurationOptions
{
    public const string SectionName = "InvariantConfiguration";

    public string ConfigurationPath { get; set; } = "Configuration/InvariantDefinitions.yaml";
    public bool EnableFileWatching { get; set; } = true;
    public bool ValidateOnLoad { get; set; } = true;
    public string Environment { get; set; } = "development";
}

/// <summary>
///     Factory for creating configurable invariants from YAML definitions
/// </summary>
public class ConfigurableInvariantFactory(
    ICustomRuleEngine customRuleEngine,
    ILogger<ConfigurableInvariantFactory> logger)
{
    /// <summary>
    ///     Create invariants from configuration for a specific type
    /// </summary>
    public List<IInvariant<T>> CreateInvariants<T>(InvariantGroupDefinition groupDefinition)
    {
        var invariants = new List<IInvariant<T>>();

        foreach (var definition in groupDefinition.Invariants)
        {
            try
            {
                var invariant = new ConfigurableInvariant<T>(definition, customRuleEngine);
                invariants.Add(invariant);

                logger.LogDebug("Created configurable invariant: {InvariantName} for type {TypeName}",
                    definition.Name, typeof(T).Name);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error creating invariant {InvariantName} for type {TypeName}",
                    definition.Name, typeof(T).Name);
            }
        }

        logger.LogInformation("Created {InvariantCount} configurable invariants for type {TypeName}",
            invariants.Count, typeof(T).Name);

        return invariants;
    }

    /// <summary>
    ///     Create all invariants from configuration
    /// </summary>
    public Dictionary<Type, List<object>> CreateAllInvariants(InvariantConfiguration configuration)
    {
        var allInvariants = new Dictionary<Type, List<object>>();

        foreach (var (groupName, groupDefinition) in configuration.InvariantGroups)
        {
            var targetType = GetTypeFromName(groupDefinition.TargetType);
            if (targetType == null)
            {
                logger.LogWarning("Unknown target type: {TargetType} for group {GroupName}",
                    groupDefinition.TargetType, groupName);
                continue;
            }

            try
            {
                var createMethod = typeof(ConfigurableInvariantFactory)
                    .GetMethod(nameof(CreateInvariants))!
                    .MakeGenericMethod(targetType);

                var invariants = (IList)createMethod.Invoke(this, [groupDefinition])!;

                allInvariants[targetType] = [.. invariants.Cast<object>()];
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error creating invariants for group {GroupName}", groupName);
            }
        }

        return allInvariants;
    }

    private static Type? GetTypeFromName(string typeName)
    {
        return typeName switch
        {
            "IconicChordDefinition" => typeof(IconicChordDefinition),
            "ChordProgressionDefinition" => typeof(ChordProgressionDefinition),
            "GuitarTechniqueDefinition" => typeof(GuitarTechniqueDefinition),
            "SpecializedTuningDefinition" => typeof(SpecializedTuningDefinition),
            _ => null
        };
    }
}
