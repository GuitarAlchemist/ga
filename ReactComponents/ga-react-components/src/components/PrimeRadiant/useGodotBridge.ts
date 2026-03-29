// src/components/PrimeRadiant/useGodotBridge.ts
// React hook wrapping the GodotBridge — manages lifecycle + exposes typed API.

import { useEffect, useRef, useState, useCallback } from 'react';
import { GodotBridge, createGodotBridge } from './GodotBridge';
import type { BridgeEvent } from './GodotBridge';

export interface UseGodotBridgeResult {
  connected: boolean;
  bridge: GodotBridge | null;
  navigateToPlanet: (planet: string) => void;
  highlightNode: (nodeId: string, color: string) => void;
  updateWeather: (nodeId: string, truthState: string, weather: string, intensity: number) => void;
  requestScreenshot: () => void;
}

export function useGodotBridge(
  enabled: boolean = true,
  mode: 'websocket' | 'postmessage' = 'websocket',
  onEvent?: (event: BridgeEvent) => void,
): UseGodotBridgeResult {
  const bridgeRef = useRef<GodotBridge | null>(null);
  const [connected, setConnected] = useState(false);

  useEffect(() => {
    if (!enabled) return;

    const bridge = createGodotBridge(mode);
    bridgeRef.current = bridge;

    // Poll connection status
    const interval = setInterval(() => {
      setConnected(bridge.connected);
    }, 1000);

    // Subscribe to events
    let unsub: (() => void) | undefined;
    if (onEvent) {
      unsub = bridge.onEvent(onEvent);
    }

    return () => {
      clearInterval(interval);
      unsub?.();
      bridge.close();
      bridgeRef.current = null;
      setConnected(false);
    };
  }, [enabled, mode, onEvent]);

  const navigateToPlanet = useCallback((planet: string) => {
    bridgeRef.current?.navigateToPlanet(planet);
  }, []);

  const highlightNode = useCallback((nodeId: string, color: string) => {
    bridgeRef.current?.highlightNode(nodeId, color);
  }, []);

  const updateWeather = useCallback((nodeId: string, truthState: string, weather: string, intensity: number) => {
    bridgeRef.current?.updateBeliefWeather(nodeId, truthState, weather, intensity);
  }, []);

  const requestScreenshot = useCallback(() => {
    bridgeRef.current?.requestScreenshot();
  }, []);

  return {
    connected,
    bridge: bridgeRef.current,
    navigateToPlanet,
    highlightNode,
    updateWeather,
    requestScreenshot,
  };
}
