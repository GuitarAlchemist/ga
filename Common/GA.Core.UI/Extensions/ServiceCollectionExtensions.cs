using GA.Core.UI.Components.Grids;
using Microsoft.Extensions.DependencyInjection;

namespace GA.Core.UI.Extensions;

public static class ServiceCollectionExtensions
{
    public static void UseCoreComponents(this IServiceCollection services)
    {
        services.AddScoped<AgGridTabularDataLoader>();
    }
}

