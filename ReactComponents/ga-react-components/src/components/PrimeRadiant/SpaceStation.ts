// src/components/PrimeRadiant/SpaceStation.ts
// Jarvis Space Station — procedural modular station mapping the five-layer
// architecture and four governance wings, with assembly animation and
// status lighting.

import * as THREE from 'three';

// ---------------------------------------------------------------------------
// Module definitions
// ---------------------------------------------------------------------------
interface StationModule {
  name: string;
  color: string;
  size: [number, number, number]; // w, h, d
  dockPosition: THREE.Vector3;    // final docked position
  startPosition: THREE.Vector3;   // detached start position
  passing: boolean;               // status: lit = passing
}

const CORE_MODULES: StationModule[] = [
  { name: 'Core',          color: '#8A8A9A', size: [1.2, 0.8, 0.8], dockPosition: new THREE.Vector3(0, 1.5, 0),   startPosition: new THREE.Vector3(0, 12, 0),   passing: true },
  { name: 'Domain',        color: '#7A7A8A', size: [1.4, 0.8, 0.8], dockPosition: new THREE.Vector3(0, 0.5, 0),   startPosition: new THREE.Vector3(-10, 5, 8),   passing: true },
  { name: 'Analysis',      color: '#9090A0', size: [1.3, 0.8, 0.8], dockPosition: new THREE.Vector3(0, -0.5, 0),  startPosition: new THREE.Vector3(8, -6, -10),  passing: true },
  { name: 'ML',            color: '#7A8A7A', size: [1.5, 0.8, 0.8], dockPosition: new THREE.Vector3(0, -1.5, 0),  startPosition: new THREE.Vector3(-12, -8, 5),  passing: true },
  { name: 'Orchestration', color: '#7A8A90', size: [1.6, 0.8, 0.8], dockPosition: new THREE.Vector3(0, -2.5, 0),  startPosition: new THREE.Vector3(6, 10, -12),  passing: true },
];

const WING_MODULES: StationModule[] = [
  { name: 'Constitutions', color: '#FFD700', size: [2.0, 0.4, 0.5], dockPosition: new THREE.Vector3(2.5, 0.5, 0),   startPosition: new THREE.Vector3(15, 3, 6),   passing: true },
  { name: 'Policies',      color: '#B0C4DE', size: [2.0, 0.4, 0.5], dockPosition: new THREE.Vector3(-2.5, 0.5, 0),  startPosition: new THREE.Vector3(-15, -3, 8),  passing: true },
  { name: 'Personas',      color: '#33CC66', size: [2.0, 0.4, 0.5], dockPosition: new THREE.Vector3(0, 0.5, 2.5),   startPosition: new THREE.Vector3(4, -12, 14),  passing: true },
  { name: 'Pipelines',     color: '#FFB300', size: [2.0, 0.4, 0.5], dockPosition: new THREE.Vector3(0, 0.5, -2.5),  startPosition: new THREE.Vector3(-8, 8, -14),  passing: true },
];

const ASSEMBLY_DURATION = 30; // seconds

// ---------------------------------------------------------------------------
// HUD label sprite
// ---------------------------------------------------------------------------
function createLabelSprite(text: string, scale: number): THREE.Sprite {
  const canvas = document.createElement('canvas');
  canvas.width = 256;
  canvas.height = 64;
  const ctx = canvas.getContext('2d');
  if (ctx) {
    ctx.fillStyle = 'transparent';
    ctx.fillRect(0, 0, 256, 64);
    ctx.font = 'bold 28px monospace';
    ctx.fillStyle = '#FFD700';
    ctx.textAlign = 'center';
    ctx.textBaseline = 'middle';
    ctx.fillText(text, 128, 32);
  }
  const texture = new THREE.CanvasTexture(canvas);
  texture.needsUpdate = true;
  const spriteMat = new THREE.SpriteMaterial({
    map: texture,
    transparent: true,
    depthWrite: false,
    blending: THREE.AdditiveBlending,
  });
  const sprite = new THREE.Sprite(spriteMat);
  sprite.scale.set(2.5 * scale, 0.6 * scale, 1);
  return sprite;
}

