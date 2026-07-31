/**
 * MediaPipe Hands hook for the IX Hand Voicing Lab.
 */

import { useEffect, useRef, useState, useCallback } from 'react';
import { Hands, Results } from '@mediapipe/hands';

export type HandLandmarks = Results['multiHandLandmarks'];

export interface HandTrackerState {
  isReady: boolean;
  error: string | null;
  landmarks: HandLandmarks;
}

export function useHandTracker(
  videoRef: React.RefObject<HTMLVideoElement | null>,
  enabled: boolean,
): HandTrackerState {
  const [state, setState] = useState<HandTrackerState>({
    isReady: false,
    error: null,
    landmarks: [],
  });
  const handsRef = useRef<Hands | null>(null);
  const rafRef = useRef<number | null>(null);
  const runningRef = useRef(false);

  const startCamera = useCallback(async () => {
    const video = videoRef.current;
    if (!video) return;
    try {
      const stream = await navigator.mediaDevices.getUserMedia({
        video: { width: 640, height: 480, facingMode: 'user' },
      });
      video.srcObject = stream;
      await video.play();
    } catch (err) {
      setState((s) => ({
        ...s,
        error: err instanceof Error ? err.message : 'Camera access denied',
      }));
    }
  }, [videoRef]);

  useEffect(() => {
    if (!enabled) return;

    startCamera();

    const hands = new Hands({
      locateFile: (file) =>
        `https://cdn.jsdelivr.net/npm/@mediapipe/hands/${file}`,
    });
    hands.setOptions({
      maxNumHands: 1,
      modelComplexity: 1,
      minDetectionConfidence: 0.5,
      minTrackingConfidence: 0.5,
    });
    hands.onResults((results) => {
      setState((s) => ({
        ...s,
        isReady: true,
        landmarks: results.multiHandLandmarks ?? [],
      }));
    });
    handsRef.current = hands;
    runningRef.current = true;

    const loop = async () => {
      if (!runningRef.current) return;
      const video = videoRef.current;
      if (video && video.readyState >= 2) {
        await hands.send({ image: video });
      }
      rafRef.current = requestAnimationFrame(loop);
    };
    rafRef.current = requestAnimationFrame(loop);

    return () => {
      runningRef.current = false;
      if (rafRef.current) cancelAnimationFrame(rafRef.current);
      hands.close();
      handsRef.current = null;
      const video = videoRef.current;
      if (video && video.srcObject) {
        video.srcObject.getTracks().forEach((t) => t.stop());
        video.srcObject = null;
      }
    };
  }, [enabled, startCamera]);

  return state;
}
