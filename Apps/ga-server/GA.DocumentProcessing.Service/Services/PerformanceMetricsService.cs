namespace GA.DocumentProcessing.Service.Services;

using System.Diagnostics;

public class PerformanceMetricsService
{
    private readonly Dictionary<string, List<long>> _metrics = new();

    public void RecordMetric(string metricName, long value)
    {
        if (!_metrics.ContainsKey(metricName))
        {
            _metrics[metricName] = new List<long>();
        }
        _metrics[metricName].Add(value);
    }

    public Dictionary<string, object> GetMetrics()
    {
        var result = new Dictionary<string, object>();
        foreach (var (key, values) in _metrics)
        {
            if (values.Count > 0)
            {
                result[key] = new
                {
                    Count = values.Count,
                    Average = values.Average(),
                    Min = values.Min(),
                    Max = values.Max()
                };
            }
        }
        return result;
    }

    public IDisposable MeasureOperation(string operationName)
    {
        return new OperationTimer(this, operationName);
    }

    private class OperationTimer : IDisposable
    {
        private readonly PerformanceMetricsService _service;
        private readonly string _operationName;
        private readonly Stopwatch _stopwatch;

        public OperationTimer(PerformanceMetricsService service, string operationName)
        {
            _service = service;
            _operationName = operationName;
            _stopwatch = Stopwatch.StartNew();
        }

        public void Dispose()
        {
            _stopwatch.Stop();
            _service.RecordMetric(_operationName, _stopwatch.ElapsedMilliseconds);
        }
    }
}

