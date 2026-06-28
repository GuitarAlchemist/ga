namespace GA.Business.Core.Orchestration.Extensions;

using GA.Business.Core.Orchestration.Services;
using GA.Business.ML.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

/// <summary>
///     DI registration for the Adaptive-AI service (issue #48 proof slice).
/// </summary>
public static class AdaptiveAIServiceExtensions
{
    /// <summary>
    ///     Registers <see cref="IAdaptiveAIService"/> (layer 4 abstraction) bound to its
    ///     layer-5 <see cref="AdaptiveAIService"/> implementation.
    /// </summary>
    /// <remarks>
    ///     Singleton: the service is stateless and deterministic, so a single instance
    ///     is safe to share across requests. <c>TryAdd</c> so a host may override the
    ///     binding (e.g. a richer ML-backed implementation) without a double registration.
    /// </remarks>
    public static IServiceCollection AddAdaptiveAIService(this IServiceCollection services)
    {
        services.TryAddSingleton<IAdaptiveAIService, AdaptiveAIService>();
        return services;
    }
}
