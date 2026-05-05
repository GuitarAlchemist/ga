namespace GA.Business.ML.Agents.AgentFramework;

using System.Text;
using GA.Business.ML.Skills;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using SystemThreadingTimer = System.Threading.Timer;

/// <summary>
/// Microsoft Agent Framework <see cref="AIContextProvider"/> backed by a
/// directory of <c>SKILL.md</c> files. Forward-compatible substitute for the
/// upcoming <c>Microsoft.Agents.AI.AgentSkillsProvider</c> (referenced in
/// <c>docs/plans/2026-05-03-chatbot-agent-framework-migration-recommendation.md</c>
/// §"C# equivalent of Spring AI generic skills" but not yet shipped in the
/// 1.0.0-preview package GA references).
/// </summary>
/// <remarks>
/// <para><b>Behavior contract:</b></para>
/// <list type="bullet">
///   <item>On construction, scans the supplied directory for <c>SKILL.md</c>
///         files and loads them via <see cref="SkillMdLoader"/>. Files without
///         <c>triggers</c> frontmatter are silently skipped.</item>
///   <item>On each <see cref="InvokingAsync"/> call, inspects the most recent
///         user message: if any loaded skill's trigger keywords match, returns
///         an <see cref="AIContext"/> with the matching skill's body as
///         additional <c>Instructions</c>. Multiple matches are concatenated in
///         load order so the agent sees a deterministic context.</item>
///   <item>Returns <see cref="AIContext.Empty"/> when no triggers match — the
///         agent then proceeds without any skill-supplied context, exactly as
///         it would have without this provider.</item>
///   <item>Read-only: this provider never executes scripts, never writes to
///         disk, and never invokes external services.</item>
/// </list>
/// <para>Forward-compatibility note: when Microsoft ships
/// <c>Microsoft.Agents.AI.AgentSkillsProvider</c>, swap call sites to it and
/// retire this type. The skill directory layout is the same, so SKILL.md files
/// migrate without change. See Phase 2 of the migration recommendation.</para>
/// </remarks>
public sealed class FileBasedSkillsProvider : AIContextProvider, IDisposable
{
    /// <summary>
    /// Debounce window for the FileSystemWatcher. Most editors save by writing
    /// a temp file, deleting the original, then renaming — three events fire
    /// in quick succession for one logical save. 200 ms collapses those into
    /// a single reload while staying well under the human "I edited, try
    /// again" reaction time.
    /// </summary>
    private static readonly TimeSpan ReloadDebounce = TimeSpan.FromMilliseconds(200);

    private readonly IReadOnlyList<string> _skillsDirectories;
    private readonly Action<string> _logWarning;
    private readonly object _reloadLock = new();
    private readonly List<FileSystemWatcher> _watchers = [];
    private readonly SystemThreadingTimer? _debounceTimer;
    private IReadOnlyList<SkillMd> _skills;
    private int _reloadCount;
    private volatile bool _disposed;

    /// <summary>
    /// Construct a provider over <paramref name="skillsDirectory"/>. Missing
    /// directory yields an empty provider (agent runs without skill context).
    /// File-system watching is enabled by default — edits to SKILL.md files
    /// under the directory trigger an automatic reload so authors see their
    /// changes without restarting the host.
    /// </summary>
    public FileBasedSkillsProvider(string skillsDirectory)
        : this(ValidateSingleAndWrap(skillsDirectory), watchForChanges: true)
    {
    }

    /// <summary>
    /// Test/production-control overload. Pass <c>watchForChanges: false</c>
    /// to disable the FileSystemWatcher (e.g. in unit tests that drive
    /// reload deterministically via <see cref="Reload"/>, or in a
    /// production deployment that wants one-shot startup loading).
    /// </summary>
    public FileBasedSkillsProvider(string skillsDirectory, bool watchForChanges)
        : this(ValidateSingleAndWrap(skillsDirectory), watchForChanges)
    {
    }

