namespace GaApi.Controllers;

using System.Runtime.CompilerServices;
using GA.BSP.Core.Spatial;
using GA.Business.Core.Atonal;
using Microsoft.AspNetCore.RateLimiting;
using Models;
// using GA.Business.Core.Spatial // REMOVED - namespace does not exist;

/// <summary>
///     API controller for Binary Space Partitioning (BSP) musical analysis
/// </summary>
[ApiController]
[Route("api/bsp")]
[EnableRateLimiting("fixed")]
public class BspController(TonalBspService bspService, ILogger<BspController> logger) : ControllerBase
{
    /// <summary>
    ///     Get spatial query results for similar chords
    /// </summary>
    /// <param name="pitchClasses">Comma-separated pitch classes (e.g., "C,E,G")</param>
    /// <param name="radius">Search radius (default: 0.5)</param>
    /// <param name="strategy">Partition strategy (default: CircleOfFifths)</param>
    /// <returns>Spatial query results</returns>
    [HttpGet("spatial-query")]
    [ProducesResponseType(typeof(ApiResponse<BspSpatialQueryResponse>), 200)]
    [ProducesResponseType(typeof(ApiResponse<object>), 400)]
    [ProducesResponseType(typeof(ApiResponse<object>), 500)]
    public IActionResult SpatialQuery(
        [FromQuery] string pitchClasses,
        [FromQuery] double radius = 0.5,
        [FromQuery] TonalPartitionStrategy strategy = TonalPartitionStrategy.CircleOfFifths)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(pitchClasses))
            {
                return BadRequest(ApiResponse<object>.Fail("pitchClasses parameter is required"));
            }

            var pitchClassSet = ParsePitchClasses(pitchClasses);
            if (pitchClassSet == null)
            {
                return BadRequest(
                    ApiResponse<object>.Fail("Invalid pitch classes format. Use comma-separated values like 'C,E,G'"));
            }

            var result = bspService.SpatialQuery(pitchClassSet, radius, strategy);

            var response = new BspSpatialQueryResponse
            {
                QueryChord = pitchClasses,
                Radius = radius,
                Strategy = strategy.ToString(),
                Region = new BspRegionDto
                {
                    Name = result.Region.Name,
                    TonalityType = result.Region.TonalityType.ToString(),
                    TonalCenter = (int)result.Region.TonalCenter,
                    PitchClasses = result.Region.PitchClassSet.Select(pc => pc.ToString()).ToList()
                },
                Elements = result.Elements.Select(e => new BspElementDto
                {
                    Name = e.Name,
                    TonalityType = e.TonalityType.ToString(),
                    TonalCenter = (int)e.TonalCenter,
                    PitchClasses = e.PitchClassSet.Select(pc => pc.ToString()).ToList()
                }).ToList(),
                Confidence = result.Confidence,
                QueryTimeMs = result.QueryTime.TotalMilliseconds
            };

            return Ok(ApiResponse<BspSpatialQueryResponse>.Ok(response));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error performing BSP spatial query");
            return StatusCode(500, ApiResponse<object>.Fail("Internal server error", ex.Message));
        }
    }

    /// <summary>
    ///     Get tonal context for a chord
    /// </summary>
    /// <param name="pitchClasses">Comma-separated pitch classes (e.g., "C,E,G")</param>
    /// <returns>Tonal context analysis</returns>
    [HttpGet("tonal-context")]
    [ProducesResponseType(typeof(ApiResponse<BspTonalContextResponse>), 200)]
    [ProducesResponseType(typeof(ApiResponse<object>), 400)]
    [ProducesResponseType(typeof(ApiResponse<object>), 500)]
    public IActionResult GetTonalContext([FromQuery] string pitchClasses)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(pitchClasses))
            {
                return BadRequest(ApiResponse<object>.Fail("pitchClasses parameter is required"));
            }

            var pitchClassSet = ParsePitchClasses(pitchClasses);
            if (pitchClassSet == null)
            {
                return BadRequest(ApiResponse<object>.Fail("Invalid pitch classes format"));
            }

            var result = bspService.FindTonalContextForChord(pitchClassSet);

            var response = new BspTonalContextResponse
            {
                QueryChord = pitchClasses,
                Region = new BspRegionDto
                {
                    Name = result.Region.Name,
                    TonalityType = result.Region.TonalityType.ToString(),
                    TonalCenter = (int)result.Region.TonalCenter,
                    PitchClasses = result.Region.PitchClassSet.Select(pc => pc.ToString()).ToList()
                },
                Confidence = result.Confidence,
                QueryTimeMs = result.QueryTime.TotalMilliseconds,
                Analysis = new BspAnalysisDto
                {
                    ContainedInRegion = result.Region.Contains(pitchClassSet),
                    CommonTones = result.Region.PitchClassSet.Intersect(pitchClassSet).Count(),
                    TotalTones = pitchClassSet.Count,
                    FitPercentage = (double)result.Region.PitchClassSet.Intersect(pitchClassSet).Count() /
                        pitchClassSet.Count * 100
                }
            };

            return Ok(ApiResponse<BspTonalContextResponse>.Ok(response));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting tonal context");
            return StatusCode(500, ApiResponse<object>.Fail("Internal server error", ex.Message));
        }
    }

    /// <summary>
    ///     Analyze a chord progression using BSP
    /// </summary>
    /// <param name="request">Progression analysis request</param>
    /// <returns>Progression analysis results</returns>
    [HttpPost("analyze-progression")]
    [ProducesResponseType(typeof(ApiResponse<BspProgressionAnalysisResponse>), 200)]
    [ProducesResponseType(typeof(ApiResponse<object>), 400)]
    [ProducesResponseType(typeof(ApiResponse<object>), 500)]
    public IActionResult AnalyzeProgression([FromBody] BspProgressionRequest request)
    {
        try
        {
            if (request?.Chords == null || !request.Chords.Any())
            {
                return BadRequest(ApiResponse<object>.Fail("Chords array is required"));
            }

            var chordAnalyses = new List<BspChordAnalysisDto>();
            var transitions = new List<BspTransitionDto>();

            // Analyze each chord
            for (var i = 0; i < request.Chords.Count; i++)
            {
                var chord = request.Chords[i];
                var pitchClassSet = ParsePitchClasses(chord.PitchClasses);

                if (pitchClassSet == null)
                {
                    return BadRequest(
                        ApiResponse<object>.Fail($"Invalid pitch classes in chord {i + 1}: {chord.PitchClasses}"));
                }

                var context = bspService.FindTonalContextForChord(pitchClassSet);

                chordAnalyses.Add(new BspChordAnalysisDto
                {
                    Name = chord.Name,
                    PitchClasses = chord.PitchClasses,
                    Region = new BspRegionDto
                    {
                        Name = context.Region.Name,
                        TonalityType = context.Region.TonalityType.ToString(),
                        TonalCenter = (int)context.Region.TonalCenter,
                        PitchClasses = context.Region.PitchClassSet.Select(pc => pc.ToString()).ToList()
                    },
                    Confidence = context.Confidence,
                    QueryTimeMs = context.QueryTime.TotalMilliseconds
                });

                // Calculate transition to next chord
                if (i < request.Chords.Count - 1)
                {
                    var nextChord = request.Chords[i + 1];
                    var nextPitchClassSet = ParsePitchClasses(nextChord.PitchClasses);

                    if (nextPitchClassSet != null)
                    {
                        var distance = CalculateSpatialDistance(pitchClassSet, nextPitchClassSet);
                        var commonTones = pitchClassSet.Intersect(nextPitchClassSet).Count();

                        transitions.Add(new BspTransitionDto
                        {
                            FromChord = chord.Name,
                            ToChord = nextChord.Name,
                            Distance = distance,
                            CommonTones = commonTones,
                            Smoothness = 1.0 - distance // Higher smoothness = lower distance
                        });
                    }
                }
            }

            var response = new BspProgressionAnalysisResponse
            {
                Progression = request.Chords.Select(c => c.Name).ToList(),
                ChordAnalyses = chordAnalyses,
                Transitions = transitions,
                OverallAnalysis = new BspOverallAnalysisDto
                {
                    AverageConfidence = chordAnalyses.Average(c => c.Confidence),
                    AverageDistance = transitions.Any() ? transitions.Average(t => t.Distance) : 0,
                    AverageSmoothness = transitions.Any() ? transitions.Average(t => t.Smoothness) : 1,
                    TotalCommonTones = transitions.Sum(t => t.CommonTones),
                    ProgressionLength = request.Chords.Count
                }
            };

            return Ok(ApiResponse<BspProgressionAnalysisResponse>.Ok(response));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error analyzing progression");
            return StatusCode(500, ApiResponse<object>.Fail("Internal server error", ex.Message));
        }
    }

    /// <summary>
    ///     Get BSP tree information
    /// </summary>
    /// <returns>BSP tree structure information</returns>
    [HttpGet("tree-info")]
    [ProducesResponseType(typeof(ApiResponse<BspTreeInfoResponse>), 200)]
    public IActionResult GetTreeInfo()
    {
        try
        {
            // This would need to be implemented in the BSP service
            var response = new BspTreeInfoResponse
            {
                RootRegion = "Chromatic Space",
                TotalRegions = 3, // Root + Major + Minor
                MaxDepth = 2,
                PartitionStrategies = Enum.GetNames<TonalPartitionStrategy>().ToList(),
                SupportedOperations =
                [
                    "Spatial Query",
                    "Tonal Context Analysis",
                    "Progression Analysis",
                    "Similarity Search"
                ]
            };

            return Ok(ApiResponse<BspTreeInfoResponse>.Ok(response));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting tree info");
            return StatusCode(500, ApiResponse<object>.Fail("Internal server error", ex.Message));
        }
    }

    /// <summary>
    ///     Get full BSP tree structure for visualization
    /// </summary>
    /// <returns>Complete BSP tree structure with all nodes and partitions</returns>
    [HttpGet("tree-structure")]
    [ProducesResponseType(typeof(ApiResponse<BspTreeStructureResponse>), 200)]
    public IActionResult GetTreeStructure()
    {
        try
        {
            var bspTree = new TonalBspTree();
            var response = BuildTreeStructure(bspTree.Root);

            return Ok(ApiResponse<BspTreeStructureResponse>.Ok(response));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting tree structure");
            return StatusCode(500, ApiResponse<object>.Fail("Internal server error", ex.Message));
        }
    }

    /// <summary>
    ///     Stream BSP tree structure for progressive rendering (memory-efficient)
    /// </summary>
    /// <param name="maxDepth">Maximum depth to traverse (default: 10)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Stream of BSP nodes</returns>
    [HttpGet("tree-structure/stream")]
    [ProducesResponseType(typeof(IAsyncEnumerable<BspNodeDto>), StatusCodes.Status200OK)]
    public async IAsyncEnumerable<BspNodeDto> GetTreeStructureStream(
        [FromQuery] int maxDepth = 10,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        logger.LogInformation("Streaming BSP tree structure with maxDepth: {MaxDepth}", maxDepth);

        var bspTree = new TonalBspTree();
        var nodeCount = 0;

        // Depth-first traversal using a stack
        var stack = new Stack<(TonalBspNode node, int depth)>();
        stack.Push((bspTree.Root, 0));

        while (stack.Count > 0)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                logger.LogInformation("BSP tree streaming cancelled after {NodeCount} nodes", nodeCount);
                yield break;
            }

            var (node, depth) = stack.Pop();

            // Skip nodes beyond max depth
            if (depth > maxDepth)
            {
                continue;
            }

            nodeCount++;

            // Convert and yield current node
            var nodeDto = new BspNodeDto
            {
                Region = new BspRegionDto
                {
                    Name = node.Region.Name,
                    TonalityType = node.Region.TonalityType.ToString(),
                    TonalCenter = (int)node.Region.TonalCenter,
                    PitchClasses = node.Region.PitchClassSet.Select(pc => pc.ToString()).ToList()
                },
                IsLeaf = node.IsLeaf,
                Depth = depth
            };

            if (node.IsLeaf)
            {
                nodeDto.Elements = node.Elements.Select(e => new BspElementDto
                {
                    Name = e.Name,
                    TonalityType = e.TonalityType.ToString(),
                    TonalCenter = (int)e.TonalCenter,
                    PitchClasses = e.PitchClassSet.Select(pc => pc.ToString()).ToList()
                }).ToList();
            }
            else if (node.PartitionPlane != null)
            {
                nodeDto.Partition = new BspPartitionDto
                {
                    Strategy = node.PartitionPlane.Strategy.ToString(),
                    ReferencePoint = node.PartitionPlane.ReferencePoint,
                    Threshold = node.PartitionPlane.Threshold,
                    Normal = [node.PartitionPlane.Normal.X, node.PartitionPlane.Normal.Y, node.PartitionPlane.Normal.Z]
                };

                // Add children to stack (right first, then left for depth-first left-to-right traversal)
                if (node.Right != null && depth < maxDepth)
                {
                    stack.Push((node.Right, depth + 1));
                }

                if (node.Left != null && depth < maxDepth)
                {
                    stack.Push((node.Left, depth + 1));
                }
            }

            yield return nodeDto;

            // Log progress every 100 nodes
            if (nodeCount % 100 == 0)
            {
                logger.LogDebug("Streamed {NodeCount} BSP nodes so far", nodeCount);
            }

            // Backpressure control
            await Task.Delay(1, cancellationToken);
        }

        logger.LogInformation("Completed streaming {NodeCount} BSP nodes", nodeCount);
    }

    private BspTreeStructureResponse BuildTreeStructure(TonalBspNode root)
    {
        var nodeCount = 0;
        var maxDepth = 0;
        var regionCount = 0;
        var partitionCount = 0;

        var rootDto = ConvertNodeToDto(root, 0, ref nodeCount, ref maxDepth, ref regionCount, ref partitionCount);

        return new BspTreeStructureResponse
        {
            Root = rootDto,
            NodeCount = nodeCount,
            MaxDepth = maxDepth,
            RegionCount = regionCount,
            PartitionCount = partitionCount
        };
    }

    private BspNodeDto ConvertNodeToDto(TonalBspNode node, int depth, ref int nodeCount, ref int maxDepth,
        ref int regionCount, ref int partitionCount)
    {
        nodeCount++;
        maxDepth = Math.Max(maxDepth, depth);

        var nodeDto = new BspNodeDto
        {
            Region = new BspRegionDto
            {
                Name = node.Region.Name,
                TonalityType = node.Region.TonalityType.ToString(),
                TonalCenter = (int)node.Region.TonalCenter,
                PitchClasses = node.Region.PitchClassSet.Select(pc => pc.ToString()).ToList()
            },
            IsLeaf = node.IsLeaf,
            Depth = depth
        };

        if (node.IsLeaf)
        {
            regionCount++;
            nodeDto.Elements = node.Elements.Select(e => new BspElementDto
            {
                Name = e.Name,
                TonalityType = e.TonalityType.ToString(),
                TonalCenter = (int)e.TonalCenter,
                PitchClasses = e.PitchClassSet.Select(pc => pc.ToString()).ToList()
            }).ToList();
        }
        else
        {
            partitionCount++;
            if (node.PartitionPlane != null)
            {
                nodeDto.Partition = new BspPartitionDto
                {
                    Strategy = node.PartitionPlane.Strategy.ToString(),
                    ReferencePoint = node.PartitionPlane.ReferencePoint,
                    Threshold = node.PartitionPlane.Threshold,
                    Normal = [node.PartitionPlane.Normal.X, node.PartitionPlane.Normal.Y, node.PartitionPlane.Normal.Z]
                };
            }

            if (node.Left != null)
            {
                nodeDto.Left = ConvertNodeToDto(node.Left, depth + 1, ref nodeCount, ref maxDepth, ref regionCount,
                    ref partitionCount);
            }

            if (node.Right != null)
            {
                nodeDto.Right = ConvertNodeToDto(node.Right, depth + 1, ref nodeCount, ref maxDepth, ref regionCount,
                    ref partitionCount);
            }
        }

        return nodeDto;
    }

    private PitchClassSet? ParsePitchClasses(string pitchClassesStr)
    {
        try
        {
            var pitchClassNames = pitchClassesStr.Split(',', StringSplitOptions.RemoveEmptyEntries)
                .Select(s => s.Trim())
                .ToArray();

            var pitchClasses = new List<PitchClass>();

            foreach (var name in pitchClassNames)
            {
                if (Enum.TryParse<PitchClass>(name, true, out var pitchClass))
                {
                    pitchClasses.Add(pitchClass);
                }
                else
                {
                    return null;
                }
            }

            return new PitchClassSet(pitchClasses);
        }
        catch
        {
            return null;
        }
    }

    private double CalculateSpatialDistance(PitchClassSet set1, PitchClassSet set2)
    {
        var intersection = set1.Intersect(set2).Count();
        var union = set1.Union(set2).Count();
        return union > 0 ? 1.0 - (double)intersection / union : 1.0;
    }
}
