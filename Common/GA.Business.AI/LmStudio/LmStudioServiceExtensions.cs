namespace GA.Business.AI.LmStudio;

/// <summary>
///     Extension methods for registering LM Studio integration services
/// </summary>
public static class LmStudioServiceExtensions
{
    /// <summary>
    ///     Adds LM Studio integration services to the service collection
    /// </summary>
    public static IServiceCollection AddLmStudioIntegration(this IServiceCollection services)
    {
        // Add MongoDB services if not already added
        //services.AddMongoDbServices();

        // Add LM Studio integration services
        // services.AddScoped<LmStudioIntegrationService>(); // Temporarily disabled

        return services;
    }
}
