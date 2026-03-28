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
    IHubContext<GovernanceHub> hubContext,
    Services.BeliefStateService beliefStateService,
    Services.VisualCriticService visualCriticService)
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
    ///     Get the content of a governance file by its relative path.
    ///     Path must be relative to the demerzel root (e.g., "constitutions/asimov.constitution.md").
    /// </summary>
    [HttpGet("file-content")]
    public async Task<ActionResult> GetFileContent([FromQuery] string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
            return BadRequest(new { error = "filePath query parameter is required" });

        var demerzelRoot = configuration["Governance:DemerzelRoot"] ?? FindDemerzelRoot();
        if (demerzelRoot == null || !Directory.Exists(demerzelRoot))
            return NotFound(new { error = "Governance root not found" });

        var resolvedPath = Path.GetFullPath(Path.Combine(demerzelRoot, filePath));
        var normalizedRoot = Path.GetFullPath(demerzelRoot);

        // Prevent directory traversal
        if (!resolvedPath.StartsWith(normalizedRoot, StringComparison.OrdinalIgnoreCase))
            return BadRequest(new { error = "Invalid file path" });

        if (!System.IO.File.Exists(resolvedPath))
            return NotFound(new { error = $"File not found: {filePath}" });

        var content = await System.IO.File.ReadAllTextAsync(resolvedPath);
        var extension = Path.GetExtension(resolvedPath).ToLowerInvariant();
        var mediaType = extension switch
        {
            ".md" => "text/markdown",
            ".yaml" or ".yml" => "text/yaml",
            ".json" => "application/json",
            ".ixql" => "text/plain",
            _ => "text/plain",
        };

        return Ok(new { content, filePath, mediaType });
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

    /// <summary>
    ///     Visual critic — analyze a Prime Radiant screenshot with Claude vision.
    ///     Returns quality score, issues, IXQL commands, and algedonic signal.
    ///     Future: refactor as ix pipeline connector.
    /// </summary>
    [HttpPost("visual-critic")]
    public async Task<ActionResult<Services.VisualCriticResult>> AnalyzeVisual(
        [FromBody] VisualCriticRequest request,
        CancellationToken ct)
    {
        if (string.IsNullOrEmpty(request.Image))
            return BadRequest(new { error = "image (base64) is required" });

        var result = await visualCriticService.AnalyzeScreenshotAsync(
            request.Image, request.MediaType ?? "image/png", ct);

        // Broadcast algedonic signal via SignalR if quality is notable
        if (result.SignalType != null)
        {
            await hubContext.Clients.All.SendAsync("AlgedonicSignal", new
            {
                signal = "visual_critic",
                type = result.SignalType,
                severity = result.SignalSeverity,
                description = result.SignalDescription,
                quality = result.Quality,
            }, ct);
        }

        return Ok(result);
    }

    public class VisualCriticRequest
    {
        public string Image { get; set; } = "";
        public string? MediaType { get; set; }
    }

    /// <summary>
    ///     Get all belief states from the Demerzel governance directory.
    ///     Each belief uses tetravalent logic: T (true), F (false), U (unknown), C (contradictory).
    /// </summary>
    [HttpGet("beliefs")]
    public ActionResult<List<Services.BeliefState>> GetBeliefs()
    {
        try
        {
            var beliefs = beliefStateService.GetBeliefs();
            return Ok(beliefs);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to read belief states");
            return StatusCode(500, new { error = "Failed to read belief states" });
        }
    }

    /// <summary>
    ///     Update a belief state and broadcast the change to connected clients.
    /// </summary>
    [HttpPut("beliefs/{id}")]
    public async Task<ActionResult<Services.BeliefState>> UpdateBelief(
        string id,
        [FromBody] BeliefUpdateRequest request)
    {
        try
        {
            var updated = beliefStateService.UpdateBelief(id, request.Status, request.Evidence);
            if (updated == null)
                return NotFound(new { error = $"Belief '{id}' not found" });

            // Broadcast to connected clients
            await GovernanceHub.BroadcastBeliefUpdate(hubContext, updated);

            return Ok(updated);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to update belief {Id}", id);
            return StatusCode(500, new { error = "Failed to update belief state" });
        }
    }

    /// <summary>
    ///     Get the project backlog parsed from BACKLOG.md.
    /// </summary>
    [HttpGet("backlog")]
    [ResponseCache(Duration = 300)]
    public ActionResult GetBacklog()
    {
        var repoRoot = FindRepoRoot();
        if (repoRoot == null)
            return Ok(new { sections = Array.Empty<object>() });

        var backlogFile = Path.Combine(repoRoot, "BACKLOG.md");
        if (!System.IO.File.Exists(backlogFile))
            return Ok(new { sections = Array.Empty<object>() });

        var lines = System.IO.File.ReadAllLines(backlogFile);
        var sections = new List<object>();
        string? currentSection = null;
        string? currentSubsection = null;
        var items = new List<string>();

        foreach (var line in lines)
        {
            if (line.StartsWith("## "))
            {
                if (currentSection != null && items.Count > 0)
                    sections.Add(new { section = currentSection, subsection = currentSubsection, items = items.ToArray() });
                currentSection = line[3..].Trim();
                currentSubsection = null;
                items = [];
            }
            else if (line.StartsWith("### "))
            {
                if (items.Count > 0 && currentSection != null)
                    sections.Add(new { section = currentSection, subsection = currentSubsection, items = items.ToArray() });
                currentSubsection = line[4..].Trim();
                items = [];
            }
            else if (line.StartsWith("- **"))
            {
                var boldEnd = line.IndexOf("**", 4);
                if (boldEnd > 4)
                {
                    var title = line[4..boldEnd];
                    var desc = boldEnd + 2 < line.Length ? line[(boldEnd + 2)..].TrimStart(' ', '\u2014', '-', ' ') : "";
                    items.Add($"{title}: {desc}");
                }
                else
                {
                    items.Add(line[2..].Trim());
                }
            }
        }
        if (currentSection != null && items.Count > 0)
            sections.Add(new { section = currentSection, subsection = currentSubsection, items = items.ToArray() });

        return Ok(new { sections, lastModified = System.IO.File.GetLastWriteTimeUtc(backlogFile) });
    }

    /// <summary>
    ///     Get active agent teams and their members (for AgentPanel).
    /// </summary>
    [HttpGet("agents")]
    public ActionResult GetAgents()
    {
        // Check for Claude Code agent state files
        var repoRoot = FindRepoRoot();
        var teams = new List<object>();

        if (repoRoot != null)
        {
            var worktreeDir = Path.Combine(repoRoot, ".claude", "worktrees");
            if (Directory.Exists(worktreeDir))
            {
                foreach (var dir in Directory.GetDirectories(worktreeDir))
                {
                    var name = Path.GetFileName(dir);
                    teams.Add(new
                    {
                        id = name,
                        name = name.Replace("agent-", "Agent "),
                        status = "active",
                        agents = new[]
                        {
                            new { id = name, name, type = "worktree", status = "running", task = "Working in isolated worktree" },
                        },
                    });
                }
            }
        }

        return Ok(new { teams });
    }

    private static string? FindRepoRoot()
    {
        var candidates = new[]
        {
            Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."),
            Directory.GetCurrentDirectory(),
            @"C:\Users\spare\source\repos\ga",
        };
        return candidates.FirstOrDefault(d => System.IO.File.Exists(Path.Combine(d, "BACKLOG.md")));
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
        ScanDirectory(root, "constitutions", "constitution", "*.md", nodes, demerzelRoot: root);

        // Scan policies
        ScanDirectory(root, "policies", "policy", "*.yaml", nodes, demerzelRoot: root);

        // Scan personas
        ScanDirectory(root, "personas", "persona", "*.yaml", nodes, demerzelRoot: root);

        // Scan pipelines
        ScanDirectory(root, "pipelines", "pipeline", "*.ixql", nodes, demerzelRoot: root);

        // Scan schemas
        ScanDirectory(root, "schemas", "schema", "*.json", nodes, demerzelRoot: root);
        ScanDirectory(Path.Combine(root, "schemas"), "contracts", "schema", "*.json", nodes, demerzelRoot: root);

        // Scan tests
        ScanDirectory(root, "tests", "test", "*.test.md", nodes, demerzelRoot: root);

        // Scan departments
        var streelingDir = Path.Combine(root, "state", "streeling", "departments");
        ScanDirectory(streelingDir, "", "department", "*.json", nodes, "dept-", demerzelRoot: root);

        // Scan IXql examples
        ScanDirectory(Path.Combine(root, "examples"), "ixql", "ixql", "*.ixql", nodes, demerzelRoot: root);

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

    private static void ScanDirectory(string root, string subDir, string nodeType, string pattern, List<GovernanceNode> nodes, string idPrefix = "", string? demerzelRoot = null)
    {
        var dir = string.IsNullOrEmpty(subDir) ? root : Path.Combine(root, subDir);
        if (!Directory.Exists(dir)) return;

        var effectiveRoot = demerzelRoot ?? root;

        foreach (var file in Directory.EnumerateFiles(dir, pattern))
        {
            var name = Path.GetFileNameWithoutExtension(file);
            var id = $"{idPrefix}{nodeType}-{name}".Replace(' ', '-').ToLowerInvariant();

            // Avoid duplicates
            if (nodes.Any(n => n.Id == id)) continue;

            // Compute relative path from demerzel root (forward slashes for consistency)
            var relativePath = Path.GetRelativePath(effectiveRoot, file).Replace('\\', '/');

            nodes.Add(new GovernanceNode
            {
                Id = id,
                Name = ToTitle(name),
                Type = nodeType,
                Description = $"{ToTitle(nodeType)}: {ToTitle(name)}",
                FilePath = relativePath,
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

        // Departments → first constitution (organizational hierarchy)
        foreach (var dept in departments)
        {
            if (constitutions.Count > 0)
            {
                edges.Add(new GovernanceEdge
                {
                    Id = $"edge-{constitutions[0].Id}-{dept.Id}",
                    Source = constitutions[0].Id,
                    Target = dept.Id,
                    Type = "constitutional-hierarchy",
                    Weight = 0.6,
                });
            }
            // Also connect to matching persona if names align
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

        // Schemas → policies (schemas validate governance artifacts)
        var schemas = nodes.Where(n => n.Type == "schema").ToList();
        foreach (var schema in schemas)
        {
            if (policies.Count > 0)
            {
                // Connect each schema to a related policy (round-robin)
                var policy = policies[schemas.IndexOf(schema) % policies.Count];
                edges.Add(new GovernanceEdge
                {
                    Id = $"edge-{policy.Id}-{schema.Id}",
                    Source = policy.Id,
                    Target = schema.Id,
                    Type = "policy-schema",
                    Weight = 0.4,
                });
            }
        }

        // Tests → constitutions (tests verify governance compliance)
        var tests = nodes.Where(n => n.Type == "test").ToList();
        foreach (var test in tests)
        {
            if (constitutions.Count > 0)
            {
                edges.Add(new GovernanceEdge
                {
                    Id = $"edge-{constitutions[0].Id}-{test.Id}",
                    Source = constitutions[0].Id,
                    Target = test.Id,
                    Type = "governance-test",
                    Weight = 0.3,
                });
            }
        }

        // Pipelines → policies (pipelines enforce policy automation)
        var pipelines = nodes.Where(n => n.Type == "pipeline").ToList();
        foreach (var pipeline in pipelines)
        {
            if (policies.Count > 0)
            {
                var policy = policies[pipelines.IndexOf(pipeline) % policies.Count];
                edges.Add(new GovernanceEdge
                {
                    Id = $"edge-{policy.Id}-{pipeline.Id}",
                    Source = policy.Id,
                    Target = pipeline.Id,
                    Type = "policy-pipeline",
                    Weight = 0.4,
                });
            }
        }

        // IxQL examples → pipelines or schemas (IxQL queries governance)
        var ixqls = nodes.Where(n => n.Type == "ixql").ToList();
        foreach (var ixql in ixqls)
        {
            if (pipelines.Count > 0)
            {
                var pipeline = pipelines[ixqls.IndexOf(ixql) % pipelines.Count];
                edges.Add(new GovernanceEdge
                {
                    Id = $"edge-{pipeline.Id}-{ixql.Id}",
                    Source = pipeline.Id,
                    Target = ixql.Id,
                    Type = "pipeline-ixql",
                    Weight = 0.3,
                });
            }
            else if (schemas.Count > 0)
            {
                var schema = schemas[ixqls.IndexOf(ixql) % schemas.Count];
                edges.Add(new GovernanceEdge
                {
                    Id = $"edge-{schema.Id}-{ixql.Id}",
                    Source = schema.Id,
                    Target = ixql.Id,
                    Type = "schema-ixql",
                    Weight = 0.3,
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
    public string? FilePath { get; init; }
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

public record BeliefUpdateRequest
{
    public string Status { get; init; } = "U";
    public string Evidence { get; init; } = "";
}
