namespace GaApi.Controllers;

using System.Collections.Immutable;
using GA.Business.Core.Fretboard.Biomechanics;
using GA.Business.Core.Fretboard.Positions;
using GA.Business.Core.Fretboard.Primitives;
using GA.Business.Core.Notes.Primitives;
using Microsoft.AspNetCore.RateLimiting;
using Models;

/// <summary>
///     API controller for biomechanical analysis of guitar chord fingerings
/// </summary>
[ApiController]
[Route("api/biomechanics")]
[EnableRateLimiting("fixed")]
public class BiomechanicsController(ILogger<BiomechanicsController> logger) : ControllerBase
{
    private readonly BiomechanicalAnalyzer _analyzer = new();

    /// <summary>
    ///     Analyze the biomechanical playability of a chord fingering
    /// </summary>
    /// <param name="request">Chord analysis request</param>
    /// <returns>Biomechanical playability analysis</returns>
    /// <response code="200">Returns the biomechanical analysis</response>
    /// <response code="400">If the request is invalid</response>
    /// <response code="500">If an error occurs during analysis</response>
    [HttpPost("analyze-chord")]
    [ProducesResponseType(typeof(ApiResponse<BiomechanicalAnalysisResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status500InternalServerError)]
    public ActionResult<ApiResponse<BiomechanicalAnalysisResponse>> AnalyzeChord(
        [FromBody] ChordAnalysisRequest request)
    {
        try
        {
            if (request == null || request.FingerAssignments == null || !request.FingerAssignments.Any())
            {
                return BadRequest(ApiResponse<object>.Fail("Finger assignments are required"));
            }

            logger.LogInformation("Analyzing chord with {Count} finger assignments", request.FingerAssignments.Count);

            // Convert request to internal format
            var positions = request.FingerAssignments.Select(fa =>
                (Position)CreatePlayedPosition(fa.String, fa.Fret)).ToImmutableList();

            // Perform analysis
            var analysis = _analyzer.AnalyzeChordPlayability(positions);

            // Convert to response format
            var response = new BiomechanicalAnalysisResponse
            {
                PlayabilityScore = analysis.OverallScore,
                FingerStretch = analysis.StretchAnalysis != null
                    ? new FingerStretchDto
                    {
                        MaxStretch = analysis.StretchAnalysis.MaxStretchDistance,
                        MaxFretSpan = analysis.StretchAnalysis.MaxFretSpan,
                        Description = analysis.StretchAnalysis.StretchDescription
                    }
                    : null,
                FingeringEfficiency = analysis.FingeringEfficiencyAnalysis != null
                    ? new FingeringEfficiencyDto
                    {
                        EfficiencyScore = analysis.FingeringEfficiencyAnalysis.EfficiencyScore,
                        FingerSpan = analysis.FingeringEfficiencyAnalysis.FingerSpan,
                        PinkyUsagePercentage = analysis.FingeringEfficiencyAnalysis.PinkyUsagePercentage,
                        HasBarreChord = analysis.FingeringEfficiencyAnalysis.HasBarreChord,
                        UsesThumb = analysis.FingeringEfficiencyAnalysis.UsesThumb,
                        Reason = analysis.FingeringEfficiencyAnalysis.Reason,
                        Recommendations = analysis.FingeringEfficiencyAnalysis.Recommendations.ToList()
                    }
                    : null,
                WristPosture = analysis.WristPostureAnalysis != null
                    ? new WristPostureDto
                    {
                        Angle = analysis.WristPostureAnalysis.WristAngleDegrees,
                        PostureType = analysis.WristPostureAnalysis.PostureType.ToString(),
                        IsErgonomic = analysis.WristPostureAnalysis.IsErgonomic
                    }
                    : null,
                Muting = analysis.MutingAnalysis != null
                    ? new MutingDto
                    {
                        Technique = analysis.MutingAnalysis.Technique.ToString(),
                        Reason = analysis.MutingAnalysis.Reason
                    }
                    : null,
                SlideLegato = analysis.SlideLegatoAnalysis != null
                    ? new SlideLegatoDto
                    {
                        Technique = analysis.SlideLegatoAnalysis.Technique.ToString(),
                        Reason = analysis.SlideLegatoAnalysis.Reason
                    }
                    : null,
                Visualization = CreateVisualizationDto(analysis)
            };

            return Ok(ApiResponse<BiomechanicalAnalysisResponse>.Ok(response));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error analyzing chord");
            return StatusCode(500, ApiResponse<object>.Fail($"Error analyzing chord: {ex.Message}"));
        }
    }

    /// <summary>
    ///     Analyze the biomechanical playability of a chord progression
    /// </summary>
    /// <param name="request">Progression analysis request</param>
    /// <returns>Biomechanical playability analysis for each chord in the progression</returns>
    /// <response code="200">Returns the biomechanical analysis for the progression</response>
    /// <response code="400">If the request is invalid</response>
    /// <response code="500">If an error occurs during analysis</response>
    [HttpPost("analyze-progression")]
    [ProducesResponseType(typeof(ApiResponse<ProgressionAnalysisResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status500InternalServerError)]
    public ActionResult<ApiResponse<ProgressionAnalysisResponse>> AnalyzeProgression(
        [FromBody] ProgressionAnalysisRequest request)
    {
        try
        {
            if (request == null || request.Chords == null || !request.Chords.Any())
            {
                return BadRequest(ApiResponse<object>.Fail("Chords are required"));
            }

            logger.LogInformation("Analyzing progression with {Count} chords", request.Chords.Count);

            var chordAnalyses = new List<BiomechanicalAnalysisResponse>();

            foreach (var chord in request.Chords)
            {
                if (chord.FingerAssignments == null || !chord.FingerAssignments.Any())
                {
                    continue;
                }

                // Convert request to internal format
                var positions = chord.FingerAssignments.Select(fa =>
                    (Position)CreatePlayedPosition(fa.String, fa.Fret)).ToImmutableList();

                // Perform analysis
                var analysis = _analyzer.AnalyzeChordPlayability(positions);

                // Convert to response format
                var response = new BiomechanicalAnalysisResponse
                {
                    PlayabilityScore = analysis.OverallScore,
                    FingerStretch = analysis.StretchAnalysis != null
                        ? new FingerStretchDto
                        {
                            MaxStretch = analysis.StretchAnalysis.MaxStretchDistance,
                            MaxFretSpan = analysis.StretchAnalysis.MaxFretSpan,
                            Description = analysis.StretchAnalysis.StretchDescription
                        }
                        : null,
                    FingeringEfficiency = analysis.FingeringEfficiencyAnalysis != null
                        ? new FingeringEfficiencyDto
                        {
                            EfficiencyScore = analysis.FingeringEfficiencyAnalysis.EfficiencyScore,
                            FingerSpan = analysis.FingeringEfficiencyAnalysis.FingerSpan,
                            PinkyUsagePercentage = analysis.FingeringEfficiencyAnalysis.PinkyUsagePercentage,
                            HasBarreChord = analysis.FingeringEfficiencyAnalysis.HasBarreChord,
                            UsesThumb = analysis.FingeringEfficiencyAnalysis.UsesThumb,
                            Reason = analysis.FingeringEfficiencyAnalysis.Reason,
                            Recommendations = analysis.FingeringEfficiencyAnalysis.Recommendations.ToList()
                        }
                        : null,
                    WristPosture = analysis.WristPostureAnalysis != null
                        ? new WristPostureDto
                        {
                            Angle = analysis.WristPostureAnalysis.WristAngleDegrees,
                            PostureType = analysis.WristPostureAnalysis.PostureType.ToString(),
                            IsErgonomic = analysis.WristPostureAnalysis.IsErgonomic
                        }
                        : null,
                    Muting = analysis.MutingAnalysis != null
                        ? new MutingDto
                        {
                            Technique = analysis.MutingAnalysis.Technique.ToString(),
                            Reason = analysis.MutingAnalysis.Reason
                        }
                        : null,
                    SlideLegato = analysis.SlideLegatoAnalysis != null
                        ? new SlideLegatoDto
                        {
                            Technique = analysis.SlideLegatoAnalysis.Technique.ToString(),
                            Reason = analysis.SlideLegatoAnalysis.Reason
                        }
                        : null
                };

                chordAnalyses.Add(response);
            }

            var progressionResponse = new ProgressionAnalysisResponse
            {
                Chords = chordAnalyses,
                AveragePlayability = chordAnalyses.Any() ? chordAnalyses.Average(c => c.PlayabilityScore) : 0.0
            };

            return Ok(ApiResponse<ProgressionAnalysisResponse>.Ok(progressionResponse));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error analyzing progression");
            return StatusCode(500, ApiResponse<object>.Fail($"Error analyzing progression: {ex.Message}"));
        }
    }

    private Position.Played CreatePlayedPosition(int stringNumber, int fret)
    {
        var str = Str.FromValue(stringNumber);
        var fretValue = Fret.FromValue(fret);
        var location = new PositionLocation(str, fretValue);
        // Use a dummy MIDI note (60 = middle C) since we're only analyzing positions
        var midiNote = MidiNote.FromValue(60 + fret);
        return new Position.Played(location, midiNote);
    }

    private HandPoseVisualizationDto? CreateVisualizationDto(BiomechanicalPlayabilityAnalysis analysis)
    {
        if (analysis.BestPose == null)
        {
            return null;
        }

        // Compute forward kinematics with visualization data
        var result = ForwardKinematics.ComputeFingertipPositions(analysis.BestPose);

        var fingertips = new Dictionary<string, FingertipVisualizationDto>();
        foreach (var (fingerType, fingertip) in result.Fingertips)
        {
            fingertips[fingerType.ToString()] = new FingertipVisualizationDto
            {
                Position = new Vector3Dto
                    { X = fingertip.Position.X, Y = fingertip.Position.Y, Z = fingertip.Position.Z },
                Direction = new Vector3Dto
                    { X = fingertip.Direction.X, Y = fingertip.Direction.Y, Z = fingertip.Direction.Z },
                JointPositions = fingertip.JointPositions.Select(p => new Vector3Dto { X = p.X, Y = p.Y, Z = p.Z })
                    .ToList(),
                ArcTrajectory = fingertip.ArcTrajectory.Select(p => new Vector3Dto { X = p.X, Y = p.Y, Z = p.Z })
                    .ToList(),
                JointFlexionAngles = fingertip.JointFlexionAngles.ToList(),
                JointAbductionAngles = fingertip.JointAbductionAngles.ToList()
            };
        }

        return new HandPoseVisualizationDto
        {
            Fingertips = fingertips,
            WristPosition = new Vector3Dto
                { X = result.WristPosition.X, Y = result.WristPosition.Y, Z = result.WristPosition.Z },
            PalmOrientation = new QuaternionDto
            {
                X = result.PalmOrientation.X,
                Y = result.PalmOrientation.Y,
                Z = result.PalmOrientation.Z,
                W = result.PalmOrientation.W
            },
            FretboardGeometry = result.FretboardGeometry != null
                ? new FretboardGeometryDto
                {
                    ThicknessMm = result.FretboardGeometry.ThicknessMm,
                    NeckThicknessAtNut = result.FretboardGeometry.GetNeckThicknessAtFret(0),
                    NeckThicknessAt12thFret = result.FretboardGeometry.GetNeckThicknessAtFret(12),
                    StringHeightAtNut = result.FretboardGeometry.StringHeightAtNut,
                    StringHeightAt12th = result.FretboardGeometry.StringHeightAt12th
                }
                : null
        };
    }
}

// Request/Response DTOs
public record ChordAnalysisRequest(
    List<FingerAssignmentDto> FingerAssignments,
    HandSize HandSize = HandSize.Medium);

public record FingerAssignmentDto(
    int String,
    int Fret,
    FingerType Finger);

public record ProgressionAnalysisRequest(
    List<ChordAnalysisRequest> Chords,
    HandSize HandSize = HandSize.Medium);

public record BiomechanicalAnalysisResponse
{
    public double PlayabilityScore { get; init; }
    public FingerStretchDto? FingerStretch { get; init; }
    public FingeringEfficiencyDto? FingeringEfficiency { get; init; }
    public WristPostureDto? WristPosture { get; init; }
    public MutingDto? Muting { get; init; }
    public SlideLegatoDto? SlideLegato { get; init; }
    public HandPoseVisualizationDto? Visualization { get; init; }
}

public record FingerStretchDto
{
    public double MaxStretch { get; init; }
    public int MaxFretSpan { get; init; }
    public string Description { get; init; } = string.Empty;
}

public record FingeringEfficiencyDto
{
    public double EfficiencyScore { get; init; }
    public int FingerSpan { get; init; }
    public double PinkyUsagePercentage { get; init; }
    public bool HasBarreChord { get; init; }
    public bool UsesThumb { get; init; }
    public string Reason { get; init; } = string.Empty;
    public List<string> Recommendations { get; init; } = [];
}

public record WristPostureDto
{
    public double Angle { get; init; }
    public string PostureType { get; init; } = string.Empty;
    public bool IsErgonomic { get; init; }
}

public record MutingDto
{
    public string Technique { get; init; } = string.Empty;
    public string Reason { get; init; } = string.Empty;
}

public record SlideLegatoDto
{
    public string Technique { get; init; } = string.Empty;
    public string Reason { get; init; } = string.Empty;
}

public record ProgressionAnalysisResponse
{
    public List<BiomechanicalAnalysisResponse> Chords { get; init; } = [];
    public double AveragePlayability { get; init; }
}

public record HandPoseVisualizationDto
{
    public Dictionary<string, FingertipVisualizationDto> Fingertips { get; init; } = [];
    public Vector3Dto WristPosition { get; init; } = new();
    public QuaternionDto PalmOrientation { get; init; } = new();
    public FretboardGeometryDto? FretboardGeometry { get; init; }
}

public record FingertipVisualizationDto
{
    public Vector3Dto Position { get; init; } = new();
    public Vector3Dto Direction { get; init; } = new();
    public List<Vector3Dto> JointPositions { get; init; } = [];
    public List<Vector3Dto> ArcTrajectory { get; init; } = [];
    public List<float> JointFlexionAngles { get; init; } = [];
    public List<float> JointAbductionAngles { get; init; } = [];
}

public record Vector3Dto
{
    public float X { get; init; }
    public float Y { get; init; }
    public float Z { get; init; }
}

public record QuaternionDto
{
    public float X { get; init; }
    public float Y { get; init; }
    public float Z { get; init; }
    public float W { get; init; }
}

public record FretboardGeometryDto
{
    public float ThicknessMm { get; init; }
    public float NeckThicknessAtNut { get; init; }
    public float NeckThicknessAt12thFret { get; init; }
    public float StringHeightAtNut { get; init; }
    public float StringHeightAt12th { get; init; }
}
