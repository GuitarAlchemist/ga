namespace GA.Data.MongoDB.Services.DocumentServices;

using Business.Core.Chords;
using Business.Core.Notes;
using Business.Core.Notes.Extensions;
using Microsoft.Extensions.Logging;
using Models;

[UsedImplicitly]
public class ArpeggioSyncService(ILogger<ArpeggioSyncService> logger, MongoDbService mongoDb)
    : ISyncService<ArpeggioDocument>
{
    public async Task<bool> SyncAsync()
    {
        try
        {
            var documents = ChordTemplateFactory.GenerateAllPossibleChords().Select(template => new ArpeggioDocument
            {
                Name = template.PitchClassSet.Name,
                Root = template.PitchClassSet.Notes.First().ToString(),
                Intervals = [.. template.PitchClassSet.Notes.Skip(1).Select(n => template.PitchClassSet.Notes.First().GetInterval(n).ToString())],
                Notes = [.. template.PitchClassSet.Notes.Select(n => n.ToString())],
                Category = template.PitchClassSet.Contains(Note.Chromatic.DSharpOrEFlat.PitchClass) ? "Minor" : "Major",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            }).ToList();

            await mongoDb.Arpeggios.DeleteManyAsync(Builders<ArpeggioDocument>.Filter.Empty);
            await mongoDb.Arpeggios.InsertManyAsync(documents);
            return true;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error syncing arpeggios");
            return false;
        }
    }

    public async Task<long> GetCountAsync()
    {
        return await mongoDb.Arpeggios.CountDocumentsAsync(Builders<ArpeggioDocument>.Filter.Empty);
    }
}
