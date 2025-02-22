namespace GA.Data.MongoDB.Services.DocumentServices;

using Business.Core;
using Business.Core.Intervals;
using GA.Business.Core.Notes.Primitives;
using Microsoft.Extensions.Logging;
using Models;

[UsedImplicitly]
public class NoteSyncService(ILogger<NoteSyncService> logger, MongoDbService mongoDb) : ISyncService<NoteDocument>
{
        public async Task<bool> SyncAsync()
    {
        try
        {
            var documents = new List<NoteDocument>();
            
            // Add Natural notes
            documents.AddRange(Assets.NaturalNotes.Select(n => new NoteDocument
            {
                Name = n.ToString(),
                MidiNumber = MidiNote.Create(Octave.Small, n.PitchClass).Value,
                Category = "Natural",
                PitchClass = n.PitchClass.Value,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            }));

            // Add Sharp notes
            documents.AddRange(Assets.SharpNotes.Select(n => new NoteDocument
            {
                Name = n.ToString(),
                MidiNumber = MidiNote.Create(Octave.Small, n.PitchClass).Value,
                Category = "Sharp",
                PitchClass = n.PitchClass.Value,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            }));

            // Add Flat notes
            documents.AddRange(Assets.FlatNotes.Select(n => new NoteDocument
            {
                Name = n.ToString(),
                MidiNumber = MidiNote.Create(Octave.Small, n.PitchClass).Value,
                Category = "Flat",
                PitchClass = n.PitchClass.Value,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            }));

            // Add Accidented notes
            documents.AddRange(Assets.AccidentedNotes.Select(n => new NoteDocument
            {
                Name = n.ToString(),
                MidiNumber = MidiNote.Create(Octave.Small, n.PitchClass).Value,
                Category = "Accidented",
                PitchClass = n.PitchClass.Value,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            }));

            await mongoDb.Notes.DeleteManyAsync(Builders<NoteDocument>.Filter.Empty);
            await mongoDb.Notes.InsertManyAsync(documents);
            return true;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error syncing notes");
            return false;
        }
    }

    public async Task<long> GetCountAsync() =>
        await mongoDb.Notes.CountDocumentsAsync(Builders<NoteDocument>.Filter.Empty);
}