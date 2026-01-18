namespace GA.AI.Service.Controllers;

using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using GA.AI.Service.Models;
using GA.AI.Service.Services;

[ApiController]
[Route("api/[controller]")]
[EnableRateLimiting("admin")]
public class VectorSearchStrategyController(
    EnhancedVectorSearchService vectorSearch,
    ILogger<VectorSearchStrategyController> logger)
    : ControllerBase
{
    /// <summary>
    ///     Get information about the current vector search strategy
    /// </summary>
    [HttpGet("current")]
    public ActionResult<VectorSearchStrategyInfo> GetCurrentStrategy()
    {
        try
        {
            var info = vectorSearch.GetCurrentStrategyInfo();
            return Ok(info);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting current strategy info");
            return StatusCode(500, "Error getting strategy information");
        }
    }

    /// <summary>
    ///     Get all available vector search strategies and their performance characteristics
    /// </summary>
    [HttpGet("available")]
    public ActionResult<Dictionary<string, VectorSearchPerformance>> GetAvailableStrategies()
    {
        try
        {
            var strategies = vectorSearch.GetAvailableStrategies();
            return Ok(strategies);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting available strategies");
            return StatusCode(500, "Error getting available strategies");
        }
    }

    /// <summary>
    ///     Switch to a specific vector search strategy
    /// </summary>
    /// <param name="strategyName">Name of the strategy to switch to (MongoDB, InMemory, CUDA)</param>
    [HttpPost("switch/{strategyName}")]
    public async Task<ActionResult> SwitchStrategy(string strategyName)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(strategyName))
            {
                return BadRequest("Strategy name is required");
            }

            await vectorSearch.SwitchStrategyAsync(strategyName);

            var newInfo = vectorSearch.GetCurrentStrategyInfo();
            logger.LogInformation("Successfully switched to strategy: {StrategyName}", strategyName);

            return Ok(new
            {
                message = $"Successfully switched to {strategyName} strategy",
                strategy = newInfo
            });
        }
        catch (ArgumentException ex)
        {
            logger.LogWarning(ex, "Invalid strategy name: {StrategyName}", strategyName);
            return BadRequest($"Strategy '{strategyName}' not found");
        }
        catch (InvalidOperationException ex)
        {
            logger.LogWarning(ex, "Strategy not available: {StrategyName}", strategyName);
            return BadRequest($"Strategy '{strategyName}' is not available on this system");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error switching to strategy: {StrategyName}", strategyName);
            return StatusCode(500, "Error switching strategy");
        }
    }

    /// <summary>
    ///     Get performance statistics for all strategies
    /// </summary>
    [HttpGet("stats")]
    public ActionResult<Dictionary<string, VectorSearchStats>> GetPerformanceStats()
    {
        try
        {
            var stats = vectorSearch.GetPerformanceStats();
            return Ok(stats);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting performance stats");
            return StatusCode(500, "Error getting performance statistics");
        }
    }

    /// <summary>
    ///     Benchmark all available strategies
    /// </summary>
    /// <param name="iterations">Number of iterations for benchmarking (default: 10)</param>
    [HttpPost("benchmark")]
    public async Task<ActionResult<Dictionary<string, object>>> BenchmarkStrategies([FromQuery] int iterations = 10)
    {
        try
        {
            if (iterations is < 1 or > 100)
            {
                return BadRequest("Iterations must be between 1 and 100");
            }

            logger.LogInformation("Starting benchmark with {Iterations} iterations", iterations);

            var benchmarkResults = await vectorSearch.BenchmarkStrategiesAsync(iterations);
            var currentInfo = vectorSearch.GetCurrentStrategyInfo();

            var response = new
            {
                currentStrategy = currentInfo.Name,
                iterations,
                results = benchmarkResults.ToDictionary(
                    kvp => kvp.Key,
                    kvp => new
                    {
                        averageTimeMs = kvp.Value.TotalMilliseconds,
                        averageTimeTicks = kvp.Value.Ticks,
                        performance = kvp.Value.TotalMilliseconds < 10 ? "Excellent" :
                            kvp.Value.TotalMilliseconds < 50 ? "Good" :
                            kvp.Value.TotalMilliseconds < 100 ? "Fair" : "Slow"
                    }),
                fastest = benchmarkResults.OrderBy(kvp => kvp.Value).First().Key,
                recommendation = GetStrategyRecommendation(benchmarkResults)
            };

            return Ok(response);
        }
        catch (InvalidOperationException ex)
        {
            logger.LogWarning(ex, "Vector search not initialized for benchmarking");
            return BadRequest("Vector search service not initialized");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error during benchmarking");
            return StatusCode(500, "Error during benchmarking");
        }
    }

    /// <summary>
    ///     Get system requirements for each strategy
    /// </summary>
    [HttpGet("requirements")]
    public ActionResult<Dictionary<string, object>> GetStrategyRequirements()
    {
        try
        {
            var requirements = new Dictionary<string, object>
            {
                ["MongoDB"] = new
                {
                    description = "Database-backed vector search",
                    requirements = new[]
                    {
                        "MongoDB 8.0+ with vector search support",
                        "Network connectivity to MongoDB",
                        "Vector search index created",
                        "Chord embeddings stored in database"
                    },
                    pros = new[]
                    {
                        "Persistent storage",
                        "Scalable to millions of chords",
                        "Built-in filtering and aggregation",
                        "No memory limitations"
                    },
                    cons = new[]
                    {
                        "Network latency",
                        "Database overhead",
                        "Requires MongoDB setup"
                    },
                    estimatedSearchTime = "50-100ms",
                    memoryUsage = "Minimal (uses MongoDB memory)"
                },
                ["InMemory"] = new
                {
                    description = "High-performance in-memory vector search",
                    requirements = new[]
                    {
                        "Sufficient RAM (estimated 500MB for 427k chords)",
                        "SIMD-capable CPU (recommended)",
                        "Initial loading time for embeddings"
                    },
                    pros = new[]
                    {
                        "Very fast search (5-10ms)",
                        "No network dependency",
                        "SIMD optimization",
                        "Predictable performance"
                    },
                    cons = new[]
                    {
                        "High memory usage",
                        "Startup time for loading",
                        "Limited by available RAM"
                    },
                    estimatedSearchTime = "5-10ms",
                    memoryUsage = "~500MB for 427k chords"
                },
                ["CUDA"] = new
                {
                    description = "GPU-accelerated vector search",
                    requirements = new[]
                    {
                        "NVIDIA GPU with CUDA support",
                        "CUDA Toolkit 12.0+",
                        "Sufficient GPU memory (estimated 200MB)",
                        "Compatible GPU drivers"
                    },
                    pros = new[]
                    {
                        "Extremely fast search (1-3ms)",
                        "Massive parallelization",
                        "Optimized for large datasets",
                        "Best performance for batch operations"
                    },
                    cons = new[]
                    {
                        "Requires NVIDIA GPU",
                        "CUDA setup complexity",
                        "GPU memory limitations",
                        "Platform-specific"
                    },
                    estimatedSearchTime = "1-3ms",
                    memoryUsage = "~200MB GPU memory"
                }
            };

            return Ok(requirements);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting strategy requirements");
            return StatusCode(500, "Error getting strategy requirements");
        }
    }

    /// <summary>
    ///     Auto-select the best strategy based on system capabilities
    /// </summary>
    [HttpPost("auto-select")]
    public async Task<ActionResult> AutoSelectBestStrategy()
    {
        try
        {
            logger.LogInformation("Auto-selecting best vector search strategy");

            var availableStrategies = vectorSearch.GetAvailableStrategies();

            if (!availableStrategies.Any())
            {
                return BadRequest("No vector search strategies are available");
            }

            // Strategy selection logic:
            // 1. CUDA if available (fastest)
            // 2. InMemory if sufficient memory (fast + reliable)
            // 3. MongoDB as fallback (always works)

            string selectedStrategy;
            if (availableStrategies.ContainsKey("CUDA"))
            {
                selectedStrategy = "CUDA";
            }
            else if (availableStrategies.ContainsKey("InMemory"))
            {
                selectedStrategy = "InMemory";
            }
            else if (availableStrategies.ContainsKey("MongoDB"))
            {
                selectedStrategy = "MongoDB";
            }
            else
            {
                return BadRequest("No suitable strategy found");
            }

            await vectorSearch.SwitchStrategyAsync(selectedStrategy);

            var newInfo = vectorSearch.GetCurrentStrategyInfo();
            logger.LogInformation("Auto-selected strategy: {StrategyName}", selectedStrategy);

            return Ok(new
            {
                message = $"Auto-selected {selectedStrategy} strategy",
                strategy = newInfo,
                reason = GetSelectionReason(selectedStrategy)
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error during auto-selection");
            return StatusCode(500, "Error during auto-selection");
        }
    }

    private string GetStrategyRecommendation(Dictionary<string, TimeSpan> benchmarkResults)
    {
        if (!benchmarkResults.Any())
        {
            return "No strategies available";
        }

        var fastest = benchmarkResults.OrderBy(kvp => kvp.Value).First();
        var fastestTime = fastest.Value.TotalMilliseconds;

        if (fastestTime < 5)
        {
            return $"{fastest.Key} - Excellent performance for real-time applications";
        }

        if (fastestTime < 20)
        {
            return $"{fastest.Key} - Good performance for interactive applications";
        }

        if (fastestTime < 50)
        {
            return $"{fastest.Key} - Acceptable performance for most use cases";
        }

        return $"{fastest.Key} - Consider optimization or hardware upgrade";
    }

    private string GetSelectionReason(string strategy)
    {
        return strategy switch
        {
            "CUDA" => "GPU acceleration provides the fastest search performance",
            "InMemory" => "In-memory search offers excellent performance without GPU requirements",
            "MongoDB" => "Database-backed search provides reliable, scalable performance",
            _ => "Selected as the best available option"
        };
    }
}
