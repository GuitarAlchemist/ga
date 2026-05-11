namespace GA.Business.ML.Tests.Unit;

using System.Text.RegularExpressions;

/// <summary>
/// Static-analysis regression pin (task #104). Fails when a new
/// <c>new ChatHookContext { ... }</c> initializer is added to
/// <c>ProductionOrchestrator.cs</c> without plumbing <c>SessionId</c>.
/// </summary>
/// <remarks>
/// <para>
/// Background: PR #157 / PR #160 Phase B plumbed <c>SessionId</c> through
/// every <c>ChatHookContext</c> instantiation in
/// <c>ProductionOrchestrator</c> so the storage-layer isolation from
/// PR #157 actually applies on every request path (skill, RAG, tab). A
/// missing site silently regresses to a per-request Guid fallback, which
/// looks correct in production but lets cross-session memory leak through
/// any hook that observes <c>ctx.SessionId</c>.
/// </para>
/// <para>
/// Why static analysis. The behavior is impossible to assert at the
/// memory-store level (we already do that in
/// <see cref="MemoryHookSessionPlumbingTests"/>) — that suite verifies the
/// hook honors a SessionId when given one, not that every orchestrator
/// path provides one. A runtime test would require most of the
/// orchestrator's DI graph; cost/benefit doesn't justify it for this
/// regression. The static pin catches the exact mistake: forgetting
/// <c>SessionId =</c> in a new initializer.
/// </para>
/// <para>
/// When this test fails. Either (a) you added a new
/// <c>new ChatHookContext</c> site and forgot to plumb
/// <c>SessionId = sessionId,</c> — fix the initializer, or (b) you
/// deliberately removed a site — update <see cref="MinimumExpectedSites"/>
/// downward AND document why in the commit message.
/// </para>
/// </remarks>
[TestFixture]
public class ProductionOrchestratorHookPlumbingTests
{
    /// <summary>
    /// Lower bound on the count of <c>new ChatHookContext</c> sites in
    /// <c>ProductionOrchestrator.cs</c>. Set at the post-PR-#160 count.
    /// If you legitimately remove a site, lower this number deliberately.
    /// </summary>
    private const int MinimumExpectedSites = 7;

    private const string SourceRelativePath =
        "Common/GA.Business.Core.Orchestration/Services/ProductionOrchestrator.cs";

    private static readonly Regex ChatHookContextInitializer =
        new(@"new\s+ChatHookContext\s*\{([^}]*)\}", RegexOptions.Singleline);

    private static readonly Regex SessionIdAssignment =
        new(@"SessionId\s*=\s*\w");

    [Test]
    public void AllChatHookContextSites_PlumbSessionId()
    {
        var source = LoadOrchestratorSource();
        var matches = ChatHookContextInitializer.Matches(source);

        Assert.That(matches.Count, Is.GreaterThanOrEqualTo(MinimumExpectedSites),
            $"Expected at least {MinimumExpectedSites} `new ChatHookContext` initializer " +
            $"sites in ProductionOrchestrator.cs, found {matches.Count}. " +
            "If a site was removed deliberately, lower MinimumExpectedSites and document why.");

        var missing = new List<string>();
        for (var i = 0; i < matches.Count; i++)
        {
            var initBody = matches[i].Groups[1].Value;
            if (!SessionIdAssignment.IsMatch(initBody))
            {
                var prefix = source[..matches[i].Index];
                var lineNo = prefix.Count(c => c == '\n') + 1;
                missing.Add($"site #{i + 1} (≈ line {lineNo})");
            }
        }

        Assert.That(missing, Is.Empty,
            "ChatHookContext sites missing `SessionId = ...` plumbing: " +
            $"{string.Join(", ", missing)}. " +
            "See PR #157 Phase B and PR #163 audit — session-scoped memory " +
            "requires every hook ctx to carry the caller's SessionId. " +
            "A missing site silently regresses to per-request Guid fallback, " +
            "letting cross-session memory leak through any hook reading ctx.SessionId.");
    }

    private static string LoadOrchestratorSource()
    {
        var repoRoot = FindRepoRoot(AppContext.BaseDirectory);
        var sourcePath = Path.Combine(repoRoot, SourceRelativePath.Replace('/', Path.DirectorySeparatorChar));
        Assert.That(File.Exists(sourcePath), Is.True,
            $"Could not locate ProductionOrchestrator.cs at {sourcePath}. " +
            "If the file was moved, update SourceRelativePath.");
        return File.ReadAllText(sourcePath);
    }

    /// <summary>
    /// Walks up from the test-assembly directory until it finds
    /// <c>AllProjects.slnx</c>, the repo root marker. Throws if not found
    /// — keeps the test honest about its dependency on the source tree.
    /// </summary>
    private static string FindRepoRoot(string startDir)
    {
        var dir = new DirectoryInfo(startDir);
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "AllProjects.slnx")))
                return dir.FullName;
            dir = dir.Parent;
        }
        throw new InvalidOperationException(
            $"Could not find repo root (AllProjects.slnx) walking up from {startDir}.");
    }
}
