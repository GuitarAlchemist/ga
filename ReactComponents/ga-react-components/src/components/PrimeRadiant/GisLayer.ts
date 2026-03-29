// src/components/PrimeRadiant/GisLayer.ts
// AI GIS layer system for Prime Radiant planets (Three.js)
// Pins, clusters, paths, real-time updates on spherical surfaces.
// Works on Earth or any planet mesh in the SolarSystem.

import * as THREE from 'three';

// ---------------------------------------------------------------------------
// Types
// ---------------------------------------------------------------------------

export interface GisPin {
  id: string;
  lat: number;
  lon: number;
  label: string;
  color?: string;
  icon?: string;           // emoji or short text
  size?: number;           // 0.5-3.0 (default 1.0)
  category?: string;       // for clustering and filtering
  data?: Record<string, unknown>;  // arbitrary payload
  pulse?: boolean;         // animated pulse ring
}

export interface GisPath {
  id: string;
  points: { lat: number; lon: number }[];
  color?: string;
  width?: number;          // 1-5 (default 2)
  dashed?: boolean;
  animated?: boolean;      // moving dash pattern
  label?: string;
}

export interface GisCluster {
  center: { lat: number; lon: number };
  count: number;
  pins: GisPin[];
  radius: number;          // cluster radius in degrees
}

export interface GisHeatPoint {
  lat: number;
  lon: number;
  intensity: number;       // 0-1
}

export type GisLayerType = 'pins' | 'paths' | 'clusters' | 'heatmap';

export interface GisLayerState {
  pins: Map<string, { pin: GisPin; group: THREE.Group }>;
  paths: Map<string, { path: GisPath; line: THREE.Line }>;
  clusters: GisCluster[];
  clusterMeshes: THREE.Group[];
  heatPoints: GisHeatPoint[];
  heatMesh: THREE.Mesh | null;
}

// ---------------------------------------------------------------------------
// Coordinate conversion — lat/lon to 3D position on a sphere
// ---------------------------------------------------------------------------

function latLonToPosition(lat: number, lon: number, radius: number): THREE.Vector3 {
  const phi = (90 - lat) * Math.PI / 180;
  const theta = (lon + 180) * Math.PI / 180;
  return new THREE.Vector3(
    -radius * Math.sin(phi) * Math.cos(theta),
    radius * Math.cos(phi),
    radius * Math.sin(phi) * Math.sin(theta),
  );
}

/** Great circle interpolation between two lat/lon points */
function interpolateGreatCircle(
  p1: { lat: number; lon: number },
  p2: { lat: number; lon: number },
  steps: number,
): { lat: number; lon: number }[] {
  const toRad = Math.PI / 180;
  const lat1 = p1.lat * toRad, lon1 = p1.lon * toRad;
  const lat2 = p2.lat * toRad, lon2 = p2.lon * toRad;

  const d = Math.acos(
    Math.sin(lat1) * Math.sin(lat2) +
    Math.cos(lat1) * Math.cos(lat2) * Math.cos(lon2 - lon1)
  );

  if (d < 0.001) return [p1, p2]; // too close

  const points: { lat: number; lon: number }[] = [];
  for (let i = 0; i <= steps; i++) {
    const f = i / steps;
    const A = Math.sin((1 - f) * d) / Math.sin(d);
    const B = Math.sin(f * d) / Math.sin(d);
    const x = A * Math.cos(lat1) * Math.cos(lon1) + B * Math.cos(lat2) * Math.cos(lon2);
    const y = A * Math.cos(lat1) * Math.sin(lon1) + B * Math.cos(lat2) * Math.sin(lon2);
    const z = A * Math.sin(lat1) + B * Math.sin(lat2);
    points.push({
      lat: Math.atan2(z, Math.sqrt(x * x + y * y)) / toRad,
      lon: Math.atan2(y, x) / toRad,
    });
  }
  return points;
}

/** Haversine distance in degrees */
function haversineDistance(a: { lat: number; lon: number }, b: { lat: number; lon: number }): number {
  const toRad = Math.PI / 180;
  const dLat = (b.lat - a.lat) * toRad;
  const dLon = (b.lon - a.lon) * toRad;
  const h = Math.sin(dLat / 2) ** 2 +
    Math.cos(a.lat * toRad) * Math.cos(b.lat * toRad) * Math.sin(dLon / 2) ** 2;
  return Math.atan2(Math.sqrt(h), Math.sqrt(1 - h)) * 2 * (180 / Math.PI);
}

