namespace GaCLI.Commands;

using GaCLI.Plugins;
using static System.Console;

/// <summary>
///     MVP scaffold for ga plugin command surface.
///     Command contracts follow docs/architecture/AGENT_MARKETPLACE_MVP_SPEC.md.
/// </summary>
public sealed class PluginCommand
{
    private static readonly PluginSettingsStore SettingsStore = new();

    public Task ExecuteAsync(string[] args)
    {
        if (args.Length < 2 || IsHelp(args[1]))
        {
            PrintRootHelp();
            return Task.CompletedTask;
        }

        var command = args[1].ToLowerInvariant();
        var tail = args.Skip(2).ToArray();

        return command switch
        {
            "marketplace" => ExecuteMarketplaceAsync(tail),
            "search" => ExecuteSearchAsync(tail),
            "install" => ExecuteInstallAsync(tail),
            "list" => ExecuteListAsync(tail),
            "info" => ExecuteInfoAsync(tail),
            "enable" => ExecuteEnableDisableAsync("enable", tail),
            "disable" => ExecuteEnableDisableAsync("disable", tail),
            "uninstall" => ExecuteUninstallAsync(tail),
            "update" => ExecuteUpdateAsync(tail),
            "doctor" => ExecuteDoctorAsync(tail),
            "status" => ExecuteStatusAsync(tail),
            "trust" => ExecuteTrustAsync(tail),
            _ => ExecuteUnknownAsync(command)
        };
    }

    private static async Task ExecuteStatusAsync(string[] args)
    {
        WriteLine("[ga plugin] System Status:");
        
        var userInstalled = SettingsStore.LoadInstalledPlugins(PluginSettingsScope.User);
        var projectInstalled = SettingsStore.LoadInstalledPlugins(PluginSettingsScope.Project);
        
        WriteLine($"  Installed Plugins (User):    {userInstalled.Count}");
        WriteLine($"  Installed Plugins (Project): {projectInstalled.Count}");
        
        var userMarketplaces = SettingsStore.LoadMarketplaces(PluginSettingsScope.User);
        var projectMarketplaces = SettingsStore.LoadMarketplaces(PluginSettingsScope.Project);
        
        WriteLine($"  Active Marketplaces:         {userMarketplaces.Count + projectMarketplaces.Count}");
        
        await Task.CompletedTask;
    }

    private static Task ExecuteMarketplaceAsync(string[] args)
    {
        if (args.Length == 0 || IsHelp(args[0]))
        {
            WriteLine("Usage:");
            WriteLine("  ga plugin marketplace add <source> [--name <name>] [--scope user|project]");
            WriteLine("  ga plugin marketplace list [--scope all|user|project]");
            WriteLine("  ga plugin marketplace update <marketplace-id|--all>");
            WriteLine("  ga plugin marketplace remove <marketplace-id> [--scope user|project]");
            return Task.CompletedTask;
        }

        return args[0].ToLowerInvariant() switch
        {
            "add" => ExecuteMarketplaceAddAsync(args),
            "list" => ExecuteMarketplaceListAsync(args),
            "update" => ExecuteMarketplaceUpdateAsync(args),
            "remove" => ExecuteMarketplaceRemoveAsync(args),
            _ => ExecuteUsageErrorAsync($"Unknown marketplace subcommand '{args[0]}'.")
        };
    }

