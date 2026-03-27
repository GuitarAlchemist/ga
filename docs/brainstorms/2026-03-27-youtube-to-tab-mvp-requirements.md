---
date: 2026-03-27
topic: youtube-to-tab-mvp
---

# YouTube to Tablature — MVP Pipeline (Mock Inference)

## Problem Frame

Guitarists want to extract tablature from YouTube performance videos. The infrastructure exists in pieces (hand pose service, YouTube transcript extraction, tab DSL parser/generator) but they're not connected. This MVP wires the end-to-end pipeline using mock hand pose data to prove the architecture. Real MediaPipe inference can be swapped in later.

## Requirements

- R1. **Video frame extraction**: Accept a YouTube URL, download the video, extract frames at configurable FPS (default 2 fps). Use ffmpeg or OpenCV.
- R2. **Batch hand pose inference**: Send extracted frames to the HandPoseService, collect hand pose results with timestamps.
- R3. **Position stream → chord sequence**: Accumulate guitar positions over time. Group simultaneous positions (within 200ms tolerance) into chords. Detect position changes as new notes.
- R4. **Tab generation**: Convert the chord/note sequence into ASCII tab format using the existing DSL. Output a readable tab with measure boundaries.
- R5. **Orchestration endpoint**: `POST /api/youtube-to-tab` accepts a YouTube URL, runs the pipeline, returns generated ASCII tab + confidence score.
- R6. **Works with mock data**: The entire pipeline functions with the current Phase 1 mock HandPoseService. When real MediaPipe inference is added later, the pipeline doesn't change — only the hand pose service output improves.

## Success Criteria

- POST a YouTube URL → get back ASCII tab (even if from mock data)
- Pipeline stages are independently testable
- Tab output parses cleanly through the existing AsciiTabParser

## Scope Boundaries

- No real MediaPipe inference — mock hand data only (Phase 2 is separate)
- No audio analysis — purely vision-based (hand pose → fret positions)
- No technique detection (bends, slides) — just notes and chords
- No rhythm/timing — notes only, evenly spaced measures
- No frontend UI for this feature — API endpoint only

## Key Decisions

- **Mock-first architecture**: Proves the pipeline works before investing in real inference
- **ffmpeg for frame extraction**: Already on the system, proven, fast
- **Reuse existing DSL for tab generation**: AsciiTabTypes + generator in F#, don't reinvent
- **Single orchestration endpoint**: One POST request triggers the full pipeline

## Next Steps

→ `/ce:plan` for implementation planning
