namespace GaApi.Controllers;

using System.Text.Json;
using GaApi.Hubs;
using Microsoft.AspNetCore.SignalR;
using Path = System.IO.Path;

/// <summary>
///     Governance graph endpoint — serves the Demerzel governance structure
///     as a JSON graph for the Prime Radiant 3D visualization.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class GovernanceController(
    IConfiguration configuration,
    ILogger<GovernanceController> logger,
    IHubContext<GovernanceHub> hubContext)
    : ControllerBase
{
    // Cache the governance graph (regenerated on demand or every 5 minutes)
    private static GovernanceGraph? _cachedGraph;
    private static DateTime _cacheTime = DateTime.MinValue;
    private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(5);

    /// <summary>
    ///     Get the full governance graph (nodes + edges + health metrics).
    ///     Used by Prime Radiant for real-time visualization.
    /// </summary>
    [HttpGet]
    [ResponseCache(Duration = 60)]
    public ActionResult<GovernanceGraph> GetGraph()
    {
        try
        {
            if (_cachedGraph != null && DateTime.UtcNow - _cacheTime < CacheDuration)
                return Ok(_cachedGraph);

            var demerzelRoot = configuration["Governance:DemerzelRoot"]
                ?? FindDemerzelRoot();

            if (demerzelRoot == null || !Directory.Exists(demerzelRoot))
            {
                logger.LogWarning("Demerzel governance root not found, serving empty graph");
                return Ok(GovernanceGraph.Empty);
            }

            var graph = BuildGovernanceGraph(demerzelRoot);
            _cachedGraph = graph;
            _cacheTime = DateTime.UtcNow;

            return Ok(graph);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to build governance graph");
            return StatusCode(500, new { error = "Failed to build governance graph" });
        }
    }

    /// <summary>
    ///     Force refresh the governance graph cache.
    /// </summary>
    [HttpPost("refresh")]
    public ActionResult Refresh()
    {
        _cachedGraph = null;
        _cacheTime = DateTime.MinValue;
        return Ok(new { message = "Cache cleared" });
    }

    /// <summary>
    ///     Get Markov prediction data for governance health forecasting.
    /// </summary>
    [HttpGet("predictions")]
    public ActionResult GetPredictions()
    {
        var demerzelRoot = configuration["Governance:DemerzelRoot"] ?? FindDemerzelRoot();
        if (demerzelRoot == null) return Ok(new { predictions = Array.Empty<object>() });

        var predictionsFile = Path.Combine(demerzelRoot, "state", "markov", "predictions.json");
        if (!System.IO.File.Exists(predictionsFile))
            return Ok(new { predictions = Array.Empty<object>() });

        var json = System.IO.File.ReadAllText(predictionsFile);
        return Content(json, "application/json");
    }

    /// <summary>
    ///     Request a screenshot from all connected Prime Radiant clients.
    /// </summary>
    [HttpPost("screenshot")]
    public async Task<ActionResult> RequestScreenshot([FromBody] ScreenshotRequest? request = null)
    {
        var reason = request?.Reason ?? "Manual screenshot request";
        await GovernanceHub.RequestScreenshotFromClients(hubContext, reason);
        return Ok(new { message = "Screenshot requested", reason });
    }

    /// <summary>
    ///     Get the most recently captured screenshot as a PNG image.
    /// </summary>
    [HttpGet("screenshot/latest")]
    public ActionResult GetLatestScreenshot()
    {
        var (base64, format, capturedAt) = GovernanceHub.GetLatestScreenshot();
        if (base64 == null || capturedAt == null)
            return NotFound(new { error = "No screenshot available yet" });

        // Strip data URL prefix if present (e.g., "data:image/png;base64,")
        var rawBase64 = base64.Contains(',') ? base64[(base64.IndexOf(',') + 1)..] : base64;
        var bytes = Convert.FromBase64String(rawBase64);
        var contentType = format switch
        {
            "image/jpeg" => "image/jpeg",
            "image/webp" => "image/webp",
            _ => "image/png",
        };

        Response.Headers.Append("X-Screenshot-Captured-At", capturedAt.Value.ToString("O"));
        return File(bytes, contentType);
    }

    // ─── Find Demerzel root relative to solution ───
    private static string? FindDemerzelRoot()
    {
        // Try relative to the repo root
        var candidates = new[]
        {
            Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "governance", "demerzel"),
            Path.Combine(Directory.GetCurrentDirectory(), "governance", "demerzel"),
            @"C:\Users\spare\source\repos\ga\governance\demerzel",
        };
        return candidates.FirstOrDefault(Directory.Exists);
    }

    // ─── Build the graph from filesystem ───
    private GovernanceGraph BuildGovernanceGraph(string root)
    {
        var nodes = new List<GovernanceNode>();
        var edges = new List<GovernanceEdge>();

        // Scan constitutions
        ScanDirectory(root, "constitutions", "constitution", "*.md", nodes);

        // Scan policies
        ScanDirectory(root, "policies", "policy", "*.yaml", nodes);

        // Scan personas
        ScanDirectory(root, "personas", "persona", "*.yaml", nodes);

        // Scan pipelines
        ScanDirectory(root, "pipelines", "pipeline", "*.ixql", nodes);

        // Scan schemas
        ScanDirectory(root, "schemas", "schema", "*.json", nodes);
        ScanDirectory(Path.Combine(root, "schemas"), "contracts", "schema", "*.json", nodes);

        // Scan tests
        ScanDirectory(root, "tests", "test", "*.test.md", nodes);

        // Scan departments
        var streelingDir = Path.Combine(root, "state", "streeling", "departments");
        ScanDirectory(streelingDir, "", "department", "*.json", nodes, "dept-");

        // Scan IXql examples
        ScanDirectory(Path.Combine(root, "examples"), "ixql", "ixql", "*.ixql", nodes);

        // Build edges based on naming conventions and references
        BuildEdges(nodes, edges);

        // Compute health metrics
        var globalHealth = ComputeGlobalHealth(root, nodes);

        return new GovernanceGraph
        {
            Nodes = nodes,
            Edges = edges,
            GlobalHealth = globalHealth,
            Timestamp = DateTime.UtcNow.ToString("O"),
        };
    }

    private static void ScanDirectory(string root, string subDir, string nodeType, string pattern, List<GovernanceNode> nodes, string idPrefix = "")
    {
        var dir = string.IsNullOrEmpty(subDir) ? root : Path.Combine(root, subDir);
        if (!Directory.Exists(dir)) return;

        foreach (var file in Directory.EnumerateFiles(dir, pattern))
        {
            var name = Path.GetFileNameWithoutExtension(file);
            var id = $"{idPrefix}{nodeType}-{name}".Replace(' ', '-').ToLowerInvariant();

            // Avoid duplicates
            if (nodes.Any(n => n.Id == id)) continue;

            nodes.Add(new GovernanceNode
            {
                Id = id,
                Name = ToTitle(name),
                Type = nodeType,
                Description = $"{ToTitle(nodeType)}: {ToTitle(name)}",
                Color = "#888888",
                Health = new HealthMetrics
                {
                    ResilienceScore = 0.8,
                    ErgolCount = 0,
                    LolliCount = 0,
                },
            });
        }
    }

    private static void BuildEdges(List<GovernanceNode> nodes, List<GovernanceEdge> edges)
    {
        var constitutions = nodes.Where(n => n.Type == "constitution").ToList();
        var policies = nodes.Where(n => n.Type == "policy").ToList();
        var personas = nodes.Where(n => n.Type == "persona").ToList();
        var departments = nodes.Where(n => n.Type == "department").ToList();

        // Constitution → policies
        foreach (var policy in policies)
        {
            if (constitutions.Count <= 0) continue;
            var constitution = constitutions[0]; // Root constitution
            edges.Add(new GovernanceEdge
            {
                Id = $"edge-{constitution.Id}-{policy.Id}",
                Source = constitution.Id,
                Target = policy.Id,
                Type = "constitutional-hierarchy",
                Weight = 1.0,
            });
        }

        // Policies → personas
        foreach (var persona in personas)
        {
            foreach (var policy in policies.Take(3)) // Connect to first 3 policies
            {
                edges.Add(new GovernanceEdge
                {
                    Id = $"edge-{policy.Id}-{persona.Id}",
                    Source = policy.Id,
                    Target = persona.Id,
                    Type = "policy-persona",
                    Weight = 0.5,
                });
            }
        }

        // Departments → personas (matching names)
        foreach (var dept in departments)
        {
            var matchingPersona = personas.FirstOrDefault(p =>
                dept.Name.Contains(p.Name, StringComparison.OrdinalIgnoreCase) ||
                p.Name.Contains(dept.Name, StringComparison.OrdinalIgnoreCase));
            if (matchingPersona != null)
            {
                edges.Add(new GovernanceEdge
                {
                    Id = $"edge-{dept.Id}-{matchingPersona.Id}",
                    Source = dept.Id,
                    Target = matchingPersona.Id,
                    Type = "policy-persona",
                    Weight = 0.7,
                });
            }
        }
    }

    private static HealthMetrics ComputeGlobalHealth(string root, List<GovernanceNode> nodes)
    {
        // Read resilience state if available
        var resilienceDir = Path.Combine(root, "state", "resilience");
        double resilience = 0.8;
        int lolliCount = 0;
        int ergolCount = nodes.Count; // Each node that exists is a live binding

        if (Directory.Exists(resilienceDir))
        {
            var files = Directory.GetFiles(resilienceDir, "*.json");
            if (files.Length > 0)
            {
                try
                {
                    var latest = files.OrderByDescending(f => f).First();
                    var json = System.IO.File.ReadAllText(latest);
                    using var doc = JsonDocument.Parse(json);
                    if (doc.RootElement.TryGetProperty("resilienceScore", out var score))
                        resilience = score.GetDouble();
                }
                catch { /* use default */ }
            }
        }

        return new HealthMetrics
        {
            ResilienceScore = resilience,
            LolliCount = lolliCount,
            ErgolCount = ergolCount,
        };
    }

    private static string ToTitle(string kebab) =>
        string.Join(' ', kebab.Split('-', '_', '.').Select(w =>
            string.IsNullOrEmpty(w) ? w : char.ToUpper(w[0]) + w[1..]));
}