    private static Task ExecuteMarketplaceAddAsync(string[] args)
    {
        if (args.Length < 2)
        {
            return ExecuteUsageErrorAsync("marketplace add requires <source>.");
        }

        var source = args[1].Trim();
        if (source.Length == 0)
        {
            return ExecuteUsageErrorAsync("marketplace add requires non-empty <source>.");
        }

        var scopeToken = GetOptionValue(args, "--scope") ?? "user";
        if (!TryParseWriteScope(scopeToken, out var scope))
        {
            return ExecuteUsageErrorAsync("marketplace add --scope must be user or project.");
        }

        var settingsPath = SettingsStore.GetSettingsPath(scope);
        var name = GetOptionValue(args, "--name") ?? InferNameFromSource(source);

        var marketplaces = SettingsStore.LoadMarketplaces(scope).ToList();
        var existingBySource = marketplaces
            .FindIndex(item => item.Source.Equals(source, StringComparison.OrdinalIgnoreCase));
        var now = DateTime.UtcNow;

        if (existingBySource >= 0)
        {
            var existing = marketplaces[existingBySource];
            marketplaces[existingBySource] = existing with
            {
                Name = name,
                UpdatedUtc = now
            };

            SettingsStore.SaveMarketplaces(scope, marketplaces);
            WriteLine($"[ga plugin] Updated marketplace '{existing.Id}' in {scopeToken} scope.");
            WriteLine($"[ga plugin] settings: {settingsPath}");
            return Task.CompletedTask;
        }

        var marketplaceId = CreateUniqueMarketplaceId(
            marketplaces,
            BuildMarketplaceId(name, source));

        marketplaces.Add(new MarketplaceRegistration
        {
            Id = marketplaceId,
            Name = name,
            Source = source,
            CreatedUtc = now,
            UpdatedUtc = now
        });

        SettingsStore.SaveMarketplaces(scope, marketplaces);
        WriteLine($"[ga plugin] Added marketplace '{marketplaceId}' in {scopeToken} scope.");
        WriteLine($"[ga plugin] settings: {settingsPath}");
        return Task.CompletedTask;
    }

    private static Task ExecuteMarketplaceListAsync(string[] args)
    {
        var scopeToken = GetOptionValue(args, "--scope") ?? "all";
        if (!TryParseListScope(scopeToken, out var listScope))
        {
            return ExecuteUsageErrorAsync("marketplace list --scope must be all, user, or project.");
        }

        if (listScope is MarketplaceListScope.All or MarketplaceListScope.User)
        {
            PrintMarketplaceScope(PluginSettingsScope.User, "user");
        }

        if (listScope is MarketplaceListScope.All or MarketplaceListScope.Project)
        {
            PrintMarketplaceScope(PluginSettingsScope.Project, "project");
        }

        return Task.CompletedTask;
    }

    private static Task ExecuteMarketplaceRemoveAsync(string[] args)
    {
        if (args.Length < 2)
        {
            return ExecuteUsageErrorAsync("marketplace remove requires <marketplace-id>.");
        }

        var marketplaceId = args[1];
        var scopeToken = GetOptionValue(args, "--scope") ?? "user";
        if (!TryParseWriteScope(scopeToken, out var scope))
        {
            return ExecuteUsageErrorAsync("marketplace remove --scope must be user or project.");
        }

        var marketplaces = SettingsStore.LoadMarketplaces(scope).ToList();
        var removed = marketplaces.RemoveAll(item =>
            item.Id.Equals(marketplaceId, StringComparison.OrdinalIgnoreCase));

        if (removed == 0)
        {
            WriteLine($"[ga plugin] Marketplace '{marketplaceId}' not found in {scopeToken} scope.");
            return Task.CompletedTask;
        }

        SettingsStore.SaveMarketplaces(scope, marketplaces);
        WriteLine($"[ga plugin] Removed marketplace '{marketplaceId}' from {scopeToken} scope.");
        WriteLine($"[ga plugin] settings: {SettingsStore.GetSettingsPath(scope)}");
        return Task.CompletedTask;
    }

