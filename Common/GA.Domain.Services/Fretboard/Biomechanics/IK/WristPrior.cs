namespace GA.Domain.Services.Fretboard.Biomechanics.IK;

using System.Numerics;

/// <summary>
///     Represents a wrist pose prior derived from external datasets (e.g., LeRobot).
/// </summary>
internal readonly record struct WristPrior(Vector3 Mean, Vector3 StdDev);