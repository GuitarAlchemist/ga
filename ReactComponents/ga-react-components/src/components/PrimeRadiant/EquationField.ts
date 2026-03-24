// src/components/PrimeRadiant/EquationField.ts
// Foundation TV aesthetic — floating mathematical equations drifting through the holographic volume
// These are the iconic symbols from the Prime Radiant in Apple TV's Foundation series

import * as THREE from 'three';
import { CONTAINMENT_SPHERE_RADIUS } from './types';

// ---------------------------------------------------------------------------
// Psychohistory equation fragments — canonical math notation
// Mixed with governance-specific symbols for thematic blending
// ---------------------------------------------------------------------------
const EQUATION_FRAGMENTS: string[] = [
  // Seldon's psychohistory
  'Ψ(t) = ∫₀ᵗ ρ(τ)dτ',
  '∂Ψ/∂t + H·∇Ψ = 0',
  'P(n→∞) = 1 - ε',
  'ΔS ≥ 0',
  'R = Σᵢ wᵢ·rᵢ',
  'dR/dt > 0',
  'ERGOL(t) = ∫ E·dΩ',
  '∇²Φ = -4πGρ',
  'Λ = h/√(2πmkT)',
  'S = -k Σ pᵢ ln pᵢ',

  // Governance symbols
  'T ∧ F → C',
  'U → ∃ investigation',
  'R(t+1) ≥ R(t)',
  'LOLLI → 0',
  'σ(beliefs) < ε',
  'H(Ω) = -Σ p log p',

  // Greek / symbolic fragments
  'α β γ δ ε ζ',
  'Σ Π Ω Δ Φ Ψ',
  '∂/∂t',
  '∮ F·dl = 0',
  '∇ × E = -∂B/∂t',
  'det(A - λI) = 0',
  'lim_{n→∞}',
  'ℏ = h/2π',
  '∫∫ dA = 4πr²',
  'e^{iπ} + 1 = 0',
];

// ---------------------------------------------------------------------------
// Single floating equation sprite
// ---------------------------------------------------------------------------
interface EquationSprite {
  sprite: THREE.Sprite;
  velocity: THREE.Vector3;
  angularVelocity: number;
  lifetime: number;
  maxLifetime: number;
  fadeIn: number;   // seconds to fade in
  fadeOut: number;  // seconds before death to start fading
}

// ---------------------------------------------------------------------------
// Create a single equation sprite from canvas text
// ---------------------------------------------------------------------------
function createEquationCanvas(text: string): HTMLCanvasElement {
  const canvas = document.createElement('canvas');
  const ctx = canvas.getContext('2d')!;

  canvas.width = 512;
  canvas.height = 64;

  ctx.clearRect(0, 0, canvas.width, canvas.height);

  // Golden text with subtle glow — Foundation's amber palette
  ctx.font = '32px "JetBrains Mono", "Fira Code", "Courier New", monospace';
  ctx.fillStyle = '#FFD700';
  ctx.shadowColor = '#FFD700';
  ctx.shadowBlur = 6;
  ctx.globalAlpha = 0.9;
  ctx.textAlign = 'center';
  ctx.textBaseline = 'middle';
  ctx.fillText(text, canvas.width / 2, canvas.height / 2);

  return canvas;
}

function createSingleEquation(text: string, position: THREE.Vector3): EquationSprite {
  const canvas = createEquationCanvas(text);
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

  // Scale — equations should be readable but not dominant
  const scale = 3.0 + Math.random() * 2.0;
  sprite.scale.set(scale, scale * 0.125, 1);

  // Slow drift velocity
  const speed = 0.2 + Math.random() * 0.3;
  const theta = Math.random() * Math.PI * 2;
  const phi = Math.acos(2 * Math.random() - 1);
  const velocity = new THREE.Vector3(
    Math.sin(phi) * Math.cos(theta) * speed,
    Math.sin(phi) * Math.sin(theta) * speed,
    Math.cos(phi) * speed,
  );

  const maxLifetime = 15 + Math.random() * 20;

  return {
    sprite,
    velocity,
    angularVelocity: (Math.random() - 0.5) * 0.01,
    lifetime: 0,
    maxLifetime,
    fadeIn: 2.0,
    fadeOut: 3.0,
  };
}

