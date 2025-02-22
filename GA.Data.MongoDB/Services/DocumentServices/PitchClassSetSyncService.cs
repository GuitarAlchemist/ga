namespace GA.Data.MongoDB.Services.DocumentServices;

using GA.Business.Core.Atonal;
using Microsoft.Extensions.Logging;
using Models;

[UsedImplicitly]
public class PitchClassSetSyncService(ILogger<PitchClassSetSyncService> logger, MongoDbService mongoDb) : ISyncService<PitchClassSetDocument>
{
    public async Task<bool> SyncAsync()
    {
        try
        {
            var documents = PitchClassSet.Items.Select(pitchClassSet => new PitchClassSetDocument
            {
                Value = pitchClassSet.Id.Value,
                Name = pitchClassSet.Name,
                Cardinality = pitchClassSet.Cardinality.Value,
                PitchClasses = pitchClassSet.Select(pc => pc.Value).ToList(),
                Notes = pitchClassSet.Notes.Select(n => n.ToString()).ToList(),
                IntervalClassVector = pitchClassSet.IntervalClassVector.ToString(),
                ModalFamily = pitchClassSet.ModalFamily?.ToString(),
                IsModal = pitchClassSet.IsModal,
                IsNormalForm = pitchClassSet.IsNormalForm,
                IsClusterFree = pitchClassSet.Id.IsClusterFree,
                ScaleVideoUrl = pitchClassSet.ScaleVideoUrl?.ToString(),
                ScalePageUrl = pitchClassSet.Id.IsScale ? pitchClassSet.ScalePageUrl.ToString() : string.Empty,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            }).ToList();

            await mongoDb.PitchClassSets.DeleteManyAsync(Builders<PitchClassSetDocument>.Filter.Empty);
            await mongoDb.PitchClassSets.InsertManyAsync(documents);
            return true;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error syncing pitch class sets");
            return false;
        }
    }

    public async Task<long> GetCountAsync() =>
        await mongoDb.PitchClassSets.CountDocumentsAsync(Builders<PitchClassSetDocument>.Filter.Empty);
}