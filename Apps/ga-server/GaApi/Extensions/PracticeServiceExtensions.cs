namespace GaApi.Extensions;

using GaApi.Services;
using Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for registering practice and ear training services.
/// </summary>
public static class PracticeServiceExtensions
{
    /// <summary>
    /// Register all practice, ear training, and fretboard context services.
    /// </summary>
    public static IServiceCollection AddPracticeServices(this IServiceCollection services)
    {
        // Practice routine generation
        services.AddSingleton<PracticeRoutineService>();

        // Ear training (quizzes and curriculum)
        services.AddSingleton<EarTrainingService>();

        // Fretboard visualization and context
        services.AddSingleton<FretboardContextService>();

        return services;
    }
}
