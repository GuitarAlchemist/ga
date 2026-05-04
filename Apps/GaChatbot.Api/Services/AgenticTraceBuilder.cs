namespace GaChatbot.Api.Services;

using System.Diagnostics;

internal sealed class AgenticTraceBuilder(string runId)
{
    private const string Protocol = "w3c-trace-context+otel-genai+ag-ui";
    private readonly List<AgenticTraceStep> _steps = [];
    private readonly string _traceId = Activity.Current?.TraceId.ToString() ?? runId;

    public string RunId => runId;

    public TimedStep StartStep(string name, IReadOnlyDictionary<string, object?>? attributes = null) =>
        new(this, name, attributes ?? new Dictionary<string, object?>());

    public void AddStep(
        string name,
        string status,
        long elapsedMs,
        IReadOnlyDictionary<string, object?>? attributes = null) =>
        _steps.Add(new AgenticTraceStep(
            name,
            status,
            elapsedMs,
            attributes ?? new Dictionary<string, object?>()));

    public AgenticTrace Build() =>
        new(_traceId, Protocol, runId, [.. _steps]);

    public sealed class TimedStep(
        AgenticTraceBuilder trace,
        string name,
        IReadOnlyDictionary<string, object?> attributes) : IDisposable
    {
        private readonly Stopwatch _stopwatch = Stopwatch.StartNew();
        private bool _completed;

        public void Complete(string status = "completed", IReadOnlyDictionary<string, object?>? finalAttributes = null)
        {
            if (_completed)
            {
                return;
            }

            _completed = true;
            _stopwatch.Stop();

            if (finalAttributes is null || finalAttributes.Count == 0)
            {
                trace.AddStep(name, status, _stopwatch.ElapsedMilliseconds, attributes);
                return;
            }

            var merged = new Dictionary<string, object?>(attributes);
            foreach (var pair in finalAttributes)
            {
                merged[pair.Key] = pair.Value;
            }

            trace.AddStep(name, status, _stopwatch.ElapsedMilliseconds, merged);
        }

        public void Dispose() =>
            Complete();
    }
}
