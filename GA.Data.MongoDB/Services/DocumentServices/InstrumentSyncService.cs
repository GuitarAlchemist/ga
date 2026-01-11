namespace GA.Data.MongoDB.Services.DocumentServices;

using Business.Core.Notes;
// using EntityFramework.Data.Instruments;
using Microsoft.Extensions.Logging;
using Models;

[UsedImplicitly]
public sealed class InstrumentSyncService(ILogger<InstrumentSyncService> logger, MongoDbService mongoDb)
    : ISyncService<InstrumentDocument>
{
    public async Task<bool> SyncAsync()
    {
         throw new NotImplementedException();
         /*
        try
        {
           // ... commented out ...
            return true;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error syncing instruments");
            return false;
        }
        */
    }

    public async Task<long> GetCountAsync()
    {
        return await mongoDb.Instruments.CountDocumentsAsync(Builders<InstrumentDocument>.Filter.Empty);
    }
}
