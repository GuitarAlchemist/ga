# AI Music Generation Services

Guitar Alchemist now includes two AI-powered microservices for interactive guitar playing simulation:

1. **HandPoseService** - Computer vision for detecting guitar hand poses
2. **SoundBankService** - AI-powered guitar sound generation

## Architecture Overview

```
┌─────────────────────────────────────────────────────────────┐
│                      GaApi (.NET)                           │
│                                                             │
│  ┌──────────────────────────────────────────────────────┐  │
│  │        GuitarPlayingController                       │  │
│  │  - Orchestrates hand pose + sound generation        │  │
│  │  - Full pipeline: image → positions → sounds        │  │
│  └────────┬─────────────────────────┬───────────────────┘  │
│           │                         │                       │
└───────────┼─────────────────────────┼───────────────────────┘
            │                         │
            │ HTTP/REST               │ HTTP/REST
            │                         │
┌───────────▼──────────┐   ┌──────────▼────────────┐
│  HandPoseService     │   │  SoundBankService     │
│    (Python/FastAPI)  │   │   (Python/FastAPI)    │
│                      │   │                       │
│  ┌────────────────┐  │   │  ┌─────────────────┐  │
│  │   MediaPipe    │  │   │  │   MusicGen      │  │
│  │   Hands ONNX   │  │   │  │   (Meta AI)     │  │
│  └────────────────┘  │   │  └─────────────────┘  │
│                      │   │                       │
│  21 keypoints/hand   │   │  Async job queue      │
│  Guitar mapping      │   │  Sample caching       │
└──────────────────────┘   └───────────────────────┘
```

## HandPoseService

### Purpose
Detects hand poses from images/video frames and maps them to guitar string/fret positions.

### Endpoints

#### `POST /v1/handpose/infer`
Detect hand pose from uploaded image.

**Request:**
```bash
curl -X POST http://localhost:8081/v1/handpose/infer \
  -F "file=@guitar_hand.jpg"
```

**Response:**
```json
{
  "hands": [
    {
      "id": 0,
      "side": "left",
      "keypoints": [
        {"name": "WRIST", "x": 0.42, "y": 0.77, "z": -0.03},
        {"name": "THUMB_TIP", "x": 0.35, "y": 0.65, "z": -0.02},
        ...
      ],
      "confidence": 0.95
    }
  ],
  "image_width": 1920,
  "image_height": 1080,
  "processing_time_ms": 45.2
}
```

#### `POST /v1/handpose/guitar-map`
Map hand keypoints to guitar positions.

**Request:**
```json
{
  "hand_pose": { ... },
  "neck_config": {
    "scale_length_mm": 648.0,
    "num_frets": 22,
    "string_spacing_mm": 10.5,
    "nut_width_mm": 42.0
  },
  "hand_to_map": "left"
}
```

**Response:**
```json
{
  "positions": [
    {"string": 3, "fret": 5, "finger": "index", "confidence": 0.85},
    {"string": 4, "fret": 7, "finger": "ring", "confidence": 0.82}
  ],
  "hand_side": "left",
  "mapping_method": "geometric_phase1"
}
```

### Development Phases

- **Phase 1 (Current)**: Mock data, simple geometric mapping
- **Phase 2**: Real MediaPipe ONNX inference, GPU acceleration
- **Phase 3**: ML-based guitar mapping, temporal smoothing

## SoundBankService

### Purpose
Generates guitar sound samples using AI (MusicGen) based on symbolic descriptions.

### Endpoints

#### `POST /v1/sounds/generate`
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

#### `GET /v1/sounds/jobs/{jobId}`
Get job status.

**Response:**
```json
{
  "job_id": "6f4e0d0c-...",
  "status": "completed",
  "progress": 1.0,
  "status_message": "Sound sample generated successfully",
  "sample": {
    "sample_id": "a1b2c3d4-...",
    "file_path": "/samples/a1b2c3d4.wav",
    "file_size_bytes": 352800,
    ...
  }
}
```

#### `GET /v1/sounds/{sampleId}/download`
Download audio file.

#### `POST /v1/sounds/search`
Search existing samples.

### Development Phases

- **Phase 1 (Current)**: Mock generation (silence WAV files), in-memory storage
- **Phase 2**: Real MusicGen AI, Redis job queue, MongoDB metadata, MinIO storage
- **Phase 3**: Sample caching, batch generation, WebSocket progress

