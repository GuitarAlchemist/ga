namespace GaApi.Components.Pages;

using GaApi.Models.AutonomousCuration;
using MudBlazor;

public partial class RetroactionLoop
{
    private bool _loading;
    private bool _isRunning;
    private List<KnowledgeGap> _knowledgeGaps = [];
    private List<IterationRecord> _iterations = [];
    private List<CurationDecision> _recentDecisions = [];
    private List<InterventionPoint> _interventionPoints = [];
    private double _averageQuality = 3.5;
    private double _knowledgeCoverage = 65.0;
    private double _insightQuality = 72.0;
    private DateTime _lastUpdated = DateTime.UtcNow;
    private int _documentsProcessed;
    private int _acceptedDecisions;
    private int _rejectedDecisions;
    private string _convergenceChartHtml = string.Empty;

    protected override async Task OnInitializedAsync()
    {
        await RefreshData();
    }

    private async Task RefreshData()
    {
        _loading = true;
        try
        {
            // Load knowledge gaps
            var gapAnalysis = await GapAnalyzer.AnalyzeGapsAsync();
            _knowledgeGaps = gapAnalysis.Gaps;

            // Load mock data for demonstration
            LoadMockData();
            ComputeDerivedMetrics();
            _lastUpdated = DateTime.UtcNow;
        }
        catch (Exception ex)
        {
            Snackbar.Add($"Error loading data: {ex.Message}", Severity.Error);
        }
        finally
        {
            _loading = false;
        }
    }

    private async Task StartCuration()
    {
        _isRunning = true;
        Snackbar.Add("Autonomous curation started", Severity.Success);

        // TODO: Call AutonomousCurationOrchestrator to start the workflow
        // For now, just simulate the start
        await Task.Delay(100);
    }

    private async Task StopCuration()
    {
        _isRunning = false;
        Snackbar.Add("Autonomous curation stopped", Severity.Info);
        await Task.Delay(100);
    }

    private async Task HandleIntervention(InterventionPoint point)
    {
        Snackbar.Add($"Reviewing intervention: {point.Description}", Severity.Info);
        // TODO: Implement intervention handling
        await Task.Delay(100);
    }

    private static Color GetPriorityColor(string priority)
    {
        return priority switch
        {
            "High" => Color.Error,
            "Medium" => Color.Warning,
            "Low" => Color.Info,
            _ => Color.Default
        };
    }

    private void ComputeDerivedMetrics()
    {
        _documentsProcessed = _iterations.Sum(i => i.DocumentsProcessed);
        _acceptedDecisions = _recentDecisions.Count(d =>
            d.Decision.Contains("accepted", StringComparison.OrdinalIgnoreCase));
        _rejectedDecisions = _recentDecisions.Count(d =>
            d.Decision.Contains("reject", StringComparison.OrdinalIgnoreCase));
        BuildConvergenceChart();
    }

    private int CriticalGapCount =>
        _knowledgeGaps.Count(g => g.Priority.Equals("Critical", StringComparison.OrdinalIgnoreCase));

    private int HighPriorityGapCount =>
        _knowledgeGaps.Count(g => g.Priority.Equals("High", StringComparison.OrdinalIgnoreCase));

    private int PendingInterventions => _interventionPoints.Count;

    private int TotalDocumentsProcessed => _documentsProcessed;

    private int AcceptedDecisionCount => _acceptedDecisions;

    private int RejectedDecisionCount => _rejectedDecisions;

    private string LastUpdatedDisplay => _lastUpdated.ToLocalTime().ToString("g");

    private void BuildConvergenceChart()
    {
        if (_iterations == null || _iterations.Count < 2)
        {
            _convergenceChartHtml = string.Empty;
            return;
        }

        var ordered = _iterations.OrderBy(i => i.Timestamp).ToList();
        var indices = Enumerable.Range(0, ordered.Count).Select(i => (double)i).ToArray();
        var labels = ordered.Select(i => i.Timestamp.ToLocalTime().ToString("MM/dd HH:mm")).ToArray();
        var qualityValues = ordered.Select(i => i.AverageQuality).ToArray();
        var coverageValues = ordered.Select((_, idx) =>
        {
            var fraction = (double)(idx + 1) / ordered.Count;
            return Math.Min(100, Math.Max(0, _knowledgeCoverage * fraction));
        }).ToArray();

        // TODO: Update Plotly.NET chart generation to use new API
        // The Plotly.NET API has changed - need to update chart generation code
        _convergenceChartHtml = "<div>Chart visualization temporarily disabled - Plotly.NET API update needed</div>";

        // var qualityLine = Chart.Line<double, double, string>(indices, qualityValues, Text: labels, Name: "Avg Quality")
        //     .WithTraceInfo(Name: "Avg Quality", Showlegend: true);
        //
        // var coverageLine = Chart.Line<double, double, string>(indices, coverageValues, Text: labels, Name: "Coverage %")
        //     .WithTraceInfo(Name: "Coverage %", Showlegend: true);
        //
        // var combined = GenericChart.combine(new[] { qualityLine, coverageLine })
        //     .WithTitle("Knowledge Quality & Coverage")
        //     .WithXAxisStyle(title: "Iteration Index")
        //     .WithYAxisStyle(title: "Score / %");
        //
        // _convergenceChartHtml = GenericChart.toHTML(
        //     combined,
        //     id: $"chart-{Guid.NewGuid():N}",
        //     width: 900,
        //     height: 360,
        //     enableScrollZoom: false,
        //     showSources: false,
        //     includePlotlyJS: true,
        //     showMathjax: false);
    }