// ---------------------------------------------------------------------------
// Module mesh with status lighting
// ---------------------------------------------------------------------------
function createModuleMesh(
  mod: StationModule,
  scale: number,
): THREE.Group {
  const moduleGroup = new THREE.Group();
  moduleGroup.name = `station-module-${mod.name}`;

  const [w, h, d] = mod.size;
  const geo = new THREE.BoxGeometry(w * scale, h * scale, d * scale);
  const mat = new THREE.MeshBasicMaterial({
    color: new THREE.Color(mod.color),
    transparent: true,
    opacity: 0.6,
    blending: THREE.AdditiveBlending,
    depthWrite: false,
  });
  const mesh = new THREE.Mesh(geo, mat);
  moduleGroup.add(mesh);

  // Wireframe overlay
  const edgesGeo = new THREE.EdgesGeometry(geo);
  const edgesMat = new THREE.LineBasicMaterial({
    color: new THREE.Color(mod.color),
    transparent: true,
    opacity: 0.8,
    blending: THREE.AdditiveBlending,
  });
  moduleGroup.add(new THREE.LineSegments(edgesGeo, edgesMat));

  // Status light — small emissive sphere
  const lightGeo = new THREE.SphereGeometry(0.08 * scale, 6, 6);
  const lightMat = new THREE.MeshBasicMaterial({
    color: mod.passing ? 0x33CC66 : 0xFF4444,
    transparent: true,
    opacity: mod.passing ? 0.9 : 0.3,
    blending: THREE.AdditiveBlending,
  });
  const statusLight = new THREE.Mesh(lightGeo, lightMat);
  statusLight.position.set(w * scale * 0.5, h * scale * 0.3, d * scale * 0.5);
  statusLight.name = `status-light-${mod.name}`;
  moduleGroup.add(statusLight);

  // HUD label
  const label = createLabelSprite(mod.name, scale);
  label.position.set(0, h * scale * 0.6, 0);
  moduleGroup.add(label);

  // Store module data for animation
  moduleGroup.userData.moduleDef = mod;
  moduleGroup.userData.scale = scale;

  return moduleGroup;
}

// ---------------------------------------------------------------------------
// Create the full space station
// ---------------------------------------------------------------------------
export function createSpaceStation(scale: number = 1): THREE.Group {
  const group = new THREE.Group();
  group.userData.isSpaceStation = true;
  group.userData.assemblyStart = -1; // set on first update
  group.userData.assembled = false;

  // ── Central hub — cylinder connecting all modules ──
  const hubGeo = new THREE.CylinderGeometry(0.5 * scale, 0.5 * scale, 5 * scale, 16);
  const hubMat = new THREE.MeshBasicMaterial({
    color: new THREE.Color('#C0C0C0'),
    transparent: true,
    opacity: 0.4,
    blending: THREE.AdditiveBlending,
    depthWrite: false,
  });
  const hub = new THREE.Mesh(hubGeo, hubMat);
  hub.name = 'station-hub';
  group.add(hub);

  // Hub wireframe
  const hubEdges = new THREE.EdgesGeometry(hubGeo);
  const hubWireMat = new THREE.LineBasicMaterial({
    color: new THREE.Color('#FFD700'),
    transparent: true,
    opacity: 0.5,
    blending: THREE.AdditiveBlending,
  });
  group.add(new THREE.LineSegments(hubEdges, hubWireMat));

  // ── Rotating ring section ──
  const ringGeo = new THREE.TorusGeometry(2.5 * scale, 0.15 * scale, 8, 32);
  const ringMat = new THREE.MeshBasicMaterial({
    color: new THREE.Color('#B0C4DE'),
    transparent: true,
    opacity: 0.5,
    blending: THREE.AdditiveBlending,
    depthWrite: false,
  });
  const ring = new THREE.Mesh(ringGeo, ringMat);
  ring.rotation.x = Math.PI / 2;
  ring.name = 'station-ring';
  group.add(ring);

  // Ring wireframe
  const ringEdges = new THREE.EdgesGeometry(ringGeo);
  const ringWireMat = new THREE.LineBasicMaterial({
    color: new THREE.Color('#FFD700'),
    transparent: true,
    opacity: 0.4,
    blending: THREE.AdditiveBlending,
  });
  const ringWire = new THREE.LineSegments(ringEdges, ringWireMat);
  ringWire.rotation.x = Math.PI / 2;
  ringWire.name = 'station-ring-wire';
  group.add(ringWire);

  // ── Core modules (5 layers) ──
  const coreModules: THREE.Group[] = [];
  for (const mod of CORE_MODULES) {
    const moduleGroup = createModuleMesh(mod, scale);
    // Start at detached position
    moduleGroup.position.copy(mod.startPosition.clone().multiplyScalar(scale));
    coreModules.push(moduleGroup);
    group.add(moduleGroup);
  }

  // ── Wing modules (4 governance) ──
  const wingModules: THREE.Group[] = [];
  for (const mod of WING_MODULES) {
    const moduleGroup = createModuleMesh(mod, scale);
    moduleGroup.position.copy(mod.startPosition.clone().multiplyScalar(scale));
    wingModules.push(moduleGroup);
    group.add(moduleGroup);
  }

  group.userData.parts = { hub, ring, ringWire, coreModules, wingModules, scale };

  return group;
}

