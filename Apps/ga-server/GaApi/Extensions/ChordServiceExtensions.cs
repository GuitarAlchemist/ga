namespace GaApi.Extensions;

using Microsoft.Extensions.DependencyInjection;
using GaApi.Services;

/// <summary>
///     Extension methods for registering chord-related services
/// </summary>
public static class ChordServiceExtensions
{
    /// <summary>
    ///     Registers all chord-related services including:
    ///     - Core chord services (IChordService)
    ///     - Contextual chord services (IContextualChordService, IVoicingFilterService, IModulationService)
    ///     - Monadic chord services (MonadicChordService)
    /// </summary>
    /// <param name="services">The service collection</param>
    /// <returns>The service collection for chaining</returns>
    public static IServiceCollection AddChordServices(this IServiceCollection services)
    {
        // Core chord service
        services.AddScoped<IChordService, ChordService>();

        // Unified chord naming façade
        services.AddScoped<GA.Business.Core.Chords.IChordNamingService, GA.Business.Core.Chords.ChordNamingService>();

        // Contextual chord services for key-aware chord generation
        services.AddScoped<IContextualChordService, ContextualChordService>();
        services.AddScoped<IVoicingFilterService, VoicingFilterService>();
        services.AddScoped<IModulationService, ModulationService>();

        // Monadic chord service for functional programming patterns
        services.AddScoped<MonadicChordService>();

        return services;
    }
}
