namespace GA.Business.Core.Fretboard.Biomechanics.IK;

/// <summary>
///     Represents a wrist pose prior derived from external datasets (e.g., LeRobot).
/// </summary>
internal readonly record struct WristPrior(Vector3 Mean, Vector3 StdDev);
