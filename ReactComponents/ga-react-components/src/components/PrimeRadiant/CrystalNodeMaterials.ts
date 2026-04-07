// src/components/PrimeRadiant/CrystalNodeMaterials.ts
// Metallic & crystal materials for governance graph nodes.
// Each node type gets a distinct precious material treatment.
// Designed via Octopus UI/UX Design Intelligence.

import * as THREE from 'three';
import type { GovernanceNodeType } from './types';

// ---------------------------------------------------------------------------
// Material definitions — one per governance artifact type
// ---------------------------------------------------------------------------

export interface NodeMaterialDef {
  color: number;
  metalness: number;
  roughness: number;
  clearcoat: number;
  clearcoatRoughness: number;
  transmission: number;     // 0 = opaque metal, >0 = crystal/glass
  ior: number;              // index of refraction (glass ~1.5, crystal ~2.0, diamond ~2.42)
  thickness: number;        // refraction depth
  iridescence: number;      // 0-1 rainbow sheen
  iridescenceIOR: number;
  sheen: number;            // 0-1 velvet/silk sheen
  sheenColor: number;
  emissive: number;
  emissiveIntensity: number;
  envMapIntensity: number;
}

const MATERIAL_DEFS: Record<GovernanceNodeType, NodeMaterialDef> = {
  // Constitution — Polished Gold Crown: commanding, warm, regal
  constitution: {
    color: 0xFFD700,
    metalness: 0.95,
    roughness: 0.08,
    clearcoat: 1.0,
    clearcoatRoughness: 0.05,
    transmission: 0,
    ior: 1.5,
    thickness: 0,
    iridescence: 0.3,
    iridescenceIOR: 1.8,
    sheen: 0.4,
    sheenColor: 0xFFA500,
    emissive: 0xFFAA00,
    emissiveIntensity: 0.15,
    envMapIntensity: 2.0,
  },

  // Department — Brushed Silver Orb: organizational, clean, structural
  department: {
    color: 0xC0C0C0,
    metalness: 0.7,      // reduced from 0.9 — less specular shimmer
    roughness: 0.45,     // increased from 0.25 — softer reflections
    clearcoat: 0.4,      // reduced from 0.8 — less double-reflection flicker
    clearcoatRoughness: 0.3,  // increased from 0.15
    transmission: 0,
    ior: 1.5,
    thickness: 0,
    iridescence: 0.1,
    iridescenceIOR: 1.5,
    sheen: 0.2,
    sheenColor: 0xE8E8E8,
    emissive: 0x888888,
    emissiveIntensity: 0.08,
    envMapIntensity: 1.5,
  },

  // Policy — Patinated Bronze Shield: warm, aged, trustworthy
  policy: {
    color: 0xCD7F32,
    metalness: 0.85,
    roughness: 0.35,
    clearcoat: 0.5,
    clearcoatRoughness: 0.3,
    transmission: 0,
    ior: 1.5,
    thickness: 0,
    iridescence: 0.2,
    iridescenceIOR: 1.6,
    sheen: 0.3,
    sheenColor: 0x8B6914,
    emissive: 0x8B4513,
    emissiveIntensity: 0.1,
    envMapIntensity: 1.2,
  },

  // Persona — Amethyst Crystal: translucent purple, mystical inner glow
  persona: {
    color: 0x9966CC,
    metalness: 0.05,
    roughness: 0.05,
    clearcoat: 1.0,
    clearcoatRoughness: 0.02,
    transmission: 0.85,
    ior: 1.9,
    thickness: 1.5,
    iridescence: 0.6,
    iridescenceIOR: 2.0,
    sheen: 0,
    sheenColor: 0x000000,
    emissive: 0x7B2FBE,
    emissiveIntensity: 0.25,
    envMapIntensity: 1.8,
  },

  // Pipeline — Oxidized Copper Wire: teal patina, iridescent
  pipeline: {
    color: 0xB87333,
    metalness: 0.8,
    roughness: 0.4,
    clearcoat: 0.6,
    clearcoatRoughness: 0.2,
    transmission: 0,
    ior: 1.5,
    thickness: 0,
    iridescence: 0.8,
    iridescenceIOR: 1.7,
    sheen: 0.5,
    sheenColor: 0x2FCED6,
    emissive: 0x2E8B8B,
    emissiveIntensity: 0.12,
    envMapIntensity: 1.4,
  },

  // Schema — Diamond: clear crystal, maximum refraction, brilliant
  schema: {
    color: 0xE8E8F0,
    metalness: 0,
    roughness: 0,
    clearcoat: 1.0,
    clearcoatRoughness: 0,
    transmission: 0.95,
    ior: 2.42,
    thickness: 0.8,
    iridescence: 0.4,
    iridescenceIOR: 2.2,
    sheen: 0,
    sheenColor: 0x000000,
    emissive: 0xAABBFF,
    emissiveIntensity: 0.15,
    envMapIntensity: 3.0,
  },

  // Test — Emerald: green gem, faceted, deep color
  test: {
    color: 0x50C878,
    metalness: 0.05,
    roughness: 0.08,
    clearcoat: 1.0,
    clearcoatRoughness: 0.05,
    transmission: 0.75,
    ior: 1.58,
    thickness: 1.2,
    iridescence: 0.3,
    iridescenceIOR: 1.7,
    sheen: 0,
    sheenColor: 0x000000,
    emissive: 0x228B22,
    emissiveIntensity: 0.2,
    envMapIntensity: 2.0,
  },

  // IXQL — Rose Gold: modern, warm pink-gold, reflective
  ixql: {
    color: 0xE8A090,
    metalness: 0.92,
    roughness: 0.12,
    clearcoat: 1.0,
    clearcoatRoughness: 0.08,
    transmission: 0,
    ior: 1.5,
    thickness: 0,
    iridescence: 0.5,
    iridescenceIOR: 1.9,
    sheen: 0.6,
    sheenColor: 0xFFB6C1,
    emissive: 0xCC6666,
    emissiveIntensity: 0.1,
    envMapIntensity: 2.0,
  },
};

