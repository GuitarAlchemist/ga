namespace GA.Business.Core.Tests.Orchestration;

using GA.Business.ML.Agents.Skills;

/// <summary>
/// Pins the priority-order invariant in
/// <c>ProductionOrchestrator.TrySelectDeterministicAgent</c>:
/// remember-this requests MUST take precedence over the voicing fast-path.
/// </summary>
/// <remarks>
/// <para>
/// <b>The bug this prevents (surfaced 2026-05-12 by the live-orchestrator
/// e2e):</b> the orchestrator runs <c>IsExplicitVoicingRequest</c> before
/// the semantic intent router. That deterministic fast-path matches on
/// <c>ExplicitVoicingKeywords</c>, which includes "drop-2 voicings",
/// "chord shape", "fingering", etc. A user typing "remember that I prefer
/// drop-2 voicings for jazz comping" matched the voicing keyword first,
/// short-circuited to <c>VoicingAgent</c>, and got back a list of
/// voicings — the memory write never happened.
/// </para>
/// <para>
/// <b>Why this is a unit test:</b> the e2e
/// (<c>LiveOrchestratorMemoryRetentionTests</c>) catches the bug only
/// when an Ollama embedder is available, which means CI doesn't catch
/// it. This test pins the <c>RememberThisParser.LooksLikeRememberRequest</c>
/// contract that the orchestrator now gates on — if the parser stops
/// matching the canonical remember-phrasings (or accidentally matches
/// pure voicing requests), CI fails before the bug ships.
/// </para>
/// </remarks>
[TestFixture]
public class RememberOverVoicingPriorityTests
{
    // Canonical remember phrasings that contain voicing keywords. Each MUST
    // be detected as a remember-request — if any of these returns false,
    // the orchestrator's TrySelectDeterministicAgent will short-circuit to
    // the voicing fast-path and the memory write silently disappears.
    [TestCase("remember that I prefer drop-2 voicings for jazz comping")]
    [TestCase("save this: my favorite chord shapes are drop-3")]
    [TestCase("note: I always use rootless voicings for ballads")]
    [TestCase("don't forget I'm working on barre chord fingerings this month")]
    [TestCase("please remember that drop 2 voicings of Cmaj7 are my go-to")]
    public void RememberRequestsWithVoicingKeywords_MustBeDetectedAsRemember(string message)
    {
        Assert.That(RememberThisParser.LooksLikeRememberRequest(message), Is.True,
            $"Message must parse as a remember-request: \"{message}\". " +
            "If this fails, ProductionOrchestrator.TrySelectDeterministicAgent " +
            "will fall through to IsExplicitVoicingRequest, match a voicing " +
            "keyword, and dispatch the message to VoicingAgent — the user " +
            "gets voicings back instead of a memory write. RememberThisParser " +
            "must keep matching the canonical remember/save/note/don't-forget " +
            "lead phrases regardless of trailing content.");
    }

    // Pure voicing requests (no remember-phrasing) MUST NOT be detected as
    // remember-requests — otherwise legitimate voicing queries would lose
    // the voicing fast-path and degrade to semantic routing.
    [TestCase("show me drop-2 voicings of Cmaj7")]
    [TestCase("what are the best fingerings for Am7")]
    [TestCase("rootless voicings for ii-V-I in C")]
    [TestCase("chord shapes for the blues")]
    [TestCase("drop 3 voicings on the top four strings")]
    public void VoicingRequests_WithoutRememberPhrasing_MustNotBeDetectedAsRemember(string message)
    {
        Assert.That(RememberThisParser.LooksLikeRememberRequest(message), Is.False,
            $"Message must NOT parse as a remember-request: \"{message}\". " +
            "If this fires true on a pure voicing query, the orchestrator's " +
            "voicing fast-path will be bypassed and the message will fall " +
            "into the semantic router, where (per the 2026-05-07 codex " +
            "diagnosis) voicing queries can misroute to the modes intent at " +
            "~0.71 cosine. The remember-parser's role is to gate on EXPLICIT " +
            "remember phrasing only.");
    }

