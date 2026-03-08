namespace GA.Business.ML.Agents;

/// <summary>
/// Records routing corrections and exposes per-agent bias for future routing decisions.
/// </summary>
/// <remarks>
/// Inspired by TARS exploration: "Learning feedback loop — user corrections compound into
/// better routing over time." Each correction nudges agent scores up (rewarded) or down
/// (penalized) by a small fixed amount, capped to prevent runaway bias.
/// </remarks>
public interface IRoutingFeedback
{
    /// <summary>
    /// Records that <paramref name="wrongAgentId"/> was selected when
    /// <paramref name="correctAgentId"/> should have been used.
    /// </summary>
    Task RecordCorrectionAsync(
        string query,
        string wrongAgentId,
        string correctAgentId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the accumulated bias for the given agent [-0.15, +0.15].
    /// Positive bias boosts routing score; negative bias suppresses it.
    /// </summary>
    float GetBias(string agentId);

    /// <summary>Gets all recorded corrections for diagnostics.</summary>
    IReadOnlyList<FeedbackEntry> GetEntries();
}

/// <summary>
/// In-memory implementation of <see cref="IRoutingFeedback"/>.
/// Survives the lifetime of the process; replace with a persistent store for production.
/// </summary>
public sealed class InMemoryRoutingFeedback : IRoutingFeedback
{
    private const float BiasStep = 0.05f;
    private const float MaxBias = 0.15f;

    private readonly Dictionary<string, int> _correctionCounts = [];
    private readonly List<FeedbackEntry> _entries = [];
    private readonly Lock _lock = new();

    public Task RecordCorrectionAsync(
        string query,
        string wrongAgentId,
        string correctAgentId,
        CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            _correctionCounts[wrongAgentId] = _correctionCounts.GetValueOrDefault(wrongAgentId) - 1;
            _correctionCounts[correctAgentId] = _correctionCounts.GetValueOrDefault(correctAgentId) + 1;

            _entries.Add(new FeedbackEntry(
                Timestamp: DateTimeOffset.UtcNow,
                Query: query,
                WrongAgentId: wrongAgentId,
                CorrectAgentId: correctAgentId));
        }

        return Task.CompletedTask;
    }

    public float GetBias(string agentId)
    {
        lock (_lock)
        {
            var count = _correctionCounts.GetValueOrDefault(agentId);
            return Math.Clamp(count * BiasStep, -MaxBias, MaxBias);
        }
    }

    public IReadOnlyList<FeedbackEntry> GetEntries()
    {
        lock (_lock) { return [.. _entries]; }
    }
}

/// <summary>A single routing correction event.</summary>
public record FeedbackEntry(
    DateTimeOffset Timestamp,
    string Query,
    string WrongAgentId,
    string CorrectAgentId);
