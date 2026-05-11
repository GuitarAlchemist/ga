namespace GA.Business.ML.Agents.Memory.Curator;

/// <summary>
/// Output of a successful curation run. Mirrors Dreams' "completed" status
/// shape: candidate entries + an audit-trail diff + usage telemetry.
/// </summary>
/// <param name="CandidateEntries">
/// The new MemoryEntry set proposed by the curator. NOT yet written to disk;
/// caller decides whether to persist (operator review then promote).
/// </param>
/// <param name="Diff">Structured trace of how the candidate derives from the input.</param>
/// <param name="ModelId">Which model produced this result (audit / cost attribution).</param>
/// <param name="InputTokens">Reported by the model (0 if the provider didn't surface usage).</param>
/// <param name="OutputTokens">Same.</param>
/// <param name="GeneratedAt">When the curator finished.</param>
public sealed record MemoryCurationResult(
    IReadOnlyList<MemoryEntry> CandidateEntries,
    CurationDiff Diff,
    string ModelId,
    long InputTokens,
    long OutputTokens,
    DateTimeOffset GeneratedAt);
