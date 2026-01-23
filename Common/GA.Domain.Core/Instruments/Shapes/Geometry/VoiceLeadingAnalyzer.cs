namespace GA.Domain.Core.Instruments.Shapes.Geometry;

/// <summary>
///     Voice leading analyzer using differential geometry
/// </summary>
[PublicAPI]
public class VoiceLeadingAnalyzer(int voices)
{
    private readonly VoiceLeadingSpace _space = new(voices);

    /// <summary>
    ///     Analyze voice leading between two shapes
    /// </summary>
    public VoiceLeadingInfo Analyze(FretboardShape from, FretboardShape to)
    {
        // Extract voicings (pitch classes of sounding notes)
        var fromVoicing = ExtractVoicing(from);
        var toVoicing = ExtractVoicing(to);

        var distance = _space.Distance(fromVoicing, toVoicing);
        var geodesic = _space.Geodesic(fromVoicing, toVoicing, 5);
        var curvature = _space.Curvature(fromVoicing);

        return new()
        {
            FromShape = from.Id,
            ToShape = to.Id,
            Distance = distance,
            Curvature = curvature,
            GeodesicLength = geodesic.Count,
            IsSmooth = distance < 5.0 // Arbitrary threshold
        };
    }

    /// <summary>
    ///     Extract voicing from fretboard shape
    /// </summary>
    private double[] ExtractVoicing(FretboardShape shape)
    {
        // Get pitch classes from the pitch class set
        var pitches = shape.PitchClassSet
            .Select(pc => (double)pc.Value)
            .OrderBy(p => p)
            .ToArray();

        return pitches;
    }
}