// ---------------------------------------------------------------------------
// Pin rendering
// ---------------------------------------------------------------------------

const PIN_GEO = new THREE.ConeGeometry(0.012, 0.04, 6);
PIN_GEO.rotateX(Math.PI); // point downward
const PIN_SPHERE_GEO = new THREE.SphereGeometry(0.008, 6, 6);
const PULSE_RING_GEO = new THREE.RingGeometry(0.01, 0.02, 12);

function createPinMesh(pin: GisPin, radius: number): THREE.Group {
  const group = new THREE.Group();
  const color = new THREE.Color(pin.color ?? '#ff4444');
  const sz = (pin.size ?? 1.0) * radius * 0.15; // scale with planet radius

  // Pin body (cone)
  const coneMat = new THREE.MeshBasicMaterial({ color, transparent: true, opacity: 0.9 });
  const cone = new THREE.Mesh(PIN_GEO, coneMat);
  cone.scale.setScalar(sz);
  group.add(cone);

  // Pin head (sphere)
  const headMat = new THREE.MeshBasicMaterial({ color, transparent: true, opacity: 1.0 });
  const head = new THREE.Mesh(PIN_SPHERE_GEO, headMat);
  head.position.y = 0.03 * sz;
  head.scale.setScalar(sz);
  group.add(head);

  // Label sprite
  if (pin.label || pin.icon) {
    const canvas = document.createElement('canvas');
    canvas.width = 256;
    canvas.height = 64;
    const ctx = canvas.getContext('2d')!;
    ctx.clearRect(0, 0, 256, 64);
    ctx.fillStyle = 'rgba(0,0,0,0.6)';
    ctx.roundRect(2, 2, 252, 60, 8);
    ctx.fill();
    ctx.fillStyle = '#ffffff';
    ctx.font = 'bold 24px sans-serif';
    ctx.textAlign = 'center';
    ctx.textBaseline = 'middle';
    ctx.fillText(`${pin.icon ?? ''} ${pin.label}`.trim().slice(0, 20), 128, 32);

    const tex = new THREE.CanvasTexture(canvas);
    const spriteMat = new THREE.SpriteMaterial({
      map: tex,
      transparent: true,
      depthWrite: false,
      sizeAttenuation: true,
    });
    const sprite = new THREE.Sprite(spriteMat);
    sprite.scale.set(0.08 * sz, 0.02 * sz, 1);
    sprite.position.y = 0.06 * sz;
    group.add(sprite);
  }

  // Pulse ring (optional)
  if (pin.pulse) {
    const ringMat = new THREE.MeshBasicMaterial({
      color,
      transparent: true,
      opacity: 0.5,
      side: THREE.DoubleSide,
      depthWrite: false,
    });
    const ring = new THREE.Mesh(PULSE_RING_GEO, ringMat);
    ring.name = 'pulse-ring';
    ring.userData.pulseTime = 0;
    group.add(ring);
  }

  // Position on sphere surface
  const pos = latLonToPosition(pin.lat, pin.lon, radius * 1.01);
  group.position.copy(pos);
  // Orient pin to point away from planet center
  group.lookAt(new THREE.Vector3(0, 0, 0));
  group.rotateX(Math.PI);

  group.userData.pin = pin;
  return group;
}

// ---------------------------------------------------------------------------
// Path rendering — great circle arcs on sphere surface
// ---------------------------------------------------------------------------

function createPathMesh(path: GisPath, radius: number): THREE.Line {
  const color = new THREE.Color(path.color ?? '#33ccff');
  const points: THREE.Vector3[] = [];

  for (let i = 0; i < path.points.length - 1; i++) {
    const arc = interpolateGreatCircle(path.points[i], path.points[i + 1], 24);
    for (const p of arc) {
      points.push(latLonToPosition(p.lat, p.lon, radius * 1.015));
    }
  }

  const geometry = new THREE.BufferGeometry().setFromPoints(points);

  let material: THREE.Material;
  if (path.dashed) {
    material = new THREE.LineDashedMaterial({
      color,
      linewidth: path.width ?? 2,
      dashSize: 0.02,
      gapSize: 0.01,
      transparent: true,
      opacity: 0.8,
    });
  } else {
    material = new THREE.LineBasicMaterial({
      color,
      linewidth: path.width ?? 2,
      transparent: true,
      opacity: 0.8,
    });
  }

  const line = new THREE.Line(geometry, material);
  if (path.dashed) line.computeLineDistances();
  line.userData.path = path;
  line.name = `gis-path-${path.id}`;
  return line;
}

