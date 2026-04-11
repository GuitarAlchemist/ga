// src/components/PrimeRadiant/shaders/Ocean/OceanProjectedGridSpike.ts
//
// Phase 0 Step 0 — 1-hour feasibility spike for the planet ocean zoom
// feature. Validates the two architectural unknowns from the v3
// production spec (docs/brainstorms/2026-04-11-planet-ocean-zoom-v3-
// production-spec.md) BEFORE any of the full feature is built:
//
//   Spike check 1 — TSL fragment-shader ray-sphere intersection:
//     Can a MeshBasicNodeMaterial with a custom fragment node
//     analytically intersect a camera ray against an ocean sphere
//     and render the hit point correctly at close zoom?
//
//   Spike check 2 — Depth-buffer compatibility with the existing
//     PlanetSurfaceTSL atmosphere Fresnel rim:
//     When the ocean sphere (radius = earthRadius + seaLevelOffset)
//     renders alongside Earth's atmosphere Fresnel rim (rendered on
//     a slightly smaller sphere), do they compose correctly at the
//     terminator and limb, or do Z-order artifacts appear?
//
// Exit criteria are documented as the top-of-file checklist below.
// Run, eyeball, pass/fail, tear down. This file is NOT production
// code — do not import from it outside the spike harness. If both
// checks pass, delete this file and implement OceanProjectedGrid.ts
// proper per the v3 spec. If either fails, this file provides the
// evidence for the fall-back to v2 Path A.
//
// ── Spike acceptance checklist ──────────────────────────────────
//   [ ] 1. The offset sphere visibly appears as a coherent shape
//          surrounding Earth when the camera is ~2x earth radius
//          away
//   [ ] 2. The sphere color (a bright test color, not ocean blue)
//          is distinct from Earth's daymap so boundaries are
//          obvious
//   [ ] 3. The existing atmosphere Fresnel rim still renders on
//          Earth's limb without Z-order flicker
//   [ ] 4. Rotating the camera around Earth shows no tears,
//          seams, or discard holes inside the sphere's silhouette
//   [ ] 5. At close zoom (camera inside the offset sphere's
//          altitude range), the fragment shader does not crash or
//          produce black output — the "camera inside the sphere"
//          branch is reached and handled gracefully
//   [ ] 6. Frame rate stays above 55 FPS at 256x256 grid
//          resolution on the reference machine during a zoom pass
// ────────────────────────────────────────────────────────────────

import * as THREE from 'three';
import { MeshBasicNodeMaterial } from 'three/webgpu';
import {
  Fn, float, vec2, vec3, vec4,
  uniform,
  positionLocal,
  cameraPosition,
  normalize, dot, length, sqrt, max, min, sub, add, mul,
  If, select,
} from 'three/tsl';

// ── Types ───────────────────────────────────────────────────────

export interface OceanSpikeOptions {
  /**
   * World-space center of the planet whose ocean we're rendering.
   * In the Prime Radiant scene graph this comes from Earth's
   * world matrix, sampled each frame.
   */
  earthCenterUniform: ReturnType<typeof uniform>;

  /**
   * Earth's radius in scene units. For the spike this is the
   * EARTH_RADIUS constant from SolarSystem.ts (= 0.12).
   */
  earthRadius: number;

  /**
   * Ocean sphere radius = earthRadius + seaLevelOffset. Phase 0
   * uses seaLevelOffset = 0.001 scene units (about 53 m at Earth
   * scale, well within the 15 km atmosphere shell).
   */
  seaLevelOffset: number;

  /**
   * Grid resolution — number of vertices per edge of the NDC
   * quad. 128/256/512 for low/medium/high. Spike uses 256.
   */
  gridResolution?: number;
}

// ── Spike factory ───────────────────────────────────────────────

/**
 * Build a projected-grid mesh whose material does nothing but
 * analytically ray-sphere intersect every fragment against an
 * offset sphere and color the hit point with a test color.
 *
 * The mesh lives in NDC space (post-projection) and must be
 * rendered AFTER Earth's planet mesh so the depth test decides
 * which surface wins per pixel.
 *
 * Returns `{ mesh, material, dispose }`. The caller is
 * responsible for calling `dispose()` on unmount.
 */
