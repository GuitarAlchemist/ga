namespace GA.Data.MongoDB.Services;

using Business.Core;
using Business.Core.Atonal;
using Business.Core.Intervals;
using Business.Core.Notes;
using Business.Core.Notes.Primitives;
using Business.Core.Scales;
using global::MongoDB.Driver;
using Microsoft.Extensions.Logging;
using Models;

public class MusicalObjectsService : IMusicalObjectsService
{
    private readonly IMongoDatabase _database;
    private readonly ILogger<MusicalObjectsService> _logger;
    
    private readonly IMongoCollection<NoteDocument> _notes;
    private readonly IMongoCollection<IntervalDocument> _intervals;
    private readonly IMongoCollection<KeyDocument> _keys;
    private readonly IMongoCollection<ScaleDocument> _scales;
    private readonly IMongoCollection<PitchClassDocument> _pitchClasses;

    public MusicalObjectsService(IMongoDatabase database, ILogger<MusicalObjectsService> logger)
    {
        _database = database;
        _logger = logger;
        
        _notes = database.GetCollection<NoteDocument>("notes");
        _intervals = database.GetCollection<IntervalDocument>("intervals");
        _keys = database.GetCollection<KeyDocument>("keys");
        _scales = database.GetCollection<ScaleDocument>("scales");
        _pitchClasses = database.GetCollection<PitchClassDocument>("pitchClasses");
        
        CreateIndexes();
    }

    private void CreateIndexes()
    {
        // Notes index
        var noteIndex = Builders<NoteDocument>.IndexKeys.Ascending(n => n.Name);
        _notes.Indexes.CreateOne(new CreateIndexModel<NoteDocument>(noteIndex, new CreateIndexOptions { Unique = true }));
        
        // Intervals index
        var intervalIndex = Builders<IntervalDocument>.IndexKeys.Ascending(i => i.Name);
        _intervals.Indexes.CreateOne(new CreateIndexModel<IntervalDocument>(intervalIndex, new CreateIndexOptions { Unique = true }));
        
        // Keys index
        var keyIndex = Builders<KeyDocument>.IndexKeys.Ascending(k => k.Name);
        _keys.Indexes.CreateOne(new CreateIndexModel<KeyDocument>(keyIndex, new CreateIndexOptions { Unique = true }));
        
        // Scales index
        var scaleIndex = Builders<ScaleDocument>.IndexKeys.Ascending(s => s.Name);
        _scales.Indexes.CreateOne(new CreateIndexModel<ScaleDocument>(scaleIndex, new CreateIndexOptions { Unique = true }));
        
        // Pitch classes index
        var pitchClassIndex = Builders<PitchClassDocument>.IndexKeys.Ascending(p => p.Value);
        _pitchClasses.Indexes.CreateOne(new CreateIndexModel<PitchClassDocument>(pitchClassIndex, new CreateIndexOptions { Unique = true }));
    }

    public async Task<List<NoteDocument>> GetAllNotesAsync() => 
        await _notes.Find(_ => true).ToListAsync();

    public async Task<bool> SyncNotesAsync()
    {
        try
        {
            var naturalNotes = NaturalNote.Items.Select(n => new NoteDocument
            {
                Name = n.ToString(),
                MidiNumber = MidiNote.Create(Octave.Min, n.PitchClass).Value,
                Category = "Natural",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            });

            var sharpNotes = Note.Sharp.Items.Select(n => new NoteDocument
            {
                Name = n.ToString(),
                MidiNumber = MidiNote.Create(Octave.Min, n.PitchClass).Value,
                Category = "Sharp",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            });

            var flatNotes = Note.Flat.Items.Select(n => new NoteDocument
            {
                Name = n.ToString(),
                MidiNumber = MidiNote.Create(Octave.Min, n.PitchClass).Value,
                Category = "Flat",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            });

            await _notes.DeleteManyAsync(_ => true);
            await _notes.InsertManyAsync(naturalNotes);
            await _notes.InsertManyAsync(sharpNotes);
            await _notes.InsertManyAsync(flatNotes);
        
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error syncing notes");
            return false;
        }
    }

    public async Task<List<IntervalDocument>> GetAllIntervalsAsync() => 
        await _intervals.Find(_ => true).ToListAsync();