// ---------------------------------------------------------------------------
// Cluster rendering — group nearby pins into single markers
// ---------------------------------------------------------------------------

function computeClusters(pins: GisPin[], clusterRadius: number): GisCluster[] {
  const clusters: GisCluster[] = [];
  const assigned = new Set<string>();

  for (const pin of pins) {
    if (assigned.has(pin.id)) continue;

    const nearby = pins.filter(p =>
      !assigned.has(p.id) && haversineDistance(pin, p) < clusterRadius
    );

    if (nearby.length >= 3) {
      // Compute centroid
      const avgLat = nearby.reduce((s, p) => s + p.lat, 0) / nearby.length;
      const avgLon = nearby.reduce((s, p) => s + p.lon, 0) / nearby.length;
      clusters.push({
        center: { lat: avgLat, lon: avgLon },
        count: nearby.length,
        pins: nearby,
        radius: clusterRadius,
      });
      for (const p of nearby) assigned.add(p.id);
    }
  }

  return clusters;
}

function createClusterMesh(cluster: GisCluster, radius: number): THREE.Group {
  const group = new THREE.Group();

  // Cluster bubble
  const sz = Math.min(0.04, 0.015 + cluster.count * 0.003);
  const geo = new THREE.SphereGeometry(sz, 8, 8);
  const mat = new THREE.MeshBasicMaterial({
    color: 0x8B5CF6,
    transparent: true,
    opacity: 0.7,
  });
  const bubble = new THREE.Mesh(geo, mat);
  group.add(bubble);

  // Count label
  const canvas = document.createElement('canvas');
  canvas.width = 64;
  canvas.height = 64;
  const ctx = canvas.getContext('2d')!;
  ctx.clearRect(0, 0, 64, 64);
  ctx.fillStyle = '#ffffff';
  ctx.font = 'bold 36px sans-serif';
  ctx.textAlign = 'center';
  ctx.textBaseline = 'middle';
  ctx.fillText(String(cluster.count), 32, 32);

  const tex = new THREE.CanvasTexture(canvas);
  const spriteMat = new THREE.SpriteMaterial({
    map: tex,
    transparent: true,
    depthWrite: false,
    sizeAttenuation: true,
  });
  const sprite = new THREE.Sprite(spriteMat);
  sprite.scale.set(sz * 2, sz * 2, 1);
  group.add(sprite);

  const pos = latLonToPosition(cluster.center.lat, cluster.center.lon, radius * 1.02);
  group.position.copy(pos);
  group.userData.cluster = cluster;
  return group;
}

// ---------------------------------------------------------------------------
// GIS Layer Manager — main API
// ---------------------------------------------------------------------------

export class GisLayerManager {
  private state: GisLayerState = {
    pins: new Map(),
    paths: new Map(),
    clusters: [],
    clusterMeshes: [],
    heatPoints: [],
    heatMesh: null,
  };
  private planetGroup: THREE.Group;
  private gisGroup: THREE.Group;
  private planetRadius: number;
  private clusterRadius = 10; // degrees
  private clusteringEnabled = false;
  private listeners = new Set<() => void>();
  private animTime = 0;

  constructor(planetGroup: THREE.Group, planetMesh: THREE.Mesh) {
    this.planetGroup = planetGroup;
    const geo = planetMesh.geometry as THREE.SphereGeometry;
    this.planetRadius = geo.parameters.radius;

    this.gisGroup = new THREE.Group();
    this.gisGroup.name = 'gis-layer';
    // Add to orbit group so it rotates with the planet
    const orbit = planetMesh.parent ?? planetGroup;
    orbit.add(this.gisGroup);
  }

  // --- Pin operations ---

  addPin(pin: GisPin): void {
    this.removePin(pin.id);
    const group = createPinMesh(pin, this.planetRadius);
    this.gisGroup.add(group);
    this.state.pins.set(pin.id, { pin, group });
    if (this.clusteringEnabled) this.rebuildClusters();
    this.notify();
  }

