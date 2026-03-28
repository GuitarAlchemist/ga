// src/components/PrimeRadiant/IxqlGisBridge.ts
// Bridge between IXQL query results and GIS pin visualization on planets.
// Maps governance node query results to deterministic lat/lon positions
// with color coding based on belief state, health status, and node type.

import type { GovernanceNode } from './types';
import type { GisLayerManager, GisPin } from './GisLayer';

// ---------------------------------------------------------------------------
// Pin ID prefix — all IXQL-generated pins share this so we can clear them
// ---------------------------------------------------------------------------
const IXQL_PIN_PREFIX = 'ixql-';

// ---------------------------------------------------------------------------
// Deterministic hash — converts a node ID string to a stable lat/lon pair
// Uses a simple FNV-1a-inspired hash to spread nodes evenly across the globe.
// ---------------------------------------------------------------------------

function hashStringToNumber(str: string): number {
  let hash = 2166136261; // FNV offset basis (32-bit)
  for (let i = 0; i < str.length; i++) {
    hash ^= str.charCodeAt(i);
    hash = (hash * 16777619) >>> 0; // FNV prime, keep as unsigned 32-bit
  }
  return hash;
}

function nodeIdToLatLon(id: string): { lat: number; lon: number } {
  const h1 = hashStringToNumber(id);
  const h2 = hashStringToNumber(id + ':lon');
  // Map to lat [-80, 80] and lon [-180, 180] — avoid extreme poles
  const lat = (h1 / 0xFFFFFFFF) * 160 - 80;
  const lon = (h2 / 0xFFFFFFFF) * 360 - 180;
  return { lat, lon };
}

// ---------------------------------------------------------------------------
// Node → GIS pin color + pulse rules
// Priority order (first match wins):
//   1. belief = CONTRADICTORY → magenta pulsing
//   2. belief = FALSE → red
//   3. health = error → orange pulsing
//   4. health = warning → yellow
//   5. type = constitution → gold
//   6. default → blue
// ---------------------------------------------------------------------------

interface PinStyle {
  color: string;
  pulse: boolean;
}

function resolvePinStyle(node: GovernanceNode): PinStyle {
  // Check belief state from metadata
  const belief = node.metadata?.belief as string | undefined;
  if (belief === 'CONTRADICTORY' || belief === 'C') {
    return { color: '#FF44FF', pulse: true };
  }
  if (belief === 'FALSE' || belief === 'F') {
    return { color: '#FF4444', pulse: false };
  }

  // Check health status
  if (node.healthStatus === 'error') {
    return { color: '#FF8800', pulse: true };
  }
  if (node.healthStatus === 'warning') {
    return { color: '#FFD700', pulse: false };
  }

  // Check node type
  if (node.type === 'constitution') {
    return { color: '#FFD700', pulse: false };
  }

  // Default
  return { color: '#4488FF', pulse: false };
}

// ---------------------------------------------------------------------------
// Convert a governance node to a GIS pin
// ---------------------------------------------------------------------------

function nodeToPin(node: GovernanceNode): GisPin {
  const { lat, lon } = nodeIdToLatLon(node.id);
  const style = resolvePinStyle(node);

  return {
    id: `${IXQL_PIN_PREFIX}${node.id}`,
    lat,
    lon,
    label: node.name,
    color: style.color,
    pulse: style.pulse,
    size: node.type === 'constitution' ? 1.5 : 1.0,
    category: 'ixql-query',
    data: {
      nodeId: node.id,
      nodeType: node.type,
      healthStatus: node.healthStatus,
      belief: node.metadata?.belief,
    },
  };
}

// ---------------------------------------------------------------------------
// Public API
// ---------------------------------------------------------------------------

/**
 * Map IXQL query result nodes to GIS pins on a planet.
 * Clears any previous IXQL pins before adding new ones.
 *
 * @param nodes - Governance nodes from an IXQL query result
 * @param gisManager - The GIS layer manager for the target planet
 */
export function ixqlToGis(nodes: GovernanceNode[], gisManager: GisLayerManager): void {
  // Clear previous IXQL pins first
  clearIxqlPins(gisManager);

  // Convert nodes to pins and add them all at once
  const pins = nodes.map(nodeToPin);
  if (pins.length > 0) {
    gisManager.addPins(pins);
  }
}

/**
 * Remove all IXQL-generated pins from the GIS layer.
 *
 * @param gisManager - The GIS layer manager to clear pins from
 */
export function clearIxqlPins(gisManager: GisLayerManager): void {
  const existing = gisManager.getPins();
  for (const pin of existing) {
    if (pin.id.startsWith(IXQL_PIN_PREFIX)) {
      gisManager.removePin(pin.id);
    }
  }
}
