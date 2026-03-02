namespace GaApi.Extensions;

using GA.Infrastructure.Documentation;
using ILGPU.Runtime;
using Models;
using Services;

/// <summary>
///     Extension methods for registering core infrastructure services.
/// </summary>
public static class InfrastructureServiceExtensions
{
    /// <summary>
    ///     Adds core infrastructure services (MongoDB, GPU Acceleration, etc.)
    /// </summary>
    public static IServiceCollection AddGaInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        // 1. Configuration
        services.Configure<MongoDbSettings>(configuration.GetSection("MongoDB"));
        services.Configure<VectorSearchOptions>(configuration.GetSection("VectorSearch"));

        // 2. MongoDB
        services.AddSingleton<MongoDbService>();

        // 3. Domain Schema Discovery
        services.AddSingleton<SchemaDiscoveryService>();

        // 4. GPU Acceleration (ILGPU)
        services.AddSingleton<IIlgpuContextManager, IlgpuContextManager>();
        services.AddSingleton<Accelerator>(sp =>
        {
            var contextManager = sp.GetRequiredService<IIlgpuContextManager>();
            var accelerator = contextManager.PrimaryAccelerator;
            if (accelerator == null)
            {
                throw new InvalidOperationException("Failed to initialize primary GPU accelerator.");
            }

            return accelerator;
        });

        return services;
    }
}