// ---------------------------------------------------------------------------
// Material cache — one MeshPhysicalMaterial per type
// ---------------------------------------------------------------------------

const materialCache = new Map<string, THREE.MeshPhysicalMaterial>();

/** Detect low-end device (mobile or few cores) */
const _isLowEnd = typeof navigator !== 'undefined' && (
  /Mobi|Android/i.test(navigator.userAgent) || navigator.hardwareConcurrency <= 4
);

/** Create or retrieve a cached MeshPhysicalMaterial for a node type */
export function getNodeMaterial(type: GovernanceNodeType, lowEnd?: boolean): THREE.MeshPhysicalMaterial {
  const isLow = lowEnd ?? _isLowEnd;
  const cacheKey = `${type}-${isLow ? 'low' : 'high'}`;
  const cached = materialCache.get(cacheKey);
  if (cached) return cached;

  const def = MATERIAL_DEFS[type];
  if (!def) {
    const fallback = new THREE.MeshPhysicalMaterial({
      color: 0x888888,
      metalness: 0.5,
      roughness: 0.3,
      clearcoat: 0.5,
      transparent: true,
      opacity: 0.9,
    });
    materialCache.set(cacheKey, fallback);
    return fallback;
  }

  // On low-end: disable transmission (avoids double-render), reduce iridescence
  const transmission = isLow ? 0 : def.transmission;
  const iridescence = isLow ? Math.min(def.iridescence, 0.2) : def.iridescence;

  const mat = new THREE.MeshPhysicalMaterial({
    color: def.color,
    metalness: def.metalness,
    roughness: isLow ? Math.max(def.roughness, 0.2) : def.roughness,
    clearcoat: isLow ? Math.min(def.clearcoat, 0.5) : def.clearcoat,
    clearcoatRoughness: def.clearcoatRoughness,
    transmission,
    ior: def.ior,
    thickness: transmission > 0 ? def.thickness : 0,
    iridescence,
    iridescenceIOR: def.iridescenceIOR,
    sheen: def.sheen,
    sheenColor: new THREE.Color(def.sheenColor),
    emissive: new THREE.Color(def.emissive),
    emissiveIntensity: def.emissiveIntensity,
    envMapIntensity: isLow ? 1.0 : def.envMapIntensity,
    transparent: transmission > 0 || isLow,
    opacity: transmission > 0 ? 0.95 : (isLow && def.transmission > 0 ? 0.85 : 1.0),
    side: THREE.DoubleSide,
  });

  materialCache.set(cacheKey, mat);
  return mat;
}

