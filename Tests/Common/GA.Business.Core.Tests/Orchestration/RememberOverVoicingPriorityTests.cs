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
}