// ---------------------------------------------------------------------------
// Random position inside the containment sphere (biased toward mid-radius)
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
// EquationField — manages a pool of floating equation sprites
// ---------------------------------------------------------------------------
export class EquationField {
  private equations: EquationSprite[] = [];
  private scene: THREE.Scene;
  private maxEquations: number;
  private spawnTimer = 0;
  private spawnInterval: number;

  constructor(scene: THREE.Scene, maxEquations: number = 40) {
    this.scene = scene;
    this.maxEquations = maxEquations;
    this.spawnInterval = 0.8; // seconds between spawns

    // Seed initial batch (staggered lifetimes so they don't all fade in at once)
    const initialCount = Math.floor(maxEquations * 0.6);
    for (let i = 0; i < initialCount; i++) {
      const text = EQUATION_FRAGMENTS[Math.floor(Math.random() * EQUATION_FRAGMENTS.length)];
      const pos = randomSpherePosition();
      const eq = createSingleEquation(text, pos);
      eq.lifetime = Math.random() * eq.maxLifetime * 0.7; // stagger
      this.equations.push(eq);
      this.scene.add(eq.sprite);
    }
  }

  // ─── Update every frame ───
  update(dt: number): void {
    const maxR = CONTAINMENT_SPHERE_RADIUS * 0.9;

    // Update existing equations
    for (let i = this.equations.length - 1; i >= 0; i--) {
      const eq = this.equations[i];
      eq.lifetime += dt;

      // Remove expired
      if (eq.lifetime >= eq.maxLifetime) {
        this.scene.remove(eq.sprite);
        (eq.sprite.material as THREE.SpriteMaterial).map?.dispose();
        eq.sprite.material.dispose();
        this.equations.splice(i, 1);
        continue;
      }

      // Move
      eq.sprite.position.addScaledVector(eq.velocity, dt);

      // Slight rotation for organic feel
      eq.sprite.material.rotation += eq.angularVelocity;

      // Bounce off containment sphere
      const dist = eq.sprite.position.length();
      if (dist > maxR) {
        // Reflect velocity inward
        const normal = eq.sprite.position.clone().normalize();
        eq.velocity.reflect(normal);
        eq.sprite.position.normalize().multiplyScalar(maxR * 0.98);
      }

      // Fade in / fade out
      const mat = eq.sprite.material as THREE.SpriteMaterial;
      if (eq.lifetime < eq.fadeIn) {
        mat.opacity = (eq.lifetime / eq.fadeIn) * 0.35;
      } else if (eq.lifetime > eq.maxLifetime - eq.fadeOut) {
        const remaining = eq.maxLifetime - eq.lifetime;
        mat.opacity = (remaining / eq.fadeOut) * 0.35;
      } else {
        mat.opacity = 0.35;
      }
    }

    // Spawn new equations
    this.spawnTimer += dt;
    if (this.spawnTimer >= this.spawnInterval && this.equations.length < this.maxEquations) {
      this.spawnTimer = 0;
      const text = EQUATION_FRAGMENTS[Math.floor(Math.random() * EQUATION_FRAGMENTS.length)];
      const pos = randomSpherePosition();
      const eq = createSingleEquation(text, pos);
      this.equations.push(eq);
      this.scene.add(eq.sprite);
    }
  }

  // ─── Cleanup ───
  dispose(): void {
    for (const eq of this.equations) {
      this.scene.remove(eq.sprite);
      (eq.sprite.material as THREE.SpriteMaterial).map?.dispose();
      eq.sprite.material.dispose();
    }
    this.equations = [];
  }
}
