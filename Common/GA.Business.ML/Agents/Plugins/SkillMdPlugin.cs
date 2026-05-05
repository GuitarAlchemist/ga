namespace GA.Business.ML.Agents.Plugins;

using GA.Business.ML.Agents.Skills;
using GA.Business.ML.Extensions;
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
    public void Register(IServiceCollection services) => Register(services, ResolveSkillsPath());

    /// <summary>
    /// Internal overload that takes an explicit skills path — used by unit
    /// tests so they can register against an isolated temp directory rather
    /// than mutating process env vars or the repo's real <c>skills/</c> tree.
    /// </summary>
    internal void Register(IServiceCollection services, string path)
    {
        if (!Directory.Exists(path))
        {
            // Warning is emitted via Trace (NOT Debug — Debug.WriteLine is
            // [Conditional("DEBUG")] and compiles out of Release builds, which
            // would silently drop this migration-critical warning in production).
            // ILogger is not available during DI registration, so Trace is the
            // fallback. Test fixture asserts via a Trace.Listeners hook.
            System.Diagnostics.Trace.WriteLine(
                $"[SkillMdPlugin] Skills directory not found: {path} — no SKILL.md skills registered.");
            return;
        }

        var skills = SkillMdLoader.LoadFromDirectory(path);
        if (skills.Count == 0)
        {
            // Migration footgun: an empty canonical `skills/` would silently
            // shadow a populated `.agent/skills/` since the resolver returns
            // the canonical path first. Detect that case and shout, so a fresh
            // `mkdir skills` doesn't drop every unported skill from production.
            var legacy = LegacyAgentSkillsPath(path);
            var legacySkillCount = legacy is not null
                ? SkillMdLoader.LoadFromDirectory(legacy).Count
                : 0;

            if (legacySkillCount > 0)
            {
                System.Diagnostics.Trace.WriteLine(
                    $"[SkillMdPlugin] Canonical '{path}' is empty but legacy '{legacy}' " +
                    $"still has {legacySkillCount} SKILL.md file(s). Production will register ZERO " +
                    $"skills. Either port them into the canonical directory or remove the empty " +
                    $"canonical directory to fall through to legacy.");
            }
            else
            {
                System.Diagnostics.Trace.WriteLine(
                    $"[SkillMdPlugin] No SKILL.md files with triggers found in {path}.");
            }
            return;
        }

        // IMcpToolsProvider is registered as a singleton by ChatPluginHost after this plugin runs.
        // IChatClientFactory is registered by AddGuitarAlchemistAi.
        // Each SkillMdDrivenSkill resolves the chat client lazily on first ExecuteAsync call.
        foreach (var skill in skills)
        {
            var captured = skill;
            services.AddSingleton<IOrchestratorSkill>(sp =>
                new SkillMdDrivenSkill(
                    captured,
                    sp.GetRequiredService<IMcpToolsProvider>(),
                    sp.GetRequiredService<IChatClientFactory>(),
                    sp.GetRequiredService<ILogger<SkillMdDrivenSkill>>()));
        }
    }

    /// <summary>MCP tool types contributed by this plugin (none — uses shared <see cref="IMcpToolsProvider"/>).</summary>
    public IReadOnlyList<Type> McpToolTypes => [];

    /// <summary>
    /// Anchors at the repo root (<c>.git</c> marker), then prefers the canonical
    /// <c>skills/</c> directory and falls back to the legacy <c>.agent/skills/</c>
    /// directory when the canonical one is missing. <c>skills/</c> is the
    /// one-way-door canonical home per
    /// <c>docs/plans/2026-05-03-chatbot-agent-framework-migration-recommendation.md</c>;
    /// the legacy probe stays until every SKILL.md migrates so unported skills
    /// don't disappear from production at flip-time.
    /// </summary>
    /// <param name="startDir">
    /// Anchor for repo-root discovery and env-var traversal validation.
    /// Defaults to <see cref="AppContext.BaseDirectory"/>; tests pass a temp
    /// path so they can exercise the resolver without touching the real repo.
    /// </param>
    /// <remarks>Internal so unit tests can re-anchor the search at a temp directory.</remarks>
    internal static string ResolveSkillsPath(string? startDir = null)
    {
        var anchor = startDir ?? AppContext.BaseDirectory;

        // 1. Explicit env var override — validated against the repo root to prevent
        //    path-traversal attacks (a compromised env var pointing to /etc/SKILL.md
        //    would otherwise inject arbitrary system prompts into every LLM call).
        var env = Environment.GetEnvironmentVariable("SKILLMD_SKILLS_PATH");
        if (!string.IsNullOrWhiteSpace(env))
        {
            var resolved = Path.GetFullPath(env);
            var rootForOverride = FindRepoRoot(anchor);
            if (rootForOverride is not null &&
                IsPathInsideDirectory(resolved, rootForOverride))
                return resolved;

            System.Diagnostics.Trace.WriteLine(
                $"[SkillMdPlugin] SKILLMD_SKILLS_PATH '{env}' resolves to '{resolved}' " +
                $"which is outside the repo root '{rootForOverride}' — ignoring.");
        }

        // 2. Anchor at the repo root, then probe canonical → legacy.
        var repoRoot = FindRepoRoot(anchor);
        if (repoRoot is not null)
        {
            var canonical = Path.Combine(repoRoot, "skills");
            if (Directory.Exists(canonical)) return canonical;

            var legacy = Path.Combine(repoRoot, ".agent", "skills");
            if (Directory.Exists(legacy)) return legacy;

            // Neither exists — return the canonical path so Register()'s
            // missing-dir warning points at the right authoring location.
            return canonical;
        }

        // 3. No .git anchor reachable — fall back to a CWD-relative canonical path.
        return Path.Combine(Directory.GetCurrentDirectory(), "skills");
    }

    /// <summary>
    /// Returns the legacy <c>.agent/skills</c> directory that pairs with the
    /// canonical <paramref name="canonicalPath"/>, or <c>null</c> if it can't
    /// be derived. Used to detect the empty-canonical / populated-legacy footgun
    /// during migration.
    /// </summary>
    private static string? LegacyAgentSkillsPath(string canonicalPath)
    {
        var parent = Path.GetDirectoryName(canonicalPath);
        if (string.IsNullOrEmpty(parent)) return null;
        var legacy = Path.Combine(parent, ".agent", "skills");
        return Directory.Exists(legacy) ? legacy : null;
    }

    /// <summary>
    /// Path-prefix check that defends against prefix-collision attacks on
    /// <c>StartsWith</c> — e.g. <c>C:\repo</c> matching <c>C:\repo-evil</c>.
    /// Both arguments are normalized via <see cref="Path.GetFullPath(string)"/>
    /// and the directory side is suffixed with the platform separator before
    /// the case-insensitive prefix check.
    /// </summary>
    /// <remarks>
    /// Note: this is a lexical check. It does NOT dereference NTFS junctions /
    /// reparse points / symlinks — that's a separate hardening pass tracked in
    /// the migration recommendation's followups.
    /// </remarks>
    private static bool IsPathInsideDirectory(string candidatePath, string directory)
    {
        var candidateFull = Path.GetFullPath(candidatePath);
        var dirFull       = Path.GetFullPath(directory);
        if (!dirFull.EndsWith(Path.DirectorySeparatorChar) &&
            !dirFull.EndsWith(Path.AltDirectorySeparatorChar))
        {
            dirFull += Path.DirectorySeparatorChar;
        }
        return candidateFull.StartsWith(dirFull, StringComparison.OrdinalIgnoreCase);
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
