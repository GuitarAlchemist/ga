namespace GA.Data.MongoDB.Services.DocumentServices;

using Business.Config;
using Business.Core.Intervals;
using Business.Core.Notes;
using GA.Business.Core.Scales;
using Microsoft.Extensions.Logging;
using Models;

[UsedImplicitly]
public class ScaleSyncService(ILogger<ScaleSyncService> logger, MongoDbService mongoDb) : ISyncService<ScaleDocument>
{
    public async Task<bool> SyncAsync()
    {
        try
        {
            var documents = new List<ScaleDocument>();
            var scales = ScalesConfig.GetAllScales();
            
            foreach (var scale in scales)
            {
                if (!AccidentedNoteCollection.TryParse(scale.Notes, null, out var notes))
                {
                    logger.LogWarning("Failed to parse notes for scale: {ScaleName}", scale.Name);
                    continue;
                }

                var scaleObj = new Scale(notes);
                var pitchClassSetId = scaleObj.PitchClassSet.Id;
                
                // Skip if the first bit (root note) is not set
                if ((pitchClassSetId.Value & 1) != 1)
                {
                    logger.LogWarning("Invalid scale ID (first bit not set) for scale: {ScaleName}", scale.Name);
                    continue;
                }

                var modes = scaleObj is ModalScale modalScale 
                    ? modalScale.Modes.Select(m => m.Name).ToList() 
                    : null;
                
                documents.Add(new ScaleDocument
                {
                    Name = scale.Name,
                    Notes = scaleObj.Select(n => n.ToString()).ToList(),
                    Intervals = scaleObj.Intervals.Select(i => i.ToString(Interval.Diatonic.Format.ShortName)).ToList(),
                    IntervalClassVector = scaleObj.IntervalClassVector.ToString(),
                    IsModal = scaleObj.IsModal,
                    ModalFamily = scaleObj.ModalFamily?.ToString(),
                    IsNormalForm = scaleObj.PitchClassSet.IsNormalForm,
                    IsClusterFree = scaleObj.PitchClassSet.IsClusterFree,
                    ScaleVideoUrl = scaleObj.PitchClassSet.ScaleVideoUrl?.ToString(),
                    ScalePageUrl = $"https://ianring.com/musictheory/scales/{pitchClassSetId.Value}",
                    Modes = modes,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                });
            }

            await mongoDb.Scales.DeleteManyAsync(Builders<ScaleDocument>.Filter.Empty);
            await mongoDb.Scales.InsertManyAsync(documents);
            return true;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error syncing scales");
            return false;
        }
    }

    public async Task<long> GetCountAsync() =>
        await mongoDb.Scales.CountDocumentsAsync(Builders<ScaleDocument>.Filter.Empty);
}
