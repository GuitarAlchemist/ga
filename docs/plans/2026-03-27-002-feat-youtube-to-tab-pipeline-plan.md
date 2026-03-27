---
title: "feat: YouTube to tablature — end-to-end pipeline with mock inference"
type: feat
status: active
date: 2026-03-27
origin: docs/brainstorms/2026-03-27-youtube-to-tab-mvp-requirements.md
---

# feat: YouTube to tablature — end-to-end pipeline with mock inference

## Overview

Wire the end-to-end pipeline: YouTube URL → video download → frame extraction → hand pose inference (mock) → guitar position stream → chord sequence → ASCII tab generation. Proves the architecture with mock data; real MediaPipe inference swaps in later without pipeline changes.

## Requirements Trace

- R1. Video frame extraction (ffmpeg/OpenCV, configurable FPS)
- R2. Batch hand pose inference via HandPoseService
- R3. Position stream → chord sequence (group simultaneous positions)
- R4. Tab generation via existing DSL
- R5. `POST /api/youtube-to-tab` orchestration endpoint
- R6. Works with mock hand pose data

## Scope Boundaries

- No real MediaPipe — mock data only
- No audio analysis — vision only
- No technique detection — notes and chords only
- No rhythm — evenly spaced measures
- No frontend UI — API only

## Context & Research

### Existing Infrastructure

- **HandPoseService** (`Apps/hand-pose-service/main.py`): FastAPI, `POST /v1/handpose/infer` returns mock 21-keypoint hands, `POST /v1/handpose/guitar-map` maps to string/fret
- **HandPoseClient** (`Common/GA.Business.AI/HandPose/HandPoseClient.cs`): C# HTTP client with `InferAsync`, `MapToGuitarAsync`
- **YouTubeTranscriptService**: Downloads transcripts but not video frames
- **AsciiTabTypes.fs**: Full tab type system (StringLine, Staff, Measure, NoteElement, etc.)
- **TabClosures.fs**: `tab.parseAscii`, `tab.generateChord` DSL closures
- **GuitarPlayingController**: Endpoints for detect-hands + map-to-guitar but no tab generation

### Key Decision

