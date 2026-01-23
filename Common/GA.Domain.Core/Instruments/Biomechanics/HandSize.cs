namespace GA.Domain.Core.Instruments.Biomechanics;

/// <summary>
///     Hand size categories for personalized biomechanical analysis
/// </summary>
public enum HandSize
{
    /// <summary>
    ///     Small hands (children, small adults) - 85% of standard
    ///     Typical hand span: 7-8 inches (18-20 cm)
    /// </summary>
    Small,

    /// <summary>
    ///     Medium hands (average adult) - 100% standard
    ///     Typical hand span: 8-9 inches (20-23 cm)
    /// </summary>
    Medium,

    /// <summary>
    ///     Large hands (large adult) - 115% of standard
    ///     Typical hand span: 9-10 inches (23-25 cm)
    /// </summary>
    Large,

    /// <summary>
    ///     Extra large hands (very large hands) - 130% of standard
    ///     Typical hand span: 10+ inches (25+ cm)
    /// </summary>
    ExtraLarge
}