    public async Task<bool> SyncIntervalsAsync()
    {
        try
        {
            var simpleIntervals = Assets.IntervalSizes.Select(i => new IntervalDocument
            {
                Name = i.ToString(),
                Semitones = i.Semitones,
                Quality = i.Consonance.ToString(), 
                Size = i.Value,
                IsCompound = false,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            });

            var compoundIntervals = Assets.CompoundIntervalSizes.Select(i => new IntervalDocument
            {
                Name = i.ToString(),
                Semitones = i.Semitones,
                Quality = i.Consonance.ToString(), 
                Size = i.Value,
                IsCompound = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            });

            await _intervals.DeleteManyAsync(_ => true);
            await _intervals.InsertManyAsync(simpleIntervals);
            await _intervals.InsertManyAsync(compoundIntervals);
            
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error syncing intervals");
            return false;
        }
    }

    public async Task<List<KeyDocument>> GetAllKeysAsync() => 
        await _keys.Find(_ => true).ToListAsync();

    public async Task<bool> SyncKeysAsync()
    {
        try
        {
            var keys = Assets.Keys.Select(k => new KeyDocument
            {
                Name = k.ToString(),
                Root = k.Root.ToString(),
                Mode = k.KeyMode.ToString(),
                AccidentedNotes = k.KeySignature.AccidentedNotes.Select(n => n.ToString()).ToList(),
                NumberOfAccidentals = k.KeySignature.AccidentalCount,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            });

            await _keys.DeleteManyAsync(_ => true);
            await _keys.InsertManyAsync(keys);
            
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error syncing keys");
            return false;
        }
    }

    public async Task<List<ScaleDocument>> GetAllScalesAsync() => 
        await _scales.Find(_ => true).ToListAsync();

    public async Task<bool> SyncScalesAsync()
    {
        try
        {
            var scales = Scale.Items.Select(s => new ScaleDocument
            {
                Name = s.ToString(),
                Intervals = s.Intervals.Select(i => i.ToString()).ToList(),
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            });

            await _scales.DeleteManyAsync(_ => true);
            await _scales.InsertManyAsync(scales);
            
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error syncing scales");
            return false;
        }
    }

    public async Task<List<PitchClassDocument>> GetAllPitchClassesAsync() => 
        await _pitchClasses.Find(_ => true).ToListAsync();

    public async Task<bool> SyncPitchClassesAsync()
    {
        try
        {
            var pitchClasses = PitchClass.Items.Select(pc => new PitchClassDocument
            {
                Value = pc.Value,
                Notes = new[] { pc.ToSharpNote().ToString(), pc.ToFlatNote().ToString() }.ToList(),
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            });

            await _pitchClasses.DeleteManyAsync(_ => true);
            await _pitchClasses.InsertManyAsync(pitchClasses);
            
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error syncing pitch classes");
            return false;
        }
    }

    public async Task<SyncResult> SyncAllAsync()
    {
        var errors = new List<string>();
        var result = new SyncResult(0, 0, 0, 0, 0, errors);

        try
        {
            if (await SyncNotesAsync())
                result = result with { NotesAdded = (int)await _notes.CountDocumentsAsync(_ => true) };
            else
                errors.Add("Failed to sync notes");

            if (await SyncIntervalsAsync())
                result = result with { IntervalsAdded = (int)await _intervals.CountDocumentsAsync(_ => true) };
            else
                errors.Add("Failed to sync intervals");

            if (await SyncKeysAsync())
                result = result with { KeysAdded = (int)await _keys.CountDocumentsAsync(_ => true) };
            else
                errors.Add("Failed to sync keys");

            if (await SyncScalesAsync())
                result = result with { ScalesAdded = (int)await _scales.CountDocumentsAsync(_ => true) };
            else
                errors.Add("Failed to sync scales");

            if (await SyncPitchClassesAsync())
                result = result with { PitchClassesAdded = (int)await _pitchClasses.CountDocumentsAsync(_ => true) };
            else
                errors.Add("Failed to sync pitch classes");
        }
        catch (Exception ex)
        {
            errors.Add($"Unexpected error during sync: {ex.Message}");
            _logger.LogError(ex, "Error during full sync");
        }

        return result with { Errors = errors };
    }
}