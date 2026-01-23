namespace GA.Domain.Core.Instruments.Shapes.Geometry;

/// <summary>
///     Information about voice leading between two shapes
/// </summary>
[PublicAPI]
public sealed record VoiceLeadingInfo
{
    public required string FromShape { get; init; }
    public required string ToShape { get; init; }
    public required double Distance { get; init; }
    public required double Curvature { get; init; }
    public required int GeodesicLength { get; init; }
    public required bool IsSmooth { get; init; }

    public override string ToString()
    {
        return $"VoiceLeading[{FromShape} ? {ToShape}, d={Distance:F2}, " +
               $"?={Curvature:F2}, smooth={IsSmooth}]";
    }
}