    private static Task ExecuteMarketplaceUpdateAsync(string[] args)
    {
        if (args.Length < 2)
        {
            return ExecuteUsageErrorAsync("marketplace update requires <marketplace-id|--all>.");
        }

        var target = args[1].Trim();
        if (target.Length == 0)
        {
            return ExecuteUsageErrorAsync("marketplace update requires non-empty <marketplace-id|--all>.");
        }

        var now = DateTime.UtcNow;
        PluginSettingsScope[] scopes = [PluginSettingsScope.User, PluginSettingsScope.Project];
        var totalUpdated = 0;

        foreach (var scope in scopes)
        {
            var marketplaces = SettingsStore.LoadMarketplaces(scope).ToList();
            if (marketplaces.Count == 0)
            {
                continue;
            }

            List<int> matches = target.Equals("--all", StringComparison.OrdinalIgnoreCase)
                ? [..marketplaces.Select((_, index) => index)]
                : [..marketplaces
                    .Select((item, index) => (item, index))
                    .Where(tuple => tuple.item.Id.Equals(target, StringComparison.OrdinalIgnoreCase))
                    .Select(tuple => tuple.index)
                ];

            if (matches.Count == 0)
            {
                continue;
            }

            foreach (var index in matches)
            {
                var existing = marketplaces[index];
                marketplaces[index] = existing with { UpdatedUtc = now };
            }

            SettingsStore.SaveMarketplaces(scope, marketplaces);
            totalUpdated += matches.Count;
            WriteLine($"[ga plugin] Updated {matches.Count} marketplace(s) in {GetScopeLabel(scope)} scope.");
            WriteLine($"[ga plugin] settings: {SettingsStore.GetSettingsPath(scope)}");
        }

        if (totalUpdated == 0)
        {
            var noun = target.Equals("--all", StringComparison.OrdinalIgnoreCase)
                ? "marketplaces"
                : $"marketplace '{target}'";
            WriteLine($"[ga plugin] No {noun} found in user/project scopes.");
            return Task.CompletedTask;
        }

        WriteLine($"[ga plugin] marketplace update completed ({totalUpdated} total).");
        return Task.CompletedTask;
    }

    private static void PrintMarketplaceScope(PluginSettingsScope scope, string label)
    {
        var marketplaces = SettingsStore.LoadMarketplaces(scope);
        var settingsPath = SettingsStore.GetSettingsPath(scope);

        WriteLine($"[{label}] {settingsPath}");
        if (marketplaces.Count == 0)
        {
            WriteLine("  (no marketplaces)");
            return;
        }

        foreach (var item in marketplaces.OrderBy(item => item.Id, StringComparer.OrdinalIgnoreCase))
        {
            WriteLine($"  - {item.Id} | {item.Name} | {item.Source}");
        }
    }

    private static void PrintInstalledScope(PluginSettingsScope scope, string label)
    {
        var installed = SettingsStore.LoadInstalledPlugins(scope);
        var settingsPath = SettingsStore.GetSettingsPath(scope);

        WriteLine($"[{label}] {settingsPath}");
        if (installed.Count == 0)
        {
            WriteLine("  (no installed plugins)");
            return;
        }

        foreach (var item in installed.OrderBy(item => item.Id, StringComparer.OrdinalIgnoreCase))
        {
            WriteLine($"  - {item.Id} | {item.Version} | {item.Reference}");
        }
    }

    private static async Task ExecuteSearchAsync(string[] args)
    {
        if (args.Length < 1)
        {
            await ExecuteUsageErrorAsync("plugin search requires <query>.");
            return;
        }

        var query = string.Join(" ", args.Where(arg => !arg.StartsWith("--", StringComparison.Ordinal)));
        var limitStr = GetOptionValue(args, "--limit") ?? "10";
        int.TryParse(limitStr, out var limit);

        WriteLine($"[ga plugin] Searching marketplaces for '{query}'...");
        
        var marketplace = new PluginMarketplaceService("http://localhost:5000");
        var results = await marketplace.SearchPluginsAsync(query, limit);

        if (results.Count == 0)
        {
            WriteLine("  No plugins found matching your query.");
            return;
        }

        foreach (var plugin in results)
        {
            WriteLine($"\n  {plugin.Name} ({plugin.Id}) v{plugin.Version}");
            WriteLine($"    {plugin.Description}");
            WriteLine($"    Author: {plugin.Author} | Tags: {string.Join(", ", plugin.Tags)}");
        }
        
        WriteLine($"\n[ga plugin] Found {results.Count} results.");
    }

