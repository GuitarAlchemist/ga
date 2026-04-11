// src/components/PrimeRadiant/shaders/Ocean/OceanProjectedGridSpike.ts
//
// Phase 0 Step 0 — 1-hour feasibility spike for the planet ocean zoom
// feature. Validates the two architectural unknowns from the v3
// production spec (docs/brainstorms/2026-04-11-planet-ocean-zoom-v3-
// production-spec.md) BEFORE any of the full feature is built:
//
//   Spike check 1 — TSL fragment-shader ray-sphere intersection:
//     Can a MeshBasicNodeMaterial with a custom position + color
//     node (a) bypass Three.js's default projection to render a
//     full-screen quad, and (b) do per-fragment ray-sphere
//     intersection with correct per-pixel rays?
//
//   Spike check 2 — Depth-buffer compatibility with the existing
//     PlanetSurfaceTSL atmosphere Fresnel rim:
//     When the spike writes depth at the ray-sphere hit point,
//     does the existing atmosphere rim on Earth's smaller sphere
//     still render correctly at the terminator and limb, or do
//     visible Z-order artifacts appear?
//
// This is NOT production code. Flat test color, full-screen quad,
// no waves, no shading beyond "hit = cyan / miss = discard". Run,
// eyeball the 6-item checklist below, mark pass/fail, tear down.
//
// ── Spike acceptance checklist ──────────────────────────────────
//   [ ] 1. A cyan spherical shape is visible at Earth's position,
//          slightly larger than Earth itself (the sea-level
//          offset should be imperceptible but the sphere should
//          be unmistakably there)
//   [ ] 2. The sphere's silhouette is correct at all camera
//          angles — rotating the camera shows no tears, seams, or
//          discard holes inside the ocean sphere
//   [ ] 3. The existing atmosphere Fresnel rim from
//          PlanetSurfaceTSL still renders on Earth's limb without
//          Z-order flicker OR is hidden entirely behind the ocean
//          sphere (either outcome is acceptable; flicker is not)
//   [ ] 4. At close zoom (camera within the ocean-sphere altitude
//          band), the fragment shader does NOT crash and does
//          NOT produce black output. Either the "camera inside
//          sphere" branch renders correctly or a known-safe
//          fallback color shows
//   [ ] 5. The non-ocean fragments (ray misses sphere) discard
//          cleanly so the underlying PlanetSurfaceTSL daymap +
//          night lights are visible through the hole
//   [ ] 6. Frame rate stays above 55 FPS during a 10-second
//          guided zoom from orbit to close Earth on the reference
//          machine
// ────────────────────────────────────────────────────────────────
//
// If all 6 pass: delete this file, implement the real
// OceanProjectedGrid.ts + OceanLODHandle.ts + shoreMask.ts per the
// v3 spec Phase 0 deliverables.
//
// If checks 1 or 2 fail: abort Path B, fall back to v2 Path A. See
// docs/brainstorms/2026-04-11-planet-ocean-zoom-brainstorm-v2-
// review-response.md section "Path A — patch the mounted-patch
// approach" for the alternate plan.

import * as THREE from 'three';
import { MeshBasicNodeMaterial } from 'three/webgpu';
import {
  Fn, float, vec3, vec4,
  uniform,
  positionLocal,
  screenUV,
  normalize, dot, sqrt, max, mul, sub, add,
  select,
} from 'three/tsl';

// ── Types ───────────────────────────────────────────────────────

export interface OceanSpikeOptions {
  /**
   * World-space center of the planet whose ocean we're rendering.
   * Each frame, the caller must copy `earthGroup.getWorldPosition(...)`
   * into `earthCenterUniform.value` so the shader sees the current
   * position.
   */
  earthCenterUniform: ReturnType<typeof uniform>;

  /**
   * Inverse of the current view-projection matrix. Unprojects
   * clip-space NDC back to world space so the fragment shader
   * can reconstruct per-pixel camera rays.
   *
   * Each frame:
   *   _tmpMat.copy(camera.projectionMatrix)
   *     .invert()
   *     .premultiply(camera.matrixWorld);
   *   invViewProjUniform.value.copy(_tmpMat);
   *
   * (Three.js's `camera.matrixWorld` is camera-to-world and
   * `camera.projectionMatrixInverse` is clip-to-view. The combined
   * `matrixWorld * projectionMatrixInverse` maps clip to world.)
   */
  invViewProjUniform: ReturnType<typeof uniform>;

  /**
   * Earth's radius in scene units. For the spike this is the
   * EARTH_RADIUS constant from SolarSystem.ts (= 0.12 at the
   * current scale).
   */
  earthRadius: number;

