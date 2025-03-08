using GA.Data.MongoDB.Services;
using Microsoft.Extensions.DependencyInjection;

namespace GA.Data.MongoDB.Extensions;

using Microsoft.Extensions.Options;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddMongoDb(this IServiceCollection services, Action<MongoDbConfig> configureOptions)
    {
        services.Configure(configureOptions);

        services.AddSingleton<IMongoClient>(sp =>
        {
            var config = sp.GetRequiredService<IOptions<MongoDbConfig>>();
            return new MongoClient(config.Value.ConnectionString);
        });

        services.AddSingleton<IMongoDatabase>(sp =>
        {
            var config = sp.GetRequiredService<IOptions<MongoDbConfig>>();
            var client = sp.GetRequiredService<IMongoClient>();
            return client.GetDatabase(config.Value.DatabaseName);
        });

        services.AddSingleton<IInstrumentService, InstrumentService>();

        return services;
    }

    public static IServiceCollection AddSyncServices(this IServiceCollection services)
    {
        // Get all types from the assembly containing ISyncService
        var assembly = typeof(ISyncService).Assembly;
        
        // Find all non-abstract classes that implement ISyncService
        var syncServiceTypes = assembly.GetTypes()
            .Where(t => t.IsClass 
                && !t.IsAbstract 
                && t.GetInterfaces().Any(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(ISyncService<>)));

        // Register each implementation
        foreach (var serviceType in syncServiceTypes)
        {
            services.AddSingleton(typeof(ISyncService), serviceType);
        }

        // Register MusicalObjectsService
        services.AddSingleton<MusicalObjectsService>();

        return services;
    }
}