    private static Task ExecuteInstallAsync(string[] args)
    {
        if (args.Length < 1)
        {
            return ExecuteUsageErrorAsync("plugin install requires <plugin-ref>.");
        }

        var pluginReference = args[0].Trim();
        if (pluginReference.Length == 0)
        {
            return ExecuteUsageErrorAsync("plugin install requires non-empty <plugin-ref>.");
        }

        var scopeToken = GetOptionValue(args, "--scope") ?? "user";
        if (!TryParseInstallScope(scopeToken, out var scope))
        {
            return ExecuteUsageErrorAsync("plugin install --scope must be user, project, or local.");
        }

        var requestedVersion = GetOptionValue(args, "--version") ?? "latest";
        var version = requestedVersion.Trim().Length == 0 ? "latest" : requestedVersion.Trim();
        var pluginId = BuildPluginId(pluginReference);

        var now = DateTime.UtcNow;
        var installed = SettingsStore.LoadInstalledPlugins(scope).ToList();
        var existingIndex = installed.FindIndex(item =>
            item.Id.Equals(pluginId, StringComparison.OrdinalIgnoreCase));

        if (existingIndex >= 0)
        {
            var existing = installed[existingIndex];
            installed[existingIndex] = existing with
            {
                Reference = pluginReference,
                Version = version,
                UpdatedUtc = now
            };

            SettingsStore.SaveInstalledPlugins(scope, installed);
            WriteLine($"[ga plugin] Updated installed plugin '{pluginId}' in {scopeToken} scope.");
            WriteLine($"[ga plugin] settings: {SettingsStore.GetSettingsPath(scope)}");
            return Task.CompletedTask;
        }

        installed.Add(new InstalledPluginRegistration
        {
            Id = pluginId,
            Reference = pluginReference,
            Version = version,
            InstalledUtc = now,
            UpdatedUtc = now
        });

        SettingsStore.SaveInstalledPlugins(scope, installed);
        WriteLine($"[ga plugin] Installed '{pluginId}' ({version}) in {scopeToken} scope.");
        WriteLine($"[ga plugin] settings: {SettingsStore.GetSettingsPath(scope)}");
        return Task.CompletedTask;
    }

    private static Task ExecuteListAsync(string[] args)
    {
        var scopeToken = GetOptionValue(args, "--scope") ?? "all";
        if (!TryParsePluginListScope(scopeToken, out var listScope))
        {
            return ExecuteUsageErrorAsync("plugin list --scope must be all, user, project, or local.");
        }

        if (listScope is PluginListScope.All or PluginListScope.User)
        {
            PrintInstalledScope(PluginSettingsScope.User, "user");
        }

        if (listScope is PluginListScope.All or PluginListScope.Project)
        {
            PrintInstalledScope(PluginSettingsScope.Project, "project");
        }

        if (listScope is PluginListScope.All or PluginListScope.Local)
        {
            PrintInstalledScope(PluginSettingsScope.Local, "local");
        }

        return Task.CompletedTask;
    }

    private static async Task ExecuteInfoAsync(string[] args)
    {
        if (args.Length < 1)
        {
            await ExecuteUsageErrorAsync("plugin info requires <plugin-id>.");
            return;
        }

        var pluginId = args[0];
        WriteLine($"[ga plugin] Fetching details for '{pluginId}'...");
        
        // In a real scenario, this would call /api/plugins/{id}
        // For MVP, we'll just mock it or search by ID
        var marketplace = new PluginMarketplaceService("http://localhost:5000");
        var results = await marketplace.SearchPluginsAsync(pluginId, 1);
        var plugin = results.FirstOrDefault();

        if (plugin == null)
        {
            WriteLine($"  [ERR] Plugin '{pluginId}' not found.");
            return;
        }

        WriteLine($"\n  Name:        {plugin.Name}");
        WriteLine($"  ID:          {plugin.Id}");
        WriteLine($"  Version:     {plugin.Version}");
        WriteLine($"  Author:      {plugin.Author}");
        WriteLine($"  Tags:        {string.Join(", ", plugin.Tags)}");
        WriteLine($"  Description: {plugin.Description}");
        
        WriteLine("\n  Manifest Highlights:");
        WriteLine("    - Commands:  /theory, /scales");
        WriteLine("    - Permissions: filesystem.read, network.access");
    }

    private static Task ExecuteEnableDisableAsync(string verb, string[] args)
    {
        if (args.Length < 1)
        {
            return ExecuteUsageErrorAsync($"plugin {verb} requires <plugin-id>.");
        }

        var scope = GetOptionValue(args, "--scope") ?? "user";
        return ExecuteScaffoldAsync(verb, $"plugin={args[0]}, scope={scope}");
    }

