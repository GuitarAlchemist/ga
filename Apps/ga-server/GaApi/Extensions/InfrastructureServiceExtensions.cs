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
        // When running under Aspire, override connection string from service discovery injection.
        // Aspire sets ConnectionStrings__guitar-alchemist when GaApi has WithReference(mongoDatabase).
        services.PostConfigure<MongoDbSettings>(settings =>
        {
            var aspireCs = configuration.GetConnectionString("guitar-alchemist");
            if (!string.IsNullOrEmpty(aspireCs))
                settings.ConnectionString = aspireCs;
        });
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
