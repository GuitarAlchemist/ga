namespace GA.Business.Core.Orchestration.AgUi;

using System.Text.Json.Serialization;

/// <summary>
/// Minimal JSON Patch operation record (RFC 6902).
/// Backend emits as anonymous object; frontend SDK applies via built-in fast-json-patch.
/// </summary>
public sealed record JsonPatchOperation(
    [property: JsonPropertyName("op")]    string Op,
    [property: JsonPropertyName("path")]  string Path,
    [property: JsonPropertyName("value")] object? Value = null);

/// <summary>
/// AG-UI event records. All properties serialize to camelCase when the
/// JsonSerializerDefaults.Web serializer is used (configured in AgUiEventWriter).
/// </summary>
public sealed record AgUiRunStartedEvent(
    string Type,
    string ThreadId,
    string RunId,
    long   Timestamp);

public sealed record AgUiRunFinishedEvent(
    string Type,
    string ThreadId,
    string RunId,
    long   Timestamp);

public sealed record AgUiRunErrorEvent(
    string Type,
    string Message,
    string Code);

public sealed record AgUiStepStartedEvent(
    string Type,
    string StepName,
    string RunId);

public sealed record AgUiTextMessageStartEvent(
    string Type,
    string MessageId,
    string Role);

public sealed record AgUiTextMessageContentEvent(
    string Type,
    string MessageId,
    string Delta);

public sealed record AgUiTextMessageEndEvent(
    string Type,
    string MessageId);

public sealed record AgUiStateSnapshotEvent(
    string  Type,
    object  Snapshot);

public sealed record AgUiStateDeltaEvent(
    string                            Type,
    IReadOnlyList<JsonPatchOperation> Delta);

public sealed record AgUiCustomEvent(
    string Type,
    string Name,
    object Value,
    long   Timestamp);
