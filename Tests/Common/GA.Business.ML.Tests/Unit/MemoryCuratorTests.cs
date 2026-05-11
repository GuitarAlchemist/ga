namespace GA.Business.ML.Tests.Unit;

using System.Text.Json;
using GA.Business.ML.Agents.Memory;
using GA.Business.ML.Agents.Memory.Curator;
using Microsoft.Extensions.AI;

/// <summary>
/// Tests for <see cref="MemoryCurator"/> — the Dreams-lite curation pass.
/// Uses a stub <see cref="IChatClient"/> that returns hand-crafted JSON
/// payloads, so we exercise the curator's parsing + validation gates
/// without hitting a real LLM.
/// </summary>
/// <remarks>
/// See <c>docs/plans/2026-05-10-feat-dreams-lite-memory-curator-plan.md</c>.
/// The validator is load-bearing: it's what makes the curator safe to point
/// at production stores. Each test pins one validator rule.
/// </remarks>
[TestFixture]
public class MemoryCuratorTests
{
    // ─── Happy path ──────────────────────────────────────────────────────

    [Test]
    public async Task CurateAsync_HappyPath_MergesDuplicates()
    {
        var inputs = new List<MemoryEntry>
        {
            new("pref-guitar-1", "preference", "user plays Strat",       [], T(0), SessionId: null),
            new("pref-guitar-2", "preference", "user plays Stratocaster",[], T(1), SessionId: null),
            new("focus-1",       "focus",      "user is studying bebop", [], T(2), SessionId: null),
        };

        var stub = new StubChatClient(JsonSerializer.Serialize(new
        {
            entries = new[]
            {
                new { Key = "pref-guitar",   Type = "preference", Content = "user plays a Fender Stratocaster",
                      Tags = Array.Empty<string>(), Timestamp = T(1), SessionId = (string?)null },
                new { Key = "focus-1",       Type = "focus",      Content = "user is studying bebop",
                      Tags = Array.Empty<string>(), Timestamp = T(2), SessionId = (string?)null },
            },
            diff = new
            {
                kept     = new[] { "focus-1" },
                merged   = new[] { new { outputKey = "pref-guitar", inputKeys = new[] { "pref-guitar-1", "pref-guitar-2" },
                                         rationale = "Same instrument stated two ways." } },
                replaced = Array.Empty<object>(),
                newItems = Array.Empty<object>(),
                dropped  = Array.Empty<object>(),
            },
        }));

        var curator = new MemoryCurator(stub);
        var result = await curator.CurateAsync(
            new MemoryCurationRequest(inputs, [], null));

        Assert.That(result.CandidateEntries, Has.Count.EqualTo(2));
        Assert.That(result.Diff.Merged, Has.Count.EqualTo(1));
        Assert.That(result.Diff.Merged[0].InputKeys, Is.EquivalentTo(new[] { "pref-guitar-1", "pref-guitar-2" }));
        Assert.That(result.Diff.Kept, Does.Contain("focus-1"));
    }

    [Test]
    public async Task CurateAsync_HappyPath_ReplacesStale()
    {
        // Models a real replacement: one input ("focus") whose content is
        // now stale, replaced by a new output ("focus-current") with revised
        // content. The output is NOT a verbatim copy of any input.
        var inputs = new List<MemoryEntry>
        {
            new("focus", "focus", "user focuses on bebop", [], T(0), SessionId: null),
        };

        var stub = new StubChatClient(JsonSerializer.Serialize(new
        {
            entries = new[]
            {
                new { Key = "focus-current", Type = "focus",
                      Content = "user focuses on fingerstyle (changed from bebop per recent sessions)",
                      Tags = Array.Empty<string>(), Timestamp = T(10), SessionId = (string?)null },
            },
            diff = new
            {
                kept     = Array.Empty<string>(),
                merged   = Array.Empty<object>(),
                replaced = new[] { new { outputKey = "focus-current", supersededKey = "focus",
                                         rationale = "Recent sessions show fingerstyle, not bebop." } },
                newItems = Array.Empty<object>(),
                dropped  = Array.Empty<object>(),
            },
        }));

        var result = await new MemoryCurator(stub).CurateAsync(
            new MemoryCurationRequest(inputs, [], null));

        Assert.That(result.CandidateEntries, Has.Count.EqualTo(1));
        Assert.That(result.Diff.Replaced, Has.Count.EqualTo(1));
        Assert.That(result.Diff.Replaced[0].SupersededKey, Is.EqualTo("focus"));
        Assert.That(result.Diff.Replaced[0].OutputKey, Is.EqualTo("focus-current"));
    }

    // ─── Validator: trace to input ───────────────────────────────────────

