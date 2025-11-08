# ✅ BSP Room Generation with MongoDB Persistence - COMPLETE

## 🎯 What We Built

A complete **backend room generation system with MongoDB persistence and job queuing** for the BSP DOOM Explorer. Rooms are generated once, stored in MongoDB, and can be retrieved instantly.

---

## 📁 Files Created/Modified

### New Files

1. **`Apps/ga-server/GaApi/Models/MusicRoomDocument.cs`** (250 lines)
   - `MusicRoomDocument` - MongoDB document for persisted layouts
   - `RoomGenerationJob` - Job queue document
   - `JobStatus` enum - Job state tracking
   - BSON attributes for MongoDB serialization

2. **`Apps/ga-server/GaApi/Services/MusicRoomService.cs`** (300 lines)
   - Complete service for room generation and persistence
   - Job queue management
   - MongoDB CRUD operations
   - Automatic indexing

3. **`BSP_MONGODB_PERSISTENCE_COMPLETE.md`** (this file)
   - Complete documentation
   - API reference
   - Usage examples

### Modified Files

4. **`Apps/ga-server/GaApi/Services/MongoDbService.cs`**
   - Added `Database` property for service access
   - Added `MusicRoomLayouts` collection
   - Added `RoomGenerationJobs` collection

5. **`Apps/ga-server/GaApi/Controllers/MusicRoomController.cs`**
   - Updated to use `MusicRoomService`
   - Added job queue endpoints
   - Added layout management endpoints

6. **`Apps/ga-server/GaApi/Program.cs`**
   - Registered `MusicRoomService` as singleton

---

## 🏗️ Architecture

```
┌─────────────────────────────────────────────────────────────┐
│                  MusicRoomController                        │
│  • Queue generation jobs                                   │
│  • Process jobs                                            │
│  • Get job status                                          │
│  • Manage layouts                                          │
└─────────────────────────────────────────────────────────────┘
                            │
                            ▼
┌─────────────────────────────────────────────────────────────┐
│                  MusicRoomService                           │
│  • QueueGenerationAsync()                                  │
│  • ProcessJobAsync()                                       │
│  • GenerateAndPersistAsync()                               │
│  • GetLayoutAsync()                                        │
└─────────────────────────────────────────────────────────────┘
                            │
                            ▼
┌─────────────────────────────────────────────────────────────┐
│                    MongoDB Collections                      │
│  • musicRoomLayouts (persisted layouts)                    │
│  • roomGenerationJobs (job queue)                          │
└─────────────────────────────────────────────────────────────┘
```

---

## 🔌 API Endpoints

### 1. Generate and Persist Floor Layout

**Endpoint**: `GET /api/music-rooms/floor/{floor}`

Generates a room layout and persists it to MongoDB. Returns cached layout if already exists.

**Parameters**:
- `floor` (path, required): Floor number (0-5)
- `floorSize` (query, optional): Size of the floor (default: 100)
- `seed` (query, optional): Seed for reproducible generation

**Response**:
```json
{
  "success": true,
  "data": {
    "floor": 0,
    "floorName": "Set Classes",
    "rooms": [...],
    "corridors": [...]
  }
}
```

**Example**:
```bash
curl https://localhost:7001/api/music-rooms/floor/0?floorSize=100&seed=42
```

---

### 2. Queue Room Generation Job

**Endpoint**: `POST /api/music-rooms/queue`

Queues a room generation job for background processing.

**Request Body**:
```json
{
  "floor": 0,
  "floorSize": 100,
  "seed": 42
}
```

**Response**:
```json
{
  "success": true,
  "data": {
    "id": "507f1f77bcf86cd799439011",
    "floor": 0,
    "floorSize": 100,
    "seed": 42,
    "status": "Pending",
    "createdAt": "2025-01-30T10:00:00Z"
  }
}
```

**Example**:
```bash
curl -X POST https://localhost:7001/api/music-rooms/queue \
  -H "Content-Type: application/json" \
  -d '{"floor": 0, "floorSize": 100, "seed": 42}'
```

---

### 3. Get Job Status

**Endpoint**: `GET /api/music-rooms/jobs/{jobId}`

Get the status of a queued job.

**Response**:
```json
{
  "success": true,
  "data": {
    "id": "507f1f77bcf86cd799439011",
    "floor": 0,
    "status": "Completed",
    "createdAt": "2025-01-30T10:00:00Z",
    "startedAt": "2025-01-30T10:00:01Z",
    "completedAt": "2025-01-30T10:00:05Z",
    "resultId": "507f1f77bcf86cd799439012",
    "processingTimeMs": 4523
  }
}
```

**Example**:
```bash
curl https://localhost:7001/api/music-rooms/jobs/507f1f77bcf86cd799439011
```

---

### 4. Process Queued Job

**Endpoint**: `POST /api/music-rooms/jobs/{jobId}/process`

Process a pending job immediately.

**Response**:
```json
{
  "success": true,
  "data": {
    "floor": 0,
    "floorName": "Set Classes",
    "rooms": [...],
    "corridors": [...]
  }
}
```

**Example**:
```bash
curl -X POST https://localhost:7001/api/music-rooms/jobs/507f1f77bcf86cd799439011/process
```

---

### 5. Get Pending Jobs

**Endpoint**: `GET /api/music-rooms/jobs/pending`

Get all pending jobs in the queue.

**Parameters**:
- `limit` (query, optional): Maximum number of jobs to return (default: 10)

