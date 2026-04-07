/**
 * AtmosphericPerspective.ts
 *
 * Adds depth cues to the Prime Radiant scene via exponential fog
 * and per-node distance-based opacity fading.
 *
 * Distant governance nodes fade toward the background color, creating
 * a natural depth gradient that makes the 3D layout readable.
 *
 * Uses THREE.FogExp2 (exponential fog) rather than a post-processing pass
 * for maximum compatibility with WebGL2 and WebGPU.
 *
 * Integration checklist (materials that must have fog: false):
 *   - sky-nebula sphere  (SkyboxNebulaTSL — BackSide, at infinity)
 *   - brightStars / dimStars PointsMaterial (starfield — at infinity)
 *   - ambient-dust PointsMaterial (cosmetic particle layer)
 *   - Sun mesh material  (SunMaterialTSL — light source, self-luminous)
 *   - Planet surface materials (PlanetSurfaceTSL — orrery objects)
 *   - Milky Way mesh (galactic band — at infinity)
 *
 * Governance node materials (CrystalNodeMaterials, MeshPhysicalMaterial)
 * respond to fog by default, which is the intended behavior.
 */

import * as THREE from 'three';

// ---------------------------------------------------------------------------
// Fog handle
// ---------------------------------------------------------------------------

export interface AtmosphericPerspectiveHandle {
  /** Update fog density based on camera zoom level */
  updateForZoom(cameraDistance: number): void;
  /** Set fog color (should match scene background) */
  setColor(color: THREE.Color): void;
  /** Enable/disable */
  setEnabled(enabled: boolean): void;
  dispose(): void;
}

export function createAtmosphericPerspective(
  scene: THREE.Scene,
): AtmosphericPerspectiveHandle {
  // FogExp2 -- exponential density feels more natural than linear.
  // Density is very low: the governance graph spans ~200 units while
  // the skybox sits at ~5 000 units (excluded via fog: false).
  const fog = new THREE.FogExp2(0x000005, 0.003); // very dark blue, subtle density
  scene.fog = fog;

  return {
    updateForZoom(cameraDistance: number) {
      // Zoomed out (overview): stronger fog to fade distant nodes.
      // Zoomed in (node closeup): weaker fog to preserve detail.
      const overviewDensity = 0.004;
      const closeupDensity = 0.001;
      const t = Math.min(1, Math.max(0, (cameraDistance - 20) / 180));
      fog.density = closeupDensity + (overviewDensity - closeupDensity) * t;
    },

    setColor(color: THREE.Color) {
      fog.color.copy(color);
    },

    setEnabled(enabled: boolean) {
      scene.fog = enabled ? fog : null;
    },

    dispose() {
      scene.fog = null;
    },
  };
}

// ---------------------------------------------------------------------------
// Distance-based node opacity
// ---------------------------------------------------------------------------

/**
 * Fade governance-node sprites (labels, halos) based on distance to camera.
 * Nodes beyond `maxDistance` fade to a minimum 15 % opacity so they remain
 * faintly visible but do not compete with nearby nodes for attention.
 *
 * Call this once per frame inside the animation tick.
 */
export function updateNodeDistanceFade(
  nodes: THREE.Object3D[],
  cameraPosition: THREE.Vector3,
  maxDistance: number = 150,
): void {
  for (const node of nodes) {
    const dist = node.position.distanceTo(cameraPosition);
    const fade = Math.min(1, Math.max(0.15, 1 - dist / maxDistance));
    node.traverse((child) => {
      if ((child as THREE.Sprite).isSprite) {
        (child.material as THREE.SpriteMaterial).opacity = fade;
      }
    });
  }
}
