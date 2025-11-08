# MongoDB Local Setup Guide for Guitar Alchemist

This guide shows how to import and query the Guitar Alchemist chord data using your local MongoDB instance.

## Prerequisites

- ✅ Local MongoDB instance running
- ✅ MongoDB command-line tools installed
- ✅ Exported chord data from GaDataCLI

## Quick Start

### Step 1: Verify MongoDB is Running

```bash
# Check if MongoDB is running
mongo --eval "db.version()"

# Or with mongosh (newer versions)
mongosh --eval "db.version()"
```

Expected output: MongoDB version number (e.g., "7.0.0")

### Step 2: Export Chord Data

If you haven't already, export the chord data:

```bash
cd C:\Users\spare\source\repos\ga
dotnet run --project Apps/GaDataCLI/GaDataCLI.csproj -- -e chords -o C:\Temp\GaExport -q
```

This creates `C:\Temp\GaExport\all-chords.json` with 427,254 chords (376.59 MB).

### Step 3: Import Data into Local MongoDB

```bash
mongoimport --db guitar-alchemist \
  --collection chords \
  --file C:\Temp\GaExport\all-chords.json \
  --jsonArray
```

**Windows PowerShell:**
```powershell
mongoimport --db guitar-alchemist `
  --collection chords `
  --file C:\Temp\GaExport\all-chords.json `
  --jsonArray
```

**Expected Output:**
```
2025-10-03T18:00:00.000+0000    connected to: mongodb://localhost/
2025-10-03T18:00:00.000+0000    427254 document(s) imported successfully. 0 document(s) failed to import.
```

### Step 4: Verify Import

```bash
# Connect to MongoDB
mongosh

# Switch to database
use guitar-alchemist

# Count documents
db.chords.countDocuments()
// Should return: 427254

# View a sample chord
db.chords.findOne()
```

### Step 5: Create Indexes

```javascript
// Connect to MongoDB
use guitar-alchemist

// Create compound index for common queries
db.chords.createIndex({ Quality: 1, Extension: 1, StackingType: 1 })

// Create index for pitch class set
db.chords.createIndex({ PitchClassSet: 1 })

// Create index for parent scale queries
db.chords.createIndex({ ParentScale: 1, ScaleDegree: 1 })

// Create text index for name search
db.chords.createIndex({ Name: "text", Description: "text" })

// Verify indexes
db.chords.getIndexes()
```

## Example Queries

### Basic Queries

**Find all major seventh chords:**
```javascript
db.chords.find({
  Quality: "Major",
  Extension: "Seventh"
}).limit(10)
```

**Find specific chord by name:**
```javascript
db.chords.findOne({ Name: "C Major 7th" })
```

**Count chords by quality:**
```javascript
db.chords.countDocuments({ Quality: "Major" })
```

### Interval-Based Queries

**Find chords with major third:**
```javascript
db.chords.find({
  "Intervals": {
    $elemMatch: {
      Semitones: 4,
      Function: "MajorThird"
    }
  }
}).limit(10)
```

**Find dominant 7th chords (major 3rd + minor 7th):**
```javascript
db.chords.find({
  Intervals: {
    $all: [
      { $elemMatch: { Semitones: 4, Function: "MajorThird" } },
      { $elemMatch: { Semitones: 10, Function: "MinorSeventh" } }
    ]
  }
})
```

### Aggregation Queries

**Count chords by quality:**
```javascript
db.chords.aggregate([
  {
    $group: {
      _id: "$Quality",
      count: { $sum: 1 }
    }
  },
  {
    $sort: { count: -1 }
  }
])
```

**Find chords by scale degree in Major scale:**
```javascript
db.chords.aggregate([
  {
    $match: {
      ParentScale: "Major"
    }
  },
  {
    $group: {
      _id: "$ScaleDegree",
      chords: { $push: "$Name" },
      count: { $sum: 1 }
    }
  },
  {
    $sort: { _id: 1 }
  }
])
```

**Statistics by stacking type:**
```javascript
db.chords.aggregate([
  {
    $group: {
      _id: "$StackingType",
      count: { $sum: 1 },
      avgNoteCount: { $avg: "$NoteCount" },
      minNotes: { $min: "$NoteCount" },
      maxNotes: { $max: "$NoteCount" }
    }
  },
  {
    $sort: { count: -1 }
  }
])
```

### Text Search

**Search for "dominant" chords:**
```javascript
db.chords.find({
  $text: { $search: "dominant" }
}).limit(10)
```

**Search with relevance score:**
```javascript
db.chords.find(
  { $text: { $search: "major seventh" } },
  { score: { $meta: "textScore" } }
).sort({ score: { $meta: "textScore" } }).limit(10)
```

## C# Integration

### Install MongoDB Driver

```bash
cd Apps/ga-server/GaApi
dotnet add package MongoDB.Driver
```

### Create MongoDB Service

Create `Services/MongoDbService.cs`:

```csharp
using MongoDB.Driver;
using MongoDB.Bson;

