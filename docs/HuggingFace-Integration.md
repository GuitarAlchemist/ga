# Hugging Face Music Generation Integration

## Overview

Guitar Alchemist now integrates with Hugging Face's text-to-audio models to generate music, backing tracks, and chord progressions using AI.

## Features

- **Text-to-Music Generation**: Generate music from natural language descriptions
- **Backing Track Creation**: Create practice backing tracks in specific keys, styles, and tempos
- **Chord Progression Audio**: Generate audio for chord progressions
- **Multiple Models**: Support for various Hugging Face models (MusicGen, Stable Audio, etc.)
- **Caching**: MD5-based caching to avoid regenerating the same audio
- **Model Loading Handling**: Automatic retry logic for cold-start model loading

## Configuration

### User Secrets (Recommended)

The Hugging Face API token should be stored as a user secret, not in `appsettings.json`:

```bash
# For GaApi
dotnet user-secrets set "HuggingFace:ApiToken" "your-token-here" --project Apps/ga-server/GaApi/GaApi.csproj

# For GuitarAlchemistChatbot
dotnet user-secrets set "HuggingFace:ApiToken" "your-token-here" --project Apps/GuitarAlchemistChatbot/GuitarAlchemistChatbot.csproj
```

### Get Your API Token

1. Go to https://huggingface.co/settings/tokens
2. Create a new token (read access is sufficient)
3. Copy the token and set it using the commands above

**Note**: The API token is optional. Without it, you'll have lower rate limits and may experience longer wait times.

### Configuration Settings

The following settings are in `appsettings.json`:

```json
{
  "HuggingFace": {
    "ApiUrl": "https://api-inference.huggingface.co",
    "DefaultMusicModel": "facebook/musicgen-small",
    "DefaultGuitarModel": "stabilityai/stable-audio-open-1.0",
    "TimeoutSeconds": 120,
    "MaxRetries": 3,
    "EnableCaching": true,
    "CacheDirectory": "cache/audio"
  }
}
```

## Available Models

### Music Generation Models

- **facebook/musicgen-small** (default) - Fast, good quality music generation
- **facebook/musicgen-large** - Higher quality, slower generation
- **stabilityai/stable-audio-open-1.0** - Open-source audio generation
- **riffusion/riffusion-model-v1** - Spectrogram-based music generation

### Text-to-Speech Models

- **2Noise/ChatTTS** - For generating tutorial narration

## API Endpoints

### Health Check
```http
GET /api/MusicGeneration/health
```

Returns configuration status and available models.

### List Models
```http
GET /api/MusicGeneration/models
```

Returns all available Hugging Face models.

### Generate Music
```http
POST /api/MusicGeneration/generate
Content-Type: application/json

{
  "description": "upbeat blues guitar riff in A minor",
  "durationSeconds": 10,
  "temperature": 0.7,
  "modelId": "facebook/musicgen-small"
}
```

Returns: Audio file (WAV format)

### Generate Backing Track
```http
POST /api/MusicGeneration/backing-track
Content-Type: application/json

{
  "key": "A minor",
  "style": "blues",
  "tempo": 120,
  "durationSeconds": 30
}
```

Returns: Audio file (WAV format)

### Generate Chord Progression
```http
POST /api/MusicGeneration/chord-progression
Content-Type: application/json

{
  "progression": "I-IV-V-I",
  "key": "C",
  "style": "acoustic guitar",
  "durationSeconds": 20
}
```

Returns: Audio file (WAV format)

## Testing

### 1. Via Swagger UI

1. Start the application:
   ```bash
   pwsh Scripts/start-all.ps1 -Dashboard
   ```

2. Navigate to: `https://localhost:7001/swagger`

3. Test the endpoints:
   - Try `/api/MusicGeneration/health` first
   - Then `/api/MusicGeneration/generate` with a simple description

### 2. Via cURL

```bash
# Health check
curl https://localhost:7001/api/MusicGeneration/health

# Generate music
curl -X POST https://localhost:7001/api/MusicGeneration/generate \
  -H "Content-Type: application/json" \
  -d '{"description":"blues guitar in A minor","durationSeconds":10}' \
  --output music.wav

# Generate backing track
curl -X POST https://localhost:7001/api/MusicGeneration/backing-track \
  -H "Content-Type: application/json" \
  -d '{"key":"A minor","style":"blues","tempo":120,"durationSeconds":30}' \
  --output backing.wav
```

### 3. Via PowerShell

```powershell
# Health check
Invoke-RestMethod -Uri "https://localhost:7001/api/MusicGeneration/health" -Method Get

# Generate music
$body = @{
    description = "upbeat jazz piano solo"
    durationSeconds = 10
} | ConvertTo-Json

Invoke-RestMethod -Uri "https://localhost:7001/api/MusicGeneration/generate" `
    -Method Post `
    -ContentType "application/json" `
    -Body $body `
    -OutFile "music.wav"
```

## Performance Notes

- **First Request**: May take 20-30 seconds as the model loads (cold start)
- **Subsequent Requests**: Faster (5-15 seconds depending on duration)
- **Cached Requests**: Instant (returns cached audio)
- **Rate Limits**: Without API token, you may hit rate limits faster

## Caching

Generated audio is cached using MD5 hashing of the request parameters:

- Cache location: `cache/audio/` (configurable)
- Cache files: `{md5}.wav` and `{md5}.json` (metadata)
- Cache key includes: description, duration, temperature, model ID

To clear cache:
```bash
Remove-Item -Path "Apps/ga-server/GaApi/cache/audio/*" -Force
```

## Architecture

### Components

1. **HuggingFaceClient** - Low-level HTTP client for Hugging Face API
2. **MusicGenService** - High-level service with caching and domain logic
3. **MusicGenerationController** - REST API endpoints
4. **HuggingFaceModels** - Request/response DTOs

### Service Registration

Services are registered in `Program.cs`:

```csharp
builder.Services.Configure<HuggingFaceSettings>(
    builder.Configuration.GetSection("HuggingFace"));

builder.Services.AddHttpClient<HuggingFaceClient>();
builder.Services.AddSingleton<MusicGenService>();
```

## Troubleshooting

### "Model is loading" errors

This is normal for the first request. The service automatically retries with exponential backoff.

### Rate limit errors

Add your API token using user secrets (see Configuration section above).

### Timeout errors

Increase `TimeoutSeconds` in `appsettings.json` or reduce `durationSeconds` in requests.

### Cache not working

Check that `EnableCaching` is `true` and `CacheDirectory` is writable.

## Future Enhancements

- [ ] Integration with existing chord/scale systems
- [ ] Add generated audio to practice sessions
- [ ] Support for GuitarFlow model (tablature to audio)
- [ ] Batch generation for multiple progressions
- [ ] Audio effects and post-processing
- [ ] Integration with Blazor UI (requires MudBlazor package)

## References

- [Hugging Face Inference API](https://huggingface.co/docs/api-inference/index)
- [MusicGen Model](https://huggingface.co/facebook/musicgen-small)
- [Stable Audio](https://huggingface.co/stabilityai/stable-audio-open-1.0)

