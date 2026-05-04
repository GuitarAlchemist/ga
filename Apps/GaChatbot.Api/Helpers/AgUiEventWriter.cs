namespace GaChatbot.Api.Helpers;

using System.Diagnostics;
using System.Text.Json;
using GA.Business.Core.Orchestration.AgUi;

internal sealed class AgUiEventWriter(HttpResponse response)
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);

    public async Task WriteEventAsync<T>(T payload, CancellationToken cancellationToken)
    {
        var json = JsonSerializer.Serialize(payload, Options);
        json = json.Replace("\n", "\\n", StringComparison.Ordinal);
        await response.WriteAsync($"data: {json}\n\n", cancellationToken);
        await response.Body.FlushAsync(cancellationToken);
    }

    public Task WriteRunStartedAsync(string threadId, string runId, CancellationToken cancellationToken) =>
        WriteEventAsync(new AgUiRunStartedEvent(AgUiEventTypes.RunStarted, threadId, runId, Now()), cancellationToken);

    public Task WriteRunFinishedAsync(string threadId, string runId, CancellationToken cancellationToken) =>
        WriteEventAsync(new AgUiRunFinishedEvent(AgUiEventTypes.RunFinished, threadId, runId, Now()), cancellationToken);

    public Task WriteRunErrorAsync(string message, string code, CancellationToken cancellationToken) =>
        WriteEventAsync(new AgUiRunErrorEvent(AgUiEventTypes.RunError, message, code), cancellationToken);

    public Task WriteStepStartedAsync(string stepName, string runId, CancellationToken cancellationToken) =>
        WriteEventAsync(new AgUiStepStartedEvent(AgUiEventTypes.StepStarted, stepName, runId), cancellationToken);

    public Task WriteTextStartAsync(string messageId, CancellationToken cancellationToken) =>
        WriteEventAsync(new AgUiTextMessageStartEvent(AgUiEventTypes.TextMessageStart, messageId, "assistant"), cancellationToken);

    public Task WriteTextChunkAsync(string messageId, string delta, CancellationToken cancellationToken) =>
        WriteEventAsync(new AgUiTextMessageContentEvent(AgUiEventTypes.TextMessageContent, messageId, delta), cancellationToken);

    public Task WriteTextEndAsync(string messageId, CancellationToken cancellationToken) =>
        WriteEventAsync(new AgUiTextMessageEndEvent(AgUiEventTypes.TextMessageEnd, messageId), cancellationToken);

    public static string NewRunId() =>
        Activity.Current?.TraceId.ToString() is { Length: > 0 } traceId
            ? traceId
            : Guid.NewGuid().ToString("N");

    private static long Now() => DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
}
