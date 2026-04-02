// src/components/PrimeRadiant/shaders/TSLNoiseLib.ts
// Shared TSL noise primitives for all Prime Radiant procedural materials.
// Extracted from SunMaterialTSL.ts — single source of truth for noise functions.
// All functions are composable Fn() nodes, no material dependency.

import {
  Fn, float, vec2, vec3,
  sin, cos, fract, floor, dot, abs, sqrt, min, max,
  mix, smoothstep,
} from 'three/tsl';

// Type alias for TSL node return types (vec3-like)
type Vec3Node = ReturnType<typeof vec3>;
type FloatNode = ReturnType<typeof float>;

// ─── Hash ───

/** Hash function: vec3 -> float. Fast pseudo-random. */
export const hash3 = Fn(([p]: [Vec3Node]) => {
  return fract(sin(dot(p, vec3(1.3, 1.7, 1.9))).mul(43758.5));
});

/** Hash vec2 -> float */
export const hash2 = Fn(([p]: [ReturnType<typeof vec2>]) => {
  return fract(sin(dot(p, vec2(127.1, 311.7))).mul(43758.5453));
});

// ─── Value Noise ───

/** 3D value noise with trilinear interpolation */
export const noise3 = Fn(([x_immutable]: [Vec3Node]) => {
  const x = vec3(x_immutable);
  const i = floor(x);
  const f = fract(x);
  const u = f.mul(f).mul(float(3.0).sub(f.mul(2.0))); // smoothstep interpolation

  const a = mix(
    mix(hash3(i), hash3(i.add(vec3(1, 0, 0))), u.x),
    mix(hash3(i.add(vec3(0, 1, 0))), hash3(i.add(vec3(1, 1, 0))), u.x),
    u.y,
  );
  const b = mix(
    mix(hash3(i.add(vec3(0, 0, 1))), hash3(i.add(vec3(1, 0, 1))), u.x),
    mix(hash3(i.add(vec3(0, 1, 1))), hash3(i.add(vec3(1, 1, 1))), u.x),
    u.y,
  );
  return mix(a, b, u.z);
});

// ─── Fractal Brownian Motion ───

/** FBM — 6 octaves (high quality) */
export const fbm6 = Fn(([p_immutable]: [Vec3Node]) => {
  const p = vec3(p_immutable).toVar();
  const v = float(0).toVar();
  const a = float(0.5).toVar();
  v.addAssign(a.mul(noise3(p))); p.mulAssign(2.1); a.mulAssign(0.48);
  v.addAssign(a.mul(noise3(p))); p.mulAssign(2.1); a.mulAssign(0.48);
  v.addAssign(a.mul(noise3(p))); p.mulAssign(2.1); a.mulAssign(0.48);
  v.addAssign(a.mul(noise3(p))); p.mulAssign(2.1); a.mulAssign(0.48);
  v.addAssign(a.mul(noise3(p))); p.mulAssign(2.1); a.mulAssign(0.48);
  v.addAssign(a.mul(noise3(p)));
  return v;
});

/** FBM — 3 octaves (low quality, ~2x faster) */
export const fbm3 = Fn(([p_immutable]: [Vec3Node]) => {
  const p = vec3(p_immutable).toVar();
  const v = float(0).toVar();
  const a = float(0.5).toVar();
  v.addAssign(a.mul(noise3(p))); p.mulAssign(2.1); a.mulAssign(0.48);
  v.addAssign(a.mul(noise3(p))); p.mulAssign(2.1); a.mulAssign(0.48);
  v.addAssign(a.mul(noise3(p)));
  return v;
});

// ─── Voronoi ───

/**
 * 2D Voronoi cell distance — returns { cellDist, edgeDist, cellId }.
 * For 3D, project world position onto a 2D parameterization first.
 *
 * @param p - 2D position to evaluate
 * @returns edgeDist as float (distance to nearest cell boundary)
 *
 * Based on Inigo Quilez's Voronoi edge distance technique.
 */
export const voronoiEdgeDist = Fn(([p_immutable]: [ReturnType<typeof vec2>]) => {
  const p = vec2(p_immutable);
  const ip = floor(p);
  const fp = fract(p);

  // First pass: find nearest cell center
  const nearestDist = float(8.0).toVar();
  const nearestCell = vec2(0, 0).toVar();

  // 3x3 neighborhood search (unrolled)
  const checkCell = (ox: number, oy: number) => {
    const offset = vec2(ox, oy);
    const cellPos = offset.add(hash2(ip.add(offset))); // jittered grid point
    const diff = cellPos.sub(fp);
    const d = dot(diff, diff);
    // If closer, update nearest
    const closer = d.lessThan(nearestDist);
    nearestDist.assign(min(nearestDist, d));
    nearestCell.assign(mix(nearestCell, diff, float(closer)));
  };

  checkCell(-1, -1); checkCell(0, -1); checkCell(1, -1);
  checkCell(-1, 0);  checkCell(0, 0);  checkCell(1, 0);
  checkCell(-1, 1);  checkCell(0, 1);  checkCell(1, 1);

  // Second pass: find distance to nearest cell edge
  const edgeDist = float(8.0).toVar();

  const checkEdge = (ox: number, oy: number) => {
    const offset = vec2(ox, oy);
    const cellPos = offset.add(hash2(ip.add(offset)));
    const diff = cellPos.sub(fp);
    // Edge distance: project midpoint onto line between nearest and this cell
    const mid = nearestCell.add(diff).mul(0.5);
    const edgeDir = diff.sub(nearestCell).normalize();
    const d = abs(dot(mid, edgeDir));
    edgeDist.assign(min(edgeDist, d));
  };

  checkEdge(-1, -1); checkEdge(0, -1); checkEdge(1, -1);
  checkEdge(-1, 0);  checkEdge(0, 0);  checkEdge(1, 0);
  checkEdge(-1, 1);  checkEdge(0, 1);  checkEdge(1, 1);

  return edgeDist;
});

// ─── Reaction-Diffusion (Gray-Scott step) ───

/**
 * Single Gray-Scott reaction-diffusion step.
 * U = inhibitor (substrate), V = activator (product).
 * f = feed rate, k = kill rate.
 * Returns updated { u, v } as vec2.
 */
export const grayScottStep = Fn(([
  u_immutable,
  v_immutable,
  laplacianU_immutable,
  laplacianV_immutable,
  f_immutable,
  k_immutable,
  dt_immutable,
]: [FloatNode, FloatNode, FloatNode, FloatNode, FloatNode, FloatNode, FloatNode]) => {
  const u = float(u_immutable);
  const v = float(v_immutable);
  const lapU = float(laplacianU_immutable);
  const lapV = float(laplacianV_immutable);
  const f = float(f_immutable);
  const k = float(k_immutable);
  const dt = float(dt_immutable);

  const Du = float(0.2097); // diffusion rate U
  const Dv = float(0.105);  // diffusion rate V
  const uvv = u.mul(v).mul(v); // reaction term u*v^2

  const newU = u.add(dt.mul(Du.mul(lapU).sub(uvv).add(f.mul(float(1.0).sub(u)))));
  const newV = v.add(dt.mul(Dv.mul(lapV).add(uvv).sub(k.add(f).mul(v))));

  return vec2(newU, newV);
});
