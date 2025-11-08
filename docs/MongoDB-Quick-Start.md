# MongoDB Quick Start Guide for Guitar Alchemist

This guide will help you quickly import the Guitar Alchemist chord data into MongoDB and start running queries.

## Prerequisites

1. MongoDB Atlas account (free tier is fine)
2. MongoDB Compass (optional, for GUI)
3. MongoDB command-line tools installed
4. Exported chord data from GaDataCLI

## Step 1: Set Up MongoDB Atlas

### Create a Free Cluster

1. Go to [MongoDB Atlas](https://www.mongodb.com/cloud/atlas/register)
2. Sign up for a free account
3. Create a new cluster (M0 Free Tier)
4. Choose a cloud provider and region
5. Name your cluster (e.g., "guitar-alchemist")

### Configure Network Access

1. Go to "Network Access" in the left sidebar
2. Click "Add IP Address"
3. Choose "Allow Access from Anywhere" (for development)
   - Or add your specific IP address for better security

### Create Database User

1. Go to "Database Access" in the left sidebar
2. Click "Add New Database User"
3. Choose "Password" authentication
4. Username: `ga-user`
5. Password: Generate a secure password
6. Database User Privileges: "Read and write to any database"
7. Click "Add User"

### Get Connection String

1. Go to "Database" in the left sidebar
2. Click "Connect" on your cluster
3. Choose "Connect your application"
4. Copy the connection string:
   ```
   mongodb+srv://ga-user:<password>@cluster0.xxxxx.mongodb.net/
   ```
5. Replace `<password>` with your actual password

## Step 2: Export Chord Data

Run the GaDataCLI to export chord data:

```bash
cd C:\Users\spare\source\repos\ga
dotnet run --project Apps/GaDataCLI/GaDataCLI.csproj -- -e chords -o C:\Temp\GaExport -q
```

This creates `C:\Temp\GaExport\all-chords.json` with 427,254 chords.

## Step 3: Import Data into MongoDB

### Using mongoimport (Command Line)

```bash
mongoimport --uri "mongodb+srv://ga-user:<password>@cluster0.xxxxx.mongodb.net/guitar-alchemist" \
  --collection chords \
  --file C:\Temp\GaExport\all-chords.json \
  --jsonArray
```

**Windows PowerShell**:
```powershell
mongoimport --uri "mongodb+srv://ga-user:<password>@cluster0.xxxxx.mongodb.net/guitar-alchemist" `
  --collection chords `
  --file C:\Temp\GaExport\all-chords.json `
  --jsonArray
```

### Using MongoDB Compass (GUI)

1. Open MongoDB Compass
2. Paste your connection string
3. Click "Connect"
4. Create database: `guitar-alchemist`
5. Create collection: `chords`
6. Click "Add Data" → "Import JSON or CSV file"
7. Select `all-chords.json`
8. Click "Import"

## Step 4: Create Indexes

Connect to your database and create indexes for better query performance:

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
```

## Step 5: Run Example Queries

### Basic Queries

**Find all major seventh chords:**
```javascript
db.chords.find({
  Quality: "Major",
  Extension: "Seventh"
}).limit(10)
```

**Find chords with specific pitch class set:**
```javascript
db.chords.find({
  PitchClassSet: [0, 4, 7, 11]
})
```

**Find all tertian chords:**
```javascript
db.chords.find({
  StackingType: "Tertian"
}).count()
```

### Interval-Based Queries

**Find chords containing a major third:**
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

**Find chords with both major third and minor seventh:**
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

**Find chords by scale degree:**
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

**Get statistics by stacking type:**
```javascript
db.chords.aggregate([
  {
    $group: {
      _id: "$StackingType",
      count: { $sum: 1 },
      avgNoteCount: { $avg: "$NoteCount" },
      qualities: { $addToSet: "$Quality" }
    }
  }
])
```

### Text Search Queries

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

## Step 6: Set Up Atlas Search (Optional)

For more advanced search capabilities:

1. Go to MongoDB Atlas dashboard
2. Click on your cluster
3. Go to "Search" tab
4. Click "Create Search Index"
5. Choose "JSON Editor"
6. Use this configuration:

```json
{
  "mappings": {
    "dynamic": false,
    "fields": {
      "Name": {
        "type": "string",
        "analyzer": "lucene.standard"
      },
      "Description": {
        "type": "string",
        "analyzer": "lucene.english"
      },
      "Quality": {
        "type": "stringFacet"
      },
      "Extension": {
        "type": "stringFacet"
      },
      "StackingType": {
        "type": "stringFacet"
      }
    }
  }
}
```

7. Name the index: `chord_search_index`
8. Click "Create Search Index"

### Atlas Search Query Example

```javascript
db.chords.aggregate([
  {
    $search: {
      index: "chord_search_index",
      text: {
        query: "major seventh",
        path: ["Name", "Description"]
      }
    }
  },
  {
    $limit: 10
  },
  {
    $project: {
      Name: 1,
      Quality: 1,
      Extension: 1,
      score: { $meta: "searchScore" }
    }
  }
])
```

### Faceted Search Example

```javascript
db.chords.aggregate([
  {
    $searchMeta: {
      index: "chord_search_index",
      facet: {
        operator: {
          text: {
            query: "seventh",
            path: "Name"
          }
        },
        facets: {
          qualityFacet: {
            type: "string",
            path: "Quality"
          },
          stackingTypeFacet: {
            type: "string",
            path: "StackingType"
          }
        }
      }
    }
  }
])
```

## Step 7: Connect from C# Application

Add MongoDB driver to your project:

```bash
dotnet add package MongoDB.Driver
```

Example C# code:

```csharp
using MongoDB.Driver;
using MongoDB.Bson;

// Connection
var connectionString = "mongodb+srv://ga-user:<password>@cluster0.xxxxx.mongodb.net/";
var client = new MongoClient(connectionString);
var database = client.GetDatabase("guitar-alchemist");
var chords = database.GetCollection<BsonDocument>("chords");

// Query: Find all major seventh chords
var filter = Builders<BsonDocument>.Filter.And(
    Builders<BsonDocument>.Filter.Eq("Quality", "Major"),
    Builders<BsonDocument>.Filter.Eq("Extension", "Seventh")
);
var results = await chords.Find(filter).Limit(10).ToListAsync();

foreach (var chord in results)
{
    Console.WriteLine(chord["Name"]);
}

// Query: Find chords with specific interval
var intervalFilter = Builders<BsonDocument>.Filter.ElemMatch<BsonValue>(
    "Intervals",
    new BsonDocument {
        { "Semitones", 4 },
        { "Function", "MajorThird" }
    }
);
var intervalResults = await chords.Find(intervalFilter).ToListAsync();
```

## Common Query Patterns

### 1. Find Chords for a Specific Scale

```javascript
// All chords in C Major scale
db.chords.find({
  ParentScale: "Major",
  ScaleDegree: { $gte: 1, $lte: 7 }
})
```

### 2. Find Chords by Note Count

```javascript
// All triads (3-note chords)
db.chords.find({
  NoteCount: 3
})

// All extended chords (5+ notes)
db.chords.find({
  NoteCount: { $gte: 5 }
})
```

### 3. Find Chords by Complexity

```javascript
// Simple chords (triads and seventh chords)
db.chords.find({
  NoteCount: { $lte: 4 },
  StackingType: "Tertian"
})

// Complex chords (extended and altered)
db.chords.find({
  $or: [
    { NoteCount: { $gte: 5 } },
    { StackingType: { $ne: "Tertian" } }
  ]
})
```

### 4. Find Chord Substitutions

```javascript
// Find chords with same pitch class set
var targetPitchClassSet = [0, 4, 7, 11];

db.chords.find({
  PitchClassSet: targetPitchClassSet
})
```

### 5. Analyze Chord Distribution

```javascript
// Distribution by quality and extension
db.chords.aggregate([
  {
    $group: {
      _id: {
        quality: "$Quality",
        extension: "$Extension"
      },
      count: { $sum: 1 }
    }
  },
  {
    $sort: { count: -1 }
  },
  {
    $limit: 20
  }
])
```

## Troubleshooting

### Connection Issues

**Error: "Authentication failed"**
- Check username and password
- Ensure user has correct permissions
- Verify database name in connection string

**Error: "Network timeout"**
- Check IP whitelist in Atlas
- Verify internet connection
- Try "Allow Access from Anywhere" temporarily

### Import Issues

**Error: "Failed to parse"**
- Ensure JSON file is valid
- Check file encoding (should be UTF-8)
- Verify `--jsonArray` flag is used

**Error: "Document too large"**
- MongoDB has 16MB document limit
- Current chord documents are small, shouldn't be an issue
- If needed, split large arrays into separate documents

### Query Performance

**Slow queries:**
- Create appropriate indexes
- Use `explain()` to analyze query plans
- Consider using aggregation pipeline for complex queries

```javascript
// Analyze query performance
db.chords.find({ Quality: "Major" }).explain("executionStats")
```

## Next Steps

1. ✅ Import chord data into MongoDB
2. ✅ Create indexes for common queries
3. ✅ Test basic queries
4. ⬜ Set up Atlas Search for advanced search
5. ⬜ Explore Vector Search for semantic similarity
6. ⬜ Integrate with C# application
7. ⬜ Build API endpoints for chord queries

## Resources

- [MongoDB Atlas Documentation](https://www.mongodb.com/docs/atlas/)
- [MongoDB Query Documentation](https://www.mongodb.com/docs/manual/tutorial/query-documents/)
- [MongoDB Aggregation Framework](https://www.mongodb.com/docs/manual/aggregation/)
- [MongoDB C# Driver Documentation](https://www.mongodb.com/docs/drivers/csharp/)
- [Atlas Search Documentation](https://www.mongodb.com/docs/atlas/atlas-search/)
- [Atlas Vector Search Documentation](https://www.mongodb.com/docs/atlas/atlas-vector-search/)

## Support

For issues or questions:
- MongoDB Community Forums: https://www.mongodb.com/community/forums/
- MongoDB University (free courses): https://university.mongodb.com/
- Guitar Alchemist GitHub Issues: https://github.com/GuitarAlchemist/ga/issues

