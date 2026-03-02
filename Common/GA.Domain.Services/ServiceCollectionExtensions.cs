namespace GA.Domain.Services;

using Chords;
using Chords.Abstractions;
using Chords.Analysis.Atonal;
using Core.Theory.Tonal.Modes.Unified;
using Microsoft.Extensions.DependencyInjection;
using Unified;

/// <summary>
///     Extension methods for registering domain services in a consistent way.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    ///     Registers core music theory and domain services.
    /// </summary>
    public static IServiceCollection AddGaMusicTheoryServices(this IServiceCollection services)
    {
        // Chord Services
        services.AddSingleton<IChordNamingService, ChordNamingService>();
        services.AddSingleton<IChordAnalysisService, AtonalChordAnalysisServiceAdapter>();

        // Unified Mode Services
        services.AddSingleton<IUnifiedModeService, UnifiedModeService>();

        // Note: VoicingAnalyzer, VoicingHarmonicAnalyzer, and VoicingPhysicalAnalyzer
        // are static classes and are used directly without DI registration.

        return services;
    }
}