// ---------------------------------------------------------------------------
// Animate space station each frame
// ---------------------------------------------------------------------------
export function updateSpaceStation(group: THREE.Group, time: number): void {
  const parts = group.userData.parts as {
    hub: THREE.Mesh;
    ring: THREE.Mesh;
    ringWire: THREE.LineSegments;
    coreModules: THREE.Group[];
    wingModules: THREE.Group[];
    scale: number;
  } | undefined;
  if (!parts) return;

  const { ring, ringWire, coreModules, wingModules, scale } = parts;

  // ── Assembly start tracking ──
  if (group.userData.assemblyStart < 0) {
    group.userData.assemblyStart = time;
  }
  const elapsed = time - (group.userData.assemblyStart as number);
  const assemblyProgress = Math.min(elapsed / ASSEMBLY_DURATION, 1);
  const eased = 1 - Math.pow(1 - assemblyProgress, 3); // ease-out cubic

  // ── Dock animation — modules lerp from start to dock positions ──
  const allModules = [...coreModules, ...wingModules];
  for (const moduleGroup of allModules) {
    const mod = moduleGroup.userData.moduleDef as StationModule;
    const start = mod.startPosition.clone().multiplyScalar(scale);
    const dock = mod.dockPosition.clone().multiplyScalar(scale);
    moduleGroup.position.lerpVectors(start, dock, eased);

    // Gentle wobble after docking
    if (assemblyProgress >= 1) {
      const idx = allModules.indexOf(moduleGroup);
      moduleGroup.position.y += Math.sin(time * 0.5 + idx * 0.7) * 0.03 * scale;
    }
  }

  group.userData.assembled = assemblyProgress >= 1;

  // ── Rotating ring ──
  ring.rotation.z = time * 0.3;
  ringWire.rotation.z = time * 0.3;

  // ── Status light flicker for failing modules ──
  for (const moduleGroup of allModules) {
    const mod = moduleGroup.userData.moduleDef as StationModule;
    const statusLight = moduleGroup.getObjectByName(`status-light-${mod.name}`) as THREE.Mesh | undefined;
    if (!statusLight) continue;

    const mat = statusLight.material as THREE.MeshBasicMaterial;
    if (mod.passing) {
      mat.opacity = 0.7 + Math.sin(time * 2) * 0.2;
      mat.color.setHex(0x33CC66);
    } else {
      // Flickering for failing
      const flicker = Math.sin(time * 8) > 0 ? 0.6 : 0.1;
      mat.opacity = flicker;
      mat.color.setHex(0xFF4444);
    }
  }

  // ── Slow station rotation ──
  group.rotation.y = time * 0.05;
  group.rotation.x = Math.sin(time * 0.1) * 0.03;
}