    /// <summary>
    /// Preserves the original single-directory ctor's <see cref="ArgumentNullException"/>
    /// semantics for null/whitespace input — the multi-dir ctor would
    /// otherwise throw <see cref="ArgumentException"/> later, which is a
    /// breaking API change for callers that catch the specific type.
    /// </summary>
    private static IReadOnlyList<string> ValidateSingleAndWrap(string dir)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(dir);
        return [dir];
    }

    /// <summary>
    /// Multi-directory overload — closes the SKILL.md iteration loop. Lists
    /// directories in priority order: skills loaded from LATER directories
    /// override skills with the same Name from EARLIER ones. Typical use:
    /// pass <c>[canonicalDir, draftsDir]</c> so drafts in
    /// <c>skills-dev/</c> shadow their canonical counterparts in
    /// <c>skills/</c> while iteration is in flight, and graduate by simply
    /// moving the file from the draft dir to the canonical dir (the
    /// override naturally lifts).
    /// </summary>
    /// <param name="skillsDirectories">Watch list. Empty / missing dirs are
    /// silently skipped. At least one must be non-null/non-whitespace.</param>
    /// <param name="watchForChanges">When true, install
    /// <see cref="FileSystemWatcher"/> on each directory. Edits in any of
    /// them trigger a single debounced reload that re-aggregates the merged
    /// skill set.</param>
    public FileBasedSkillsProvider(IReadOnlyList<string> skillsDirectories, bool watchForChanges)
    {
        ArgumentNullException.ThrowIfNull(skillsDirectories);
        if (skillsDirectories.Count == 0 || skillsDirectories.All(string.IsNullOrWhiteSpace))
            throw new ArgumentException(
                "At least one non-empty skills directory must be provided.",
                nameof(skillsDirectories));

        _skillsDirectories = [.. skillsDirectories.Where(d => !string.IsNullOrWhiteSpace(d))];
        // Trace, not Debug — Debug.WriteLine is [Conditional("DEBUG")] and
        // strips out of Release builds, silently dropping warnings in prod.
        _logWarning = msg => System.Diagnostics.Trace.WriteLine(msg);
        _skills = LoadSkillsCore();

        if (!watchForChanges)
            return;

        _debounceTimer = new SystemThreadingTimer(
            _ => Reload(),
            state: null,
            dueTime: Timeout.Infinite,
            period: Timeout.Infinite);

        // One watcher per existing directory. Missing directories are
        // skipped (they may be created later — re-instantiate to pick them
        // up; live-reload doesn't track directory creation).
        foreach (var dir in _skillsDirectories.Where(Directory.Exists))
        {
            var w = new FileSystemWatcher(dir)
            {
                Filter = "SKILL.md",
                IncludeSubdirectories = true,
                NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName,
                EnableRaisingEvents = true,
            };
            w.Changed += OnSkillFileEvent;
            w.Created += OnSkillFileEvent;
            w.Deleted += OnSkillFileEvent;
            w.Renamed += OnSkillFileEvent;
            _watchers.Add(w);
        }
    }

    /// <summary>Loaded skills, in directory enumeration order.</summary>
    public IReadOnlyList<SkillMd> Skills => _skills;

    /// <summary>
    /// Number of times <see cref="Reload"/> has executed since construction.
    /// Exposed so callers (and tests) can confirm a watcher event actually
    /// triggered a reload, not just fired without effect.
    /// </summary>
    public int ReloadCount => _reloadCount;

    /// <summary>
    /// Re-reads SKILL.md files from disk and atomically swaps the cached
    /// skill set. Safe to call concurrently; the latest call wins. Used by
    /// the FileSystemWatcher in production and called directly from tests
    /// to assert reload semantics deterministically.
    /// </summary>
    public void Reload()
    {
        if (_disposed) return;

        IReadOnlyList<SkillMd> next;
        try
        {
            next = LoadSkillsCore();
        }
        catch (Exception ex)
        {
            // A malformed SKILL.md mid-save shouldn't crash the host. Log,
            // keep the previous skill set, and let the next watcher event
            // (the editor's final write) trigger another attempt.
            _logWarning($"[FileBasedSkillsProvider] Reload failed: {ex.GetType().Name}: {ex.Message}");
            return;
        }

        lock (_reloadLock)
        {
            _skills = next;
            Interlocked.Increment(ref _reloadCount);
        }
    }

    private IReadOnlyList<SkillMd> LoadSkillsCore()
    {
        // Load from each directory in order; later directories override earlier
        // ones on Name collision. This implements the drop-in / overlay pattern:
        // canonical `skills/` first, drafts `skills-dev/` second, drafts win.
        var byName = new Dictionary<string, SkillMd>(StringComparer.OrdinalIgnoreCase);
        foreach (var dir in _skillsDirectories)
        {
            if (!Directory.Exists(dir)) continue;
            foreach (var skill in SkillMdLoader.LoadFromDirectory(dir, _logWarning))
            {
                byName[skill.Name] = skill;  // last write wins
            }
        }
        return [.. byName.Values];
    }

    private void OnSkillFileEvent(object sender, FileSystemEventArgs e)
    {
        if (_disposed) return;
        // Debounce: editors save in 2-3 events; we only want one reload per
        // logical save. Restart the timer; if no further events arrive in
        // the window, the timer fires and reload runs.
        _debounceTimer?.Change(ReloadDebounce, Timeout.InfiniteTimeSpan);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        foreach (var w in _watchers)
        {
            w.EnableRaisingEvents = false;
            w.Changed -= OnSkillFileEvent;
            w.Created -= OnSkillFileEvent;
            w.Deleted -= OnSkillFileEvent;
            w.Renamed -= OnSkillFileEvent;
            w.Dispose();
        }
        _watchers.Clear();
        _debounceTimer?.Dispose();
    }

    /// <summary>
    /// Provide additional <see cref="AIContext"/> for the current invocation.
    /// The framework wraps this in <c>InvokingCoreAsync</c> which handles
    /// filtering (External-only messages by default), merging with input
    /// context, and source stamping — overriding this method is the
    /// recommended path per the Agent Framework docs.
    /// </summary>
    protected override ValueTask<AIContext?> ProvideAIContextAsync(
        InvokingContext context,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (_skills.Count == 0)
            return ValueTask.FromResult<AIContext?>(new AIContext());

        // Inspect the latest user-authored message in the input context —
        // that's what the agent is about to respond to. Earlier messages
        // would cause stale skill matches in long sessions.
        var latestUserText = context.AIContext.Messages?
            .LastOrDefault(m => m.Role == ChatRole.User)?
            .Text;

        if (string.IsNullOrWhiteSpace(latestUserText))
            return ValueTask.FromResult<AIContext?>(new AIContext());

        var lower = latestUserText.ToLowerInvariant();
        var matches = _skills
            .Where(s => s.Triggers.Any(t => !string.IsNullOrWhiteSpace(t) &&
                                            lower.Contains(t.ToLowerInvariant())))
            .ToList();

        if (matches.Count == 0)
            return ValueTask.FromResult<AIContext?>(new AIContext());

        var instructions = new StringBuilder();
        for (var i = 0; i < matches.Count; i++)
        {
            if (i > 0) instructions.AppendLine().AppendLine();
            instructions.AppendLine($"### Skill: {matches[i].Name}");
            instructions.Append(matches[i].Body);
        }

        return ValueTask.FromResult<AIContext?>(new AIContext { Instructions = instructions.ToString() });
    }
}
