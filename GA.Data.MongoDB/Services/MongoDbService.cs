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
    
   
    public IMongoCollection<FingeringDocument> Fingerings =>
        _database.GetCollection<FingeringDocument>("fingerings");
    
    public IMongoCollection<PitchClassSetDocument> PitchClassSets => 
        _database.GetCollection<PitchClassSetDocument>("pitchClassSets");    
}
