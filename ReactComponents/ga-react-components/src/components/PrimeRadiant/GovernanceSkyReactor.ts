// GovernanceSkyReactor.ts — "Healthy = beautiful space, troubled = stars go wrong"
//
// Monitors aggregate governance health and modifies sky/star visuals accordingly.
// When everything is healthy the cosmos is serene and beautiful. As governance
// degrades the stars themselves become disturbed: dimming, shifting red, flickering.
//
// Usage in ForceRadiant.tsx animation loop:
//   import { createGovernanceSkyReactor, computeHealthSummary } from './GovernanceSkyReactor';
//   import type { GraphNode } from './types';
//
//   // After starField creation:
//   const skyReactor = createGovernanceSkyReactor(starField);
//
//   // Inside animation tick:
//   const healthSummary = computeHealthSummary(graphNodes);
//   skyReactor.update(healthSummary, elapsedTime);
//
//   // On cleanup:
//   skyReactor.dispose();

import * as THREE from 'three';
import type { GovernanceHealthStatus } from './types';

// ---------------------------------------------------------------------------
// Types
// ---------------------------------------------------------------------------

export interface GovernanceHealthSummary {
  totalNodes: number;
  healthyCount: number;
  warningCount: number;
  errorCount: number;
  contradictoryCount: number;
  unknownCount: number;
}

export interface SkyReactorHandle {
  update(health: GovernanceHealthSummary, time: number): void;
  dispose(): void;
}

// ---------------------------------------------------------------------------
// Helper: compute summary from a node array
// ---------------------------------------------------------------------------

interface HasHealthStatus {
  healthStatus?: GovernanceHealthStatus;
}

export function computeHealthSummary(nodes: HasHealthStatus[]): GovernanceHealthSummary {
  let healthyCount = 0;
  let warningCount = 0;
  let errorCount = 0;
  let contradictoryCount = 0;
  let unknownCount = 0;
  for (let i = 0; i < nodes.length; i++) {
    switch (nodes[i].healthStatus) {
      case 'healthy': healthyCount++; break;
      case 'warning': warningCount++; break;
      case 'error': errorCount++; break;
      case 'contradictory': contradictoryCount++; break;
      default: unknownCount++; break;
    }
  }
  return {
    totalNodes: nodes.length,
    healthyCount,
    warningCount,
    errorCount,
    contradictoryCount,
    unknownCount,
  };
}

// ---------------------------------------------------------------------------
// Constants
// ---------------------------------------------------------------------------

// Ambient colors: healthy cool deep blue --> troubled dark amber
const HEALTHY_AMBIENT = new THREE.Color(0x111122);
const TROUBLED_AMBIENT = new THREE.Color(0x221100);

// Star color shift: normal white/blue --> red-giant red
const STAR_RED_SHIFT = new THREE.Color(1.0, 0.35, 0.15);

// Lerp speed: ~2-second smooth transition at 60fps
// Each frame moves ~0.8% of the remaining distance
const LERP_SPEED = 0.008;

// ---------------------------------------------------------------------------
// Factory
// ---------------------------------------------------------------------------

