// src/components/PrimeRadiant/FlowFieldEngine.ts
// CPU-driven 3D flow field for governance compliance visualization.
//
// The flow field is a 3D grid of direction vectors encoding governance
// directive flow. Particles sample this field for velocity, creating
// visible "compliance rivers" through the governance graph.
//
// Governance meaning:
//   - Laminar flow: clean execution, aligned governance
//   - Vortices: bureaucratic churn, feedback loops
//   - Dead zones: blocked governance pathways
//   - Bifurcation: policy ambiguity

import * as THREE from 'three';
import type { GovernanceNode, GovernanceEdge } from './types';

// ── Types ──

interface PositionedNode extends GovernanceNode {
  x?: number;
  y?: number;
  z?: number;
}

export interface FlowParticle {
  position: THREE.Vector3;
  velocity: THREE.Vector3;
  health: number;    // 0-1 compliance health
  phase: number;     // 0-1 lifecycle (0=born, 1=dead)
  speed: number;     // current speed magnitude
  sourceEdgeIdx: number; // which edge spawned this particle
}

export interface FlowFieldConfig {
  /** Grid resolution per axis (default 16) */
  resolution: number;
  /** Max particles (default 2000) */
  maxParticles: number;
  /** Particle speed multiplier */
  speedScale: number;
  /** Curl noise strength (adds organic swirl) */
  curlStrength: number;
}

const DEFAULT_CONFIG: FlowFieldConfig = {
  resolution: 16,
  maxParticles: 2000,
  speedScale: 0.8,
  curlStrength: 0.3,
};

// ── Flow Field Grid ──

class FlowField3D {
  readonly res: number;
  readonly data: Float32Array; // res^3 * 3 (vec3 per cell)
  private bounds = { min: new THREE.Vector3(), max: new THREE.Vector3(), size: new THREE.Vector3() };

  constructor(resolution: number) {
    this.res = resolution;
    this.data = new Float32Array(resolution * resolution * resolution * 3);
  }

  /** Rebuild field from governance graph topology */
  build(nodes: PositionedNode[], edges: GovernanceEdge[]): void {
    // Compute bounds from node positions
    const min = this.bounds.min.set(Infinity, Infinity, Infinity);
    const max = this.bounds.max.set(-Infinity, -Infinity, -Infinity);
    const _pos = new THREE.Vector3();

    for (const n of nodes) {
      if (n.x === undefined) continue;
      _pos.set(n.x, n.y ?? 0, n.z ?? 0);
      min.min(_pos);
      max.max(_pos);
    }

    // Pad bounds
    const pad = 10;
    min.subScalar(pad);
    max.addScalar(pad);
    this.bounds.size.subVectors(max, min);

    // Build node position map
    const nodePos = new Map<string, THREE.Vector3>();
    for (const n of nodes) {
      if (n.x === undefined) continue;
      nodePos.set(n.id, new THREE.Vector3(n.x, n.y ?? 0, n.z ?? 0));
    }

    // Clear field
    this.data.fill(0);

    // For each edge, add directional influence to nearby grid cells
    const _src = new THREE.Vector3();
    const _tgt = new THREE.Vector3();
    const _dir = new THREE.Vector3();
    const _cell = new THREE.Vector3();

    for (const edge of edges) {
      const srcPos = nodePos.get(edge.source);
      const tgtPos = nodePos.get(edge.target);
      if (!srcPos || !tgtPos) continue;

      _src.copy(srcPos);
      _tgt.copy(tgtPos);
      _dir.subVectors(_tgt, _src).normalize();

      // Influence radius in grid cells
      const influenceRadius = 3;

      // Walk along edge, adding directional influence
      const steps = 8;
      for (let s = 0; s <= steps; s++) {
        const t = s / steps;
        _cell.lerpVectors(_src, _tgt, t);

        // Convert to grid coords
        const gx = (((_cell.x - min.x) / this.bounds.size.x) * this.res) | 0;
        const gy = (((_cell.y - min.y) / this.bounds.size.y) * this.res) | 0;
        const gz = (((_cell.z - min.z) / this.bounds.size.z) * this.res) | 0;

        // Add influence to neighboring cells
        for (let dx = -influenceRadius; dx <= influenceRadius; dx++) {
          for (let dy = -influenceRadius; dy <= influenceRadius; dy++) {
            for (let dz = -influenceRadius; dz <= influenceRadius; dz++) {
              const cx = gx + dx, cy = gy + dy, cz = gz + dz;
              if (cx < 0 || cx >= this.res || cy < 0 || cy >= this.res || cz < 0 || cz >= this.res) continue;

              const dist = Math.sqrt(dx * dx + dy * dy + dz * dz);
              if (dist > influenceRadius) continue;

              const weight = 1.0 - dist / influenceRadius; // linear falloff
              const idx = (cz * this.res * this.res + cy * this.res + cx) * 3;
              this.data[idx] += _dir.x * weight;
              this.data[idx + 1] += _dir.y * weight;
              this.data[idx + 2] += _dir.z * weight;
            }
          }
        }
      }
    }

    // Normalize field vectors
    for (let i = 0; i < this.data.length; i += 3) {
      const len = Math.sqrt(this.data[i] ** 2 + this.data[i + 1] ** 2 + this.data[i + 2] ** 2);
      if (len > 0.001) {
        this.data[i] /= len;
        this.data[i + 1] /= len;
        this.data[i + 2] /= len;
      }
    }
  }

