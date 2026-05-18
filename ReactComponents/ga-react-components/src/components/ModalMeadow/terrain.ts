/**
 * Modal Meadow — heightmap sampler (v0.6).
 *
 * Rolling hills derived from a tiny inline 2D hash-noise function so we do
 * NOT add `simplex-noise` as a dependency. The same algorithm is mirrored
 * in GLSL inside ModalMeadow.tsx (`HEIGHT_GLSL`) so the camera, grass
 * blades, and any other JS-side queries agree with what the shader paints.
 *
 * Surface character (v0.6):
 *  - amplitude AMP_M ≈ 8m (peak-to-trough ≈ 16m over ~22m horizontal —
 *    actual hills, not gentle rolling; v0.5 used 3m and user reported
 *    "I don't see Hills?")
 *  - dominant wavelength ≈ 45m primary + 18m detail (unchanged from v0.5)
 *  - bandlimited so the camera doesn't jitter as you walk
 *  - flattened toward the boundary band (|x| < BLEND_HALF) to keep the
 *    Ionian/Phrygian transition readable and avoid a giant wall mid-field
 *
 * Cost: ~10 multiplies / sample on the CPU. Camera samples once per frame;
 * blades sample once at construction. No per-frame per-blade JS work.
 */

const HASH_K1 = 127.1;
const HASH_K2 = 311.7;
const HASH_K3 = 269.5;
const HASH_K4 = 183.3;
const HASH_M = 43758.5453123;

const fract = (n: number): number => n - Math.floor(n);

// 2-component hash matching the GLSL `hash2` used by the shader.
const hash2 = (px: number, py: number): [number, number] => {
  const dx = px * HASH_K1 + py * HASH_K2;
  const dy = px * HASH_K3 + py * HASH_K4;
  return [-1 + 2 * fract(Math.sin(dx) * HASH_M), -1 + 2 * fract(Math.sin(dy) * HASH_M)];
};

// 2D value noise in [-1, 1], smoothstep-interpolated. Mirrors GLSL `vnoise`.
const vnoise = (x: number, y: number): number => {
  const ix = Math.floor(x);
  const iy = Math.floor(y);
  const fx = x - ix;
  const fy = y - iy;
  const ux = fx * fx * (3 - 2 * fx);
  const uy = fy * fy * (3 - 2 * fy);
  const [ax, ay] = hash2(ix, iy);
  const [bx, by] = hash2(ix + 1, iy);
  const [cx, cy] = hash2(ix, iy + 1);
  const [dx, dy] = hash2(ix + 1, iy + 1);
  const da = ax * fx + ay * fy;
  const db = bx * (fx - 1) + by * fy;
  const dc = cx * fx + cy * (fy - 1);
  const dd = dx * (fx - 1) + dy * (fy - 1);
  const mx0 = da + (db - da) * ux;
  const mx1 = dc + (dd - dc) * ux;
  return mx0 + (mx1 - mx0) * uy;
};

/** Amplitude of the rolling hills, metres above mean ground. */
export const TERRAIN_AMP_M = 8.0;

/** Dominant wavelength in metres (low frequency ≈ broad rolling shape). */
const FREQ_A = 1 / 45; // ≈ 45m wavelength
const FREQ_B = 1 / 18; // ≈ 18m secondary detail

/**
 * Returns terrain elevation in metres at world XZ.
 *
 * The boundary band (|x| < BLEND_HALF_METERS, 25m) is flattened via a
 * smoothstep so the mode transition reads cleanly. Far from the boundary
 * the surface is full amplitude.
 */
export const sampleTerrainY = (x: number, z: number): number => {
  const big = vnoise(x * FREQ_A, z * FREQ_A);
  const small = vnoise(x * FREQ_B + 7.3, z * FREQ_B - 4.1) * 0.35;
  const raw = big + small;
  // Flatten the boundary band so crossing Ionian↔Phrygian doesn't put a
  // hill in the player's face. boundaryFlat = 1 at ±BLEND_HALF and beyond,
  // 0.25 at x=0 (gentle bowl, never zero so it doesn't look stamped flat).
  const absX = Math.abs(x);
  const flatT = Math.min(1, Math.max(0, (absX - 5) / 25));
  const boundaryFlat = 0.25 + 0.75 * (flatT * flatT * (3 - 2 * flatT));
  return raw * TERRAIN_AMP_M * boundaryFlat;
};

/**
 * GLSL snippet that produces the same elevation as `sampleTerrainY`. Use
 * inside vertex shaders that need to displace a vertex by terrain height.
 *
 * Requires `vnoise(vec2)` to be in scope (defined by NOISE_GLSL in
 * ModalMeadow.tsx).
 *
 * Provides:
 *   float terrainY(vec2 xz)
 */
export const HEIGHT_GLSL = /* glsl */ `
  float terrainY(vec2 xz) {
    float big = vnoise(xz * ${(FREQ_A).toFixed(6)});
    float small = vnoise(xz * ${(FREQ_B).toFixed(6)} + vec2(7.3, -4.1)) * 0.35;
    float raw = big + small;
    float absX = abs(xz.x);
    float flatT = clamp((absX - 5.0) / 25.0, 0.0, 1.0);
    float boundaryFlat = 0.25 + 0.75 * (flatT * flatT * (3.0 - 2.0 * flatT));
    return raw * ${TERRAIN_AMP_M.toFixed(4)} * boundaryFlat;
  }
`;
