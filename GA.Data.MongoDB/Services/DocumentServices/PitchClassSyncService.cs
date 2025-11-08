namespace GA.Data.MongoDB.Services.DocumentServices;

using Business.Core;
using Microsoft.Extensions.Logging;
using Models;

[UsedImplicitly]
public class PitchClassSyncService(ILogger<PitchClassSyncService> logger, MongoDbService mongoDb)
    : ISyncService<PitchClassDocument>
{
    public async Task<bool> SyncAsync()
    {
        try
        {
            var documents = AssetCatalog.PitchClasses.Select(pc => new PitchClassDocument
            {
                Value = pc.Value,
                Name = pc.ToString(),
                Notes = new[] { pc.ToSharpNote().ToString(), pc.ToFlatNote().ToString() }.Distinct().ToList(),
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            }).ToList();

            await mongoDb.PitchClasses.DeleteManyAsync(Builders<PitchClassDocument>.Filter.Empty);
            await mongoDb.PitchClasses.InsertManyAsync(documents);
            return true;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error syncing pitch classes");
            return false;
        }
    }

    public async Task<long> GetCountAsync()
    {
        return await mongoDb.PitchClasses.CountDocumentsAsync(Builders<PitchClassDocument>.Filter.Empty);
    }
}