    [Test]
    public void CurateAsync_OutputEntryNotInDiff_Throws()
    {
        // An output entry that never appears in kept/merged/replaced/newItems —
        // the curator must reject this rather than silently accepting an
        // ungrounded fact (covers "NEVER invents facts" rule).
        var inputs = new List<MemoryEntry>
        {
            new("real", "fact", "real input", [], T(0), SessionId: null),
        };

        var stub = new StubChatClient(JsonSerializer.Serialize(new
        {
            entries = new[]
            {
                new { Key = "real",          Type = "fact", Content = "real input",
                      Tags = Array.Empty<string>(), Timestamp = T(0), SessionId = (string?)null },
                new { Key = "hallucinated",  Type = "fact", Content = "made up",
                      Tags = Array.Empty<string>(), Timestamp = T(1), SessionId = (string?)null },
            },
            diff = new
            {
                kept     = new[] { "real" },
                merged   = Array.Empty<object>(),
                replaced = Array.Empty<object>(),
                newItems = Array.Empty<object>(),
                dropped  = Array.Empty<object>(),
            },
        }));

        Assert.That(async () => await new MemoryCurator(stub).CurateAsync(
                new MemoryCurationRequest(inputs, [], null)),
            Throws.TypeOf<MemoryCurationException>()
                  .With.Message.Contains("hallucinated"));
    }

    [Test]
    public void CurateAsync_InputKeyMissingFromDiff_Throws()
    {
        // Every input must be accounted for. If an input vanishes silently
        // (not in kept/merged/replaced/dropped), the curator hid a deletion —
        // refuse.
        var inputs = new List<MemoryEntry>
        {
            new("a", "fact", "first",  [], T(0), SessionId: null),
            new("b", "fact", "second", [], T(1), SessionId: null),
        };

        var stub = new StubChatClient(JsonSerializer.Serialize(new
        {
            entries = new[]
            {
                new { Key = "a", Type = "fact", Content = "first", Tags = Array.Empty<string>(),
                      Timestamp = T(0), SessionId = (string?)null },
            },
            diff = new
            {
                kept     = new[] { "a" },
                merged   = Array.Empty<object>(),
                replaced = Array.Empty<object>(),
                newItems = Array.Empty<object>(),
                dropped  = Array.Empty<object>(), // <-- "b" missing
            },
        }));

        Assert.That(async () => await new MemoryCurator(stub).CurateAsync(
                new MemoryCurationRequest(inputs, [], null)),
            Throws.TypeOf<MemoryCurationException>()
                  .With.Message.Contains("'b'"));
    }

    // ─── Validator: session-scope preservation ───────────────────────────

    [Test]
    public void CurateAsync_KeptEntryChangesScope_Throws()
    {
        var inputs = new List<MemoryEntry>
        {
            new("scoped", "fact", "session A's fact", [], T(0), SessionId: "session-A"),
        };

        var stub = new StubChatClient(JsonSerializer.Serialize(new
        {
            entries = new[]
            {
                new { Key = "scoped", Type = "fact", Content = "session A's fact",
                      Tags = Array.Empty<string>(), Timestamp = T(0), SessionId = (string?)null }, // <-- promoted to global
            },
            diff = new
            {
                kept     = new[] { "scoped" },
                merged   = Array.Empty<object>(),
                replaced = Array.Empty<object>(),
                newItems = Array.Empty<object>(),
                dropped  = Array.Empty<object>(),
            },
        }));

        Assert.That(async () => await new MemoryCurator(stub).CurateAsync(
                new MemoryCurationRequest(inputs, [], null)),
            Throws.TypeOf<MemoryCurationException>()
                  .With.Message.Contains("scope"));
    }

    [Test]
    public void CurateAsync_MergesAcrossSessionScopes_Throws()
    {
        // Cross-scope merges leak — session A's preference must NEVER merge
        // with session B's preference into a single output entry, even if
        // the content is identical.
        var inputs = new List<MemoryEntry>
        {
            new("a-pref", "preference", "wants jazz",   [], T(0), SessionId: "session-A"),
            new("b-pref", "preference", "wants jazz",   [], T(1), SessionId: "session-B"),
        };

        var stub = new StubChatClient(JsonSerializer.Serialize(new
        {
            entries = new[]
            {
                new { Key = "ab-pref", Type = "preference", Content = "wants jazz",
                      Tags = Array.Empty<string>(), Timestamp = T(1), SessionId = (string?)null },
            },
            diff = new
            {
                kept     = Array.Empty<string>(),
                merged   = new[] { new { outputKey = "ab-pref", inputKeys = new[] { "a-pref", "b-pref" },
                                         rationale = "Identical preference content." } },
                replaced = Array.Empty<object>(),
                newItems = Array.Empty<object>(),
                dropped  = Array.Empty<object>(),
            },
        }));

        Assert.That(async () => await new MemoryCurator(stub).CurateAsync(
                new MemoryCurationRequest(inputs, [], null)),
            Throws.TypeOf<MemoryCurationException>()
                  .With.Message.Contains("scope"));
    }

