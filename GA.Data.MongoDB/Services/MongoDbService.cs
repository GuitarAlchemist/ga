using GA.Data.MongoDB.Configuration;
using GA.Data.MongoDB.Models;
using Microsoft.Extensions.Options;

namespace GA.Data.MongoDB.Services;

using global::MongoDB.Bson.Serialization.Conventions;
using Models.Rag;

public class MongoDbService
{
    private readonly IMongoDatabase _database;

    /// <summary>
    /// Initializes a new instance of the <see cref="MongoDbService"/> class.
    /// </summary>
    /// <param name="settings">The MongoDB connection settings.</param>
    public MongoDbService(IOptions<MongoDbSettings> settings)
    {
        var conventionPack = new ConventionPack { new CamelCaseElementNameConvention() };
        ConventionRegistry.Register("camelCase", conventionPack, _ => true);
        
        var client = new MongoClient(settings.Value.ConnectionString);
        _database = client.GetDatabase(settings.Value.DatabaseName);
    }

    /// <summary>
    /// Deletes all documents from all collections in the database.
    /// </summary>
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

    public IMongoCollection<NoteDocument> Notes => _database.GetCollection<NoteDocument>("notes");
    public IMongoCollection<IntervalDocument> Intervals => _database.GetCollection<IntervalDocument>("intervals");
    public IMongoCollection<KeyDocument> Keys => _database.GetCollection<KeyDocument>("keys");
    public IMongoCollection<ScaleDocument> Scales => _database.GetCollection<ScaleDocument>("scales");
    public IMongoCollection<PitchClassDocument> PitchClasses => _database.GetCollection<PitchClassDocument>("pitchClasses");
    public IMongoCollection<InstrumentDocument> Instruments => _database.GetCollection<InstrumentDocument>("instruments");
    public IMongoCollection<ChordDocument> Chords => _database.GetCollection<ChordDocument>("chords");
    public IMongoCollection<ChordRagDocument> ChordsRag => _database.GetCollection<ChordRagDocument>("chordsRag");
    public IMongoCollection<ArpeggioDocument> Arpeggios => _database.GetCollection<ArpeggioDocument>("arpeggios");
    public IMongoCollection<ProgressionDocument> Progressions => _database.GetCollection<ProgressionDocument>("progressions");
    public IMongoCollection<PitchClassSetDocument> PitchClassSets => _database.GetCollection<PitchClassSetDocument>("pitchClassSets");
    public IMongoCollection<SetClassDocument> SetClasses => _database.GetCollection<SetClassDocument>("setClasses");

    public async Task CreateIndexesAsync()
    {
        // Notes indexes
        await Notes.Indexes.CreateOneAsync(
            new CreateIndexModel<NoteDocument>(
                Builders<NoteDocument>.IndexKeys
                    .Ascending(x => x.Name)
                    .Ascending(x => x.PitchClass)
            )
        );

        // Intervals indexes
        await Intervals.Indexes.CreateOneAsync(
            new CreateIndexModel<IntervalDocument>(
                Builders<IntervalDocument>.IndexKeys
                    .Ascending(x => x.Name)
                    .Ascending(x => x.Semitones)
            )
        );

        // Keys indexes
        await Keys.Indexes.CreateOneAsync(
            new CreateIndexModel<KeyDocument>(
                Builders<KeyDocument>.IndexKeys
                    .Ascending(x => x.Name)
                    .Ascending(x => x.Root)
            )
        );

        // Scales indexes
        await Scales.Indexes.CreateManyAsync([
            new CreateIndexModel<ScaleDocument>(
                Builders<ScaleDocument>.IndexKeys
                    .Ascending(x => x.Name)
                    .Ascending(x => x.Notes)
            )
        ]);

        // PitchClasses indexes
        await PitchClasses.Indexes.CreateOneAsync(
            new CreateIndexModel<PitchClassDocument>(
                Builders<PitchClassDocument>.IndexKeys
                    .Ascending(x => x.Value)
            )
        );

        // Instruments indexes
        await Instruments.Indexes.CreateOneAsync(
            new CreateIndexModel<InstrumentDocument>(
                Builders<InstrumentDocument>.IndexKeys
                    .Ascending(x => x.Name)
                    .Ascending(x => x.Family)
            )
        );

        // Chords indexes
        await Chords.Indexes.CreateManyAsync([
            new CreateIndexModel<ChordDocument>(
                Builders<ChordDocument>.IndexKeys
                    .Ascending(x => x.Name)
                    .Ascending(x => x.Root)
                    .Ascending(x => x.Quality)
            ),
            new CreateIndexModel<ChordDocument>(
                Builders<ChordDocument>.IndexKeys
                    .Ascending("relatedScales.name")
            )
        ]);

        // Arpeggios indexes
        await Arpeggios.Indexes.CreateOneAsync(
            new CreateIndexModel<ArpeggioDocument>(
                Builders<ArpeggioDocument>.IndexKeys
                    .Ascending(x => x.Name)
                    .Ascending(x => x.Root)
            )
        );

        // Progressions indexes
        await Progressions.Indexes.CreateOneAsync(
            new CreateIndexModel<ProgressionDocument>(
                Builders<ProgressionDocument>.IndexKeys
                    .Ascending(x => x.Name)
                    .Ascending(x => x.Key)
            )
        );

        // PitchClassSets indexes
        await PitchClassSets.Indexes.CreateManyAsync([
            new CreateIndexModel<PitchClassSetDocument>(
                Builders<PitchClassSetDocument>.IndexKeys
                    .Ascending(x => x.Value)
            ),
            new CreateIndexModel<PitchClassSetDocument>(
                Builders<PitchClassSetDocument>.IndexKeys
                    .Ascending(x => x.Cardinality)
            )
        ]);

        // SetClasses indexes
        await SetClasses.Indexes.CreateOneAsync(
            new CreateIndexModel<SetClassDocument>(
                Builders<SetClassDocument>.IndexKeys
                    .Ascending(x => x.PrimeFormId)
                    .Ascending(x => x.Cardinality)
                    .Ascending(x => x.IntervalClassVector)
            )
        );
    }

    public async Task CreateRagIndexesAsync()
    {
        // Chord RAG indexes
        await ChordsRag.Indexes.CreateManyAsync([
            new CreateIndexModel<ChordRagDocument>(
                Builders<ChordRagDocument>.IndexKeys
                    .Ascending(x => x.Name)
                    .Ascending(x => x.Root)
                    .Ascending(x => x.Quality)
            ),
            new CreateIndexModel<ChordRagDocument>(
                Builders<ChordRagDocument>.IndexKeys
                    .Ascending(x => x.Embedding)
            ),
            new CreateIndexModel<ChordRagDocument>(
                Builders<ChordRagDocument>.IndexKeys
                    .Ascending("RelatedScales.Name")
            ),
            new CreateIndexModel<ChordRagDocument>(
                Builders<ChordRagDocument>.IndexKeys
                    .Ascending("CommonProgressions.Name")
            )
        ]);
    }
}