  addPins(pins: GisPin[]): void {
    for (const pin of pins) {
      this.removePin(pin.id);
      const group = createPinMesh(pin, this.planetRadius);
      this.gisGroup.add(group);
      this.state.pins.set(pin.id, { pin, group });
    }
    if (this.clusteringEnabled) this.rebuildClusters();
    this.notify();
  }

  removePin(id: string): void {
    const entry = this.state.pins.get(id);
    if (entry) {
      this.gisGroup.remove(entry.group);
      entry.group.traverse(obj => {
        if (obj instanceof THREE.Mesh) {
          obj.geometry?.dispose();
          if (obj.material instanceof THREE.Material) obj.material.dispose();
        }
      });
      this.state.pins.delete(id);
    }
  }

  updatePin(id: string, updates: Partial<GisPin>): void {
    const entry = this.state.pins.get(id);
    if (!entry) return;
    const updated = { ...entry.pin, ...updates };
    this.removePin(id);
    this.addPin(updated);
  }

  getPins(): GisPin[] {
    return Array.from(this.state.pins.values()).map(e => e.pin);
  }

  // --- Path operations ---

  addPath(path: GisPath): void {
    this.removePath(path.id);
    const line = createPathMesh(path, this.planetRadius);
    this.gisGroup.add(line);
    this.state.paths.set(path.id, { path, line });
    this.notify();
  }

  removePath(id: string): void {
    const entry = this.state.paths.get(id);
    if (entry) {
      this.gisGroup.remove(entry.line);
      entry.line.geometry.dispose();
      if (entry.line.material instanceof THREE.Material) entry.line.material.dispose();
      this.state.paths.delete(id);
    }
  }

  getPaths(): GisPath[] {
    return Array.from(this.state.paths.values()).map(e => e.path);
  }

  // --- Clustering ---

  enableClustering(radius: number = 10): void {
    this.clusteringEnabled = true;
    this.clusterRadius = radius;
    this.rebuildClusters();
  }

  disableClustering(): void {
    this.clusteringEnabled = false;
    this.clearClusters();
    // Show all individual pins
    for (const entry of this.state.pins.values()) {
      entry.group.visible = true;
    }
  }

  private rebuildClusters(): void {
    this.clearClusters();
    const allPins = this.getPins();
    const clusters = computeClusters(allPins, this.clusterRadius);
    this.state.clusters = clusters;

    const clusteredIds = new Set<string>();
    for (const c of clusters) {
      for (const p of c.pins) clusteredIds.add(p.id);
      const mesh = createClusterMesh(c, this.planetRadius);
      this.gisGroup.add(mesh);
      this.state.clusterMeshes.push(mesh);
    }

    // Hide clustered pins, show unclustered
    for (const [id, entry] of this.state.pins) {
      entry.group.visible = !clusteredIds.has(id);
    }
  }

  private clearClusters(): void {
    for (const mesh of this.state.clusterMeshes) {
      this.gisGroup.remove(mesh);
    }
    this.state.clusterMeshes = [];
    this.state.clusters = [];
  }

  // --- Heatmap operations ---

  addHeatPoints(points: GisHeatPoint[]): void {
    this.state.heatPoints.push(...points);
    this.rebuildHeatmap();
    this.notify();
  }

  clearHeatmap(): void {
    this.state.heatPoints = [];
    if (this.state.heatMesh) {
      this.gisGroup.remove(this.state.heatMesh);
      this.state.heatMesh.geometry.dispose();
      if (this.state.heatMesh.material instanceof THREE.Material) {
        this.state.heatMesh.material.dispose();
      }
      this.state.heatMesh = null;
    }
    this.notify();
  }

