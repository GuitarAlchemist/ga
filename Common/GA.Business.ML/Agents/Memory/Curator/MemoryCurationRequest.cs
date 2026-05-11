namespace GA.Business.ML.Agents.Memory.Curator;

/// <summary>
/// Input to <see cref="MemoryCurator.CurateAsync(MemoryCurationRequest, CancellationToken)"/>.
/// Mirrors the shape of Anthropic Dreams' input: an existing store + optional
/// past sessions + optional free-text instructions narrowing the scope.
/// </summary>
/// <param name="ExistingEntries">
/// Snapshot of the live <see cref="MemoryStore"/> entries. The curator never
/// mutates this list — output is a new candidate list.
/// </param>
/// <param name="RecentTranscripts">
/// Past chat transcripts the curator may mine for emergent insights. Empty
/// is fine — the curator can still dedupe / collapse based on
/// <paramref name="ExistingEntries"/> alone.
/// </param>
/// <param name="Instructions">
/// Optional operator hints (≤4096 chars to match Dreams' limit). For example:
/// "Focus on user preferences; ignore one-off debugging notes."
/// </param>
public sealed record MemoryCurationRequest(
    IReadOnlyList<MemoryEntry> ExistingEntries,
    IReadOnlyList<ChatTranscript> RecentTranscripts,
    string? Instructions = null);
