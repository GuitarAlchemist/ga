namespace GA.Business.ML.Agents.Memory.Curator;

using System.Text;
using System.Text.Json;

/// <summary>
/// Builds the prompt that defines the curator's contract. Kept separate from
/// <see cref="MemoryCurator"/> so the prompt can be A/B-tested independently
/// — prompt churn is expected; orchestration is stable.
/// </summary>
/// <remarks>
/// The contract is intentionally narrow: produce JSON, every output entry
/// must trace to an input or transcript, session scope must be preserved.
/// Violating any of these makes the response unparseable by the validator
/// in <see cref="MemoryCurator"/> — the curator returns a failure rather
/// than emitting a corrupt candidate.
/// </remarks>
internal static class CurationPromptBuilder
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        WriteIndented = false,
        PropertyNameCaseInsensitive = true,
    };

    public const string SystemPrompt = """
        You are a memory curator. You receive an existing MemoryStore (JSON array
        of entries with keys: Key, Type, Content, Tags, Timestamp, SessionId) and
        optional past chat transcripts. Produce a NEW MemoryStore that:

        1. Merges duplicate entries (same fact stated different ways) into one.
        2. Replaces entries contradicted by later evidence — use Timestamp to
           decide which is later. The newer wins.
        3. Surfaces new memory entries when transcripts show a recurring pattern
           that is not already captured.
        4. NEVER invents facts not grounded in the inputs.
        5. Preserves SessionId scope. NEVER move a session-scoped entry to
           global (SessionId=null), nor a global entry to a session.

        Respond with a SINGLE JSON object and NOTHING ELSE — no prose, no
        markdown fences. The object has exactly two keys:

        {
          "entries": [
            { "Key": "...", "Type": "...", "Content": "...",
              "Tags": ["..."], "Timestamp": "ISO-8601",
              "SessionId": "..." or null }
          ],
          "diff": {
            "kept":     ["inputKey1", "inputKey2"],
            "merged":   [{ "outputKey": "...", "inputKeys": ["..."],
                           "rationale": "..." }],
            "replaced": [{ "outputKey": "...", "supersededKey": "...",
                           "rationale": "..." }],
            "newItems": [{ "outputKey": "...",
                           "supportingTranscriptRefs": ["sessionId:turnIndex"],
                           "rationale": "..." }],
            "dropped":  [{ "inputKey": "...", "rationale": "..." }]
          }
        }

        Every Key in `entries` MUST appear in exactly one of `kept`,
        `merged.outputKey`, `replaced.outputKey`, or `newItems.outputKey`.
        Every input Key MUST appear in exactly one of `kept`,
        `merged.inputKeys`, `replaced.supersededKey`, or `dropped.inputKey`.
        Rationales must be one sentence each, grounded in the inputs.
        """;

    public static string BuildUserMessage(MemoryCurationRequest request)
    {
        var sb = new StringBuilder(capacity: 4096);
        sb.AppendLine("EXISTING MEMORY STORE:");
        sb.AppendLine(JsonSerializer.Serialize(request.ExistingEntries, JsonOpts));
        sb.AppendLine();
        sb.AppendLine("RECENT CHAT TRANSCRIPTS:");
        sb.AppendLine(JsonSerializer.Serialize(request.RecentTranscripts, JsonOpts));
        if (!string.IsNullOrWhiteSpace(request.Instructions))
        {
            sb.AppendLine();
            sb.AppendLine("OPERATOR INSTRUCTIONS:");
            sb.AppendLine(request.Instructions);
        }
        return sb.ToString();
    }
}
