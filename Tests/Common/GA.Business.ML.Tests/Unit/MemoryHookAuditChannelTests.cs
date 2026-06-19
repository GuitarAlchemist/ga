namespace GA.Business.ML.Tests.Unit;

using System.Collections.Concurrent;
using GA.Business.ML.Agents;
using GA.Business.ML.Agents.Hooks;
using GA.Business.ML.Agents.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

/// <summary>
/// Pins the dropped-response audit channel (PR #174 follow-up). The hook
/// silently skips writes when <see cref="AgentResponse.Confidence"/> &lt; 0.7
/// OR <see cref="AgentResponse.Result"/> length &lt; 100. Without an audit
/// log, operators can't tell whether "no entries in the transcript store"
/// means "MemoryHook is broken" or "the chatbot rarely meets threshold."
/// </summary>
/// <remarks>
/// Behavior contract pinned here:
/// <list type="bullet">
/// <item>First drop per reason logs at Information.</item>
/// <item>Every 100 drops per reason logs a summary at Information.</item>
/// <item>Every drop logs at Debug (regardless of count).</item>
/// <item>Confidence floor and content-length floor are tracked as distinct reasons.</item>
/// <item>Successful writes (above both thresholds) emit no audit messages.</item>
/// <item>Null Response is not counted as a drop — there's nothing to drop.</item>
/// </list>
/// </remarks>
[TestFixture]
public class MemoryHookAuditChannelTests
{
    private string _tempDir = string.Empty;
    private string _memoryPath = string.Empty;
    private string _transcriptPath = string.Empty;
    private MemoryStore _store = null!;
    private ChatTranscriptStore _transcriptStore = null!;
    private RecordingLogger<MemoryHook> _logger = null!;
    private MemoryHook _hook = null!;

    [SetUp]
    public void SetUp()
    {
        _tempDir        = Path.Combine(Path.GetTempPath(), $"ga-audit-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
        _memoryPath     = Path.Combine(_tempDir, "memory.json");
        _transcriptPath = Path.Combine(_tempDir, "transcripts.json");
        _store          = new MemoryStore(_memoryPath);
        _transcriptStore = new ChatTranscriptStore(_transcriptPath);

        // EnrichOnRetrieve=false — these tests exercise OnResponseSent only;
        // we don't need the retrieval branch enabled.
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Memory:EnrichOnRetrieve"] = "false",
            })
            .Build();

