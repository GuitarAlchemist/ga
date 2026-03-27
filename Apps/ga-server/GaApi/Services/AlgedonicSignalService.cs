namespace GaApi.Services;

using System.Text.Json;
using System.Text.Json.Serialization;
using Models;
using Path = System.IO.Path;

/// <summary>
///     Evaluates belief state transitions and emits algedonic signals (pain/pleasure).
///     Persists signals to the governance directory and provides recent signal retrieval.
/// </summary>
public sealed class AlgedonicSignalService(
    ILogger<AlgedonicSignalService> logger,
    IConfiguration configuration)
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private const int MaxSignalFiles = 100;
    private static readonly Lock PersistLock = new();

    /// <summary>
    ///     Evaluate a belief state transition and return an algedonic signal if meaningful.
    ///     Returns null when no significant change is detected or on error.
    /// </summary>
    public AlgedonicSignalDto? EvaluateTransition(BeliefState? oldState, BeliefState newState)
    {
        try
        {
            // New belief with high confidence — knowledge harvest
            if (oldState is null)
            {
                if (string.Equals(newState.TruthValue, "T", StringComparison.OrdinalIgnoreCase)
                    && newState.Confidence > 0.8)
                {
                    return CreateSignal(
                        "knowledge_harvest",
                        "pleasure",
                        "info",
                        $"New high-confidence belief established: {newState.Proposition}",
                        newState.Id);
                }

                return null;
            }

            // Truth value transitions
            var oldTruth = oldState.TruthValue.ToUpperInvariant();
            var newTruth = newState.TruthValue.ToUpperInvariant();

            // T -> C or T -> F: policy violation (pain/emergency)
            if (oldTruth == "T" && (newTruth is "C" or "F"))
            {
                return CreateSignal(
                    "policy_violation",
                    "pain",
                    "emergency",
                    $"Belief '{newState.Proposition}' transitioned from True to {newTruth} — possible policy violation",
                    newState.Id);
            }

            // U -> T: domain convergence (pleasure/info)
            if (oldTruth == "U" && newTruth == "T")
            {
                return CreateSignal(
                    "domain_convergence",
                    "pleasure",
                    "info",
                    $"Belief '{newState.Proposition}' converged from Unknown to True",
                    newState.Id);
            }

            // Confidence drop > 0.2: belief collapse (pain/warning)
            var confidenceDelta = newState.Confidence - oldState.Confidence;
            if (confidenceDelta < -0.2)
            {
                return CreateSignal(
                    "belief_collapse",
                    "pain",
                    "warning",
                    $"Confidence in '{newState.Proposition}' dropped from {oldState.Confidence:F2} to {newState.Confidence:F2}",
                    newState.Id);
            }

            // Confidence rise > 0.2: resilience recovery (pleasure/info)
            if (confidenceDelta > 0.2)
            {
                return CreateSignal(
                    "resilience_recovery",
                    "pleasure",
                    "info",
                    $"Confidence in '{newState.Proposition}' rose from {oldState.Confidence:F2} to {newState.Confidence:F2}",
                    newState.Id);
            }

            return null;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error evaluating belief state transition");
            return null;
        }
    }

    /// <summary>
    ///     Persist an algedonic signal as a JSON file in the signals directory.
    ///     Prunes oldest files when the directory exceeds <see cref="MaxSignalFiles" />.
    /// </summary>
    public async Task PersistSignalAsync(AlgedonicSignalDto signal)
    {
        try
        {
            var signalsDir = GetSignalsDirectory();
            if (signalsDir is null)
            {
                logger.LogWarning("Signals directory could not be resolved; signal not persisted");
                return;
            }

            Directory.CreateDirectory(signalsDir);

            var date = signal.Timestamp.ToString("yyyy-MM-dd");
            var seq = GetNextSequenceNumber(signalsDir, signal.Signal, date);
            var fileName = $"sig-{signal.Signal}-{date}-{seq:D3}.signal.json";
            var filePath = Path.Combine(signalsDir, fileName);

            var json = JsonSerializer.Serialize(signal, JsonOptions);
            await File.WriteAllTextAsync(filePath, json);

            logger.LogInformation("Persisted algedonic signal {Signal} to {File}", signal.Signal, fileName);

            PruneOldSignals(signalsDir);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to persist algedonic signal {Signal}", signal.Signal);
        }
    }

    /// <summary>
    ///     Read the most recent signal files, sorted by timestamp descending.
    /// </summary>
    public async Task<List<AlgedonicSignalDto>> GetRecentSignalsAsync(int count = 50)
    {
        try
        {
            var signalsDir = GetSignalsDirectory();
            if (signalsDir is null || !Directory.Exists(signalsDir))
            {
                return [];
            }

            var signals = new List<AlgedonicSignalDto>();
            var files = Directory.EnumerateFiles(signalsDir, "*.signal.json")
                .OrderByDescending(File.GetLastWriteTimeUtc)
                .Take(count);

            foreach (var file in files)
            {
                try
                {
                    var json = await File.ReadAllTextAsync(file);
                    var signal = JsonSerializer.Deserialize<AlgedonicSignalDto>(json, JsonOptions);
                    if (signal is not null)
                    {
                        signals.Add(signal);
                    }
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Failed to parse signal file: {File}", file);
                }
            }

            return [.. signals.OrderByDescending(s => s.Timestamp)];
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to read recent algedonic signals");
            return [];
        }
    }

    private static AlgedonicSignalDto CreateSignal(
        string channel,
        string type,
        string severity,
        string description,
        string? nodeId) =>
        new()
        {
            Id = $"sig-{channel}-{DateTimeOffset.UtcNow:yyyyMMddHHmmss}-{Guid.NewGuid().ToString()[..8]}",
            Timestamp = DateTimeOffset.UtcNow,
            Signal = channel,
            Type = type,
            Source = "belief-state-service",
            Severity = severity,
            Status = "active",
            Description = description,
            NodeId = nodeId,
        };

    private static int GetNextSequenceNumber(string signalsDir, string channel, string date)
    {
        lock (PersistLock)
        {
            var prefix = $"sig-{channel}-{date}-";
            var existing = Directory.EnumerateFiles(signalsDir, $"{prefix}*.signal.json")
                .Select(f =>
                {
                    var name = Path.GetFileNameWithoutExtension(f).Replace(".signal", "");
                    var seqPart = name[prefix.Length..];
                    return int.TryParse(seqPart, out var n) ? n : 0;
                })
                .DefaultIfEmpty(0)
                .Max();

            return existing + 1;
        }
    }

    private void PruneOldSignals(string signalsDir)
    {
        try
        {
            var files = Directory.EnumerateFiles(signalsDir, "*.signal.json")
                .OrderByDescending(File.GetLastWriteTimeUtc)
                .Skip(MaxSignalFiles)
                .ToList();

            foreach (var file in files)
            {
                File.Delete(file);
                logger.LogDebug("Pruned old signal file: {File}", Path.GetFileName(file));
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Error pruning signal files");
        }
    }

    private string? GetSignalsDirectory()
    {
        var demerzelRoot = configuration["Governance:DemerzelRoot"]
            ?? FindDemerzelRoot();

        return demerzelRoot is not null
            ? Path.Combine(demerzelRoot, "state", "signals")
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
