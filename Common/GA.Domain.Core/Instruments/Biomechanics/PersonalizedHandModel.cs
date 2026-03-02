namespace GA.Domain.Core.Instruments.Biomechanics;

/// <summary>
///     Factory for creating personalized hand models based on hand size
/// </summary>
public static class PersonalizedHandModel
{
    /// <summary>
    ///     Create a hand model scaled for the specified hand size
    /// </summary>
    public static HandModel Create(HandSize size)
    {
        var baseModel = HandModel.CreateStandardAdult();

        return size switch
        {
            HandSize.Small => ScaleHandModel(baseModel, 0.85f, "Small Adult Hand"),
            HandSize.Medium => baseModel,
            HandSize.Large => ScaleHandModel(baseModel, 1.15f, "Large Adult Hand"),
            HandSize.ExtraLarge => ScaleHandModel(baseModel, 1.30f, "Extra Large Adult Hand"),
            _ => baseModel
        };
    }

    /// <summary>
    ///     Get the scale factor for a hand size
    /// </summary>
    public static float GetScaleFactor(HandSize size) => size switch
    {
        HandSize.Small => 0.85f,
        HandSize.Medium => 1.00f,
        HandSize.Large => 1.15f,
        HandSize.ExtraLarge => 1.30f,
        _ => 1.00f
    };

    /// <summary>
    ///     Get typical hand span in millimeters for a hand size
    /// </summary>
    public static float GetTypicalHandSpanMm(HandSize size) => size switch
    {
        HandSize.Small => 190.0f, // ~7.5 inches
        HandSize.Medium => 215.0f, // ~8.5 inches
        HandSize.Large => 240.0f, // ~9.5 inches
        HandSize.ExtraLarge => 265.0f, // ~10.5 inches
        _ => 215.0f
    };

    /// <summary>
    ///     Scale a hand model by the specified factor
    /// </summary>
    private static HandModel ScaleHandModel(HandModel baseModel, float scaleFactor, string name)
    {
        var scaledFingers = baseModel.Fingers.Select(finger => new Finger
        {
            Type = finger.Type,
            BasePosition = finger.BasePosition * scaleFactor,
            Joints = [.. finger.Joints.Select(joint => new FingerJoint
            {
                Type = joint.Type,
                BoneLength = joint.BoneLength * scaleFactor,
                // Joint angles remain the same - biomechanical limits don't change with size
                MinFlexion = joint.MinFlexion,
                MaxFlexion = joint.MaxFlexion,
                MinAbduction = joint.MinAbduction,
                MaxAbduction = joint.MaxAbduction,
                RestFlexion = joint.RestFlexion,
                RestAbduction = joint.RestAbduction
            })]
        }).ToImmutableList();

        return new()
        {
            PalmWidth = baseModel.PalmWidth * scaleFactor,
            PalmLength = baseModel.PalmLength * scaleFactor,
            Fingers = scaledFingers
        };
    }

    /// <summary>
    ///     Determine recommended hand size based on hand span measurement
    /// </summary>
    public static HandSize DetermineHandSize(float handSpanMm) => handSpanMm switch
    {
        < 200.0f => HandSize.Small,
        < 230.0f => HandSize.Medium,
        < 255.0f => HandSize.Large,
        _ => HandSize.ExtraLarge
    };

    /// <summary>
    ///     Get difficulty adjustment factor based on hand size and chord characteristics
    /// </summary>
    public static double GetDifficultyAdjustment(HandSize handSize, int fretSpan, int stringSpan)
    {
        // Smaller hands have more difficulty with wide stretches
        var baseAdjustment = handSize switch
        {
            HandSize.Small => 1.20, // 20% harder
            HandSize.Medium => 1.00, // Baseline
            HandSize.Large => 0.90, // 10% easier
            HandSize.ExtraLarge => 0.80, // 20% easier
            _ => 1.00
        };

        // Additional penalty for wide stretches with small hands
        if (handSize == HandSize.Small && fretSpan >= 4)
        {
            baseAdjustment *= 1.15; // Extra 15% penalty for wide stretches
        }

        // Large hands have slight advantage on wide string spans
        if (handSize >= HandSize.Large && stringSpan >= 5)
        {
            baseAdjustment *= 0.95; // 5% easier for wide string spans
        }

        return baseAdjustment;
    }
}