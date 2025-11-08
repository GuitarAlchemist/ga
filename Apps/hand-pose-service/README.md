# HandPoseService

FastAPI microservice for detecting guitar hand poses using MediaPipe Hands model.

## Features

- **Hand Pose Detection**: Detects 21 keypoints per hand from images
- **Guitar Mapping**: Maps hand keypoints to guitar string/fret positions
- **RESTful API**: FastAPI with automatic OpenAPI documentation
- **Health Checks**: Built-in health and metrics endpoints

## API Endpoints

### `POST /v1/handpose/infer`
Detect hand pose from uploaded image.

**Request:**
- Multipart form data with image file

**Response:**
```json
{
  "hands": [
    {
      "id": 0,
      "side": "left",
      "keypoints": [
        {"name": "WRIST", "x": 0.42, "y": 0.77, "z": -0.03},
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

### `POST /v1/handpose/guitar-map`
Map hand pose to guitar string/fret positions.

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
    {
      "string": 3,
      "fret": 5,
      "finger": "index",
      "confidence": 0.85
    }
  ],
  "hand_side": "left",
  "mapping_method": "geometric_phase1"
}
```

### `GET /healthz`
Health check endpoint.

### `GET /metrics`
Prometheus metrics (placeholder).

## Development Phases

### Phase 1 (Current): Mock Data
- ✅ FastAPI service structure
- ✅ API endpoints with Pydantic models
- ✅ Mock hand pose detection
- ✅ Simple geometric guitar mapping
- ✅ Docker containerization

### Phase 2: MediaPipe Integration
- ⏳ Load MediaPipe Hands ONNX model
- ⏳ Real hand pose inference
- ⏳ GPU acceleration support

### Phase 3: Advanced Mapping
- ⏳ ML-based guitar position mapping
- ⏳ Temporal smoothing for video
- ⏳ Multi-hand tracking

## Running Locally

### Python (Development)
```bash
cd Apps/hand-pose-service
pip install -r requirements.txt
python main.py
```

Service runs on http://localhost:8080

### Docker
```bash
cd Apps/hand-pose-service
docker build -t hand-pose-service .
docker run -p 8080:8080 hand-pose-service
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

# Test hand pose detection
curl -X POST http://localhost:8080/v1/handpose/infer \
  -F "file=@test_image.jpg"

# Interactive API docs
open http://localhost:8080/docs
```

## Dependencies

- **FastAPI**: Web framework
- **Uvicorn**: ASGI server
- **Pydantic**: Data validation
- **Pillow**: Image processing
- **NumPy**: Numerical operations
- **OpenCV**: Computer vision utilities
- **ONNX Runtime** (Phase 2): Model inference

## Configuration

Environment variables:
- `PORT`: Service port (default: 8080)
- `LOG_LEVEL`: Logging level (default: INFO)
- `MODEL_PATH`: Path to ONNX model (Phase 2)

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
┌────────▼────────┐
│ HandPoseService │
│    (Python)     │
│                 │
│  ┌───────────┐  │
│  │  FastAPI  │  │
│  └─────┬─────┘  │
│        │        │
│  ┌─────▼─────┐  │
│  │ MediaPipe │  │
│  │   ONNX    │  │
│  └───────────┘  │
└─────────────────┘
```

## License

Part of Guitar Alchemist project.

