namespace GaApi.Actors.Messages;

/// <summary>
///     Base message for player session actor
/// </summary>
public abstract record PlayerSessionMessage;

/// <summary>
///     Update player performance metrics
/// </summary>
public sealed record UpdatePerformance(
    double Accuracy,
    double Speed,
    double Consistency,
    string? Context = null
) : PlayerSessionMessage;

/// <summary>
///     Get current difficulty level
/// </summary>
public sealed record GetDifficulty : PlayerSessionMessage;

/// <summary>
///     Response containing current difficulty level
/// </summary>
public sealed record DifficultyResponse(
    double CurrentDifficulty,
    double TargetDifficulty,
    string AdaptationReason
);

/// <summary>
///     Get recommended next exercise
/// </summary>
public sealed record GetRecommendedExercise : PlayerSessionMessage;

/// <summary>
///     Response containing recommended exercise
/// </summary>
public sealed record ExerciseRecommendation(
    string ExerciseId,
    double Difficulty,
    string Rationale,
    Dictionary<string, object> Parameters
);

/// <summary>
///     Get player session state
/// </summary>
public sealed record GetSessionState : PlayerSessionMessage;

/// <summary>
///     Response containing full session state
/// </summary>
public sealed record SessionStateResponse(
    string PlayerId,
    double CurrentDifficulty,
    double TargetDifficulty,
    int TotalExercises,
    DateTime SessionStarted,
    TimeSpan SessionDuration,
    Dictionary<string, object> Metrics
);

/// <summary>
///     Reset player session
/// </summary>
public sealed record ResetSession : PlayerSessionMessage;

/// <summary>
///     Acknowledgment message
/// </summary>
public sealed record Ack(string Message = "OK");
