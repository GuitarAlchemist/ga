namespace GA.Data.MongoDB.Services.DocumentServices;

using Business.Core;
using Business.Core.Tonal;
using Microsoft.Extensions.Logging;
using Models;

[UsedImplicitly]
public class KeySyncService(ILogger<KeySyncService> logger, MongoDbService mongoDb) 
    : ISyncService<KeyDocument>
{
    public async Task<bool> SyncAsync()
    {
        try
        {
            var documents = Assets.Keys.Select(key => new KeyDocument
            {
                Name = key.ToString(),
                Root = key.Root.ToString(),
                Mode = key.KeyMode.ToString(),
                AccidentedNotes = key.KeySignature.AccidentedNotes.Select(n => n.ToString()).ToList(),
                NumberOfAccidentals = key.KeySignature.AccidentalCount,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            }).ToList();

            await mongoDb.Keys.DeleteManyAsync(Builders<KeyDocument>.Filter.Empty);
            await mongoDb.Keys.InsertManyAsync(documents);
            return true;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error syncing keys");
            return false;
        }
    }

    public async Task<long> GetCountAsync() =>
        await mongoDb.Keys.CountDocumentsAsync(Builders<KeyDocument>.Filter.Empty);
}