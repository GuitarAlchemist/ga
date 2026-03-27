namespace GaApi.Services;

using System.Text.Json;
using System.Text.Json.Serialization;
using Hubs;
using Microsoft.AspNetCore.SignalR;
using Path = System.IO.Path;

/// <summary>
///     Reads and updates tetravalent belief state files from the Demerzel governance directory.
///     Belief states use four-valued logic: T (true), F (false), U (unknown), C (contradictory).
/// </summary>
public sealed class BeliefStateService(
    IConfiguration configuration,
    ILogger<BeliefStateService> logger,
    AlgedonicSignalService algedonicSignalService,
    IHubContext<GovernanceHub> hubContext)
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    /// <summary>
    ///     Read all belief state files from the beliefs directory.
    /// </summary>
    public List<BeliefState> GetBeliefs()
    {
        var beliefsDir = GetBeliefsDirectory();
        if (beliefsDir == null || !Directory.Exists(beliefsDir))
        {
            logger.LogWarning("Beliefs directory not found");
            return [];
        }

        var beliefs = new List<BeliefState>();
        foreach (var file in Directory.EnumerateFiles(beliefsDir, "*.belief.json"))
        {
            try
            {
                var json = File.ReadAllText(file);
                var belief = JsonSerializer.Deserialize<BeliefState>(json, JsonOptions);
                if (belief != null)
                {
                    // Use filename as ID if not set
                    belief = belief with
                    {
                        Id = belief.Id ?? Path.GetFileNameWithoutExtension(file).Replace(".belief", ""),
                        SourceFile = Path.GetFileName(file),
                    };
                    beliefs.Add(belief);
                }
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to parse belief file: {File}", file);
            }
        }

        return beliefs;
    }

    /// <summary>
    ///     Update a belief state and persist to disk.
    ///     Returns the updated belief, or null if the belief was not found.
    /// </summary>
    public BeliefState? UpdateBelief(string id, string newStatus, string evidence)
    {
        var beliefsDir = GetBeliefsDirectory();
        if (beliefsDir == null || !Directory.Exists(beliefsDir))
        {
            logger.LogWarning("Beliefs directory not found, cannot update belief {Id}", id);
            return null;
        }

        // Find the matching file
        var matchingFile = Directory.EnumerateFiles(beliefsDir, "*.belief.json")
            .FirstOrDefault(f =>
            {
                var fileId = Path.GetFileNameWithoutExtension(f).Replace(".belief", "");
                return string.Equals(fileId, id, StringComparison.OrdinalIgnoreCase);
            });

        if (matchingFile == null)
        {
            logger.LogWarning("Belief file not found for ID: {Id}", id);
            return null;
        }

        try
        {
            var json = File.ReadAllText(matchingFile);
            var belief = JsonSerializer.Deserialize<BeliefState>(json, JsonOptions);
            if (belief == null) return null;

            // Update fields
            var updatedBelief = belief with
            {
                Id = id,
                TruthValue = newStatus,
                LastUpdated = DateTime.UtcNow.ToString("O"),
                SourceFile = Path.GetFileName(matchingFile),
                Evidence = belief.Evidence != null
                    ? belief.Evidence with
                    {
                        Supporting =
                        [
                            ..belief.Evidence.Supporting,
                            new EvidenceItem
                            {
                                Source = "prime-radiant-update",
                                Claim = evidence,
                                Timestamp = DateTime.UtcNow.ToString("O"),
                                Reliability = 0.8,
                            },
                        ],
                    }
                    : new BeliefEvidence
                    {
                        Supporting =
                        [
                            new EvidenceItem
                            {
                                Source = "prime-radiant-update",
                                Claim = evidence,
                                Timestamp = DateTime.UtcNow.ToString("O"),
                                Reliability = 0.8,
                            },
                        ],
                        Contradicting = [],
                    },
            };

            // Persist
            var updatedJson = JsonSerializer.Serialize(updatedBelief, JsonOptions);
            File.WriteAllText(matchingFile, updatedJson);

            logger.LogInformation("Belief {Id} updated to {Status}", id, newStatus);

            // Evaluate algedonic signal — never block belief updates
            _ = EvaluateAndBroadcastSignalAsync(belief, updatedBelief);

            return updatedBelief;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to update belief {Id}", id);
            return null;
        }
    }

    /// <summary>
    ///     Evaluate a belief state transition for algedonic signals and broadcast if one is detected.
    ///     Runs asynchronously and never throws — signal evaluation must not block belief updates.
    /// </summary>
    private async Task EvaluateAndBroadcastSignalAsync(BeliefState oldState, BeliefState newState)
    {
        try
        {
            var signal = algedonicSignalService.EvaluateTransition(oldState, newState);
            if (signal is null) return;

            await algedonicSignalService.PersistSignalAsync(signal);
            await GovernanceHub.BroadcastAlgedonicSignal(hubContext, signal);

            logger.LogInformation("Algedonic signal {Signal} broadcast for belief {Id}",
                signal.Signal, newState.Id);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to evaluate/broadcast algedonic signal for belief {Id}", newState.Id);
        }
    }

    private string? GetBeliefsDirectory()
    {
        var demerzelRoot = configuration["Governance:DemerzelRoot"]
            ?? FindDemerzelRoot();

        return demerzelRoot != null
            ? Path.Combine(demerzelRoot, "state", "beliefs")
            : null;
    }

    private static string? FindDemerzelRoot()
    {
        var candidates = new[]
        {
            Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "governance", "demerzel"),
            Path.Combine(Directory.GetCurrentDirectory(), "governance", "demerzel"),
            @"C:\Users\spare\source\repos\ga\governance\demerzel",
        };
        return candidates.FirstOrDefault(Directory.Exists);
    }
}

// ─── Belief DTOs ───

public record BeliefState
{
    [JsonPropertyName("id")]
    public string? Id { get; init; }

    [JsonPropertyName("proposition")]
    public string Proposition { get; init; } = "";

    [JsonPropertyName("truth_value")]
    public string TruthValue { get; init; } = "U";

    [JsonPropertyName("confidence")]
    public double Confidence { get; init; }

    [JsonPropertyName("evidence")]
    public BeliefEvidence? Evidence { get; init; }

    [JsonPropertyName("last_updated")]
    public string? LastUpdated { get; init; }

    [JsonPropertyName("evaluated_by")]
    public string? EvaluatedBy { get; init; }

    [JsonPropertyName("source_file")]
    public string? SourceFile { get; init; }
}

public record BeliefEvidence
{
    [JsonPropertyName("supporting")]
    public List<EvidenceItem> Supporting { get; init; } = [];

    [JsonPropertyName("contradicting")]
    public List<EvidenceItem> Contradicting { get; init; } = [];
}

public record EvidenceItem
{
    [JsonPropertyName("source")]
    public string Source { get; init; } = "";

    [JsonPropertyName("claim")]
    public string Claim { get; init; } = "";

    [JsonPropertyName("timestamp")]
    public string? Timestamp { get; init; }

    [JsonPropertyName("reliability")]
    public double Reliability { get; init; }
}
