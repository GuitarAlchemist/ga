namespace GA.Business.ML.Agents.Memory;

using System.Security.Cryptography;
using System.Text;

/// <summary>
/// One-shot migration that drains legacy <c>type=response</c> entries from
/// <see cref="MemoryStore"/> into <see cref="ChatTranscriptStore"/>. Closes
/// the architectural gap documented in
/// <c>docs/solutions/architecture/2026-05-11-memoryhook-conflates-transcript-log-with-durable-memory.md</c>:
/// every chatbot response prior to PR #174 landed in the durable memory file
/// as <c>type=response</c>, but those rows are transient transcript content
/// — not durable knowledge — and they crowd out real <c>fact</c> /
/// <c>preference</c> / <c>focus</c> entries.
/// </summary>
/// <remarks>
/// <para>
/// <b>Pure planning, no IO.</b> <see cref="Plan"/> takes the loaded entries
/// and produces a <see cref="LegacyResponseMigrationPlan"/> describing what
/// would change. Callers (the <c>ga-memory migrate-transcripts</c> CLI) own
/// the read/write side-effects so the planning logic stays unit-testable.
/// </para>
/// <para>
/// <b>Idempotent.</b> Migrated turns get a deterministic id derived from
/// <c>SHA256("legacy|" + key + "|" + timestamp)[..24]</c> with a
/// <c>"legacy-"</c> prefix. Re-running the plan against a transcripts file
/// that already contains those ids produces an empty migration set —
/// operators can safely re-invoke <c>migrate-transcripts --apply</c>.
/// </para>
/// <para>
/// <b>Session bucket.</b> All legacy entries land in a single
/// <see cref="LegacySessionId"/> bucket. The historic store was 100% global
/// (SessionId=null), so there is no per-session information to recover.
/// Bucketing under one synthetic session keeps the curator's cross-session
/// <c>GetRecentAsync</c> from interleaving 86 ancient assistant turns into
/// every live session's "most-recent" view.
/// </para>
/// <para>
/// <b>Role.</b> All migrated turns are <c>"assistant"</c> — they came from
/// <c>MemoryHook.OnResponseSent</c>, which only ever wrote the response
/// text. The originating user prompts were never persisted in the legacy
/// flow and are unrecoverable.
/// </para>
/// </remarks>
public static class LegacyResponseMigration
{
    /// <summary>
    /// Synthetic session id used for every migrated turn. Choosing a single
    /// non-null value here (rather than null / null-by-design) preserves the
    /// invariant that every <see cref="TranscriptTurnEntry.SessionId"/> is a
    /// non-empty string — <see cref="ChatTranscriptStore.AppendTurn"/>
    /// enforces this for live writes and the migration must not introduce
    /// a class of entries that violates it.
    /// </summary>
    public const string LegacySessionId = "legacy-migrated";

    /// <summary>
    /// Type marker on the source entry that this migration drains. All other
    /// types (<c>fact</c>, <c>preference</c>, <c>focus</c>, etc.) are durable
    /// knowledge and must not be touched.
    /// </summary>
    public const string MigrateableType = "response";

    /// <summary>
    /// Builds a migration plan from the current contents of both stores. The
    /// caller is responsible for loading <paramref name="memoryEntries"/>
    /// and <paramref name="existingTranscriptTurns"/> from disk and for
    /// persisting the plan's outputs.
    /// </summary>
    public static LegacyResponseMigrationPlan Plan(
        IReadOnlyList<MemoryEntry> memoryEntries,
        IReadOnlyList<TranscriptTurnEntry> existingTranscriptTurns)
    {
        ArgumentNullException.ThrowIfNull(memoryEntries);
        ArgumentNullException.ThrowIfNull(existingTranscriptTurns);

        var existingIds = existingTranscriptTurns
            .Select(t => t.Id)
            .ToHashSet(StringComparer.Ordinal);

        var toMigrate    = new List<MemoryEntry>();
        var toKeep       = new List<MemoryEntry>();
        var newTurns     = new List<TranscriptTurnEntry>();
        var alreadyDone  = new List<MemoryEntry>();

        // Seed sequence numbers above the existing maximum so chronological
        // tiebreaking via Sequence in ChatTranscriptStore.GetRecentAsync
        // continues to work for both legacy and live turns. PR #174 review
        // CR-H2 — sequence breaks Timestamp ties; new legacy turns sorted
        // by their original Timestamp are safe even sharing the bucket.
        var seqStart = existingTranscriptTurns.Count == 0
            ? 0
            : existingTranscriptTurns.Max(t => t.Sequence);

        foreach (var entry in memoryEntries
                     .OrderBy(e => e.Timestamp))   // chronological for deterministic Sequence ordering
        {
            if (!string.Equals(entry.Type, MigrateableType, StringComparison.OrdinalIgnoreCase))
            {
                toKeep.Add(entry);
                continue;
            }

            // PR #176 review (correctness LOW-DeriveId-Session): include
            // the source SessionId. The historic store was empirically
            // global (SessionId=null), but if a legacy file ever contains
            // per-session entries sharing (Key, Timestamp) across different
            // sessions, a (Key+Timestamp)-only hash would collide and
            // silently drop content on ChatTranscriptStore.Load()'s dedupe.
            // Costs nothing — closes a future-drift foot-gun.
            var id = DeriveId(entry.SessionId, entry.Key, entry.Timestamp);
            if (existingIds.Contains(id))
            {
                // Already migrated in a prior run — drop it from the source
                // (so re-running --apply still completes the move), but do
                // not re-emit a new transcript turn.
                alreadyDone.Add(entry);
                continue;
            }

            seqStart++;
            newTurns.Add(new TranscriptTurnEntry(
                Id:            id,
                SessionId:     LegacySessionId,
                Role:          "assistant",
                Content:       entry.Content,
                Timestamp:     entry.Timestamp,
                Sequence:      seqStart,
                CorrelationId: null,
                AgentId:       null));
            toMigrate.Add(entry);
        }

        return new LegacyResponseMigrationPlan(
            EntriesToMigrate:          toMigrate,
            EntriesToKeep:             toKeep,
            EntriesAlreadyMigrated:    alreadyDone,
            NewTranscriptTurns:        newTurns,
            ExistingTranscriptTurns:   existingTranscriptTurns);
    }