/** Get a clone with adjusted emissive intensity (for health-based glow) */
export function getNodeMaterialWithGlow(
  type: GovernanceNodeType,
  glowMultiplier: number,
): THREE.MeshPhysicalMaterial {
  const base = getNodeMaterial(type);
  const clone = base.clone();
  const def = MATERIAL_DEFS[type];
  if (def) {
    clone.emissiveIntensity = def.emissiveIntensity * glowMultiplier;
  }
  return clone;
}

/** Get the raw material definition for a type (for UI/debug) */
export function getMaterialDef(type: GovernanceNodeType): NodeMaterialDef | undefined {
  return MATERIAL_DEFS[type];
}

// ---------------------------------------------------------------------------
// Governance Redshift / Blueshift — compliance drift visible as color shift
// ---------------------------------------------------------------------------

// Pre-allocated colors to avoid per-frame allocation
const _blueShift = new THREE.Color(0.1, 0.3, 1.0);
const _redShift  = new THREE.Color(1.0, 0.2, 0.05);

/**
 * Apply a spectral shift to a node's emissive color based on compliance direction.
 *
 * - complianceDelta > 0 → blueshift  (moving toward compliance)
 * - complianceDelta < 0 → redshift   (drifting away from compliance)
 * - complianceDelta = 0 → neutral    (no shift)
 *
 * The shift is subtle (lerp capped at 0.4) so it tints rather than replaces
 * the material's base emissive color.
 */
export function applyGovernanceShift(
  material: THREE.MeshPhysicalMaterial,
  complianceDelta: number, // -1.0 … +1.0
): void {
  // Lazily cache the original emissive so we can always blend from it
  if (!material.userData.baseEmissive) {
    material.userData.baseEmissive = material.emissive.clone();
    material.userData.baseEmissiveIntensity = material.emissiveIntensity;
  }

  const base = material.userData.baseEmissive as THREE.Color;
  const baseIntensity = material.userData.baseEmissiveIntensity as number;
  const shift = Math.abs(complianceDelta);

  const shiftColor = complianceDelta > 0 ? _blueShift : _redShift;

  // Tint emissive toward the shift color — max 40 % blend
  material.emissive.copy(base).lerp(shiftColor, shift * 0.4);
  // Slightly boost emissive intensity proportional to drift magnitude
  material.emissiveIntensity = baseIntensity + shift * 0.3;
}

// ---------------------------------------------------------------------------
// Fresnel rim glow — crystal edges glow brighter than centers
// ---------------------------------------------------------------------------

/**
 * Enhance a material with Fresnel-style rim glow via sheen.
 * Uses the material's emissive color at half intensity for a holographic edge.
 * Call once after cloning; idempotent (caches base values).
 */
export function applyFresnelRimGlow(
  material: THREE.MeshPhysicalMaterial,
): void {
  if (material.userData.fresnelApplied) return;
  material.userData.fresnelApplied = true;

  // Derive rim color from emissive at 50 % brightness
  const rimColor = material.emissive.clone().multiplyScalar(0.5);
  material.sheenColor.copy(rimColor);
  material.sheen = Math.max(material.sheen, 0.3);
}

