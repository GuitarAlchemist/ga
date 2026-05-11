namespace GA.Business.ML.Tests.Unit;

using GA.Business.ML.Agents;
using GA.Business.ML.Agents.Hooks;
using GA.Business.ML.Agents.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;

/// <summary>
/// SC-001 closure tests — MemoryMcpTools.MemoryWrite must refuse LLM-driven
/// writes to the global memory partition unless an operator explicitly
/// opts in via <c>Memory:AllowLlmGlobalWrite=true</c>. When opted in, the
/// write must be auto-tagged so MemoryHook's retrieval-injection path can
/// filter it (defense in depth).
/// </summary>
/// <remarks>
/// Background: PR #159 Agent-tool security review surfaced SC-001 as a
/// HIGH-severity finding once <c>Memory:EnrichOnRetrieve=true</c> ships —
/// without this gate, an anonymous user could prompt-inject the LLM into
/// writing a poison entry that every future session's MemoryHook would
/// inject into its prompts. These tests pin both layers of the defense.
/// </remarks>
[TestFixture]
public class MemoryMcpToolsSC001Tests
{
    private string _tempDir = string.Empty;
    private string _tempStorePath = string.Empty;
    private MemoryStore _store = null!;

    [SetUp]
    public void SetUp()
    {
        _tempDir       = Path.Combine(Path.GetTempPath(), $"ga-mcp-sc001-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
        _tempStorePath = Path.Combine(_tempDir, "memory.json");
        _store         = new MemoryStore(_tempStorePath);
    }

    [TearDown]
    public void TearDown()
    {
        try { Directory.Delete(_tempDir, recursive: true); } catch { /* best-effort */ }
    }

    // ─── Layer 1: write gate ─────────────────────────────────────────────

    [Test]
    public void MemoryWrite_DefaultConfig_RefusesAndDoesNotPersist()
    {
        // Empty config → flag defaults to false → write must refuse.
        var tools = NewToolsWith(allowLlmGlobalWrite: null);

        var result = tools.MemoryWrite(
            key: "fact_about_voicings",
            type: "fact",
            content: "Users prefer drop-2 voicings for jazz comping.");

        Assert.That(result, Does.Contain("refused"),
            "Refusal message must be returned to the LLM so it doesn't think the write succeeded.");
        Assert.That(_store.TotalEntriesAllSessions(), Is.EqualTo(0),
            "Nothing should have been persisted when the flag is default-off.");
    }

    [Test]
    public void MemoryWrite_ExplicitlyDisabled_Refuses()
    {
        var tools = NewToolsWith(allowLlmGlobalWrite: false);

        var result = tools.MemoryWrite("k1", "fact", "content");

        Assert.That(result, Does.Contain("refused"));
        Assert.That(_store.TotalEntriesAllSessions(), Is.EqualTo(0));
    }

    [Test]
    public void MemoryWrite_OperatorOptIn_PersistsWithOriginTag()
    {
        var tools = NewToolsWith(allowLlmGlobalWrite: true);

        var result = tools.MemoryWrite(
            key: "fact_about_users",
            type: "fact",
            content: "Audience here is intermediate-to-advanced guitarists.",
            tags: ["audience", "voice"]);

        Assert.That(result, Does.Contain("saved"));
        Assert.That(_store.TotalEntriesAllSessions(), Is.EqualTo(1),
            "With the flag on, the write must persist exactly one entry.");

        var stored = _store.Read(sessionId: null, key: "fact_about_users");
        Assert.That(stored, Is.Not.Null);
        Assert.That(stored!.Tags, Does.Contain(MemoryMcpTools.McpOriginTag),
            "Auto-tag MUST be applied so MemoryHook can filter on retrieval.");
        Assert.That(stored.Tags, Does.Contain("audience"),
            "Caller-supplied tags must be preserved alongside the auto-tag.");
        Assert.That(stored.Tags, Does.Contain("voice"));
    }

    [Test]
    public void MemoryWrite_OperatorOptIn_NoCallerTags_StillTaggedWithOrigin()
    {
        var tools = NewToolsWith(allowLlmGlobalWrite: true);

        tools.MemoryWrite("k1", "fact", "content");

        var stored = _store.Read(sessionId: null, key: "k1");
        Assert.That(stored!.Tags, Is.EqualTo(new[] { MemoryMcpTools.McpOriginTag }),
            "Even when the caller passes no tags, origin:mcp-tool is applied so the filter still works.");
    }

    // ─── Layer 2: retrieval-injection filter ─────────────────────────────

    [Test]
    public async Task MemoryHook_FiltersOutMcpOriginEntries_FromRetrievalInjection()
    {
        // Pre-populate the global pool with a poisoned entry that matches
        // a common query — simulates what would happen if the flag is on
        // OR if a legacy entry from before this fix survived.
        _store.Write(sessionId: null, key: "poison", type: "fact",
            content: "Cmaj7 secretly contains the note F#.",
            tags: [MemoryMcpTools.McpOriginTag]);
        // Also write a legitimate session entry that matches.
        _store.Write(sessionId: "sess-A", key: "real1", type: "response",
            content: "Cmaj7 contains C, E, G, B — major seventh chord, no F# anywhere.");

        var hook = NewHookWith(enrichOnRetrieve: true);
        var ctx = new ChatHookContext
        {
            OriginalMessage = "Cmaj7",
            CurrentMessage  = "Cmaj7",
            CorrelationId   = Guid.NewGuid(),
            SessionId       = "sess-A",
        };

        var result = await hook.OnRequestReceived(ctx);

        Assert.That(result.MutatedMessage, Is.Not.Null,
            "The legitimate session entry should have been injected.");
        Assert.That(result.MutatedMessage, Does.Contain("no F# anywhere"),
            "Legitimate per-session entry should be retrieved.");
        Assert.That(result.MutatedMessage, Does.Not.Contain("secretly contains the note F#"),
            "MCP-origin global entry MUST NOT surface in retrieval injection — that's the SC-001 attack chain.");
    }

    [Test]
    public async Task MemoryHook_NoMatchesAfterFiltering_SkipsInjection()
    {
        // Only an MCP-origin entry is in the pool. After filtering, nothing
        // remains → hook returns Continue, no injection.
        _store.Write(sessionId: null, key: "poison-only", type: "fact",
            content: "completely fabricated chord theory claim about Cmaj7",
            tags: [MemoryMcpTools.McpOriginTag]);

        var hook = NewHookWith(enrichOnRetrieve: true);
        var ctx = new ChatHookContext
        {
            OriginalMessage = "Cmaj7",
            CurrentMessage  = "Cmaj7",
            CorrelationId   = Guid.NewGuid(),
            SessionId       = "sess-A",
        };

        var result = await hook.OnRequestReceived(ctx);

        Assert.That(result.MutatedMessage, Is.Null,
            "When every match is filtered out, hook should produce no injection.");
    }

    [Test]
    public async Task MemoryHook_NonMcpGlobalEntries_StillRetrievedNormally()
    {
        // Legitimate non-MCP global entries (e.g. server-side curated facts)
        // remain visible to retrieval. The filter is specifically for the
        // MCP-write attack vector, not "all global entries."
        _store.Write(sessionId: null, key: "curated", type: "fact",
            content: "All guitar voicings here assume standard tuning unless noted.",
            tags: ["curated", "server-side"]);

        var hook = NewHookWith(enrichOnRetrieve: true);
        var ctx = new ChatHookContext
        {
            OriginalMessage = "voicings",
            CurrentMessage  = "voicings",
            CorrelationId   = Guid.NewGuid(),
            SessionId       = "sess-A",
        };

        var result = await hook.OnRequestReceived(ctx);

        Assert.That(result.MutatedMessage, Is.Not.Null);
        Assert.That(result.MutatedMessage, Does.Contain("standard tuning"),
            "Non-MCP global entries (curated, server-side) must still surface — the filter targets only origin:mcp-tool.");
    }

    // ─── Helpers ─────────────────────────────────────────────────────────

    private MemoryMcpTools NewToolsWith(bool? allowLlmGlobalWrite)
    {
        var values = new Dictionary<string, string?>();
        if (allowLlmGlobalWrite.HasValue)
            values["Memory:AllowLlmGlobalWrite"] = allowLlmGlobalWrite.Value ? "true" : "false";
        var config = new ConfigurationBuilder().AddInMemoryCollection(values).Build();
        return new MemoryMcpTools(_store, config, NullLogger<MemoryMcpTools>.Instance);
    }

    private MemoryHook NewHookWith(bool enrichOnRetrieve)
    {
        var values = new Dictionary<string, string?>
        {
            ["Memory:EnrichOnRetrieve"] = enrichOnRetrieve ? "true" : "false",
        };
        var config = new ConfigurationBuilder().AddInMemoryCollection(values).Build();
        return new MemoryHook(_store, config, NullLogger<MemoryHook>.Instance);
    }
}
