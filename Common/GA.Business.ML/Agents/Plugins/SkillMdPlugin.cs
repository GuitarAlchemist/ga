namespace GA.Business.ML.Agents.Plugins;

using GA.Business.ML.Agents.Skills;
using GA.Business.ML.Skills;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

/// <summary>
/// <see cref="IChatPlugin"/> that discovers all SKILL.md files with trigger keywords
/// and registers each as a <see cref="SkillMdDrivenSkill"/> backed by Anthropic + GA MCP tools.
///
/// Skills directory defaults to <c>.agent/skills</c> under <see cref="AppContext.BaseDirectory"/>
/// but can be overridden via the <c>SkillMd:SkillsPath</c> environment variable.
/// Files without frontmatter or without <c>triggers</c> are silently ignored.
/// Missing directory → logs a warning, 0 dynamic skills registered.
/// </summary>
[ChatPlugin]
public sealed class SkillMdPlugin : IChatPlugin
{
    public string Name    => "SkillMd";
    public string Version => "1.0";

    /// <remarks>
    /// Design note: this method performs synchronous filesystem I/O (directory check + SKILL.md reads)
    /// at DI registration time. This is intentional for now — SKILL.md files are small and few, so
    /// startup latency is negligible in practice. The tradeoff is that unit-testing plugin registration
    /// requires a real directory tree on disk. A future <c>IHostedService</c>-based approach (see todo 040)
    /// would defer loading and allow full in-memory testing, but adds complexity not currently warranted.
    /// </remarks>
    public void Register(IServiceCollection services)
    {
        var path = ResolveSkillsPath();

        if (!Directory.Exists(path))
        {
            // Warning is emitted at runtime via ILogger — here we use Trace as fallback
            // (ILogger not available during DI registration, so use Debug.WriteLine)
            System.Diagnostics.Debug.WriteLine(
                $"[SkillMdPlugin] Skills directory not found: {path} — no SKILL.md skills registered.");
            return;
        }

        var skills = SkillMdLoader.LoadFromDirectory(path);
        if (skills.Count == 0)
        {
            System.Diagnostics.Debug.WriteLine(
                $"[SkillMdPlugin] No SKILL.md files with triggers found in {path}.");
            return;
        }

        // IMcpToolsProvider is registered as a singleton by ChatPluginHost after this plugin runs.
        // Each SkillMdDrivenSkill resolves it lazily on first ExecuteAsync call.
        foreach (var skill in skills)
        {
            var captured = skill;
            services.AddSingleton<IOrchestratorSkill>(sp =>
                new SkillMdDrivenSkill(
                    captured,
                    sp.GetRequiredService<IMcpToolsProvider>(),
                    sp.GetRequiredService<IConfiguration>(),
                    sp.GetRequiredService<ILogger<SkillMdDrivenSkill>>()));
        }
    }

    /// <summary>MCP tool types contributed by this plugin (none — uses shared <see cref="IMcpToolsProvider"/>).</summary>
    public IReadOnlyList<Type> McpToolTypes => [];

    private static string ResolveSkillsPath()
    {
        // 1. Explicit env var override — validated against the repo root to prevent
        //    path-traversal attacks (a compromised env var pointing to /etc/SKILL.md
        //    would otherwise inject arbitrary system prompts into every LLM call).
        var env = Environment.GetEnvironmentVariable("SKILLMD_SKILLS_PATH");
        if (!string.IsNullOrWhiteSpace(env))
        {
            var resolved = Path.GetFullPath(env);
            var repoRoot = FindRepoRoot(AppContext.BaseDirectory);
            if (repoRoot is not null &&
                resolved.StartsWith(repoRoot, StringComparison.OrdinalIgnoreCase))
                return resolved;

            System.Diagnostics.Debug.WriteLine(
                $"[SkillMdPlugin] SKILLMD_SKILLS_PATH '{env}' resolves to '{resolved}' " +
                $"which is outside the repo root '{repoRoot}' — ignoring.");
        }

        // 2. Crawl up from the binary directory to find the repo root (.git marker)
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            var candidate = Path.Combine(dir.FullName, ".agent", "skills");
            if (Directory.Exists(candidate))
                return candidate;

            // Stop at repo root markers
            if (File.Exists(Path.Combine(dir.FullName, ".git"))
                || Directory.Exists(Path.Combine(dir.FullName, ".git")))
                return candidate; // return even if missing — Register() will log warning

            dir = dir.Parent;
        }

        // 3. Fallback relative to CWD
        return Path.Combine(Directory.GetCurrentDirectory(), ".agent", "skills");
    }

    private static string? FindRepoRoot(string startDir)
    {
        var dir = new DirectoryInfo(startDir);
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, ".git"))
                || Directory.Exists(Path.Combine(dir.FullName, ".git")))
                return dir.FullName;
            dir = dir.Parent;
        }
        return null;
    }
}