namespace GaApi.Services;

public class MongoDbService
{
    private readonly IMongoDatabase _database;
    private readonly IMongoCollection<BsonDocument> _chords;

    public MongoDbService(IConfiguration configuration)
    {
        var connectionString = configuration["MongoDB:ConnectionString"];
        var databaseName = configuration["MongoDB:DatabaseName"];
        
        var client = new MongoClient(connectionString);
        _database = client.GetDatabase(databaseName);
        _chords = _database.GetCollection<BsonDocument>("chords");
    }

    public async Task<List<BsonDocument>> GetChordsByQualityAsync(string quality, int limit = 10)
    {
        var filter = Builders<BsonDocument>.Filter.Eq("Quality", quality);
        return await _chords.Find(filter).Limit(limit).ToListAsync();
    }

    public async Task<BsonDocument?> GetChordByNameAsync(string name)
    {
        var filter = Builders<BsonDocument>.Filter.Eq("Name", name);
        return await _chords.Find(filter).FirstOrDefaultAsync();
    }

    public async Task<List<BsonDocument>> FindChordsByIntervalsAsync(
        int semitones, 
        string function, 
        int limit = 10)
    {
        var filter = Builders<BsonDocument>.Filter.ElemMatch<BsonValue>(
            "Intervals",
            new BsonDocument {
                { "Semitones", semitones },
                { "Function", function }
            }
        );
        return await _chords.Find(filter).Limit(limit).ToListAsync();
    }

    public async Task<List<BsonDocument>> SearchChordsAsync(string searchText, int limit = 10)
    {
        var filter = Builders<BsonDocument>.Filter.Text(searchText);
        var projection = Builders<BsonDocument>.Projection
            .Include("Name")
            .Include("Quality")
            .Include("Extension")
            .MetaTextScore("score");
        
        return await _chords.Find(filter)
            .Project(projection)
            .Sort(Builders<BsonDocument>.Sort.MetaTextScore("score"))
            .Limit(limit)
            .ToListAsync();
    }

    public async Task<Dictionary<string, int>> GetChordCountByQualityAsync()
    {
        var pipeline = new[]
        {
            new BsonDocument("$group", new BsonDocument
            {
                { "_id", "$Quality" },
                { "count", new BsonDocument("$sum", 1) }
            }),
            new BsonDocument("$sort", new BsonDocument("count", -1))
        };

        var results = await _chords.Aggregate<BsonDocument>(pipeline).ToListAsync();
        
        return results.ToDictionary(
            doc => doc["_id"].AsString,
            doc => doc["count"].AsInt32
        );
    }
}
```

### Register Service in Program.cs

```csharp
// Add to Program.cs
builder.Services.AddSingleton<MongoDbService>();
```

### Create API Controller

Create `Controllers/ChordsController.cs`:

```csharp
using Microsoft.AspNetCore.Mvc;
using GaApi.Services;
using MongoDB.Bson;

namespace GaApi.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ChordsController : ControllerBase
{
    private readonly MongoDbService _mongoDb;

    public ChordsController(MongoDbService mongoDb)
    {
        _mongoDb = mongoDb;
    }

    [HttpGet("quality/{quality}")]
    public async Task<IActionResult> GetByQuality(string quality, [FromQuery] int limit = 10)
    {
        var chords = await _mongoDb.GetChordsByQualityAsync(quality, limit);
        return Ok(chords);
    }

    [HttpGet("name/{name}")]
    public async Task<IActionResult> GetByName(string name)
    {
        var chord = await _mongoDb.GetChordByNameAsync(name);
        if (chord == null)
            return NotFound();
        return Ok(chord);
    }

    [HttpGet("search")]
    public async Task<IActionResult> Search([FromQuery] string q, [FromQuery] int limit = 10)
    {
        var chords = await _mongoDb.SearchChordsAsync(q, limit);
        return Ok(chords);
    }

    [HttpGet("stats/quality")]
    public async Task<IActionResult> GetQualityStats()
    {
        var stats = await _mongoDb.GetChordCountByQualityAsync();
        return Ok(stats);
    }

    [HttpGet("intervals")]
    public async Task<IActionResult> GetByInterval(
        [FromQuery] int semitones, 
        [FromQuery] string function,
        [FromQuery] int limit = 10)
    {
        var chords = await _mongoDb.FindChordsByIntervalsAsync(semitones, function, limit);
        return Ok(chords);
    }
}
```

### Test API Endpoints

```bash
# Start the API
dotnet run --project Apps/ga-server/GaApi

