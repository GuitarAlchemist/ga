namespace GA.Data.MongoDB.Services.DocumentServices;

using Business.Core.Scales;
using Microsoft.Extensions.Logging;
using Models;

[UsedImplicitly]
public class  ChordSyncService(ILogger<ChordSyncService> logger, MongoDbService mongoDb) : ISyncService<ChordDocument>
{
    public async Task<bool> SyncAsync()
    {
        try
        {
            var documents = ChordTemplateFactory.CreateAllChordTemplates().Select(template => new ChordDocument
            {
                Name = template.PitchClassSet.Name,
                Root = template.PitchClassSet.Notes.First().ToString(),
                Quality = "Major", // TODO: Determine quality from pitch class set
                Intervals = template.PitchClassSet.Notes.Skip(1)
                    .Select(n => template.PitchClassSet.Notes.First().GetInterval(n).ToString())
                    .ToList(),
                Notes = template.PitchClassSet.Notes.Select(n => n.ToString()).ToList(),
                Category = template.PitchClassSet.Cardinality.Value switch
                {
                    3 => "Triad",
                    4 => "Seventh",
                    _ => "Extended"
                },
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            }).ToList();
            
            await mongoDb.Chords.DeleteManyAsync(Builders<ChordDocument>.Filter.Empty);
            await mongoDb.Chords.InsertManyAsync(documents);
            return true;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error syncing chords");
            return false;
        }
    }

    public async Task<long> GetCountAsync() =>
        await mongoDb.Chords.CountDocumentsAsync(Builders<ChordDocument>.Filter.Empty);
}