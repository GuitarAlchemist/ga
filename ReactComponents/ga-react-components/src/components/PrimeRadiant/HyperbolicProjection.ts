// src/components/PrimeRadiant/HyperbolicProjection.ts
// Poincaré ball model — maps governance hierarchy into hyperbolic space
// Center = constitutions (most important), boundary = tests/schemas (peripheral)
// Clicking a node applies Möbius transformation to bring it to focus

import * as THREE from 'three';
import { CONTAINMENT_SPHERE_RADIUS } from './types';

// The ball radius in hyperbolic space (nodes must stay inside this)
const BALL_RADIUS = CONTAINMENT_SPHERE_RADIUS * 0.92;

// Curvature parameter — higher = more compression at edges
const CURVATURE = 1.8;

// ---------------------------------------------------------------------------
// Poincaré ball mapping — maps Euclidean radius to hyperbolic radius
// tanh maps [0, ∞) → [0, 1), so nodes never reach the boundary
// ---------------------------------------------------------------------------
export function euclideanToHyperbolic(euclideanPos: THREE.Vector3, ballRadius: number = BALL_RADIUS): THREE.Vector3 {
  const r = euclideanPos.length();
  if (r < 0.001) return euclideanPos.clone();

  // Normalize to unit, apply tanh compression, scale to ball
  const normalized = euclideanPos.clone().normalize();
  const hyperbolicR = Math.tanh(r / ballRadius * CURVATURE) * ballRadius;

  return normalized.multiplyScalar(hyperbolicR);
}

// ---------------------------------------------------------------------------
// Inverse — hyperbolic back to Euclidean (for hit-testing)
// ---------------------------------------------------------------------------
export function hyperbolicToEuclidean(hyperbolicPos: THREE.Vector3, ballRadius: number = BALL_RADIUS): THREE.Vector3 {
  const r = hyperbolicPos.length();
  if (r < 0.001) return hyperbolicPos.clone();

  const normalized = hyperbolicPos.clone().normalize();
  const euclideanR = Math.atanh(Math.min(r / ballRadius, 0.999)) * ballRadius / CURVATURE;

  return normalized.multiplyScalar(euclideanR);
}

// ---------------------------------------------------------------------------
// Möbius transformation in the Poincaré ball
// Translates the ball so that point `a` maps to the origin
// This is the key interaction: click a node → it comes to center
//
// Formula: M_a(x) = ((1 - |a|²)(x - a) - (|x - a|² * a)) / (|1 - a·x̄|²)
// Simplified for real vectors in 3D Poincaré ball:
//   M_a(x) = ((1-|a|²)(x-a) + |x-a|²·a) / (1 - 2<a,x> + |a|²|x|²)
// ---------------------------------------------------------------------------
export function moebiusTranslation(
  point: THREE.Vector3,
  focusPoint: THREE.Vector3,
  ballRadius: number = BALL_RADIUS,
): THREE.Vector3 {
  // Normalize to unit ball
  const x = point.clone().divideScalar(ballRadius);
  const a = focusPoint.clone().divideScalar(ballRadius);

  const aSq = a.lengthSq();
  const xSq = x.lengthSq();
  const ax = a.dot(x);

  // Denominator: 1 - 2<a,x> + |a|²|x|²
  const denom = 1 - 2 * ax + aSq * xSq;
  if (Math.abs(denom) < 0.0001) return point.clone();

  // Numerator: (1 + 2<a, x-a>/denom_part + |x|²) * ...
  // Using the standard formula for Möbius addition in the ball:
  // a ⊕ x = ((1 + 2<a,x> + |x|²)a + (1 - |a|²)x) / (1 + 2<a,x> + |a|²|x|²)
  // To translate so `a` goes to origin, we use ⊖a ⊕ x:
  const negA = a.clone().negate();
  const negASq = negA.lengthSq(); // same as aSq
  const negAx = negA.dot(x);

  const denomFinal = 1 + 2 * negAx + negASq * xSq;
  if (Math.abs(denomFinal) < 0.0001) return point.clone();

  const result = new THREE.Vector3();
  // ((1 + 2<-a,x> + |x|²)(-a) + (1 - |-a|²)x) / denomFinal
  const coefA = 1 + 2 * negAx + xSq;
  const coefX = 1 - negASq;

  result.copy(negA).multiplyScalar(coefA);
  result.addScaledVector(x, coefX);
  result.divideScalar(denomFinal);

  // Scale back to ball radius
  return result.multiplyScalar(ballRadius);
}

// ---------------------------------------------------------------------------
// Interpolate Möbius transformation for smooth animation
// Returns positions for all nodes given a focus point and blend factor (0–1)
// blend=0 means no transformation, blend=1 means fully focused
// ---------------------------------------------------------------------------
export function interpolateMoebius(
  originalPositions: Map<string, THREE.Vector3>,
  focusPoint: THREE.Vector3 | null,
  blend: number,
  ballRadius: number = BALL_RADIUS,
): Map<string, THREE.Vector3> {
  const result = new Map<string, THREE.Vector3>();

  if (!focusPoint || blend < 0.001) {
    // No transformation — return originals
    for (const [id, pos] of originalPositions) {
      result.set(id, pos.clone());
    }
    return result;
  }

  for (const [id, pos] of originalPositions) {
    const transformed = moebiusTranslation(pos, focusPoint, ballRadius);
    const blended = new THREE.Vector3().lerpVectors(pos, transformed, blend);
    result.set(id, blended);
  }

  return result;
}

// ---------------------------------------------------------------------------
// Hyperbolic distance between two points in the Poincaré ball
// d(x,y) = arcosh(1 + 2|x-y|² / ((1-|x|²)(1-|y|²)))
// ---------------------------------------------------------------------------
export function hyperbolicDistance(
  a: THREE.Vector3,
  b: THREE.Vector3,
  ballRadius: number = BALL_RADIUS,
): number {
  const an = a.clone().divideScalar(ballRadius);
  const bn = b.clone().divideScalar(ballRadius);

  const diffSq = an.clone().sub(bn).lengthSq();
  const aSq = an.lengthSq();
  const bSq = bn.lengthSq();

  const denom = (1 - aSq) * (1 - bSq);
  if (denom <= 0) return Infinity;

  const arg = 1 + 2 * diffSq / denom;
  return Math.acosh(Math.max(arg, 1));
}