    private void LoadMockData()
    {
        // Mock iteration history
        _iterations =
        [
            new IterationRecord
            {
                IterationNumber = 5,
                Timestamp = DateTime.UtcNow.AddHours(-1),
                DocumentsProcessed = 12,
                AverageQuality = 4.2
            },
            new IterationRecord
            {
                IterationNumber = 4,
                Timestamp = DateTime.UtcNow.AddHours(-3),
                DocumentsProcessed = 8,
                AverageQuality = 3.9
            },
            new IterationRecord
            {
                IterationNumber = 3,
                Timestamp = DateTime.UtcNow.AddHours(-6),
                DocumentsProcessed = 15,
                AverageQuality = 4.1
            },
            new IterationRecord
            {
                IterationNumber = 2,
                Timestamp = DateTime.UtcNow.AddHours(-12),
                DocumentsProcessed = 10,
                AverageQuality = 3.7
            },
            new IterationRecord
            {
                IterationNumber = 1,
                Timestamp = DateTime.UtcNow.AddDays(-1),
                DocumentsProcessed = 5,
                AverageQuality = 3.5
            }
        ];

        // Mock recent decisions
        _recentDecisions =
        [
            new CurationDecision
            {
                Timestamp = DateTime.UtcNow.AddMinutes(-15),
                Decision = "Accepted YouTube video on jazz harmony",
                Source = "YouTube",
                SourceUrl = "https://youtube.com/watch?v=abc123",
                Details = "Strong explanation of quartal voicings with clear exercises.",
                Confidence = 0.92
            },
            new CurationDecision
            {
                Timestamp = DateTime.UtcNow.AddMinutes(-30),
                Decision = "Rejected low-quality tutorial",
                Source = "YouTube",
                SourceUrl = "https://youtube.com/watch?v=xyz789",
                Details = "Poor audio and missing references to target gap.",
                Confidence = 0.78
            },
            new CurationDecision
            {
                Timestamp = DateTime.UtcNow.AddHours(-1),
                Decision = "Accepted PDF on chord voicings",
                Source = "PDF",
                SourceUrl = "https://example.com/docs/voicings.pdf",
                Details = "Detailed voicing charts mapped to fretboard heatmaps.",
                Confidence = 0.95
            },
            new CurationDecision
            {
                Timestamp = DateTime.UtcNow.AddHours(-2),
                Decision = "Accepted web article on modal theory",
                Source = "Web",
                SourceUrl = "https://example.com/articles/modal-theory",
                Details = "Good historical context and Dorian vs. Aeolian comparison.",
                Confidence = 0.88
            }
        ];

        // Mock intervention points
        _interventionPoints =
        [
            new InterventionPoint
            {
                Description = "Conflicting information about Dorian mode",
                Reason = "Multiple sources provide different definitions",
                Severity = "Medium"
            },
            new InterventionPoint
            {
                Description = "Low confidence in chord substitution rules",
                Reason = "Insufficient training data",
                Severity = "Low"
            }
        ];
    }
}

// Supporting classes for the UI
public class IterationRecord
{
    public int IterationNumber { get; set; }
    public DateTime Timestamp { get; set; }
    public int DocumentsProcessed { get; set; }
    public double AverageQuality { get; set; }
}

public class CurationDecision
{
    public DateTime Timestamp { get; set; }
    public string Decision { get; set; } = string.Empty;
    public string Source { get; set; } = string.Empty;
    public string? SourceUrl { get; set; }
    public string? Details { get; set; }
    public double Confidence { get; set; }
}

public class InterventionPoint
{
    public string Description { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
    public string Severity { get; set; } = string.Empty;
}
