namespace GA.Business.ML.Tests.Unit;

using GA.Business.ML.Agents;
using GA.Business.ML.Agents.Hooks;
using GA.Business.ML.Agents.Memory;
using GA.Business.ML.Agents.Skills;
using Microsoft.Extensions.Logging.Abstractions;

/// <summary>
/// Pins the RememberThisSkill + MemoryWriteHook split. Three layers:
/// </summary>
/// <list type="number">
/// <item>RememberThisParser — pure parsing of user phrasing → MemoryWriteRequest.</item>
/// <item>RememberThisSkill — wraps the parser; emits the request on AgentResponse.Data.</item>
/// <item>MemoryWriteHook — picks up the request, persists with ctx.SessionId.</item>
/// </list>
/// <remarks>
/// The split is load-bearing for SC-001: skills are session-agnostic so
/// they can't write the wrong session by mistake; the hook owns the
/// SessionId boundary and refuses to write when it's null.
/// </remarks>
[TestFixture]
public class RememberThisTests
{
    // ─── Parser ─────────────────────────────────────────────────────────

    [TestCase("remember that I prefer drop-2 voicings", "preference")]
    [TestCase("save this: my favorite key is Bb",       "preference")]
    [TestCase("note: I'm working on fingerstyle",       "focus")]
    [TestCase("don't forget that I'm an intermediate guitarist", "fact")]
    [TestCase("remember I always tune to drop D",       "preference")]
    [TestCase("please remember my focus this week is jazz comping", "focus")]
    [TestCase("store this fact: my main guitar is a Telecaster",    "fact")]
    public void Parser_InfersType_FromPhrasing(string input, string expectedType)
    {
        var req = RememberThisParser.TryParse(input);
        Assert.That(req, Is.Not.Null, $"Expected a MemoryWriteRequest for: \"{input}\"");
        Assert.That(req!.Type, Is.EqualTo(expectedType));
    }

    [TestCase("What is Cmaj7?")]
    [TestCase("hello")]
    [TestCase("")]
    [TestCase("   ")]
    [TestCase("I prefer drop-2 voicings")]   // no lead phrase — bare statement
    public void Parser_RejectsNonRememberMessages(string input)
    {
        var req = RememberThisParser.TryParse(input);
        Assert.That(req, Is.Null, $"Parser should NOT match: \"{input}\"");
    }

    [Test]
    public void Parser_StripsLeadPhraseAndPunctuation()
    {
        var req = RememberThisParser.TryParse("remember that I prefer drop-2 voicings.");
        Assert.That(req!.Content, Is.EqualTo("I prefer drop-2 voicings"),
            "Lead phrase and trailing punctuation should both be stripped.");
    }

    [Test]
    public void Parser_GeneratesDeterministicKey_SameContent_SameKey()
    {
        var a = RememberThisParser.TryParse("remember I prefer drop-2 voicings");
        var b = RememberThisParser.TryParse("remember I prefer drop-2 voicings");
        Assert.That(a!.Key, Is.EqualTo(b!.Key),
            "Same content → same key, so re-running 'remember the same thing' is idempotent.");
    }

    [Test]
    public void Parser_KeyPrefixedWithInferredType()
    {
        var req = RememberThisParser.TryParse("remember that I prefer drop-2 voicings");
        Assert.That(req!.Key, Does.StartWith("preference_"));
    }

    [Test]
    public void Parser_RejectsEmptyContent_AfterStrippingLead()
    {
        // The lead phrase parser would consume "remember:" leaving nothing.
        var req = RememberThisParser.TryParse("remember:");
        Assert.That(req, Is.Null);
    }

    [Test]
    public void Parser_TagsIncludeTypeAndUserStated()
    {
        var req = RememberThisParser.TryParse("remember that I prefer drop-2 voicings");
        Assert.That(req!.Tags, Does.Contain("user-stated"));
        Assert.That(req.Tags, Does.Contain("type:preference"));
    }

    [Test]
    public void Parser_LooksLikeRememberRequest_QuickCheck()
    {
        Assert.That(RememberThisParser.LooksLikeRememberRequest("remember that X"), Is.True);
        Assert.That(RememberThisParser.LooksLikeRememberRequest("save this: X"),    Is.True);
        Assert.That(RememberThisParser.LooksLikeRememberRequest("What is Cmaj7?"),  Is.False);
        Assert.That(RememberThisParser.LooksLikeRememberRequest(""),                Is.False);
        Assert.That(RememberThisParser.LooksLikeRememberRequest(null!),             Is.False);
    }

    // ─── Skill ──────────────────────────────────────────────────────────

    [Test]
    public async Task Skill_HappyPath_ReturnsConfirmationWithDataPayload()
    {
        var skill = new RememberThisSkill(NullLogger<RememberThisSkill>.Instance);

        var response = await skill.ExecuteAsync("remember that I prefer drop-2 voicings");

        Assert.That(response.Confidence, Is.EqualTo(1.0f));
        Assert.That(response.AgentId, Is.EqualTo("remember-this"));
        Assert.That(response.Result, Does.Contain("remember"));
        Assert.That(response.Result, Does.Contain("drop-2 voicings"));
        Assert.That(response.Data, Is.InstanceOf<MemoryWriteRequest>());
        var req = (MemoryWriteRequest)response.Data!;
        Assert.That(req.Type, Is.EqualTo("preference"));
        Assert.That(req.Content, Is.EqualTo("I prefer drop-2 voicings"));
    }

