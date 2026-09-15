/**
 * IX Hand Voicing Lab — webcam hand landmarks become fretboard contacts,
 * then IX-style scoring estimates how close the detected pose is to a target
 * chord voicing.
 */

import React, { useEffect, useMemo, useRef, useState } from 'react';
import {
  Box,
  Button,
  Chip,
  Paper,
  Stack,
  ToggleButton,
  ToggleButtonGroup,
  Typography,
} from '@mui/material';
import { useHandTracker } from './useHandTracker';
import { CHORDS, FINGERTIP_INDICES, mapLandmarkToFretboard, scoreVoicing, fretsToString } from './chords';
import { drawFretboard, drawHandOverlay } from './render';

export const IxHandVoicingLab: React.FC = () => {
  const videoRef = useRef<HTMLVideoElement>(null);
  const canvasRef = useRef<HTMLCanvasElement>(null);
  const handCanvasRef = useRef<HTMLCanvasElement>(null);
  const [cameraOn, setCameraOn] = useState(false);
  const [selectedChord, setSelectedChord] = useState(CHORDS[0]);
  const { isReady, error, landmarks } = useHandTracker(videoRef, cameraOn);

  const detected = useMemo(() => {
    const frets: (number | null)[] = [null, null, null, null, null, null];
    if (!landmarks || landmarks.length === 0) return frets;
    const hand = landmarks[0];
    for (const idx of FINGERTIP_INDICES) {
      const lm = hand[idx];
      if (!lm) continue;
      const { stringIndex, fretIndex } = mapLandmarkToFretboard(lm.x, lm.y);
      if (fretIndex !== null && frets[stringIndex] === null) {
        frets[stringIndex] = fretIndex;
      }
    }
    return frets;
  }, [landmarks]);

  const score = useMemo(
    () => scoreVoicing(detected, selectedChord),
    [detected, selectedChord],
  );

  useEffect(() => {
    const canvas = canvasRef.current;
    if (!canvas) return;
    const ctx = canvas.getContext('2d');
    if (!ctx) return;
    drawFretboard(ctx, canvas.width, canvas.height, selectedChord, landmarks);

    const handCanvas = handCanvasRef.current;
    if (!handCanvas) return;
    const handCtx = handCanvas.getContext('2d');
    if (!handCtx) return;
    drawHandOverlay(handCtx, handCanvas.width, handCanvas.height, landmarks);
  }, [landmarks, selectedChord]);

  return (
    <Box sx={{ width: '100%', height: '100%', bgcolor: '#05050d', color: '#f5f0ff', p: 2 }}>
      <Stack direction="row" spacing={2} alignItems="center" sx={{ mb: 2, flexWrap: 'wrap' }}>
        <Typography variant="h6" sx={{ fontFamily: 'monospace', fontWeight: 800 }}>
          IX HAND VOICING LAB
        </Typography>
        <Chip
          label="MediaPipe + webcam + fretboard scoring"
          size="small"
          sx={{ bgcolor: 'rgba(189,164,255,0.15)', color: '#d8c8ff', fontFamily: 'monospace' }}
        />
        <Button
          variant={cameraOn ? 'contained' : 'outlined'}
          size="small"
          onClick={() => setCameraOn((v) => !v)}
          sx={{ fontFamily: 'monospace', textTransform: 'none' }}
        >
          {cameraOn ? 'Stop camera' : 'Start camera'}
        </Button>
        {landmarks.length > 0 && <Chip label="hand detected" size="small" color="success" />}
        {isReady && landmarks.length === 0 && (
          <Chip
            label="tracking active"
            size="small"
            sx={{ bgcolor: 'rgba(189,164,255,0.15)', color: '#d8c8ff', fontFamily: 'monospace' }}
          />
        )}
        {error && (
          <Typography variant="caption" color="error" sx={{ fontFamily: 'monospace' }}>
            {error}
          </Typography>
        )}
      </Stack>

      <Stack direction="row" spacing={2} alignItems="center" sx={{ mb: 2 }}>
        <Typography variant="body2" sx={{ fontFamily: 'monospace', color: '#a99dcc' }}>
          Target chord:
        </Typography>
        <ToggleButtonGroup
          value={selectedChord.name}
          exclusive
          size="small"
          onChange={(_, value) => {
            if (!value) return;
            const chord = CHORDS.find((c) => c.name === value);
            if (chord) setSelectedChord(chord);
          }}
          sx={{ flexWrap: 'wrap' }}
        >
          {CHORDS.map((c) => (
            <ToggleButton
              key={c.name}
              value={c.name}
              sx={{ fontFamily: 'monospace', color: '#d8c8ff', textTransform: 'none' }}
            >
              {c.name}
            </ToggleButton>
          ))}
        </ToggleButtonGroup>
      </Stack>

      <Stack direction={{ xs: 'column', md: 'row' }} spacing={2} sx={{ height: 'calc(100% - 120px)' }}>
        <Paper
          elevation={0}
          sx={{
            flex: 1,
            bgcolor: 'rgba(10,8,20,0.8)',
            border: '1px solid rgba(189,164,255,0.22)',
            display: 'flex',
            alignItems: 'center',
            justifyContent: 'center',
            minHeight: 240,
            overflow: 'hidden',
            position: 'relative',
          }}
        >
          <video
            ref={videoRef}
            autoPlay
            playsInline
            muted
            style={{
              width: '100%',
              height: '100%',
              objectFit: 'cover',
              transform: 'scaleX(-1)',
              display: cameraOn ? 'block' : 'none',
            }}
          />
          <canvas
            ref={handCanvasRef}
            width={640}
            height={480}
            style={{
              position: 'absolute',
              top: 0,
              left: 0,
              width: '100%',
              height: '100%',
              objectFit: 'cover',
              transform: 'scaleX(-1)',
              display: cameraOn ? 'block' : 'none',
              pointerEvents: 'none',
            }}
          />
          {!cameraOn && (
            <Typography sx={{ fontFamily: 'monospace', color: '#a99dcc', p: 2 }}>
              Start the camera to begin hand tracking.
            </Typography>
          )}
        </Paper>

        <Paper
          elevation={0}
          sx={{
            flex: 1,
            bgcolor: 'rgba(10,8,20,0.8)',
            border: '1px solid rgba(189,164,255,0.22)',
            display: 'flex',
            flexDirection: 'column',
            minHeight: 240,
            p: 1,
          }}
        >
          <canvas
            ref={canvasRef}
            width={400}
            height={500}
            style={{ width: '100%', height: '100%', borderRadius: 4 }}
          />
          <Stack direction="row" spacing={2} sx={{ mt: 1, justifyContent: 'space-between' }}>
            <Typography variant="caption" sx={{ fontFamily: 'monospace', color: '#a99dcc' }}>
              Detected: {fretsToString(detected)}
            </Typography>
            <Typography variant="caption" sx={{ fontFamily: 'monospace', color: '#9deaff' }}>
              Match: {score}%
            </Typography>
          </Stack>
        </Paper>
      </Stack>
    </Box>
  );
};

export default IxHandVoicingLab;
