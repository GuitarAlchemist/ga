namespace GA.Domain.Core.Instruments.Biomechanics;

/// <summary>
///     Joint type enumeration
/// </summary>
public enum JointType
{
    /// <summary>Carpometacarpal (base of finger)</summary>
    Cmc,

    /// <summary>Metacarpophalangeal (knuckle)</summary>
    Mcp,

    /// <summary>Proximal Interphalangeal (middle joint)</summary>
    Pip,

    /// <summary>Distal Interphalangeal (fingertip joint)</summary>
    Dip,

    /// <summary>Interphalangeal (thumb only)</summary>
    Ip
}