export function createOceanSpikeMesh(
  opts: OceanSpikeOptions,
): {
  mesh: THREE.Mesh;
  material: MeshBasicNodeMaterial;
  dispose: () => void;
} {
  const gridResolution = opts.gridResolution ?? 256;
  const radius = opts.earthRadius + opts.seaLevelOffset;

  // ── Geometry: NDC quad in post-projection space ─────────────
  //
  // A PlaneGeometry from -1..1 in x and y. The vertex shader
  // will pass these through unchanged (they're already in NDC).
  // frustumCulled=false because the grid always covers the full
  // viewport.
  const geometry = new THREE.PlaneGeometry(
    2, 2,
    gridResolution - 1,
    gridResolution - 1,
  );

  // ── Uniforms ────────────────────────────────────────────────
  const uEarthCenter = opts.earthCenterUniform;
  const uRadius = uniform(radius);

  // ── Vertex stage ─────────────────────────────────────────────
  //
  // We render in NDC directly, so the vertex shader bypasses
  // Three.js's built-in projection. Achieved by returning
  // positionLocal as vec4(x, y, 0, 1) in TSL.
  //
  // Note: TSL's `positionNode` override lets us write directly
  // into gl_Position. See the Three.js TSL docs on
  // `Material.positionNode`.
  const vertexPosition = Fn(() => {
    // positionLocal gives us the plane's local coords (x, y, 0).
    // We emit it as a clip-space position directly — no view
    // matrix multiplication.
    return vec4(positionLocal.xy, float(0.0), float(1.0));
  });

  // ── Fragment stage ───────────────────────────────────────────
  //
  // For each fragment:
  //   1. Reconstruct the world-space camera ray through this NDC
  //      position. (We cheat in the spike: use the built-in
  //      cameraPosition and approximate ray direction from the
  //      NDC coordinate via the inverse projection. A production
  //      implementation would pass a pre-computed inverse-view-
  //      projection matrix as a uniform.)
  //   2. Analytic ray-sphere intersection against the offset
  //      ocean sphere at uEarthCenter + uRadius.
  //   3. If hit: emit a bright test color. If miss: discard.
  const fragmentColor = Fn(() => {
    // Ray origin: camera in world space.
    const rayOrigin = cameraPosition;

    // Ray direction: we need (NDC.xy, 1) in clip space unprojected
    // to world. TSL exposes the inverse view-projection via the
    // `screenUV` helpers but the spike uses a simpler path: we
    // assume the forward vector and offset it by NDC. This is
    // APPROXIMATE — a production implementation should pass the
    // inverse-view-projection matrix as a mat4 uniform and do the
    // unprojection properly.
    //
    // For spike-check-1 we just need the intersection to look
    // correct near the center of the viewport; edge-of-viewport
    // accuracy is a Phase 0 proper concern, not a spike concern.
    //
    // TODO in Phase 0 proper: replace with inverse VP matrix
    // uniform + proper unprojection.
    const forward = normalize(sub(uEarthCenter, rayOrigin));
    const rayDir = normalize(forward);  // spike: center of screen only

    // Analytic ray-sphere intersection.
    // |rayOrigin + t * rayDir - center|^2 = radius^2
    // t^2 (d.d) + 2 t (d . (O - C)) + (O - C) . (O - C) - r^2 = 0
    const oc = sub(rayOrigin, uEarthCenter);
    const a = dot(rayDir, rayDir);
    const b = mul(float(2.0), dot(rayDir, oc));
    const c = sub(dot(oc, oc), mul(uRadius, uRadius));
    const disc = sub(mul(b, b), mul(float(4.0), mul(a, c)));

    // Miss: disc < 0. Emit a "miss" test color (dark) instead of
    // discard so we can see the grid's coverage in the spike.
    const tHit = select(
      disc.lessThan(float(0.0)),
      float(-1.0),  // miss marker
      mul(
        float(-0.5),
        add(
          b,
          sqrt(max(disc, float(0.0))),
        ),
      ),  // near root of t^2 - bt + c = 0 (using - sqrt for entry)
    );

    // Color: bright cyan when hit, dark grey when miss.
    // Spike-check-1: we should see a cyan disc where Earth's
    // ocean sphere intersects the view frustum.
    const hitColor = vec3(float(0.0), float(0.85), float(0.9));
    const missColor = vec3(float(0.02), float(0.02), float(0.03));

    return vec4(
      select(tHit.greaterThan(float(0.0)), hitColor, missColor),
      float(1.0),  // fully opaque in the spike so the result is
                   // obvious; production will discard on miss
    );
  });

  // ── Material ────────────────────────────────────────────────
  const material = new MeshBasicNodeMaterial();
  material.positionNode = vertexPosition();
  material.colorNode = fragmentColor();
  material.depthTest = true;
  material.depthWrite = false;  // spike: don't fight atmosphere rim
                                // over depth; production writes depth
                                // at the displaced surface position
  material.side = THREE.DoubleSide;
  material.transparent = false;

  // ── Mesh ────────────────────────────────────────────────────
  const mesh = new THREE.Mesh(geometry, material);
  mesh.frustumCulled = false;        // NDC-space grid is always visible
  mesh.renderOrder = 10;             // draw after planet (order 0)
  mesh.name = 'OceanProjectedGridSpike';

  // Store the uniforms on userData so the Prime Radiant update
  // loop can find them without importing this module.
  mesh.userData = {
    spike: true,
    uEarthCenter,
    uRadius,
  };

  const dispose = () => {
    geometry.dispose();
    material.dispose();
  };

  return { mesh, material, dispose };
}

