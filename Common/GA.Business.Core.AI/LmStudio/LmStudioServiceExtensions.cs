using GA.Data.MongoDB.Extensions;
using GA.Data.MongoDB.Services.Embeddings;
using Microsoft.Extensions.DependencyInjection;

namespace GA.Business.Core.AI.LmStudio;

/// <summary>
/// Extension methods for registering LM Studio integration services
/// </summary>
public static class LmStudioServiceExtensions
{
    /// <summary>
    /// Adds LM Studio integration services to the service collection
    /// </summary>
    public static IServiceCollection AddLmStudioIntegration(this IServiceCollection services)
    {
        // Add MongoDB services if not already added
        //services.AddMongoDbServices();
        
        // Add LM Studio integration services
        services.AddScoped<LmStudioIntegrationService>();
        
        return services;
    }
}
