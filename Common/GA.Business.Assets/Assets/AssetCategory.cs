namespace GA.Business.Assets.Assets;

using JetBrains.Annotations;

/// <summary>
///     Categories for 3D assets used in BSP DOOM Explorer
/// </summary>
[PublicAPI]
public enum AssetCategory
{
    /// <summary>
    ///     Pyramids, pillars, obelisks - structural elements
    /// </summary>
    Architecture,

    /// <summary>
    ///     Ankh, Eye of Horus, flasks, scrolls - alchemy themed props
    /// </summary>
    AlchemyProps,

    /// <summary>
    ///     Various gem cuts and precious stones
    /// </summary>
    Gems,

    /// <summary>
    ///     Canopic jars, vessels, containers
    /// </summary>
    Jars,

    /// <summary>
    ///     Light sources - torches, braziers
    /// </summary>
    Torches,

    /// <summary>
    ///     Scarabs, statues, masks, sarcophagi - Egyptian artifacts
    /// </summary>
    Artifacts,

    /// <summary>
    ///     General decoration elements
    /// </summary>
    Decorative
}
