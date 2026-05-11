namespace GA.Business.ML.Tests.Unit;

using GA.Business.ML.Agents.Memory;

/// <summary>
/// Unit tests for <see cref="LegacyResponseMigration"/> — the one-shot
/// drain of pre-PR-174 <c>type=response</c> memory entries into the
/// transcript store. Pinned behaviors:
/// </summary>
/// <remarks>
/// <list type="bullet">
/// <item>Plan partitions correctly across mixed-type memory entries.</item>
/// <item>Idempotency: a re-run with the prior turns already in the
/// transcripts file drops the source entries without emitting duplicates.</item>
/// <item>Deterministic id derivation across invocations.</item>
/// <item>Sequence numbers continue above the existing maximum so the
/// transcript store's tie-breaking sort remains correct.</item>
/// <item>Non-response entries (durable knowledge — fact / preference / focus)
/// are never touched.</item>
/// </list>
/// </remarks>
[TestFixture]
public class LegacyResponseMigrationTests
{
    [Test]
    public void Plan_OnlyResponseEntries_MigratesAllAsAssistantTurns()
    {
        var t0 = new DateTimeOffset(2026, 3, 1, 12, 0, 0, TimeSpan.Zero);
        var entries = new[]
        {
            new MemoryEntry("response_a", "response", "answer about Cmaj7",
                Tags: [], Timestamp: t0, SessionId: null),
            new MemoryEntry("response_b", "response", "answer about scales",
                Tags: ["topic:scales"], Timestamp: t0.AddMinutes(1), SessionId: null),
        };

        var plan = LegacyResponseMigration.Plan(entries, []);

        Assert.That(plan.EntriesToMigrate.Count, Is.EqualTo(2));
        Assert.That(plan.EntriesToKeep, Is.Empty);
        Assert.That(plan.EntriesAlreadyMigrated, Is.Empty);
        Assert.That(plan.NewTranscriptTurns.Count, Is.EqualTo(2));
        Assert.That(plan.NewTranscriptTurns.All(t => t.Role == "assistant"));
        Assert.That(plan.NewTranscriptTurns.All(
            t => t.SessionId == LegacyResponseMigration.LegacySessionId));
        Assert.That(plan.IsNoOp, Is.False);
    }

    [Test]
    public void Plan_MixedTypes_OnlyResponseMigrated_DurableTypesPreserved()
    {
        var t0 = new DateTimeOffset(2026, 3, 1, 12, 0, 0, TimeSpan.Zero);
        var entries = new[]
        {
            new MemoryEntry("fact_audience",  "fact",       "intermediate guitarists",
                Tags: [], Timestamp: t0, SessionId: null),
            new MemoryEntry("response_x",     "response",   "answer",
                Tags: [], Timestamp: t0.AddSeconds(1), SessionId: null),
            new MemoryEntry("pref_voicings",  "preference", "drop-2",
                Tags: [], Timestamp: t0.AddSeconds(2), SessionId: null),
            new MemoryEntry("focus_jazz",     "focus",      "jazz comping",
                Tags: [], Timestamp: t0.AddSeconds(3), SessionId: null),
        };

        var plan = LegacyResponseMigration.Plan(entries, []);

        Assert.That(plan.EntriesToMigrate.Count, Is.EqualTo(1));
        Assert.That(plan.EntriesToMigrate.Single().Key, Is.EqualTo("response_x"));
        Assert.That(plan.EntriesToKeep.Select(e => e.Type),
            Is.EquivalentTo(new[] { "fact", "preference", "focus" }),
            "fact / preference / focus are durable knowledge — must be left untouched.");
    }

    [Test]
    public void Plan_IsNoOp_WhenNoResponseEntries()
    {
        var entries = new[]
        {
            new MemoryEntry("fact_a", "fact", "durable", Tags: [], Timestamp: DateTimeOffset.UtcNow, SessionId: null),
        };

        var plan = LegacyResponseMigration.Plan(entries, []);

        Assert.That(plan.IsNoOp, Is.True);
        Assert.That(plan.EntriesToKeep.Count, Is.EqualTo(1));
        Assert.That(plan.NewTranscriptTurns, Is.Empty);
    }

