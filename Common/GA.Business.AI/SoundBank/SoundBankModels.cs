namespace GA.Business.AI.SoundBank;

/// <summary>
///     Job status enum
/// </summary>
public enum JobStatus
{
    Queued,
    Processing,
    Completed,
    Failed
}

/// <summary>
///     Request to generate a guitar sound sample
/// </summary>
public record SoundGenerationRequest(
    string Instrument,
    int String,
    int Fret,
    double Velocity,
    List<string> Technique,
    string? StylePrompt = null,
    double DurationSeconds = 2.0
);

/// <summary>
///     Response when job is created
/// </summary>
public record JobResponse(
    string JobId,
    JobStatus Status,
    double EstimatedSeconds,
    string CreatedAt
);

/// <summary>
///     Metadata for a generated sound sample
/// </summary>
public record SoundSample(
    string SampleId,
    string Instrument,
    int String,
    int Fret,
    double Velocity,
    List<string> Technique,
    string? StylePrompt,
    double DurationSeconds,
    string FilePath,
    long FileSizeBytes,
    int SampleRate,
    string CreatedAt
);

/// <summary>
///     Status of a generation job
/// </summary>
public record JobStatusResponse(
    string JobId,
    JobStatus Status,
    double Progress,
    string StatusMessage,
    SoundSample? Sample,
    string? ErrorMessage,
    string CreatedAt,
    string UpdatedAt
);

/// <summary>
///     Search for existing sound samples
/// </summary>
public record SearchRequest(
    string? Instrument = null,
    int? String = null,
    int? Fret = null,
    List<string>? Technique = null,
    int Limit = 10
);

/// <summary>
///     Search results
/// </summary>
public record SearchResponse(
    List<SoundSample> Samples,
    int Total
);
