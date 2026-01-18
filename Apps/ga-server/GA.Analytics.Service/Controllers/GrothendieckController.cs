namespace GA.Analytics.Service.Controllers;
using Microsoft.AspNetCore.Mvc;

using System.Buffers;
using System.Runtime.CompilerServices;
using System.Text.Json;
using static GA.Analytics.Service.Services.Constants;
using GA.Business.Config;
using GA.Business.Core.Atonal;
using GA.Business.Core.Atonal.Grothendieck;
using GA.Business.Core.Fretboard;
using GA.Business.Core.Notes;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Caching.Memory;
using GA.Analytics.Service.Services;
using GA.Analytics.Service.Constants;
using GA.Business.Core.Fretboard.Shapes;
using GA.Business.Core.Fretboard.Shapes.Applications;
using GA.Business.Core.Fretboard.Shapes.InformationTheory;
using GA.Business.Core.Fretboard.Shapes.DynamicalSystems;

/// <summary>
///     API endpoints for Grothendieck monoid operations and fretboard shape navigation
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
[EnableRateLimiting("regular")]
public class GrothendieckController(
    GA.Analytics.Service.Services.IGrothendieckService grothendieckService,
    GA.Business.Core.Fretboard.Shapes.IShapeGraphBuilder shapeGraphBuilder,
    MarkovWalker markovWalker,
    GA.Business.Core.Fretboard.Shapes.Applications.HarmonicAnalysisEngine harmonicAnalysisEngine,
    GA.Business.Core.Fretboard.Shapes.Applications.ProgressionOptimizer progressionOptimizer,
    ILogger<GrothendieckController> logger,
    IMemoryCache cache,
    PerformanceMetricsService metrics)
    : ControllerBase
{
    /// <summary>
    ///     Compute interval-class vector for a pitch-class set
    /// </summary>
    /// <param name="request">Pitch classes (0-11)</param>
    /// <returns>Interval-class vector</returns>
    [HttpPost("compute-icv")]
    [ProducesResponseType(typeof(IntervalClassVector), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IntervalClassVector>> ComputeICV([FromBody] ComputeIcvRequest request)
    {
        using var _ = metrics.TrackRegularRequest();

        if (request.PitchClasses.Length == 0)
        {
            return BadRequest("Pitch classes cannot be empty");
        }

        if (request.PitchClasses.Any(pc => pc is < 0 or > 11))
        {
            return BadRequest("Pitch classes must be between 0 and 11");
        }

        var icv = await grothendieckService.ComputeICV(
            "pitch_classes",
            new Dictionary<string, object> { ["pitchClasses"] = request.PitchClasses }
        );
        return Ok(icv);
    }

    /// <summary>
    ///     Compute Grothendieck delta between two interval-class vectors
    /// </summary>
    /// <param name="request">Source and target ICVs</param>
    /// <returns>Grothendieck delta with explanation</returns>
    [HttpPost("compute-delta")]
    [ProducesResponseType(typeof(GrothendieckDeltaResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<GrothendieckDeltaResponse>> ComputeDelta([FromBody] ComputeDeltaRequest request)
    {
        using var _ = metrics.TrackRegularRequest();

        var delta = await grothendieckService.ComputeDelta(
            "delta",
            new Dictionary<string, object>
            {
                ["source"] = request.Source,
                ["target"] = request.Target
            });

        var cost = await grothendieckService.ComputeHarmonicCost(
            "cost",
            new Dictionary<string, object>
            {
                ["delta"] = delta
            });

        // Mock conversion for response - in real app, map appropriately
        var responseDelta = new GA.Business.Core.Atonal.Grothendieck.GrothendieckDelta(0, 0, 0, 0, 0, 0); 

        return Ok(new GrothendieckDeltaResponse(
            responseDelta,
            cost.Value,
            delta.Explain()
        ));
    }

    /// <summary>
    ///     Find pitch-class sets harmonically similar to the given set
    /// </summary>
    /// <param name="request">Source pitch-class set and search parameters</param>
    /// <returns>List of nearby pitch-class sets with deltas and costs</returns>
    [HttpPost("find-nearby")]
    [ProducesResponseType(typeof(IEnumerable<NearbySetResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IEnumerable<NearbySetResponse>>> FindNearby([FromBody] FindNearbyRequest request)
    {
        using var _ = metrics.TrackRegularRequest();

        var result = await grothendieckService.FindNearby(
            string.Join(",", request.PitchClasses),
            new Dictionary<string, object>
            {
                ["limit"] = request.MaxResults,
                ["maxDistance"] = request.MaxDistance
            });

        // Mock response
        return Ok(new List<NearbySetResponse>());
    }



    /// <summary>
    ///     Generate fretboard shapes for a pitch-class set
    /// </summary>
    /// <param name="request">Pitch-class set and generation options</param>
    /// <returns>List of fretboard shapes</returns>
    [HttpPost("generate-shapes")]
    [ProducesResponseType(typeof(IEnumerable<FretboardShapeResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public ActionResult<IEnumerable<FretboardShapeResponse>> GenerateShapes([FromBody] GenerateShapesRequest request)
    {
        using var _ = metrics.TrackRegularRequest();

        var cacheKey = CacheKeys.GetFretboardShapesKey($"{request.TuningId}:{string.Join("", request.PitchClasses)}:{request.GetHashCode()}");

        if (cache.TryGetValue(cacheKey, out IEnumerable<FretboardShapeResponse>? cached))
        {
            return Ok(cached);
        }

        var tuning = Tuning.Default; // TODO: Support multiple tunings
        var pcs = PitchClassSet.Parse(string.Join("", request.PitchClasses.Select(pc => pc.ToString("X"))));

        var options = new ShapeGraphBuildOptions
        {
            MaxFret = request.MaxFret,
            MaxSpan = request.MaxSpan,
            MinErgonomics = request.MinErgonomics,
            MaxShapesPerSet = request.MaxShapes
        };

        var shapes = shapeGraphBuilder.GenerateShapes(tuning, pcs, options);

        var results = shapes.Select(s => new FretboardShapeResponse(
            Id: s.Id,
            Positions: s.Positions.Select(p => new PositionResponse(
                String: p.Str.Value,
                Fret: p.Fret.Value,
                IsMuted: p.IsMuted
            )).ToArray(),
            MinFret: s.MinFret,
            MaxFret: s.MaxFret,
            Span: s.Span,
            Diagness: s.Diagness,
            Ergonomics: s.Ergonomics,
            FingerCount: s.FingerCount,
            Tags: s.Tags
        )).ToList();

        cache.Set(cacheKey, results, CacheKeys.Durations.FretboardShapes);

        return Ok(results);
    }

    /// <summary>
    ///     Generate fretboard shapes for a pitch-class set (streaming version)
    ///     Returns shapes progressively as they're generated for better performance and UX
    /// </summary>
    /// <param name="tuningId">Tuning ID (e.g., "standard")</param>
    /// <param name="pitchClasses">Comma-separated pitch classes (e.g., "0,4,7" for C major)</param>
    /// <param name="maxFret">Maximum fret to consider (default: 12)</param>
    /// <param name="maxSpan">Maximum span (fret range) for a shape (default: 5)</param>
    /// <param name="minErgonomics">Minimum ergonomics score (0-1, default: 0.3)</param>
    /// <param name="maxShapes">Maximum number of shapes to return (default: 20)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Stream of fretboard shapes</returns>
    [HttpGet("generate-shapes-stream")]
    [ProducesResponseType(typeof(IAsyncEnumerable<FretboardShapeResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async IAsyncEnumerable<FretboardShapeResponse> GenerateShapesStream(
        [FromQuery] string tuningId = "standard",
        [FromQuery] string pitchClasses = "047",
        [FromQuery] int maxFret = 12,
        [FromQuery] int maxSpan = 5,
        [FromQuery] double minErgonomics = 0.3,
        [FromQuery] int maxShapes = 20,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        using var _ = metrics.TrackRegularRequest();

        logger.LogInformation(
            "Streaming shapes for {PitchClasses} on {Tuning}",
            pitchClasses,
            tuningId
        );

        // Parse parameters
        var tuning = Tuning.Default; // TODO: Support multiple tunings
        var pcs = PitchClassSet.Parse(pitchClasses);

        var options = new ShapeGraphBuildOptions
        {
            MaxFret = maxFret,
            MaxSpan = maxSpan,
            MinErgonomics = minErgonomics,
            MaxShapesPerSet = maxShapes
        };

        var count = 0;

        // Stream shapes as they're generated
        await foreach (var shape in
                       shapeGraphBuilder.GenerateShapesStreamAsync(tuning, pcs, options, cancellationToken))
        {
            count++;

            yield return new FretboardShapeResponse(
                shape.Id,
                shape.Positions.Select(p => new PositionResponse(
                    String: p.Str.Value,
                    Fret: p.Fret.Value,
                    IsMuted: p.IsMuted
                )).ToArray(),
                shape.MinFret,
                shape.MaxFret,
                shape.Span,
                shape.Diagness,
                shape.Ergonomics,
                shape.FingerCount,
                shape.Tags
            );

            // Log progress every 10 shapes
            if (count % 10 == 0)
            {
                logger.LogDebug("Streamed {Count} shapes so far", count);
            }
        }

        logger.LogInformation("Completed streaming {Count} shapes", count);
    }

    /// <summary>
    ///     Generate a fretboard heat map showing probability distribution for next positions
    /// </summary>
    /// <param name="request">Current shape and walk options</param>
    /// <returns>6x24 heat map grid (normalized 0-1)</returns>
    [HttpPost("heat-map")]
    [ProducesResponseType(typeof(HeatMapResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<HeatMapResponse>> GenerateHeatMap([FromBody] HeatMapRequest request)
    {
        using var _ = metrics.TrackRegularRequest();

        var cacheKey = CacheKeys.GetHeatMapKey($"{request.CurrentShapeId}:{request.GetHashCode()}");

        if (cache.TryGetValue(cacheKey, out HeatMapResponse? cached))
        {
            return Ok(cached);
        }

        // Build shape graph (cached internally)
        var tuning = Tuning.Default;
        var pitchClassSets = PitchClassSet.Items.Where(pcs => pcs.Cardinality == request.Cardinality).ToList();

        var graphOptions = new ShapeGraphBuildOptions
        {
            MaxFret = 12,
            MaxShapesPerSet = 10
        };

        var graph = await shapeGraphBuilder.BuildGraphAsync(tuning, pitchClassSets, graphOptions);

        if (!graph.Shapes.TryGetValue(request.CurrentShapeId, out var currentShape))
        {
            return BadRequest($"Shape {request.CurrentShapeId} not found");
        }

        var walkOptions = new WalkOptions
        {
            Steps = 1,
            Temperature = request.Temperature,
            BoxPreference = request.BoxPreference,
            MaxSpan = request.MaxSpan
        };

        var heatMap = markovWalker.GenerateHeatMap(graph, currentShape, walkOptions);

        var response = new HeatMapResponse(
            ConvertHeatMapToArray(heatMap),
            6,
            24
        );

        cache.Set(cacheKey, response, CacheKeys.Durations.HeatMap);

        return Ok(response);
    }

    /// <summary>
    ///     Generate a practice path with gradual difficulty progression
    /// </summary>
    /// <param name="request">Starting shape and practice options</param>
    /// <returns>Sequence of shapes for practice</returns>
    [HttpPost("practice-path")]
    [ProducesResponseType(typeof(PracticePathResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<PracticePathResponse>> GeneratePracticePath([FromBody] PracticePathRequest request)
    {
        using var _ = metrics.TrackRegularRequest();

        // Build shape graph
        var tuning = Tuning.Default;
        var pitchClassSets = PitchClassSet.Items.Where(pcs => pcs.Cardinality == request.Cardinality).ToList();

        var graphOptions = new ShapeGraphBuildOptions
        {
            MaxFret = 12,
            MaxShapesPerSet = 10
        };

        var graph = await shapeGraphBuilder.BuildGraphAsync(tuning, pitchClassSets, graphOptions);

        if (!graph.Shapes.TryGetValue(request.StartShapeId, out var startShape))
        {
            return BadRequest($"Shape {request.StartShapeId} not found");
        }

        var walkOptions = new WalkOptions
        {
            Steps = request.Steps,
            Temperature = request.Temperature,
            MaxSpan = request.MaxSpan
        };

        var path = markovWalker.GeneratePracticePath(graph, startShape, walkOptions);

        var response = new PracticePathResponse(
            path.Select(s => new FretboardShapeResponse(
                Id: s.Id,
                Positions: s.Positions.Select(p => new PositionResponse(
                    String: p.Str.Value,
                    Fret: p.Fret.Value,
                    IsMuted: p.IsMuted
                )).ToArray(),
                MinFret: s.MinFret,
                MaxFret: s.MaxFret,
                Span: s.Span,
                Diagness: s.Diagness,
                Ergonomics: s.Ergonomics,
                FingerCount: s.FingerCount,
                Tags: s.Tags
            )).ToArray()
        );

        return Ok(response);
    }

    /// <summary>
    ///     Convert 2D heat map to jagged array using ArrayPool for memory efficiency
    /// </summary>
    private double[][] ConvertHeatMapToArray(double[,] heatMap)
    {
        var pool = ArrayPool<double>.Shared;
        var result = new double[6][];

        for (var s = 0; s < 6; s++)
        {
            // Rent array from pool (may be larger than needed)
            var rented = pool.Rent(24);

            // Copy data
            for (var f = 0; f < 24; f++)
            {
                rented[f] = heatMap[s, f];
            }

            // Create exact-size array for result
            result[s] = new double[24];
            Array.Copy(rented, result[s], 24);

            // Return rented array to pool
            pool.Return(rented, true);
        }

        return result;
    }

    /// <summary>
    ///     Analyze a shape graph using comprehensive harmonic analysis
    /// </summary>
    /// <param name="request">Analysis request with pitch classes and options</param>
    /// <returns>Comprehensive analysis including spectral, dynamical, and topological metrics</returns>
    [HttpPost("analyze-shape-graph")]
    [ProducesResponseType(typeof(ShapeGraphAnalysisResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ShapeGraphAnalysisResponse>> AnalyzeShapeGraph(
        [FromBody] AnalyzeShapeGraphRequest request)
    {
        using var _ = metrics.TrackRegularRequest();

        logger.LogInformation(
            "Analyzing shape graph for {PitchClasses} on {Tuning}",
            string.Join(",", request.PitchClasses),
            request.TuningId
        );

        try
        {
            // Get tuning
            var tuning = GetTuningFromString(request.TuningId) ?? Tuning.Default;

            // Parse pitch class sets
            var pitchClassSets = new[] { PitchClassSet.Parse(string.Join("", request.PitchClasses)) };

            // Build shape graph
            var graph = await shapeGraphBuilder.BuildGraphAsync(
                tuning,
                pitchClassSets,
                new ShapeGraphBuildOptions
                {
                    MaxFret = request.MaxFret,
                    MaxSpan = request.MaxSpan,
                    MaxShapesPerSet = 20
                }
            );

            // Validate graph has shapes
            if (graph.ShapeCount == 0)
            {
                return BadRequest(
                    $"No valid shapes found for pitch classes {string.Join(",", request.PitchClasses)} on {request.TuningId}. Try reducing the number of pitch classes or increasing MaxFret/MaxSpan.");
            }

            // Perform comprehensive analysis
            var report = await harmonicAnalysisEngine.AnalyzeAsync(graph, new HarmonicAnalysisOptions
            {
                IncludeSpectralAnalysis = request.IncludeSpectralAnalysis,
                IncludeDynamicalAnalysis = request.IncludeDynamicalAnalysis,
                IncludeTopologicalAnalysis = request.IncludeTopologicalAnalysis,
                ClusterCount = request.ClusterCount,
                TopCentralShapes = request.TopCentralShapes
            });

            // Map to response DTOs
            var response = new ShapeGraphAnalysisResponse(
                report.Spectral != null
                    ? new SpectralMetricsDto(
                        report.Spectral.AlgebraicConnectivity,
                        report.Spectral.SpectralGap,
                        report.Spectral.EstimatedComponentCount,
                        0.0, // Not available in SpectralMetrics
                        0 // Not available in SpectralMetrics
                    )
                    : null,
                report.ChordFamilies.Select(cf => new ChordFamilyDto(
                    ClusterId: cf.Id,
                    ShapeIds: cf.ShapeIds.ToList(),
                    Representative: cf.ShapeIds.FirstOrDefault() ?? ""
                )).ToList(),
                report.CentralShapes.Select(cs => new CentralShapeDto(
                    ShapeId: cs.Item1,
                    Centrality: cs.Item2
                )).ToList(),
                report.Bottlenecks.Select(b => new BottleneckDto(
                    ShapeId: b.Item1,
                    BetweennessCentrality: b.Item2
                )).ToList(),
                report.Dynamics != null
                    ? new DynamicsDto(
                        report.Dynamics.Attractors.Select(a => new AttractorDto(
                            ShapeId: a.ShapeId,
                            BasinSize: a.Strength,
                            Type: "Attractor"
                        )).ToList(),
                        report.Dynamics.LimitCycles.Select(lc => new LimitCycleDto(
                            ShapeIds: lc.ShapeIds.ToList(),
                            Period: lc.Period,
                            Stability: lc.Stability
                        )).ToList(),
                        report.Dynamics.LyapunovExponent,
                        report.Dynamics.IsChaotic,
                        true // IsStable placeholder
                    )
                    : null,
                report.Topology != null
                    ? new TopologyDto(
                        report.Topology.BettiNumbers.ElementAtOrDefault(0),
                        report.Topology.BettiNumbers.ElementAtOrDefault(1),
                        new List<PersistentFeatureDto>() // Intervals removed
                    )
                    : null
            );

            logger.LogInformation(
                "Analysis complete: {CentralShapes} central shapes, {ChordFamilies} families, {Attractors} attractors",
                response.CentralShapes.Count,
                response.ChordFamilies.Count,
                response.Dynamics?.Attractors.Count ?? 0
            );

            return Ok(response);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error analyzing shape graph");
            return BadRequest($"Error analyzing shape graph: {ex.Message}");
        }
    }

    /// <summary>
    ///     Analyze a shape graph with streaming progress updates (Server-Sent Events)
    /// </summary>
    /// <param name="request">Analysis request with pitch classes and options</param>
    [HttpPost("analyze-shape-graph/stream")]
    public async Task AnalyzeShapeGraphStream([FromBody] AnalyzeShapeGraphRequest request)
    {
        Response.Headers.Append("Content-Type", "text/event-stream");
        Response.Headers.Append("Cache-Control", "no-cache");
        Response.Headers.Append("Connection", "keep-alive");

        try
        {
            await Response.WriteAsync("data: {\"status\": \"started\", \"message\": \"Building shape graph...\"}\n\n");
            await Response.Body.FlushAsync();

            // Get tuning
            var tuning = GetTuningFromString(request.TuningId) ?? Tuning.Default;
            var pitchClassSets = new[] { PitchClassSet.Parse(string.Join("", request.PitchClasses)) };

            // Build shape graph
            var graph = await shapeGraphBuilder.BuildGraphAsync(
                tuning,
                pitchClassSets,
                new ShapeGraphBuildOptions
                {
                    MaxFret = request.MaxFret,
                    MaxSpan = request.MaxSpan,
                    MaxShapesPerSet = 20
                }
            );

            await Response.WriteAsync(
                $"data: {{\"status\": \"progress\", \"message\": \"Graph built with {graph.Shapes.Count} shapes. Running spectral analysis...\"}}\n\n");
            await Response.Body.FlushAsync();

            // Perform comprehensive analysis
            var report = await harmonicAnalysisEngine.AnalyzeAsync(graph, new HarmonicAnalysisOptions
            {
                IncludeSpectralAnalysis = request.IncludeSpectralAnalysis,
                IncludeDynamicalAnalysis = request.IncludeDynamicalAnalysis,
                IncludeTopologicalAnalysis = request.IncludeTopologicalAnalysis,
                ClusterCount = request.ClusterCount,
                TopCentralShapes = request.TopCentralShapes
            });

            await Response.WriteAsync(
                "data: {\"status\": \"progress\", \"message\": \"Analysis complete. Formatting results...\"}\n\n");
            await Response.Body.FlushAsync();

            // Map to response DTOs
            var response = new ShapeGraphAnalysisResponse(
                report.Spectral != null
                    ? new SpectralMetricsDto(
                        report.Spectral.AlgebraicConnectivity,
                        report.Spectral.SpectralGap,
                        report.Spectral.EstimatedComponentCount,
                        0.0,
                        0
                    )
                    : null,
                report.ChordFamilies.Select(cf => new ChordFamilyDto(
                    ClusterId: cf.Id,
                    ShapeIds: cf.ShapeIds.ToList(),
                    Representative: cf.ShapeIds.FirstOrDefault() ?? ""
                )).ToList(),
                report.CentralShapes.Select(cs => new CentralShapeDto(
                    ShapeId: cs.Item1,
                    Centrality: cs.Item2
                )).ToList(),
                report.Bottlenecks.Select(b => new BottleneckDto(
                    ShapeId: b.Item1,
                    BetweennessCentrality: b.Item2
                )).ToList(),
                report.Dynamics != null
                    ? new DynamicsDto(
                        report.Dynamics.Attractors.Select(a => new AttractorDto(
                            ShapeId: a.ShapeId,
                            BasinSize: a.Strength,
                            Type: "Attractor"
                        )).ToList(),
                        report.Dynamics.LimitCycles.Select(lc => new LimitCycleDto(
                            ShapeIds: lc.ShapeIds.ToList(),
                            Period: lc.Period,
                            Stability: lc.Stability
                        )).ToList(),
                        report.Dynamics.LyapunovExponent,
                        report.Dynamics.IsChaotic,
                        true // IsStable placeholder
                    )
                    : null,
                report.Topology != null
                    ? new TopologyDto(
                        report.Topology.BettiNumbers.ElementAtOrDefault(0),
                        report.Topology.BettiNumbers.ElementAtOrDefault(1),
                        new List<PersistentFeatureDto>() // Intervals removed
                    )
                    : null
            );

            // Send final result
            var jsonResponse = JsonSerializer.Serialize(new { status = "complete", data = response });
            await Response.WriteAsync($"data: {jsonResponse}\n\n");
            await Response.WriteAsync("data: [DONE]\n\n");
            await Response.Body.FlushAsync();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error in streaming shape graph analysis");
            await Response.WriteAsync($"data: {{\"status\": \"error\", \"message\": \"{ex.Message}\"}}\n\n");
        }
    }

    /// <summary>
    ///     Generate an optimal practice path using progression optimization
    /// </summary>
    /// <param name="request">Practice path generation request</param>
    /// <returns>Optimized practice path with quality metrics</returns>
    [HttpPost("generate-practice-path")]
    [ProducesResponseType(typeof(OptimizedPracticePathResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<OptimizedPracticePathResponse>> GeneratePracticePath(
        [FromBody] GeneratePracticePathRequest request)
    {
        using var _ = metrics.TrackRegularRequest();

        logger.LogInformation(
            "Generating practice path for {PitchClasses}, length={Length}, strategy={Strategy}",
            string.Join(",", request.PitchClasses),
            request.PathLength,
            request.Strategy
        );

        try
        {
            // Get tuning
            var tuning = GetTuningFromString(request.TuningId) ?? Tuning.Default;

            // Parse pitch class sets
            var pitchClassSets = new[] { PitchClassSet.Parse(string.Join("", request.PitchClasses)) };

            // Build shape graph
            var graph = await shapeGraphBuilder.BuildGraphAsync(
                tuning,
                pitchClassSets,
                new ShapeGraphBuildOptions
                {
                    MaxFret = request.MaxFret,
                    MaxSpan = request.MaxSpan,
                    MaxShapesPerSet = 50
                }
            );

            // Validate graph has shapes
            if (graph.ShapeCount == 0)
            {
                return BadRequest(
                    $"No valid shapes found for pitch classes {string.Join(",", request.PitchClasses)} on {request.TuningId}. Try reducing the number of pitch classes or increasing MaxFret/MaxSpan.");
            }

            // Parse strategy
            var strategy = request.Strategy.ToLowerInvariant() switch
            {
                "balanced" => GA.Business.Core.Fretboard.Shapes.Applications.OptimizationStrategy.BalancedPractice,
                "minimizevoiceleading" => GA.Business.Core.Fretboard.Shapes.Applications.OptimizationStrategy.MinimizeVoiceLeading,
                "maximizeinformationgain" => GA.Business.Core.Fretboard.Shapes.Applications.OptimizationStrategy.MaximizeVariety,
                _ => GA.Business.Core.Fretboard.Shapes.Applications.OptimizationStrategy.BalancedPractice
            };

            // Generate optimal progression
            var progression = progressionOptimizer.GeneratePracticeProgression(graph, new GA.Business.Core.Fretboard.Shapes.Applications.ProgressionConstraints
            {
                TargetLength = request.PathLength,
                StartShapeId = request.StartShapeId,
                Strategy = strategy,
                PreferCentralShapes = request.PreferCentralShapes,
                MinErgonomics = request.MinErgonomics
            });

            // Get shape details
            var shapes = new List<FretboardShapeResponse>();
            foreach (var shapeId in progression.ShapeIds)
            {
                if (graph.Shapes.TryGetValue(shapeId, out var shape))
                {
                    shapes.Add(new FretboardShapeResponse(
                        shape.Id,
                        shape.Positions.Select(p => new PositionResponse(
                            String: p.Str.Value,
                            Fret: p.Fret.Value,
                            IsMuted: p.IsMuted
                        )).ToArray(),
                        shape.MinFret,
                        shape.MaxFret,
                        shape.Span,
                        shape.Diagness,
                        shape.Ergonomics,
                        shape.FingerCount,
                        shape.Tags
                    ));
                }
            }

            var response = new OptimizedPracticePathResponse(
                progression.ShapeIds.ToList(),
                shapes,
                progression.Entropy,
                progression.Complexity,
                progression.Predictability,
                progression.Diversity,
                progression.Quality
            );

            return Ok(response);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error generating practice path");
            return BadRequest($"Error generating practice path: {ex.Message}");
        }
    }

    /// <summary>
    ///     Generate an optimal practice path with streaming progress updates (Server-Sent Events)
    /// </summary>
    /// <param name="request">Practice path generation request</param>
    [HttpPost("generate-practice-path/stream")]
    public async Task GeneratePracticePathStream([FromBody] GeneratePracticePathRequest request)
    {
        Response.Headers.Append("Content-Type", "text/event-stream");
        Response.Headers.Append("Cache-Control", "no-cache");
        Response.Headers.Append("Connection", "keep-alive");

        try
        {
            await Response.WriteAsync("data: {\"status\": \"started\", \"message\": \"Building shape graph...\"}\n\n");
            await Response.Body.FlushAsync();

            // Get tuning
            var tuning = GetTuningFromString(request.TuningId) ?? Tuning.Default;
            var pitchClassSets = new[] { PitchClassSet.Parse(string.Join("", request.PitchClasses)) };

            // Build shape graph
            var graph = await shapeGraphBuilder.BuildGraphAsync(
                tuning,
                pitchClassSets,
                new ShapeGraphBuildOptions
                {
                    MaxFret = request.MaxFret,
                    MaxSpan = request.MaxSpan,
                    MaxShapesPerSet = 50
                }
            );

            await Response.WriteAsync(
                $"data: {{\"status\": \"progress\", \"message\": \"Graph built with {graph.Shapes.Count} shapes. Optimizing practice path...\"}}\n\n");
            await Response.Body.FlushAsync();

            // Parse strategy
            var strategy = request.Strategy.ToLowerInvariant() switch
            {
                "balanced" => OptimizationStrategy.Balanced,
                "minimizevoiceleading" => OptimizationStrategy.MinimizeVoiceLeading,
                "maximizeinformationgain" => OptimizationStrategy.MaximizeInformationGain,
                "explorefamilies" => OptimizationStrategy.ExploreFamilies,
                "followattractors" => OptimizationStrategy.FollowAttractors,
                _ => OptimizationStrategy.Balanced
            };

            // Generate optimal progression
            var progression = progressionOptimizer.GeneratePracticeProgression(graph, new ProgressionConstraints
            {
                TargetLength = request.PathLength,
                StartShapeId = request.StartShapeId,
                Strategy = strategy,
                PreferCentralShapes = request.PreferCentralShapes,
                MinErgonomics = request.MinErgonomics
            });

            await Response.WriteAsync(
                "data: {\"status\": \"progress\", \"message\": \"Path optimized. Gathering shape details...\"}\n\n");
            await Response.Body.FlushAsync();

            // Get shape details
            var shapes = new List<FretboardShapeResponse>();
            foreach (var shapeId in progression.ShapeIds)
            {
                if (graph.Shapes.TryGetValue(shapeId, out var shape))
                {
                    shapes.Add(new FretboardShapeResponse(
                        shape.Id,
                        shape.Positions.Select(p => new PositionResponse(
                            String: p.Str.Value,
                            Fret: p.Fret.Value,
                            IsMuted: p.IsMuted
                        )).ToArray(),
                        shape.MinFret,
                        shape.MaxFret,
                        shape.Span,
                        shape.Diagness,
                        shape.Ergonomics,
                        shape.FingerCount,
                        shape.Tags
                    ));
                }
            }

            var response = new OptimizedPracticePathResponse(
                progression.ShapeIds.ToList(),
                shapes,
                progression.Entropy,
                progression.Complexity,
                progression.Predictability,
                progression.Diversity,
                progression.Quality
            );

            // Send final result
            var jsonResponse = JsonSerializer.Serialize(new { status = "complete", data = response });
            await Response.WriteAsync($"data: {jsonResponse}\n\n");
            await Response.WriteAsync("data: [DONE]\n\n");
            await Response.Body.FlushAsync();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error in streaming practice path generation");
            await Response.WriteAsync($"data: {{\"status\": \"error\", \"message\": \"{ex.Message}\"}}\n\n");
        }
    }

    /// <summary>
    ///     Helper method to get a Tuning from a tuning string (e.g., "E2 A2 D3 G3 B3 E4")
    /// </summary>
    private static Tuning? GetTuningFromString(string? tuningStr)
    {
        if (string.IsNullOrWhiteSpace(tuningStr))
        {
            return null;
        }

        // Try to parse as PitchCollection
        if (PitchCollection.TryParse(tuningStr, null, out var pitchCollection))
        {
            return new Tuning(pitchCollection);
        }

        // Try to get from instruments config
        var instruments = InstrumentsConfig.getAllInstruments();
        foreach (var instrument in instruments)
        {
            foreach (var tuning in instrument.Tunings)
            {
                if (tuning.Name.Equals(tuningStr, StringComparison.OrdinalIgnoreCase) ||
                    tuning.Tuning.Equals(tuningStr, StringComparison.OrdinalIgnoreCase))
                {
                    if (PitchCollection.TryParse(tuning.Tuning, null, out var pc))
                    {
                        return new Tuning(pc);
                    }
                }
            }
        }

        return null;
    }
}

// Request/Response DTOs
public record ComputeIcvRequest(int[] PitchClasses);

public record ComputeDeltaRequest(IntervalClassVector Source, IntervalClassVector Target);

public record GrothendieckDeltaResponse(GrothendieckDelta Delta, double HarmonicCost, string Explanation);

public record FindNearbyRequest(int[] PitchClasses, int MaxDistance = 3, int MaxResults = 10);

public record NearbySetResponse(
    int[] PitchClasses,
    string SetName,
    GrothendieckDelta Delta,
    double Cost,
    string Explanation);

public record GenerateShapesRequest(
    string TuningId,
    int[] PitchClasses,
    int MaxFret = 12,
    int MaxSpan = 5,
    double MinErgonomics = 0.3,
    int MaxShapes = 20);

public record PositionResponse(int String, int Fret, bool IsMuted);

public record FretboardShapeResponse(
    string Id,
    PositionResponse[] Positions,
    int MinFret,
    int MaxFret,
    int Span,
    double Diagness,
    double Ergonomics,
    int FingerCount,
    Dictionary<string, string> Tags);

public record HeatMapRequest(
    string CurrentShapeId,
    int Cardinality = 3,
    double Temperature = 1.0,
    bool? BoxPreference = null,
    int? MaxSpan = null);

public record HeatMapResponse(double[][] Grid, int Strings, int Frets);

public record PracticePathRequest(
    string StartShapeId,
    int Cardinality = 3,
    int Steps = 10,
    double Temperature = 0.8,
    int? MaxSpan = null);

public record PracticePathResponse(FretboardShapeResponse[] Shapes);

// Advanced Analysis DTOs
public record AnalyzeShapeGraphRequest(
    string TuningId,
    int[] PitchClasses,
    int MaxFret = 12,
    int MaxSpan = 5,
    bool IncludeSpectralAnalysis = true,
    bool IncludeDynamicalAnalysis = true,
    bool IncludeTopologicalAnalysis = true,
    int ClusterCount = 5,
    int TopCentralShapes = 10);

public record ShapeGraphAnalysisResponse(
    SpectralMetricsDto? Spectral,
    List<ChordFamilyDto> ChordFamilies,
    List<CentralShapeDto> CentralShapes,
    List<BottleneckDto> Bottlenecks,
    DynamicsDto? Dynamics,
    TopologyDto? Topology);

public record SpectralMetricsDto(
    double AlgebraicConnectivity,
    double SpectralGap,
    int ComponentCount,
    double AveragePathLength,
    double Diameter);

public record ChordFamilyDto(
    int ClusterId,
    List<string> ShapeIds,
    string Representative);

public record CentralShapeDto(
    string ShapeId,
    double Centrality);

public record BottleneckDto(
    string ShapeId,
    double BetweennessCentrality);

public record DynamicsDto(
    List<AttractorDto> Attractors,
    List<LimitCycleDto> LimitCycles,
    double LyapunovExponent,
    bool IsChaotic,
    bool IsStable);

public record AttractorDto(
    string ShapeId,
    double BasinSize,
    string Type);

public record LimitCycleDto(
    List<string> ShapeIds,
    int Period,
    double Stability);

public record TopologyDto(
    int BettiNumber0,
    int BettiNumber1,
    List<PersistentFeatureDto> Features);

public record PersistentFeatureDto(
    double Birth,
    double Death,
    double Persistence,
    int Dimension);

public record GeneratePracticePathRequest(
    string TuningId,
    int[] PitchClasses,
    int PathLength = 8,
    string? StartShapeId = null,
    string Strategy = "Balanced",
    bool PreferCentralShapes = true,
    double MinErgonomics = 0.5,
    int MaxFret = 12,
    int MaxSpan = 5);

public record OptimizedPracticePathResponse(
    List<string> ShapeIds,
    List<FretboardShapeResponse> Shapes,
    double Entropy,
    double Complexity,
    double Predictability,
    double Diversity,
    double Quality);