    // PR #192 correctness review false-positive guards. The verbs
    // save/note/store have non-memory senses (save to library, note as a
    // verb on a pitch, store data) that were leaking through because the
    // parser allowed bare forms. Tightening required \s+this/\s+that or
    // an immediate colon/comma. These tests pin the tightening.
    [TestCase("save these drop-2 voicings of Cmaj7")]
    [TestCase("save the chord shape for Am7")]
    [TestCase("note these chord shapes for ii-V-I")]
    [TestCase("note the fingering for Drop 3 Cmaj7")]
    [TestCase("store the drop-2 voicing in my library")]
    public void SaveOrNoteOrStore_WithoutMemorableReferent_MustNotMatch(string message)
    {
        Assert.That(RememberThisParser.LooksLikeRememberRequest(message), Is.False,
            $"\"{message}\" — save/note/store followed by 'these'/'the'/etc. " +
            "is a domain operation (save to library, note as pitch, store as " +
            "data), NOT a memory write. The parser must NOT match unless the " +
            "verb is followed by an explicit memorable referent (\\s+this | " +
            "\\s+that | : | ,).");
    }

    // Positive cases that documented save/note/store usages MUST still match
    // after the tightening — these are direct copies of the example prompts
    // in RememberThisSkill.ExamplePrompts so a parser regression here would
    // break the canonical chatbot UX.
    [TestCase("save this: my favorite key is Bb")]
    [TestCase("note: I'm working on fingerstyle technique this month")]
    [TestCase("store this fact: my main guitar is a Telecaster")]
    [TestCase("save that I prefer drop-2 voicings")]
    [TestCase("note this drop-2 fingering is my favorite")]
    public void SaveOrNoteOrStore_WithMemorableReferent_StillMatch(string message)
    {
        Assert.That(RememberThisParser.LooksLikeRememberRequest(message), Is.True,
            $"\"{message}\" — save/note/store with explicit memorable " +
            "referent (this/that/colon/comma) MUST still parse as a " +
            "remember-request. These are the documented example prompts in " +
            "RememberThisSkill.ExamplePrompts; regressing them breaks the " +
            "canonical chatbot UX.");
    }

    // Pin the orchestrator's call ordering: TrySelectDeterministicAgent
    // must call LooksLikeRememberRequest BEFORE IsExplicitVoicingRequest.
    // We can't reach the private method directly, but we can pin the
    // STRUCTURAL invariant by reading the source — if a future refactor
    // reorders or removes the gate, the assertion below fails with a
    // human-readable explanation that points the reader at the right
    // location.
    [Test]
    public void ProductionOrchestrator_HasRememberGate_BeforeVoicingFastPath()
    {
        var path = LocateProductionOrchestratorSource();
        var source = File.ReadAllText(path);

        // Match the CALL sites specifically (not the documentation
        // comments or the method definition). The remember check is a
        // call to `LooksLikeRememberRequest(message)`; the voicing
        // fast-path is a call to `IsExplicitVoicingRequest(message)`.
        // Searching for the method-call shape (`(message)` suffix)
        // skips matches inside XML-doc comments that may mention the
        // method names but don't represent the ordering.
        var rememberGateIndex = source.IndexOf(
            "LooksLikeRememberRequest(message)",
            StringComparison.Ordinal);
        var voicingGateIndex = source.IndexOf(
            "IsExplicitVoicingRequest(message)",
            StringComparison.Ordinal);

        Assert.Multiple(() =>
        {
            Assert.That(rememberGateIndex, Is.GreaterThan(0),
                "ProductionOrchestrator.cs must call " +
                "RememberThisParser.LooksLikeRememberRequest somewhere — " +
                "the gate that lets remember-with-voicing-keyword prompts " +
                "skip the voicing fast-path. If this assertion fails, the " +
                "gate was removed and the PR #192 production bug has " +
                "regressed: 'remember that I prefer drop-2 voicings' will " +
                "route to VoicingAgent instead of writing to MemoryStore.");

            Assert.That(rememberGateIndex, Is.LessThan(voicingGateIndex),
                "RememberThisParser.LooksLikeRememberRequest must appear " +
                "BEFORE IsExplicitVoicingRequest in TrySelectDeterministicAgent. " +
                "If the gate is moved below, the voicing fast-path fires " +
                "first and the remember-flow silently breaks for any " +
                "prompt containing a voicing keyword. See PR #192.");
        });
    }

    private static string LocateProductionOrchestratorSource()
    {
        // Walk up from the test bin dir to the repo root (the GA.Business.ML.Tests
        // harness does the same dance — kept inline here to avoid coupling
        // this test project to that one).
        var d = new DirectoryInfo(AppContext.BaseDirectory);
        while (d is not null)
        {
            var candidate = Path.Combine(
                d.FullName,
                "Common", "GA.Business.Core.Orchestration", "Services",
                "ProductionOrchestrator.cs");
            if (File.Exists(candidate)) return candidate;
            d = d.Parent;
        }
        throw new FileNotFoundException(
            "Could not locate ProductionOrchestrator.cs by walking up from " +
            AppContext.BaseDirectory);
    }
}
