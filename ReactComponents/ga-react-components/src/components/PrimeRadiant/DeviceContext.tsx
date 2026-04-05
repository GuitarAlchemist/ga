// src/components/PrimeRadiant/DeviceContext.tsx
// Shared state for audio/presentation device selection — lets the Devices panel
// communicate the chosen TTS output sink ID to the voice playback layer.

import { createContext, useContext, useState, useEffect, useCallback, useMemo } from 'react';
import type { ReactNode } from 'react';
import { setTtsSinkId } from './VoxtralTTS';

interface DeviceContextValue {
  /** Selected audio output device ID, or empty string for browser default. */
  outputSinkId: string;
  setOutputSinkId: (id: string) => void;
  /** Human-readable label of the currently selected output device. */
  outputLabel: string;
  /** All audio output devices reported by the browser. */
  outputDevices: MediaDeviceInfo[];
  /** Selected input (mic) device ID, or empty string for browser default. */
  inputDeviceId: string;
  setInputDeviceId: (id: string) => void;
  inputDevices: MediaDeviceInfo[];
  /** Re-enumerate devices (call after granting mic permission to get labels). */
  refreshDevices: () => Promise<void>;
}

const DeviceContext = createContext<DeviceContextValue | null>(null);

const STORAGE_KEY_OUT = 'prime-radiant.audioOutputSinkId';
const STORAGE_KEY_IN = 'prime-radiant.audioInputDeviceId';

export function DeviceProvider({ children }: { children: ReactNode }): JSX.Element {
  const [outputSinkId, setOutputSinkIdState] = useState<string>(() => localStorage.getItem(STORAGE_KEY_OUT) ?? '');
  const [inputDeviceId, setInputDeviceIdState] = useState<string>(() => localStorage.getItem(STORAGE_KEY_IN) ?? '');
  const [outputDevices, setOutputDevices] = useState<MediaDeviceInfo[]>([]);
  const [inputDevices, setInputDevices] = useState<MediaDeviceInfo[]>([]);

  const refreshDevices = useCallback(async () => {
    if (!navigator.mediaDevices?.enumerateDevices) return;
    const devices = await navigator.mediaDevices.enumerateDevices();
    setOutputDevices(devices.filter(d => d.kind === 'audiooutput'));
    setInputDevices(devices.filter(d => d.kind === 'audioinput'));
  }, []);

  useEffect(() => {
    void refreshDevices();
    // Device list changes when headphones connect/disconnect
    navigator.mediaDevices?.addEventListener('devicechange', refreshDevices);
    return () => navigator.mediaDevices?.removeEventListener('devicechange', refreshDevices);
  }, [refreshDevices]);

  // Sync the chosen output sink with the TTS playback module.
  useEffect(() => { setTtsSinkId(outputSinkId); }, [outputSinkId]);

  const setOutputSinkId = useCallback((id: string) => {
    setOutputSinkIdState(id);
    if (id) localStorage.setItem(STORAGE_KEY_OUT, id);
    else localStorage.removeItem(STORAGE_KEY_OUT);
  }, []);

  const setInputDeviceId = useCallback((id: string) => {
    setInputDeviceIdState(id);
    if (id) localStorage.setItem(STORAGE_KEY_IN, id);
    else localStorage.removeItem(STORAGE_KEY_IN);
  }, []);

  const outputLabel = useMemo(() => {
    if (!outputSinkId) return 'Default';
    const found = outputDevices.find(d => d.deviceId === outputSinkId);
    return found?.label || 'Default';
  }, [outputSinkId, outputDevices]);

  const value = useMemo<DeviceContextValue>(() => ({
    outputSinkId,
    setOutputSinkId,
    outputLabel,
    outputDevices,
    inputDeviceId,
    setInputDeviceId,
    inputDevices,
    refreshDevices,
  }), [outputSinkId, setOutputSinkId, outputLabel, outputDevices, inputDeviceId, setInputDeviceId, inputDevices, refreshDevices]);

  return <DeviceContext.Provider value={value}>{children}</DeviceContext.Provider>;
}

export function useDevices(): DeviceContextValue {
  const ctx = useContext(DeviceContext);
  if (!ctx) throw new Error('useDevices must be used within a DeviceProvider');
  return ctx;
}
