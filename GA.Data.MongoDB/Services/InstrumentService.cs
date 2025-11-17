namespace GA.Data.MongoDB.Services;

using EntityFramework.Data.Instruments;
using Models;

public class InstrumentService : IInstrumentService
{
    private readonly IMongoCollection<InstrumentDocument> _instruments;

    public InstrumentService(IMongoDatabase database)
    {
        _instruments = database.GetCollection<InstrumentDocument>("instruments");
        CreateIndexes();
    }

    public async Task<InstrumentDocument> CreateInstrumentAsync(InstrumentsRepository.InstrumentInfo instrumentInfo)
    {
        var timestamp = DateTime.UtcNow;
        var stringCount = GetStringCount(instrumentInfo);

        var document = new InstrumentDocument
        {
            Name = instrumentInfo.Name,
            Category = DetermineInstrumentCategory(instrumentInfo.Name),
            StringCount = stringCount,
            Family = DetermineInstrumentFamily(instrumentInfo.Name),
            Tunings = [.. instrumentInfo.Tunings
                .Where(t => !string.IsNullOrWhiteSpace(t.Value.Tuning))
                .Select(t => new TuningDocument
                {
                    Name = t.Value.Name,
                    Notes = [.. t.Value.Tuning.Split(' ', StringSplitOptions.RemoveEmptyEntries)],
                    IsStandard = t.Value.Name.Contains("Standard", StringComparison.OrdinalIgnoreCase),
                    Description = GetTuningDescription(t.Value)
                })],
            Description = $"Standard {instrumentInfo.Name} with {instrumentInfo.Tunings.Count} available tunings",
            Metadata = new Dictionary<string, string>
            {
                ["stringCount"] = stringCount.ToString(),
                ["hasStandardTuning"] = instrumentInfo.Tunings.Any(t =>
                    t.Value.Name.Contains("Standard", StringComparison.OrdinalIgnoreCase)).ToString()
            },
            CreatedAt = timestamp,
            UpdatedAt = timestamp
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
            .Set(i => i.Tunings, [.. instrumentInfo.Tunings.Select(t => new TuningDocument
            {
                Name = t.Value.Name,
                Notes = [.. t.Value.Tuning.Split(' ')],
                IsStandard = t.Value.Name.Contains("Standard", StringComparison.OrdinalIgnoreCase),
                Description = null
            })])
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
            new BsonRegularExpression(searchTerm, "i"));

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

    private void CreateIndexes()
    {
        var indexKeysDefinition = Builders<InstrumentDocument>.IndexKeys.Ascending(i => i.Name);
        var indexOptions = new CreateIndexOptions { Unique = true };
        var indexModel = new CreateIndexModel<InstrumentDocument>(indexKeysDefinition, indexOptions);
        _instruments.Indexes.CreateOne(indexModel);
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

    private static string DetermineInstrumentCategory(string instrumentName)
    {
        return instrumentName.ToLowerInvariant() switch
        {
            var name when name.Contains("guitar") => "Guitar",
            var name when name.Contains("bass") => "Bass",
            var name when name.Contains("ukulele") => "Ukulele",
            var name when name.Contains("banjo") => "Banjo",
            _ => "Other"
        };
    }

    private static string DetermineInstrumentFamily(string instrumentName)
    {
        return instrumentName.ToLowerInvariant() switch
        {
            var name when name.Contains("guitar") || name.Contains("bass") || name.Contains("ukulele") ||
                          name.Contains("banjo")
                => "String",
            var name when name.Contains("saxophone") || name.Contains("flute") || name.Contains("clarinet")
                => "Wind",
            var name when name.Contains("drum") || name.Contains("percussion")
                => "Percussion",
            _ => "Other"
        };
    }

    private static int GetStringCount(InstrumentsRepository.InstrumentInfo instrument)
    {
        var standardTuning = instrument.Tunings.Values
            .FirstOrDefault(t => t.Name.Contains("Standard", StringComparison.OrdinalIgnoreCase));

        if (standardTuning != null)
        {
            return standardTuning.Tuning.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length;
        }

        return instrument.Tunings.Values
            .FirstOrDefault()?.Tuning.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length ?? 0;
    }

    private static string? GetTuningDescription(InstrumentsRepository.TuningInfo tuningInfo)
    {
        var fullNameProp = tuningInfo.TuningInstance.GetType().GetProperty("FullName");
        return fullNameProp?.GetValue(tuningInfo.TuningInstance) as string;
    }
}
