/**
 * 2D canvas rendering for the fretboard and hand contacts.
 */

import { HandLandmarks } from './useHandTracker';
import { ChordVoicing, FINGERTIP_INDICES, mapLandmarkToFretboard, STANDARD_TUNING } from './chords';

const HAND_CONNECTIONS: [number, number][] = [
  [0, 1], [1, 2], [2, 3], [3, 4],
  [0, 5], [5, 6], [6, 7], [7, 8],
  [5, 9], [9, 10], [10, 11], [11, 12],
  [9, 13], [13, 14], [14, 15], [15, 16],
  [13, 17], [17, 18], [18, 19], [19, 20],
  [0, 17],
];

export function drawFretboard(
  ctx: CanvasRenderingContext2D,
  width: number,
  height: number,
  chord: ChordVoicing,
  landmarks: HandLandmarks,
): void {
  ctx.clearRect(0, 0, width, height);

  const margin = 24;
  const boardLeft = margin;
  const boardRight = width - margin;
  const boardTop = margin;
  const boardBottom = height - margin;
  const boardWidth = boardRight - boardLeft;
  const boardHeight = boardBottom - boardTop;

  // Fretboard background.
  ctx.fillStyle = '#1a1525';
  ctx.fillRect(boardLeft, boardTop, boardWidth, boardHeight);
  ctx.strokeStyle = 'rgba(189, 164, 255, 0.4)';
  ctx.lineWidth = 2;
  ctx.strokeRect(boardLeft, boardTop, boardWidth, boardHeight);

  // Frets.
  ctx.strokeStyle = 'rgba(189, 164, 255, 0.25)';
  ctx.lineWidth = 1;
  for (let i = 0; i <= 12; i++) {
    const y = boardTop + (i / 12) * boardHeight;
    ctx.beginPath();
    ctx.moveTo(boardLeft, y);
    ctx.lineTo(boardRight, y);
    ctx.stroke();
  }

  // Strings.
  for (let i = 0; i < 6; i++) {
    const x = boardLeft + (i / 5) * boardWidth;
    ctx.strokeStyle = 'rgba(220, 210, 255, 0.5)';
    ctx.lineWidth = 1 + (i / 5) * 1.5;
    ctx.beginPath();
    ctx.moveTo(x, boardTop);
    ctx.lineTo(x, boardBottom);
    ctx.stroke();

    // String note label at top.
    ctx.fillStyle = '#bda4ff';
    ctx.font = '12px monospace';
    ctx.textAlign = 'center';
    ctx.fillText(STANDARD_TUNING[i], x, boardTop - 8);
  }

  // Fret numbers on the left.
  ctx.fillStyle = 'rgba(189, 164, 255, 0.6)';
  ctx.font = '11px monospace';
  ctx.textAlign = 'right';
  for (let i = 0; i <= 12; i++) {
    const y = boardTop + (i / 12) * boardHeight + 4;
    ctx.fillText(String(i), boardLeft - 8, y);
  }

  // Target chord frets.
  for (let i = 0; i < 6; i++) {
    const fret = chord.frets[i];
    if (fret === null || fret === 0) continue;
    const x = boardLeft + (i / 5) * boardWidth;
    const y = boardTop + (fret / 12) * boardHeight - boardHeight / 24;
    ctx.fillStyle = 'rgba(122, 226, 255, 0.25)';
    ctx.beginPath();
    ctx.arc(x, y, 12, 0, Math.PI * 2);
    ctx.fill();
    ctx.strokeStyle = 'rgba(122, 226, 255, 0.7)';
    ctx.lineWidth = 1;
    ctx.stroke();
  }

  // Detected finger contacts from hand landmarks.
  if (landmarks && landmarks.length > 0) {
    const hand = landmarks[0];
    for (const idx of FINGERTIP_INDICES) {
      const lm = hand[idx];
      if (!lm) continue;
      const { stringIndex, fretIndex } = mapLandmarkToFretboard(lm.x, lm.y);
      if (fretIndex === null) continue;
      const x = boardLeft + (stringIndex / 5) * boardWidth;
      const y = boardTop + (fretIndex / 12) * boardHeight - boardHeight / 24;
      ctx.fillStyle = '#ff6b6b';
      ctx.beginPath();
      ctx.arc(x, y, 10, 0, Math.PI * 2);
      ctx.fill();
      ctx.strokeStyle = '#ffffff';
      ctx.lineWidth = 1.5;
      ctx.stroke();
    }
  }
}

export function drawHandOverlay(
  ctx: CanvasRenderingContext2D,
  width: number,
  height: number,
  landmarks: HandLandmarks,
): void {
  ctx.clearRect(0, 0, width, height);
  if (!landmarks || landmarks.length === 0) return;
  const hand = landmarks[0];

  ctx.strokeStyle = 'rgba(122, 226, 255, 0.7)';
  ctx.lineWidth = 2;
  for (const [a, b] of HAND_CONNECTIONS) {
    const pa = hand[a];
    const pb = hand[b];
    if (!pa || !pb) continue;
    ctx.beginPath();
    ctx.moveTo(pa.x * width, pa.y * height);
    ctx.lineTo(pb.x * width, pb.y * height);
    ctx.stroke();
  }

  for (const lm of hand) {
    ctx.fillStyle = '#7ae2ff';
    ctx.beginPath();
    ctx.arc(lm.x * width, lm.y * height, 4, 0, Math.PI * 2);
    ctx.fill();
  }

  for (const idx of FINGERTIP_INDICES) {
    const lm = hand[idx];
    if (!lm) continue;
    ctx.fillStyle = '#ff6b6b';
    ctx.beginPath();
    ctx.arc(lm.x * width, lm.y * height, 6, 0, Math.PI * 2);
    ctx.fill();
  }
}
