// src/components/PrimeRadiant/GodotReactSync.ts
// Bidirectional sync bridge between Godot (iframe/postMessage) and React GIS/governance.
// React -> Godot: governance pins and belief updates
// Godot -> React: node clicks and algedonic signals

import type { GisLayerManager, GisPin } from './GisLayer';
import type { GodotInboundMessage, GodotOutboundMessage } from './GodotScene';

// ---------------------------------------------------------------------------
// Types
// ---------------------------------------------------------------------------

export interface GodotReactSyncHandle {
  /** Send an arbitrary message to the Godot iframe */
  sendToGodot: (msg: GodotInboundMessage) => void;
  /** Send a governance pin to Godot */
  sendPin: (nodeId: string, text: string, color: string) => void;
  /** Send a belief update to Godot */
  sendBelief: (nodeId: string, state: string, confidence: number) => void;
  /** Tear down the listener — call on unmount */
  cleanup: () => void;
}

// ---------------------------------------------------------------------------
// Deterministic hash — maps a string to a lat/lon for reproducible placement
// ---------------------------------------------------------------------------

function hashToLatLon(input: string): { lat: number; lon: number } {
  let h = 0;
  for (let i = 0; i < input.length; i++) {
    h = (h * 31 + input.charCodeAt(i)) | 0;
  }
  // Map hash to lat [-60, 60] and lon [-180, 180] — avoid extreme poles
  const lat = ((Math.abs(h) % 12000) / 100) - 60;
  const lon = ((Math.abs(h >> 8) % 36000) / 100) - 180;
  return { lat, lon };
}

// ---------------------------------------------------------------------------
// Severity -> pin color
// ---------------------------------------------------------------------------

function severityColor(severity: string): string {
  switch (severity) {
    case 'critical': return '#ff0000';
    case 'warning':  return '#ffaa00';
    case 'info':     return '#33ccff';
    default:         return '#ff44ff';
  }
}

// ---------------------------------------------------------------------------
// Factory
// ---------------------------------------------------------------------------

/**
 * Create a bidirectional sync bridge between a Godot iframe and the React
 * GIS / governance layer.
 *
 * @param iframeEl   The Godot iframe element (may be null if not mounted yet)
 * @param gisManager The GIS layer manager for placing pins on the globe
 * @param onNodeSelect Callback when Godot reports a node click
 */
export function createGodotReactSync(
  iframeEl: HTMLIFrameElement | null,
  gisManager: GisLayerManager,
  onNodeSelect: (id: string) => void,
): GodotReactSyncHandle {
  // Track algedonic pin counter for unique IDs
  let algedonicSeq = 0;

  // --- Godot -> React listener -------------------------------------------
  const handleMessage = (ev: MessageEvent) => {
    // Only accept messages from the Godot iframe
    if (iframeEl && ev.source !== iframeEl.contentWindow) return;

    const msg = ev.data as GodotOutboundMessage;
    if (!msg || typeof msg.type !== 'string') return;

    switch (msg.type) {
      case 'godot:node-clicked': {
        onNodeSelect(msg.nodeId);
        break;
      }

      case 'godot:algedonic': {
        // Place a temporary pulsing pin at a hashed location
        const loc = hashToLatLon(msg.description + msg.severity);
        const pin: GisPin = {
          id: `algedonic-${++algedonicSeq}`,
          lat: loc.lat,
          lon: loc.lon,
          label: msg.description.slice(0, 18),
          color: severityColor(msg.severity),
          icon: '\u26A0',
          pulse: true,
          category: 'algedonic',
          size: msg.severity === 'critical' ? 2.0 : 1.2,
        };
        gisManager.addPin(pin);

        // Auto-remove after 30 seconds to avoid clutter
        setTimeout(() => {
          gisManager.removePin(pin.id);
        }, 30_000);
        break;
      }

      default:
        break;
    }
  };

  window.addEventListener('message', handleMessage);

  // --- React -> Godot helpers --------------------------------------------
  const sendToGodot = (msg: GodotInboundMessage) => {
    if (!iframeEl?.contentWindow) return;
    iframeEl.contentWindow.postMessage(msg, '*');
  };

  const sendPin = (nodeId: string, text: string, color: string) => {
    // Send governance pin data into Godot for 3D visualization
    sendToGodot({
      type: 'governance:select',
      nodeId,
    });
    // Also use the generic update channel to push pin metadata
    sendToGodot({
      type: 'governance:update',
      nodes: [{ id: nodeId, label: text, color }],
      edges: [],
    });
  };

  const sendBelief = (nodeId: string, state: string, confidence: number) => {
    sendToGodot({
      type: 'governance:belief',
      nodeId,
      state,
      confidence,
    });
  };

  // --- Cleanup -----------------------------------------------------------
  const cleanup = () => {
    window.removeEventListener('message', handleMessage);
  };

  return { sendToGodot, sendPin, sendBelief, cleanup };
}
