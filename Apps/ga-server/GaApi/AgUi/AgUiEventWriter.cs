namespace GaApi.AgUi;

using System.Diagnostics;
using System.Text.Json;
using GA.Business.Core.Orchestration.AgUi;

/// <summary>
/// Writes AG-UI SSE frames to an HttpResponse.
/// Each event is a <c>data: {json}\n\n</c> frame using camelCase JSON.
/// </summary>
public sealed class AgUiEventWriter(HttpResponse response)
{
    private static readonly JsonSerializerOptions _options = new(JsonSerializerDefaults.Web);

    private static long Now() => DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

    public async Task WriteEventAsync<T>(T payload, CancellationToken ct)
    {
        var json = JsonSerializer.Serialize(payload, _options);
        // Escape embedded newlines to keep the SSE frame on one data: line
        json = json.Replace("\n", "\\n", StringComparison.Ordinal);
        await response.WriteAsync($"data: {json}\n\n", ct);
        await response.Body.FlushAsync(ct);
    }

    public Task WriteRunStartedAsync(string threadId, string runId, CancellationToken ct) =>
        WriteEventAsync(new AgUiRunStartedEvent(AgUiEventTypes.RunStarted, threadId, runId, Now()), ct);

    public Task WriteRunFinishedAsync(string threadId, string runId, CancellationToken ct) =>
        WriteEventAsync(new AgUiRunFinishedEvent(AgUiEventTypes.RunFinished, threadId, runId, Now()), ct);

    public Task WriteRunErrorAsync(string message, string code, CancellationToken ct) =>
        WriteEventAsync(new AgUiRunErrorEvent(AgUiEventTypes.RunError, message, code), ct);

    public Task WriteStepStartedAsync(string stepName, string runId, CancellationToken ct) =>
        WriteEventAsync(new AgUiStepStartedEvent(AgUiEventTypes.StepStarted, stepName, runId), ct);

    public Task WriteTextStartAsync(string messageId, CancellationToken ct) =>
        WriteEventAsync(new AgUiTextMessageStartEvent(AgUiEventTypes.TextMessageStart, messageId, "assistant"), ct);

    public Task WriteTextChunkAsync(string messageId, string delta, CancellationToken ct) =>
        WriteEventAsync(new AgUiTextMessageContentEvent(AgUiEventTypes.TextMessageContent, messageId, delta), ct);

    public Task WriteTextEndAsync(string messageId, CancellationToken ct) =>
        WriteEventAsync(new AgUiTextMessageEndEvent(AgUiEventTypes.TextMessageEnd, messageId), ct);

    public Task WriteStateSnapshotAsync(object snapshot, CancellationToken ct) =>
        WriteEventAsync(new AgUiStateSnapshotEvent(AgUiEventTypes.StateSnapshot, snapshot), ct);

    public Task WriteStateDeltaAsync(IReadOnlyList<JsonPatchOperation> patch, CancellationToken ct) =>
        WriteEventAsync(new AgUiStateDeltaEvent(AgUiEventTypes.StateDelta, patch), ct);

    public Task WriteCustomAsync(string name, object value, CancellationToken ct) =>
        WriteEventAsync(new AgUiCustomEvent(AgUiEventTypes.Custom, name, value, Now()), ct);

    public static string NewRunId() =>
        Activity.Current?.TraceId.ToString() is { Length: > 0 } traceId
            ? traceId
            : Guid.NewGuid().ToString("N");
}
