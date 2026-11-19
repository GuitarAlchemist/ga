namespace GaChatbot.Api.Tests.Corpus;

using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

/// <summary>
/// Reads pruned canonical-trace signatures emitted by
/// <c>Scripts/extract-trace-dominators.ps1</c> and asserts a live trace's
/// step shape matches per (name, agent.id) per position.
/// </summary>
/// <remarks>
/// <para>
/// Signatures are committed under <c>state/quality/chatbot-qa/golden-traces/&lt;slug&gt;/_signature.json</c>.
/// Each signature captures the *stable* subset of the canonical: step name,
/// status, and the routing agent.id per step. Run-dependent values
/// (routing.confidence, response.length, traceId, elapsedMs) are intentionally
/// excluded so signatures are stable across environments and CI runs.
/// </para>
/// <para>
/// The slug derivation matches PowerShell's <c>ConvertTo-Slug</c> in the
/// recorder so file lookups round-trip between the .NET test and the
/// PowerShell tooling. Tests assert both directions in
/// <see cref="CanonicalSignatureCheckerTests"/>.
/// </para>
/// </remarks>
internal static class CanonicalSignatureChecker
{
    public sealed record StepSignature(string Name, string Status, string? AgentId);

    public sealed record Signature(
        int SchemaVersion,
        string PromptId,
        string? Prompt,
        string? Category,
        IReadOnlyList<StepSignature> Steps);

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        ReadCommentHandling  = JsonCommentHandling.Skip,
        AllowTrailingCommas  = true,
    };

    private static readonly Regex SlugReplace =
        new("[^a-z0-9]+", RegexOptions.Compiled);

    /// <summary>
    /// Mirrors PowerShell's <c>ConvertTo-Slug</c> in <c>record-golden-traces.ps1</c>:
    /// lowercase, replace non-alphanumeric runs with "-", trim, truncate to 64.
    /// Identical output is essential — signatures are looked up by slug.
    /// </summary>
    public static string ToSlug(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        var lower = text.ToLowerInvariant();
        var slug  = SlugReplace.Replace(lower, "-").Trim('-');
        if (slug.Length > 64) slug = slug[..64].TrimEnd('-');
        return slug;
    }

    public static string? ResolveSignaturePath(string goldenTracesRoot, string promptText)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(goldenTracesRoot);
        ArgumentException.ThrowIfNullOrWhiteSpace(promptText);
        var slug = ToSlug(promptText);
        var path = Path.Combine(goldenTracesRoot, slug, "_signature.json");
        return File.Exists(path) ? path : null;
    }

    public static Signature Load(string path)
    {
        var json = File.ReadAllText(path);
        return JsonSerializer.Deserialize<Signature>(json, JsonOptions)
            ?? throw new InvalidOperationException($"Signature at {path} deserialized to null");
    }

    /// <summary>
    /// Returns <c>null</c> if the trace matches the signature, otherwise a
    /// short human-readable diagnostic suitable for embedding in a test
    /// failure message.
    /// </summary>
    public static string? Compare(
        IReadOnlyList<(string Name, IDictionary<string, string?>? Attributes)> traceSteps,
        Signature signature)
    {
        ArgumentNullException.ThrowIfNull(traceSteps);
        ArgumentNullException.ThrowIfNull(signature);

        // llm.call steps record each LLM round trip; their number varies with tool rounds and
        // the critique loop, so they are not part of the canonical shape. Dropped from both
        // sides, so a signature generated before scripts/extract-trace-dominators.ps1 filtered
        // them still compares on its stable steps.
        traceSteps = [.. traceSteps.Where(s => !IsLlmCall(s.Name))];
        var canonicalSteps = signature.Steps.Where(s => !IsLlmCall(s.Name)).ToList();

        for (var i = 0; i < canonicalSteps.Count; i++)
        {
            var canon = canonicalSteps[i];

            if (i >= traceSteps.Count)
            {
                return $"signature expects step '{canon.Name}' at position {i} but trace has only {traceSteps.Count} step(s)";
            }

            var actual = traceSteps[i];
            if (!string.Equals(actual.Name, canon.Name, StringComparison.OrdinalIgnoreCase))
            {
                return $"position {i}: signature expects '{canon.Name}' but trace has '{actual.Name}'";
            }

            // agent.id check fires only when the signature recorded one for
            // this position. Framing steps (chat.request, response.emit) have
            // null agentId in the signature and intentionally skip this check.
            if (canon.AgentId is not null)
            {
                var actualAgentId =
                    actual.Attributes is not null
                    && actual.Attributes.TryGetValue("agent.id", out var a)
                        ? a : null;

                if (!string.Equals(actualAgentId, canon.AgentId, StringComparison.OrdinalIgnoreCase))
                {
                    return $"step '{canon.Name}': signature expects agent.id '{canon.AgentId}' but trace has '{actualAgentId ?? "<null>"}'";
                }
            }
        }

        return null;
    }

    private static bool IsLlmCall(string stepName) =>
        string.Equals(stepName, "llm.call", StringComparison.OrdinalIgnoreCase);
}