// ─── DTOs ───

public record GovernanceGraph
{
    public List<GovernanceNode> Nodes { get; init; } = [];
    public List<GovernanceEdge> Edges { get; init; } = [];
    public HealthMetrics GlobalHealth { get; init; } = new();
    public string Timestamp { get; init; } = DateTime.UtcNow.ToString("O");

    public static GovernanceGraph Empty => new();
}

public record GovernanceNode
{
    public string Id { get; init; } = "";
    public string Name { get; init; } = "";
    public string Type { get; init; } = "";
    public string Description { get; init; } = "";
    public string Color { get; init; } = "#888888";
    public string? Repo { get; init; }
    public string? Domain { get; init; }
    public string? Version { get; init; }
    public HealthMetrics? Health { get; init; }
    public string? HealthStatus { get; init; }
    public string[]? Children { get; init; }
    public Dictionary<string, object>? Metadata { get; init; }
}

public record GovernanceEdge
{
    public string Id { get; init; } = "";
    public string Source { get; init; } = "";
    public string Target { get; init; } = "";
    public string Type { get; init; } = "";
    public string? Label { get; init; }
    public double Weight { get; init; } = 1.0;
}

public record HealthMetrics
{
    public double ResilienceScore { get; init; } = 0.8;
    public int LolliCount { get; init; }
    public int ErgolCount { get; init; }
    public double Staleness { get; init; }
    public double[]? MarkovPrediction { get; init; }
}

public record ScreenshotRequest
{
    public string? Reason { get; init; }
}
