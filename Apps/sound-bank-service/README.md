# SoundBankService

FastAPI microservice for AI-powered guitar sound generation using Meta's MusicGen.

## Features

- **AI Sound Generation**: Generate guitar sounds using MusicGen
- **Async Job Queue**: Long-running generation tasks with progress tracking
- **Sample Caching**: Avoid regenerating identical samples
- **Search & Discovery**: Find existing samples by parameters
- **RESTful API**: FastAPI with automatic OpenAPI documentation

## API Endpoints

### `POST /v1/sounds/generate`
Queue a sound generation job.

**Request:**
```json
{
  "instrument": "electric_guitar",
  "string": 3,
  "fret": 5,
  "velocity": 0.7,
  "technique": ["pluck", "palm_mute"],
  "style_prompt": "crunchy rock tone",
  "duration_seconds": 2.0
}
```

**Response:**
```json
{
  "job_id": "6f4e0d0c-...",
  "status": "queued",
  "estimated_seconds": 15,
  "created_at": "2024-01-15T10:30:00Z"
}
```

### `GET /v1/sounds/jobs/{jobId}`
Get status of a generation job.

**Response:**
```json
{
  "job_id": "6f4e0d0c-...",
  "status": "completed",
  "progress": 1.0,
  "status_message": "Sound sample generated successfully",
  "sample": {
    "sample_id": "a1b2c3d4-...",
    "instrument": "electric_guitar",
    "string": 3,
    "fret": 5,
    "file_path": "/samples/a1b2c3d4.wav",
    "file_size_bytes": 352800,
    "sample_rate": 44100,
    "created_at": "2024-01-15T10:30:15Z"
  },
  "created_at": "2024-01-15T10:30:00Z",
  "updated_at": "2024-01-15T10:30:15Z"
}
```

### `GET /v1/sounds/{sampleId}`
Get metadata for a sound sample.

### `GET /v1/sounds/{sampleId}/download`
Download the audio file.

### `POST /v1/sounds/search`
Search for existing samples.

**Request:**
```json
{
  "instrument": "electric_guitar",
  "string": 3,
  "fret": 5,
  "technique": ["pluck"],
  "limit": 10
}
```

### `GET /v1/sounds/jobs`
List generation jobs (with optional status filter).

### `GET /healthz`
Health check endpoint.

### `GET /metrics`
Prometheus metrics.

## Development Phases

### Phase 1 (Current): Mock Generation
- ✅ FastAPI service structure
- ✅ Async job queue with background tasks
- ✅ API endpoints with Pydantic models
- ✅ Placeholder audio generation (silence WAV files)
- ✅ In-memory job/sample storage
- ✅ Docker containerization

### Phase 2: MusicGen Integration
- ⏳ Load MusicGen model from Hugging Face
- ⏳ Real audio generation with AI
- ⏳ GPU acceleration support
- ⏳ Redis job queue for scalability
- ⏳ MongoDB for sample metadata
- ⏳ MinIO/S3 for audio file storage

### Phase 3: Advanced Features
- ⏳ Sample caching and deduplication
- ⏳ Multi-model support (different MusicGen variants)
- ⏳ Audio post-processing (EQ, compression, reverb)
- ⏳ Batch generation
- ⏳ WebSocket progress updates

## Running Locally

### Python (Development)
```bash
cd Apps/sound-bank-service
pip install -r requirements.txt
python main.py
```

Service runs on http://localhost:8080

### Docker
```bash
cd Apps/sound-bank-service
docker build -t sound-bank-service .
docker run -p 8080:8080 -v $(pwd)/samples:/app/samples sound-bank-service
```

### With Aspire (Production)
```bash
cd ../..
dotnet run --project AllProjects.AppHost
```

## Testing

```bash
# Health check
curl http://localhost:8080/healthz

# Generate a sound
curl -X POST http://localhost:8080/v1/sounds/generate \
  -H "Content-Type: application/json" \
  -d '{
    "instrument": "electric_guitar",
    "string": 3,
    "fret": 5,
    "velocity": 0.7,
    "technique": ["pluck"],
    "duration_seconds": 2.0
  }'

# Check job status
curl http://localhost:8080/v1/sounds/jobs/{job_id}

# Download sample
curl http://localhost:8080/v1/sounds/{sample_id}/download -o sample.wav

# Interactive API docs
open http://localhost:8080/docs
```

## Dependencies

- **FastAPI**: Web framework
- **Uvicorn**: ASGI server
- **Pydantic**: Data validation
- **AudioCraft** (Phase 2): Meta's MusicGen
- **PyTorch** (Phase 2): Model inference
- **Redis** (Phase 2): Job queue
- **MongoDB** (Phase 2): Sample metadata
- **MinIO/S3** (Phase 2): Audio storage

## Configuration

Environment variables:
- `PORT`: Service port (default: 8080)
- `LOG_LEVEL`: Logging level (default: INFO)
- `MODEL_PATH`: Path to MusicGen model (Phase 2)
- `REDIS_URL`: Redis connection string (Phase 2)
- `MONGODB_URL`: MongoDB connection string (Phase 2)
- `S3_ENDPOINT`: S3/MinIO endpoint (Phase 2)
- `S3_BUCKET`: Storage bucket name (Phase 2)

## Architecture

```
┌─────────────────┐
│   GaApi (.NET)  │
│                 │
│  HTTP Client    │
└────────┬────────┘
         │
         │ HTTP/REST
         │
┌────────▼────────────┐
│  SoundBankService   │
│     (Python)        │
│                     │
│  ┌──────────────┐   │
│  │   FastAPI    │   │
│  └──────┬───────┘   │
│         │           │
│  ┌──────▼───────┐   │
│  │ Job Queue    │   │
│  │  (Redis)     │   │
│  └──────┬───────┘   │
│         │           │
│  ┌──────▼───────┐   │
│  │  MusicGen    │   │
│  │   (PyTorch)  │   │
│  └──────┬───────┘   │
│         │           │
│  ┌──────▼───────┐   │
│  │   Storage    │   │
│  │ (MinIO/S3)   │   │
│  └──────────────┘   │
└─────────────────────┘
```

## Sample Storage Structure

```
samples/
├── {sample_id_1}.wav
├── {sample_id_2}.wav
└── ...
```

## Job Lifecycle

1. **QUEUED**: Job created, waiting for processing
2. **PROCESSING**: MusicGen generating audio
3. **COMPLETED**: Audio generated and stored
4. **FAILED**: Generation failed with error

## License

Part of Guitar Alchemist project.