  /**
   * Ocean sphere radius = earthRadius + seaLevelOffset. Phase 0
   * uses seaLevelOffset ≈ 0.001 scene units (~53 m at Earth
   * scale, well within the atmosphere shell).
   */
  seaLevelOffset: number;
}

// ── Spike factory ───────────────────────────────────────────────

/**
 * Build a full-screen quad mesh whose material ray-sphere
 * intersects every fragment against an offset ocean sphere and
 * colors the hit point with a test color.
 *
 * Geometry is a 2×2 plane (two triangles) in local space [-1, 1].
 * The material's positionNode emits those coordinates directly as
 * clip-space, bypassing the view/projection matrix pipeline. This
 * turns the mesh into a full-screen quad regardless of where it
 * lives in the scene graph.
 *
 * Depth writes are enabled at the ray-sphere hit depth, so
 * spike-check-2 can observe depth interaction with Earth's
 * existing atmosphere Fresnel rim.
 *
 * Returns `{ mesh, material, earthCenterUniform, invViewProjUniform,
 * dispose }`. The caller owns the uniforms and must update them
 * each frame — see the "Integration notes" block at the bottom.
 */
export function createOceanSpikeMesh(
  opts: OceanSpikeOptions,
): {
  mesh: THREE.Mesh;
  material: MeshBasicNodeMaterial;
  dispose: () => void;
} {
  const oceanRadius = opts.earthRadius + opts.seaLevelOffset;

  // ── Geometry: two-triangle full-screen quad ─────────────────
  //
  // Local coords are [-1, 1] in x and y. The vertex shader will
  // emit them as clip-space positions directly. Two triangles is
  // all we need — per-vertex work is zero; all interesting logic
  // lives in the fragment shader.
  const geometry = new THREE.PlaneGeometry(2, 2, 1, 1);

  // ── Uniforms ────────────────────────────────────────────────
  const uEarthCenter = opts.earthCenterUniform;
  const uInvViewProj = opts.invViewProjUniform;
  const uRadius = uniform(oceanRadius);

  // ── Vertex stage ─────────────────────────────────────────────
  //
  // Emit local position as clip-space position directly. This
  // bypasses Three.js's modelViewMatrix and projectionMatrix so
  // the mesh fills the viewport regardless of camera.
  //
  // Z = 0 in clip space (middle of the near/far range) and W = 1.
  // The fragment shader will compute actual depth per-pixel from
  // the ray-sphere hit point.
  const vertexPosition = Fn(() => {
    return vec4(positionLocal.x, positionLocal.y, float(0.0), float(1.0));
  });

  // ── Fragment stage ───────────────────────────────────────────
  //
  // Per fragment:
  //   1. Reconstruct world-space camera ray from screen-space UV
  //      via the inverse view-projection uniform
  //   2. Analytic ray-sphere intersection against the ocean
  //      sphere at uEarthCenter + uRadius
  //   3. If hit: emit bright cyan, preserve depth at the hit
  //      point. If miss: emit miss color.
  //
  // Note: actual per-pixel depth writing requires either a
  // fragment-shader depth output or Three.js's built-in
  // gl_FragDepth equivalent in TSL. TSL's `depthNode` on the
  // material lets us override fragment depth. For the spike we
  // use a conservative constant depth (1.0, far) when the ray
  // misses to avoid occluding Earth; production will compute the
  // actual hit depth.
  const fragmentColor = Fn(() => {
    // Unproject the current fragment's NDC position to two world-
    // space points (near plane z=-1, far plane z=+1 in clip),
    // then build a world-space ray from them.
    //
    // screenUV is [0, 1] in screen space; convert to NDC [-1, 1].
    // Note: Three.js screenUV is (0,0) at the bottom-left on WebGL
    // and top-left on WebGPU; the ray reconstruction is symmetric
    // so this doesn't matter for the spike.
    const ndcX = sub(mul(screenUV.x, float(2.0)), float(1.0));
    const ndcY = sub(mul(screenUV.y, float(2.0)), float(1.0));

    // Unproject the near-plane clip point [ndc.x, ndc.y, -1, 1]
    // through uInvViewProj to world space. Do the mat4 * vec4
    // multiply manually in TSL by accessing rows.
    //
    // TSL note: matrix-vector mul syntax is uniform.mul(vec).
    // Three.js TSL exposes this via the `mul` node on mat4
    // uniforms. If `uInvViewProj.mul(...)` doesn't compile, fall
    // back to `mul(uInvViewProj, ...)` or a .element(row, col)
    // accessor.
    const clipNear = vec4(ndcX, ndcY, float(-1.0), float(1.0));
    const clipFar = vec4(ndcX, ndcY, float(1.0), float(1.0));

    // eslint-disable-next-line @typescript-eslint/no-explicit-any
    const worldNear4 = (uInvViewProj as any).mul(clipNear) as ReturnType<typeof vec4>;
    // eslint-disable-next-line @typescript-eslint/no-explicit-any
    const worldFar4 = (uInvViewProj as any).mul(clipFar) as ReturnType<typeof vec4>;

    // Perspective divide to get world-space positions.
    const worldNear = worldNear4.xyz.div(worldNear4.w);
    const worldFar = worldFar4.xyz.div(worldFar4.w);

    // Camera ray: origin at near plane, direction toward far plane.
    const rayOrigin = worldNear;
    const rayDir = normalize(sub(worldFar, worldNear));

    // ── Analytic ray-sphere intersection ─────────────────────
    //
    // |rayOrigin + t * rayDir - center|^2 = radius^2
    //
    // Expand:
    //   t^2 (d . d) + 2 t (d . (O - C)) + (O - C) . (O - C) - r^2 = 0
    //
    // Since rayDir is normalized, (d . d) = 1 so we omit `a`.
    const oc = sub(rayOrigin, uEarthCenter);
    const b = mul(float(2.0), dot(rayDir, oc));
    const cCoef = sub(dot(oc, oc), mul(uRadius, uRadius));
    const disc = sub(mul(b, b), mul(float(4.0), cCoef));

    // Entry-root t (use the negative sqrt branch).
    // Safe sqrt via max(disc, 0) to avoid NaN in the miss branch;
    // we gate on disc > 0 via select afterward so the sqrt value
    // in the miss case is discarded.
    const safeDisc = max(disc, float(0.0));
    const tEntry = mul(float(-0.5), add(b, sqrt(safeDisc)));

    // Hit if disc > 0 AND entry t > 0 (sphere is in front of the
    // camera, not behind it).
    const hit = disc.greaterThan(float(0.0));
    // Additional "t > 0" gate — skip for simplicity in the spike;
    // if the camera enters the sphere, the entry t will be
    // negative and the exit t would be positive. Acceptable
    // spike behavior: camera-inside-sphere will produce one of
    // (a) a visible hit if the exit t is positive, (b) a discard
    // to the miss color. Either is informative.

    // Colors.
    const hitColor = vec3(float(0.0), float(0.85), float(0.9));
    const missColor = vec3(float(0.05), float(0.0), float(0.0));  // red tint
                                                                  // so misses
                                                                  // are obvious
                                                                  // during
                                                                  // debugging

    return vec4(
      select(hit, hitColor, missColor),
      float(1.0),
    );

    // Intentionally unused in the spike color output — preserved
    // here so the linter doesn't complain about `tEntry` while we
    // decide whether to also write depth. Production Phase 0
    // proper will use `tEntry` to compute the displaced hit
    // position and write depth.
    // Referencing tEntry in a no-op comparison so TypeScript sees
    // it as consumed.
    // (Intentionally discarded — TSL node values are referenced
    // at shader-graph build time regardless of runtime use.)
    void tEntry;
  });

  // ── Material ────────────────────────────────────────────────
  const material = new MeshBasicNodeMaterial();
  // eslint-disable-next-line @typescript-eslint/no-explicit-any
  (material as any).positionNode = vertexPosition();
  // eslint-disable-next-line @typescript-eslint/no-explicit-any
  (material as any).colorNode = fragmentColor();

  // Depth settings for spike-check-2.
  //
  // depthWrite=true so Z-order interaction with the atmosphere
  // Fresnel rim is OBSERVABLE (the whole point of check 2).
  // depthTest=true so Earth's opaque surface still occludes the
  // ocean where the ray hits land.
  //
  // Since the full-screen quad is at clip-space z=0 (middle of
  // depth range), the depth buffer will be written with that
  // constant depth. This is acceptable for the spike: we want to
  // see IF any Z-order artifact appears at the limb, even if the
  // written depth is not physically accurate. Phase 0 proper
  // will output the correct ray-sphere hit depth via TSL's
  // depthNode.
  material.depthTest = true;
  material.depthWrite = true;
  material.side = THREE.DoubleSide;
  material.transparent = false;

  // ── Mesh ────────────────────────────────────────────────────
  const mesh = new THREE.Mesh(geometry, material);
  mesh.frustumCulled = false;  // full-screen quad is always visible
  mesh.renderOrder = 10;       // after planet (renderOrder 0), before
                               // postprocess
  mesh.matrixAutoUpdate = false; // transform is ignored anyway
  mesh.name = 'OceanProjectedGridSpike';

  // Store references so the caller can find them without importing
  // this module's types.
  mesh.userData = {
    spike: true,
    uEarthCenter,
    uInvViewProj,
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
// Mount in SolarSystem.ts near Earth's group creation, behind a
// runtime flag:
//
//   import { uniform } from 'three/tsl';
//   import { createOceanSpikeMesh } from './shaders/Ocean/OceanProjectedGridSpike';
//
//   const ENABLE_OCEAN_SPIKE = true;
//   const _tmpVec3 = new THREE.Vector3();
//   const _tmpMat4 = new THREE.Matrix4();
//
//   if (ENABLE_OCEAN_SPIKE) {
//     const earthCenterUniform = uniform(new THREE.Vector3());
//     const invViewProjUniform = uniform(new THREE.Matrix4());
//     const spike = createOceanSpikeMesh({
//       earthCenterUniform,
//       invViewProjUniform,
//       earthRadius: EARTH_RADIUS,
//       seaLevelOffset: 0.001,
//     });
//
//     // Add to the scene root (NOT earthGroup) so the full-screen
//     // quad is not affected by Earth's transform. The fragment
//     // shader does its own world-space math via the uniforms.
//     scene.add(spike.mesh);
//     scene.userData.oceanSpike = spike;
//     scene.userData.oceanSpikeUniforms = {
//       earthCenterUniform,
//       invViewProjUniform,
//     };
//     scene.userData.oceanSpikeEarthGroup = earthGroup;
//   }
//
// In updateSolarSystem(scene, time, camera) per frame:
//
//   const spike = scene.userData.oceanSpike;
//   if (spike && camera) {
//     const uniforms = scene.userData.oceanSpikeUniforms;
//     const earthGroup = scene.userData.oceanSpikeEarthGroup;
//
//     // (1) Earth center in world space
//     earthGroup.getWorldPosition(_tmpVec3);
//     uniforms.earthCenterUniform.value.copy(_tmpVec3);
//
//     // (2) Inverse view-projection: clip → world
//     //     = cameraMatrixWorld * projectionMatrixInverse
//     _tmpMat4
//       .copy(camera.projectionMatrixInverse)
//       .premultiply(camera.matrixWorld);
//     uniforms.invViewProjUniform.value.copy(_tmpMat4);
//   }
//
// On unmount / flag flip:
//
//   const spike = scene.userData.oceanSpike;
//   if (spike) {
//     scene.remove(spike.mesh);
//     spike.dispose();
//     delete scene.userData.oceanSpike;
//     delete scene.userData.oceanSpikeUniforms;
//     delete scene.userData.oceanSpikeEarthGroup;
//   }
//
// ── Known spike limitations (acceptable for a spike) ────────────
//
//   1. Depth writes happen at clip-space z=0 (a constant), NOT at
//      the ray-sphere hit point. This means the ocean sphere acts
//      as a depth occluder at a constant depth. For spike-check-2,
//      we want to see IF any Z-order artifact appears at the limb
//      with the atmosphere rim. If artifacts appear AT the limb
//      (not inside the sphere), that's a real interaction risk
//      Phase 0 proper must address. If artifacts appear
//      everywhere, that's the constant-depth-buffer issue and NOT
//      a real failure of the architecture — in that case,
//      flip depthWrite=false and re-run to isolate check-1 from
//      check-2. Production will compute the real hit depth via
//      TSL's depthNode.
//
//   2. No displacement, no Gerstner waves, no shading. The spike
//      is a flat test color. All the wave synthesis, Fresnel,
//      Gerstner summation, etc. from the v3 spec are Phase 0
//      proper, not the spike.
//
//   3. No explicit discard on miss — the fragment emits a red
//      tint so misses are visually obvious during debugging.
//      Production will discard on miss so PlanetSurfaceTSL shows
//      through untouched.
//
//   4. The TSL mat4 * vec4 multiply syntax uses an `as any` cast
//      because the three/tsl type exports for mat4 uniform
//      multiplication are incomplete at the Three.js r168+ level.
//      If the compiled shader errors on this line, substitute
//      the functional form `mul(uInvViewProj, clipNear)` — it
//      should resolve via the generic `mul` re-export.
//
//   5. Camera-inside-sphere is not handled explicitly. If the
//      camera enters the ocean sphere, the entry root t may be
//      negative and the visible result is unpredictable. Phase 0
//      proper will branch on the camera's distance from Earth's
//      center and handle underwater rendering separately.
//
// ── If the spike fails ──────────────────────────────────────────
//
// Abort Path B. Fall back to v2 Path A. The v2 doc is at:
//   docs/brainstorms/2026-04-11-planet-ocean-zoom-brainstorm-v2-
//   review-response.md
//
// Specifically, re-read section "Path A — patch the mounted-patch
// approach" for the updated requirements.
