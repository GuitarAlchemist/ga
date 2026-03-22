import * as THREE from 'three';

// --- Poincaré Disk (2D) ---

/** Map hierarchy depth to Poincaré disk radius */
export function depthToRadius(depth: number, kappa: number = 0.6): number {
  return Math.tanh(depth * kappa);
}

/** Conformal scale factor at position p (distance from origin) */
export function conformalFactor(r: number): number {
  return 2 / (1 - r * r);
}

/** Visual size of a tile at Poincaré radius r */
export function tileScale(baseSize: number, r: number): number {
  return baseSize * (1 - r * r) / 2;
}

/** 2D Möbius transform: re-center disk on point a (complex number) */
export function mobiusTransform2D(
  z: [number, number],
  a: [number, number]
): [number, number] {
  // T_a(z) = (z - a) / (1 - conj(a) * z)
  const [zr, zi] = z;
  const [ar, ai] = a;
  const nr = zr - ar;
  const ni = zi - ai;
  const dr = 1 - (ar * zr + ai * zi);
  const di = -(ar * zi - ai * zr);
  const denom = dr * dr + di * di;
  return [(nr * dr + ni * di) / denom, (ni * dr - nr * di) / denom];
}

// --- Poincaré Ball (3D) ---

/** Map hierarchy depth to 3D ball radius */
export function depthToRadius3D(depth: number, kappa: number = 0.5): number {
  return Math.tanh(depth * kappa);
}

/** Distribute N children on a sphere at given radius using Fibonacci spiral */
export function fibonacciSphere(n: number, radius: number): THREE.Vector3[] {
  const points: THREE.Vector3[] = [];
  const goldenAngle = Math.PI * (3 - Math.sqrt(5));
  for (let i = 0; i < n; i++) {
    const y = 1 - (2 * i) / (n - 1 || 1);
    const r = Math.sqrt(1 - y * y);
    const theta = goldenAngle * i;
    points.push(new THREE.Vector3(
      r * Math.cos(theta) * radius,
      y * radius,
      r * Math.sin(theta) * radius
    ));
  }
  return points;
}

/**
 * 3D gyration isometry (re-center Poincaré ball on point a).
 * Ref: Ungar, "Analytic Hyperbolic Geometry"
 *
 * T_a(x) = ((1 + 2<a,x> + |x|²) · a - (1 - |a|²) · x) / (1 + 2<a,x> + |a|²|x|²)
 */
export function gyrationTransform3D(
  x: THREE.Vector3,
  a: THREE.Vector3,
  target?: THREE.Vector3
): THREE.Vector3 {
  const out = target ?? new THREE.Vector3();
  const ax = a.dot(x);
  const x2 = x.lengthSq();
  const a2 = a.lengthSq();
  const denom = 1 + 2 * ax + a2 * x2;
  out.copy(a).multiplyScalar(1 + 2 * ax + x2)
    .addScaledVector(x, -(1 - a2));
  out.divideScalar(denom);
  return out;
}

// --- Layout helpers ---

/** Compute 2D positions for children around a parent at given radius */
export function layoutChildren2D(
  count: number,
  parentR: number,
  childDepth: number,
  kappa: number = 0.6
): Array<{ x: number; y: number; r: number }> {
  const r = depthToRadius(childDepth, kappa);
  return Array.from({ length: count }, (_, i) => {
    const theta = (i / count) * 2 * Math.PI - Math.PI / 2;
    return { x: r * Math.cos(theta), y: r * Math.sin(theta), r };
  });
}
