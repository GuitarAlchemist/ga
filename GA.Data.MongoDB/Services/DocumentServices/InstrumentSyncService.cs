namespace GA.Data.MongoDB.Services.DocumentServices;

using GA.Domain.Core.Primitives;
// using EntityFramework.Data.Instruments;
using Models;

[UsedImplicitly]
public sealed class InstrumentSyncService(MongoDbService mongoDb)
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
