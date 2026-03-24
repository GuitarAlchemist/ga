// src/components/PrimeRadiant/EquationField.ts
// Foundation TV aesthetic — floating equations derived from real governance math
// Each formula maps to an actual computation in the Demerzel framework

import * as THREE from 'three';
import { CONTAINMENT_SPHERE_RADIUS } from './types';
import type { GovernanceGraph } from './types';

// ---------------------------------------------------------------------------
// Real governance equations — each one is a formula actually used in Demerzel
// ---------------------------------------------------------------------------
function buildEquations(graph: GovernanceGraph): string[] {
  const R = graph.globalHealth.resilienceScore;
  const E = graph.globalHealth.ergolCount;
  const L = graph.globalHealth.lolliCount;
  const N = graph.nodes.length;
  const edges = graph.edges.length;

  // Count by type
  const policies = graph.nodes.filter(n => n.type === 'policy').length;
  const personas = graph.nodes.filter(n => n.type === 'persona').length;
  const tests = graph.nodes.filter(n => n.type === 'test').length;
  const schemas = graph.nodes.filter(n => n.type === 'schema').length;

  // Staleness — fraction of nodes with staleness > 0
  const staleNodes = graph.nodes.filter(n => n.health?.staleness && n.health.staleness > 0.3).length;
  const staleRatio = staleNodes / Math.max(N, 1);

  return [
    // Resilience score — weighted sum of component health
    `R = ${R.toFixed(2)}`,
    `R = (E - L) / N = (${E} - ${L}) / ${N}`,

    // ERGOL — executed governance bindings
    `ERGOL = ${E}`,
    `ERGOL / N = ${(E / Math.max(N, 1)).toFixed(2)}`,

    // LOLLI — dead references (lower is better)
    `LOLLI = ${L}`,
    ...(L > 0 ? [`L / E = ${(L / Math.max(E, 1)).toFixed(3)}`] : []),

    // Graph density — edges per node
    `density = |E| / |V| = ${edges} / ${N} = ${(edges / Math.max(N, 1)).toFixed(2)}`,

    // Test coverage ratio
    `coverage = tests / policies = ${tests} / ${policies} = ${(tests / Math.max(policies, 1)).toFixed(2)}`,

    // Persona-to-policy binding ratio
    `binding = personas / policies = ${personas} / ${policies}`,

    // Schema validation coverage
    `validation = schemas / N = ${schemas} / ${N} = ${(schemas / Math.max(N, 1)).toFixed(2)}`,

    // Staleness decay
    `staleness = ${staleRatio.toFixed(2)}`,
    ...(staleRatio > 0 ? [`stale nodes = ${staleNodes} / ${N}`] : []),

    // Tetravalent logic states
    `states = {T, F, U, C}`,
    `T + F + U + C = 1`,

    // Governance monotonicity constraint
    `R(t+1) >= R(t)`,
    `dR/dt > 0`,

    // Information entropy of node type distribution
    `H = -sum(p * log p)`,

    // Confidence thresholds from constitution
    `conf >= 0.9: autonomous`,
    `conf >= 0.7: proceed + note`,
    `conf >= 0.5: confirm`,
    `conf < 0.3: halt`,
  ];
}

// ---------------------------------------------------------------------------
// Single floating equation sprite
// ---------------------------------------------------------------------------
interface EquationSprite {
  sprite: THREE.Sprite;
  velocity: THREE.Vector3;
  angularVelocity: number;
  lifetime: number;
  maxLifetime: number;
  fadeIn: number;
  fadeOut: number;
}

// ---------------------------------------------------------------------------
// Create a canvas that auto-sizes to fit the text — no truncation
// ---------------------------------------------------------------------------
function createEquationCanvas(text: string): { canvas: HTMLCanvasElement; aspect: number } {
  const canvas = document.createElement('canvas');
  const ctx = canvas.getContext('2d')!;

  const fontSize = 36;
  const font = `${fontSize}px "JetBrains Mono", "Fira Code", "Courier New", monospace`;
  const padding = 24;

  // Measure text width first
  ctx.font = font;
  const measured = ctx.measureText(text);
  const textWidth = Math.ceil(measured.width);

  // Size canvas to fit — power of 2 not required for CanvasTexture
  canvas.width = textWidth + padding * 2;
  canvas.height = fontSize + padding;

  // Re-set font after resize (canvas resize clears state)
  ctx.clearRect(0, 0, canvas.width, canvas.height);
  ctx.font = font;
  ctx.fillStyle = '#FFD700';
  ctx.shadowColor = '#FFD700';
  ctx.shadowBlur = 4;
  ctx.globalAlpha = 0.95;
  ctx.textAlign = 'center';
  ctx.textBaseline = 'middle';
  ctx.fillText(text, canvas.width / 2, canvas.height / 2);

  return { canvas, aspect: canvas.width / canvas.height };
}