    // ─── Robustness: malformed response handling ─────────────────────────

    [Test]
    public void CurateAsync_NonJsonResponse_Throws()
    {
        var stub = new StubChatClient("Sure, here's the curation: ...");

        Assert.That(async () => await new MemoryCurator(stub).CurateAsync(
                new MemoryCurationRequest([], [], null)),
            Throws.TypeOf<MemoryCurationException>());
    }

    [Test]
    public async Task CurateAsync_JsonWrappedInCodeFence_ParsesAnyway()
    {
        // Some providers wrap JSON in ```json ... ``` even when told not to.
        // Curator strips this defensively (doesn't change the contract).
        var fenced = "```json\n" +
            JsonSerializer.Serialize(new
            {
                entries = Array.Empty<object>(),
                diff = new
                {
                    kept = Array.Empty<string>(),
                    merged = Array.Empty<object>(),
                    replaced = Array.Empty<object>(),
                    newItems = Array.Empty<object>(),
                    dropped = Array.Empty<object>(),
                },
            }) +
            "\n```";
        var stub = new StubChatClient(fenced);

        var result = await new MemoryCurator(stub).CurateAsync(
            new MemoryCurationRequest([], [], null));

        Assert.That(result.CandidateEntries, Is.Empty);
    }

    [Test]
    public void CurateAsync_EmptyResponse_Throws()
    {
        var stub = new StubChatClient("");

        Assert.That(async () => await new MemoryCurator(stub).CurateAsync(
                new MemoryCurationRequest([], [], null)),
            Throws.TypeOf<MemoryCurationException>().With.Message.Contains("empty"));
    }

    [Test]
    public void CurateAsync_OutputCapHit_ThrowsWithOperatorContext()
    {
        // PR #170 review N1 — the truncation-detection branch (FinishReason ==
        // Length) was added when an end-to-end smoke ran into a 4k-token output
        // cap. The unit tests couldn't reproduce this with a string-only stub
        // — we needed a stub that surfaces FinishReason. This test locks in
        // the M2 contract: the exception names the cap, the tokens used, and
        // the input entry count so an operator can decide raise-cap vs.
        // split-batch without re-running the curator in a debugger.
        var inputs = new List<MemoryEntry>
        {
            new("e1", "fact", "input one", [], T(0), SessionId: null),
            new("e2", "fact", "input two", [], T(1), SessionId: null),
        };
        var stub = new TruncatingStubChatClient(
            partialJson: "{\"entries\":[{\"Key\":\"e1\",\"Type\":\"fact\",",
            outputTokens: 4096);

        Assert.That(async () => await new MemoryCurator(stub).CurateAsync(
                new MemoryCurationRequest(inputs, [], null)),
            Throws.TypeOf<MemoryCurationException>()
                  .With.Message.Contains("truncated")
                  .And.Message.Contains("outputTokensUsed=4096")
                  .And.Message.Contains("inputEntryCount=2"));
    }

    // ─── Helpers ─────────────────────────────────────────────────────────

    private static DateTimeOffset T(int seconds) =>
        new(2026, 5, 10, 0, 0, seconds, TimeSpan.Zero);
}

/// <summary>
/// Stub IChatClient that always returns the same JSON payload. No real LLM
/// call. Used by every test in <see cref="MemoryCuratorTests"/>.
/// </summary>
file sealed class StubChatClient(string responseJson) : IChatClient
{
    public Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        var response = new ChatResponse(new ChatMessage(ChatRole.Assistant, responseJson))
        {
            ModelId = "stub-model",
        };
        return Task.FromResult(response);
    }

    public IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default) =>
        throw new NotImplementedException();

    public object? GetService(Type serviceType, object? serviceKey = null) => null;
    public void Dispose() { }
}

/// <summary>
/// Stub IChatClient that returns a response with
/// <see cref="ChatFinishReason.Length"/> (output-cap-hit signal) plus a
/// recorded usage count. Used by the truncation-path test to verify the
/// curator's M2 exception message includes the operator-actionable fields.
/// </summary>
file sealed class TruncatingStubChatClient(string partialJson, int outputTokens) : IChatClient
{
    public Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        var response = new ChatResponse(new ChatMessage(ChatRole.Assistant, partialJson))
        {
            ModelId = "stub-truncating-model",
            FinishReason = ChatFinishReason.Length,
            Usage = new UsageDetails { OutputTokenCount = outputTokens },
        };
        return Task.FromResult(response);
    }

    public IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default) =>
        throw new NotImplementedException();

    public object? GetService(Type serviceType, object? serviceKey = null) => null;
    public void Dispose() { }
}