The pipeline runs on GaApi (C#). Frame extraction calls ffmpeg as a subprocess. Hand pose inference calls the Python service via HttpClient. Tab generation uses the F# DSL. All stages connected by a new orchestration service.

## Implementation Units

- [ ] **Unit 1: Video frame extraction service**

  **Goal:** Download a YouTube video and extract frames at configurable FPS.

  **Requirements:** R1

  **Files:**
  - Create: `Apps/ga-server/GaApi/Services/VideoFrameExtractor.cs`

  **Approach:**
  - `ExtractFramesAsync(string youtubeUrl, double fps = 2.0, CancellationToken ct)` → returns `IReadOnlyList<ExtractedFrame>` where `ExtractedFrame = record(byte[] ImageData, double TimestampSeconds, int FrameIndex)`
  - Use `yt-dlp` (or `youtube-dl`) subprocess to download video to temp directory
  - Use `ffmpeg` subprocess to extract frames: `ffmpeg -i video.mp4 -vf fps={fps} frame_%04d.jpg`
  - Read frames from disk, return as byte arrays with timestamps
  - Clean up temp files after extraction
  - Log progress (downloading, extracting, frame count)

  **Patterns to follow:**
  - `YouTubeTranscriptService.cs` for subprocess pattern (Process.Start, stdout/stderr capture)

  **Test scenarios:**
  - Extracts frames at specified FPS
  - Handles missing yt-dlp/ffmpeg gracefully (returns error)
  - Cleans up temp files

  **Verification:**
  - Service compiles. Can be tested with a sample video URL.

- [ ] **Unit 2: Batch hand pose pipeline**

  **Goal:** Send extracted frames to HandPoseService, collect results with timestamps.

  **Requirements:** R2

  **Files:**
  - Create: `Apps/ga-server/GaApi/Services/HandPosePipeline.cs`

  **Approach:**
  - `ProcessFramesAsync(IReadOnlyList<ExtractedFrame> frames, CancellationToken ct)` → returns `IReadOnlyList<TimestampedGuitarPosition>`
  - For each frame: call `HandPoseClient.InferAsync(frame.ImageData)` → call `HandPoseClient.MapToGuitarAsync(result)` → record positions with timestamp
  - Process frames sequentially (mock service is fast; can parallelize later for real inference)
  - `TimestampedGuitarPosition = record(double TimestampSeconds, GuitarPosition[] Positions)`
  - Skip frames where no hands detected

  **Patterns to follow:**
  - `HandPoseClient.cs` for API call pattern

  **Test scenarios:**
  - Processes batch of frames and returns positions
  - Handles frames with no detected hands (skips)
  - Returns empty list for empty input

  **Verification:**
  - Service compiles. Returns mock positions for mock frames.

- [ ] **Unit 3: Position stream → tab generation**

  **Goal:** Convert timestamped guitar positions into ASCII tab.

  **Requirements:** R3, R4

  **Files:**
  - Create: `Apps/ga-server/GaApi/Services/PositionToTabGenerator.cs`

  **Approach:**
  - `GenerateTab(IReadOnlyList<TimestampedGuitarPosition> positions)` → returns `string` (ASCII tab)
  - Group positions by time proximity (200ms tolerance) → chord events
  - Detect position changes → new note events
  - Format as ASCII tab: 6 string lines (e, B, G, D, A, E), fret numbers at note positions, dashes for silence
  - Break into measures (4 notes/chords per measure, or every 2 seconds)
  - Add header comment with video URL and generation timestamp

  **Patterns to follow:**
  - `AsciiTabTypes.fs` for tab structure understanding
  - Standard ASCII tab format: `e|---0---1---|`, etc.

  **Test scenarios:**
  - Single notes render correctly on the right string
  - Simultaneous positions render as a chord (vertically aligned)
  - Empty positions produce dashes
  - Output parses through AsciiTabParser

  **Verification:**
  - Service compiles. Generates readable ASCII tab from mock position data.

- [ ] **Unit 4: Orchestration endpoint**

  **Goal:** `POST /api/youtube-to-tab` wires the full pipeline.

  **Requirements:** R5, R6

  **Files:**
  - Create: `Apps/ga-server/GaApi/Controllers/YouTubeTabController.cs`
  - Modify: `Apps/ga-server/GaApi/Extensions/AIServiceExtensions.cs` (register services)

  **Approach:**
  - Request: `{ "url": "https://youtube.com/watch?v=...", "fps": 2.0 }`
  - Response: `{ "tab": "e|---0---|...", "frameCount": 300, "noteCount": 45, "duration": "2:30", "confidence": 0.65, "processingTimeMs": 5000 }`
  - Pipeline: VideoFrameExtractor → HandPosePipeline → PositionToTabGenerator
  - Wrap in try/catch, return friendly errors for missing tools (yt-dlp, ffmpeg, hand-pose-service)
  - Register all 3 new services + controller in DI

  **Patterns to follow:**
  - `ChatbotController.cs` for controller pattern
  - `AIServiceExtensions.cs` for service registration

  **Test scenarios:**
  - Full pipeline returns tab for a valid URL (with mock hand data)
  - Missing yt-dlp returns 503 with clear error message
  - Missing hand-pose-service returns 503
  - Invalid URL returns 400

  **Verification:**
  - Backend builds. `POST /api/youtube-to-tab` returns ASCII tab (mock data). Pipeline stages log progress.

## Risks & Dependencies

- **yt-dlp/ffmpeg availability**: Must be installed on the system. Return clear error if missing.
- **Hand pose service must be running**: Port 8080. Return 503 if unreachable.
- **Temp file cleanup**: Use try/finally to ensure temp video/frames are deleted.
- **Mock data quality**: Mock hand data produces synthetic but valid-looking tab. Useful for testing the pipeline, not for real transcription.

## Sources & References

- **Origin:** docs/brainstorms/2026-03-27-youtube-to-tab-mvp-requirements.md
- Related: `HandPoseClient.cs`, `HandPoseService/main.py`, `YouTubeTranscriptService.cs`, `AsciiTabTypes.fs`
- Issue: #17
