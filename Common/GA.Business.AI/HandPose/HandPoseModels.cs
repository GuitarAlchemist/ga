namespace GA.Business.AI.AI.HandPose;

/// <summary>
///     3D keypoint with name and coordinates
/// </summary>
public record Keypoint(
    string Name,
    double X,
    double Y,
    double Z
);

/// <summary>
///     Detected hand with keypoints
/// </summary>
public record Hand(
    int Id,
    string Side,
    List<Keypoint> Keypoints,
    double Confidence
);

/// <summary>
///     Response from hand pose inference
/// </summary>
public record HandPoseResponse(
    List<Hand> Hands,
    int ImageWidth,
    int ImageHeight,
    double ProcessingTimeMs
);

/// <summary>
///     Guitar string/fret position
/// </summary>
public record GuitarPosition(
    int String,
    int Fret,
    string? Finger,
    double Confidence
);

/// <summary>
///     Guitar neck configuration for mapping
/// </summary>
public record NeckConfig(
    double ScaleLengthMm = 648.0,
    int NumFrets = 22,
    double StringSpacingMm = 10.5,
    double NutWidthMm = 42.0
);

/// <summary>
///     Request to map hand pose to guitar positions
/// </summary>
public record GuitarMappingRequest(
    HandPoseResponse HandPose,
    NeckConfig NeckConfig,
    string HandToMap
);

/// <summary>
///     Response with guitar string/fret positions
/// </summary>
public record GuitarMappingResponse(
    List<GuitarPosition> Positions,
    string HandSide,
    string MappingMethod
);
