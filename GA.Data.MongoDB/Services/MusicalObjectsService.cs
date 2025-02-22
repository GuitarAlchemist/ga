using Microsoft.Extensions.Logging;

namespace GA.Data.MongoDB.Services;

public class MusicalObjectsService(
    ILogger<MusicalObjectsService> logger,
    IEnumerable<ISyncService> syncServices)
{
    public async Task<SyncResult> SyncAllAsync()
    {
        var result = new SyncResult();
        try
        {
            logger.LogDebug("Found {Count} sync services", syncServices.Count());
            
            foreach (var syncService in syncServices)
            {
                var serviceType = syncService.GetType();
                logger.LogDebug("Processing sync service: {ServiceType}", serviceType.Name);
                
                if (await syncService.SyncAsync())
                {
                    var count = await syncService.GetCountAsync();
                    UpdateResultCount(result, serviceType, count);
                    logger.LogDebug("Successfully synced {ServiceType} with count {Count}", serviceType.Name, count);
                }
                else
                {
                    logger.LogWarning("Sync failed for {ServiceType}", serviceType.Name);
                }
            }
            
            logger.LogDebug("Final counts dictionary has {Count} entries", result.Counts.Count);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error syncing musical objects");
            result.Errors.Add(ex.Message);
        }
        return result;
    }

    private static void UpdateResultCount(SyncResult result, Type serviceType, long count)
    {
        var documentType = serviceType.GetInterfaces()
            .First(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(ISyncService<>))
            .GetGenericArguments()[0];

        result.Counts[documentType] = count;
    }
}
