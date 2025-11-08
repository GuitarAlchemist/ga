namespace GA.Data.MongoDB.Services.DocumentServices;

using Business.Core.Notes;
using EntityFramework.Data.Instruments;
using Microsoft.Extensions.Logging;
using Models;

[UsedImplicitly]
public sealed class InstrumentSyncService(ILogger<InstrumentSyncService> logger, MongoDbService mongoDb)
    : ISyncService<InstrumentDocument>
{
    public async Task<bool> SyncAsync()
    {
        try
        {
            var documents = InstrumentsRepository.Instance.Instruments
                .Select(instrument =>
                {
                    var defaultTuning = instrument.Tunings.First().Value.Tuning;
                    _ = AccidentedNoteCollection.TryParse(defaultTuning, null, out var countNotes);

                    return new InstrumentDocument
                    {
                        Name = instrument.Name,
                        Category = "String",
                        StringCount = countNotes?.Count ?? 0,
                        Tunings = instrument.Tunings
                            .Select(t => new TuningDocument
                            {
                                Name = t.Key,
                                Notes = AccidentedNoteCollection.TryParse(t.Value.Tuning, null, out var notes)
                                    ? notes.Select(n => n.ToString()).ToList()
                                    : [],
                                IsStandard = t.Value.IsStandard,
                                Description = null
                            })
                            .ToList(),
                        Description = null,
                        Family = "String",
                        Range = null,
                        CreatedAt = DateTime.UtcNow,
                        UpdatedAt = DateTime.UtcNow
                    };
                })
                .ToList();

            await mongoDb.Instruments.DeleteManyAsync(Builders<InstrumentDocument>.Filter.Empty);
            await mongoDb.Instruments.InsertManyAsync(documents);
            return true;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error syncing instruments");
            return false;
        }
    }

    public async Task<long> GetCountAsync()
    {
        return await mongoDb.Instruments.CountDocumentsAsync(Builders<InstrumentDocument>.Filter.Empty);
    }
}