  private rebuildHeatmap(): void {
    // Remove old heatmap mesh
    if (this.state.heatMesh) {
      this.gisGroup.remove(this.state.heatMesh);
      this.state.heatMesh.geometry.dispose();
      if (this.state.heatMesh.material instanceof THREE.Material) {
        this.state.heatMesh.material.dispose();
      }
      this.state.heatMesh = null;
    }

    if (this.state.heatPoints.length === 0) return;

    // Low-poly sphere shell slightly above the planet surface
    const heatRadius = this.planetRadius * 1.02;
    const segments = 32;
    const geo = new THREE.SphereGeometry(heatRadius, segments, segments);
    const posAttr = geo.attributes.position;
    const vertexCount = posAttr.count;

    // Build vertex colors by sampling proximity to heat points
    const colors = new Float32Array(vertexCount * 3);
    const cold = new THREE.Color(0x0044ff); // blue
    const hot = new THREE.Color(0xff2200);  // red

    for (let i = 0; i < vertexCount; i++) {
      const vx = posAttr.getX(i);
      const vy = posAttr.getY(i);
      const vz = posAttr.getZ(i);

      // Convert vertex position back to lat/lon
      const r = Math.sqrt(vx * vx + vy * vy + vz * vz);
      const lat = Math.asin(vy / r) * (180 / Math.PI);
      const lon = Math.atan2(vz, -vx) * (180 / Math.PI) - 180;

      // Compute heat influence from all heat points (inverse distance weighting)
      let heat = 0;
      for (const hp of this.state.heatPoints) {
        const dist = haversineDistance({ lat, lon }, { lat: hp.lat, lon: hp.lon });
        // Influence falls off with distance; 15-degree half-life
        const influence = hp.intensity * Math.exp(-dist / 15);
        heat = Math.max(heat, influence);
      }
      heat = Math.min(1, heat);

      const c = cold.clone().lerp(hot, heat);
      colors[i * 3] = c.r;
      colors[i * 3 + 1] = c.g;
      colors[i * 3 + 2] = c.b;
    }

    geo.setAttribute('color', new THREE.BufferAttribute(colors, 3));

    const mat = new THREE.MeshBasicMaterial({
      vertexColors: true,
      transparent: true,
      opacity: 0.45,
      depthWrite: false,
      blending: THREE.AdditiveBlending,
      side: THREE.FrontSide,
    });

    const mesh = new THREE.Mesh(geo, mat);
    mesh.name = 'gis-heatmap';
    this.state.heatMesh = mesh;
    this.gisGroup.add(mesh);
  }

  // --- Clear all ---

  clearAll(): void {
    for (const [id] of this.state.pins) this.removePin(id);
    for (const [id] of this.state.paths) this.removePath(id);
    this.clearClusters();
    this.clearHeatmap();
    this.notify();
  }

  // --- Animation tick (call from updateSolarSystem) ---

  update(time: number): void {
    this.animTime = time;
    // Pulse rings
    for (const entry of this.state.pins.values()) {
      const ring = entry.group.getObjectByName('pulse-ring') as THREE.Mesh | undefined;
      if (ring) {
        const t = (ring.userData.pulseTime ?? 0) + 0.02;
        ring.userData.pulseTime = t;
        const scale = 1 + Math.sin(t * 3) * 0.5;
        ring.scale.setScalar(scale);
        const mat = ring.material as THREE.MeshBasicMaterial;
        mat.opacity = 0.5 * (1 - Math.abs(Math.sin(t * 3)) * 0.6);
      }
    }

    // Animated dashed paths
    for (const entry of this.state.paths.values()) {
      if (entry.path.animated && entry.line.material instanceof THREE.LineDashedMaterial) {
        (entry.line.material as THREE.LineDashedMaterial).dashOffset -= 0.001;
      }
    }

    // Sync GIS group rotation with planet mesh rotation
    // (already handled by parenting to orbit group)
  }

  // --- Subscribe to changes ---

  onChange(fn: () => void): () => void {
    this.listeners.add(fn);
    return () => { this.listeners.delete(fn); };
  }

  private notify(): void {
    for (const fn of this.listeners) fn();
  }

  // --- Stats ---

  get pinCount(): number { return this.state.pins.size; }
  get pathCount(): number { return this.state.paths.size; }
  get clusterCount(): number { return this.state.clusters.length; }

  dispose(): void {
    this.clearAll();
    this.gisGroup.parent?.remove(this.gisGroup);
  }
}

// ---------------------------------------------------------------------------
// Factory — create GIS manager for a specific planet in the solar system
// ---------------------------------------------------------------------------

export function createGisLayer(
  solarGroup: THREE.Group,
  planetName: string = 'earth',
): GisLayerManager | null {
  const planets = solarGroup.userData.planets as
    { mesh: THREE.Mesh; orbit: THREE.Group; def: { name: string } }[] | undefined;
  const planet = planets?.find(p => p.def.name === planetName);
  if (!planet) return null;
  return new GisLayerManager(solarGroup, planet.mesh);
}