function createSingleEquation(text: string, position: THREE.Vector3): EquationSprite {
  const { canvas, aspect } = createEquationCanvas(text);
  const texture = new THREE.CanvasTexture(canvas);
  texture.minFilter = THREE.LinearFilter;

  const material = new THREE.SpriteMaterial({
    map: texture,
    transparent: true,
    opacity: 0,
    blending: THREE.AdditiveBlending,
    depthWrite: false,
    depthTest: true,
  });

  const sprite = new THREE.Sprite(material);
  sprite.position.copy(position);

  // Scale based on actual text aspect ratio
  const height = 0.8 + Math.random() * 0.4;
  sprite.scale.set(height * aspect, height, 1);

  // Slow drift
  const speed = 0.15 + Math.random() * 0.25;
  const theta = Math.random() * Math.PI * 2;
  const phi = Math.acos(2 * Math.random() - 1);
  const velocity = new THREE.Vector3(
    Math.sin(phi) * Math.cos(theta) * speed,
    Math.sin(phi) * Math.sin(theta) * speed,
    Math.cos(phi) * speed,
  );

  return {
    sprite,
    velocity,
    angularVelocity: (Math.random() - 0.5) * 0.008,
    lifetime: 0,
    maxLifetime: 18 + Math.random() * 22,
    fadeIn: 2.5,
    fadeOut: 3.5,
  };
}

// ---------------------------------------------------------------------------
// Random position inside the containment sphere
// ---------------------------------------------------------------------------
function randomSpherePosition(): THREE.Vector3 {
  const r = CONTAINMENT_SPHERE_RADIUS * 0.85 * Math.pow(Math.random(), 0.4);
  const theta = Math.random() * Math.PI * 2;
  const phi = Math.acos(2 * Math.random() - 1);

  return new THREE.Vector3(
    r * Math.sin(phi) * Math.cos(theta),
    r * Math.sin(phi) * Math.sin(theta),
    r * Math.cos(phi),
  );
}

// ---------------------------------------------------------------------------
// EquationField — pool of floating equation sprites from live data
// ---------------------------------------------------------------------------
export class EquationField {
  private equations: EquationSprite[] = [];
  private scene: THREE.Scene;
  private maxEquations: number;
  private spawnTimer = 0;
  private spawnInterval: number;
  private fragments: string[];

  constructor(scene: THREE.Scene, graph: GovernanceGraph, maxEquations: number = 12) {
    this.scene = scene;
    this.maxEquations = maxEquations;
    this.spawnInterval = 3.0;
    this.fragments = buildEquations(graph);

    // Seed initial batch with staggered lifetimes
    const initialCount = Math.floor(maxEquations * 0.4);
    for (let i = 0; i < initialCount; i++) {
      const text = this.fragments[Math.floor(Math.random() * this.fragments.length)];
      const pos = randomSpherePosition();
      const eq = createSingleEquation(text, pos);
      eq.lifetime = Math.random() * eq.maxLifetime * 0.6;
      this.equations.push(eq);
      this.scene.add(eq.sprite);
    }
  }

  update(dt: number): void {
    const maxR = CONTAINMENT_SPHERE_RADIUS * 0.9;

    for (let i = this.equations.length - 1; i >= 0; i--) {
      const eq = this.equations[i];
      eq.lifetime += dt;

      if (eq.lifetime >= eq.maxLifetime) {
        this.scene.remove(eq.sprite);
        (eq.sprite.material as THREE.SpriteMaterial).map?.dispose();
        eq.sprite.material.dispose();
        this.equations.splice(i, 1);
        continue;
      }

      eq.sprite.position.addScaledVector(eq.velocity, dt);
      eq.sprite.material.rotation += eq.angularVelocity;

      const dist = eq.sprite.position.length();
      if (dist > maxR) {
        const normal = eq.sprite.position.clone().normalize();
        eq.velocity.reflect(normal);
        eq.sprite.position.normalize().multiplyScalar(maxR * 0.98);
      }

      const mat = eq.sprite.material as THREE.SpriteMaterial;
      if (eq.lifetime < eq.fadeIn) {
        mat.opacity = (eq.lifetime / eq.fadeIn) * 0.25;
      } else if (eq.lifetime > eq.maxLifetime - eq.fadeOut) {
        const remaining = eq.maxLifetime - eq.lifetime;
        mat.opacity = (remaining / eq.fadeOut) * 0.25;
      } else {
        mat.opacity = 0.25;
      }
    }

    this.spawnTimer += dt;
    if (this.spawnTimer >= this.spawnInterval && this.equations.length < this.maxEquations) {
      this.spawnTimer = 0;
      const text = this.fragments[Math.floor(Math.random() * this.fragments.length)];
      const pos = randomSpherePosition();
      const eq = createSingleEquation(text, pos);
      this.equations.push(eq);
      this.scene.add(eq.sprite);
    }
  }

  dispose(): void {
    for (const eq of this.equations) {
      this.scene.remove(eq.sprite);
      (eq.sprite.material as THREE.SpriteMaterial).map?.dispose();
      eq.sprite.material.dispose();
    }
    this.equations = [];
  }
}
