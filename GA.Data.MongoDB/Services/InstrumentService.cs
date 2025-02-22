namespace GA.Data.MongoDB.Services;

using GA.Business.Core.Data.Instruments;
using GA.Data.MongoDB.Models;
using global::MongoDB.Driver;

public class InstrumentService : IInstrumentService
{
    private readonly IMongoCollection<InstrumentDocument> _instruments;

    public InstrumentService(IMongoDatabase database)
    {
        _instruments = database.GetCollection<InstrumentDocument>("instruments");
        CreateIndexes();
    }

    private void CreateIndexes()
    {
        var indexKeysDefinition = Builders<InstrumentDocument>.IndexKeys.Ascending(i => i.Name);
        var indexOptions = new CreateIndexOptions { Unique = true };
        var indexModel = new CreateIndexModel<InstrumentDocument>(indexKeysDefinition, indexOptions);
        _instruments.Indexes.CreateOne(indexModel);
    }

    public async Task<InstrumentDocument> CreateInstrumentAsync(InstrumentsRepository.InstrumentInfo instrumentInfo)
    {
        var document = new InstrumentDocument
        {
            Name = instrumentInfo.Name,
            Tunings = instrumentInfo.Tunings.Select(t => new TuningDocument
            {
                Name = t.Value.Name,
                Notes = t.Value.Tuning.Split(' ').ToList(),
                IsStandard = t.Value.Name.Contains("Standard", StringComparison.OrdinalIgnoreCase),
                Description = null
            }).ToList(),
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        await _instruments.InsertOneAsync(document);
        return document;
    }

    public async Task<InstrumentDocument?> GetInstrumentAsync(string name)
    {
        return await _instruments
            .Find(i => i.Name == name)
            .FirstOrDefaultAsync();
    }

    public async Task<List<InstrumentDocument>> GetAllInstrumentsAsync()
    {
        return await _instruments
            .Find(Builders<InstrumentDocument>.Filter.Empty)
            .ToListAsync();
    }

    public async Task<bool> UpdateInstrumentAsync(string name, InstrumentsRepository.InstrumentInfo instrumentInfo)
    {
        var update = Builders<InstrumentDocument>.Update
            .Set(i => i.Name, instrumentInfo.Name)
            .Set(i => i.Tunings, instrumentInfo.Tunings.Select(t => new TuningDocument
            {
                Name = t.Value.Name,
                Notes = t.Value.Tuning.Split(' ').ToList(),
                IsStandard = t.Value.Name.Contains("Standard", StringComparison.OrdinalIgnoreCase),
                Description = null
            }).ToList())
            .Set(i => i.UpdatedAt, DateTime.UtcNow);

        var result = await _instruments.UpdateOneAsync(
            i => i.Name == name,
            update);

        return result.ModifiedCount > 0;
    }

    public async Task<bool> DeleteInstrumentAsync(string name)
    {
        var result = await _instruments.DeleteOneAsync(i => i.Name == name);
        return result.DeletedCount > 0;
    }

    public async Task<List<InstrumentDocument>> SearchInstrumentsAsync(string searchTerm)
    {
        var filter = Builders<InstrumentDocument>.Filter.Regex(
            i => i.Name,
            new global::MongoDB.Bson.BsonRegularExpression(searchTerm, "i"));

        return await _instruments
            .Find(filter)
            .ToListAsync();
    }

    public async Task<bool> ExistsAsync(string name)
    {
        return await _instruments
            .Find(i => i.Name == name)
            .AnyAsync();
    }

    public async Task<List<InstrumentDocument>> GetInstrumentsWithTuningAsync(string tuningName)
    {
        var filter = Builders<InstrumentDocument>.Filter
            .ElemMatch(i => i.Tunings, t => t.Name == tuningName);

        return await _instruments
            .Find(filter)
            .ToListAsync();
    }

    public async Task<bool> AddTuningToInstrumentAsync(string instrumentName, TuningDocument tuning)
    {
        var update = Builders<InstrumentDocument>.Update
            .Push(i => i.Tunings, tuning)
            .Set(i => i.UpdatedAt, DateTime.UtcNow);

        var result = await _instruments.UpdateOneAsync(
            i => i.Name == instrumentName,
            update);

        return result.ModifiedCount > 0;
    }

    public async Task<bool> RemoveTuningFromInstrumentAsync(string instrumentName, string tuningName)
    {
        var update = Builders<InstrumentDocument>.Update
            .PullFilter(i => i.Tunings, t => t.Name == tuningName)
            .Set(i => i.UpdatedAt, DateTime.UtcNow);

        var result = await _instruments.UpdateOneAsync(
            i => i.Name == instrumentName,
            update);

        return result.ModifiedCount > 0;
    }
}