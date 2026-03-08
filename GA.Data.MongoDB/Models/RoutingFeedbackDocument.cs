namespace GA.Data.MongoDB.Models;

/// <summary>
/// Persisted routing correction entry. Each document records a single instance of a user
/// indicating that <see cref="WrongAgentId"/> was selected when <see cref="CorrectAgentId"/>
/// should have been used.
/// </summary>
[PublicAPI]
public sealed record RoutingFeedbackDocument
{
    /// <summary>
    /// Deterministic ID derived from the agent IDs and timestamp to avoid duplicate inserts
    /// on retry. Format: <c>feedback_{wrongAgentId}_{correctAgentId}_{yyyyMMddHHmmss}</c>.
    /// </summary>
    [BsonId]
    [BsonElement("_id")]
    public required string Id { get; init; }

    /// <summary>The original user query that was mis-routed.</summary>
    public required string Query { get; init; }

    /// <summary>The agent that was incorrectly selected.</summary>
    public required string WrongAgentId { get; init; }

    /// <summary>The agent that should have been selected.</summary>
    public required string CorrectAgentId { get; init; }

    /// <summary>UTC timestamp of the correction.</summary>
    public required DateTimeOffset Timestamp { get; init; }

    /// <summary>
    /// Builds a deterministic document ID for the given correction event.
    /// </summary>
    public static string BuildId(string wrongAgentId, string correctAgentId, DateTimeOffset timestamp) =>
        $"feedback_{wrongAgentId}_{correctAgentId}_{timestamp:yyyyMMddHHmmss}";
}