# Test endpoints
curl http://localhost:5000/api/chords/quality/Major?limit=5
curl http://localhost:5000/api/chords/name/C%20Major%207th
curl http://localhost:5000/api/chords/search?q=dominant&limit=10
curl http://localhost:5000/api/chords/stats/quality
curl http://localhost:5000/api/chords/intervals?semitones=4&function=MajorThird&limit=5
```

## Local MongoDB vs. Atlas

### Available in Local MongoDB ✅
- All CRUD operations
- Aggregation pipelines
- Text search with indexes
- Geospatial queries
- Transactions (replica set required)
- Change streams (replica set required)
- All standard MongoDB features

### Atlas-Only Features ❌
- **Atlas Vector Search** - Semantic similarity search
- **Atlas Search** - Advanced Lucene-based search
- **Atlas Data Federation** - Query across multiple sources
- **Atlas Charts** - Built-in visualization
- **Automated backups** - Point-in-time recovery
- **Global clusters** - Multi-region deployment

### When to Migrate to Atlas

Consider migrating to MongoDB Atlas if you need:
1. **Vector Search** for semantic chord similarity
2. **Advanced search** with facets and autocomplete
3. **Managed hosting** with automatic scaling
4. **Global distribution** for low-latency access
5. **Automated backups** and monitoring

### Migration is Easy

```bash
# Export from local MongoDB
mongodump --db guitar-alchemist --out ./backup

# Import to Atlas
mongorestore --uri "mongodb+srv://..." --db guitar-alchemist ./backup/guitar-alchemist
```

## Performance Tips

### 1. Use Indexes Wisely

```javascript
// Check query performance
db.chords.find({ Quality: "Major" }).explain("executionStats")

// Look for:
// - executionTimeMillis (should be low)
// - totalDocsExamined (should be close to nReturned)
// - stage: "IXSCAN" (means index was used)
```

### 2. Optimize Aggregations

```javascript
// Put $match early in pipeline
db.chords.aggregate([
  { $match: { Quality: "Major" } },  // Filter first
  { $group: { _id: "$Extension", count: { $sum: 1 } } }
])
```

### 3. Use Projections

```javascript
// Only return needed fields
db.chords.find(
  { Quality: "Major" },
  { Name: 1, Quality: 1, Extension: 1, _id: 0 }
)
```

### 4. Batch Operations

```javascript
// Use cursor for large result sets
const cursor = db.chords.find({ Quality: "Major" });
while (cursor.hasNext()) {
  const chord = cursor.next();
  // Process chord
}
```

## Troubleshooting

### Import Fails

**Error: "Failed to connect to localhost:27017"**
- Ensure MongoDB is running: `sudo systemctl start mongod` (Linux) or check Services (Windows)
- Check MongoDB port: Default is 27017

**Error: "Failed to parse JSON"**
- Verify JSON file is valid
- Check file encoding (should be UTF-8)
- Ensure `--jsonArray` flag is used

### Slow Queries

**Query takes too long:**
1. Check if indexes exist: `db.chords.getIndexes()`
2. Analyze query plan: `db.chords.find({...}).explain("executionStats")`
3. Create appropriate index
4. Consider using aggregation pipeline

### Memory Issues

**Error: "Cursor not found"**
- Increase cursor timeout
- Use smaller batch sizes
- Process results in chunks

## Next Steps

1. ✅ Import chord data to local MongoDB
2. ✅ Create indexes for performance
3. ✅ Test queries in mongosh
4. ⬜ Integrate with C# API
5. ⬜ Build API endpoints
6. ⬜ Add more complex queries
7. ⬜ Consider Atlas migration for Vector Search

## Resources

- [MongoDB Manual](https://www.mongodb.com/docs/manual/)
- [MongoDB C# Driver](https://www.mongodb.com/docs/drivers/csharp/)
- [Aggregation Pipeline](https://www.mongodb.com/docs/manual/aggregation/)
- [Query Optimization](https://www.mongodb.com/docs/manual/core/query-optimization/)
- [Indexes](https://www.mongodb.com/docs/manual/indexes/)

## Summary

Your local MongoDB instance is perfect for:
- ✅ Development and testing
- ✅ All standard MongoDB features
- ✅ Fast local queries
- ✅ Full control over data
- ✅ No cloud costs

For AI features like Vector Search, you can:
- Migrate to Atlas later (easy with mongodump/mongorestore)
- Use separate vector database (Pinecone, Weaviate)
- Implement custom similarity in application code

The current setup gives you immediate access to all 427,254 chords with powerful query capabilities!

