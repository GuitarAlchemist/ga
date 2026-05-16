namespace GA.Business.Core.Tests.Orchestration;

using GA.Business.Core.Orchestration.Services;

[TestFixture]
public class ConversationHistoryStoreTests
{
    [Test]
    public void AddTurn_Then_GetHistory_RoundTrips()
    {
        var store = new ConversationHistoryStore();
        store.AddTurn("s1", "user", "hello");
        store.AddTurn("s1", "assistant", "hi");

        var history = store.GetHistory("s1");

        Assert.That(history.Count, Is.EqualTo(2));
        Assert.That(history[0].Role, Is.EqualTo("user"));
        Assert.That(history[0].Content, Is.EqualTo("hello"));
        Assert.That(history[1].Role, Is.EqualTo("assistant"));
        Assert.That(history[1].Content, Is.EqualTo("hi"));
    }

    [Test]
    public void GetHistory_UnknownSession_ReturnsEmpty()
    {
        var store = new ConversationHistoryStore();
        Assert.That(store.GetHistory("nope"), Is.Empty);
    }

    [Test]
    public void AddTurn_PerSessionCap_Holds_At_50()
    {
        var store = new ConversationHistoryStore();

        // 60 turns; per-session cap should drop the oldest 10 so we keep the last 50.
        for (var i = 0; i < 60; i++)
            store.AddTurn("s1", "user", $"t{i}");

        var history = store.GetHistory("s1");
        Assert.That(history.Count, Is.EqualTo(50));
        Assert.That(history[0].Content, Is.EqualTo("t10"));
        Assert.That(history[^1].Content, Is.EqualTo("t59"));
    }

    [Test]
    public void EvictsOldestSessions_When_GlobalCap_Exceeded()
    {
        // The store caps total sessions; once we cross the cap, the LRU eviction
        // batch should prune the oldest sessions. Touch a "hot" session along the
        // way so eviction prefers stale sessions over it.
        var store = new ConversationHistoryStore();

        store.AddTurn("hot", "user", "first");

        // Push past the global cap (1000) with a wide enough margin to guarantee
        // at least one full eviction batch fires regardless of internal threshold.
        for (var i = 0; i < 1200; i++)
        {
            store.AddTurn($"stale_{i:0000}", "user", "x");

            // Periodically re-touch "hot" so its last-access stays recent. Order
            // matters: eviction picks the oldest-touched sessions, so this is
            // the contract we're asserting.
            if (i % 50 == 0)
                store.AddTurn("hot", "user", $"keepalive_{i}");
        }

        Assert.That(store.GetHistory("hot").Count, Is.GreaterThan(0),
            "the recently-touched 'hot' session must survive eviction");

        // At least some of the oldest stale_* sessions should have been pruned.
        var earlyStaleSurvivors = 0;
        for (var i = 0; i < 100; i++)
        {
            if (store.GetHistory($"stale_{i:0000}").Count > 0)
                earlyStaleSurvivors++;
        }

        Assert.That(earlyStaleSurvivors, Is.LessThan(100),
            "the oldest 100 stale sessions cannot all survive once we have 1200 sessions and a 1000-session cap");
    }

    [Test]
    public void FormatAsContext_Returns_Newline_Joined_Recent_Turns()
    {
        var store = new ConversationHistoryStore();
        store.AddTurn("s1", "user", "a");
        store.AddTurn("s1", "assistant", "b");
        store.AddTurn("s1", "user", "c");

        var ctx = store.FormatAsContext("s1", maxTurns: 2);

        Assert.That(ctx, Is.EqualTo("[assistant]: b\n[user]: c"));
    }
}