        _logger = new RecordingLogger<MemoryHook>();
        _hook = new MemoryHook(_store, _transcriptStore, config, _logger);
    }

    [TearDown]
    public void TearDown()
    {
        try { Directory.Delete(_tempDir, recursive: true); } catch { /* best-effort */ }
    }

    [Test]
    public async Task LowConfidence_Drop_LogsFirstHitAtInformation()
    {
        await _hook.OnResponseSent(MakeCtx(confidence: 0.5f, contentLength: 200));

        var infos = _logger.Entries.Where(e => e.Level == LogLevel.Information).ToList();
        Assert.That(infos, Has.Count.EqualTo(1),
            "First-hit message should fire once at Information.");
        Assert.That(infos[0].Message, Does.Contain("low-confidence"));
        Assert.That(infos[0].Message, Does.Contain("first response dropped"));
    }

    [Test]
    public async Task ShortContent_Drop_LogsFirstHitAtInformation()
    {
        await _hook.OnResponseSent(MakeCtx(confidence: 0.95f, contentLength: 50));

        var infos = _logger.Entries.Where(e => e.Level == LogLevel.Information).ToList();
        Assert.That(infos, Has.Count.EqualTo(1));
        Assert.That(infos[0].Message, Does.Contain("short-content"));
    }

    [Test]
    public async Task LowConfidence_SubsequentDrops_DoNotLogFirstHitAgain()
    {
        // First drop: fires the first-hit Information message.
        await _hook.OnResponseSent(MakeCtx(confidence: 0.5f, contentLength: 200));
        // 50 more drops: none should fire another first-hit message.
        for (var i = 0; i < 50; i++)
            await _hook.OnResponseSent(MakeCtx(confidence: 0.5f, contentLength: 200));

        var infos = _logger.Entries.Where(e => e.Level == LogLevel.Information).ToList();
        Assert.That(infos, Has.Count.EqualTo(1),
            "First-hit log should be rate-limited to once per process per reason.");
    }

    [Test]
    public async Task LowConfidence_SummaryFires_EveryHundredDrops()
    {
        // 100 drops total -> 1 first-hit + 1 summary at count=100.
        for (var i = 0; i < 100; i++)
            await _hook.OnResponseSent(MakeCtx(confidence: 0.5f, contentLength: 200));

        var infos = _logger.Entries.Where(e => e.Level == LogLevel.Information).ToList();
        Assert.That(infos, Has.Count.EqualTo(2));
        Assert.That(infos[0].Message, Does.Contain("first response dropped"));
        Assert.That(infos[1].Message, Does.Contain("low-confidence drops = 100"));
    }

    [Test]
    public async Task LowConfidence_AndShortContent_TrackedIndependently()
    {
        await _hook.OnResponseSent(MakeCtx(confidence: 0.5f,  contentLength: 200));
        await _hook.OnResponseSent(MakeCtx(confidence: 0.95f, contentLength: 50));

        var infos = _logger.Entries.Where(e => e.Level == LogLevel.Information).ToList();
        Assert.That(infos, Has.Count.EqualTo(2),
            "Each reason should fire its own first-hit Information message.");
        Assert.That(infos.Any(e => e.Message.Contains("low-confidence")));
        Assert.That(infos.Any(e => e.Message.Contains("short-content")));
    }

    [Test]
    public async Task SuccessfulWrite_AboveBothThresholds_EmitsNoAuditMessages()
    {
        await _hook.OnResponseSent(MakeCtx(confidence: 0.95f, contentLength: 200, sessionId: "s1"));

        var infos = _logger.Entries.Where(e => e.Level == LogLevel.Information).ToList();
        Assert.That(infos, Is.Empty,
            "A response that meets both thresholds should not produce an audit log entry.");
        Assert.That(_logger.Entries.Where(e =>
                e.Level == LogLevel.Debug && e.Message.Contains("drop")),
            Is.Empty,
            "Successful writes should not record a Debug drop either.");
    }

    [Test]
    public async Task NullResponse_NotCountedAsDrop()
    {
        // ctx with Response==null: nothing to write, but it's NOT a "drop"
        // — the audit channel should distinguish this from below-threshold.
        var ctx = new ChatHookContext
        {
            OriginalMessage = "hi",
            CurrentMessage  = "hi",
            CorrelationId   = Guid.NewGuid(),
            SessionId       = "sess-A",
            Response        = null,
        };

        await _hook.OnResponseSent(ctx);

        var infos = _logger.Entries.Where(e => e.Level == LogLevel.Information).ToList();
        Assert.That(infos, Is.Empty,
            "Null Response is 'nothing to write,' not 'dropped because below threshold' — no audit log.");
    }

    [Test]
    public async Task ConcurrentDrops_ExactlyOneFirstHit_AndCorrectSummaryCount()
    {
        // PR #177 review (correctness MED-1) regression pin: under
        // concurrent OnResponseSent, the previous CompareExchange-gated
        // design could let one thread win the first-hit CAS with count=100
        // while another thread saw count=1 with firstForReason=false, both
        // suppressing the count=100 summary AND attributing wrong detail
        // to the first-hit log. The fixed design gates on count == 1
        // directly so exactly one thread observes the first hit. Run 500
        // parallel drops — expect exactly 1 first-hit + 5 summaries.
        const int total = 500;
        await Parallel.ForEachAsync(
            Enumerable.Range(0, total),
            new ParallelOptions { MaxDegreeOfParallelism = 16 },
            async (_, _) => await _hook.OnResponseSent(MakeCtx(confidence: 0.5f, contentLength: 200)));

        var infos = _logger.Entries.Where(e => e.Level == LogLevel.Information).ToList();
        var firstHits = infos.Count(e => e.Message.Contains("first response dropped"));
        var summaries = infos.Count(e => e.Message.Contains("low-confidence drops ="));

        Assert.That(firstHits, Is.EqualTo(1),
            "Exactly one thread must observe count == 1 (no CAS race) — got first-hits = " + firstHits);
        Assert.That(summaries, Is.EqualTo(total / 100),
            $"Summary log must fire at every 100-multiple (5 times for total={total}).");
    }

    [Test]
    public async Task EveryDrop_EmitsDebugLog()
    {
        // Three drops, all at Debug level (regardless of summary cadence).
        await _hook.OnResponseSent(MakeCtx(confidence: 0.5f, contentLength: 200));
        await _hook.OnResponseSent(MakeCtx(confidence: 0.6f, contentLength: 200));
        await _hook.OnResponseSent(MakeCtx(confidence: 0.95f, contentLength: 30));

        var debugs = _logger.Entries
            .Where(e => e.Level == LogLevel.Debug && e.Message.Contains("MemoryHook drop"))
            .ToList();
        Assert.That(debugs, Has.Count.EqualTo(3),
            "Each drop must emit one Debug-level audit entry — that's the per-drop forensic channel.");
    }

    // ─── Helpers ─────────────────────────────────────────────────────────

    private static ChatHookContext MakeCtx(
        float confidence,
        int contentLength,
        string? sessionId = "sess-A") => new()
    {
        OriginalMessage = "hi",
        CurrentMessage  = "hi",
        CorrelationId   = Guid.NewGuid(),
        SessionId       = sessionId,
        Response = new AgentResponse
        {
            AgentId    = "test-agent",
            Result     = new string('x', contentLength),
            Confidence = confidence,
        },
    };

    /// <summary>
    /// Captures log entries in-memory for assertion. Thread-safe so the
    /// 100-drop summary test can hit the logger concurrently without
    /// racing on the entries list (we don't actually use parallelism in
    /// these tests, but the cost of safety is zero).
    /// </summary>
    private sealed class RecordingLogger<T> : ILogger<T>
    {
        public ConcurrentQueue<LogEntry> Entries { get; } = new();

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel, EventId eventId, TState state,
            Exception? exception, Func<TState, Exception?, string> formatter) => Entries.Enqueue(new LogEntry(logLevel, formatter(state, exception)));
    }

    private sealed record LogEntry(LogLevel Level, string Message);
}