  /** Sample flow field at a world position (trilinear interpolation) */
  sample(worldPos: THREE.Vector3, out: THREE.Vector3): THREE.Vector3 {
    const { min, size } = this.bounds;
    // Normalized coords [0, res)
    const nx = ((worldPos.x - min.x) / size.x) * this.res;
    const ny = ((worldPos.y - min.y) / size.y) * this.res;
    const nz = ((worldPos.z - min.z) / size.z) * this.res;

    // Clamp to grid
    const x0 = Math.max(0, Math.min(this.res - 2, nx | 0));
    const y0 = Math.max(0, Math.min(this.res - 2, ny | 0));
    const z0 = Math.max(0, Math.min(this.res - 2, nz | 0));
    const fx = nx - x0, fy = ny - y0, fz = nz - z0;

    // Trilinear interpolation
    const get = (x: number, y: number, z: number) => {
      const idx = (z * this.res * this.res + y * this.res + x) * 3;
      return { x: this.data[idx] ?? 0, y: this.data[idx + 1] ?? 0, z: this.data[idx + 2] ?? 0 };
    };

    const c000 = get(x0, y0, z0), c100 = get(x0 + 1, y0, z0);
    const c010 = get(x0, y0 + 1, z0), c110 = get(x0 + 1, y0 + 1, z0);
    const c001 = get(x0, y0, z0 + 1), c101 = get(x0 + 1, y0, z0 + 1);
    const c011 = get(x0, y0 + 1, z0 + 1), c111 = get(x0 + 1, y0 + 1, z0 + 1);

    const lerp = (a: number, b: number, t: number) => a + (b - a) * t;

    out.x = lerp(lerp(lerp(c000.x, c100.x, fx), lerp(c010.x, c110.x, fx), fy),
                 lerp(lerp(c001.x, c101.x, fx), lerp(c011.x, c111.x, fx), fy), fz);
    out.y = lerp(lerp(lerp(c000.y, c100.y, fx), lerp(c010.y, c110.y, fx), fy),
                 lerp(lerp(c001.y, c101.y, fx), lerp(c011.y, c111.y, fx), fy), fz);
    out.z = lerp(lerp(lerp(c000.z, c100.z, fx), lerp(c010.z, c110.z, fx), fy),
                 lerp(lerp(c001.z, c101.z, fx), lerp(c011.z, c111.z, fx), fy), fz);

    return out;
  }
}

// ── Particle System ──

/**
 * CPU flow field particle system.
 *
 * Particles spawn at source nodes of directed edges, follow the flow field,
 * and despawn after their lifecycle completes.
 */
export class FlowParticleSystem {
  readonly particles: FlowParticle[];
  readonly config: FlowFieldConfig;
  private field: FlowField3D;
  private edgeData: { srcPos: THREE.Vector3; tgtPos: THREE.Vector3; health: number }[] = [];
  private _vel = new THREE.Vector3();

