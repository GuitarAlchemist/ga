namespace GaApi.Extensions;

using Services;

/// <summary>
///     Extension methods for registering BSP (Binary Space Partitioning) services
/// </summary>
public static class BSPServiceExtensions
{
    /// <summary>
    ///     Registers all BSP services including:
    ///     - Music hierarchy service for hierarchical music structure analysis
    ///     - Music room service for procedural music room generation
    ///     - Room generation background service for async room generation
    /// </summary>
    /// <param name="services">The service collection</param>
    /// <returns>The service collection for chaining</returns>
    /// <remarks>
    ///     BSP (Binary Space Partitioning) is used for:
    ///     - Spatial analysis of harmonic relationships
    ///     - Procedural generation of music practice rooms
    ///     - Hierarchical decomposition of musical structures
    ///     - Dungeon-like music theory exploration spaces
    ///     Note: TonalBSPService is registered via AddTonalBSP() extension method from GA.BSP.Core
    /// </remarks>
    public static IServiceCollection AddBSPServices(this IServiceCollection services)
    {
        // Music hierarchy service for hierarchical analysis
        services.AddSingleton<MusicHierarchyService>();

        // Music room generation service
        services.AddSingleton<MusicRoomService>();

        // Background job processor for room generation
        services.AddHostedService<RoomGenerationBackgroundService>();

        return services;
    }
}
