namespace GA.Business.ML.Agents.Memory.Curator;

using System.Text.Json;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

/// <summary>
/// Periodic curation pass over the chatbot <see cref="MemoryStore"/>. Modeled
/// on Anthropic's Dreams Research Preview — same intent (merge duplicates,
/// replace stale entries, surface insights), local implementation (uses our
/// existing <see cref="IChatClient"/> rather than the managed-agents API).
///
/// See <c>docs/plans/2026-05-10-feat-dreams-lite-memory-curator-plan.md</c>.
/// </summary>
/// <remarks>
/// <para>
/// <b>Two-way door.</b> The curator never mutates its input. It produces a
/// candidate <see cref="MemoryCurationResult"/> that the operator (or a
/// promote command) decides to persist or discard.
/// </para>
/// <para>
/// <b>Validation gate.</b> Even with a strong prompt, an LLM can return
/// malformed JSON or a diff that fails the trace-to-input rule. The
/// validator below rejects such responses with
/// <see cref="MemoryCurationException"/> — the candidate is never written.
/// </para>
/// </remarks>
public sealed class MemoryCurator(IChatClient chatClient, ILogger<MemoryCurator>? logger = null)
{
    public const string DefaultModelId = "claude-sonnet-5";

    /// <summary>
    /// Output-token budget for the curator's single LLM call. See the
    /// CurateAsync XML doc for why this needs to be generous. Sized for
    /// claude-sonnet-5: max_tokens covers thinking PLUS response text
    /// (adaptive thinking is on by default when the request omits the
    /// thinking parameter), and the Sonnet 5 tokenizer produces ~30% more
    /// tokens than Sonnet 4.6 for the same text — the previous 32_768 cap,
    /// sufficient for ≤300 entries on 4.6, could truncate equivalent output.
    /// </summary>
    public const int MaxOutputTokens = 65_536;

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = false,
    };

    /// <summary>
    /// Runs one curation pass. Returns the candidate entries + diff. Does not
    /// touch the live <see cref="MemoryStore"/> on disk.
    /// </summary>
    /// <exception cref="MemoryCurationException">
    /// The model response was unparseable, or the diff failed the trace-to-
    /// input audit (an output key with no matching input/transcript).
    /// </exception>
    public async Task<MemoryCurationResult> CurateAsync(
        MemoryCurationRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var messages = new List<ChatMessage>
        {
            new(ChatRole.System, CurationPromptBuilder.SystemPrompt),
            new(ChatRole.User,   CurationPromptBuilder.BuildUserMessage(request)),
        };

        // Generous output budget — discovered necessary by the first
        // smoke run (86 entries → truncated mid-Timestamp at byte ~2940
        // under the provider default cap of ~4096 tokens). The curator
        // produces ONE entry per kept/merged/new output × ~150 tokens for
        // the JSON shape, plus per-input rationales in the diff. 32k was
        // sized for ≤300 entries on Sonnet 4.6; 64k covers the same batch
        // on claude-sonnet-5, whose max_tokens also absorbs default-on
        // adaptive thinking and a ~30% heavier tokenizer. Bump if you see
        // truncation again.
        var options = new ChatOptions { MaxOutputTokens = MaxOutputTokens };

        var response = await chatClient.GetResponseAsync(messages, options, cancellationToken);
        var responseText = response.Messages.LastOrDefault()?.Text ?? "";

        if (string.IsNullOrWhiteSpace(responseText))
            throw new MemoryCurationException("Curator response was empty.");

        // Detect output-cap truncation early so the error message points at
        // the right fix (raise MaxOutputTokens, not "rewrite the prompt").
        // ChatFinishReason is a value type (readonly struct) with value-based
        // equality; this is a direct equality compare, not a string compare.
        // PR #170 review M2: include the numbers an operator needs to decide
        // whether to raise the cap or split the batch — output tokens used,
        // input entry count, and the configured cap.
        if (response.FinishReason == ChatFinishReason.Length)
        {
            var outputTokens = response.Usage?.OutputTokenCount;
            throw new MemoryCurationException(
                $"Curator response was truncated by the model's output cap " +
                $"(MaxOutputTokens={MaxOutputTokens}, outputTokensUsed=" +
                $"{(outputTokens.HasValue ? outputTokens.Value.ToString() : "unknown")}, " +
                $"inputEntryCount={request.ExistingEntries.Count}). " +
                $"Raise the cap or curate a smaller batch of entries.");
        }

        // Strip optional ```json``` fences (some providers wrap JSON even when
        // told not to — defensive parsing, not contract relaxation).
        var cleaned = StripCodeFence(responseText);

        CuratorPayload? payload;
        try
        {
            payload = JsonSerializer.Deserialize<CuratorPayload>(cleaned, JsonOpts);
        }
        catch (JsonException ex)
        {
            throw new MemoryCurationException(
                $"Curator response was not valid JSON: {ex.Message}", ex);
        }

        if (payload is null || payload.Entries is null || payload.Diff is null)
            throw new MemoryCurationException("Curator response missing 'entries' or 'diff'.");

        var diff = ToCurationDiff(payload.Diff);
        var candidateEntries = payload.Entries
            .Select(ToMemoryEntry)
            .ToList();

        ValidateTraceability(request, candidateEntries, diff);
        ValidateSessionScopePreserved(request, candidateEntries, diff);

        logger?.LogInformation(
            "MemoryCurator produced {OutCount} candidate entries from {InCount} inputs " +
            "({Kept} kept, {Merged} merged, {Replaced} replaced, {New} new, {Dropped} dropped).",
            candidateEntries.Count, request.ExistingEntries.Count,
            diff.Kept.Count, diff.Merged.Count, diff.Replaced.Count,
            diff.NewItems.Count, diff.Dropped.Count);

        return new MemoryCurationResult(
            CandidateEntries: candidateEntries,
            Diff: diff,
            ModelId: response.ModelId ?? DefaultModelId,
            InputTokens: response.Usage?.InputTokenCount ?? 0,
            OutputTokens: response.Usage?.OutputTokenCount ?? 0,
            GeneratedAt: DateTimeOffset.UtcNow);
    }

    // ── Validation ───────────────────────────────────────────────────────

    private static void ValidateTraceability(
        MemoryCurationRequest request,
        IReadOnlyList<MemoryEntry> candidate,
        CurationDiff diff)
    {
        var inputKeys = request.ExistingEntries.Select(e => e.Key).ToHashSet(StringComparer.Ordinal);

        // Every output entry must appear in exactly one of: kept, merged.outputKey,
        // replaced.outputKey, newItems.outputKey.
        var accountedOutputKeys = new HashSet<string>(diff.Kept, StringComparer.Ordinal);
        foreach (var m in diff.Merged)     accountedOutputKeys.Add(m.OutputKey);
        foreach (var r in diff.Replaced)   accountedOutputKeys.Add(r.OutputKey);
        foreach (var n in diff.NewItems)   accountedOutputKeys.Add(n.OutputKey);

        foreach (var entry in candidate)
        {
            if (!accountedOutputKeys.Contains(entry.Key))
                throw new MemoryCurationException(
                    $"Output entry '{entry.Key}' is not referenced in the diff. " +
                    "Every output entry must trace to an input or transcript.");
        }

        // Every input key must appear in exactly one of: kept, merged.inputKeys,
        // replaced.supersededKey, dropped.inputKey.
        var accountedInputKeys = new HashSet<string>(diff.Kept, StringComparer.Ordinal);
        foreach (var m in diff.Merged)   foreach (var ik in m.InputKeys) accountedInputKeys.Add(ik);
        foreach (var r in diff.Replaced) accountedInputKeys.Add(r.SupersededKey);
        foreach (var d in diff.Dropped)  accountedInputKeys.Add(d.InputKey);

        foreach (var key in inputKeys)
        {
            if (!accountedInputKeys.Contains(key))
                throw new MemoryCurationException(
                    $"Input entry '{key}' is missing from the diff. " +
                    "Every input key must appear in exactly one of kept/merged/replaced/dropped.");
        }
    }

    private static void ValidateSessionScopePreserved(
        MemoryCurationRequest request,
        IReadOnlyList<MemoryEntry> candidate,
        CurationDiff diff)
    {
        // Build a lookup of input Key → SessionId so we can check whether
        // a kept/merged/replaced output's session scope matches its source.
        var inputScopeByKey = request.ExistingEntries
            .GroupBy(e => e.Key, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.First().SessionId, StringComparer.Ordinal);

        var candidateByKey = candidate
            .GroupBy(e => e.Key, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.Ordinal);

        foreach (var key in diff.Kept)
        {
            if (inputScopeByKey.TryGetValue(key, out var inScope) &&
                candidateByKey.TryGetValue(key, out var outEntry) &&
                !string.Equals(inScope, outEntry.SessionId, StringComparison.Ordinal))
            {
                throw new MemoryCurationException(
                    $"Kept entry '{key}' changed SessionId scope from " +
                    $"'{inScope ?? "global"}' to '{outEntry.SessionId ?? "global"}'. " +
                    "Session scope must be preserved.");
            }
        }

        foreach (var merge in diff.Merged)
        {
            if (!candidateByKey.TryGetValue(merge.OutputKey, out var outEntry)) continue;
            // All inputs in a merge must share a session scope, and the output
            // must inherit it. Cross-scope merges leak.
            var inScopes = merge.InputKeys
                .Select(k => inputScopeByKey.TryGetValue(k, out var s) ? s : null)
                .Distinct(StringComparer.Ordinal)
                .ToList();
            if (inScopes.Count > 1)
                throw new MemoryCurationException(
                    $"Merged entry '{merge.OutputKey}' collapses inputs from different SessionId scopes " +
                    $"({string.Join(", ", inScopes.Select(s => s ?? "global"))}). " +
                    "Cross-scope merges leak — refuse.");
            if (inScopes.Count == 1 && !string.Equals(inScopes[0], outEntry.SessionId, StringComparison.Ordinal))
                throw new MemoryCurationException(
                    $"Merged entry '{merge.OutputKey}' changed scope from " +
                    $"'{inScopes[0] ?? "global"}' to '{outEntry.SessionId ?? "global"}'.");
        }
    }

    // ── JSON mapping ─────────────────────────────────────────────────────

    private static string StripCodeFence(string text)
    {
        var trimmed = text.Trim();
        if (trimmed.StartsWith("```", StringComparison.Ordinal))
        {
            var firstNewline = trimmed.IndexOf('\n');
            if (firstNewline > 0) trimmed = trimmed[(firstNewline + 1)..];
            if (trimmed.EndsWith("```", StringComparison.Ordinal))
                trimmed = trimmed[..^3];
        }
        return trimmed.Trim();
    }

    private static MemoryEntry ToMemoryEntry(CuratorEntry e) =>
        new(e.Key, e.Type, e.Content, e.Tags ?? [], e.Timestamp, e.SessionId);

    private static CurationDiff ToCurationDiff(CuratorDiff d) =>
        new(
            Kept:     d.Kept     ?? [],
            Merged:   (d.Merged   ?? []).Select(m => new MergeOp(m.OutputKey, m.InputKeys ?? [], m.Rationale ?? "")).ToList(),
            Replaced: (d.Replaced ?? []).Select(r => new ReplaceOp(r.OutputKey, r.SupersededKey, r.Rationale ?? "")).ToList(),
            NewItems: (d.NewItems ?? []).Select(n => new NewOp(n.OutputKey, n.SupportingTranscriptRefs ?? [], n.Rationale ?? "")).ToList(),
            Dropped:  (d.Dropped  ?? []).Select(p => new DropOp(p.InputKey, p.Rationale ?? "")).ToList());

    // Plain DTOs for deserialization — keep flexible (nullable) so a missing
    // section fails in the validator with a useful message instead of a
    // cryptic JsonException.
    private sealed record CuratorPayload(
        IReadOnlyList<CuratorEntry>? Entries,
        CuratorDiff? Diff);

    private sealed record CuratorEntry(
        string Key,
        string Type,
        string Content,
        string[]? Tags,
        DateTimeOffset Timestamp,
        string? SessionId);

    private sealed record CuratorDiff(
        IReadOnlyList<string>? Kept,
        IReadOnlyList<CuratorMerge>? Merged,
        IReadOnlyList<CuratorReplace>? Replaced,
        IReadOnlyList<CuratorNew>? NewItems,
        IReadOnlyList<CuratorDrop>? Dropped);

    private sealed record CuratorMerge(string OutputKey, IReadOnlyList<string>? InputKeys, string? Rationale);
    private sealed record CuratorReplace(string OutputKey, string SupersededKey, string? Rationale);
    private sealed record CuratorNew(string OutputKey, IReadOnlyList<string>? SupportingTranscriptRefs, string? Rationale);
    private sealed record CuratorDrop(string InputKey, string? Rationale);
}

/// <summary>Thrown when the curator's response can't be safely turned into a candidate.</summary>
public sealed class MemoryCurationException : Exception
{
    public MemoryCurationException(string message) : base(message) { }
    public MemoryCurationException(string message, Exception inner) : base(message, inner) { }
}
