namespace GA.Data.MongoDB.Services.DocumentServices;

using GA.Business.Core.Atonal;
using Microsoft.Extensions.Logging;
using Models;

[UsedImplicitly]
public class SetClassSyncService(ILogger<SetClassSyncService> logger, MongoDbService mongoDb) 
    : ISyncService<SetClassDocument>
{
    public async Task<bool> SyncAsync()
    {
        try
        {
            var documents = SetClass.Items.Select(setClass => new SetClassDocument
            {
                Cardinality = setClass.Cardinality.Value,
                IntervalClassVector = setClass.IntervalClassVector.ToString(),
                PrimeFormId = setClass.PrimeForm.Id.Value,
                IsModal = setClass.IsModal,
                ModalFamily = setClass.ModalFamily?.ToString(),
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            }).ToList();

            await mongoDb.SetClasses.DeleteManyAsync(Builders<SetClassDocument>.Filter.Empty);
            await mongoDb.SetClasses.InsertManyAsync(documents);
            return true;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error syncing set classes");
            return false;
        }
    }

    public async Task<long> GetCountAsync() =>
        await mongoDb.SetClasses.CountDocumentsAsync(Builders<SetClassDocument>.Filter.Empty);
}