  constructor(config?: Partial<FlowFieldConfig>) {
    this.config = { ...DEFAULT_CONFIG, ...config };
    this.field = new FlowField3D(this.config.resolution);
    this.particles = [];
  }

  /** Rebuild flow field from current graph state */
  rebuildField(nodes: PositionedNode[], edges: GovernanceEdge[]): void {
    this.field.build(nodes, edges);

    // Cache edge source/target positions for spawning
    const nodePos = new Map<string, THREE.Vector3>();
    for (const n of nodes) {
      if (n.x === undefined) continue;
      nodePos.set(n.id, new THREE.Vector3(n.x, n.y ?? 0, n.z ?? 0));
    }

    this.edgeData = [];
    for (const e of edges) {
      const src = nodePos.get(e.source);
      const tgt = nodePos.get(e.target);
      if (!src || !tgt) continue;
      // Health from source node
      const srcNode = nodes.find(n => n.id === e.source);
      const health = srcNode?.healthStatus === 'healthy' ? 1.0
        : srcNode?.healthStatus === 'warning' ? 0.6
        : srcNode?.healthStatus === 'error' ? 0.2
        : 0.5;
      this.edgeData.push({ srcPos: src.clone(), tgtPos: tgt.clone(), health });
    }
  }

  /** Advance simulation by dt seconds */
  step(dt: number): void {
    if (this.edgeData.length === 0) return;

    // Spawn new particles to maintain target count
    const spawnRate = this.config.maxParticles * 0.1 * dt; // 10% per second
    const toSpawn = Math.min(spawnRate, this.config.maxParticles - this.particles.length);
    for (let i = 0; i < toSpawn; i++) {
      const edgeIdx = (Math.random() * this.edgeData.length) | 0;
      const edge = this.edgeData[edgeIdx];
      // Spawn near source with slight jitter
      const jitter = 2.0;
      this.particles.push({
        position: edge.srcPos.clone().add(
          new THREE.Vector3(
            (Math.random() - 0.5) * jitter,
            (Math.random() - 0.5) * jitter,
            (Math.random() - 0.5) * jitter,
          ),
        ),
        velocity: new THREE.Vector3(),
        health: edge.health,
        phase: 0,
        speed: 0,
        sourceEdgeIdx: edgeIdx,
      });
    }

    // Update particles
    const lifetime = 8.0; // seconds
    const curlScale = this.config.curlStrength;
    const speedScale = this.config.speedScale;

    for (let i = this.particles.length - 1; i >= 0; i--) {
      const p = this.particles[i];

      // Advance phase
      p.phase += dt / lifetime;
      if (p.phase >= 1.0) {
        // Remove dead particle (swap with last for O(1))
        this.particles[i] = this.particles[this.particles.length - 1];
        this.particles.pop();
        continue;
      }

      // Sample flow field
      this.field.sample(p.position, this._vel);

      // Add curl noise for organic swirl
      const t = Date.now() * 0.0003;
      const px = p.position.x * 0.05 + t;
      const py = p.position.y * 0.05;
      const pz = p.position.z * 0.05;
      this._vel.x += Math.sin(py * 3.7 + pz * 1.3) * curlScale;
      this._vel.y += Math.sin(pz * 2.9 + px * 1.7) * curlScale;
      this._vel.z += Math.sin(px * 3.3 + py * 2.1) * curlScale;

      // Scale velocity
      this._vel.multiplyScalar(speedScale);
      p.speed = this._vel.length();

      // Integrate position
      p.position.addScaledVector(this._vel, dt);
      p.velocity.copy(this._vel);
    }
  }

  /** Write particle data to typed arrays (for BufferGeometry attributes) */
  writeToBuffers(
    positions: Float32Array,
    healths: Float32Array,
    phases: Float32Array,
    speeds: Float32Array,
  ): number {
    const count = this.particles.length;
    for (let i = 0; i < count; i++) {
      const p = this.particles[i];
      const i3 = i * 3;
      positions[i3] = p.position.x;
      positions[i3 + 1] = p.position.y;
      positions[i3 + 2] = p.position.z;
      healths[i] = p.health;
      phases[i] = p.phase;
      speeds[i] = p.speed;
    }
    return count;
  }
}