export function createGovernanceSkyReactor(
  starField: THREE.Group,
): SkyReactorHandle {
  // ── Resolve children by name ──
  const brightStars = starField.getObjectByName('stars-bright') as THREE.Points | undefined;
  const dimStars = starField.getObjectByName('stars-dim') as THREE.Points | undefined;
  const skyNebula = starField.getObjectByName('sky-nebula') as THREE.Mesh | undefined;

  // Materials we will modify
  const brightMat = brightStars?.material as THREE.PointsMaterial | undefined;
  const dimMat = dimStars?.material as THREE.PointsMaterial | undefined;
  const nebulaMatRaw = skyNebula?.material as THREE.Material | undefined;

  // Cache original values so we can lerp FROM them
  const origBrightOpacity = brightMat?.opacity ?? 0.9;
  const origDimOpacity = dimMat?.opacity ?? 0.5;

  // Store original star colors for bright stars (for red-shift blending)
  const origBrightColors: Float32Array | null =
    brightStars?.geometry.attributes.color
      ? new Float32Array((brightStars.geometry.attributes.color as THREE.BufferAttribute).array)
      : null;
  const origDimColors: Float32Array | null =
    dimStars?.geometry.attributes.color
      ? new Float32Array((dimStars.geometry.attributes.color as THREE.BufferAttribute).array)
      : null;

  // Smoothed health ratio (lerps toward target each frame)
  let smoothHealthRatio = 1.0;

  // Pre-allocated temporaries (no allocations in tick)
  const _lerpColor = new THREE.Color();
  const _starColor = new THREE.Color();

  // ---------------------------------------------------------------------------
  // update() — called every animation frame
  // ---------------------------------------------------------------------------
  function update(health: GovernanceHealthSummary, time: number): void {
    if (health.totalNodes === 0) return;

    const targetRatio = health.healthyCount / health.totalNodes;

    // Smooth toward target (exponential ease)
    smoothHealthRatio += (targetRatio - smoothHealthRatio) * LERP_SPEED;
    // Clamp to avoid floating-point drift
    if (Math.abs(smoothHealthRatio - targetRatio) < 0.001) {
      smoothHealthRatio = targetRatio;
    }

    const hr = smoothHealthRatio;
    const distress = 1.0 - hr; // 0 = healthy, 1 = fully troubled

    // ── 1. Star brightness (opacity) ──
    if (brightMat) {
      if (hr > 0.8) {
        // Full brightness
        brightMat.opacity = origBrightOpacity;
      } else if (hr > 0.5) {
        // Slight dimming: 100% --> 70%
        const t = (hr - 0.5) / 0.3; // 1 at 0.8, 0 at 0.5
        brightMat.opacity = origBrightOpacity * (0.7 + 0.3 * t);
      } else if (hr > 0.3) {
        // Visible dimming: 70% --> 40%, with flicker
        const t = (hr - 0.3) / 0.2; // 1 at 0.5, 0 at 0.3
        const flicker = 1.0 - Math.random() * 0.15 * (1.0 - t);
        brightMat.opacity = origBrightOpacity * (0.4 + 0.3 * t) * flicker;
      } else {
        // Severe: 40% base with heavy flicker
        const flicker = 1.0 - Math.random() * 0.3;
        brightMat.opacity = origBrightOpacity * 0.4 * flicker;
      }
    }

    if (dimMat) {
      // Dim stars fade faster than bright ones
      dimMat.opacity = origDimOpacity * (0.3 + 0.7 * hr);
    }

    // ── 2. Star color temperature (red shift) ──
    if (origBrightColors && brightStars) {
      const colorAttr = brightStars.geometry.attributes.color as THREE.BufferAttribute;
      const arr = colorAttr.array as Float32Array;
      const redShiftAmount = distress * 0.7; // max 70% shift toward red
      for (let i = 0; i < arr.length; i += 3) {
        _starColor.setRGB(origBrightColors[i], origBrightColors[i + 1], origBrightColors[i + 2]);
        _starColor.lerp(STAR_RED_SHIFT, redShiftAmount);
        arr[i] = _starColor.r;
        arr[i + 1] = _starColor.g;
        arr[i + 2] = _starColor.b;
      }
      colorAttr.needsUpdate = true;
    }

    if (origDimColors && dimStars) {
      const colorAttr = dimStars.geometry.attributes.color as THREE.BufferAttribute;
      const arr = colorAttr.array as Float32Array;
      const redShiftAmount = distress * 0.5; // dim stars shift less
      for (let i = 0; i < arr.length; i += 3) {
        _starColor.setRGB(origDimColors[i], origDimColors[i + 1], origDimColors[i + 2]);
        _starColor.lerp(STAR_RED_SHIFT, redShiftAmount);
        arr[i] = _starColor.r;
        arr[i + 1] = _starColor.g;
        arr[i + 2] = _starColor.b;
      }
      colorAttr.needsUpdate = true;
    }

    // ── 3. Nebula opacity (fades as universe loses energy) ──
    if (nebulaMatRaw && 'opacity' in nebulaMatRaw) {
      const mat = nebulaMatRaw as THREE.MeshBasicMaterial;
      if (mat.transparent) {
        // Full opacity when healthy, fade to 30% when troubled
        mat.opacity = 0.3 + 0.7 * hr;
      }
    }

    // ── 4. Star glitch (position jitter at critical levels) ──
    if (hr < 0.3 && brightStars) {
      const posAttr = brightStars.geometry.attributes.position as THREE.BufferAttribute;
      const posArr = posAttr.array as Float32Array;
      // Only jitter a few stars each frame (cheap, looks organic)
      const jitterCount = Math.floor((0.3 - hr) * 20); // 0-6 stars per frame
      for (let j = 0; j < jitterCount; j++) {
        const idx = Math.floor(Math.random() * (posArr.length / 3)) * 3;
        const jitterMag = (0.3 - hr) * 80; // max ~24 units at hr=0
        posArr[idx] += (Math.random() - 0.5) * jitterMag;
        posArr[idx + 1] += (Math.random() - 0.5) * jitterMag;
        posArr[idx + 2] += (Math.random() - 0.5) * jitterMag;
      }
      posAttr.needsUpdate = true;
    }
  }

  // ---------------------------------------------------------------------------
  // dispose()
  // ---------------------------------------------------------------------------
  function dispose(): void {
    // Restore original colors so disposal leaves scene clean
    if (origBrightColors && brightStars) {
      const colorAttr = brightStars.geometry.attributes.color as THREE.BufferAttribute;
      (colorAttr.array as Float32Array).set(origBrightColors);
      colorAttr.needsUpdate = true;
    }
    if (origDimColors && dimStars) {
      const colorAttr = dimStars.geometry.attributes.color as THREE.BufferAttribute;
      (colorAttr.array as Float32Array).set(origDimColors);
      colorAttr.needsUpdate = true;
    }
    if (brightMat) brightMat.opacity = origBrightOpacity;
    if (dimMat) dimMat.opacity = origDimOpacity;
  }

  return { update, dispose };
}
