namespace GA.Business.Core.Tests.Orchestration;

using GA.Business.Core.Orchestration.Models;
using GA.Business.Core.Orchestration.Services;

[TestFixture]
public class RoutingContextEnricherTests
{
    private ConversationHistoryStore _store = null!;
    private RoutingContextEnricher _enricher = null!;

    [SetUp]
    public void Setup()
    {
        _store = new ConversationHistoryStore();
        _enricher = new RoutingContextEnricher(_store);
    }

    [Test]
    public void EnrichIfFollowUp_NoMatch_ReturnsMessageUnchanged()
    {
        // "What is C major?" is a standalone question — not a deictic
        // follow-up, so the enricher must not splice in anything.
        const string msg = "What is C major?";
        var result = _enricher.EnrichIfFollowUp(msg, "s1", requestHistory: null);
        Assert.That(result, Is.EqualTo(msg));
    }

    [Test]
    public void EnrichIfFollowUp_ShowMeTheXMajorScale_DoesNotEnrich()
    {
        // Negative case from the 2026-05-14 correctness review: "show me"
        // takes its own object noun ("the C major scale"), so it must NOT
        // be treated as a deictic follow-up that pulls in an unrelated
        // prior turn. Only the more specific "show me a practical example"
        // / "give me an example" forms match.
        _store.AddTurn("s1", "user", "How do I tune a guitar?");
        _store.AddTurn("s1", "assistant", "Standard tuning is EADGBE.");

        const string msg = "Show me the C major scale";
        var result = _enricher.EnrichIfFollowUp(msg, "s1", requestHistory: null);

        Assert.That(result, Is.EqualTo(msg),
            "show-me-with-noun-phrase must not pick up the unrelated prior turn");
    }

    [Test]
    public void EnrichIfFollowUp_ShowMeAPracticalExample_DoesEnrich()
    {
        // Live regression from 2026-05-14: this exact phrase had embeddings
        // matching practice-routine when it should have inherited the prior
        // turn's chord-substitution context.
        _store.AddTurn("s1", "user", "How do I make this progression sound darker?");
        _store.AddTurn("s1", "assistant", "Try borrowed iv or bVII.");

        const string msg = "Show me a practical example on guitar";
        var result = _enricher.EnrichIfFollowUp(msg, "s1", requestHistory: null);

        Assert.That(result, Does.StartWith("How do I make this progression sound darker?"),
            "the deictic follow-up must be prefixed with the prior user turn");
        Assert.That(result, Does.EndWith(msg));
    }

    [Test]
    public void EnrichIfFollowUp_WhatAbout_DoesEnrich()
    {
        _store.AddTurn("s1", "user", "Diatonic chords in C major");
        _store.AddTurn("s1", "assistant", "C, Dm, Em, F, G, Am, B°");

        var result = _enricher.EnrichIfFollowUp("what about minor?", "s1", requestHistory: null);

        Assert.That(result, Does.StartWith("Diatonic chords in C major"));
    }

    [Test]
    public void EnrichIfFollowUp_RequestHistoryFallback_WhenStoreEmpty()
    {
        // The React frontend posts conversation history per request without
        // a stable sessionId, so the session-scoped store can be empty
        // mid-conversation. The enricher must fall through to the
        // request-supplied history. requestHistory does NOT include the
        // just-arrived turn (the controller adds it after dispatch), so
        // the last entry IS the prior turn.
        var requestHistory = new List<ConversationTurn>
        {
            new("user", "Tell me about Dorian mode", DateTimeOffset.UtcNow.AddSeconds(-30)),
            new("assistant", "Dorian is the second mode of major.", DateTimeOffset.UtcNow.AddSeconds(-25)),
        };

        var result = _enricher.EnrichIfFollowUp(
            "what about Phrygian?",
            sessionId: "s_unstable",
            requestHistory: requestHistory);

        Assert.That(result, Does.StartWith("Tell me about Dorian mode"));
    }

    [Test]
    public void EnrichIfFollowUp_TooLongMessage_SkipsEnrichment()
    {
        _store.AddTurn("s1", "user", "earlier");

        // Above FollowUpMaxLength=80; embedding signal is already strong
        // enough on a long utterance, so we skip the splice.
        var longMessage = "what about " + new string('x', 100);

        var result = _enricher.EnrichIfFollowUp(longMessage, "s1", requestHistory: null);

        Assert.That(result, Is.EqualTo(longMessage));
    }

    [Test]
    public void EnrichIfFollowUp_NoPriorUserTurn_ReturnsUnchanged()
    {
        // Store has only an assistant turn — no prior user message to splice.
        _store.AddTurn("s1", "assistant", "Welcome!");

        var result = _enricher.EnrichIfFollowUp("what about minor?", "s1", requestHistory: null);

        Assert.That(result, Is.EqualTo("what about minor?"));
    }

    [Test]
    public void EnrichIfFollowUp_EmptyMessage_ReturnsAsIs()
    {
        var result = _enricher.EnrichIfFollowUp("", "s1", requestHistory: null);
        Assert.That(result, Is.EqualTo(""));
    }

    [Test]
    public void TruncatePreservingSurrogates_PreservesEmojiPair()
    {
        // "ABC" + 🎸 (high+low surrogate pair) = 5 UTF-16 units total.
        // Cutting at 4 would slice the guitar emoji's surrogate pair and
        // produce an unpaired high surrogate that downstream JSON
        // serialisation rejects. The truncator backs the cut up to 3,
        // keeping the result well-formed.
        var input = "ABC" + char.ConvertFromUtf32(0x1F3B8);
        var result = RoutingContextEnricher.TruncatePreservingSurrogates(input, maxUtf16Units: 4);
        Assert.That(result, Is.EqualTo("ABC…"));
    }

    [Test]
    public void TruncatePreservingSurrogates_ShortString_Unchanged()
    {
        var result = RoutingContextEnricher.TruncatePreservingSurrogates("abc", maxUtf16Units: 10);
        Assert.That(result, Is.EqualTo("abc"));
    }

    [Test]
    public void FindMostRecentPriorUserTurn_SkipLast_IgnoresFinalEntry()
    {
        // Session-store layout: the just-added user turn is the last entry,
        // so the helper must skip it to find the actual prior turn.
        var turns = new List<ConversationTurn>
        {
            new("user", "first ask", DateTimeOffset.UtcNow.AddSeconds(-30)),
            new("assistant", "ans", DateTimeOffset.UtcNow.AddSeconds(-25)),
            new("user", "second ask", DateTimeOffset.UtcNow.AddSeconds(-20)),
            new("assistant", "ans2", DateTimeOffset.UtcNow.AddSeconds(-15)),
            new("user", "current ask", DateTimeOffset.UtcNow),  // skip me
        };

        var prior = RoutingContextEnricher.FindMostRecentPriorUserTurn(turns, skipLast: true);

        Assert.That(prior, Is.Not.Null);
        Assert.That(prior!.Content, Is.EqualTo("second ask"));
    }
}
