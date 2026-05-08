namespace GA.Business.Core.Orchestration.Trace;

using System.Diagnostics;

/// <summary>
/// Default <see cref="IAgenticTraceCapture"/>. Mirrors the
/// <c>AgenticTraceBuilder</c> that originally lived inside
/// <c>Apps/GaChatbot.Api/Services/AgenticTraceBuilder.cs</c>, lifted to
/// public/scoped for shared decorator use across hosts.
/// </summary>
internal sealed class AgenticTraceCapture : IAgenticTraceCapture
{
    private const string Protocol = "w3c-trace-context+otel-genai+ag-ui";

    private readonly List<AgenticTraceStep> _steps = [];
    private readonly string _traceId;

    public AgenticTraceCapture()
    {
        RunId = $"run_{Guid.NewGuid():N}";
        _traceId = Activity.Current?.TraceId.ToString() ?? RunId;
    }

    public string RunId { get; }

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

    public ITimedStep StartStep(
        string name,
        IReadOnlyDictionary<string, object?>? attributes = null) =>
        new TimedStep(this, name, attributes ?? new Dictionary<string, object?>());

    public AgenticTrace Build() => new(_traceId, Protocol, RunId, [.. _steps]);

    private sealed class TimedStep(
        AgenticTraceCapture capture,
        string name,
        IReadOnlyDictionary<string, object?> attributes) : ITimedStep
    {
        private readonly Stopwatch _stopwatch = Stopwatch.StartNew();
        private bool _completed;

        public void Complete(
            string status = "completed",
            IReadOnlyDictionary<string, object?>? finalAttributes = null)
        {
            if (_completed) return;
            _completed = true;
            _stopwatch.Stop();

            if (finalAttributes is null || finalAttributes.Count == 0)
            {
                capture.AddStep(name, status, _stopwatch.ElapsedMilliseconds, attributes);
                return;
            }

            var merged = new Dictionary<string, object?>(attributes);
            foreach (var pair in finalAttributes)
                merged[pair.Key] = pair.Value;
            capture.AddStep(name, status, _stopwatch.ElapsedMilliseconds, merged);
        }

        public void Dispose() => Complete();
    }
}