    private static Task ExecuteUninstallAsync(string[] args)
    {
        if (args.Length < 1)
        {
            return ExecuteUsageErrorAsync("plugin uninstall requires <plugin-id>.");
        }

        var pluginId = args[0];
        var scopeToken = GetOptionValue(args, "--scope") ?? "user";
        if (!TryParseInstallScope(scopeToken, out var scope))
        {
            return ExecuteUsageErrorAsync("plugin uninstall --scope must be user, project, or local.");
        }

        var installed = SettingsStore.LoadInstalledPlugins(scope).ToList();
        var removed = installed.RemoveAll(item =>
            item.Id.Equals(pluginId, StringComparison.OrdinalIgnoreCase) ||
            item.Reference.Equals(pluginId, StringComparison.OrdinalIgnoreCase));

        if (removed == 0)
        {
            WriteLine($"[ga plugin] Plugin '{pluginId}' is not installed in {scopeToken} scope.");
            return Task.CompletedTask;
        }

        SettingsStore.SaveInstalledPlugins(scope, installed);
        WriteLine($"[ga plugin] Uninstalled '{pluginId}' from {scopeToken} scope.");
        WriteLine($"[ga plugin] settings: {SettingsStore.GetSettingsPath(scope)}");
        return Task.CompletedTask;
    }

    private static Task ExecuteUpdateAsync(string[] args)
    {
        if (args.Length < 1)
        {
            return ExecuteUsageErrorAsync("plugin update requires <plugin-id|--all>.");
        }

        var scope = GetOptionValue(args, "--scope") ?? "user";
        return ExecuteScaffoldAsync("update", $"target={args[0]}, scope={scope}");
    }

    private static async Task ExecuteDoctorAsync(string[] args)
    {
        WriteLine("[ga plugin] Running system diagnostics...");
        
        // 1. Directory Checks
        CheckDirectory(".ga", "Project data directory");
        CheckFile(SettingsStore.GetSettingsPath(PluginSettingsScope.User), "User settings");
        CheckFile(SettingsStore.GetSettingsPath(PluginSettingsScope.Project), "Project settings");

        // 2. Connectivity Checks (Heuristic)
        WriteLine("\nChecking service connectivity:");
        await CheckService("GaApi", "http://localhost:5000/api/health");
        await CheckService("MongoDB", "mongodb://localhost:27017", isMongo: true);
        
        WriteLine("\n[ga plugin] Diagnostics complete.");
    }

    private static void CheckDirectory(string path, string label)
    {
        if (Directory.Exists(path))
            WriteLine($"  [OK] {label} exists ({path})");
        else
            WriteLine($"  [WARN] {label} not found ({path})");
    }

    private static void CheckFile(string path, string label)
    {
        if (File.Exists(path))
            WriteLine($"  [OK] {label} file exists");
        else
            WriteLine($"  [INFO] {label} file not present (using defaults)");
    }

