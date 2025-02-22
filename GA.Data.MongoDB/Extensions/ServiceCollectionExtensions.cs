using Microsoft.Extensions.DependencyInjection;
using MongoDB.Driver;
using GA.Data.MongoDB.Services;

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
}