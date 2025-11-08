namespace GA.Data.MongoDB.Services.DocumentServices;

using Business.Core;
using Business.Core.Intervals.Primitives;
using Microsoft.Extensions.Logging;
using Models;

[UsedImplicitly]
public class IntervalSyncService(ILogger<IntervalSyncService> logger, MongoDbService mongoDb)
    : ISyncService<IntervalDocument>
{
    public async Task<bool> SyncAsync()
    {
        try
        {
            var documents = new List<IntervalDocument>();

            // Add simple intervals
            documents.AddRange(AssetCatalog.IntervalSizes.Select(i => new IntervalDocument
            {
                Name = i.ToString(),
                Semitones = i.Semitones,
                Quality = i.Consonance.ToString(),
                Size = i.Value,
                IsCompound = false,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            }));

            // Add compound intervals
            documents.AddRange(CompoundIntervalSize.Items.Select(i => new IntervalDocument
            {
                Name = i.ToString(),
                Semitones = i.Semitones,
                Quality = i.Consonance.ToString(),
                Size = i.Value,
                IsCompound = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            }));

            await mongoDb.Intervals.DeleteManyAsync(Builders<IntervalDocument>.Filter.Empty);
            await mongoDb.Intervals.InsertManyAsync(documents);
            return true;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error syncing intervals");
            return false;
        }
    }

    public async Task<long> GetCountAsync()
    {
        return await mongoDb.Intervals.CountDocumentsAsync(Builders<IntervalDocument>.Filter.Empty);
    }
}