    private static async Task CheckService(string name, string connectionString, bool isMongo = false)
    {
        try
        {
            if (isMongo)
            {
                // Simple TCP check for MongoDB port
                using var client = new System.Net.Sockets.TcpClient();
                var task = client.ConnectAsync("localhost", 27017);
                if (await Task.WhenAny(task, Task.Delay(1000)) == task && client.Connected)
                    WriteLine($"  [OK] {name} is reachable");
                else
                    WriteLine($"  [FAIL] {name} is NOT reachable on 27017");
            }
            else
            {
                using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(2) };
                var response = await http.GetAsync(connectionString);
                if (response.IsSuccessStatusCode)
                    WriteLine($"  [OK] {name} is healthy");
                else
                    WriteLine($"  [FAIL] {name} returned status {response.StatusCode}");
            }
        }
        catch (Exception ex)
        {
            WriteLine($"  [FAIL] {name} check failed: {ex.Message}");
        }
    }

    private static Task ExecuteTrustAsync(string[] args)
    {
        if (args.Length == 0 || IsHelp(args[0]))
        {
            WriteLine("Usage:");
            WriteLine("  ga plugin trust list");
            WriteLine("  ga plugin trust add <publisher-or-key-id>");
            WriteLine("  ga plugin trust remove <publisher-or-key-id>");
            return Task.CompletedTask;
        }

        return args[0].ToLowerInvariant() switch
        {
            "list" => ExecuteScaffoldAsync("trust list"),
            "add" => args.Length < 2
                ? ExecuteUsageErrorAsync("plugin trust add requires <publisher-or-key-id>.")
                : ExecuteScaffoldAsync("trust add", $"subject={args[1]}"),
            "remove" => args.Length < 2
                ? ExecuteUsageErrorAsync("plugin trust remove requires <publisher-or-key-id>.")
                : ExecuteScaffoldAsync("trust remove", $"subject={args[1]}"),
            _ => ExecuteUsageErrorAsync($"Unknown trust subcommand '{args[0]}'.")
        };
    }

    private static Task ExecuteUnknownAsync(string command) =>
        ExecuteUsageErrorAsync($"Unknown plugin subcommand '{command}'.");

    private static Task ExecuteScaffoldAsync(string command, string? details = null)
    {
        WriteLine($"[ga plugin] '{command}' command recognized.");
        if (!string.IsNullOrWhiteSpace(details))
        {
            WriteLine($"[ga plugin] args: {details}");
        }

        WriteLine("[ga plugin] Not implemented yet. This is command-contract scaffold only.");
        return Task.CompletedTask;
    }

    private static Task ExecuteUsageErrorAsync(string message)
    {
        WriteLine($"[ga plugin] {message}");
        WriteLine("[ga plugin] Run 'ga plugin --help' for available commands.");
        return Task.CompletedTask;
    }

    private static void PrintRootHelp()
    {
        WriteLine("ga plugin command surface (MVP scaffold):");
        WriteLine("  ga plugin marketplace add <source> [--name <name>] [--scope user|project]");
        WriteLine("  ga plugin marketplace list [--scope all|user|project]");
        WriteLine("  ga plugin marketplace update <marketplace-id|--all>");
        WriteLine("  ga plugin marketplace remove <marketplace-id> [--scope user|project]");
        WriteLine("  ga plugin search <query> [--marketplace <id>] [--limit 20]");
        WriteLine("  ga plugin install <plugin-ref> [--scope user|project|local] [--version <semver>]");
        WriteLine("  ga plugin list [--scope all|user|project|local]");
        WriteLine("  ga plugin info <plugin-id>");
        WriteLine("  ga plugin enable <plugin-id> [--scope user|project|local]");
        WriteLine("  ga plugin disable <plugin-id> [--scope user|project|local]");
        WriteLine("  ga plugin uninstall <plugin-id> [--scope user|project|local]");
        WriteLine("  ga plugin update <plugin-id|--all> [--scope user|project|local]");
        WriteLine("  ga plugin doctor");
        WriteLine("  ga plugin status");
        WriteLine("  ga plugin trust list");
        WriteLine("  ga plugin trust add <publisher-or-key-id>");
        WriteLine("  ga plugin trust remove <publisher-or-key-id>");
    }

    private static bool IsHelp(string token) =>
        token.Equals("--help", StringComparison.OrdinalIgnoreCase) ||
        token.Equals("-h", StringComparison.OrdinalIgnoreCase) ||
        token.Equals("help", StringComparison.OrdinalIgnoreCase);

    private static string GetScopeLabel(PluginSettingsScope scope) => scope switch
    {
        PluginSettingsScope.User => "user",
        PluginSettingsScope.Project => "project",
        PluginSettingsScope.Local => "local",
        _ => "unknown"
    };

    private static string BuildMarketplaceId(string name, string source)
    {
        var seed = $"{name}-{source}"
            .ToLowerInvariant()
            .Replace("https://", string.Empty, StringComparison.Ordinal)
            .Replace("http://", string.Empty, StringComparison.Ordinal)
            .Replace("ssh://", string.Empty, StringComparison.Ordinal)
            .Replace("git@", string.Empty, StringComparison.Ordinal)
            .Replace(".git", string.Empty, StringComparison.Ordinal);

        var cleaned = new string([..seed.Select(ch => char.IsLetterOrDigit(ch) ? ch : '-')]);

        var collapsed = string.Join(
            "-",
            cleaned.Split('-', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));

        return collapsed.Length == 0 ? "marketplace" : collapsed;
    }

    private static string BuildPluginId(string pluginReference)
    {
        var trimmed = pluginReference.Trim();
        if (trimmed.Length == 0)
        {
            return "plugin";
        }

        if (Uri.TryCreate(trimmed, UriKind.Absolute, out _))
        {
            return BuildMarketplaceId("plugin", trimmed);
        }

        var cleaned = new string([..trimmed
            .ToLowerInvariant()
            .Select(ch => char.IsLetterOrDigit(ch) || ch is '.' or '-' or '_' ? ch : '-')]);

        var collapsed = string.Join(
            "-",
            cleaned.Split('-', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));

        return collapsed.Length == 0 ? "plugin" : collapsed;
    }

    private static string CreateUniqueMarketplaceId(
        IEnumerable<MarketplaceRegistration> marketplaces,
        string baseId)
    {
        var existingIds = marketplaces
            .Select(item => item.Id)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        if (!existingIds.Contains(baseId))
        {
            return baseId;
        }

        for (var i = 2; i < 1000; i++)
        {
            var candidate = $"{baseId}-{i}";
            if (!existingIds.Contains(candidate))
            {
                return candidate;
            }
        }

        return $"{baseId}-{Guid.NewGuid():N}";
    }

    private static string InferNameFromSource(string source)
    {
        if (Uri.TryCreate(source, UriKind.Absolute, out var uri))
        {
            var path = uri.AbsolutePath.Trim('/');
            if (path.Length == 0)
            {
                return uri.Host;
            }

            var tail = path.Split('/').Last();
            return tail.Length == 0 ? uri.Host : tail;
        }

        return Path.GetFileNameWithoutExtension(source) switch
        {
            { Length: > 0 } fileName => fileName,
            _ => source
        };
    }

    private static bool TryParseWriteScope(string token, out PluginSettingsScope scope)
    {
        switch (token.ToLowerInvariant())
        {
            case "user":
                scope = PluginSettingsScope.User;
                return true;
            case "project":
                scope = PluginSettingsScope.Project;
                return true;
            default:
                scope = PluginSettingsScope.User;
                return false;
        }
    }

    private static bool TryParseInstallScope(string token, out PluginSettingsScope scope)
    {
        switch (token.ToLowerInvariant())
        {
            case "user":
                scope = PluginSettingsScope.User;
                return true;
            case "project":
                scope = PluginSettingsScope.Project;
                return true;
            case "local":
                scope = PluginSettingsScope.Local;
                return true;
            default:
                scope = PluginSettingsScope.User;
                return false;
        }
    }

    private static bool TryParseListScope(string token, out MarketplaceListScope scope)
    {
        switch (token.ToLowerInvariant())
        {
            case "all":
                scope = MarketplaceListScope.All;
                return true;
            case "user":
                scope = MarketplaceListScope.User;
                return true;
            case "project":
                scope = MarketplaceListScope.Project;
                return true;
            default:
                scope = MarketplaceListScope.All;
                return false;
        }
    }

    private static bool TryParsePluginListScope(string token, out PluginListScope scope)
    {
        switch (token.ToLowerInvariant())
        {
            case "all":
                scope = PluginListScope.All;
                return true;
            case "user":
                scope = PluginListScope.User;
                return true;
            case "project":
                scope = PluginListScope.Project;
                return true;
            case "local":
                scope = PluginListScope.Local;
                return true;
            default:
                scope = PluginListScope.All;
                return false;
        }
    }

    private static string? GetOptionValue(IReadOnlyList<string> args, string option)
    {
        for (var i = 0; i < args.Count - 1; i++)
        {
            if (args[i].Equals(option, StringComparison.OrdinalIgnoreCase))
            {
                return args[i + 1];
            }
        }

        return null;
    }

    private enum MarketplaceListScope
    {
        All,
        User,
        Project
    }

    private enum PluginListScope
    {
        All,
        User,
        Project,
        Local
    }
}