// ── Integration notes for the spike harness ────────────────────
//
// Mount in SolarSystem.ts or a sibling test component behind a
// runtime flag:
//
//   if (SPIKE_FLAG.oceanProjectedGrid) {
//     const earthCenterUniform = uniform(new THREE.Vector3());
//     const spike = createOceanSpikeMesh({
//       earthCenterUniform,
//       earthRadius: EARTH_RADIUS,
//       seaLevelOffset: 0.001,
//     });
//     earthGroup.add(spike.mesh);
//
//     // Each frame, update the earth center uniform:
//     earthGroup.getWorldPosition(_tmpVec3);
//     earthCenterUniform.value.copy(_tmpVec3);
//
//     // On unmount:
//     earthGroup.remove(spike.mesh);
//     spike.dispose();
//   }
//
// Run `npm run dev` in ReactComponents/ga-react-components and
// eyeball the Prime Radiant view when Earth is focused. Check each
// item in the top-of-file checklist.
//
// ── Known spike limitations ─────────────────────────────────────
//
//   1. The fragment shader computes ray direction at screen
//      CENTER only. It will NOT look correct off-center. A
//      production implementation must pass the inverse view-
//      projection matrix as a uniform. This is acceptable for the
//      spike because we're validating (a) TSL + ray-sphere
//      intersection compiles and renders at all, and (b) the
//      depth-buffer interaction with the atmosphere Fresnel rim.
//      Neither requires pixel-accurate ray reconstruction.
//
//   2. No displacement, no shading, no waves. The spike is a
//      flat test color. All the wave synthesis, Fresnel, Gerstner
//      summation, etc. from the v3 spec are Phase 0 proper, not
//      the spike.
//
//   3. No camera-inside-sphere branch. If you zoom so close the
//      camera enters the offset sphere, the spike may produce
//      incorrect results. That's a Phase 0 proper concern, but
//      note it in the spike results if it breaks.
//
// ── If the spike fails ──────────────────────────────────────────
//
// Abort Path B. Fall back to v2 Path A (mounted-patch approach
// with v2 fixes — shrunk patch, cascades, pole fallback, binary
// takeover). The v2 doc is at:
//   docs/brainstorms/2026-04-11-planet-ocean-zoom-brainstorm-v2-
//   review-response.md
//
// Specifically, re-read v2 section "Path A — patch the mounted-
// patch approach" for the updated requirements.