// ---------------------------------------------------------------------------
// Stress fractures — roughness / clearcoat degrade with health
// ---------------------------------------------------------------------------

const STRESS_ROUGHNESS: Record<string, number> = {
  healthy: 0.05,
  warning: 0.25,
  error: 0.55,
  unknown: 0.35,
  contradictory: 0.45,
};

const STRESS_CLEARCOAT: Record<string, number> = {
  healthy: 1.0,
  warning: 0.7,
  error: 0.2,
  unknown: 0.5,
  contradictory: 0.3,
};

/**
 * Modify material roughness and clearcoat based on node health.
 *
 * - Healthy: smooth crystal (low roughness, full clearcoat)
 * - Warning: slight surface roughness (micro-fractures forming)
 * - Error: high roughness + reduced clearcoat (fractured, dull)
 * - Contradictory: anisotropic roughness (directional stress)
 *
 * Caches original values on first call so repeated invocations blend from
 * the true baseline, not from a previously-shifted state.
 */
export function applyStressFractures(
  material: THREE.MeshPhysicalMaterial,
  health: string,
): void {
  if (material.userData.baseRoughness === undefined) {
    material.userData.baseRoughness = material.roughness;
    material.userData.baseClearcoat = material.clearcoat;
  }
  material.roughness = STRESS_ROUGHNESS[health] ?? 0.3;
  material.clearcoat = STRESS_CLEARCOAT[health] ?? 0.5;
}

// ---------------------------------------------------------------------------
// Crystal clarity — stable nodes become more transparent over time
// ---------------------------------------------------------------------------

/**
 * Gradually increase transmission clarity for nodes that have been healthy
 * for a sustained period. Only affects transmission-capable materials
 * (persona, schema, test — those with baseTransmission > 0).
 *
 * @param stabilityDuration  Seconds since the last health-state change.
 *                           Full clarity is reached at 86 400 s (24 h).
 */
export function applyCrystalClarity(
  material: THREE.MeshPhysicalMaterial,
  stabilityDuration: number,
): void {
  // Cache base transmission on first call
  if (material.userData.baseTransmission === undefined) {
    material.userData.baseTransmission = material.transmission;
    material.userData.baseThickness = material.thickness;
  }

  const baseT = material.userData.baseTransmission as number;
  if (baseT <= 0) return; // opaque metals — nothing to clarify

  // 0 → 1 over 24 hours of uninterrupted stability
  const clarity = Math.min(1, stabilityDuration / 86400);

  material.transmission = Math.min(0.95, baseT + clarity * 0.2);
  material.thickness = Math.max(0.3, (material.userData.baseThickness as number) - clarity * 0.5);
}

// ---------------------------------------------------------------------------
// Governance activity iridescence — recent actions make nodes shimmer
// ---------------------------------------------------------------------------

/**
 * Heighten iridescence on nodes that recently participated in a governance
 * action (audit pass, policy evaluation, constitutional check, etc.).
 *
 * @param activityLevel  0-1 value that decays over time after an event.
 */
export function applyGovernanceActivity(
  material: THREE.MeshPhysicalMaterial,
  activityLevel: number,
): void {
  if (material.userData.baseIridescence === undefined) {
    material.userData.baseIridescence = material.iridescence;
    material.userData.baseIridescenceIOR = material.iridescenceIOR;
  }

  const baseIri = material.userData.baseIridescence as number;
  material.iridescence = Math.min(1.0, baseIri + activityLevel * 0.6);
  material.iridescenceIOR = (material.userData.baseIridescenceIOR as number) + activityLevel * 0.3;
}

/** Dispose all cached materials */
export function disposeCrystalMaterials(): void {
  for (const mat of materialCache.values()) mat.dispose();
  materialCache.clear();
}
