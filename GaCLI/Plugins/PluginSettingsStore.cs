namespace GaCLI.Plugins;

using System.Text.Json;
using System.Text.Json.Nodes;

internal enum PluginSettingsScope
{
    User,
    Project,
    Local
}

internal sealed record MarketplaceRegistration
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public required string Source { get; init; }
    public required DateTime CreatedUtc { get; init; }
    public required DateTime UpdatedUtc { get; init; }
}

internal sealed record InstalledPluginRegistration
{
    public required string Id { get; init; }
    public required string Reference { get; init; }
    public required string Version { get; init; }
    public required DateTime InstalledUtc { get; init; }
    public required DateTime UpdatedUtc { get; init; }
}

internal sealed class PluginSettingsStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    public string GetSettingsPath(PluginSettingsScope scope) => scope switch
    {
        PluginSettingsScope.User => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".ga",
            "settings.json"),
        PluginSettingsScope.Project => Path.Combine(
            Directory.GetCurrentDirectory(),
            ".ga",
            "settings.json"),
        PluginSettingsScope.Local => Path.Combine(
            Directory.GetCurrentDirectory(),
            ".ga",
            "local",
            "plugins",
            "settings.json"),
        _ => throw new ArgumentOutOfRangeException(nameof(scope), scope, "Unsupported scope.")
    };

    public IReadOnlyList<MarketplaceRegistration> LoadMarketplaces(PluginSettingsScope scope)
    {
        var root = LoadRoot(scope);
        if (root["plugin"] is not JsonObject pluginNode)
        {
            return [];
        }

        var marketplaces = pluginNode["marketplaces"];
        return marketplaces?.Deserialize<List<MarketplaceRegistration>>(JsonOptions) ?? [];
    }

    public IReadOnlyList<InstalledPluginRegistration> LoadInstalledPlugins(PluginSettingsScope scope)
    {
        var root = LoadRoot(scope);
        if (root["plugin"] is not JsonObject pluginNode)
        {
            return [];
        }

        var installed = pluginNode["installed"];
        return installed?.Deserialize<List<InstalledPluginRegistration>>(JsonOptions) ?? [];
    }

    public void SaveMarketplaces(PluginSettingsScope scope, IReadOnlyCollection<MarketplaceRegistration> marketplaces)
    {
        var root = LoadRoot(scope);
        var pluginNode = root["plugin"] as JsonObject ?? [];
        pluginNode["marketplaces"] = JsonSerializer.SerializeToNode(marketplaces, JsonOptions) ?? new JsonArray();
        root["plugin"] = pluginNode;
        SaveRoot(scope, root);
    }

    public void SaveInstalledPlugins(PluginSettingsScope scope, IReadOnlyCollection<InstalledPluginRegistration> installedPlugins)
    {
        var root = LoadRoot(scope);
        var pluginNode = root["plugin"] as JsonObject ?? [];
        pluginNode["installed"] = JsonSerializer.SerializeToNode(installedPlugins, JsonOptions) ?? new JsonArray();
        root["plugin"] = pluginNode;
        SaveRoot(scope, root);
    }

    private void SaveRoot(PluginSettingsScope scope, JsonObject root)
    {
        var settingsPath = GetSettingsPath(scope);
        var directory = Path.GetDirectoryName(settingsPath)
            ?? throw new InvalidOperationException($"Could not determine settings directory for '{settingsPath}'.");

        Directory.CreateDirectory(directory);
        File.WriteAllText(settingsPath, root.ToJsonString(JsonOptions));
    }

    private JsonObject LoadRoot(PluginSettingsScope scope)
    {
        var settingsPath = GetSettingsPath(scope);
        if (!File.Exists(settingsPath))
        {
            return [];
        }

        var json = File.ReadAllText(settingsPath);
        if (string.IsNullOrWhiteSpace(json))
        {
            return [];
        }

        return JsonNode.Parse(json) as JsonObject
            ?? throw new InvalidOperationException($"Settings file '{settingsPath}' must contain a JSON object.");
    }
}
