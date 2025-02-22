using GA.Data.MongoDB.Configuration;
using GA.Data.MongoDB.Models;
using Microsoft.Extensions.Options;

namespace GA.Data.MongoDB.Services;

public class MongoDbService
{
    private readonly IMongoDatabase _database;

    public MongoDbService(IOptions<MongoDbSettings> settings)
    {
        var client = new MongoClient(settings.Value.ConnectionString);
        _database = client.GetDatabase(settings.Value.DatabaseName);
    }

    public async Task DropAllCollectionsAsync()
    {
        await Notes.DeleteManyAsync(Builders<NoteDocument>.Filter.Empty);
        await Intervals.DeleteManyAsync(Builders<IntervalDocument>.Filter.Empty);
        await Keys.DeleteManyAsync(Builders<KeyDocument>.Filter.Empty);
        await Scales.DeleteManyAsync(Builders<ScaleDocument>.Filter.Empty);
        await PitchClasses.DeleteManyAsync(Builders<PitchClassDocument>.Filter.Empty);
        await Instruments.DeleteManyAsync(Builders<InstrumentDocument>.Filter.Empty);
        await Chords.DeleteManyAsync(Builders<ChordDocument>.Filter.Empty);
        await Arpeggios.DeleteManyAsync(Builders<ArpeggioDocument>.Filter.Empty);
        await Progressions.DeleteManyAsync(Builders<ProgressionDocument>.Filter.Empty);
        await PitchClassSets.DeleteManyAsync(Builders<PitchClassSetDocument>.Filter.Empty);
        await SetClasses.DeleteManyAsync(Builders<SetClassDocument>.Filter.Empty);
    }

    public IMongoCollection<NoteDocument> Notes => 
        _database.GetCollection<NoteDocument>("notes");
    
    public IMongoCollection<IntervalDocument> Intervals => 
        _database.GetCollection<IntervalDocument>("intervals");
    
    public IMongoCollection<KeyDocument> Keys => 
        _database.GetCollection<KeyDocument>("keys");
    
    public IMongoCollection<ScaleDocument> Scales => 
        _database.GetCollection<ScaleDocument>("scales");
    
    public IMongoCollection<PitchClassDocument> PitchClasses => 
        _database.GetCollection<PitchClassDocument>("pitchClasses");
    
    public IMongoCollection<InstrumentDocument> Instruments => 
        _database.GetCollection<InstrumentDocument>("instruments");

    public IMongoCollection<ChordDocument> Chords =>
        _database.GetCollection<ChordDocument>("chords");
    
    public IMongoCollection<ArpeggioDocument> Arpeggios =>
        _database.GetCollection<ArpeggioDocument>("arpeggios");
    
    public IMongoCollection<ProgressionDocument> Progressions =>
        _database.GetCollection<ProgressionDocument>("progressions");
    
    public IMongoCollection<PitchClassSetDocument> PitchClassSets => 
        _database.GetCollection<PitchClassSetDocument>("pitchClassSets");
    
    public IMongoCollection<SetClassDocument> SetClasses => 
        _database.GetCollection<SetClassDocument>("setClasses");
}
