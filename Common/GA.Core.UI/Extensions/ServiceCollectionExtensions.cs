namespace GA.Core.UI.Extensions;

using Components.Grids;
using Microsoft.Extensions.DependencyInjection;

public static class ServiceCollectionExtensions
{
    public static void UseCoreComponents(this IServiceCollection services)
    {
        services.AddScoped<AgGridTabularDataLoader>();
    }
}
