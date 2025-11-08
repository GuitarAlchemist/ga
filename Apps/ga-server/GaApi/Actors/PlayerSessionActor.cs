namespace GaApi.Actors;

using Messages;
using Proto;

/// <summary>
///     Actor that manages an individual player's adaptive difficulty session
///     Each player gets their own isolated actor instance with private state
/// </summary>
public class PlayerSessionActor : IActor
{
    private readonly AdaptiveDifficultySystem _difficultySystem;
    private readonly ILogger<PlayerSessionActor> _logger;
    private readonly string _playerId;
    private readonly DateTime _sessionStarted;
    private int _totalExercises;

    public PlayerSessionActor(string playerId, ILoggerFactory loggerFactory)
    {
        _playerId = playerId;
        _logger = loggerFactory.CreateLogger<PlayerSessionActor>();
        _difficultySystem = new AdaptiveDifficultySystem(loggerFactory);
        _sessionStarted = DateTime.UtcNow;
        _totalExercises = 0;

        _logger.LogInformation("Player session actor created for {PlayerId}", playerId);
    }

    public Task ReceiveAsync(IContext context)
    {
        return context.Message switch
        {
            UpdatePerformance msg => HandleUpdatePerformance(context, msg),
            GetDifficulty msg => HandleGetDifficulty(context, msg),
            GetRecommendedExercise msg => HandleGetRecommendedExercise(context, msg),
            GetSessionState msg => HandleGetSessionState(context, msg),
            ResetSession msg => HandleResetSession(context, msg),
            Started => HandleStarted(context),
            Stopping => HandleStopping(context),
            Stopped => HandleStopped(context),
            _ => Task.CompletedTask
        };
    }

    private Task HandleUpdatePerformance(IContext context, UpdatePerformance msg)
    {
        _logger.LogDebug(
            "Player {PlayerId} performance update: Accuracy={Accuracy:F2}, Speed={Speed:F2}, Consistency={Consistency:F2}",
            _playerId, msg.Accuracy, msg.Speed, msg.Consistency);

        // Convert to PlayerPerformance and record
        var performance = new PlayerPerformance
        {
            Success = msg.Accuracy > 0.5,
            TimeMs = 1000.0 / Math.Max(msg.Speed, 0.01), // Convert speed back to time
            Attempts = (int)(1.0 / Math.Max(msg.Consistency, 0.01)), // Convert consistency back to attempts
            ShapeId = msg.Context ?? "unknown",
            Timestamp = DateTime.UtcNow
        };

        _difficultySystem.RecordPerformance(performance);
        _totalExercises++;

        var stats = _difficultySystem.GetPlayerStats();
        var response = new DifficultyResponse(
            stats.CurrentDifficulty,
            stats.CurrentDifficulty, // Target difficulty (would be tracked separately in real implementation)
            $"Updated based on performance: A={msg.Accuracy:F2}, S={msg.Speed:F2}, C={msg.Consistency:F2}"
        );

        context.Respond(response);
        return Task.CompletedTask;
    }

    private Task HandleGetDifficulty(IContext context, GetDifficulty msg)
    {
        var stats = _difficultySystem.GetPlayerStats();
        var response = new DifficultyResponse(
            stats.CurrentDifficulty,
            stats.CurrentDifficulty, // Target difficulty
            "Current difficulty level"
        );

        context.Respond(response);
        return Task.CompletedTask;
    }

    private Task HandleGetRecommendedExercise(IContext context, GetRecommendedExercise msg)
    {
        // Generate exercise recommendation based on current difficulty
        var stats = _difficultySystem.GetPlayerStats();
        var difficulty = stats.CurrentDifficulty;

        var exerciseId = difficulty switch
        {
            < 0.3 => "beginner-chord-transitions",
            < 0.5 => "intermediate-progressions",
            < 0.7 => "advanced-voicings",
            _ => "expert-jazz-reharmonization"
        };

        var parameters = new Dictionary<string, object>
        {
            ["difficulty"] = difficulty,
            ["targetDifficulty"] = difficulty,
            ["totalExercises"] = _totalExercises,
            ["sessionDuration"] = (DateTime.UtcNow - _sessionStarted).TotalMinutes
        };

        var response = new ExerciseRecommendation(
            exerciseId,
            difficulty,
            $"Recommended based on current difficulty {difficulty:F2}",
            parameters
        );

        context.Respond(response);
        return Task.CompletedTask;
    }

    private Task HandleGetSessionState(IContext context, GetSessionState msg)
    {
        var stats = _difficultySystem.GetPlayerStats();

        var metrics = new Dictionary<string, object>
        {
            ["currentDifficulty"] = stats.CurrentDifficulty,
            ["targetDifficulty"] = stats.CurrentDifficulty,
            ["totalExercises"] = _totalExercises,
            ["averageAccuracy"] = 0.0, // Would track this in real implementation
            ["averageSpeed"] = 0.0,
            ["averageConsistency"] = 0.0,
            ["successRate"] = stats.SuccessRate,
            ["averageTime"] = stats.AverageTime,
            ["learningRate"] = stats.LearningRate
        };

        var response = new SessionStateResponse(
            _playerId,
            stats.CurrentDifficulty,
            stats.CurrentDifficulty,
            _totalExercises,
            _sessionStarted,
            DateTime.UtcNow - _sessionStarted,
            metrics
        );

        context.Respond(response);
        return Task.CompletedTask;
    }

    private Task HandleResetSession(IContext context, ResetSession msg)
    {
        _logger.LogInformation("Resetting session for player {PlayerId}", _playerId);

        // Reset would recreate the difficulty system
        // For now, just acknowledge
        _totalExercises = 0;

        context.Respond(new Ack("Session reset"));
        return Task.CompletedTask;
    }

    private Task HandleStarted(IContext context)
    {
        _logger.LogInformation("Player session actor started for {PlayerId}", _playerId);
        return Task.CompletedTask;
    }

    private Task HandleStopping(IContext context)
    {
        _logger.LogInformation("Player session actor stopping for {PlayerId}", _playerId);
        return Task.CompletedTask;
    }

    private Task HandleStopped(IContext context)
    {
        _logger.LogInformation("Player session actor stopped for {PlayerId}", _playerId);
        return Task.CompletedTask;
    }
}
