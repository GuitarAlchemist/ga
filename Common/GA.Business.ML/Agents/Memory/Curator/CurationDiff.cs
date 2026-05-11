namespace GA.Business.ML.Agents.Memory.Curator;

/// <summary>
/// Structured audit trail of what the curator did. Every output entry's
/// origin must trace to one or more input entries (or to a transcript
/// reference if it's a <see cref="NewItems"/> insight). Operator review
/// compares input → output via this diff rather than re-reading the whole
/// store.
/// </summary>
/// <remarks>
/// The curator MUST refuse to emit an output entry that doesn't appear in
/// at least one of: <see cref="Kept"/>, <see cref="Merged"/>,
/// <see cref="Replaced"/>, <see cref="NewItems"/>. The validator in
/// <see cref="MemoryCurator"/> rejects a response that breaks this rule —
/// see "NEVER invents facts" rule in the curation prompt.
/// </remarks>
public sealed record CurationDiff(
    IReadOnlyList<string> Kept,
    IReadOnlyList<MergeOp> Merged,
    IReadOnlyList<ReplaceOp> Replaced,
    IReadOnlyList<NewOp> NewItems,
    IReadOnlyList<DropOp> Dropped);

/// <summary>Output key produced by merging two or more input entries into one.</summary>
/// <param name="OutputKey">Key of the resulting output entry.</param>
/// <param name="InputKeys">Keys of the input entries collapsed into <see cref="OutputKey"/>.</param>
/// <param name="Rationale">One-sentence explanation grounded in the input content.</param>
public sealed record MergeOp(string OutputKey, IReadOnlyList<string> InputKeys, string Rationale);

/// <summary>An output entry that replaces an older (now-stale) input entry.</summary>
/// <param name="OutputKey">Key of the surviving output entry.</param>
/// <param name="SupersededKey">Key of the input entry that has been superseded.</param>
/// <param name="Rationale">Why the older entry is now stale (timestamp + content reference).</param>
public sealed record ReplaceOp(string OutputKey, string SupersededKey, string Rationale);

/// <summary>An emergent insight derived from transcript patterns (not present in the input store).</summary>
/// <param name="OutputKey">Key of the new output entry.</param>
/// <param name="SupportingTranscriptRefs">Session IDs and turn indexes the curator cites.</param>
/// <param name="Rationale">Why the pattern justifies storing this as durable memory.</param>
public sealed record NewOp(string OutputKey, IReadOnlyList<string> SupportingTranscriptRefs, string Rationale);

/// <summary>An input entry dropped from the output (e.g., one-off debugging note).</summary>
/// <param name="InputKey">Key of the dropped input entry.</param>
/// <param name="Rationale">Why the entry is not worth preserving.</param>
public sealed record DropOp(string InputKey, string Rationale);