**Response**:
```json
{
  "success": true,
  "data": [
    {
      "id": "507f1f77bcf86cd799439011",
      "floor": 0,
      "status": "Pending",
      "createdAt": "2025-01-30T10:00:00Z"
    },
    ...
  ]
}
```

**Example**:
```bash
curl https://localhost:7001/api/music-rooms/jobs/pending?limit=20
```

---

### 6. Get Floor Layouts

**Endpoint**: `GET /api/music-rooms/floor/{floor}/layouts`

Get all persisted layouts for a specific floor.

**Parameters**:
- `floor` (path, required): Floor number (0-5)
- `limit` (query, optional): Maximum number of layouts to return (default: 10)

**Response**:
```json
{
  "success": true,
  "data": [
    {
      "id": "507f1f77bcf86cd799439012",
      "floor": 0,
      "floorName": "Set Classes",
      "seed": 42,
      "createdAt": "2025-01-30T10:00:05Z",
      "rooms": [...],
      "corridors": [...]
    },
    ...
  ]
}
```

**Example**:
```bash
curl https://localhost:7001/api/music-rooms/floor/0/layouts?limit=5
```

---

### 7. Delete Layout

**Endpoint**: `DELETE /api/music-rooms/layouts/{layoutId}`

Delete a persisted layout.

**Response**:
```json
{
  "success": true,
  "data": true
}
```

**Example**:
```bash
curl -X DELETE https://localhost:7001/api/music-rooms/layouts/507f1f77bcf86cd799439012
```

---

## 📊 MongoDB Collections

### Collection: `musicRoomLayouts`

Stores generated room layouts.

**Document Structure**:
```json
{
  "_id": ObjectId("507f1f77bcf86cd799439012"),
  "floor": 0,
  "floorName": "Set Classes",
  "floorSize": 100,
  "seed": 42,
  "totalItems": 93,
  "categories": ["Chromatic", "Diatonic", ...],
  "rooms": [
    {
      "id": "floor0_room0",
      "x": 10,
      "y": 15,
      "width": 20,
      "height": 18,
      "centerX": 20,
      "centerY": 24,
      "floor": 0,
      "category": "Chromatic",
      "items": [],
      "color": "hsl(0, 70%, 50%)",
      "description": "Chromatic - Floor 0"
    },
    ...
  ],
  "corridors": [
    {
      "points": [{"x": 20, "y": 24}, {"x": 45, "y": 24}],
      "width": 1
    },
    ...
  ],
  "createdAt": ISODate("2025-01-30T10:00:05Z"),
  "version": "1.0",
  "generationParams": {
    "minRoomSize": 12,
    "maxRoomSize": 24,
    "maxDepth": 7,
    "corridorWidth": 1
  }
}
```

**Indexes**:
- `{ floor: 1, seed: 1 }` - For quick lookups by floor and seed

---

### Collection: `roomGenerationJobs`

Stores job queue for background processing.

**Document Structure**:
```json
{
  "_id": ObjectId("507f1f77bcf86cd799439011"),
  "floor": 0,
  "floorSize": 100,
  "seed": 42,
  "status": "Completed",
  "createdAt": ISODate("2025-01-30T10:00:00Z"),
  "startedAt": ISODate("2025-01-30T10:00:01Z"),
  "completedAt": ISODate("2025-01-30T10:00:05Z"),
  "error": null,
  "resultId": ObjectId("507f1f77bcf86cd799439012"),
  "processingTimeMs": 4523
}
```

**Indexes**:
- `{ status: 1, createdAt: 1 }` - For efficient queue processing

---

## 🚀 Usage Examples

### Example 1: Generate and Cache Layout

```csharp
// Generate floor 0 with seed 42
var response = await client.GetAsync(
    "https://localhost:7001/api/music-rooms/floor/0?floorSize=100&seed=42");

// First call: generates and persists to MongoDB
// Subsequent calls: returns cached layout instantly
```

### Example 2: Queue Background Job

```csharp
// Queue job
var queueResponse = await client.PostAsJsonAsync(
    "https://localhost:7001/api/music-rooms/queue",
    new { floor = 0, floorSize = 100, seed = 42 });

var job = await queueResponse.Content.ReadFromJsonAsync<RoomGenerationJob>();

// Check status
var statusResponse = await client.GetAsync(
    $"https://localhost:7001/api/music-rooms/jobs/{job.Id}");

// Process job
var processResponse = await client.PostAsync(
    $"https://localhost:7001/api/music-rooms/jobs/{job.Id}/process", null);
```

### Example 3: Background Job Processor

```csharp
// Simple background job processor
while (true)
{
    var jobs = await musicRoomService.GetPendingJobsAsync(limit: 5);
    
    foreach (var job in jobs)
    {
        try
        {
            await musicRoomService.ProcessJobAsync(job.Id);
            Console.WriteLine($"Processed job {job.Id}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to process job {job.Id}: {ex.Message}");
        }
    }
    
    await Task.Delay(TimeSpan.FromSeconds(5));
}
```

---

## ✅ Benefits

🚀 **Performance** - Layouts generated once, cached forever  
💾 **Persistence** - Survives server restarts  
🔄 **Reproducibility** - Same seed = same layout  
📊 **Scalability** - Job queue for background processing  
🛠️ **Maintainability** - Single source of truth in MongoDB  
📈 **Analytics** - Track generation times and usage  

---

**Status**: ✅ **PRODUCTION READY - MongoDB persistence complete!** 🎸🎮

