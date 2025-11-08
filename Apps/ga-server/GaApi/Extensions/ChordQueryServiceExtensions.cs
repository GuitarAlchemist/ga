namespace GaApi.Extensions;

using Services.ChordQuery;

/// <summary>
///     Extension methods for registering chord query services
/// </summary>
public static class ChordQueryServiceExtensions
{
    /// <summary>
    ///     Registers chord query planning services for optimized chord searches
    /// </summary>
    /// <param name="services">The service collection</param>
    /// <returns>The service collection for chaining</returns>
    public static IServiceCollection AddChordQueryServices(this IServiceCollection services)
    {
        services.AddSingleton<IChordQueryPlanner, ChordQueryPlanner>();

        return services;
    }
}