    /// <summary>
    /// Deterministic transcript-turn id for a legacy memory entry. The
    /// <c>(SessionId, Key, Timestamp)</c> triple uniquely identifies a
    /// legacy entry — MemoryStore's primary key is <c>(SessionId, Key)</c>,
    /// so any two entries in the same source file are distinguishable
    /// by that key plus their timestamp. Null SessionId (the global
    /// partition) is normalized to the empty string in the hash payload
    /// so global vs. session-scoped entries with the same Key/Timestamp
    /// don't collide.
    /// </summary>
    /// <remarks>
    /// PR #176 review (correctness LOW-DeriveId-Session) added the
    /// SessionId dimension. The historic store was empirically global,
    /// but the hash now defends against any future legacy file with
    /// per-session response entries.
    /// </remarks>
    public static string DeriveId(string? sessionId, string key, DateTimeOffset timestamp)
    {
        ArgumentException.ThrowIfNullOrEmpty(key);
        var payload = $"legacy|{sessionId ?? string.Empty}|{key}|{timestamp:O}";
        var bytes   = SHA256.HashData(Encoding.UTF8.GetBytes(payload));
        return "legacy-" + Convert.ToHexString(bytes)[..24].ToLowerInvariant();
    }
}

/// <summary>
/// Outcome of <see cref="LegacyResponseMigration.Plan"/>. Holds every
/// fragment the caller needs to execute the migration — the entries that
/// will be removed from memory.json, the entries that will be kept, the
/// new transcript turns to append, and a record of entries skipped because
/// a prior run already migrated them.
/// </summary>
/// <param name="EntriesToMigrate">Source memory entries of type
/// <c>response</c> that will be drained into the transcripts file.</param>
/// <param name="EntriesToKeep">Source memory entries the migration leaves
/// untouched — anything that is not type=response.</param>
/// <param name="EntriesAlreadyMigrated">Source memory entries that map to
/// transcript-turn ids already present in the transcripts file. These will
/// be dropped from memory.json on --apply (the move is what finishes the
/// migration) but no new transcript turn is emitted for them.</param>
/// <param name="NewTranscriptTurns">Transcript turns that will be appended
/// to the transcripts file. Sequence numbers are pre-assigned above the
/// existing maximum.</param>
/// <param name="ExistingTranscriptTurns">Snapshot of the transcripts file
/// before migration. Useful for the writer to merge + atomic-rename.</param>
public sealed record LegacyResponseMigrationPlan(
    IReadOnlyList<MemoryEntry> EntriesToMigrate,
    IReadOnlyList<MemoryEntry> EntriesToKeep,
    IReadOnlyList<MemoryEntry> EntriesAlreadyMigrated,
    IReadOnlyList<TranscriptTurnEntry> NewTranscriptTurns,
    IReadOnlyList<TranscriptTurnEntry> ExistingTranscriptTurns)
{
    /// <summary>
    /// True when running the plan would make zero changes. Lets the CLI
    /// report "nothing to do — already migrated" without printing an empty
    /// dry-run table.
    /// </summary>
    public bool IsNoOp =>
        EntriesToMigrate.Count == 0 && EntriesAlreadyMigrated.Count == 0;
}