    [Test]
    public async Task Skill_NonRememberMessage_ReturnsCannotHelp_WithNoData()
    {
        var skill = new RememberThisSkill(NullLogger<RememberThisSkill>.Instance);

        var response = await skill.ExecuteAsync("What is Cmaj7?");

        Assert.That(response.Confidence, Is.EqualTo(0.0f),
            "CannotHelp confidence is 0 so downstream gates ignore it.");
        Assert.That(response.Data, Is.Null,
            "No MemoryWriteRequest emitted — nothing for the hook to write.");
    }

    [Test]
    public void Skill_CanHandle_AgreesWithParser()
    {
        var skill = new RememberThisSkill(NullLogger<RememberThisSkill>.Instance);
        Assert.That(skill.CanHandle("remember that X"), Is.True);
        Assert.That(skill.CanHandle("save this: X"),    Is.True);
        Assert.That(skill.CanHandle("What is Cmaj7?"),  Is.False);
    }

    // ─── Hook ───────────────────────────────────────────────────────────

    [Test]
    public async Task Hook_NoData_IsNoOp()
    {
        using var harness = new HookHarness();

        await harness.Hook.OnResponseSent(MakeCtx(harness, data: null));

        Assert.That(harness.Store.TotalEntriesAllSessions(), Is.EqualTo(0));
    }

    [Test]
    public async Task Hook_HappyPath_PersistsToCallerSession()
    {
        using var harness = new HookHarness();
        var req = new MemoryWriteRequest("preference_foo_deadbeef", "preference",
            "I prefer drop-2 voicings", Tags: ["user-stated", "type:preference"]);

        await harness.Hook.OnResponseSent(MakeCtx(harness, data: req, sessionId: "sess-A"));

        Assert.That(harness.Store.TotalEntriesAllSessions(), Is.EqualTo(1));
        var stored = harness.Store.Read(sessionId: "sess-A", key: "preference_foo_deadbeef");
        Assert.That(stored, Is.Not.Null);
        Assert.That(stored!.Type, Is.EqualTo("preference"));
        Assert.That(stored.Content, Is.EqualTo("I prefer drop-2 voicings"));
        Assert.That(stored.SessionId, Is.EqualTo("sess-A"),
            "Entry MUST be scoped to the caller's session — global writes would defeat SC-001.");
    }

    [Test]
    public async Task Hook_NullSessionId_RefusesToWrite()
    {
        using var harness = new HookHarness();
        var req = new MemoryWriteRequest("fact_x_abcd1234", "fact", "the user is a guitarist", Tags: []);

        await harness.Hook.OnResponseSent(MakeCtx(harness, data: req, sessionId: null));

        Assert.That(harness.Store.TotalEntriesAllSessions(), Is.EqualTo(0),
            "Null SessionId → refuse to write. Mirrors MemoryHook's SC-001 posture; " +
            "writing globally would defeat the session-scoping defense.");
    }

    [TestCase("response")]
    [TestCase("conversation")]
    [TestCase("anything_else")]
    public async Task Hook_DisallowedType_RefusesToWrite(string disallowedType)
    {
        using var harness = new HookHarness();
        var req = new MemoryWriteRequest("k", disallowedType, "content", Tags: []);

        await harness.Hook.OnResponseSent(MakeCtx(harness, data: req, sessionId: "sess-A"));

        Assert.That(harness.Store.TotalEntriesAllSessions(), Is.EqualTo(0),
            $"Type '{disallowedType}' is not in the durable-memory whitelist; refusing prevents " +
            "re-introducing the transcript/memory conflation that PR #173/#174 closed.");
    }

    [Test]
    public async Task Hook_StoreThrows_LoggedNotRethrown()
    {
        // Use a path the OS rejects to force an IO failure on Write.
        // Mid-pipeline exceptions become 500s to the chat client; the
        // hook must swallow and log.
        var badPath = "Z:\\does-not-exist\\subdir\\memory.json";
        var store   = new MemoryStore(badPath);
        var hook    = new MemoryWriteHook(store, NullLogger<MemoryWriteHook>.Instance);
        var req = new MemoryWriteRequest("k", "fact", "content", Tags: []);

        Assert.DoesNotThrowAsync(async () =>
            await hook.OnResponseSent(new ChatHookContext
            {
                OriginalMessage = "x",
                CurrentMessage  = "x",
                CorrelationId   = Guid.NewGuid(),
                SessionId       = "sess-A",
                Response = new AgentResponse
                {
                    AgentId    = "remember-this",
                    Result     = "ok",
                    Confidence = 1.0f,
                    Data       = req,
                },
            }));
    }

    // ─── Helpers ────────────────────────────────────────────────────────

    private sealed class HookHarness : IDisposable
    {
        public string Dir { get; }
        public MemoryStore Store { get; }
        public MemoryWriteHook Hook { get; }

        public HookHarness()
        {
            Dir   = Path.Combine(Path.GetTempPath(), $"ga-remember-{Guid.NewGuid():N}");
            Directory.CreateDirectory(Dir);
            Store = new MemoryStore(Path.Combine(Dir, "memory.json"));
            Hook  = new MemoryWriteHook(Store, NullLogger<MemoryWriteHook>.Instance);
        }

        public void Dispose()
        {
            try { Directory.Delete(Dir, recursive: true); } catch { /* best-effort */ }
        }
    }

    private static ChatHookContext MakeCtx(HookHarness h, object? data, string? sessionId = "sess-A") => new()
    {
        OriginalMessage = "remember stuff",
        CurrentMessage  = "remember stuff",
        CorrelationId   = Guid.NewGuid(),
        SessionId       = sessionId,
        Response = new AgentResponse
        {
            AgentId    = "remember-this",
            Result     = "I've saved that.",
            Confidence = 1.0f,
            Data       = data,
        },
    };
}
