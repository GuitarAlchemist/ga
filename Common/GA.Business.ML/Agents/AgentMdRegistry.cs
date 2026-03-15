namespace GA.Business.ML.Agents;

/// <summary>
/// Singleton registry that discovers and caches all declarative agent <c>.md</c> files
/// from well-known search paths. Lazy-initialized on first access.
/// </summary>
/// <remarks>
/// Search paths (in priority order):
/// <list type="number">
///   <item><c>.agent/agents/</c> relative to the repository root</item>
///   <item><c>~/.ga/agents/</c> (user-level overrides)</item>
/// </list>
/// When multiple files declare the same <see cref="AgentMd.Role"/>, the last one wins
/// (user-level overrides repo-level).
/// </remarks>
public sealed class AgentMdRegistry
{
    private readonly Lazy<IReadOnlyDictionary<string, AgentMd>> _byRole;
    private readonly Lazy<IReadOnlyList<AgentMd>> _all;
    private readonly string[] _searchPaths;

    /// <summary>
    /// Creates a registry that searches the default paths (repo root + user home).
    /// </summary>
    public AgentMdRegistry()
        : this(DefaultSearchPaths())
    {
    }

    /// <summary>
    /// Creates a registry with explicit search directories — useful for testing.
    /// </summary>
    public AgentMdRegistry(params string[] searchPaths)
    {
        _searchPaths = searchPaths;
        _byRole = new Lazy<IReadOnlyDictionary<string, AgentMd>>(LoadByRole);
        _all = new Lazy<IReadOnlyList<AgentMd>>(() => _byRole.Value.Values.ToList().AsReadOnly());
    }

    /// <summary>
    /// Looks up an agent definition by its role (case-insensitive).
    /// Returns <see langword="null"/> when no matching <c>.md</c> file was found.
    /// </summary>
    public AgentMd? TryGetByRole(string role)
    {
        return _byRole.Value.TryGetValue(role.ToLowerInvariant(), out var md) ? md : null;
    }

    /// <summary>
    /// Returns all discovered agent definitions.
    /// </summary>
    public IReadOnlyList<AgentMd> GetAll() => _all.Value;

    // ── Private ──────────────────────────────────────────────────────────────

    private IReadOnlyDictionary<string, AgentMd> LoadByRole()
    {
        var dict = new Dictionary<string, AgentMd>(StringComparer.OrdinalIgnoreCase);

        foreach (var dir in _searchPaths)
        {
            foreach (var agent in AgentMdParser.DiscoverAll(dir))
            {
                // Later paths override earlier ones (user overrides repo)
                dict[agent.Role.ToLowerInvariant()] = agent;
            }
        }

        return dict;
    }

    private static string[] DefaultSearchPaths()
    {
        var paths = new List<string>();

        // 1. Repo root: walk up from assembly location looking for .agent/ directory
        var repoRoot = FindRepoRoot();
        if (repoRoot is not null)
        {
            var repoAgents = Path.Combine(repoRoot, ".agent", "agents");
            paths.Add(repoAgents);
        }

        // 2. User home: ~/.ga/agents/
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (!string.IsNullOrEmpty(home))
        {
            paths.Add(Path.Combine(home, ".ga", "agents"));
        }

        return paths.ToArray();
    }

    private static string? FindRepoRoot()
    {
        // Start from the assembly location and walk up
        var dir = Path.GetDirectoryName(typeof(AgentMdRegistry).Assembly.Location);
        while (dir is not null)
        {
            if (Directory.Exists(Path.Combine(dir, ".agent")))
                return dir;
            dir = Path.GetDirectoryName(dir);
        }

        // Fallback: try current working directory and walk up
        dir = Directory.GetCurrentDirectory();
        while (dir is not null)
        {
            if (Directory.Exists(Path.Combine(dir, ".agent")))
                return dir;
            dir = Path.GetDirectoryName(dir);
        }

        return null;
    }
}