## GaApi Integration

### GuitarPlayingController

Orchestrates both services for complete guitar playing simulation.

#### `POST /api/guitarplaying/detect-hands`
Detect hands from image.

#### `POST /api/guitarplaying/detect-and-map`
Detect hands and map to guitar in one call.

#### `POST /api/guitarplaying/play-from-image`
**Full pipeline**: Detect hands → Map to guitar → Generate sounds

**Request:**
```bash
curl -X POST "http://localhost:5000/api/guitarplaying/play-from-image?waitForGeneration=true" \
  -F "file=@guitar_playing.jpg"
```

**Response:**
```json
{
  "handPose": { ... },
  "guitarMapping": {
    "positions": [
      {"string": 3, "fret": 5, "finger": "index", "confidence": 0.85}
    ]
  },
  "soundJobs": [
    {"job_id": "...", "status": "queued"}
  ],
  "completedSamples": [
    {"sample_id": "...", "file_path": "..."}
  ]
}
```

## Running the Services

### Local Development (Python)

```bash
# HandPoseService
cd Apps/hand-pose-service
pip install -r requirements.txt
python main.py
# Runs on http://localhost:8080

# SoundBankService
cd Apps/sound-bank-service
pip install -r requirements.txt
python main.py
# Runs on http://localhost:8080
```

### Docker

```bash
# Build images
docker build -t hand-pose-service Apps/hand-pose-service
docker build -t sound-bank-service Apps/sound-bank-service

# Run containers
docker run -p 8081:8080 hand-pose-service
docker run -p 8082:8080 -v $(pwd)/samples:/app/samples sound-bank-service
```

### Aspire Orchestration (Production)

```bash
dotnet run --project AllProjects.AppHost
```

Services are automatically discovered and configured:
- HandPoseService: `https+http://hand-pose-service` (port 8081)
- SoundBankService: `https+http://sound-bank-service` (port 8082)
- GaApi references both services via Aspire service discovery

## Configuration

### HandPoseService
- `PORT`: Service port (default: 8080)
- `LOG_LEVEL`: Logging level (default: INFO)
- `MODEL_PATH`: Path to ONNX model (Phase 2)

### SoundBankService
- `PORT`: Service port (default: 8080)
- `LOG_LEVEL`: Logging level (default: INFO)
- `MODEL_PATH`: Path to MusicGen model (Phase 2)
- `REDIS_URL`: Redis connection (Phase 2)
- `MONGODB_URL`: MongoDB connection (Phase 2)
- `S3_ENDPOINT`: Storage endpoint (Phase 2)

## Testing

### Interactive API Documentation

```bash
# HandPoseService
open http://localhost:8081/docs

# SoundBankService
open http://localhost:8082/docs

# GaApi
open http://localhost:5000/swagger
```

### Example Workflow

```bash
# 1. Upload image and detect hands
curl -X POST http://localhost:5000/api/guitarplaying/detect-hands \
  -F "file=@test_image.jpg" > hands.json

# 2. Generate sound for detected position
curl -X POST http://localhost:5000/api/guitarplaying/generate-sound \
  -H "Content-Type: application/json" \
  -d '{
    "instrument": "electric_guitar",
    "string": 3,
    "fret": 5,
    "velocity": 0.7,
    "technique": ["pluck"]
  }' > job.json

# 3. Check job status
JOB_ID=$(jq -r '.job_id' job.json)
curl http://localhost:5000/api/guitarplaying/sound-jobs/$JOB_ID

# 4. Download sample
SAMPLE_ID=$(jq -r '.sample.sample_id' job.json)
curl http://localhost:5000/api/guitarplaying/sounds/$SAMPLE_ID/download -o sample.wav
```

## Future Enhancements

### Phase 2 (AI Integration)
- [ ] Load MediaPipe Hands ONNX model
- [ ] Load MusicGen model from Hugging Face
- [ ] GPU acceleration for both services
- [ ] Redis job queue for scalability
- [ ] MongoDB for sample metadata
- [ ] MinIO/S3 for audio storage

### Phase 3 (Advanced Features)
- [ ] ML-based guitar position mapping
- [ ] Temporal smoothing for video
- [ ] Multi-hand tracking
- [ ] Sample caching and deduplication
- [ ] Batch sound generation
- [ ] WebSocket progress updates
- [ ] Audio post-processing (EQ, compression, reverb)

## License

Part of Guitar Alchemist project.