    [Test]
    public void Plan_AlreadyMigrated_ReportedSeparately_AndNoDuplicateTurnEmitted()
    {
        // Idempotency contract: if a prior --apply run produced a transcript
        // turn with the deterministic id, the current plan must not emit a
        // duplicate. The source entry still belongs in EntriesToMigrate's
        // "drop from memory.json" outcome — that's how a re-run finishes a
        // partially-completed migration.
        var t0 = new DateTimeOffset(2026, 3, 1, 12, 0, 0, TimeSpan.Zero);
        var entry = new MemoryEntry("response_x", "response", "answer", Tags: [], Timestamp: t0, SessionId: null);

        var priorTurn = new TranscriptTurnEntry(
            Id:        LegacyResponseMigration.DeriveId(entry.SessionId, entry.Key, entry.Timestamp),
            SessionId: LegacyResponseMigration.LegacySessionId,
            Role:      "assistant",
            Content:   "answer",
            Timestamp: t0,
            Sequence:  1);

        var plan = LegacyResponseMigration.Plan([entry], [priorTurn]);

        Assert.That(plan.EntriesAlreadyMigrated.Count, Is.EqualTo(1),
            "Entry whose deterministic id is already in transcripts must surface as already-migrated.");
        Assert.That(plan.EntriesToMigrate, Is.Empty,
            "Already-migrated entries must NOT also be in the new-migration list.");
        Assert.That(plan.NewTranscriptTurns, Is.Empty,
            "Idempotency: no duplicate transcript turn emitted.");
        Assert.That(plan.IsNoOp, Is.False,
            "Re-running with already-migrated entries is still actionable — the memory rows need removal.");
    }

    [Test]
    public void DeriveId_IsDeterministic_AcrossCalls()
    {
        var ts = new DateTimeOffset(2026, 3, 1, 12, 0, 0, TimeSpan.Zero);

        var id1 = LegacyResponseMigration.DeriveId(sessionId: null, "response_x", ts);
        var id2 = LegacyResponseMigration.DeriveId(sessionId: null, "response_x", ts);

        Assert.That(id1, Is.EqualTo(id2));
        Assert.That(id1, Does.StartWith("legacy-"));
        Assert.That(id1.Length, Is.EqualTo("legacy-".Length + 24));
    }

    [Test]
    public void DeriveId_DiffersForDifferentKeys()
    {
        var ts = DateTimeOffset.UtcNow;
        var a = LegacyResponseMigration.DeriveId(sessionId: null, "response_a", ts);
        var b = LegacyResponseMigration.DeriveId(sessionId: null, "response_b", ts);
        Assert.That(a, Is.Not.EqualTo(b));
    }

    [Test]
    public void DeriveId_DiffersForDifferentTimestamps()
    {
        var t1 = new DateTimeOffset(2026, 3, 1, 12, 0, 0, TimeSpan.Zero);
        var t2 = t1.AddSeconds(1);
        var a = LegacyResponseMigration.DeriveId(sessionId: null, "response_x", t1);
        var b = LegacyResponseMigration.DeriveId(sessionId: null, "response_x", t2);
        Assert.That(a, Is.Not.EqualTo(b));
    }

    [Test]
    public void DeriveId_DiffersForDifferentSessionIds()
    {
        // PR #176 review (LOW-DeriveId-Session) regression pin: even with
        // identical (Key, Timestamp), two different SessionIds must produce
        // distinct ids. Without this, per-session entries sharing a
        // sub-second timestamp would collide and lose content on
        // ChatTranscriptStore.Load()'s dictionary dedupe.
        var ts = new DateTimeOffset(2026, 3, 1, 12, 0, 0, TimeSpan.Zero);
        var globalId  = LegacyResponseMigration.DeriveId(sessionId: null,         "k", ts);
        var sessionA  = LegacyResponseMigration.DeriveId(sessionId: "session-A",  "k", ts);
        var sessionB  = LegacyResponseMigration.DeriveId(sessionId: "session-B",  "k", ts);
        Assert.That(globalId, Is.Not.EqualTo(sessionA));
        Assert.That(sessionA, Is.Not.EqualTo(sessionB));
        Assert.That(globalId, Is.Not.EqualTo(sessionB));
    }

    [Test]
    public void Plan_NewTurnsSequencedAboveExistingMaximum()
    {
        // Existing transcripts already have Sequence values; new turns must
        // start above the maximum so ChatTranscriptStore.GetRecentAsync's
        // Timestamp+Sequence tie-break remains correct.
        var t0 = new DateTimeOffset(2026, 3, 1, 12, 0, 0, TimeSpan.Zero);
        var existing = new[]
        {
            new TranscriptTurnEntry("existing-1", "live-session", "user",      "hi", t0.AddDays(-1), Sequence: 7),
            new TranscriptTurnEntry("existing-2", "live-session", "assistant", "hello", t0.AddDays(-1).AddSeconds(1), Sequence: 12),
        };
        var entries = new[]
        {
            new MemoryEntry("response_a", "response", "old answer", Tags: [], Timestamp: t0, SessionId: null),
        };

        var plan = LegacyResponseMigration.Plan(entries, existing);

        Assert.That(plan.NewTranscriptTurns.Single().Sequence, Is.GreaterThan(12),
            "New legacy turn's Sequence must start above the existing max (12).");
    }

    [Test]
    public void Plan_MigratedTurnsOrderedChronologicallyWithMonotonicSequence()
    {
        // Even when input order is jumbled, the emitted turns must be
        // ordered by source Timestamp and have strictly-increasing Sequence.
        // This guarantees the curator sees them in the natural order they
        // were originally produced.
        var t0 = new DateTimeOffset(2026, 3, 1, 12, 0, 0, TimeSpan.Zero);
        var entries = new[]
        {
            new MemoryEntry("c", "response", "third",  Tags: [], Timestamp: t0.AddMinutes(2), SessionId: null),
            new MemoryEntry("a", "response", "first",  Tags: [], Timestamp: t0,                  SessionId: null),
            new MemoryEntry("b", "response", "second", Tags: [], Timestamp: t0.AddMinutes(1), SessionId: null),
        };

        var plan = LegacyResponseMigration.Plan(entries, []);

        Assert.That(plan.NewTranscriptTurns.Select(t => t.Content),
            Is.EqualTo(new[] { "first", "second", "third" }));
        var seqs = plan.NewTranscriptTurns.Select(t => t.Sequence).ToArray();
        for (var i = 1; i < seqs.Length; i++)
        {
            Assert.That(seqs[i], Is.GreaterThan(seqs[i - 1]),
                $"Sequence must be strictly increasing across migrated turns: {string.Join(",", seqs)}");
        }
    }

    [Test]
    public void Plan_PreservesContentAndTimestampVerbatim()
    {
        // Migration must not paraphrase or normalize content/timestamp —
        // the value is a forensic record of what the chatbot said and when.
        var ts = new DateTimeOffset(2026, 3, 1, 12, 34, 56, 789, TimeSpan.Zero);
        var entry = new MemoryEntry("response_x", "response",
            "Cmaj7 = C E G B", Tags: ["a", "b"], Timestamp: ts, SessionId: null);

        var plan = LegacyResponseMigration.Plan([entry], []);
        var turn = plan.NewTranscriptTurns.Single();

        Assert.That(turn.Content,   Is.EqualTo("Cmaj7 = C E G B"));
        Assert.That(turn.Timestamp, Is.EqualTo(ts));
    }

    [Test]
    public void Plan_ResponseTypeMatchIsCaseInsensitive()
    {
        // Defensive: a few legacy entries may have "Response" or "RESPONSE"
        // capitalization. We still want to migrate them.
        var entries = new[]
        {
            new MemoryEntry("a", "Response", "answer1", Tags: [], Timestamp: DateTimeOffset.UtcNow, SessionId: null),
            new MemoryEntry("b", "RESPONSE", "answer2", Tags: [], Timestamp: DateTimeOffset.UtcNow.AddSeconds(1), SessionId: null),
            new MemoryEntry("c", "response", "answer3", Tags: [], Timestamp: DateTimeOffset.UtcNow.AddSeconds(2), SessionId: null),
        };

        var plan = LegacyResponseMigration.Plan(entries, []);

        Assert.That(plan.EntriesToMigrate.Count, Is.EqualTo(3));
    }

    [Test]
    public void Plan_NullArguments_Throw()
    {
        Assert.That(() => LegacyResponseMigration.Plan(null!, []), Throws.InstanceOf<ArgumentNullException>());
        Assert.That(() => LegacyResponseMigration.Plan([], null!), Throws.InstanceOf<ArgumentNullException>());
    }
}
