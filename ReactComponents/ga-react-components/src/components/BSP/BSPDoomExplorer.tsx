// @ts-nocheck
/**
 * BSPDoomExplorer - DOOM-like First-Person BSP Tree Explorer
 * 
 * Navigate through the Binary Space Partitioning tree structure like exploring
 * a DOOM level. Each BSP partition plane becomes a wall, and tonal regions
 * become rooms with distinct visual characteristics.
 * 
 * Features:
 * - First-person camera with WASD movement and mouse look
 * - BSP partition planes rendered as semi-transparent walls
 * - Tonal regions visualized as colored rooms
 * - Real-time collision detection using BSP tree
 * - HUD showing current region information
 * - Minimap with BSP tree structure
 * - WebGPU rendering for maximum performance
 */

import React, { useEffect, useRef, useState, useCallback } from 'react';
import * as THREE from 'three';
import { GLTFLoader } from 'three/examples/jsm/loaders/GLTFLoader.js';
import { Sky } from 'three/examples/jsm/objects/Sky.js';
import { Box, Typography, Paper, Stack, Chip, Button } from '@mui/material';
import { BSPApiService, BSPTreeInfoResponse, BSPRegion, BSPTreeStructureResponse } from './BSPApiService';
import { ElementInfoPanel, ElementInfo } from './ElementInfoPanel';
import BSPElementDetailPanel, { BSPElementData } from './BSPElementDetailPanel';
import { PortalDoor } from './PortalDoor';
import { AnkhReticle3D } from './AnkhReticle3D';

// ==================
// Types
// ==================

interface BSPDoomExplorerProps {
  width?: number;
  height?: number;
  moveSpeed?: number;
  lookSpeed?: number;
  showHUD?: boolean;
  showMinimap?: boolean;
  onRegionChange?: (region: BSPRegion) => void;
  maxElementsPerRegion?: number; // Max elements to render individually (default: 100)
  enableLOD?: boolean; // Enable Level-of-Detail rendering (default: true)
  enableInstancing?: boolean; // Enable instanced rendering for performance (default: true)
}

interface PlayerState {
  position: THREE.Vector3;
  rotation: THREE.Euler;
  velocity: THREE.Vector3;
  currentRegion: BSPRegion | null;
}

interface KeyState {
  forward: boolean;
  backward: boolean;
  left: boolean;
  right: boolean;
  up: boolean;
  down: boolean;
}

// ==================
// Tonal Atmosphere System - Musical Biomes
// ==================

// Floor-based color schemes (Musical Biomes)
// Each floor has a distinct atmosphere that reflects its musical abstraction level
const FLOOR_ATMOSPHERES = [
  // Floor 0: Pitch Class Sets - Purple Cosmos (abstract space)
  {
    name: 'Pitch Class Sets',
    fogColor: new THREE.Color(0x2a1a3a), // Purple vapor
    skyTop: new THREE.Color(0x1a0a2a),
    skyHorizon: new THREE.Color(0x3a2a4a),
    skyBottom: new THREE.Color(0x0a0a1a),
    ambientColor: new THREE.Color(0x4a3a5a),
    floorColor: 0x1a1a2e,
    emissiveColor: 0x3a2a4a,
  },
  // Floor 1: Forte Codes - Sepia Archive (catalogued knowledge)
  {
    name: 'Forte Codes',
    fogColor: new THREE.Color(0x3a2a1a), // Sepia brown
    skyTop: new THREE.Color(0x2a1a0a),
    skyHorizon: new THREE.Color(0x4a3a2a),
    skyBottom: new THREE.Color(0x1a0a0a),
    ambientColor: new THREE.Color(0x5a4a3a),
    floorColor: 0x2a1a0a,
    emissiveColor: 0x4a3a2a,
  },
  // Floor 2: Prime Forms - Deep Teal (major-like simplicity)
  {
    name: 'Prime Forms',
    fogColor: new THREE.Color(0x1a3a3a), // Deep teal
    skyTop: new THREE.Color(0x0a2a2a),
    skyHorizon: new THREE.Color(0x2a4a4a),
    skyBottom: new THREE.Color(0x0a1a1a),
    ambientColor: new THREE.Color(0x3a5a5a),
    floorColor: 0x1a2a2a,
    emissiveColor: 0x2a4a4a,
  },
  // Floor 3: Chords - Bright Sky (grass floor with realistic sky)
  {
    name: 'Chords',
    fogColor: new THREE.Color(0x87CEEB), // Sky blue fog
    skyTop: new THREE.Color(0x4A90E2), // Bright blue sky
    skyHorizon: new THREE.Color(0x87CEEB), // Light blue horizon
    skyBottom: new THREE.Color(0xB0E0E6), // Powder blue bottom
    ambientColor: new THREE.Color(0xE0F0FF), // Bright ambient
    floorColor: 0x2a2a1a,
    emissiveColor: 0x4a4a3a,
  },
  // Floor 4: Inversions - Sunset Sky (grass floor with warm sunset)
  {
    name: 'Inversions',
    fogColor: new THREE.Color(0xFFB366), // Warm orange fog
    skyTop: new THREE.Color(0xFF6B35), // Orange-red sky
    skyHorizon: new THREE.Color(0xFFB366), // Warm orange horizon
    skyBottom: new THREE.Color(0xFFD9B3), // Light peach bottom
    ambientColor: new THREE.Color(0xFFE0CC), // Warm ambient
    floorColor: 0x2a1a2a,
    emissiveColor: 0x4a2a4a,
  },
  // Floor 5: Voicings - Golden Hour (grass floor with golden light)
  {
    name: 'Voicings',
    fogColor: new THREE.Color(0xFFD700), // Golden fog
    skyTop: new THREE.Color(0xFFA500), // Orange sky
    skyHorizon: new THREE.Color(0xFFD700), // Golden horizon
    skyBottom: new THREE.Color(0xFFE4B5), // Moccasin bottom
    ambientColor: new THREE.Color(0xFFF8DC), // Cornsilk ambient
    floorColor: 0x2a1a0a,
    emissiveColor: 0x4a3a2a,
  },
];

// Tonal family hue mapping (for color harmonics)
const TONAL_FAMILY_HUES: Record<string, number> = {
  'G-Family': 0.50,  // Cyan/Teal (deep teal for major-like simplicity)
  'C-Family': 0.55,  // Cyan
  'D-Family': 0.33,  // Green
  'A-Family': 0.92,  // Magenta
  'E-Family': 0.45,  // Teal
  'B-Family': 0.75,  // Purple
  'F-Family': 0.08,  // Orange/Amber
};

// Tonality type colors (DOOM-inspired palette) - Legacy support
const TONALITY_COLORS: Record<string, number> = {
  'Major': 0x00ff00,      // Green (safe/stable)
  'Minor': 0x0000ff,      // Blue (melancholic)
  'Modal': 0xff00ff,      // Magenta (exotic)
  'Atonal': 0xff0000,     // Red (chaotic)
  'Chromatic': 0xffff00,  // Yellow (transitional)
  'Pentatonic': 0x00ffff, // Cyan (simple)
  'Blues': 0x8800ff,      // Purple (soulful)
  'WholeTone': 0xff8800,  // Orange (dreamy)
  'Diminished': 0xff0088, // Pink (tense)
};

// ==================
// 3D Model Configuration
// ==================

// Model paths for different element types
const MODEL_PATHS: Record<string, string> = {
  // Egyptian models (NEW! - Available for all floors)
  'ankh': '/models/ankh.glb',
  'stele': '/models/stele.glb',
  'scarab': '/models/scarab.glb',
  'pyramid': '/models/pyramid.glb',
  'lotus': '/models/lotus.glb',

  // Guitar models for musical elements
  'guitar': '/models/guitar.glb',
  'guitar2': '/models/guitar2.glb',

  // Ocean environment models (Floor 2)
  'coral': '/models/ocean/coral_platform.glb',
  'seaweed': '/models/ocean/seaweed.glb',
  'fish': '/models/ocean/fish.glb',

  // Desert environment models (Floors 0-1)
  'sandstone': '/models/desert/sandstone_platform.glb',
  'dune_rock': '/models/desert/dune_rock.glb',
  'cactus': '/models/desert/cactus.glb',

  // Gem/Crystal models (Floors 3-4)
  'emerald': '/models/gems/emerald_crystal.glb',
  'ruby': '/models/gems/ruby_crystal.glb',
  'diamond': '/models/gems/diamond_platform.glb',

  // Metal models (Floor 5)
  'gold_platform': '/models/metal/gold_platform.glb',
  'brass_ornament': '/models/metal/brass_ornament.glb',

  // Fallback to simple geometries if models not available
  'box': 'primitive:box',
  'sphere': 'primitive:sphere',
  'cylinder': 'primitive:cylinder',
  'cone': 'primitive:cone',
};

// Model cache to avoid reloading
const modelCache = new Map<string, THREE.Group>();

// GLTF Loader instance (reusable)
const gltfLoader = new GLTFLoader();

/**
 * Load a 3D model from GLB/GLTF file or create primitive geometry
 */
const loadModel = async (modelKey: string): Promise<THREE.Group> => {
  // Check cache first
  if (modelCache.has(modelKey)) {
    return modelCache.get(modelKey)!.clone();
  }

  const modelPath = MODEL_PATHS[modelKey] || MODEL_PATHS['box'];

  // Handle primitive geometries
  if (modelPath.startsWith('primitive:')) {
    const primitiveType = modelPath.split(':')[1];
    const group = new THREE.Group();
    let geometry: THREE.BufferGeometry;

    switch (primitiveType) {
      case 'sphere':
        geometry = new THREE.SphereGeometry(2, 16, 16);
        break;
      case 'cylinder':
        geometry = new THREE.CylinderGeometry(1.5, 1.5, 4, 16);
        break;
      case 'cone':
        geometry = new THREE.ConeGeometry(2, 4, 16);
        break;
      case 'box':
      default:
        geometry = new THREE.BoxGeometry(4, 4, 4);
        break;
    }

    const material = new THREE.MeshStandardMaterial({
      color: 0x44ffff,
      emissive: 0x44ffff,
      emissiveIntensity: 0.6,
      metalness: 0.3,
      roughness: 0.8,
      flatShading: true,
    });

    const mesh = new THREE.Mesh(geometry, material);
    group.add(mesh);
    modelCache.set(modelKey, group);
    return group.clone();
  }

  // Load GLTF/GLB model
  return new Promise((resolve, reject) => {
    gltfLoader.load(
      modelPath,
      (gltf) => {
        const model = gltf.scene;

        // Center and scale the model
        const box = new THREE.Box3().setFromObject(model);
        const center = box.getCenter(new THREE.Vector3());
        const size = box.getSize(new THREE.Vector3());

        // Center the model
        model.position.sub(center);

        // Scale to fit in 4x4x4 box (standard element size)
        const maxDim = Math.max(size.x, size.y, size.z);
        const scale = 4 / maxDim;
        model.scale.multiplyScalar(scale);

        // Enable shadows
        model.traverse((child) => {
          if ((child as THREE.Mesh).isMesh) {
            child.castShadow = true;
            child.receiveShadow = true;
          }
        });

        // Wrap in group for easier manipulation
        const group = new THREE.Group();
        group.add(model);

        modelCache.set(modelKey, group);
        resolve(group.clone());
      },
      undefined,
      (error) => {
        console.warn(`Failed to load model ${modelPath}, using fallback:`, error);
        // Fallback to box primitive
        loadModel('box').then(resolve).catch(reject);
      }
    );
  });
};

/**
 * Create ocean water material with Gerstner waves (from Ocean component)
 * Reserved for future use
 */
// eslint-disable-next-line @typescript-eslint/no-unused-vars
const createOceanMaterial = (): THREE.ShaderMaterial => {
  const waterTint = new THREE.Color(0x0b4259);
  const sigmaRGB = [0.12, 0.06, 0.02]; // Beer absorption (m^-1)

  return new THREE.ShaderMaterial({
    uniforms: {
      time: { value: 0 },
      waterTint: { value: waterTint },
      sigmaRGB: { value: new THREE.Vector3(...sigmaRGB) },
      foamStrength: { value: 0.15 },
      baseRoughness: { value: 0.02 },
      maxRoughness: { value: 0.18 }
    },
    vertexShader: `
      uniform float time;
      varying vec3 vPosition;
      varying vec3 vNormal;
      varying vec3 vWorldPosition;

      // Gerstner wave function
      vec3 gerstnerWave(vec2 dir, float amp, float steep, float len, float speed, vec2 xz, float t) {
        float k = 6.2831853 / len;
        vec2 d = normalize(dir);
        float w = sqrt(9.81 * k);
        float ph = k * dot(d, xz) - (w + speed) * t;

        float c = cos(ph);
        float s = sin(ph);

        float Qa = steep * amp;
        float dispX = d.x * Qa * c;
        float dispZ = d.y * Qa * c;
        float dispY = amp * s;

        return vec3(dispX, dispY, dispZ);
      }

      // Gerstner wave normal
      vec3 gerstnerNormal(vec2 dir, float amp, float steep, float len, float speed, vec2 xz, float t) {
        float k = 6.2831853 / len;
        vec2 d = normalize(dir);
        float w = sqrt(9.81 * k);
        float ph = k * dot(d, xz) - (w + speed) * t;

        float c = cos(ph);
        float s = sin(ph);

        float Qa = steep * amp;
        float ddx = -d.x * d.x * Qa * s * k;
        float ddz = -d.y * d.y * Qa * s * k;
        float ddy = amp * c * k;

        return normalize(vec3(-ddx, 1.0 - ddy, -ddz));
      }

      void main() {
        vec2 xz = vec2(position.x, position.z);

        // 3-wave set: long swell + mid + chop
        vec3 disp = vec3(0.0);
        vec3 nrm = vec3(0.0, 1.0, 0.0);

        // Wave 1: Long swell
        disp += gerstnerWave(vec2(1.0, 0.2), 0.70, 0.90, 45.0, 0.0, xz, time);
        nrm += gerstnerNormal(vec2(1.0, 0.2), 0.70, 0.90, 45.0, 0.0, xz, time);

        // Wave 2: Mid
        disp += gerstnerWave(vec2(0.6, 1.0), 0.35, 0.80, 18.0, 0.0, xz, time);
        nrm += gerstnerNormal(vec2(0.6, 1.0), 0.35, 0.80, 18.0, 0.0, xz, time);

        // Wave 3: Chop
        disp += gerstnerWave(vec2(-0.8, 0.4), 0.22, 0.70, 9.0, 0.0, xz, time);
        nrm += gerstnerNormal(vec2(-0.8, 0.4), 0.22, 0.70, 9.0, 0.0, xz, time);

        nrm = normalize(nrm);

        vec3 pos = position + disp;
        vPosition = pos;
        vNormal = nrm;
        vWorldPosition = (modelMatrix * vec4(pos, 1.0)).xyz;

        gl_Position = projectionMatrix * modelViewMatrix * vec4(pos, 1.0);
      }
    `,
    fragmentShader: `
      uniform vec3 waterTint;
      uniform vec3 sigmaRGB;
      uniform float foamStrength;
      uniform float baseRoughness;
      uniform float maxRoughness;

      varying vec3 vPosition;
      varying vec3 vNormal;
      varying vec3 vWorldPosition;

      void main() {
        vec3 normal = normalize(vNormal);
        vec3 viewDir = normalize(cameraPosition - vWorldPosition);

        // Fresnel (Schlick approximation)
        float F0 = 0.02; // water ~2% reflectance
        float NoV = clamp(dot(normal, viewDir), 0.0, 1.0);
        float fresnel = F0 + (1.0 - F0) * pow(1.0 - NoV, 5.0);

        // Beer's law absorption by depth
        float depth = clamp(-vWorldPosition.y, 0.0, 100.0);
        vec3 atten = vec3(
          exp(-sigmaRGB.x * depth),
          exp(-sigmaRGB.y * depth),
          exp(-sigmaRGB.z * depth)
        );
        vec3 refrCol = waterTint * atten;

        // Slope-based foam
        vec3 up = vec3(0.0, 1.0, 0.0);
        float slope = 1.0 - clamp(dot(normal, up), 0.0, 1.0);
        float foam = pow(smoothstep(0.55, 0.85, slope), 2.0) * foamStrength;

        // Final color: mix refraction with white foam
        vec3 color = mix(refrCol, vec3(1.0), foam);

        // Add some sky reflection based on Fresnel
        vec3 skyColor = vec3(0.7, 0.85, 1.0);
        color = mix(color, skyColor, fresnel * 0.6);

        gl_FragColor = vec4(color, 1.0);
      }
    `,
    side: THREE.DoubleSide
  });
};

/**
 * Create emerald gem material (Floor 3)
 */
const createEmeraldMaterial = (): THREE.MeshPhysicalMaterial => {
  return new THREE.MeshPhysicalMaterial({
    color: 0x50C878,
    metalness: 0.0,
    roughness: 0.05,
    transmission: 0.7,
    thickness: 1.5,
    ior: 1.57, // Emerald IOR
    clearcoat: 1.0,
    clearcoatRoughness: 0.05,
    sheen: 0.5,
    sheenColor: new THREE.Color(0x90EE90),
    reflectivity: 1.0,
  });
};

/**
 * Create ruby gem material (Floor 4)
 * Reserved for future use
 */
// eslint-disable-next-line @typescript-eslint/no-unused-vars
const createRubyMaterial = (): THREE.MeshPhysicalMaterial => {
  return new THREE.MeshPhysicalMaterial({
    color: 0xE0115F,
    metalness: 0.0,
    roughness: 0.05,
    transmission: 0.6,
    thickness: 1.2,
    ior: 1.76, // Ruby IOR
    clearcoat: 1.0,
    clearcoatRoughness: 0.05,
    sheen: 0.6,
    sheenColor: new THREE.Color(0xFF6B9D),
    reflectivity: 1.0,
  });
};

/**
 * Create gold metal material (Floor 5)
 */
const createGoldMaterial = (): THREE.MeshPhysicalMaterial => {
  return new THREE.MeshPhysicalMaterial({
    color: 0xFFD700,
    metalness: 1.0,
    roughness: 0.15,
    clearcoat: 1.0,
    clearcoatRoughness: 0.1,
    reflectivity: 1.0,
    sheen: 1.0,
    sheenColor: new THREE.Color(0xFFA500),
    envMapIntensity: 1.5,
  });
};

/**
 * Create marble material with veining
 */
const createMarbleMaterial = (baseColor: number = 0xF5F5DC): THREE.MeshPhysicalMaterial => {
  return new THREE.MeshPhysicalMaterial({
    color: baseColor,
    metalness: 0.1,
    roughness: 0.3,
    clearcoat: 0.8,
    clearcoatRoughness: 0.2,
    reflectivity: 0.5,
    sheen: 0.3,
    sheenColor: new THREE.Color(0xFFFFFF),
  });
};

/**
 * Create sand dune material with procedural texture
 * Reserved for future use
 */
// eslint-disable-next-line @typescript-eslint/no-unused-vars
const createSandMaterial = (): THREE.MeshStandardMaterial => {
  return new THREE.MeshStandardMaterial({
    color: 0xC2B280, // Sand color
    roughness: 0.9,
    metalness: 0.0,
    emissive: 0x3A2A1A,
    emissiveIntensity: 0.1,
  });
};

/**
 * Create glass material for pyramid walls (transparent with reflections)
 */
const createGlassMaterial = (): THREE.MeshPhysicalMaterial => {
  return new THREE.MeshPhysicalMaterial({
    color: 0xccddff, // Light blue tint for glass
    metalness: 0.0,
    roughness: 0.05, // Very smooth for glass
    transparent: true,
    opacity: 0.25, // 25% opacity - very transparent
    transmission: 0.9, // High transmission for glass effect
    thickness: 0.5, // Glass thickness
    envMapIntensity: 1.5, // Strong reflections
    clearcoat: 1.0, // Glass coating
    clearcoatRoughness: 0.1,
    side: THREE.DoubleSide, // Render both sides
  });
};

/**
 * Create realistic stone material for pyramid walls (opaque version for armature)
 */
const createStoneMaterial = (baseColor: number = 0x8B7D6B): THREE.MeshStandardMaterial => {
  return new THREE.MeshStandardMaterial({
    color: baseColor,
    roughness: 0.95,
    metalness: 0.0,
    flatShading: false,
    // Add subtle variation
    emissive: new THREE.Color(baseColor).multiplyScalar(0.05),
    emissiveIntensity: 0.1,
  });
};

/**
 * Create realistic marble floor material with procedural texture
 * OPTIMIZED: Much faster generation
 */
const createProceduralMarbleMaterial = (): THREE.MeshStandardMaterial => {
  const canvas = document.createElement('canvas');
  const ctx = canvas.getContext('2d');
  if (!ctx) return new THREE.MeshStandardMaterial({ color: 0xE8E8E8 });

  // OPTIMIZED: Reduced from 2048×2048 to 512×512 (16× faster)
  canvas.width = 512;
  canvas.height = 512;

  // Base marble color (white/cream)
  ctx.fillStyle = '#F5F5F0';
  ctx.fillRect(0, 0, canvas.width, canvas.height);

  // OPTIMIZED: Use canvas drawing instead of per-pixel loops
  // Draw flowing vein patterns using bezier curves
  ctx.strokeStyle = 'rgba(120, 120, 130, 0.15)';
  ctx.lineWidth = 2;

  // Draw 20 flowing veins (instead of 262,144 pixel calculations)
  for (let i = 0; i < 20; i++) {
    ctx.beginPath();
    const startX = Math.random() * canvas.width;
    const startY = Math.random() * canvas.height;
    ctx.moveTo(startX, startY);

    // Create flowing bezier curve
    for (let j = 0; j < 5; j++) {
      const cp1x = startX + (Math.random() - 0.5) * 200;
      const cp1y = startY + j * 100 + (Math.random() - 0.5) * 50;
      const cp2x = startX + (Math.random() - 0.5) * 200;
      const cp2y = startY + (j + 1) * 100 + (Math.random() - 0.5) * 50;
      const endX = startX + (Math.random() - 0.5) * 150;
      const endY = startY + (j + 1) * 100;
      ctx.bezierCurveTo(cp1x, cp1y, cp2x, cp2y, endX, endY);
    }
    ctx.stroke();
  }

  // Add subtle texture variation
  ctx.fillStyle = 'rgba(240, 240, 235, 0.3)';
  for (let i = 0; i < 50; i++) {
    const x = Math.random() * canvas.width;
    const y = Math.random() * canvas.height;
    const size = Math.random() * 10 + 5;
    ctx.fillRect(x, y, size, size);
  }

  // Create texture
  const texture = new THREE.CanvasTexture(canvas);
  texture.wrapS = THREE.RepeatWrapping;
  texture.wrapT = THREE.RepeatWrapping;
  texture.repeat.set(2, 2); // Tile the marble pattern

  return new THREE.MeshStandardMaterial({
    map: texture,
    color: 0xFFFFFF,
    roughness: 0.3, // Polished marble
    metalness: 0.1,
    envMapIntensity: 0.5,
  });
};

/**
 * Create realistic paved stone floor material with procedural canvas texture
 */
const createPavedStoneMaterial = (): THREE.MeshStandardMaterial => {
  // Create canvas for procedural stone texture
  const canvas = document.createElement('canvas');
  const ctx = canvas.getContext('2d');
  if (!ctx) return new THREE.MeshStandardMaterial({ color: 0x6B6B6B });

  canvas.width = 1024;
  canvas.height = 1024;

  // Helper: Simple hash-based noise
  const hash = (x: number, y: number): number => {
    const h = Math.sin(x * 127.1 + y * 311.7) * 43758.5453;
    return h - Math.floor(h);
  };

  // Helper: Smooth noise
  const noise = (x: number, y: number): number => {
    const ix = Math.floor(x);
    const iy = Math.floor(y);
    const fx = x - ix;
    const fy = y - iy;

    const a = hash(ix, iy);
    const b = hash(ix + 1, iy);
    const c = hash(ix, iy + 1);
    const d = hash(ix + 1, iy + 1);

    const ux = fx * fx * (3 - 2 * fx);
    const uy = fy * fy * (3 - 2 * fy);

    return a * (1 - ux) * (1 - uy) + b * ux * (1 - uy) + c * (1 - ux) * uy + d * ux * uy;
  };

  // Draw paved stone pattern
  const tileSize = 128; // Size of each stone tile
  const mortarWidth = 8; // Width of mortar between stones
  const tilesX = Math.ceil(canvas.width / tileSize);
  const tilesY = Math.ceil(canvas.height / tileSize);

  for (let ty = 0; ty < tilesY; ty++) {
    for (let tx = 0; tx < tilesX; tx++) {
      const x = tx * tileSize;
      const y = ty * tileSize;

      // Stone color variation per tile
      const variation = hash(tx, ty);
      const baseGray = 90 + variation * 40; // 90-130 range
      const r = baseGray + noise(tx * 0.5, ty * 0.5) * 20;
      const g = baseGray + noise(tx * 0.7, ty * 0.3) * 20;
      const b = baseGray + noise(tx * 0.3, ty * 0.7) * 20;

      // Fill stone tile
      ctx.fillStyle = `rgb(${r}, ${g}, ${b})`;
      ctx.fillRect(x, y, tileSize - mortarWidth, tileSize - mortarWidth);

      // Add surface texture/cracks
      for (let i = 0; i < 30; i++) {
        const px = x + Math.random() * (tileSize - mortarWidth);
        const py = y + Math.random() * (tileSize - mortarWidth);
        const crackNoise = noise(px * 0.1, py * 0.1);

        if (crackNoise > 0.6) {
          ctx.fillStyle = `rgba(${r * 0.6}, ${g * 0.6}, ${b * 0.6}, 0.3)`;
          ctx.fillRect(px, py, 2, 2);
        }
      }

      // Add highlights
      for (let i = 0; i < 10; i++) {
        const px = x + Math.random() * (tileSize - mortarWidth);
        const py = y + Math.random() * (tileSize - mortarWidth);
        const highlightNoise = noise(px * 0.05, py * 0.05);

        if (highlightNoise > 0.7) {
          ctx.fillStyle = `rgba(${r * 1.3}, ${g * 1.3}, ${b * 1.3}, 0.2)`;
          ctx.fillRect(px, py, 3, 3);
        }
      }
    }
  }

  // Draw mortar (dark gray lines between stones)
  ctx.fillStyle = '#2A2A2A';
  for (let ty = 0; ty <= tilesY; ty++) {
    ctx.fillRect(0, ty * tileSize - mortarWidth / 2, canvas.width, mortarWidth);
  }
  for (let tx = 0; tx <= tilesX; tx++) {
    ctx.fillRect(tx * tileSize - mortarWidth / 2, 0, mortarWidth, canvas.height);
  }

  // Create texture from canvas
  const texture = new THREE.CanvasTexture(canvas);
  texture.wrapS = THREE.RepeatWrapping;
  texture.wrapT = THREE.RepeatWrapping;
  texture.repeat.set(4, 4); // Repeat pattern

  // Create realistic stone material
  return new THREE.MeshStandardMaterial({
    map: texture,
    color: 0x888888, // Medium gray tint
    roughness: 0.9, // Very rough stone surface
    metalness: 0.0, // Stone is not metallic
    envMapIntensity: 0.2, // Subtle reflections
  });
};

// ==================
// Component
// ==================

export const BSPDoomExplorer: React.FC<BSPDoomExplorerProps> = ({
  width = 1200,
  height = 800,
  moveSpeed = 5.0,
  lookSpeed = 0.002,
  showHUD = true,
  showMinimap = true,
  onRegionChange,
  maxElementsPerRegion = 100,
  enableLOD = true,
  enableInstancing = true,
}): React.ReactElement => {
  const containerRef = useRef<HTMLDivElement>(null);
  const canvasRef = useRef<HTMLCanvasElement>(null);
  const rendererRef = useRef<THREE.WebGLRenderer | null>(null);
  const sceneRef = useRef<THREE.Scene | null>(null);
  const cameraRef = useRef<THREE.PerspectiveCamera | null>(null);
  const animationIdRef = useRef<number | null>(null);
  const clockRef = useRef<THREE.Clock>(new THREE.Clock());
  
  // Player state - Start at Floor 0 (top of inverted pyramid - smallest, most abstract)
  const playerStateRef = useRef<PlayerState>({
    position: new THREE.Vector3(0, 18, 0), // Floor 0: Y=0 base + 18 (eye height + elevation)
    rotation: new THREE.Euler(0, 0, 0, 'YXZ'), // Look straight ahead
    velocity: new THREE.Vector3(0, 0, 0),
    currentRegion: null,
  });

  // Input state
  const keyStateRef = useRef<KeyState>({
    forward: false,
    backward: false,
    left: false,
    right: false,
    up: false,
    down: false,
  });

  const mouseMovementRef = useRef({ x: 0, y: 0 });
  const isPointerLockedRef = useRef(false);

  // Touch support for POV orientation (1 finger) and movement (2 fingers)
  const touchStartRef = useRef<{ x: number; y: number } | null>(null);
  const touchMovementRef = useRef({ x: 0, y: 0 });
  const twoFingerTouchStartRef = useRef<{ x: number; y: number } | null>(null);
  const twoFingerMovementRef = useRef({ x: 0, y: 0 });

  // Cloud layers for atmospheric effect
  const cloudLayersRef = useRef<THREE.Mesh[]>([]);

  // MCP WebSocket connection for remote control
  const mcpWebSocketRef = useRef<WebSocket | null>(null);
  const mcpObjectsRef = useRef<Map<string, THREE.Object3D>>(new Map());

  // Component state
  const [isWebGPU, setIsWebGPU] = useState(false);
  const [isLoading, setIsLoading] = useState(true);
  const [loadingProgress, setLoadingProgress] = useState(0); // 0-100
  const [loadingStatus, setLoadingStatus] = useState('Initializing...'); // Status message
  const [error, setError] = useState<string | null>(null);
  const [treeInfo, setTreeInfo] = useState<BSPTreeInfoResponse | null>(null);
  const [currentRegion, setCurrentRegion] = useState<BSPRegion | null>(null);
  const [fps, setFps] = useState(0);
  const [currentFloor, setCurrentFloor] = useState(0); // 0=Chromatic, 1=Keys, 2=Scales, 3=Modes, 4=Chords
  const [hoveredElement, setHoveredElement] = useState<ElementInfo | null>(null);
  const [selectedElement, setSelectedElement] = useState<ElementInfo | null>(null);
  const [autoNavigate, setAutoNavigate] = useState(false); // Auto-navigation demo mode
  const [scopeFilter, setScopeFilter] = useState<{floor: number; group: string} | null>(null); // Scope filtering
  const [detailPanelData, setDetailPanelData] = useState<BSPElementData | null>(null);
  const [mousePosition, setMousePosition] = useState<{ x: number; y: number }>({ x: 0, y: 0 });
  const [playerRotation, setPlayerRotation] = useState(0); // For minimap FOV cone rotation

  // Atmosphere state (for smooth transitions)
  const atmosphereStateRef = useRef({
    currentFloor: 0,
    targetFloor: 0,
    transitionProgress: 1.0, // 0-1, 1 = complete
    transitionDuration: 2000, // ms
    transitionStartTime: 0,
  });

  // Floor navigator 3D canvas ref
  const floorNavCanvasRef = useRef<HTMLCanvasElement>(null);
  const floorNavRendererRef = useRef<THREE.WebGLRenderer | null>(null);
  const floorNavSceneRef = useRef<THREE.Scene | null>(null);
  const floorNavCameraRef = useRef<THREE.OrthographicCamera | null>(null);
  const floorNavAnimationRef = useRef<number | null>(null);

  // 3D Bracelet diagram state
  const activeBraceletRef = useRef<THREE.Group | null>(null);
  const braceletFadeRef = useRef({ opacity: 0, targetOpacity: 0 });

  // BSP world geometry
  const bspWorldRef = useRef<THREE.Group | null>(null);
  const partitionPlanesRef = useRef<THREE.Mesh[]>([]);
  const bspTreeDataRef = useRef<BSPTreeStructureResponse | null>(null);
  const floorGroupsRef = useRef<THREE.Group[]>([]); // One group per floor level
  const instancedMeshesRef = useRef<Map<string, THREE.InstancedMesh>>(new Map()); // Instanced meshes for performance
  const lodGroupsRef = useRef<Map<number, THREE.LOD>>(new Map()); // LOD groups per floor
  const raycasterRef = useRef<THREE.Raycaster>(new THREE.Raycaster());
  const interactableObjectsRef = useRef<THREE.Object3D[]>([]); // All objects that can be interacted with
  const portalsRef = useRef<PortalDoor[]>([]); // Portal doors to other visualizations

  // Hierarchical navigation state (room/door system like zoomable sunburst)
  const [navigationStack, setNavigationStack] = useState<Array<{
    name: string;
    floor: number;
    group?: string;
    parent?: string;
  }>>([{ name: 'Root', floor: 0 }]);
  const [currentRoom, setCurrentRoom] = useState<string>('Root');
  const hoverDetailCardRef = useRef<THREE.Sprite | null>(null); // 3D detail card shown on hover
  const skyRef = useRef<Sky | null>(null); // Sky object for parallax effect

  // ==================
  // 3D Bracelet Diagram Helpers
  // ==================

  /**
   * Create a 3D door object that represents navigation to a sub-room
   * Similar to segments in a zoomable sunburst diagram
   * @param doorName - Name of the door/category
   * @param position - Position in 3D space
   * @param color - Door color
   * @param targetFloor - Target floor when entering this door
   * @param children - Child elements accessible through this door
   * @returns THREE.Group containing the door visualization
   */
  const create3DDoor = useCallback((
    doorName: string,
    position: THREE.Vector3,
    color: number = 0x00ff00,
    targetFloor?: number,
    children?: string[]
  ): THREE.Group => {
    const group = new THREE.Group();
    group.position.copy(position);

    // Create door frame (archway)
    const frameGeometry = new THREE.BoxGeometry(6, 8, 0.5);
    const frameMaterial = new THREE.MeshPhysicalMaterial({
      color: color,
      emissive: color,
      emissiveIntensity: 0.5,
      metalness: 0.8,
      roughness: 0.2,
      clearcoat: 1.0,
      clearcoatRoughness: 0.1,
    });
    const frame = new THREE.Mesh(frameGeometry, frameMaterial);
    group.add(frame);

    // Create door panel (slightly inset)
    const panelGeometry = new THREE.BoxGeometry(5, 7, 0.3);
    const panelMaterial = new THREE.MeshPhysicalMaterial({
      color: new THREE.Color(color).multiplyScalar(0.6),
      emissive: color,
      emissiveIntensity: 0.3,
      metalness: 0.5,
      roughness: 0.4,
      transparent: true,
      opacity: 0.8,
    });
    const panel = new THREE.Mesh(panelGeometry, panelMaterial);
    panel.position.z = -0.1;
    group.add(panel);

    // Add glowing border effect
    const borderGeometry = new THREE.EdgesGeometry(frameGeometry);
    const borderMaterial = new THREE.LineBasicMaterial({
      color: color,
      linewidth: 2,
    });
    const border = new THREE.LineSegments(borderGeometry, borderMaterial);
    group.add(border);

    // Add door label
    const label = createTextLabel(
      doorName,
      new THREE.Vector3(0, 5, 0.5),
      color,
      0.8
    );
    if (label) group.add(label);

    // Add child count indicator if there are children
    if (children && children.length > 0) {
      const countLabel = createTextLabel(
        `${children.length} items`,
        new THREE.Vector3(0, -4, 0.5),
        0xffffff,
        0.5
      );
      if (countLabel) group.add(countLabel);
    }

    // Store metadata
    group.userData = {
      type: 'door',
      name: doorName,
      targetFloor: targetFloor,
      children: children,
      isInteractable: true,
    };

    return group;
  }, []);

  /**
   * Create a 3D bracelet diagram mesh for a pitch class set
   * @param pitchClasses - Array of pitch classes (0-11) that are active
   * @param radius - Radius of the bracelet circle
   * @param position - Position in 3D space
   * @returns THREE.Group containing the bracelet visualization
   */
  const create3DBraceletDiagram = useCallback((
    pitchClasses: number[],
    radius: number = 2,
    position: THREE.Vector3 = new THREE.Vector3(0, 0, 0)
  ): THREE.Group => {
    const group = new THREE.Group();
    group.position.copy(position);

    // Create outer ring (clock face)
    const ringGeometry = new THREE.TorusGeometry(radius, 0.05, 16, 32);
    const ringMaterial = new THREE.MeshPhysicalMaterial({
      color: 0x00ff00,
      emissive: 0x00ff00,
      emissiveIntensity: 0.3,
      metalness: 0.8,
      roughness: 0.2,
    });
    const ring = new THREE.Mesh(ringGeometry, ringMaterial);
    ring.rotation.x = Math.PI / 2; // Lay flat
    group.add(ring);

    // Create 12 pitch class positions (like clock hours)
    const pitchClassNames = ['0', '1', '2', '3', '4', '5', '6', '7', '8', '9', 'T', 'E'];
    const pitchClassSet = new Set(pitchClasses);

    for (let i = 0; i < 12; i++) {
      const angle = (i * Math.PI * 2) / 12 - Math.PI / 2; // Start at top (12 o'clock)
      const x = Math.cos(angle) * radius;
      const z = Math.sin(angle) * radius;

      const isActive = pitchClassSet.has(i);

      // Create pitch class sphere
      const sphereGeometry = new THREE.SphereGeometry(isActive ? 0.15 : 0.08, 16, 16);
      const sphereMaterial = new THREE.MeshPhysicalMaterial({
        color: isActive ? 0x00ff00 : 0x333333,
        emissive: isActive ? 0x00ff00 : 0x000000,
        emissiveIntensity: isActive ? 0.6 : 0,
        metalness: 0.5,
        roughness: 0.3,
      });
      const sphere = new THREE.Mesh(sphereGeometry, sphereMaterial);
      sphere.position.set(x, 0, z);
      group.add(sphere);

      // Add text label (pitch class name)
      const canvas = document.createElement('canvas');
      const context = canvas.getContext('2d')!;
      canvas.width = 64;
      canvas.height = 64;
      context.fillStyle = isActive ? '#0f0' : '#666';
      context.font = 'bold 32px monospace';
      context.textAlign = 'center';
      context.textBaseline = 'middle';
      context.fillText(pitchClassNames[i], 32, 32);

      const texture = new THREE.CanvasTexture(canvas);
      const spriteMaterial = new THREE.SpriteMaterial({ map: texture, transparent: true });
      const sprite = new THREE.Sprite(spriteMaterial);
      sprite.position.set(x * 1.3, 0, z * 1.3); // Outside the ring
      sprite.scale.set(0.4, 0.4, 0.4);
      group.add(sprite);
    }

    // Add connecting lines between active pitch classes
    if (pitchClasses.length > 1) {
      const sortedPitches = [...pitchClasses].sort((a, b) => a - b);
      const points: THREE.Vector3[] = [];

      for (const pc of sortedPitches) {
        const angle = (pc * Math.PI * 2) / 12 - Math.PI / 2;
        const x = Math.cos(angle) * radius;
        const z = Math.sin(angle) * radius;
        points.push(new THREE.Vector3(x, 0, z));
      }

      // Close the polygon
      points.push(points[0].clone());

      const lineGeometry = new THREE.BufferGeometry().setFromPoints(points);
      const lineMaterial = new THREE.LineBasicMaterial({
        color: 0x00ff00,
        opacity: 0.5,
        transparent: true,
        linewidth: 2,
      });
      const line = new THREE.Line(lineGeometry, lineMaterial);
      group.add(line);
    }

    // Add rotation animation userData
    group.userData = {
      type: 'bracelet',
      rotationSpeed: 0.5,
    };

    return group;
  }, []);

  // ==================
  // Tonal Atmosphere Helpers
  // ==================

  // NOTE: These functions are reserved for future use (dynamic material coloring)
  // Temporarily commented out to avoid linting errors
  /*
  const encodeMusicalColor = useCallback((
    tonalFamily: string,
    consonance: number = 0.7,
    complexity: number = 0.5
  ): THREE.Color => {
    const baseHue = TONAL_FAMILY_HUES[tonalFamily] || 0.5;
    const lightness = 0.3 + consonance * 0.5;
    const saturation = 0.4 + complexity * 0.6;
    return new THREE.Color().setHSL(baseHue, saturation, lightness);
  }, []);

  const calculateChordTension = useCallback((pitchClasses: number[]): number => {
    if (!pitchClasses || pitchClasses.length === 0) return 0.5;
    const uniquePitches = new Set(pitchClasses);
    const chromaticDensity = uniquePitches.size / 12;
    let dissonanceScore = 0;
    for (let i = 0; i < pitchClasses.length; i++) {
      for (let j = i + 1; j < pitchClasses.length; j++) {
        const interval = Math.abs(pitchClasses[i] - pitchClasses[j]) % 12;
        if (interval === 1 || interval === 6) dissonanceScore += 1;
      }
    }
    return Math.min(1, (chromaticDensity + dissonanceScore / 10));
  }, []);

  const createRoughnessMap = useCallback((): THREE.Texture => {
    const canvas = document.createElement('canvas');
    canvas.width = 256;
    canvas.height = 256;
    const ctx = canvas.getContext('2d')!;
    for (let x = 0; x < canvas.width; x++) {
      for (let y = 0; y < canvas.height; y++) {
        const noise = 0.6 + Math.random() * 0.4;
        const gray = Math.floor(noise * 255);
        ctx.fillStyle = `rgb(${gray}, ${gray}, ${gray})`;
        ctx.fillRect(x, y, 1, 1);
      }
    }
    const texture = new THREE.CanvasTexture(canvas);
    texture.wrapS = THREE.RepeatWrapping;
    texture.wrapT = THREE.RepeatWrapping;
    texture.repeat.set(4, 4);
    return texture;
  }, []);
  */

  /**
   * Smooth floor atmosphere transition
   * Lerps fog color, skybox, and ambient light
   */
  // eslint-disable-next-line @typescript-eslint/no-unused-vars
  const updateAtmosphereTransition = useCallback((delta: number) => {
    const state = atmosphereStateRef.current;

    if (state.transitionProgress >= 1.0) return; // No transition in progress

    const elapsed = Date.now() - state.transitionStartTime;
    state.transitionProgress = Math.min(1.0, elapsed / state.transitionDuration);

    // Ease in-out cubic
    const t = state.transitionProgress < 0.5
      ? 4 * state.transitionProgress * state.transitionProgress * state.transitionProgress
      : 1 - Math.pow(-2 * state.transitionProgress + 2, 3) / 2;

    const fromAtmosphere = FLOOR_ATMOSPHERES[state.currentFloor];
    const toAtmosphere = FLOOR_ATMOSPHERES[state.targetFloor];

    if (!sceneRef.current) return;

    // Lerp fog color
    if (sceneRef.current.fog instanceof THREE.FogExp2) {
      const fogColor = new THREE.Color().lerpColors(
        fromAtmosphere.fogColor,
        toAtmosphere.fogColor,
        t
      );
      sceneRef.current.fog.color.copy(fogColor);
    }

    // Update skybox colors (if skybox material exists)
    sceneRef.current.traverse((obj) => {
      if (obj.userData && obj.userData.type === 'skybox') {
        const material = (obj as THREE.Mesh).material as THREE.ShaderMaterial;
        if (material && material.uniforms) {
          material.uniforms.topColor.value.lerpColors(
            fromAtmosphere.skyTop,
            toAtmosphere.skyTop,
            t
          );
          material.uniforms.horizonColor.value.lerpColors(
            fromAtmosphere.skyHorizon,
            toAtmosphere.skyHorizon,
            t
          );
          material.uniforms.bottomColor.value.lerpColors(
            fromAtmosphere.skyBottom,
            toAtmosphere.skyBottom,
            t
          );
        }
      }
    });

    // Mark transition complete
    if (state.transitionProgress >= 1.0) {
      state.currentFloor = state.targetFloor;
    }
  }, []);

  /**
   * Trigger floor atmosphere transition
   */
  const transitionToFloor = useCallback((targetFloor: number) => {
    const state = atmosphereStateRef.current;
    state.targetFloor = Math.max(0, Math.min(FLOOR_ATMOSPHERES.length - 1, targetFloor));
    state.transitionProgress = 0.0;
    state.transitionStartTime = Date.now();

    if (import.meta.env.DEV) {
      console.log(`🌈 Atmosphere transition: Floor ${state.currentFloor} → ${state.targetFloor}`);
    }
  }, []);

  // ==================
  // Initialization
  // ==================

  useEffect(() => {
    let isMounted = true;

    const init = async () => {
      if (!canvasRef.current) return;

      try {
        // Step 1: Fetch BSP tree structure (10%)
        console.log('🔄 Step 1: Fetching BSP tree structure...');
        setLoadingStatus('Fetching BSP tree structure...');
        setLoadingProgress(10);

        try {
          const treeStructure = await BSPApiService.getTreeStructure();
          if (isMounted) {
            bspTreeDataRef.current = treeStructure;
            setTreeInfo({
              rootRegion: 'Root',
              totalRegions: 50,
              maxDepth: treeStructure.maxDepth,
              partitionStrategies: ['Harmonic', 'Melodic'],
              supportedOperations: ['Navigate', 'Filter', 'Analyze'],
            });
            console.log('✅ BSPDoomExplorer: Loaded full BSP tree from API', treeStructure);
          }
        } catch {
          // Gracefully fall back to demo mode - API is optional
          // This is expected behavior when backend is not running
          console.log('ℹ️ BSPDoomExplorer: Using demo mode (backend API not available)');
          // Use enhanced demo BSP tree data with 6-floor harmonic equivalence hierarchy
          if (isMounted) {
            setTreeInfo({
              rootRegion: 'Demo Root',
              totalRegions: 50,
              maxDepth: 5, // 6 floors (0-5)
              partitionStrategies: ['Harmonic', 'Melodic'],
              supportedOperations: ['Navigate', 'Filter', 'Analyze'],
            });
          }
        }

        console.log('✅ Step 1 complete');

        if (!isMounted) return;

        // Step 2: Initialize scene (20%)
        console.log('🔄 Step 2: Initializing 3D scene...');
        setLoadingStatus('Initializing 3D scene...');
        setLoadingProgress(20);

        // Initialize Three.js scene with blue sky
        const scene = new THREE.Scene();
        // Sky will be added below - no solid background color needed
        scene.fog = new THREE.FogExp2(0xb8d4e8, 0.00015); // Light blue fog for distance

        // Create enhanced environment map for realistic metal reflections
        // This creates a gradient cube map with colored lights for interesting reflections
        const pmremGenerator = new THREE.PMREMGenerator(new THREE.WebGLRenderer());
        const envScene = new THREE.Scene();
        envScene.background = new THREE.Color(0x1a1410); // Dark background

        // Add gradient sphere for environment reflections
        const envGeometry = new THREE.SphereGeometry(500, 32, 32);
        const envMaterial = new THREE.MeshBasicMaterial({
          color: 0x2a1a10,
          side: THREE.BackSide,
        });
        const envSphere = new THREE.Mesh(envGeometry, envMaterial);
        envScene.add(envSphere);

        // Add colored lights to the environment for interesting reflections
        const envLight1 = new THREE.PointLight(0xff9955, 2, 200);
        envLight1.position.set(0, 100, 0);
        envScene.add(envLight1);

        const envLight2 = new THREE.PointLight(0x88ccff, 1.5, 200);
        envLight2.position.set(100, 50, 100);
        envScene.add(envLight2);

        const envLight3 = new THREE.PointLight(0x55ffcc, 1.5, 200);
        envLight3.position.set(-100, 50, -100);
        envScene.add(envLight3);

        const envMap = pmremGenerator.fromScene(envScene).texture;
        scene.environment = envMap;
        pmremGenerator.dispose();

        sceneRef.current = scene;

        // Add Physical Sky as parallax skybox
        const sky = new Sky();
        sky.scale.setScalar(4500);
        scene.add(sky);
        skyRef.current = sky; // Store reference for parallax updates

        const skyUniforms = sky.material.uniforms;
        skyUniforms['turbidity'].value = 2.2;   // dust/haze (1-20)
        skyUniforms['rayleigh'].value = 2.8;    // molecular scattering (0-4)
        skyUniforms['mieCoefficient'].value = 0.006; // aerosol density (0-0.02)
        skyUniforms['mieDirectionalG'].value = 0.8;  // forward scatter (0-1)

        // Sun position (desert afternoon)
        const sunPhi = THREE.MathUtils.degToRad(85);   // elevation angle
        const sunTheta = THREE.MathUtils.degToRad(25); // azimuth
        const sunDir = new THREE.Vector3().setFromSphericalCoords(1, Math.PI / 2 - sunPhi, sunTheta);
        skyUniforms['sunPosition'].value.copy(sunDir.clone().multiplyScalar(600));

        // Set scene background to sky color to eliminate black areas
        scene.background = new THREE.Color(0x87CEEB); // Sky blue background

        // Add realistic sand dunes terrain with advanced shader
        const dunesGeometry = new THREE.PlaneGeometry(3000, 3000, 512, 512);
        dunesGeometry.rotateX(-Math.PI / 2); // Make it horizontal

        // Apply procedural height to create realistic dunes
        const positions = dunesGeometry.attributes.position;
        const vertex = new THREE.Vector3();

        // Noise functions for terrain generation
        const noise2D = (x: number, y: number) => {
          const X = Math.floor(x) & 255;
          const Y = Math.floor(y) & 255;
          const xf = x - Math.floor(x);
          const yf = y - Math.floor(y);
          const u = xf * xf * (3.0 - 2.0 * xf);
          const v = yf * yf * (3.0 - 2.0 * yf);
          const a = Math.sin(X * 12.9898 + Y * 78.233) * 43758.5453;
          const b = Math.sin((X + 1) * 12.9898 + Y * 78.233) * 43758.5453;
          const c = Math.sin(X * 12.9898 + (Y + 1) * 78.233) * 43758.5453;
          const d = Math.sin((X + 1) * 12.9898 + (Y + 1) * 78.233) * 43758.5453;
          const aa = a - Math.floor(a);
          const bb = b - Math.floor(b);
          const cc = c - Math.floor(c);
          const dd = d - Math.floor(d);
          return aa * (1 - u) * (1 - v) + bb * u * (1 - v) + cc * (1 - u) * v + dd * u * v;
        };

        const fbm = (x: number, y: number, octaves: number) => {
          let value = 0;
          let amplitude = 1;
          let frequency = 1;
          for (let i = 0; i < octaves; i++) {
            value += noise2D(x * frequency, y * frequency) * amplitude;
            frequency *= 2;
            amplitude *= 0.5;
          }
          return value;
        };

        // Apply height based on noise
        for (let i = 0; i < positions.count; i++) {
          vertex.fromBufferAttribute(positions, i);
          const x = vertex.x;
          const z = vertex.z;

          // Large scale dunes
          const largeDunes = fbm(x * 0.0008, z * 0.0008, 4) * 40;
          // Medium scale variation
          const mediumDunes = fbm(x * 0.002, z * 0.002, 3) * 15;
          // Small ripples
          const ripples = fbm(x * 0.01, z * 0.01, 2) * 2;

          const height = largeDunes + mediumDunes + ripples;
          positions.setY(i, height - 200); // Much lower below pyramid (200 units down)
        }

        positions.needsUpdate = true;
        dunesGeometry.computeVertexNormals();

        // Realistic sand material with proper lighting
        const dunesMaterial = new THREE.MeshStandardMaterial({
          color: 0xD2B48C, // Tan/sand color
          roughness: 0.95,
          metalness: 0.0,
          flatShading: false, // Smooth shading for realistic dunes
        });

        const dunesMesh = new THREE.Mesh(dunesGeometry, dunesMaterial);
        dunesMesh.receiveShadow = true;
        dunesMesh.castShadow = false;
        scene.add(dunesMesh);

        // Initialize camera (first-person)
        const camera = new THREE.PerspectiveCamera(
          75, // FOV (DOOM-like wide FOV)
          width / height,
          0.1,
          5000 // Increased far plane to see sky and distant dunes
        );
        camera.position.copy(playerStateRef.current.position);
        cameraRef.current = camera;

        // Step 3: Initialize renderer (40%)
        setLoadingStatus('Initializing renderer...');
        setLoadingProgress(40);

        // Initialize renderer (WebGL only)
        const canvas = canvasRef.current;
        const renderer = new THREE.WebGLRenderer({
          canvas,
          antialias: true,
          alpha: false,
        });
        setIsWebGPU(false);

        if (!isMounted) {
          renderer.dispose();
          return;
        }

        renderer.setSize(width, height);
        renderer.setPixelRatio(window.devicePixelRatio);

        // Enable VSM shadows for soft, realistic shadows
        if (!isWebGPU && renderer instanceof THREE.WebGLRenderer) {
          renderer.shadowMap.enabled = true;
          renderer.shadowMap.type = THREE.VSMShadowMap;
        }

        // Enable ACES tone mapping for unified brightness and believable glow
        renderer.toneMapping = THREE.ACESFilmicToneMapping;
        renderer.toneMappingExposure = 1.0; // Slightly lower for more dramatic atmosphere

        rendererRef.current = renderer;

        // Add drifting cloud layers for atmospheric effect
        const makeCloudLayer = async (y: number, scale = 400, speed = 0.002): Promise<THREE.Mesh> => {
          // Create procedural cloud texture (simple noise-based)
          const canvas = document.createElement('canvas');
          canvas.width = 512;
          canvas.height = 512;
          const ctx = canvas.getContext('2d');

          if (ctx) {
            // Base white
            ctx.fillStyle = '#ffffff';
            ctx.fillRect(0, 0, canvas.width, canvas.height);

            // Add cloud-like noise patterns
            for (let i = 0; i < 100; i++) {
              const x = Math.random() * canvas.width;
              const y = Math.random() * canvas.height;
              const radius = 20 + Math.random() * 60;
              const opacity = 0.1 + Math.random() * 0.3;

              const gradient = ctx.createRadialGradient(x, y, 0, x, y, radius);
              gradient.addColorStop(0, `rgba(255, 255, 255, ${opacity})`);
              gradient.addColorStop(1, 'rgba(255, 255, 255, 0)');

              ctx.fillStyle = gradient;
              ctx.fillRect(0, 0, canvas.width, canvas.height);
            }
          }

          const texture = new THREE.CanvasTexture(canvas);
          texture.wrapS = texture.wrapT = THREE.RepeatWrapping;
          texture.repeat.set(3, 3);

          const mat = new THREE.MeshBasicMaterial({
            map: texture,
            transparent: true,
            depthWrite: false, // avoid sorting artifacts with transparent layers
            opacity: 0.7,
          });

          const mesh = new THREE.Mesh(
            new THREE.PlaneGeometry(scale, scale, 1, 1),
            mat
          );
          mesh.rotation.x = -Math.PI / 2;
          mesh.position.y = y;

          // Store speed for animation
          mesh.userData.cloudSpeed = speed;

          return mesh;
        };

        // Create two cloud layers at different heights
        try {
          const [cloud1, cloud2] = await Promise.all([
            makeCloudLayer(120, 800, 0.004),
            makeCloudLayer(150, 1000, 0.002)
          ]);

          cloud2.material.opacity = 0.6; // subtle depth
          scene.add(cloud1, cloud2);
          cloudLayersRef.current = [cloud1, cloud2];

          if (import.meta.env.DEV) {
            console.log('✅ Added drifting cloud layers');
          }
        } catch (error) {
          console.warn('Failed to create cloud layers:', error);
        }

        // Step 4: Build BSP world geometry (60%)
        setLoadingStatus('Building BSP world...');
        setLoadingProgress(60);

        await buildBSPWorld(scene);

        // Add portals to other visualizations
        addPortals(scene);

        // Step 5: Setup lighting (80%)
        setLoadingStatus('Setting up lighting...');
        setLoadingProgress(80);

        // Enhanced lighting system for metallic materials
        // Warm, alchemical atmosphere with better metal reflections

        // Ambient light - bright and neutral to reduce shadows
        const ambientLight = new THREE.AmbientLight(0xFFFFFF, 0.8);
        scene.add(ambientLight);

        // Main directional light - simulates sunlight/skylight for metal reflections
        const mainLight = new THREE.DirectionalLight(0xffeedd, 0.6);
        mainLight.position.set(10, 30, 10);
        mainLight.castShadow = false; // Disable shadows to prevent black spots on floor
        scene.add(mainLight);

        // Key light - warm orange (torchlight from above)
        const torchLight = new THREE.PointLight(0xff9955, 2.5, 100);
        torchLight.position.set(0, 25, 0);
        torchLight.castShadow = false; // Disable shadows to prevent black spots on floor
        scene.add(torchLight);

        // Fill lights - positioned around the space for better metal highlights
        const fillLight1 = new THREE.PointLight(0xffbb66, 1.8, 60);
        fillLight1.position.set(-25, 18, -25);
        scene.add(fillLight1);

        const fillLight2 = new THREE.PointLight(0xffbb66, 1.8, 60);
        fillLight2.position.set(25, 18, -25);
        scene.add(fillLight2);

        const fillLight3 = new THREE.PointLight(0xffbb66, 1.8, 60);
        fillLight3.position.set(-25, 18, 25);
        scene.add(fillLight3);

        const fillLight4 = new THREE.PointLight(0xffbb66, 1.8, 60);
        fillLight4.position.set(25, 18, 25);
        scene.add(fillLight4);

        // Rim light - cool accent for metal edges
        const rimLight = new THREE.PointLight(0x88ccff, 1.0, 40);
        rimLight.position.set(0, 10, -30);
        scene.add(rimLight);

        // Mystical accent light (alchemical glow from below) - enhanced
        const alchemyGlow = new THREE.PointLight(0x55ffcc, 1.2, 35);
        alchemyGlow.position.set(0, -5, 0);
        scene.add(alchemyGlow);

        // Step 6: Add starfield background (90%)
        setLoadingStatus('Creating starfield...');
        setLoadingProgress(90);

        // Add starfield background for depth
        const starGeometry = new THREE.BufferGeometry();
        const starCount = 1000;
        const starPositions = new Float32Array(starCount * 3);
        const starColors = new Float32Array(starCount * 3);

        for (let i = 0; i < starCount; i++) {
          const i3 = i * 3;
          // Random position in a large sphere
          const radius = 200 + Math.random() * 100;
          const theta = Math.random() * Math.PI * 2;
          const phi = Math.random() * Math.PI;

          starPositions[i3] = radius * Math.sin(phi) * Math.cos(theta);
          starPositions[i3 + 1] = radius * Math.sin(phi) * Math.sin(theta);
          starPositions[i3 + 2] = radius * Math.cos(phi);

          // Random star colors (white to blue)
          const colorVariation = 0.7 + Math.random() * 0.3;
          starColors[i3] = colorVariation;
          starColors[i3 + 1] = colorVariation;
          starColors[i3 + 2] = 1.0;
        }

        starGeometry.setAttribute('position', new THREE.BufferAttribute(starPositions, 3));
        starGeometry.setAttribute('color', new THREE.BufferAttribute(starColors, 3));

        const starMaterial = new THREE.PointsMaterial({
          size: 0.5,
          vertexColors: true,
          transparent: true,
          opacity: 0.8,
          sizeAttenuation: true,
        });

        const stars = new THREE.Points(starGeometry, starMaterial);
        scene.add(stars);

        // No skybox - clean black space for pyramid focus

        // Step 7: Finalize (100%)
        setLoadingStatus('Ready!');
        setLoadingProgress(100);

        // Small delay to show 100% before hiding loading screen
        await new Promise(resolve => setTimeout(resolve, 300));

        setIsLoading(false);

        // Start animation loop
        startAnimationLoop();

        // Setup MCP WebSocket connection for remote control
        setupMCPWebSocket();

      } catch (err) {
        console.error('BSPDoomExplorer initialization error:', err);
        if (isMounted) {
          setError(err instanceof Error ? err.message : 'Unknown error');
          setIsLoading(false);
        }
      }
    };

    init();

    return () => {
      isMounted = false;
      cleanup();
      // Close MCP WebSocket
      if (mcpWebSocketRef.current) {
        mcpWebSocketRef.current.close();
        mcpWebSocketRef.current = null;
      }
    };
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [width, height]);

  // ==================
  // Portal System
  // ==================

  const addPortals = useCallback((scene: THREE.Scene) => {
    // Clear existing portals
    portalsRef.current.forEach(portal => {
      portal.dispose();
      scene.remove(portal.mesh);
    });
    portalsRef.current = [];

    // Portal to Sunburst3D (SMALLER and less obtrusive - positioned at edge of world)
    const sunburstPortal = new PortalDoor({
      position: new THREE.Vector3(40, 3, 0),
      rotation: new THREE.Euler(0, -Math.PI / 2, 0),
      scale: 0.6, // REDUCED from 1.5 to 0.6
      color: 0x00ffff,
      targetUrl: '/test/sunburst-3d',
      label: 'SUNBURST 3D',
      glowIntensity: 0.2, // REDUCED from 0.7 to 0.2
    });
    scene.add(sunburstPortal.mesh);
    portalsRef.current.push(sunburstPortal);
    interactableObjectsRef.current.push(sunburstPortal.mesh);

    // Portal to Immersive Musical World (SMALLER and less obtrusive - positioned opposite side)
    const immersivePortal = new PortalDoor({
      position: new THREE.Vector3(-40, 3, 0),
      rotation: new THREE.Euler(0, Math.PI / 2, 0),
      scale: 0.6, // REDUCED from 1.5 to 0.6
      color: 0xff00ff,
      targetUrl: '/test/immersive-musical-world',
      label: 'IMMERSIVE WORLD',
      glowIntensity: 0.2, // REDUCED from 0.7 to 0.2
    });
    scene.add(immersivePortal.mesh);
    portalsRef.current.push(immersivePortal);
    interactableObjectsRef.current.push(immersivePortal.mesh);

    console.log('✅ Added portals to scene:', portalsRef.current.length);
  }, []);

  // ==================
  // Build BSP World
  // ==================

  const buildBSPWorld = async (scene: THREE.Scene) => {
    const bspWorld = new THREE.Group();
    bspWorldRef.current = bspWorld;
    scene.add(bspWorld);

    // Create ground plane with improved material
    const groundGeometry = new THREE.PlaneGeometry(100, 100, 50, 50); // Segmented for detail
    const groundMaterial = new THREE.MeshStandardMaterial({
      color: 0x1a1410, // Dark stone/obsidian
      roughness: 0.85,
      metalness: 0.15, // Slight metallic sheen
      emissive: 0x0a0805,
      emissiveIntensity: 0.1,
      flatShading: false, // Smooth for ground
    });
    const ground = new THREE.Mesh(groundGeometry, groundMaterial);
    ground.rotation.x = -Math.PI / 2;
    ground.receiveShadow = true;
    bspWorld.add(ground);

    // Build from real BSP tree data if available, otherwise use demo
    if (bspTreeDataRef.current) {
      if (import.meta.env.DEV) {
        console.log('✅ Building BSP world from real API data');
      }
      await buildRealBSPWorld(bspWorld, bspTreeDataRef.current);
    } else {
      if (import.meta.env.DEV) {
        console.log('⚠️ Building demo BSP world with multi-floor hierarchy');
      }
      await buildDemoBSPWorld(bspWorld);
    }
  };

  const buildDemoBSPWorld = async (parent: THREE.Group) => {
    // Create 6-floor hierarchical structure based on HARMONIC EQUIVALENCE GROUPS
    // From https://harmoniousapp.net/p/ec/Equivalence-Groups
    //
    // ATONAL FOUNDATION → TONAL INTERPRETATION
    // Floors 0-2: Pure atonal space (pitch class sets, mathematical)
    // Floors 3-5: Tonal interpretation (named chords, key context, physical voicings)
    //
    // Key + Tonal Center are OPTIONAL filters applied to the atonal foundation
    floorGroupsRef.current = [];

    // Update progress
    setLoadingStatus('Building floor hierarchy...');

    // COMPREHENSIVE MUSICAL DATA for each floor with GROUP STRUCTURE
    const floorData = [
      // Floor 0: OPTIC/K - Set Classes (~115 rows, grouped by complementarity)
      {
        name: 'Set Classes (OPTIC/K)',
        groups: [
          {
            name: 'Trichords',
            color: 0x8844ff,
            elements: ['3-1', '3-2 (phrygian)', '3-3 (major 2nd)', '3-4 (minor 3rd)', '3-5 (4th)', '3-6 (tritone)', '3-7 (5th)', '3-8 (minor 6th)', '3-9 (major 6th)', '3-10 (dim)', '3-11 (maj/min)', '3-12 (aug)']
          },
          {
            name: 'Tetrachords',
            color: 0x9955ff,
            elements: ['4-1', '4-2', '4-4', '4-5', '4-7', '4-8', '4-11', '4-12', '4-13', '4-14', '4-16', '4-17', '4-18', '4-19 (maj7)', '4-20 (m7b5)', '4-23 (quartal)', '4-26 (m7)', '4-27 (dom7)', '4-28 (dim7)']
          },
          {
            name: 'Pentachords',
            color: 0xaa66ff,
            elements: ['5-1', '5-2', '5-4', '5-6', '5-7', '5-9', '5-10', '5-11', '5-Z12 (blues)', '5-23 (pent)', '5-35 (pent)']
          },
          {
            name: 'Hexachords',
            color: 0xbb77ff,
            elements: ['6-1', '6-2', '6-7', '6-8', '6-9', '6-14', '6-15', '6-18', '6-20 (hex)', '6-27', '6-30', '6-32 (hex)', '6-35 (whole tone)', '6-Z44']
          },
          {
            name: 'Septachords',
            color: 0xcc88ff,
            elements: ['7-1', '7-2', '7-4', '7-6', '7-7', '7-10', '7-11', '7-14', '7-15', '7-19', '7-21', '7-22', '7-24', '7-27', '7-31', '7-32', '7-34', '7-35 (diatonic)']
          },
          {
            name: 'Octachords',
            color: 0xdd99ff,
            elements: ['8-1', '8-2', '8-4', '8-5', '8-6', '8-9', '8-11', '8-12', '8-13', '8-16', '8-17', '8-19', '8-20', '8-21', '8-23', '8-24', '8-26', '8-27', '8-28 (octatonic)']
          }
        ],
        color: 0x8844ff,
        description: 'OPTIC/K Equivalence - ~115 Set Class Rows'
      },

      // Floor 1: OPTIC - Forte Codes (~200+ items, accounting for involution)
      {
        name: 'Forte Codes (OPTIC)',
        groups: [
          {
            name: 'Major Family',
            color: 0x44ff88,
            elements: ['3-11 (maj)', '4-19 (maj7)', '4-20 (maj6)', '5-35 (maj pent)', '7-35 (major scale)']
          },
          {
            name: 'Minor Family',
            color: 0x55ff99,
            elements: ['3-11 (min)', '4-26 (m7)', '4-23 (m6)', '5-35 (min pent)', '7-32 (harm min)', '7-34 (mel min)']
          },
          {
            name: 'Dominant Family',
            color: 0x66ffaa,
            elements: ['4-27 (dom7)', '5-27 (dom9)', '6-27 (dom13)', '4-28 (dim7)', '4-20 (m7b5)']
          },
          {
            name: 'Augmented',
            color: 0x77ffbb,
            elements: ['3-12 (aug)', '6-20 (aug hex)', '9-12 (aug scale)']
          },
          {
            name: 'Diminished',
            color: 0x88ffcc,
            elements: ['3-10 (dim)', '4-28 (dim7)', '8-28 (octatonic)']
          },
          {
            name: 'Whole Tone',
            color: 0x99ffdd,
            elements: ['2-2', '4-21', '6-35 (whole tone)', '6-Z44']
          },
          {
            name: 'Pentatonic',
            color: 0xaaffee,
            elements: ['5-35 (major pent)', '5-35 (minor pent)', '5-23', '5-33', '5-34']
          },
          {
            name: 'Blues',
            color: 0xbbffff,
            elements: ['5-Z12 (blues)', '6-Z13 (blues hex)', '7-Z12']
          }
        ],
        color: 0x44ff88,
        description: 'OPTIC Equivalence - ~200 Forte Codes'
      },

      // Floor 2: OPTC - Prime Forms (~350+ items, ignoring involution)
      {
        name: 'Prime Forms (OPTC)',
        groups: [
          {
            name: 'Triads',
            color: 0xff8844,
            elements: ['[0,4,7] maj', '[0,3,7] min', '[0,4,8] aug', '[0,3,6] dim', '[0,2,7] sus2', '[0,5,7] sus4']
          },
          {
            name: 'Seventh Chords',
            color: 0xff9955,
            elements: ['[0,4,7,11] maj7', '[0,3,7,10] m7', '[0,4,7,10] dom7', '[0,3,6,9] dim7', '[0,3,6,10] m7b5', '[0,4,8,11] aug7']
          },
          {
            name: 'Extended Chords',
            color: 0xffaa66,
            elements: ['[0,4,7,9] 6', '[0,3,7,9] m6', '[0,2,4,7,9] 6/9', '[0,2,4,7,11] maj9', '[0,2,3,7,10] m9', '[0,2,4,7,10] dom9', '[0,2,4,5,7,11] maj11', '[0,2,4,7,9,11] maj13']
          },
          {
            name: 'Altered Chords',
            color: 0xffbb77,
            elements: ['[0,1,4,7,10] 7b9', '[0,3,4,7,10] 7#9', '[0,1,4,6,10] 7alt', '[0,3,4,6,10] 7#5b9', '[0,1,6,7,10] 7b5b9']
          },
          {
            name: 'Add & Sus',
            color: 0xffcc88,
            elements: ['[0,2,4,7] add9', '[0,4,7,14] add2', '[0,4,6,7] add#11', '[0,2,7] sus2', '[0,5,7] sus4', '[0,5,7,10] 7sus4']
          },
          {
            name: 'Scales',
            color: 0xffdd99,
            elements: ['[0,2,4,5,7,9,11] major', '[0,2,3,5,7,8,10] nat min', '[0,2,3,5,7,8,11] harm min', '[0,2,3,5,7,9,11] mel min']
          },
          {
            name: 'Modes',
            color: 0xffeeaa,
            elements: ['[0,2,4,5,7,9,11] ionian', '[0,2,3,5,7,9,10] dorian', '[0,1,3,5,7,8,10] phrygian', '[0,2,4,6,7,9,11] lydian', '[0,2,4,5,7,9,10] mixolydian', '[0,2,3,5,7,8,10] aeolian', '[0,1,3,5,6,8,10] locrian']
          }
        ],
        color: 0xff8844,
        description: 'OPTC Equivalence - ~350 Prime Forms'
      },

      // Floor 3: OPC - Pitch Class Sets (~4,096 colored clocks)
      {
        name: 'Pitch Class Sets (OPC)',
        groups: [
          {
            name: 'C Family',
            color: 0x44ffff,
            elements: ['C', 'Cmaj7', 'C6', 'Cmaj9', 'Cm', 'Cm7', 'Cm6', 'Cm9', 'C7', 'C9', 'Cdim', 'Caug', 'Csus2', 'Csus4']
          },
          {
            name: 'D Family',
            color: 0x55ffff,
            elements: ['D', 'Dmaj7', 'D6', 'Dmaj9', 'Dm', 'Dm7', 'Dm6', 'Dm9', 'D7', 'D9']
          },
          {
            name: 'E Family',
            color: 0x66ffff,
            elements: ['E', 'Emaj7', 'E6', 'Emaj9', 'Em', 'Em7', 'Em6', 'Em9', 'E7', 'E9']
          },
          {
            name: 'F Family',
            color: 0x77ffff,
            elements: ['F', 'Fmaj7', 'F6', 'Fmaj9', 'Fm', 'Fm7', 'Fm6', 'Fm9', 'F7', 'F9']
          },
          {
            name: 'G Family',
            color: 0x88ffff,
            elements: ['G', 'Gmaj7', 'G6', 'Gmaj9', 'Gm', 'Gm7', 'Gm6', 'Gm9', 'G7', 'G9']
          },
          {
            name: 'A Family',
            color: 0x99ffff,
            elements: ['A', 'Amaj7', 'A6', 'Amaj9', 'Am', 'Am7', 'Am6', 'Am9', 'A7', 'A9']
          },
          {
            name: 'B Family',
            color: 0xaaffff,
            elements: ['B', 'Bmaj7', 'B6', 'Bmaj9', 'Bm', 'Bm7', 'Bm6', 'Bm9', 'B7', 'B9']
          },
          {
            name: 'Accidentals',
            color: 0xbbffff,
            elements: ['Db', 'Dbmaj7', 'Eb', 'Ebmaj7', 'Gb', 'Gbmaj7', 'Ab', 'Abmaj7', 'Bb', 'Bbmaj7']
          }
        ],
        color: 0x44ffff,
        description: 'OPC Equivalence - ~4,096 Pitch Class Sets'
      },

      // Floor 4: OC - Chord Inversions/Modes (~10,000s items)
      {
        name: 'Inversions (OC)',
        groups: [
          {
            name: 'Triad Inversions',
            color: 0xff4488,
            elements: ['C/C (root)', 'C/E (1st)', 'C/G (2nd)', 'Cm/C (root)', 'Cm/Eb (1st)', 'Cm/G (2nd)']
          },
          {
            name: '7th Inversions',
            color: 0xff5599,
            elements: ['C7/C (root)', 'C7/E (1st)', 'C7/G (2nd)', 'C7/Bb (3rd)', 'Cmaj7/C', 'Cmaj7/E', 'Cmaj7/G', 'Cmaj7/B', 'Cm7/C', 'Cm7/Eb', 'Cm7/G', 'Cm7/Bb']
          },
          {
            name: 'Slash Chords',
            color: 0xff66aa,
            elements: ['C/D', 'C/F', 'C/A', 'C/B', 'Dm/C', 'Em/C', 'F/C', 'G/C', 'Am/C']
          },
          {
            name: 'Major Modes',
            color: 0xff77bb,
            elements: ['C Ionian', 'D Dorian', 'E Phrygian', 'F Lydian', 'G Mixolydian', 'A Aeolian', 'B Locrian']
          },
          {
            name: 'Melodic Minor Modes',
            color: 0xff88cc,
            elements: ['C Mel Min', 'D Dorian b2', 'Eb Lydian Aug', 'F Lydian Dom', 'G Mixo b6', 'A Locrian #2', 'B Altered']
          },
          {
            name: 'Harmonic Minor Modes',
            color: 0xff99dd,
            elements: ['C Harm Min', 'D Locrian #6', 'Eb Ionian #5', 'F Dorian #4', 'G Phrygian Dom', 'Ab Lydian #2', 'B Altered bb7']
          }
        ],
        color: 0xff4488,
        description: 'OC Equivalence - ~10,000s Inversions & Modes'
      },

      // Floor 5: Octave - Voicings (~100,000s items)
      {
        name: 'Voicings (Octave)',
        groups: [
          {
            name: 'CAGED System',
            color: 0xffff44,
            elements: ['C-shape', 'A-shape', 'G-shape', 'E-shape', 'D-shape']
          },
          {
            name: 'Drop Voicings',
            color: 0xffff55,
            elements: ['Drop 2', 'Drop 3', 'Drop 2+4', 'Drop 2+3', 'Close Position']
          },
          {
            name: 'Spread Voicings',
            color: 0xffff66,
            elements: ['Spread Triad', 'Spread 7th', 'Quartal', 'Quintal', 'Cluster']
          },
          {
            name: 'Jazz Voicings',
            color: 0xffff77,
            elements: ['Shell', 'Rootless', '3-Note', '4-Note', 'Polychord']
          },
          {
            name: 'Position',
            color: 0xffff88,
            elements: ['Open', 'Closed', 'Low', 'Mid', 'High', 'Soprano', 'Alto', 'Tenor', 'Bass']
          },
          {
            name: 'Register',
            color: 0xffff99,
            elements: ['C2-C3', 'C3-C4', 'C4-C5 (middle)', 'C5-C6', 'C6-C7', 'C7-C8']
          },
          {
            name: 'String Sets',
            color: 0xffffaa,
            elements: ['Strings 1-3', 'Strings 2-4', 'Strings 3-5', 'Strings 4-6', 'Strings 1-4', 'Strings 2-5', 'All 6']
          },
          {
            name: 'Fret Position',
            color: 0xffffbb,
            elements: ['Open', 'Frets 1-3', 'Frets 3-5', 'Frets 5-7', 'Frets 7-9', 'Frets 9-12', 'Frets 12+']
          },
          {
            name: 'Style',
            color: 0xffffcc,
            elements: ['Classical', 'Jazz', 'Blues', 'Rock', 'Folk', 'Country', 'Metal', 'Funk']
          }
        ],
        color: 0xffff44,
        description: 'Octave Equivalence - ~100,000s Physical Voicings'
      },
    ];

    // Fluffy grass creation function for grass floors (currently unused - reserved for future use)
    // eslint-disable-next-line @typescript-eslint/no-unused-vars, @typescript-eslint/no-explicit-any
    const createFluffyGrass = (parent: THREE.Group, floorIndex: number, _atmosphere: any) => {
      const grassDensity = 600; // Instances per chunk (INCREASED for denser grass)
      const chunkSize = 10;
      const chunkCount = 15; // 15x15 grid = 225 chunks (INCREASED for larger floor)
      const grassHeight = 0.8;
      const grassWidth = 0.08;
      const planeCount = 6; // 6 intersecting planes per blade

      // Grass colors based on floor
      const grassColorSets = [
        { base: new THREE.Color(0x1a4d2e), tip: new THREE.Color(0x4f9d69), tip2: new THREE.Color(0x5fa573) }, // Floor 3
        { base: new THREE.Color(0x1e5a3e), tip: new THREE.Color(0x5a9d6a), tip2: new THREE.Color(0x6fb583) }, // Floor 4
        { base: new THREE.Color(0x2a6b4e), tip: new THREE.Color(0x6bab7b), tip2: new THREE.Color(0x7fc593) }, // Floor 5
      ];
      const colors = grassColorSets[floorIndex - 3] || grassColorSets[0];

      // Create grass shader material
      const grassMaterial = new THREE.ShaderMaterial({
        uniforms: {
          time: { value: 0 },
          baseColor: { value: colors.base },
          tipColor: { value: colors.tip },
          tipColor2: { value: colors.tip2 },
          windSpeed: { value: 2.0 }, // INCREASED for faster wind
          windStrength: { value: 0.6 }, // INCREASED for more visible wind effect
        },
        vertexShader: `
          uniform float time;
          uniform float windSpeed;
          uniform float windStrength;

          varying vec2 vUv;
          varying vec3 vNormal;
          varying vec3 vWorldPos;

          vec2 hash2(vec2 p) {
            p = vec2(dot(p, vec2(127.1, 311.7)), dot(p, vec2(269.5, 183.3)));
            return -1.0 + 2.0 * fract(sin(p) * 43758.5453123);
          }

          float noise(vec2 p) {
            vec2 i = floor(p);
            vec2 f = fract(p);
            vec2 u = f * f * (3.0 - 2.0 * f);

            return mix(
              mix(dot(hash2(i + vec2(0.0, 0.0)), f - vec2(0.0, 0.0)),
                  dot(hash2(i + vec2(1.0, 0.0)), f - vec2(1.0, 0.0)), u.x),
              mix(dot(hash2(i + vec2(0.0, 1.0)), f - vec2(0.0, 1.0)),
                  dot(hash2(i + vec2(1.0, 1.0)), f - vec2(1.0, 1.0)), u.x),
              u.y);
          }

          void main() {
            vUv = uv;
            vNormal = normalize(normalMatrix * normal);

            vec4 worldPos = modelMatrix * instanceMatrix * vec4(position, 1.0);
            vWorldPos = worldPos.xyz;

            vec3 pos = position;

            float windEffect = uv.y * uv.y;
            float windBase = sin(time * windSpeed + worldPos.x * 0.3 + worldPos.z * 0.3) * 0.5 + 0.5;
            vec2 windUV = vec2(worldPos.x * 0.1 + time * 0.05, worldPos.z * 0.1 + time * 0.03);
            float windNoise = noise(windUV) * 0.5 + 0.5;
            float wind = (windBase * 0.6 + windNoise * 0.4) * windStrength;

            pos.x += wind * windEffect;
            pos.z += wind * windEffect * 0.7;
            float windRotation = wind * windEffect * 0.1;
            pos.x += sin(windRotation) * 0.1;

            gl_Position = projectionMatrix * viewMatrix * vec4(vWorldPos + pos - position, 1.0);
          }
        `,
        fragmentShader: `
          uniform vec3 baseColor;
          uniform vec3 tipColor;
          uniform vec3 tipColor2;

          varying vec2 vUv;
          varying vec3 vNormal;
          varying vec3 vWorldPos;

          vec2 hash2(vec2 p) {
            p = vec2(dot(p, vec2(127.1, 311.7)), dot(p, vec2(269.5, 183.3)));
            return -1.0 + 2.0 * fract(sin(p) * 43758.5453123);
          }

          float noise(vec2 p) {
            vec2 i = floor(p);
            vec2 f = fract(p);
            vec2 u = f * f * (3.0 - 2.0 * f);

            return mix(
              mix(dot(hash2(i + vec2(0.0, 0.0)), f - vec2(0.0, 0.0)),
                  dot(hash2(i + vec2(1.0, 0.0)), f - vec2(1.0, 0.0)), u.x),
              mix(dot(hash2(i + vec2(0.0, 1.0)), f - vec2(0.0, 1.0)),
                  dot(hash2(i + vec2(1.0, 1.0)), f - vec2(1.0, 1.0)), u.x),
              u.y);
          }

          void main() {
            float ao = pow(vUv.y, 1.5);
            vec2 colorUV = vWorldPos.xz * 0.5;
            float colorNoise = noise(colorUV) * 0.5 + 0.5;
            vec3 finalTipColor = mix(tipColor, tipColor2, colorNoise);
            vec3 color = mix(baseColor, finalTipColor, ao);
            float variation = noise(vWorldPos.xz * 2.0) * 0.05;
            color += variation;

            vec3 lightDir = normalize(vec3(0.5, 1.0, 0.3));
            float diff = max(dot(vNormal, lightDir), 0.0);
            diff = diff * 0.5 + 0.5;
            color *= diff;

            float alpha = 1.0;
            float edgeFade = smoothstep(0.0, 0.15, vUv.x) * smoothstep(1.0, 0.85, vUv.x);
            float topFade = smoothstep(1.0, 0.7, vUv.y);
            alpha = edgeFade * (1.0 - topFade * 0.3);

            if (alpha < 0.1) discard;

            gl_FragColor = vec4(color, alpha);
          }
        `,
        side: THREE.DoubleSide,
        transparent: true,
        depthWrite: false,
        depthTest: true,
      });

      // Create grass chunks
      const halfCount = chunkCount / 2;
      for (let cx = 0; cx < chunkCount; cx++) {
        for (let cz = 0; cz < chunkCount; cz++) {
          const chunkX = (cx - halfCount) * chunkSize;
          const chunkZ = (cz - halfCount) * chunkSize;

          for (let planeIndex = 0; planeIndex < planeCount; planeIndex++) {
            const angle = (Math.PI / planeCount) * planeIndex;
            const geometry = new THREE.PlaneGeometry(grassWidth, grassHeight, 1, 4);
            const instancedMesh = new THREE.InstancedMesh(geometry, grassMaterial, grassDensity);
            instancedMesh.castShadow = true;
            instancedMesh.receiveShadow = false;

            const dummy = new THREE.Object3D();
            for (let i = 0; i < grassDensity; i++) {
              const x = chunkX + Math.random() * chunkSize;
              const z = chunkZ + Math.random() * chunkSize;
              const y = 0; // Flat floor

              dummy.position.set(x, y + grassHeight / 2, z);
              dummy.rotation.y = angle + Math.random() * 0.2;
              dummy.scale.set(
                0.8 + Math.random() * 0.4,
                0.8 + Math.random() * 0.4,
                1
              );
              dummy.updateMatrix();
              instancedMesh.setMatrixAt(i, dummy.matrix);
            }

            instancedMesh.instanceMatrix.needsUpdate = true;
            parent.add(instancedMesh);
          }
        }
      }
    };

    // LOD SYSTEM: Use Level of Detail to handle massive scale
    // LOD 0 (Far): Show only group labels (6-10 groups per floor)
    // LOD 1 (Medium): Show group regions with simplified geometry
    // LOD 2 (Close): Show individual elements with full detail
    // Reserved for future LOD implementation
    /* const LOD_DISTANCES = {
      GROUP_LABELS: 100,  // Beyond this, show only group labels
      GROUP_REGIONS: 50,  // Beyond this, show group regions
      FULL_DETAIL: 0      // Within this, show all individual elements
    }; */

    // PYRAMID STRUCTURE: Calculate floor sizes based on ACTUAL element counts
    // Count actual elements from floorData
    const floorElementCounts = floorData.map(floor => {
      return floor.groups.reduce((total, group) => total + group.elements.length, 0);
    });

    // Log actual counts for verification
    if (import.meta.env.DEV) {
      console.log('📊 Actual floor element counts:', floorElementCounts);
      floorData.forEach((floor, i) => {
        console.log(`  Floor ${i} (${floor.name}): ${floorElementCounts[i]} items`);
      });
    }

    // Calculate pyramid scaling factors (logarithmic scale for better visualization)
    const minSize = 50;  // Smallest floor size
    const maxSize = 200; // Largest floor size
    const logMin = Math.log10(floorElementCounts[0]);
    const logMax = Math.log10(floorElementCounts[5]);

    const calculateFloorSize = (elementCount: number): number => {
      const logCount = Math.log10(elementCount);
      const normalized = (logCount - logMin) / (logMax - logMin);
      return minSize + normalized * (maxSize - minSize);
    };

    for (let i = 0; i < floorData.length; i++) {
      const floor = floorData[i];

      // Update progress for each floor (60% to 75%)
      const floorProgress = 60 + (i / floorData.length) * 15;
      setLoadingStatus(`Building ${floor.name}...`);
      setLoadingProgress(Math.round(floorProgress));

      const floorGroup = new THREE.Group();
      floorGroup.position.y = i * 20; // 20 units between floors (matches camera spacing)
      floorGroup.visible = i === 0; // PERFORMANCE: Only show first floor initially
      parent.add(floorGroup);
      floorGroupsRef.current.push(floorGroup);

      // PYRAMID STRUCTURE: Calculate floor size based on element count
      const floorSize = calculateFloorSize(floorElementCounts[i]);

      // Add floor plane - FULL SIZE to consume all real estate
      // EXCEPTION: Ocean floor (Floor 2) needs higher resolution for Gerstner waves
      const floorSegments = i === 2 ? 100 : 32; // Higher resolution for better texture detail
      const floorGeometry = new THREE.PlaneGeometry(floorSize, floorSize, floorSegments, floorSegments);

      // ALL FLOORS: Use realistic marble material
      const floorMaterial = createProceduralMarbleMaterial();
      const floorPlane = new THREE.Mesh(floorGeometry, floorMaterial);
      floorPlane.rotation.x = -Math.PI / 2;
      floorPlane.receiveShadow = false; // Disable shadows to prevent black spots
      floorPlane.castShadow = false;
      floorGroup.add(floorPlane);

      // Add floor grid helper with THIN, SUBTLE lines (PYRAMID-SIZED)
      // Reduced divisions to prevent moiré effect
      const gridColor1 = 0x555555; // Medium gray
      const gridColor2 = 0x333333; // Dark gray

      const gridDivisions = Math.max(8, Math.floor(floorSize / 10)); // Fewer divisions to prevent moiré
      const floorGrid = new THREE.GridHelper(floorSize, gridDivisions, gridColor1, gridColor2);
      floorGrid.position.y = 0.02; // Slightly higher to prevent z-fighting

      // Make grid lines subtle but visible
      const gridMaterial = floorGrid.material as THREE.LineBasicMaterial;
      if (gridMaterial) {
        gridMaterial.opacity = 0.15; // More transparent to reduce moiré
        gridMaterial.transparent = true;
        gridMaterial.depthWrite = false; // Prevent depth conflicts
      }

      floorGroup.add(floorGrid);

      // Add ceiling grid (from floor above) for spatial context (PYRAMID-SIZED)
      if (i < floorData.length - 1) {
        const nextFloorSize = calculateFloorSize(floorElementCounts[i + 1]);
        const nextFloorIsAtonal = (i + 1) <= 2;
        const ceilingColor1 = nextFloorIsAtonal ? 0x6A5A4A : 0x2A4A2A; // Sand: brown, Grass: dark green
        const ceilingColor2 = nextFloorIsAtonal ? 0x4A3A2A : 0x1A3A1A; // Sand: darker brown, Grass: darker green
        const ceilingDivisions = Math.max(10, Math.floor(nextFloorSize / 5));
        const ceilingGrid = new THREE.GridHelper(nextFloorSize, ceilingDivisions, ceilingColor1, ceilingColor2);
        ceilingGrid.position.y = 19.99; // Just below next floor (20 units spacing)
        ceilingGrid.rotation.x = Math.PI; // Flip upside down for ceiling

        // Make ceiling grid lines thinner and more subtle
        const ceilingMaterial = ceilingGrid.material as THREE.LineBasicMaterial;
        if (ceilingMaterial) {
          ceilingMaterial.opacity = 0.2; // Even more transparent for ceiling
          ceilingMaterial.transparent = true;
          ceilingMaterial.linewidth = 0.5;
        }

        floorGroup.add(ceilingGrid);
      }

      // Add PROMINENT floor label on the pyramid wall (not floating in air)
      // Color code: Desert/Ocean (floors 0-2) = cyan, Forest/Gems (floors 3-5) = yellow
      const labelColor = i <= 2 ? 0x00ffff : 0xffff00;
      const wallZ = -(floorSize / 2 - 1); // Position on back wall, slightly inside

      const floorLabel = createTextLabel(
        `${floor.name.toUpperCase()}`,
        new THREE.Vector3(0, -8, wallZ), // On back wall, mid-height
        labelColor,
        4 // LARGER for better focus
      );
      if (floorLabel) floorGroup.add(floorLabel);

      // Add floor description subtitle on wall
      const floorDesc = createTextLabel(
        floor.description,
        new THREE.Vector3(0, -12, wallZ), // Below main label
        i <= 2 ? 0x88ffff : 0xffff88,
        1.2
      );
      if (floorDesc) floorGroup.add(floorDesc);

      // Add floor number on wall
      const floorNumber = createTextLabel(
        `Floor ${i}`,
        new THREE.Vector3(0, -15, wallZ), // Below description
        0x888888,
        1
      );
      if (floorNumber) floorGroup.add(floorNumber);

      // GROUP-BASED LOD RENDERING with SCOPE FILTERING
      // If scope filter is active and matches this floor, show only that group's elements
      // Otherwise, show all groups as clickable platforms
      const groups = floor.groups || [];
      const totalElements = groups.reduce((sum, g) => sum + g.elements.length, 0);

      // Check if scope filter is active for this floor
      const isFiltered = scopeFilter && scopeFilter.floor === i;
      const activeGroup = isFiltered ? groups.find(g => g.name === scopeFilter.group) : null;

      if (isFiltered && activeGroup) {
        // EXPANDED VIEW: Show individual elements from the selected group
        const elements = activeGroup.elements;

        // Create breadcrumb label
        const breadcrumbText = `${floor.name} > ${activeGroup.name}`;
        const breadcrumbLabel = createTextLabel(
          breadcrumbText,
          new THREE.Vector3(0, 3, -30),
          0x00ff00,
          1.5
        );
        if (breadcrumbLabel) floorGroup.add(breadcrumbLabel);

        // Show element count
        const countText = `${elements.length} elements`;
        const countLabel = createTextLabel(
          countText,
          new THREE.Vector3(0, 1, -30),
          0xffffff,
          1
        );
        if (countLabel) floorGroup.add(countLabel);

        // FLOOR 0: Special bookshelf library layout for Set Classes
        if (i === 0) {
          // Create a circular library with bookshelves organized by cardinality
          // Each group (Trichords, Tetrachords, etc.) gets its own bookshelf section

          const libraryRadius = floorSize * 0.35; // Circular arrangement

          // Process each cardinality group
          floor.groups.forEach((group, groupIdx) => {
            const groupElements = group.elements;
            const groupColor = group.color || 0x8844ff;

            // Calculate angle for this group's bookshelf
            const anglePerGroup = (Math.PI * 2) / floor.groups.length;
            const groupAngle = groupIdx * anglePerGroup;

            // Position bookshelf facing inward toward center
            const shelfX = Math.cos(groupAngle) * libraryRadius;
            const shelfZ = Math.sin(groupAngle) * libraryRadius;

            // Create bookshelf structure
            const shelfWidth = 15;
            const shelfHeight = 8;
            const shelfDepth = 2;

            // Bookshelf back panel
            const backGeometry = new THREE.BoxGeometry(shelfWidth, shelfHeight, 0.2);
            const backMaterial = new THREE.MeshStandardMaterial({
              color: 0x3a2a1a,
              roughness: 0.8,
              metalness: 0.1,
            });
            const backPanel = new THREE.Mesh(backGeometry, backMaterial);
            backPanel.position.set(shelfX, shelfHeight / 2, shelfZ);
            backPanel.rotation.y = groupAngle + Math.PI; // Face inward
            floorGroup.add(backPanel);

            // Add shelves (horizontal dividers)
            const numShelves = 4;
            for (let s = 0; s <= numShelves; s++) {
              const shelfY = (s / numShelves) * shelfHeight;
              const shelfGeometry = new THREE.BoxGeometry(shelfWidth, 0.3, shelfDepth);
              const shelfMaterial = new THREE.MeshStandardMaterial({
                color: 0x4a3a2a,
                roughness: 0.7,
                metalness: 0.2,
              });
              const shelf = new THREE.Mesh(shelfGeometry, shelfMaterial);
              const shelfOffsetZ = Math.cos(groupAngle) * (shelfDepth / 2);
              const shelfOffsetX = Math.sin(groupAngle) * (shelfDepth / 2);
              shelf.position.set(shelfX - shelfOffsetX, shelfY, shelfZ + shelfOffsetZ);
              shelf.rotation.y = groupAngle + Math.PI;
              floorGroup.add(shelf);
            }

            // Add group label above bookshelf
            const groupLabel = createTextLabel(
              group.name,
              new THREE.Vector3(shelfX, shelfHeight + 1, shelfZ),
              groupColor,
              1.2
            );
            if (groupLabel) floorGroup.add(groupLabel);

            // Place crystals (books) on shelves
            const itemsPerShelf = Math.ceil(groupElements.length / numShelves);

            groupElements.forEach((elementName, elemIdx) => {
              const shelfIndex = Math.floor(elemIdx / itemsPerShelf);
              const positionOnShelf = elemIdx % itemsPerShelf;

              // Calculate position on shelf
              const shelfY = (shelfIndex / numShelves) * shelfHeight + 0.5;
              const itemSpacing = shelfWidth / (itemsPerShelf + 1);
              const itemX = -shelfWidth / 2 + (positionOnShelf + 1) * itemSpacing;

              // Extract cardinality
              const cardinality = parseInt(elementName.split('-')[0]) || 3;

              // Create crystal "book" on shelf
              const crystalHeight = 0.8 + cardinality * 0.15;
              const crystalWidth = 0.4;
              const crystalDepth = 0.6;

              // Book-like box geometry
              const geometry = new THREE.BoxGeometry(crystalWidth, crystalHeight, crystalDepth);

              // Create glowing crystal material
              const material = new THREE.MeshPhysicalMaterial({
                color: groupColor,
                emissive: groupColor,
                emissiveIntensity: 0.5,
                metalness: 0.2,
                roughness: 0.3,
                transparent: true,
                opacity: 0.9,
                clearcoat: 1.0,
                clearcoatRoughness: 0.1,
                transmission: 0.2,
                ior: 2.0,
              });

              const crystal = new THREE.Mesh(geometry, material);

              // Position on shelf (rotate to face outward)
              const worldX = shelfX + Math.cos(groupAngle + Math.PI) * itemX;
              const worldZ = shelfZ + Math.sin(groupAngle + Math.PI) * itemX;
              const offsetZ = Math.cos(groupAngle) * (shelfDepth * 0.3);
              const offsetX = Math.sin(groupAngle) * (shelfDepth * 0.3);

              crystal.position.set(worldX - offsetX, shelfY, worldZ + offsetZ);
              crystal.rotation.y = groupAngle + Math.PI;
              crystal.castShadow = false;
              crystal.receiveShadow = false;
              crystal.userData = {
                type: 'element',
                name: elementName,
                tonalityType: floor.name,
                groupName: group.name,
                depth: i,
                cardinality: cardinality,
              };
              floorGroup.add(crystal);

              // Add small label on crystal
              const labelOffset = Math.cos(groupAngle) * 0.5;
              const labelOffsetX = Math.sin(groupAngle) * 0.5;
              const itemLabel = createTextLabel(
                elementName,
                new THREE.Vector3(worldX - offsetX - labelOffsetX, shelfY, worldZ + offsetZ + labelOffset),
                0xffffff,
                0.3
              );
              if (itemLabel) {
                itemLabel.rotation.y = groupAngle + Math.PI;
                floorGroup.add(itemLabel);
              }
            });
          });

        } else {
          // OTHER FLOORS: Use grid layout
          const elementsPerRow = Math.ceil(Math.sqrt(elements.length));
          const usableFloorSize = floorSize * 0.8;
          const spacing = elementsPerRow > 1 ? usableFloorSize / (elementsPerRow - 1) : usableFloorSize;
          const startX = -(elementsPerRow - 1) * spacing / 2;
          const startZ = -(elementsPerRow - 1) * spacing / 2;

          elements.forEach((elementName, idx) => {
            const row = Math.floor(idx / elementsPerRow);
            const col = idx % elementsPerRow;
            const x = startX + col * spacing;
            const z = startZ + row * spacing;

            // Use existing tree/cube approach for other floors
            const useTree = idx % 3 === 0;

            if (useTree) {
              const treeHeight = 4 + Math.random() * 2;
              const tree = createTree(new THREE.Vector3(x, 0, z), treeHeight, 3);
              tree.userData = {
                type: 'element',
                name: elementName,
                tonalityType: floor.name,
                groupName: activeGroup.name,
                depth: i,
                isTree: true,
              };
              floorGroup.add(tree);
            } else {
              const geometry = new THREE.BoxGeometry(2.5, 2.5, 2.5, 4, 4, 4);
              const woodVariation = Math.random();
              const material = createWoodMaterial(0x8B4513, woodVariation);

              const mesh = new THREE.Mesh(geometry, material);
              mesh.position.set(x, 1.25, z);
              mesh.castShadow = true;
              mesh.receiveShadow = true;
              mesh.userData = {
                type: 'element',
                name: elementName,
                tonalityType: floor.name,
                groupName: activeGroup.name,
                depth: i,
              };
              floorGroup.add(mesh);
            }

            // Add element label
            const label = createTextLabel(
              elementName,
              new THREE.Vector3(x, 6, z),
              0xffffff,
              0.7
            );
            if (label) floorGroup.add(label);
          });
        }

      } else {
        // GROUP VIEW: Show all groups as clickable platforms
        const summaryText = `${groups.length} groups | ${totalElements} items`;
        const summaryLabel = createTextLabel(
          summaryText,
          new THREE.Vector3(0, 3, -30),
          0xffffff,
          2
        );
        if (summaryLabel) floorGroup.add(summaryLabel);

        // FLOOR 0: Special circular arrangement of curved stone steles (monuments)
        if (i === 0) {
          // Create curved stone steles arranged in a circle, one per cardinality group
          const steleRadius = floorSize * 0.35;

          floor.groups.forEach((group, groupIdx) => {
            const groupElements = group.elements;
            const groupColor = group.color || 0x8844ff;

            // Calculate angular span for this stele with spacing
            const totalGroups = floor.groups.length;
            const spacingAngle = Math.PI / 180 * 5; // 5 degrees spacing between steles
            const availableAngle = (Math.PI * 2) - (totalGroups * spacingAngle);
            const anglePerStele = availableAngle / totalGroups;

            const startAngle = groupIdx * (anglePerStele + spacingAngle);
            const centerAngle = startAngle + anglePerStele / 2;

            // Calculate stele height based on number of items in this group
            const lineHeight = 0.8;
            const headerSpace = 3; // Space for title
            const itemCount = groupElements.length;
            const steleHeight = headerSpace + (itemCount * lineHeight) + 1; // Dynamic height per group

            const steleThickness = 0.5;
            const curveRadius = steleRadius;

            // Create curved stele using CylinderGeometry segment
            const steleGeometry = new THREE.CylinderGeometry(
              curveRadius + steleThickness / 2,
              curveRadius + steleThickness / 2,
              steleHeight,
              32, // segments for smooth curve
              1,
              false,
              startAngle,
              anglePerStele
            );

            // Stone material - weathered ancient stone
            const steleMaterial = new THREE.MeshStandardMaterial({
              color: 0x8a7f6f, // Weathered stone color
              roughness: 0.9,
              metalness: 0.0,
              flatShading: false,
            });

            const stele = new THREE.Mesh(steleGeometry, steleMaterial);
            stele.position.set(0, steleHeight / 2, 0);

            stele.userData = {
              type: 'stele',
              groupName: group.name,
            };
            floorGroup.add(stele);

            // Add group title at top of stele (engraved on curved surface)
            const titleX = Math.cos(centerAngle) * curveRadius;
            const titleZ = Math.sin(centerAngle) * curveRadius;
            const titleLabel = createTextLabel(
              group.name,
              new THREE.Vector3(titleX, steleHeight - 1.5, titleZ),
              groupColor,
              0.8
            );
            if (titleLabel) {
              // Text follows the curve naturally - no rotation compensation
              titleLabel.rotation.y = centerAngle - Math.PI / 2;
              floorGroup.add(titleLabel);
            }

            // Engrave each item name vertically on the curved stele with crystal details
            const startY = steleHeight - headerSpace;

            groupElements.forEach((elementName, elemIdx) => {
              const textY = startY - elemIdx * lineHeight;

              // Position text on the curved surface at center of this stele
              const textX = Math.cos(centerAngle) * curveRadius;
              const textZ = Math.sin(centerAngle) * curveRadius;

              // Extract cardinality for crystal size
              const cardinality = parseInt(elementName.split('-')[0]) || 3;

              // Determine if this is a diatonic item for special styling
              const setNumber = parseInt(elementName.split('-')[1] || '1', 10) || 1;
              const isDiatonicItem = setNumber >= 4 && setNumber <= 6;

              // Create small crystal detail embedded in the stone
              const crystalSize = 0.18 + cardinality * 0.03; // Larger crystals
              const crystalGeometry = new THREE.OctahedronGeometry(crystalSize);

              // Special golden color for diatonic items
              const crystalColor = isDiatonicItem ? 0xd4af37 : groupColor; // Gold vs group color

              const crystalMaterial = new THREE.MeshPhysicalMaterial({
                color: crystalColor,
                emissive: crystalColor,
                emissiveIntensity: isDiatonicItem ? 0.7 : 0.5, // Brighter for diatonic
                metalness: isDiatonicItem ? 0.4 : 0.2, // More metallic for diatonic
                roughness: 0.3,
                transparent: true,
                opacity: 0.85,
                clearcoat: 1.0,
                clearcoatRoughness: 0.1,
                transmission: 0.15,
                ior: 2.0,
              });

              const crystal = new THREE.Mesh(crystalGeometry, crystalMaterial);

              // Position crystal on the curved surface, slightly offset from text
              const crystalAngle = centerAngle - 0.05; // Slightly to the left
              const crystalX = Math.cos(crystalAngle) * (curveRadius + 0.1);
              const crystalZ = Math.sin(crystalAngle) * (curveRadius + 0.1);
              crystal.position.set(crystalX, textY, crystalZ);
              crystal.rotation.y = crystalAngle - Math.PI / 2;

              crystal.userData = {
                type: 'element',
                name: elementName,
                tonalityType: floor.name,
                groupName: group.name,
                depth: i,
                cardinality: cardinality,
              };
              floorGroup.add(crystal);

              // Create detailed engraved text with contextual information
              // Format: "3-1 | 3-note | Trichord | Chromatic"
              const forteNumber = elementName;

              // Generate contextual details based on Forte number
              let detailText = `${forteNumber} | ${cardinality}-note`;

              // Add interval vector representation (simplified)
              if (cardinality === 3) {
                detailText += ` | Trichord`;
              } else if (cardinality === 4) {
                detailText += ` | Tetrachord`;
              } else if (cardinality === 5) {
                detailText += ` | Pentachord`;
              } else if (cardinality === 6) {
                detailText += ` | Hexachord`;
              } else if (cardinality === 7) {
                detailText += ` | Septachord`;
              } else if (cardinality === 8) {
                detailText += ` | Octachord`;
              }

              // Add prime form hint (example patterns)
              // Reuse setNumber from above
              let characterType = '';
              let isDiatonic = false;

              if (setNumber === 1) {
                characterType = 'Chromatic';
              } else if (setNumber <= 3) {
                characterType = 'Compact';
              } else if (setNumber <= 6) {
                characterType = 'Diatonic';
                isDiatonic = true;
              } else {
                characterType = 'Distributed';
              }

              detailText += ` | ${characterType}`;

              // Special styling for diatonic items - golden color
              const textColor = isDiatonic ? 0xd4af37 : 0x4a4035; // Gold vs dark brown

              const engravedLabel = createTextLabel(
                detailText,
                new THREE.Vector3(textX, textY, textZ),
                textColor,
                0.25 // Smaller text for detailed info
              );

              if (engravedLabel) {
                // Text follows the curve naturally - tangent to the cylinder
                engravedLabel.rotation.y = centerAngle - Math.PI / 2;

                // Store element data for interaction
                engravedLabel.userData = {
                  type: 'element',
                  name: elementName,
                  tonalityType: floor.name,
                  groupName: group.name,
                  depth: i,
                  cardinality: cardinality,
                };

                floorGroup.add(engravedLabel);
              }
            });

            // Add decorative curved base for stele
            const baseGeometry = new THREE.CylinderGeometry(
              curveRadius + steleThickness / 2 + 0.2,
              curveRadius + steleThickness / 2 + 0.2,
              0.3,
              32,
              1,
              false,
              startAngle,
              anglePerStele
            );
            const baseMaterial = new THREE.MeshStandardMaterial({
              color: 0x6a5f4f,
              roughness: 0.85,
              metalness: 0.0,
            });
            const base = new THREE.Mesh(baseGeometry, baseMaterial);
            base.position.set(0, 0.15, 0);
            floorGroup.add(base);
          });

        } else {
          // OTHER FLOORS: Render groups as large clickable regions in a grid
          const groupsPerRow = Math.ceil(Math.sqrt(groups.length));
          const usableFloorSize = floorSize * 0.8;
          const groupSpacing = groupsPerRow > 1 ? usableFloorSize / (groupsPerRow - 1) : usableFloorSize;
          const startX = -(groupsPerRow - 1) * groupSpacing / 2;
          const startZ = -(groupsPerRow - 1) * groupSpacing / 2;

          groups.forEach((group, groupIdx) => {
            const row = Math.floor(groupIdx / groupsPerRow);
            const col = groupIdx % groupsPerRow;
            const x = startX + col * groupSpacing;
            const z = startZ + row * groupSpacing;

            // Create clickable group region (platform/pedestal)
            const platformSize = Math.min(groupSpacing * 0.7, 12);
            const groupGeometry = new THREE.BoxGeometry(platformSize, 1.5, platformSize, 4, 1, 4);

            // PREMIUM MATERIALS - Environment-specific platform materials
            let platformMaterial: THREE.Material;

            if (i === 1) {
              // FLOOR 1: Sandstone platforms
              platformMaterial = new THREE.MeshStandardMaterial({
                color: group.color,
                emissive: group.color,
                emissiveIntensity: 0.25,
                metalness: 0.1,
                roughness: 0.9,
                flatShading: true,
              });
            } else if (i === 2) {
            // OCEAN FLOOR: Coral/shell platforms with pearlescent sheen
            platformMaterial = new THREE.MeshPhysicalMaterial({
              color: group.color,
              emissive: group.color,
              emissiveIntensity: 0.3,
              metalness: 0.0,
              roughness: 0.3,
              clearcoat: 0.8,
              clearcoatRoughness: 0.2,
              sheen: 0.7,
              sheenColor: new THREE.Color(0xFFFFFF),
              iridescence: 0.5, // Pearl-like iridescence
              iridescenceIOR: 1.3,
            });
          } else if (i === 3) {
            // EMERALD FOREST: Emerald gem platforms
            platformMaterial = createEmeraldMaterial();
          } else if (i === 4) {
            // RUBY CAVE: Marble platforms with ruby accents
            platformMaterial = createMarbleMaterial(0xFFE4E1);
          } else {
            // GOLDEN TEMPLE: Polished gold platforms
            platformMaterial = createGoldMaterial();
          }

          // Material properties - reserved for future use
          /* let metalness, roughness, emissiveIntensity, envMapIntensity, clearcoat, clearcoatRoughness, sheen, sheenRoughness, sheenColor;
          if (i <= 1) {
            metalness = 0.1;
            roughness = 0.9;
            emissiveIntensity = 0.25;
            envMapIntensity = 0.3;
            clearcoat = 0;
            clearcoatRoughness = 1;
            sheen = 0;
            sheenRoughness = 1;
            sheenColor = new THREE.Color(0x000000);
          } else if (i === 2) {
            metalness = 0.0;
            roughness = 0.3;
            emissiveIntensity = 0.3;
            envMapIntensity = 1.0;
            clearcoat = 0.8;
            clearcoatRoughness = 0.2;
            sheen = 0.7;
            sheenRoughness = 0.3;
            sheenColor = new THREE.Color(0xFFFFFF);
          } else if (i === 3) {
            metalness = 0.0;
            roughness = 0.05;
            emissiveIntensity = 0.4;
            envMapIntensity = 2.0;
            clearcoat = 1.0;
            clearcoatRoughness = 0.05;
            sheen = 0.5;
            sheenRoughness = 0.2;
            sheenColor = new THREE.Color(0x90EE90);
          } else if (i === 4) {
            metalness = 0.1;
            roughness = 0.3;
            emissiveIntensity = 0.35;
            envMapIntensity = 1.5;
            clearcoat = 0.8;
            clearcoatRoughness = 0.2;
            sheen = 0.3;
            sheenRoughness = 0.4;
            sheenColor = new THREE.Color(0xFFFFFF);
          } else {
            // Gold platforms (Floor 5)
            metalness = 1.0;
            roughness = 0.15;
            emissiveIntensity = 0.4;
            envMapIntensity = 2.5;
            clearcoat = 1.0;
            clearcoatRoughness = 0.1;
            sheen = 1.0;
            sheenRoughness = 0.2;
            sheenColor = new THREE.Color(0xFFA500);
          } */

          // Use the premium material we created earlier, but apply group color
          if (platformMaterial instanceof THREE.MeshPhysicalMaterial || platformMaterial instanceof THREE.MeshStandardMaterial) {
            platformMaterial.color = new THREE.Color(group.color);
            platformMaterial.emissive = new THREE.Color(group.color);
          }

          const groupMesh = new THREE.Mesh(groupGeometry, platformMaterial);
          groupMesh.position.set(x, 0.75, z); // Platform sits on terrain (height=1.5, so center at y=0.75 means base at y=0)
          groupMesh.castShadow = true;

          // Add userData for platform breathing animation
          groupMesh.userData.baseScale = new THREE.Vector3(1, 1, 1);
          groupMesh.userData.breathingPhase = Math.random() * Math.PI * 2; // Random phase offset
          groupMesh.receiveShadow = true;
          groupMesh.userData = {
            type: 'group',
            name: group.name,
            floorIndex: i,
            floorName: floor.name,
            groupData: group,
            clickable: true, // Mark as clickable for scope filtering
          };
          floorGroup.add(groupMesh);

          // Add dedicated point light for each platform to enhance anodized metal reflections
          if (i > 1) { // Only for metallic platforms
            const platformLight = new THREE.PointLight(
              new THREE.Color(group.color).multiplyScalar(0.8), // Colored light matching platform
              0.8, // Intensity
              12, // Distance
              2 // Decay
            );
            platformLight.position.set(x, 4, z); // Above the platform
            floorGroup.add(platformLight);

            // Add subtle rim light from the side for extra highlights
            const rimLight = new THREE.PointLight(
              0xffffff, // White rim light
              0.4, // Lower intensity
              8, // Shorter distance
              2
            );
            rimLight.position.set(x + 3, 2, z + 3); // Offset to the side
            floorGroup.add(rimLight);
          }

          // Add group label on the pyramid wall (not floating above platform)
          // Position label on the nearest wall based on platform position
          const wallDistance = floorSize / 2 - 2; // Slightly inside the wall
          let labelPosition: THREE.Vector3;

          // Determine which wall is closest and position label there
          if (Math.abs(x) > Math.abs(z)) {
            // Closer to left or right wall
            labelPosition = new THREE.Vector3(
              x > 0 ? wallDistance : -wallDistance,
              -5, // Mid-height on wall
              z
            );
          } else {
            // Closer to front or back wall
            labelPosition = new THREE.Vector3(
              x,
              -5, // Mid-height on wall
              z > 0 ? wallDistance : -wallDistance
            );
          }

          const groupLabel = createTextLabel(
            group.name,
            labelPosition,
            0xffffff,
            1.2
          );
          if (groupLabel) floorGroup.add(groupLabel);

          // Add element count below group name
          const countLabel = createTextLabel(
            `${group.elements.length} items`,
            new THREE.Vector3(x, 3.5, z),
            0xaaaaaa,
            0.8
          );
          if (countLabel) floorGroup.add(countLabel);

          // Add a small sample element on top of the platform (visual preview)
          if (group.elements.length > 0) {
            const sampleElement = group.elements[0];

            // Some platforms get small trees, others get wooden cubes
            const useTree = groupIdx % 4 === 0; // Every 4th platform gets a tree

            if (useTree) {
              // Create a small decorative tree
              const treeHeight = 2 + Math.random() * 1; // 2-3 units tall
              const tree = createTree(new THREE.Vector3(x, 2.5, z), treeHeight, 2);
              tree.userData = {
                type: 'sample',
                name: sampleElement,
                groupName: group.name,
                group: group.name,
                isTree: true,
              };
              floorGroup.add(tree);
            } else {
              // Use 3D model for sample element (async loading)
              // Determine model type based on floor ENVIRONMENT
              let modelKey = 'box'; // Default fallback

              if (i === 0) {
                // Floor 0: Desert - use cones (abstract)
                modelKey = 'cone';
              } else if (i === 1) {
                // Floor 1: Desert - use cylinders (catalogued)
                modelKey = 'cylinder';
              } else if (i === 2) {
                // Floor 2: Ocean - use spheres (bubbles/pearls)
                modelKey = 'sphere';
              } else if (i === 3) {
                // Floor 3: Emerald Forest - use emerald crystals or guitars
                modelKey = groupIdx % 3 === 0 ? 'emerald' : (groupIdx % 2 === 0 ? 'guitar' : 'guitar2');
              } else if (i === 4) {
                // Floor 4: Ruby Cave - use ruby crystals or guitars
                modelKey = groupIdx % 3 === 0 ? 'ruby' : (groupIdx % 2 === 0 ? 'guitar' : 'guitar2');
              } else {
                // Floor 5: Golden Temple - use gold ornaments or guitars
                modelKey = groupIdx % 3 === 0 ? 'brass_ornament' : (groupIdx % 2 === 0 ? 'guitar' : 'guitar2');
              }

              // Load model asynchronously
              loadModel(modelKey).then((model) => {
                model.position.set(x, 2.25, z); // On top of platform
                model.scale.set(0.4, 0.4, 0.4); // Scale down to fit on platform

                // Apply color tint to model materials
                model.traverse((child) => {
                  if ((child as THREE.Mesh).isMesh) {
                    const mesh = child as THREE.Mesh;
                    if (mesh.material) {
                      const mat = mesh.material as THREE.MeshStandardMaterial;
                      mat.emissive = new THREE.Color(group.color);
                      mat.emissiveIntensity = 0.3;
                      mesh.castShadow = true;
                      mesh.receiveShadow = true;
                    }
                  }
                });

                // Create floor-specific userData for detail panel
                const userData: Record<string, unknown> = {
                  type: 'sample',
                  name: sampleElement,
                  groupName: group.name,
                  group: group.name,
                  modelKey: modelKey,
                  // Add rotation speed variation for animation
                  rotationSpeed: 0.3 + Math.random() * 0.4, // Random speed between 0.3 and 0.7
                  rotationAxis: Math.random() > 0.5 ? 'y' : 'x', // Vary rotation axis
                };

                // Add floor-specific demo data
                if (i <= 2) {
                  // Floors 0-2: Pitch Class Sets, Forte Codes, Prime Forms
                  userData.pitchClassSet = 2773; // C major scale
                  userData.forteCode = '7-35';
                  userData.intervalClassVector = [2, 5, 4, 3, 6, 1];
                  userData.primeForm = [0, 1, 3, 5, 6, 8, 10];
                } else if (i <= 4) {
                  // Floors 3-4: Chords and Inversions
                  userData.chordSymbol = sampleElement;
                  userData.chordQuality = 'Major';
                  userData.rootNote = 'C';
                  userData.inversion = groupIdx % 3; // Vary inversions
                  userData.notes = ['C', 'E', 'G'];
                  userData.voicing = [3, 3, 2, 0, 1, 0]; // C chord
                } else {
                  // Floor 5: Voicings
                  const cagedShapes = ['C', 'A', 'G', 'E', 'D'];
                  const difficulties = ['Easy', 'Intermediate', 'Advanced'];
                  userData.voicing = [3, 3, 2, 0, 1, 0];
                  userData.cagedShape = cagedShapes[groupIdx % cagedShapes.length];
                  userData.fretRange = { min: 0, max: 3 };
                  userData.difficulty = difficulties[groupIdx % difficulties.length];
                  userData.tabNotation = '6/3 5/3 4/2 3/0 2/1 1/0';
                  userData.chordSymbol = sampleElement;
                  userData.notes = ['C', 'E', 'G', 'C', 'E'];
                }

                model.userData = userData;
                floorGroup.add(model);
              }).catch((error) => {
                console.warn(`Failed to load model ${modelKey} for element ${sampleElement}:`, error);
              });
            }
          }
          });
        }
      }

      // Add FOCUSED partition - only one per floor for clarity
      if (i > 0 && i < 5) {
        const partitionStrategies = [
          { name: 'Circle of Fifths', color: 0x00ff00 },
          { name: 'Chromatic Distance', color: 0x00ffff },
          { name: 'Harmonic Series', color: 0xff00ff },
          { name: 'Modal Brightness', color: 0xffff00 },
        ];
        const strategy = partitionStrategies[(i - 1) % partitionStrategies.length];

        // Enhanced partition material with metallic/glass-like appearance
        const partitionMaterial = new THREE.MeshStandardMaterial({
          color: strategy.color,
          transparent: true,
          opacity: 0.35, // More visible
          side: THREE.DoubleSide,
          emissive: strategy.color,
          emissiveIntensity: 0.7, // Stronger glow
          metalness: 0.6, // More metallic/glass-like
          roughness: 0.3, // Smoother for reflections
          flatShading: false, // Smooth for glass effect
        });

        // Single, prominent partition plane with segmented geometry for blocky feel
        const partition = new THREE.Mesh(new THREE.PlaneGeometry(50, 8, 10, 4), partitionMaterial);
        partition.position.set(0, 4, 0);
        partition.rotation.y = i % 2 === 0 ? 0 : Math.PI / 2;
        partition.userData = {
          type: 'partition',
          strategy: strategy.name,
          depth: i,
          name: `${strategy.name} Partition`,
        };
        floorGroup.add(partition);
        partitionPlanesRef.current.push(partition);

        // Add partition label
        const partitionLabel = createTextLabel(
          strategy.name,
          new THREE.Vector3(0, 8, i % 2 === 0 ? 2 : 0),
          strategy.color,
          0.6
        );
        if (partitionLabel) floorGroup.add(partitionLabel);
      }
    }

    // Create CONTINUOUS SMOOTH PYRAMID EXTERIOR (not stepped)
    // Base is at Floor 0 (y=0), apex is above Floor 5
    const baseFloorSize = calculateFloorSize(floorElementCounts[0]); // Largest floor (bottom)
    const halfBaseSize = baseFloorSize / 2;
    const pyramidHeight = floorData.length * 20 + 25; // Total height from base to apex
    const apexY = pyramidHeight;

    const pyramidGroup = new THREE.Group();
    pyramidGroup.position.y = 0; // Start at base level
    parent.add(pyramidGroup);

    // Materials for continuous pyramid
    const glassMaterial = createGlassMaterial();
    const armatureMaterial = createStoneMaterial(0x1a1a1a); // Very dark (almost black) for high contrast

    // Create 4 continuous triangular faces from base to apex
    const pyramidFaces = [
      // Back face (negative Z)
      [
        [-halfBaseSize, 0, -halfBaseSize],
        [halfBaseSize, 0, -halfBaseSize],
        [0, apexY, 0], // Apex point
      ],
      // Front face (positive Z)
      [
        [-halfBaseSize, 0, halfBaseSize],
        [halfBaseSize, 0, halfBaseSize],
        [0, apexY, 0], // Apex point
      ],
      // Left face (negative X)
      [
        [-halfBaseSize, 0, -halfBaseSize],
        [-halfBaseSize, 0, halfBaseSize],
        [0, apexY, 0], // Apex point
      ],
      // Right face (positive X)
      [
        [halfBaseSize, 0, -halfBaseSize],
        [halfBaseSize, 0, halfBaseSize],
        [0, apexY, 0], // Apex point
      ],
    ];

    pyramidFaces.forEach((faceCorners) => {
      // Create glass panel for each face
      const vertices = new Float32Array([
        ...faceCorners[0], ...faceCorners[1], ...faceCorners[2],
      ]);

      const geometry = new THREE.BufferGeometry();
      geometry.setAttribute('position', new THREE.BufferAttribute(vertices, 3));
      geometry.computeVertexNormals();

      const glassPanel = new THREE.Mesh(geometry, glassMaterial);
      glassPanel.castShadow = false;
      glassPanel.receiveShadow = true;
      glassPanel.userData = { type: 'pyramid_glass' };
      pyramidGroup.add(glassPanel);

      // Create armature beams along edges
      const edges = [
        [faceCorners[0], faceCorners[1]], // Base edge
        [faceCorners[1], faceCorners[2]], // Right edge to apex
        [faceCorners[2], faceCorners[0]], // Left edge to apex
      ];

      edges.forEach((edge) => {
        const start = new THREE.Vector3(...edge[0]);
        const end = new THREE.Vector3(...edge[1]);
        const length = start.distanceTo(end);
        const beamRadius = 0.4; // Thicker beams for better visibility

        const beamGeometry = new THREE.CylinderGeometry(beamRadius, beamRadius, length, 12);
        const beam = new THREE.Mesh(beamGeometry, armatureMaterial);

        // Position and orient the beam
        beam.position.copy(start.clone().add(end).multiplyScalar(0.5));
        beam.quaternion.setFromUnitVectors(
          new THREE.Vector3(0, 1, 0),
          end.clone().sub(start).normalize()
        );

        beam.castShadow = true;
        beam.receiveShadow = true;
        pyramidGroup.add(beam);
      });
    });



    // Attempt to populate floors with real API data (disabled during loading to prevent blocking)
    // populateFloorsWithApiData();
  };

  /**
   * Populate floors with real data from API when available
   * This runs asynchronously and updates the scene with real musical data
   * Reserved for future use
   */
  // eslint-disable-next-line @typescript-eslint/no-unused-vars
  const populateFloorsWithApiData = async () => {
    try {
      // Floor 3: Populate with real chords from C Major key
      const cMajorChords = await BSPApiService.getChordsForKey('C Major', {
        extension: 'Triad',
        onlyNaturallyOccurring: true,
        limit: 24
      });

      if (cMajorChords.length > 0 && floorGroupsRef.current[3]) {
        updateFloorElements(3, cMajorChords.map(c => c.name), 0x44ffff);
        if (import.meta.env.DEV) {
          console.log(`✅ Floor 3 populated with ${cMajorChords.length} chords from C Major`);
        }
      }

      // Floor 4: Populate with chord inversions
      const cMajorExtendedChords = await BSPApiService.getChordsForKey('C Major', {
        extension: 'Seventh',
        onlyNaturallyOccurring: true,
        limit: 18
      });

      if (cMajorExtendedChords.length > 0 && floorGroupsRef.current[4]) {
        // Create inversion names from the chords
        const inversions = cMajorExtendedChords.flatMap(chord => {
          const root = chord.root;
          return [`${chord.name}/${root}`, `${chord.name}/inv1`, `${chord.name}/inv2`];
        }).slice(0, 18);

        updateFloorElements(4, inversions, 0xff4488);
        if (import.meta.env.DEV) {
          console.log(`✅ Floor 4 populated with ${inversions.length} inversions`);
        }
      }

      // Floor 5: Populate with real voicings for C Major chord
      const cMajorVoicings = await BSPApiService.getVoicingsForChord('C Major', {
        maxDifficulty: 'Intermediate',
        limit: 20
      });

      if (cMajorVoicings.length > 0 && floorGroupsRef.current[5]) {
        const voicingNames = cMajorVoicings.map((v) => {
          const shape = v.cagedShape || 'Open';
          const frets = `${v.fretRange.min}-${v.fretRange.max}`;
          return `${shape} (${frets})`;
        });

        updateFloorElements(5, voicingNames, 0xffff44);
        if (import.meta.env.DEV) {
          console.log(`✅ Floor 5 populated with ${voicingNames.length} voicings for C Major`);
        }
      }

      // Floor 5: Add hierarchical room/door system (like zoomable sunburst)
      if (floorGroupsRef.current[5]) {
        const voicingDoors = generateRoomHierarchy(5);
        createRoomWithDoors(
          'Voicings Explorer',
          voicingDoors,
          floorGroupsRef.current[5],
          new THREE.Vector3(0, 0, 0)
        );

        if (import.meta.env.DEV) {
          console.log(`✅ Floor 5: Created room with ${voicingDoors.length} category doors`);
        }
      }

    } catch {
      // Silently fail - demo data is already in place
      if (import.meta.env.DEV) {
        console.debug('API data population failed, using demo data');
      }
    }
  };

  /**
   * Update elements on a specific floor with new data
   */
  const updateFloorElements = (floorIndex: number, elementNames: string[], color: number) => {
    const floorGroup = floorGroupsRef.current[floorIndex];
    if (!floorGroup) return;

    // Remove existing element meshes and labels (keep floor, grid, partition)
    const toRemove: THREE.Object3D[] = [];
    floorGroup.children.forEach(child => {
      if (child.userData.type === 'element' || (child instanceof THREE.Sprite && child.position.y < 10)) {
        toRemove.push(child);
      }
    });
    toRemove.forEach(obj => floorGroup.remove(obj));

    // Add new elements
    const elementsPerRow = Math.ceil(Math.sqrt(elementNames.length));
    const spacing = 15;
    const startX = -(elementsPerRow - 1) * spacing / 2;
    const startZ = -(elementsPerRow - 1) * spacing / 2;

    elementNames.forEach((elementName, idx) => {
      const row = Math.floor(idx / elementsPerRow);
      const col = idx % elementsPerRow;
      const x = startX + col * spacing;
      const z = startZ + row * spacing;

      // Create element box
      const geometry = new THREE.BoxGeometry(4, 4, 4);
      const material = new THREE.MeshStandardMaterial({
        color: color,
        emissive: color,
        emissiveIntensity: 0.6,
        metalness: 0.3,
        roughness: 0.8,
        flatShading: true,
      });
      const mesh = new THREE.Mesh(geometry, material);
      mesh.position.set(x, 2, z);
      mesh.userData = {
        type: 'element',
        name: elementName,
        tonalityType: `Floor ${floorIndex}`,
        depth: floorIndex,
      };
      floorGroup.add(mesh);

      // Add label
      const label = createTextLabel(
        elementName,
        new THREE.Vector3(x, 6, z),
        0xffffff,
        0.8
      );
      if (label) floorGroup.add(label);
    });
  };

  /**
   * Create a room with doors arranged in a circular pattern (like zoomable sunburst)
   * @param roomName - Name of the room
   * @param doors - Array of door configurations
   * @param floorGroup - THREE.Group to add the room to
   * @param centerPosition - Center position of the room
   */
  /**
   * Generate hierarchical room/door data for a given floor
   * This creates the sunburst-like hierarchy
   */
  const generateRoomHierarchy = useCallback((floor: number, parentName?: string): Array<{
    name: string;
    color: number;
    targetFloor?: number;
    children?: string[];
  }> => {
    // Floor 5 (Voicings) - Create category doors
    if (floor === 5 && !parentName) {
      return [
        {
          name: 'Jazz Voicings',
          color: 0x00ff00,
          targetFloor: 5,
          children: ['Drop 2', 'Drop 3', 'Drop 2+4', 'Rootless', 'Shell', 'Quartal']
        },
        {
          name: 'Classical Voicings',
          color: 0x0088ff,
          targetFloor: 5,
          children: ['Close Position', 'Open Position', 'Four-Part', 'SATB']
        },
        {
          name: 'Rock Voicings',
          color: 0xff0088,
          targetFloor: 5,
          children: ['Power Chords', 'Barre Chords', 'Open Chords', 'Triads']
        },
        {
          name: 'CAGED System',
          color: 0xffff00,
          targetFloor: 5,
          children: ['C Shape', 'A Shape', 'G Shape', 'E Shape', 'D Shape']
        },
        {
          name: 'Position-Based',
          color: 0xff8800,
          targetFloor: 5,
          children: ['Position I', 'Position II', 'Position III', 'Position IV', 'Position V']
        },
        {
          name: 'String Sets',
          color: 0x00ffff,
          targetFloor: 5,
          children: ['Strings 1-3', 'Strings 2-4', 'Strings 3-5', 'Strings 4-6']
        },
      ];
    }

    // Sub-rooms for Jazz Voicings
    if (parentName === 'Jazz Voicings') {
      return [
        { name: 'Drop 2', color: 0x00ff00, children: ['Maj7', 'Min7', 'Dom7', 'Min7b5', 'Dim7'] },
        { name: 'Drop 3', color: 0x00dd00, children: ['Maj7', 'Min7', 'Dom7', 'Min7b5'] },
        { name: 'Drop 2+4', color: 0x00bb00, children: ['Maj7', 'Min7', 'Dom7'] },
        { name: 'Rootless', color: 0x009900, children: ['Type A', 'Type B'] },
        { name: 'Shell', color: 0x007700, children: ['Root-3-7', 'Root-7-3'] },
        { name: 'Quartal', color: 0x005500, children: ['4ths', 'Sus4', 'Add11'] },
      ];
    }

    // Default: return empty array
    return [];
  }, []);

  const createRoomWithDoors = useCallback((
    roomName: string,
    doors: Array<{ name: string; color: number; targetFloor?: number; children?: string[] }>,
    floorGroup: THREE.Group,
    centerPosition: THREE.Vector3 = new THREE.Vector3(0, 0, 0)
  ) => {
    const roomGroup = new THREE.Group();
    roomGroup.position.copy(centerPosition);

    // Create central platform
    const platformGeometry = new THREE.CylinderGeometry(8, 8, 1, 32);
    const platformMaterial = new THREE.MeshPhysicalMaterial({
      color: 0x2a2a2a,
      metalness: 0.6,
      roughness: 0.4,
      emissive: 0x1a1a1a,
      emissiveIntensity: 0.2,
    });
    const platform = new THREE.Mesh(platformGeometry, platformMaterial);
    platform.position.y = 0.5;
    roomGroup.add(platform);

    // Add room label above
    const roomLabel = createTextLabel(
      roomName,
      new THREE.Vector3(0, 10, 0),
      0x00ffff,
      2
    );
    if (roomLabel) roomGroup.add(roomLabel);

    // Arrange doors in a circle around the platform
    const radius = 15;
    const angleStep = (Math.PI * 2) / doors.length;

    doors.forEach((doorConfig, index) => {
      const angle = index * angleStep;
      const x = Math.cos(angle) * radius;
      const z = Math.sin(angle) * radius;

      const door = create3DDoor(
        doorConfig.name,
        new THREE.Vector3(x, 4, z),
        doorConfig.color,
        doorConfig.targetFloor,
        doorConfig.children
      );

      // Rotate door to face the center
      door.lookAt(centerPosition.x, door.position.y, centerPosition.z);

      roomGroup.add(door);

      // Add a path from platform to door
      const pathGeometry = new THREE.BoxGeometry(2, 0.2, radius - 8);
      const pathMaterial = new THREE.MeshPhysicalMaterial({
        color: doorConfig.color,
        emissive: doorConfig.color,
        emissiveIntensity: 0.3,
        metalness: 0.5,
        roughness: 0.5,
        transparent: true,
        opacity: 0.6,
      });
      const path = new THREE.Mesh(pathGeometry, pathMaterial);
      path.position.set(x * 0.5, 0.1, z * 0.5);
      path.rotation.y = angle;
      roomGroup.add(path);
    });

    floorGroup.add(roomGroup);
    return roomGroup;
  }, [create3DDoor]);

  const createTextLabel = (text: string, position: THREE.Vector3, color: number = 0xffffff, size: number = 1) => {
    // Create enhanced sprite-based text label with glow effect
    const canvas = document.createElement('canvas');
    const context = canvas.getContext('2d');
    if (!context) return null;

    // Calculate optimal canvas size based on text length and size
    const fontSize = Math.max(32, Math.min(72, size * 48));
    context.font = `bold ${fontSize}px Arial, sans-serif`;
    const metrics = context.measureText(text);
    const textWidth = metrics.width;

    // Add padding for glow and outline effects
    const padding = 40;
    canvas.width = Math.max(256, Math.ceil(textWidth + padding * 2));
    canvas.height = Math.max(64, Math.ceil(fontSize * 1.5 + padding));

    // Re-apply font after canvas resize (canvas resize clears context)
    context.font = `bold ${fontSize}px Arial, sans-serif`;
    context.textAlign = 'center';
    context.textBaseline = 'middle';

    const centerX = canvas.width / 2;
    const centerY = canvas.height / 2;

    // Draw outline FIRST (so it appears behind the fill)
    context.strokeStyle = '#000000';
    context.lineWidth = Math.max(2, fontSize / 16);
    context.lineJoin = 'round';
    context.miterLimit = 2;
    context.strokeText(text, centerX, centerY);

    // Add glow effect
    context.shadowColor = `#${color.toString(16).padStart(6, '0')}`;
    context.shadowBlur = Math.max(10, fontSize / 4);
    context.shadowOffsetX = 0;
    context.shadowOffsetY = 0;

    // Draw text with glow AFTER outline
    context.fillStyle = `#${color.toString(16).padStart(6, '0')}`;
    context.fillText(text, centerX, centerY);

    const texture = new THREE.CanvasTexture(canvas);
    texture.needsUpdate = true;
    const spriteMaterial = new THREE.SpriteMaterial({
      map: texture,
      transparent: true,
      opacity: 0.9,
      depthTest: false, // Always visible
    });
    const sprite = new THREE.Sprite(spriteMaterial);
    sprite.position.copy(position);

    // Scale sprite proportionally to canvas aspect ratio
    const aspectRatio = canvas.width / canvas.height;
    sprite.scale.set(size * 4 * aspectRatio, size * 4, 1);

    return sprite;
  };

  const createTree = (position: THREE.Vector3, height: number = 5, iterations: number = 3) => {
    // Create a realistic 3D tree using fractal branching (L-system inspired)
    const treeGroup = new THREE.Group();

    // Trunk material - dark brown bark
    const trunkMaterial = new THREE.MeshStandardMaterial({
      color: 0x4a2511,
      roughness: 0.9,
      metalness: 0.0,
    });

    // Leaves material - green foliage
    const leavesMaterial = new THREE.MeshStandardMaterial({
      color: 0x2d5016,
      roughness: 0.8,
      metalness: 0.0,
    });

    // Recursive branch generation
    const createBranch = (
      startPos: THREE.Vector3,
      direction: THREE.Vector3,
      length: number,
      thickness: number,
      depth: number
    ) => {
      if (depth <= 0 || length < 0.3) {
        // Create leaves at branch tips
        const leavesGeometry = new THREE.SphereGeometry(thickness * 3, 4, 4);
        const leaves = new THREE.Mesh(leavesGeometry, leavesMaterial);
        leaves.position.copy(startPos);
        treeGroup.add(leaves);
        return;
      }

      // Create branch segment
      const branchGeometry = new THREE.CylinderGeometry(
        thickness * 0.7, // Top radius (tapers)
        thickness,       // Bottom radius
        length,
        6,              // Radial segments
        1
      );
      const branch = new THREE.Mesh(branchGeometry, trunkMaterial);

      // Position branch
      const midPoint = startPos.clone().add(direction.clone().multiplyScalar(length / 2));
      branch.position.copy(midPoint);

      // Rotate branch to align with direction
      const up = new THREE.Vector3(0, 1, 0);
      const quaternion = new THREE.Quaternion();
      quaternion.setFromUnitVectors(up, direction.clone().normalize());
      branch.setRotationFromQuaternion(quaternion);

      treeGroup.add(branch);

      // Calculate end position
      const endPos = startPos.clone().add(direction.clone().multiplyScalar(length));

      // Create child branches (fractal branching)
      const branchCount = 2 + Math.floor(Math.random() * 2); // 2-3 branches
      const angleSpread = Math.PI / 3; // 60 degrees

      for (let i = 0; i < branchCount; i++) {
        // Random angle around the parent branch
        const angle = (i / branchCount) * Math.PI * 2 + Math.random() * 0.5;
        const elevation = angleSpread * (0.5 + Math.random() * 0.5);

        // Calculate new direction
        const newDir = new THREE.Vector3(
          Math.sin(elevation) * Math.cos(angle),
          Math.cos(elevation),
          Math.sin(elevation) * Math.sin(angle)
        );

        // Rotate around parent direction
        const rotationAxis = direction.clone().normalize();
        newDir.applyAxisAngle(rotationAxis, Math.random() * Math.PI * 2);

        // Recursive call with reduced length and thickness
        createBranch(
          endPos.clone(),
          newDir,
          length * (0.6 + Math.random() * 0.2), // 60-80% of parent length
          thickness * 0.7,
          depth - 1
        );
      }
    };

    // Start with main trunk
    const trunkDirection = new THREE.Vector3(0, 1, 0);
    createBranch(
      position.clone(),
      trunkDirection,
      height,
      height * 0.1, // Trunk thickness proportional to height
      iterations
    );

    treeGroup.position.copy(position);
    return treeGroup;
  };

  const createWoodMaterial = (baseColor: number = 0x8B4513, variation: number = 0) => {
    // Create realistic wood material with procedural grain
    const canvas = document.createElement('canvas');
    const context = canvas.getContext('2d');
    if (!context) return new THREE.MeshStandardMaterial({ color: baseColor });

    canvas.width = 512;
    canvas.height = 512;

    // Base wood color (saddle brown with variation)
    const r = Math.floor(139 + variation * 30);
    const g = Math.floor(69 + variation * 20);
    const b = Math.floor(19 + variation * 10);

    // Fill with base color
    context.fillStyle = `rgb(${r}, ${g}, ${b})`;
    context.fillRect(0, 0, canvas.width, canvas.height);

    // Add wood grain lines (vertical with slight curves)
    const grainCount = 20 + Math.floor(Math.random() * 10);
    for (let i = 0; i < grainCount; i++) {
      const x = (i / grainCount) * canvas.width;
      const darkness = 0.7 + Math.random() * 0.3;

      context.strokeStyle = `rgba(${Math.floor(r * darkness)}, ${Math.floor(g * darkness)}, ${Math.floor(b * darkness)}, ${0.3 + Math.random() * 0.4})`;
      context.lineWidth = 1 + Math.random() * 3;

      context.beginPath();
      context.moveTo(x, 0);

      // Create wavy grain lines
      for (let y = 0; y < canvas.height; y += 10) {
        const offset = Math.sin(y * 0.02 + i) * 5;
        context.lineTo(x + offset, y);
      }
      context.stroke();
    }

    // Add knots (circular darker spots)
    const knotCount = 2 + Math.floor(Math.random() * 3);
    for (let i = 0; i < knotCount; i++) {
      const knotX = Math.random() * canvas.width;
      const knotY = Math.random() * canvas.height;
      const knotRadius = 10 + Math.random() * 20;

      const gradient = context.createRadialGradient(knotX, knotY, 0, knotX, knotY, knotRadius);
      gradient.addColorStop(0, `rgba(${Math.floor(r * 0.4)}, ${Math.floor(g * 0.4)}, ${Math.floor(b * 0.4)}, 0.8)`);
      gradient.addColorStop(1, `rgba(${r}, ${g}, ${b}, 0)`);

      context.fillStyle = gradient;
      context.beginPath();
      context.arc(knotX, knotY, knotRadius, 0, Math.PI * 2);
      context.fill();
    }

    // Create texture from canvas
    const texture = new THREE.CanvasTexture(canvas);
    texture.wrapS = THREE.RepeatWrapping;
    texture.wrapT = THREE.RepeatWrapping;
    texture.repeat.set(2, 2);

    // Create wood material with realistic properties (IMPROVED)
    return new THREE.MeshStandardMaterial({
      map: texture,
      color: new THREE.Color(baseColor),
      roughness: 0.8, // Wood is quite rough
      metalness: 0.0, // Wood is not metallic
      normalScale: new THREE.Vector2(0.5, 0.5),
      envMapIntensity: 0.3, // Subtle reflections
      bumpScale: 0.02, // Slight surface variation
    });
  };

  const createDetailCard = (
    title: string,
    details: string[],
    position: THREE.Vector3,
    color: number = 0xffffff,
    size: number = 1
  ) => {
    // Create a detailed info card with multiple lines
    const canvas = document.createElement('canvas');
    const context = canvas.getContext('2d');
    if (!context) return null;

    canvas.width = 1024;
    canvas.height = 512;

    // Clear canvas
    context.clearRect(0, 0, canvas.width, canvas.height);

    // Draw background card
    context.fillStyle = 'rgba(0, 0, 0, 0.85)';
    context.fillRect(20, 20, canvas.width - 40, canvas.height - 40);

    // Draw border with glow
    context.strokeStyle = `#${color.toString(16).padStart(6, '0')}`;
    context.lineWidth = 6;
    context.shadowColor = `#${color.toString(16).padStart(6, '0')}`;
    context.shadowBlur = 20;
    context.strokeRect(20, 20, canvas.width - 40, canvas.height - 40);

    // Draw title
    context.font = 'bold 56px Arial, sans-serif';
    context.fillStyle = `#${color.toString(16).padStart(6, '0')}`;
    context.textAlign = 'center';
    context.textBaseline = 'top';
    context.shadowBlur = 15;
    context.fillText(title, canvas.width / 2, 60);

    // Draw details
    context.font = '36px Arial, sans-serif';
    context.fillStyle = '#ffffff';
    context.shadowBlur = 5;
    context.shadowColor = '#000000';
    let yOffset = 140;
    details.forEach((detail) => {
      context.fillText(detail, canvas.width / 2, yOffset);
      yOffset += 50;
    });

    const texture = new THREE.CanvasTexture(canvas);
    texture.needsUpdate = true;
    const spriteMaterial = new THREE.SpriteMaterial({
      map: texture,
      transparent: true,
      opacity: 0.95,
      depthTest: true,
    });
    const sprite = new THREE.Sprite(spriteMaterial);
    sprite.position.copy(position);
    sprite.scale.set(size * 8, size * 4, 1);
    sprite.userData.type = 'detail-card';

    return sprite;
  };

  const createInstancedElements = (
    elements: string[],
    bounds: { minX: number; maxX: number; minZ: number; maxZ: number },
    yPosition: number,
    color: number,
    floorGroup: THREE.Group
  ) => {
    if (elements.length === 0) return;

    const { minX, maxX, minZ, maxZ } = bounds;
    const centerX = (minX + maxX) / 2;
    const centerZ = (minZ + maxZ) / 2;
    const sizeX = maxX - minX;
    const sizeZ = maxZ - minZ;

    if (enableInstancing && elements.length > maxElementsPerRegion) {
      // Use instanced rendering for large numbers of elements with enhanced visuals
      const geometry = new THREE.SphereGeometry(0.15, 12, 12); // Higher detail
      const material = new THREE.MeshStandardMaterial({
        color,
        emissive: color,
        emissiveIntensity: 0.6,
        metalness: 0.4,
        roughness: 0.3,
        envMapIntensity: 1.2,
      });

      const instancedMesh = new THREE.InstancedMesh(geometry, material, elements.length);
      const matrix = new THREE.Matrix4();
      const position = new THREE.Vector3();

      // Arrange in a grid or spiral pattern
      const gridSize = Math.ceil(Math.sqrt(elements.length));

      elements.forEach((element, idx) => {
        const row = Math.floor(idx / gridSize);
        const col = idx % gridSize;
        const offsetX = (col - gridSize / 2) * (sizeX / gridSize) * 0.9;
        const offsetZ = (row - gridSize / 2) * (sizeZ / gridSize) * 0.9;

        position.set(centerX + offsetX, yPosition, centerZ + offsetZ);
        matrix.setPosition(position);
        instancedMesh.setMatrixAt(idx, matrix);
      });

      instancedMesh.instanceMatrix.needsUpdate = true;
      instancedMesh.userData = { type: 'instanced-elements', count: elements.length };
      floorGroup.add(instancedMesh);

      // Add aggregated label showing count
      const countLabel = createTextLabel(
        `${elements.length.toLocaleString()} chords`,
        new THREE.Vector3(centerX, yPosition + 2, centerZ),
        0xffff00,
        1.5
      );
      if (countLabel) floorGroup.add(countLabel);

    } else {
      // Render individual elements for smaller counts
      const gridSize = Math.ceil(Math.sqrt(elements.length));

      elements.slice(0, maxElementsPerRegion).forEach((element, idx) => {
        const sphereGeometry = new THREE.SphereGeometry(0.2, 16, 16); // Higher detail for individual spheres
        const sphereMaterial = new THREE.MeshStandardMaterial({
          color: TONALITY_COLORS[element.tonalityType] || color,
          emissive: TONALITY_COLORS[element.tonalityType] || color,
          emissiveIntensity: 0.7,
          metalness: 0.5,
          roughness: 0.2,
          envMapIntensity: 1.5,
        });
        const sphere = new THREE.Mesh(sphereGeometry, sphereMaterial);

        const row = Math.floor(idx / gridSize);
        const col = idx % gridSize;
        const offsetX = (col - gridSize / 2) * (sizeX / gridSize) * 0.8;
        const offsetZ = (row - gridSize / 2) * (sizeZ / gridSize) * 0.8;

        sphere.position.set(centerX + offsetX, yPosition, centerZ + offsetZ);
        sphere.userData = {
          type: 'element',
          name: element.name,
          pitchClasses: element.pitchClasses ? element.pitchClasses.map((pc: string) => {
            // Convert pitch class string to number (0-11)
            // Handle 'T' (10) and 'E' (11) notation
            if (pc === 'T' || pc === 't') return 10;
            if (pc === 'E' || pc === 'e') return 11;
            return parseInt(pc, 10);
          }) : []
        };
        floorGroup.add(sphere);

        // Add element label (only for small counts)
        if (elements.length <= 20) {
          const elementLabel = createTextLabel(
            element.name,
            new THREE.Vector3(centerX + offsetX, yPosition + 0.5, centerZ + offsetZ),
            0xffffff,
            0.3
          );
          if (elementLabel) floorGroup.add(elementLabel);
        }
      });

      // Show "and X more" if we truncated
      if (elements.length > maxElementsPerRegion) {
        const moreLabel = createTextLabel(
          `...and ${(elements.length - maxElementsPerRegion).toLocaleString()} more`,
          new THREE.Vector3(centerX, yPosition + 2, centerZ),
          0xff8800,
          0.8
        );
        if (moreLabel) floorGroup.add(moreLabel);
      }
    }
  };

  const buildRealBSPWorld = async (parent: THREE.Group, treeData: BSPTreeStructureResponse) => {
    // Create floor groups (one per hierarchy level)
    const maxDepth = treeData.maxDepth;
    floorGroupsRef.current = [];

    for (let i = 0; i <= maxDepth; i++) {
      const floorGroup = new THREE.Group();
      floorGroup.position.y = i * 20; // 20 units between floors (matches camera spacing)
      floorGroup.visible = i === 0; // PERFORMANCE: Only show first floor initially
      parent.add(floorGroup);
      floorGroupsRef.current.push(floorGroup);

      // Add floor plane with grid pattern
      const floorGeometry = new THREE.PlaneGeometry(100, 100);
      const floorMaterial = new THREE.MeshStandardMaterial({
        color: i === 0 ? 0x1a1a2e : 0x16213e, // Darker for floor 0, slightly lighter for others
        roughness: 0.9,
        metalness: 0.1,
        emissive: 0x0f0f1a,
        emissiveIntensity: 0.1,
      });
      const floor = new THREE.Mesh(floorGeometry, floorMaterial);
      floor.rotation.x = -Math.PI / 2;
      floor.receiveShadow = true;
      floorGroup.add(floor);

      // Add grid helper for visual reference (THIN, SUBTLE lines)
      const gridHelper = new THREE.GridHelper(100, 20, 0x444466, 0x222233);
      gridHelper.position.y = 0.01; // Slightly above floor to prevent z-fighting

      // Make grid lines thinner and more subtle
      const gridHelperMaterial = gridHelper.material as THREE.LineBasicMaterial;
      if (gridHelperMaterial) {
        gridHelperMaterial.opacity = 0.3;
        gridHelperMaterial.transparent = true;
        gridHelperMaterial.linewidth = 0.5;
      }

      floorGroup.add(gridHelper);

      // Add floor label with atonal/tonal color coding
      const floorNames = ['Pitch Class Sets', 'Forte Codes', 'Prime Forms', 'Chords', 'Inversions', 'Voicings'];
      const isAtonal = i <= 2;
      const labelColor = isAtonal ? 0x00ffff : 0xffff00;

      const label = createTextLabel(
        `Floor ${i}: ${floorNames[i] || `Level ${i}`}`,
        new THREE.Vector3(0, 5, -40),
        labelColor,
        2
      );
      if (label) floorGroup.add(label);
    }

    // Recursively build the BSP world from the real tree structure
    const buildNode = (node: typeof treeData.root, depth: number, bounds: { minX: number; maxX: number; minZ: number; maxZ: number }) => {
      if (!node) return;

      const { minX, maxX, minZ, maxZ } = bounds;
      const centerX = (minX + maxX) / 2;
      const centerZ = (minZ + maxZ) / 2;
      const sizeX = maxX - minX;
      const sizeZ = maxZ - minZ;

      // Get the floor group for this depth level
      const floorGroup = floorGroupsRef.current[depth];
      if (!floorGroup) return;

      if (node.isLeaf) {
        // Create a colored region box for leaf nodes (Icicle-style) with enhanced visuals
        const color = TONALITY_COLORS[node.region.tonalityType] || 0xffffff;
        const height = 3; // Taller boxes for better visibility
        const geometry = new THREE.BoxGeometry(sizeX * 0.95, height, sizeZ * 0.95);
        const material = new THREE.MeshStandardMaterial({
          color,
          transparent: true,
          opacity: 0.6,
          emissive: color,
          emissiveIntensity: 0.4,
          metalness: 0.2,
          roughness: 0.5,
          envMapIntensity: 1.0,
        });
        const mesh = new THREE.Mesh(geometry, material);
        mesh.position.set(centerX, height / 2, centerZ);
        mesh.userData = {
          type: 'region',
          name: node.region.name,
          tonalityType: node.region.tonalityType,
          depth: node.depth,
          bounds: { minX, maxX, minZ, maxZ }
        };
        floorGroup.add(mesh);

        // Add region label
        const regionLabel = createTextLabel(
          node.region.name,
          new THREE.Vector3(centerX, height + 1, centerZ),
          color,
          Math.min(sizeX, sizeZ) / 10
        );
        if (regionLabel) floorGroup.add(regionLabel);

        // Use instanced rendering for elements (handles massive scale)
        if (node.elements.length > 0) {
          createInstancedElements(
            node.elements,
            { minX, maxX, minZ, maxZ },
            height + 0.5,
            color,
            floorGroup
          );
        }
      } else if (node.partition) {
        // Create a partition plane (wall) - Icicle style with color coding by strategy
        const strategyColors: Record<string, number> = {
          'CircleOfFifths': 0x00ff00,
          'ChromaticDistance': 0x00ffff,
          'HarmonicSeries': 0xff00ff,
          'ModalBrightness': 0xffff00,
          'TonalStability': 0xff8800,
        };
        const partitionColor = strategyColors[node.partition.strategy] || 0x00ff00;

        let planeGeometry: THREE.PlaneGeometry;
        let position: THREE.Vector3;
        let rotation: THREE.Euler;
        let labelPosition: THREE.Vector3;

        // Determine partition orientation based on normal vector
        const normal = node.partition.normal;
        const wallHeight = 8; // Taller walls

        if (Math.abs(normal[0]) > Math.abs(normal[2])) {
          // Vertical partition (X-axis aligned)
          planeGeometry = new THREE.PlaneGeometry(sizeZ * 0.95, wallHeight);
          position = new THREE.Vector3(centerX, wallHeight / 2, centerZ);
          rotation = new THREE.Euler(0, Math.PI / 2, 0);
          labelPosition = new THREE.Vector3(centerX, wallHeight + 1, centerZ);
        } else {
          // Horizontal partition (Z-axis aligned)
          planeGeometry = new THREE.PlaneGeometry(sizeX * 0.95, wallHeight);
          position = new THREE.Vector3(centerX, wallHeight / 2, centerZ);
          rotation = new THREE.Euler(0, 0, 0);
          labelPosition = new THREE.Vector3(centerX, wallHeight + 1, centerZ);
        }

        const partitionMaterial = new THREE.MeshStandardMaterial({
          color: partitionColor,
          transparent: true,
          opacity: 0.5,
          side: THREE.DoubleSide,
          emissive: partitionColor,
          emissiveIntensity: 0.5,
          metalness: 0.3,
          roughness: 0.4,
          envMapIntensity: 1.0,
        });

        const partition = new THREE.Mesh(planeGeometry, partitionMaterial);
        partition.position.copy(position);
        partition.rotation.copy(rotation);
        partition.userData = {
          type: 'partition',
          strategy: node.partition.strategy,
          depth: node.depth,
          bounds: { minX, maxX, minZ, maxZ }
        };
        floorGroup.add(partition);
        partitionPlanesRef.current.push(partition);

        // Add partition strategy label
        const strategyLabel = createTextLabel(
          node.partition.strategy,
          labelPosition,
          partitionColor,
          0.8
        );
        if (strategyLabel) floorGroup.add(strategyLabel);

        // Recursively build left and right children
        const childFloorGroup = floorGroupsRef.current[depth + 1];

        if (node.left) {
          const leftBounds = Math.abs(normal[0]) > Math.abs(normal[2])
            ? { minX, maxX: centerX, minZ, maxZ }
            : { minX, maxX, minZ, maxZ: centerZ };

          // Draw connection line to child (icicle-style)
          if (childFloorGroup) {
            const leftCenterX = (leftBounds.minX + leftBounds.maxX) / 2;
            const leftCenterZ = (leftBounds.minZ + leftBounds.maxZ) / 2;

            const points = [
              new THREE.Vector3(centerX, wallHeight, centerZ),
              new THREE.Vector3(leftCenterX, 15, leftCenterZ) // 15 units to next floor
            ];
            const lineGeometry = new THREE.BufferGeometry().setFromPoints(points);
            const lineMaterial = new THREE.LineBasicMaterial({
              color: partitionColor,
              opacity: 0.3,
              transparent: true
            });
            const line = new THREE.Line(lineGeometry, lineMaterial);
            floorGroup.add(line);
          }

          buildNode(node.left, depth + 1, leftBounds);
        }

        if (node.right) {
          const rightBounds = Math.abs(normal[0]) > Math.abs(normal[2])
            ? { minX: centerX, maxX, minZ, maxZ }
            : { minX, maxX, minZ: centerZ, maxZ };

          // Draw connection line to child (icicle-style)
          if (childFloorGroup) {
            const rightCenterX = (rightBounds.minX + rightBounds.maxX) / 2;
            const rightCenterZ = (rightBounds.minZ + rightBounds.maxZ) / 2;

            const points = [
              new THREE.Vector3(centerX, wallHeight, centerZ),
              new THREE.Vector3(rightCenterX, 15, rightCenterZ) // 15 units to next floor
            ];
            const lineGeometry = new THREE.BufferGeometry().setFromPoints(points);
            const lineMaterial = new THREE.LineBasicMaterial({
              color: partitionColor,
              opacity: 0.3,
              transparent: true
            });
            const line = new THREE.Line(lineGeometry, lineMaterial);
            floorGroup.add(line);
          }

          buildNode(node.right, depth + 1, rightBounds);
        }
      }
    };

    // Start building from root with initial bounds
    buildNode(treeData.root, 0, { minX: -50, maxX: 50, minZ: -50, maxZ: 50 });
  };

  // ==================
  // Input Handling
  // ==================

  useEffect(() => {
    const handleKeyDown = (e: KeyboardEvent) => {
      switch (e.code) {
        case 'KeyW': keyStateRef.current.forward = true; break;
        case 'KeyS': keyStateRef.current.backward = true; break;
        case 'KeyA': keyStateRef.current.left = true; break;
        case 'KeyD': keyStateRef.current.right = true; break;
        case 'Space': keyStateRef.current.up = true; break;
        case 'ShiftLeft': keyStateRef.current.down = true; break;
      }
    };

    const handleKeyUp = (e: KeyboardEvent) => {
      switch (e.code) {
        case 'KeyW': keyStateRef.current.forward = false; break;
        case 'KeyS': keyStateRef.current.backward = false; break;
        case 'KeyA': keyStateRef.current.left = false; break;
        case 'KeyD': keyStateRef.current.right = false; break;
        case 'Space': keyStateRef.current.up = false; break;
        case 'ShiftLeft': keyStateRef.current.down = false; break;
      }
    };

    window.addEventListener('keydown', handleKeyDown);
    window.addEventListener('keyup', handleKeyUp);

    return () => {
      window.removeEventListener('keydown', handleKeyDown);
      window.removeEventListener('keyup', handleKeyUp);
    };
  }, []);

  // Mouse look (Pointer Lock API)
  useEffect(() => {
    const handlePointerLockChange = () => {
      isPointerLockedRef.current = document.pointerLockElement === containerRef.current;
    };

    const handleMouseMove = (e: MouseEvent) => {
      // Track absolute mouse position for tooltip (even when not locked)
      if (containerRef.current) {
        const rect = containerRef.current.getBoundingClientRect();
        setMousePosition({
          x: e.clientX - rect.left,
          y: e.clientY - rect.top
        });
      }

      // Track movement for camera rotation (only when locked)
      if (!isPointerLockedRef.current) return;

      mouseMovementRef.current.x = e.movementX;
      mouseMovementRef.current.y = e.movementY;
    };

    // Touch event handlers for POV orientation (1 finger) and movement (2 fingers)
    const handleTouchStart = (e: TouchEvent) => {
      if (e.touches.length === 1) {
        // Single finger touch - track for camera rotation
        touchStartRef.current = {
          x: e.touches[0].clientX,
          y: e.touches[0].clientY,
        };
        // Clear 2-finger tracking
        twoFingerTouchStartRef.current = null;
      } else if (e.touches.length === 2) {
        // Two finger touch - track center point for movement
        const centerX = (e.touches[0].clientX + e.touches[1].clientX) / 2;
        const centerY = (e.touches[0].clientY + e.touches[1].clientY) / 2;
        twoFingerTouchStartRef.current = {
          x: centerX,
          y: centerY,
        };
        // Clear 1-finger tracking
        touchStartRef.current = null;
      }
    };

    const handleTouchMove = (e: TouchEvent) => {
      if (e.touches.length === 1 && touchStartRef.current) {
        // Single finger - camera rotation
        const deltaX = e.touches[0].clientX - touchStartRef.current.x;
        const deltaY = e.touches[0].clientY - touchStartRef.current.y;

        // Store movement for camera rotation (scaled for touch sensitivity)
        touchMovementRef.current.x = deltaX * 0.5; // Reduce sensitivity
        touchMovementRef.current.y = deltaY * 0.5;

        // Update touch start position for continuous dragging
        touchStartRef.current = {
          x: e.touches[0].clientX,
          y: e.touches[0].clientY,
        };

        // Prevent default to avoid scrolling
        e.preventDefault();
      } else if (e.touches.length === 2 && twoFingerTouchStartRef.current) {
        // Two fingers - movement (walk)
        const centerX = (e.touches[0].clientX + e.touches[1].clientX) / 2;
        const centerY = (e.touches[0].clientY + e.touches[1].clientY) / 2;

        const deltaX = centerX - twoFingerTouchStartRef.current.x;
        const deltaY = centerY - twoFingerTouchStartRef.current.y;

        // Store movement for walking (scaled for movement sensitivity)
        twoFingerMovementRef.current.x = deltaX * 0.3; // Forward/backward
        twoFingerMovementRef.current.y = deltaY * 0.3; // Strafe left/right

        // Update touch start position for continuous movement
        twoFingerTouchStartRef.current = {
          x: centerX,
          y: centerY,
        };

        // Prevent default to avoid scrolling
        e.preventDefault();
      }
    };

    const handleTouchEnd = () => {
      touchStartRef.current = null;
      touchMovementRef.current = { x: 0, y: 0 };
      twoFingerTouchStartRef.current = null;
      twoFingerMovementRef.current = { x: 0, y: 0 };
    };

    const handleClick = () => {
      if (containerRef.current && !isPointerLockedRef.current) {
        containerRef.current.requestPointerLock();
      } else if (isPointerLockedRef.current && hoveredElement) {
        // Check if clicking on a PORTAL - navigate to another visualization
        if (hoveredElement.type === 'portal' || (hoveredElement as Record<string, unknown>).userData?.isPortal) {
          const portalData = (hoveredElement as Record<string, unknown>).userData || hoveredElement;
          const targetUrl = portalData.targetUrl;
          if (targetUrl) {
            console.log(`🌀 Portal activated: ${portalData.label} → ${targetUrl}`);
            // Navigate to the target URL
            window.location.href = targetUrl;
          }
          return;
        }
        // Check if clicking on a DOOR - enter the sub-room (like zoomable sunburst)
        if (hoveredElement.type === 'door') {
          const doorData = hoveredElement as Record<string, unknown>;

          // Push current room to navigation stack
          setNavigationStack(prev => [...prev, {
            name: doorData.name,
            floor: doorData.targetFloor ?? currentFloor + 1,
            parent: currentRoom,
          }]);

          // Enter the new room
          setCurrentRoom(doorData.name);

          // Move to target floor if specified
          if (doorData.targetFloor !== undefined) {
            setCurrentFloor(doorData.targetFloor);

            // Update camera position to new floor
            if (cameraRef.current && playerStateRef.current) {
              const targetY = doorData.targetFloor * 20 + 18;
              playerStateRef.current.position.y = targetY;
              cameraRef.current.position.y = targetY;
            }
          }

          // Set scope filter to show children of this door
          if (doorData.children && doorData.children.length > 0) {
            setScopeFilter({
              floor: doorData.targetFloor ?? currentFloor + 1,
              group: doorData.name
            });
          }

          // Clear existing floor and create sub-room with new doors
          const targetFloor = doorData.targetFloor ?? currentFloor;
          if (floorGroupsRef.current[targetFloor]) {
            // Remove all existing objects except terrain
            const floorGroup = floorGroupsRef.current[targetFloor];
            const objectsToRemove = floorGroup.children.filter(
              child => child.userData.type === 'door' ||
                       child.userData.type === 'element' ||
                       child.userData.type === 'group'
            );
            objectsToRemove.forEach(obj => floorGroup.remove(obj));

            // Generate sub-doors based on the parent door
            const subDoors = generateRoomHierarchy(targetFloor, doorData.name);

            if (subDoors.length > 0) {
              // Create new room with sub-doors
              createRoomWithDoors(
                doorData.name,
                subDoors,
                floorGroup,
                new THREE.Vector3(0, 0, 0)
              );
              console.log(`🚪 Entered room: ${doorData.name} with ${subDoors.length} sub-doors`);
            } else {
              // No sub-doors, show the actual elements
              console.log(`🚪 Entered room: ${doorData.name} (${doorData.children?.length || 0} items)`);
            }
          }
        }
        // Check if clicking on a group - set scope filter to show elements in that group
        else if (hoveredElement.type === 'group') {
          const groupData = hoveredElement as Record<string, unknown>;
          setScopeFilter({
            floor: (groupData.floorIndex as number) || currentFloor,
            group: groupData.name as string
          });
          console.log(`🎯 Scope filter set: Floor ${groupData.floorIndex}, Group "${groupData.name}"`);
        }
        // Check if clicking on an element - drill down to next floor in hierarchy
        else if (hoveredElement.type === 'element' || hoveredElement.type === 'sample') {
          const elementData = hoveredElement as Record<string, unknown>;

          // Determine next floor based on current floor
          // Floor hierarchy: 0=Pitch Class Sets → 1=Forte Codes → 2=Prime Forms → 3=Chords → 4=Inversions → 5=Voicings
          const nextFloor = currentFloor + 1;

          if (nextFloor < 6) {
            // Move to next floor and filter by this element's group
            setCurrentFloor(nextFloor);

            // Set scope filter to show related items on next floor
            // Use the element's group name as the filter
            if (elementData.groupName) {
              setScopeFilter({
                floor: nextFloor,
                group: elementData.groupName
              });
              console.log(`🎯 Drilling down: ${elementData.name} → Floor ${nextFloor}, Group "${elementData.groupName}"`);
            }

            // Update camera position to new floor
            if (cameraRef.current && playerStateRef.current) {
              const targetY = nextFloor * 20 + 18; // Floor base + max elevation
              playerStateRef.current.position.y = targetY;
              cameraRef.current.position.y = targetY;
            }
          } else {
            console.log(`⚠️ Already at deepest floor (Voicings)`);
          }

          // Also select the element for detail view
          setSelectedElement(hoveredElement);
        }
        else {
          // Select the hovered element when clicking while pointer is locked
          setSelectedElement(hoveredElement);
        }
      }
    };

    // Handle keyboard shortcuts for navigation
    const handleKeyDown = (e: KeyboardEvent) => {
      // Backspace or Escape - go back up the navigation stack
      if ((e.key === 'Backspace' || e.key === 'Escape') && navigationStack.length > 1) {
        e.preventDefault();

        // Pop the current room from the stack
        setNavigationStack(prev => {
          const newStack = [...prev];
          newStack.pop();

          // Get the parent room
          const parentRoom = newStack[newStack.length - 1];
          setCurrentRoom(parentRoom.name);

          // Move back to parent floor
          if (parentRoom.floor !== undefined) {
            setCurrentFloor(parentRoom.floor);

            // Update camera position
            if (cameraRef.current && playerStateRef.current) {
              const targetY = parentRoom.floor * 20 + 18;
              playerStateRef.current.position.y = targetY;
              cameraRef.current.position.y = targetY;
            }
          }

          // Clear scope filter when going back to root
          if (newStack.length === 1) {
            setScopeFilter(null);
          }

          console.log(`⬅️ Back to room: ${parentRoom.name}`);
          return newStack;
        });
      }
    };

    document.addEventListener('pointerlockchange', handlePointerLockChange);
    document.addEventListener('mousemove', handleMouseMove);
    document.addEventListener('keydown', handleKeyDown);
    containerRef.current?.addEventListener('click', handleClick);

    // Add touch event listeners
    containerRef.current?.addEventListener('touchstart', handleTouchStart, { passive: false });
    containerRef.current?.addEventListener('touchmove', handleTouchMove, { passive: false });
    containerRef.current?.addEventListener('touchend', handleTouchEnd);

    const currentContainer = containerRef.current;

    return () => {
      document.removeEventListener('pointerlockchange', handlePointerLockChange);
      document.removeEventListener('mousemove', handleMouseMove);
      document.removeEventListener('keydown', handleKeyDown);
      currentContainer?.removeEventListener('click', handleClick);

      // Remove touch event listeners
      currentContainer?.removeEventListener('touchstart', handleTouchStart);
      currentContainer?.removeEventListener('touchmove', handleTouchMove);
      currentContainer?.removeEventListener('touchend', handleTouchEnd);
    };
  }, [navigationStack, currentRoom]);

  // Auto-navigation demo mode - walk around floor then change
  useEffect(() => {
    if (!autoNavigate) return;

    let walkSteps = 0;
    const stepsPerFloor = 10; // Walk 10 steps before changing floor
    const stepInterval = 500; // 500ms per step

    const interval = setInterval(() => {
      if (walkSteps < stepsPerFloor) {
        // Walk around current floor
        if (playerStateRef.current && cameraRef.current) {
          // Random walk direction
          const angle = Math.random() * Math.PI * 2;
          const distance = 5 + Math.random() * 10;

          playerStateRef.current.position.x += Math.cos(angle) * distance;
          playerStateRef.current.position.z += Math.sin(angle) * distance;

          // Clamp to floor bounds
          playerStateRef.current.position.x = Math.max(-40, Math.min(40, playerStateRef.current.position.x));
          playerStateRef.current.position.z = Math.max(-40, Math.min(40, playerStateRef.current.position.z));

          // Update camera position
          cameraRef.current.position.x = playerStateRef.current.position.x;
          cameraRef.current.position.z = playerStateRef.current.position.z;

          // Rotate camera to look at center
          const lookAtPoint = new THREE.Vector3(0, playerStateRef.current.position.y, 0);
          cameraRef.current.lookAt(lookAtPoint);
        }
        walkSteps++;
      } else {
        // Change floor after walking
        walkSteps = 0;
        setCurrentFloor(prev => {
          // Safety check: ensure floor groups exist
          if (!floorGroupsRef.current || floorGroupsRef.current.length === 0) {
            return prev;
          }

          const maxFloor = Math.min(floorGroupsRef.current.length - 1, treeInfo?.maxDepth || 5);
          const nextFloor = (prev + 1) % (maxFloor + 1); // Cycle through floors

          // Smoothly transition camera to new floor height at maximum elevation
          if (cameraRef.current && playerStateRef.current) {
            const targetY = nextFloor * 20 + 3 + 15; // Floor base + eye height + max terrain elevation
            playerStateRef.current.position.y = targetY;
            cameraRef.current.position.y = targetY;

            // Reset to center of floor
            playerStateRef.current.position.x = 0;
            playerStateRef.current.position.z = 0;
            cameraRef.current.position.x = 0;
            cameraRef.current.position.z = 0;
          }

          return nextFloor;
        });
      }
    }, stepInterval);

    return () => clearInterval(interval);
  }, [autoNavigate, treeInfo]);

  // Mouse wheel for floor navigation
  useEffect(() => {
    const handleWheel = (e: WheelEvent) => {
      e.preventDefault();

      setCurrentFloor(prev => {
        // Safety check: ensure floor groups exist
        if (!floorGroupsRef.current || floorGroupsRef.current.length === 0) {
          return prev;
        }

        const maxFloor = Math.min(floorGroupsRef.current.length - 1, treeInfo?.maxDepth || 5);
        const newFloor = prev + (e.deltaY > 0 ? 1 : -1);
        const clampedFloor = Math.max(0, Math.min(maxFloor, newFloor));

        // Smoothly transition camera to new floor height at MAXIMUM ELEVATION
        // Floors are spaced 20 units apart, camera at eye height (3m) + max terrain elevation (15m)
        if (cameraRef.current && playerStateRef.current) {
          const targetY = clampedFloor * 20 + 3 + 15; // Floor base + eye height + max terrain elevation
          playerStateRef.current.position.y = targetY;
          cameraRef.current.position.y = targetY;

          // Trigger atmosphere transition (fog, skybox, ambient light)
          transitionToFloor(clampedFloor);

          if (import.meta.env.DEV) {
            console.log(`🏢 Floor navigation: ${prev} → ${clampedFloor}, Camera Y: ${targetY.toFixed(1)}`);
          }
        }

        // DO NOT hide floors - all floors should remain visible
        // The highlighting is handled in the animation loop based on currentFloor state

        return clampedFloor;
      });
    };

    const container = containerRef.current;
    container?.addEventListener('wheel', handleWheel, { passive: false });

    return () => {
      container?.removeEventListener('wheel', handleWheel);
    };
  }, [treeInfo]);

  // Update floor visibility when currentFloor changes (PERFORMANCE: Only show current floor)
  useEffect(() => {
    if (!floorGroupsRef.current || floorGroupsRef.current.length === 0) return;

    // Show only the current floor, hide all others
    floorGroupsRef.current.forEach((floorGroup, floorIndex) => {
      floorGroup.visible = floorIndex === currentFloor;
    });
  }, [currentFloor]);

  // ==================
  // 3D Floor Navigator Widget
  // ==================

  const initFloorNavigator = useCallback(() => {
    const canvas = floorNavCanvasRef.current;
    if (!canvas) return;

    // Create renderer
    const renderer = new THREE.WebGLRenderer({
      canvas,
      antialias: true,
      alpha: true,
    });
    renderer.setSize(200, 200);
    renderer.setPixelRatio(window.devicePixelRatio);
    floorNavRendererRef.current = renderer;

    // Create scene
    const scene = new THREE.Scene();
    floorNavSceneRef.current = scene;

    // Create orthographic camera (top-down view)
    const camera = new THREE.OrthographicCamera(-5, 5, 5, -5, 0.1, 100);
    camera.position.set(5, 10, 5); // Isometric angle
    camera.lookAt(0, 0, 0);
    floorNavCameraRef.current = camera;

    // Add ambient light
    const ambientLight = new THREE.AmbientLight(0x00ff00, 0.5);
    scene.add(ambientLight);

    // Add directional light
    const dirLight = new THREE.DirectionalLight(0x00ff00, 0.8);
    dirLight.position.set(5, 10, 5);
    scene.add(dirLight);

    // Create SMOOTH PYRAMID visualization (not stepped)
    const pyramidBaseSize = 4.0;
    const pyramidHeight = 7.5;
    const halfBase = pyramidBaseSize / 2;

    // Create 4 triangular faces for smooth pyramid
    const pyramidFaces = [
      // Back face
      [[-halfBase, 0, -halfBase], [halfBase, 0, -halfBase], [0, pyramidHeight, 0]],
      // Front face
      [[-halfBase, 0, halfBase], [halfBase, 0, halfBase], [0, pyramidHeight, 0]],
      // Left face
      [[-halfBase, 0, -halfBase], [-halfBase, 0, halfBase], [0, pyramidHeight, 0]],
      // Right face
      [[halfBase, 0, -halfBase], [halfBase, 0, halfBase], [0, pyramidHeight, 0]],
    ];

    pyramidFaces.forEach((faceCorners) => {
      const vertices = new Float32Array([
        ...faceCorners[0], ...faceCorners[1], ...faceCorners[2],
      ]);

      const geometry = new THREE.BufferGeometry();
      geometry.setAttribute('position', new THREE.BufferAttribute(vertices, 3));
      geometry.computeVertexNormals();

      const material = new THREE.MeshPhysicalMaterial({
        color: 0x00ff00,
        emissive: 0x00ff00,
        emissiveIntensity: 0.3,
        metalness: 0.3,
        roughness: 0.6,
        transparent: true,
        opacity: 0.4,
        side: THREE.DoubleSide,
      });

      const mesh = new THREE.Mesh(geometry, material);
      scene.add(mesh);
    });

    // Add floor indicator discs inside pyramid
    const floorSpacing = pyramidHeight / 6;
    for (let i = 0; i < 6; i++) {
      const floorY = i * floorSpacing + 0.1;
      const floorRatio = i / 5; // 0 at bottom, 1 at top
      const floorSize = pyramidBaseSize * (1 - floorRatio * 0.8); // Shrinks towards top

      const atmosphere = FLOOR_ATMOSPHERES[i];
      const geometry = new THREE.CircleGeometry(floorSize / 2, 32);
      geometry.rotateX(-Math.PI / 2);

      const material = new THREE.MeshPhysicalMaterial({
        color: atmosphere.floorColor,
        emissive: atmosphere.emissiveColor,
        emissiveIntensity: i === currentFloor ? 0.8 : 0.2,
        metalness: 0.3,
        roughness: 0.6,
        transparent: true,
        opacity: i === currentFloor ? 0.9 : 0.3,
      });

      const mesh = new THREE.Mesh(geometry, material);
      mesh.position.y = floorY;
      mesh.userData = { floorIndex: i };
      scene.add(mesh);

      // Add floor label (text sprite)
      const canvas = document.createElement('canvas');
      const context = canvas.getContext('2d')!;
      canvas.width = 128;
      canvas.height = 32;
      context.font = 'bold 20px monospace';
      context.textAlign = 'center';
      context.textBaseline = 'middle';

      // Draw outline first
      context.strokeStyle = '#000';
      context.lineWidth = 3;
      context.lineJoin = 'round';
      context.strokeText(`F${i}`, 64, 16);

      // Draw fill text
      context.fillStyle = '#0f0';
      context.fillText(`F${i}`, 64, 16);

      const texture = new THREE.CanvasTexture(canvas);
      const spriteMaterial = new THREE.SpriteMaterial({ map: texture, transparent: true });
      const sprite = new THREE.Sprite(spriteMaterial);
      // INVERTED: Use same Y position as the floor mesh
      sprite.position.set(floorSize / 2 + 0.5, (5 - i) * floorSpacing, 0);
      sprite.scale.set(1, 0.25, 1);
      scene.add(sprite);
    }

    // Animation loop for floor navigator
    const animate = () => {
      if (!floorNavRendererRef.current || !floorNavSceneRef.current || !floorNavCameraRef.current) return;

      // Update floor highlighting based on currentFloor
      floorNavSceneRef.current.traverse((obj) => {
        if (obj instanceof THREE.Mesh && obj.userData.floorIndex !== undefined) {
          const material = obj.material as THREE.MeshPhysicalMaterial;
          const isCurrentFloor = obj.userData.floorIndex === currentFloor;
          material.emissiveIntensity = isCurrentFloor ? 0.8 : 0.2;
          material.opacity = isCurrentFloor ? 1.0 : 0.6;
        }
      });

      floorNavRendererRef.current.render(floorNavSceneRef.current, floorNavCameraRef.current);
      floorNavAnimationRef.current = requestAnimationFrame(animate);
    };

    animate();
  }, [currentFloor]);

  // Initialize floor navigator when component mounts
  useEffect(() => {
    if (!isLoading && floorNavCanvasRef.current) {
      initFloorNavigator();
    }

    return () => {
      if (floorNavAnimationRef.current) {
        cancelAnimationFrame(floorNavAnimationRef.current);
      }
      if (floorNavRendererRef.current) {
        floorNavRendererRef.current.dispose();
      }
    };
  }, [isLoading, initFloorNavigator]);

  // Update floor navigator when currentFloor changes
  useEffect(() => {
    if (floorNavSceneRef.current) {
      floorNavSceneRef.current.traverse((obj) => {
        if (obj instanceof THREE.Mesh && obj.userData.floorIndex !== undefined) {
          const material = obj.material as THREE.MeshPhysicalMaterial;
          const isCurrentFloor = obj.userData.floorIndex === currentFloor;
          material.emissiveIntensity = isCurrentFloor ? 0.8 : 0.2;
          material.opacity = isCurrentFloor ? 1.0 : 0.6;
        }
      });
    }
  }, [currentFloor]);

  // ==================
  // Animation Loop
  // ==================

  const startAnimationLoop = () => {
    const animate = () => {
      try {
        if (!sceneRef.current || !cameraRef.current || !rendererRef.current) return;

        const delta = clockRef.current.getDelta(); // Get time delta for smooth animations
        const time = clockRef.current.getElapsedTime();

        // Update atmosphere transition (smooth fog/skybox color changes)
        updateAtmosphereTransition(delta);

        // Update player movement
        updatePlayerMovement(delta);

        // Update camera rotation (mouse look)
        updateCameraRotation();

        // Update camera position
        if (playerStateRef.current && playerStateRef.current.position && playerStateRef.current.rotation) {
          cameraRef.current.position.copy(playerStateRef.current.position);
          cameraRef.current.rotation.copy(playerStateRef.current.rotation);
        }

        // Detect current region (with error handling)
        try {
          detectCurrentRegion();
        } catch {
          // Silently ignore region detection errors
        }

        // Detect hovered element (with error handling)
        try {
          detectHoveredElement();
        } catch {
          // Silently ignore hover detection errors
        }

        // Update ocean shader time (Gerstner waves) if present
        if (sceneRef.current) {
          sceneRef.current.traverse((obj) => {
            if (obj instanceof THREE.Mesh && obj.material instanceof THREE.ShaderMaterial) {
              const material = obj.material as THREE.ShaderMaterial;
              if (material.uniforms && material.uniforms.time && material.uniforms.waterTint) {
                // This is the ocean material - update time for wave animation
                material.uniforms.time.value = time;
              }
            }
          });
        }

        // Update portals
        portalsRef.current.forEach(portal => {
          portal.update(delta);
        });

        // Animate 3D bracelet diagram (rotation)
        if (activeBraceletRef.current) {
          activeBraceletRef.current.rotation.y += delta * 0.5; // Slow rotation

          // Fade in/out animation
          const fadeSpeed = 3.0;
          const currentOpacity = braceletFadeRef.current.opacity;
          const targetOpacity = braceletFadeRef.current.targetOpacity;

          if (Math.abs(currentOpacity - targetOpacity) > 0.01) {
            braceletFadeRef.current.opacity += (targetOpacity - currentOpacity) * fadeSpeed * delta;

            // Update opacity of all materials in bracelet
            activeBraceletRef.current.traverse((obj) => {
              if (obj instanceof THREE.Mesh) {
                const material = obj.material as THREE.MeshPhysicalMaterial;
                if (material && material.transparent !== undefined) {
                  material.opacity = braceletFadeRef.current.opacity;
                  material.transparent = true;
                }
              }
            });
          }
        }

        // Animate sample elements and add pulsing glow to platforms
        if (bspWorldRef.current) {
          const time = clockRef.current.getElapsedTime();

          bspWorldRef.current.traverse((obj) => {
            // Rotate sample elements with varied speeds (but NOT trees)
            if (obj.userData && obj.userData.type === 'sample' && !obj.userData.isTree) {
              const speed = obj.userData.rotationSpeed || 0.5;
              const axis = obj.userData.rotationAxis || 'y';

              if (axis === 'y') {
                obj.rotation.y += delta * speed;
                obj.rotation.x += delta * speed * 0.6;
              } else {
                obj.rotation.x += delta * speed;
                obj.rotation.y += delta * speed * 0.6;
              }

              // Add subtle floating animation
              const floatOffset = Math.sin(time * 2 + obj.position.x) * 0.2;
              obj.position.y = 3.5 + floatOffset;
            }

            // Pulse group platforms with varied timing + breathing animation
            if (obj.userData && obj.userData.type === 'group') {
              const mesh = obj as THREE.Mesh;
              const material = mesh.material as THREE.MeshStandardMaterial;

              // Emissive intensity pulse
              if (material && material.emissiveIntensity !== undefined) {
                // Subtle pulsing effect with position-based phase offset
                const baseIntensity = 0.35;
                const pulseAmount = 0.2;
                const phaseOffset = (obj.position.x + obj.position.z) * 0.1;
                material.emissiveIntensity = baseIntensity + Math.sin(time * 1.5 + phaseOffset) * pulseAmount;
              }

              // Platform breathing (subtle scale animation)
              if (obj.userData.baseScale && obj.userData.breathingPhase !== undefined) {
                const breathingSpeed = 0.8; // Slow, calm breathing
                const breathingAmount = 0.02; // Very subtle (2% scale change)
                const phase = obj.userData.breathingPhase;
                const scale = 1.0 + Math.sin(time * breathingSpeed + phase) * breathingAmount;
                mesh.scale.set(scale, scale, scale);
              }
            }
          });
        }

        // Update sky position for parallax effect (skybox follows camera)
        if (skyRef.current && cameraRef.current) {
          skyRef.current.position.copy(cameraRef.current.position);
        }

        // Update cloud layers - drift and parallax
        if (cloudLayersRef.current.length > 0 && cameraRef.current) {
          const t = performance.now() * 0.001; // time in seconds

          cloudLayersRef.current.forEach((cloud) => {
            // Parallax effect - clouds follow camera slowly to emulate distance
            cloud.position.x = cameraRef.current!.position.x * 0.4;
            cloud.position.z = cameraRef.current!.position.z * 0.4;

            // UV drift for cloud movement
            const speed = cloud.userData.cloudSpeed || 0.002;
            const material = cloud.material as THREE.MeshBasicMaterial;
            if (material.map) {
              material.map.offset.set(t * speed, t * speed * 0.6);
            }
          });
        }

        // Update MCP objects (rotation)
        mcpObjectsRef.current.forEach((obj) => {
          if (obj.userData.rotationSpeed) {
            obj.rotation.y += obj.userData.rotationSpeed;
          }
        });

        // Render scene
        rendererRef.current.render(sceneRef.current, cameraRef.current);

        // Update FPS counter
        updateFPS();
      } catch (error) {
        // Log error but don't crash the animation loop
        if (import.meta.env.DEV) {
          console.error('Animation loop error:', error);
        }
      }

      animationIdRef.current = requestAnimationFrame(animate);
    };

    animate();
  };

  const updatePlayerMovement = (delta: number) => {
    const player = playerStateRef.current;
    const keys = keyStateRef.current;
    const twoFingerTouch = twoFingerMovementRef.current;
    const speed = moveSpeed * delta;

    // Calculate movement direction based on camera rotation
    const forward = new THREE.Vector3(0, 0, -1);
    const right = new THREE.Vector3(1, 0, 0);

    forward.applyEuler(player.rotation);
    right.applyEuler(player.rotation);

    // Reset velocity
    player.velocity.set(0, 0, 0);

    // Apply keyboard movement
    if (keys.forward) player.velocity.add(forward.multiplyScalar(speed));
    if (keys.backward) player.velocity.add(forward.multiplyScalar(-speed));
    if (keys.left) player.velocity.add(right.multiplyScalar(-speed));
    if (keys.right) player.velocity.add(right.multiplyScalar(speed));
    if (keys.up) player.velocity.y += speed;
    if (keys.down) player.velocity.y -= speed;

    // Apply 2-finger touch movement
    if (twoFingerTouch.x !== 0 || twoFingerTouch.y !== 0) {
      // Touch sensitivity for movement (0.05 units per pixel)
      const touchMoveSensitivity = 0.05;

      // Y movement (up/down on screen) = forward/backward
      // Inverted: drag down = move forward, drag up = move backward
      player.velocity.add(forward.multiplyScalar(-twoFingerTouch.y * touchMoveSensitivity));

      // X movement (left/right on screen) = strafe left/right
      player.velocity.add(right.multiplyScalar(twoFingerTouch.x * touchMoveSensitivity));

      // Reset 2-finger movement after applying
      twoFingerTouch.x = 0;
      twoFingerTouch.y = 0;
    }

    // Apply velocity to position (with collision detection)
    const newPosition = player.position.clone().add(player.velocity);

    // PYRAMID COLLISION: Keep player within pyramid bounds
    // Calculate max Y based on number of floors (6 floors * 20 units + 10 units headroom)
    const maxFloors = floorGroupsRef.current.length || 6;
    const maxY = maxFloors * 20 + 10; // Allow movement above top floor
    newPosition.y = Math.max(1.6, Math.min(maxY, newPosition.y)); // Clamp Y to floor range

    // PYRAMID BOUNDS: Calculate bounds based on current floor
    // Floor sizes range from 50 (floor 0) to 200 (floor 5)
    const currentFloorIndex = Math.floor(newPosition.y / 20);
    const clampedFloorIndex = Math.max(0, Math.min(5, currentFloorIndex));

    // Element counts for pyramid scaling
    const floorElementCounts = [115, 200, 350, 4096, 10000, 100000];
    const minSize = 50;
    const maxSize = 200;
    const logMin = Math.log10(floorElementCounts[0]);
    const logMax = Math.log10(floorElementCounts[5]);

    const calculateFloorSize = (elementCount: number): number => {
      const logCount = Math.log10(elementCount);
      const normalized = (logCount - logMin) / (logMax - logMin);
      return minSize + normalized * (maxSize - minSize);
    };

    const currentFloorSize = calculateFloorSize(floorElementCounts[clampedFloorIndex]);
    const halfSize = currentFloorSize / 2;

    // Clamp X and Z to current floor's pyramid bounds
    newPosition.x = Math.max(-halfSize, Math.min(halfSize, newPosition.x));
    newPosition.z = Math.max(-halfSize, Math.min(halfSize, newPosition.z));

    player.position.copy(newPosition);
  };

  const updateCameraRotation = () => {
    const player = playerStateRef.current;
    const mouse = mouseMovementRef.current;
    const touch = touchMovementRef.current;

    // Apply mouse movement to rotation (pointer lock)
    if (mouse.x !== 0 || mouse.y !== 0) {
      player.rotation.y -= mouse.x * lookSpeed;
      player.rotation.x -= mouse.y * lookSpeed;

      // Clamp vertical rotation (prevent looking too far up/down)
      player.rotation.x = Math.max(-Math.PI / 2, Math.min(Math.PI / 2, player.rotation.x));

      // Update minimap rotation state (throttled to avoid too many re-renders)
      setPlayerRotation(player.rotation.y);

      // Reset mouse movement
      mouse.x = 0;
      mouse.y = 0;
    }

    // Apply touch movement to rotation (touch drag)
    if (touch.x !== 0 || touch.y !== 0) {
      // Touch sensitivity (0.002 radians per pixel)
      const touchSensitivity = 0.002;

      player.rotation.y -= touch.x * touchSensitivity;
      player.rotation.x -= touch.y * touchSensitivity;

      // Clamp vertical rotation (prevent looking too far up/down)
      player.rotation.x = Math.max(-Math.PI / 2, Math.min(Math.PI / 2, player.rotation.x));

      // Update minimap rotation state
      setPlayerRotation(player.rotation.y);

      // Reset touch movement
      touch.x = 0;
      touch.y = 0;
    }
  };

  const detectCurrentRegion = () => {
    if (!bspWorldRef.current || !cameraRef.current || !playerStateRef.current) return;

    const player = playerStateRef.current;
    if (!player.position) return;

    // Raycast downward to detect which region we're in
    const raycaster = new THREE.Raycaster(
      player.position,
      new THREE.Vector3(0, -1, 0),
      0,
      10
    );
    raycaster.camera = cameraRef.current; // Set camera for sprite raycasting

    // Filter out sprites to avoid raycasting errors
    const nonSpriteChildren = bspWorldRef.current.children.filter(
      obj => !(obj instanceof THREE.Sprite)
    );

    if (nonSpriteChildren.length === 0) return;

    const intersects = raycaster.intersectObjects(nonSpriteChildren, true);

    for (const intersect of intersects) {
      if (intersect.object.userData && intersect.object.userData.type === 'region') {
        const regionName = intersect.object.userData.name;

        // Update current region if changed
        if (!currentRegion || currentRegion.name !== regionName) {
          const newRegion: BSPRegion = {
            name: regionName,
            tonalityType: 'Major', // Would be fetched from BSP tree
            tonalCenter: 0,
            pitchClasses: [],
          };

          setCurrentRegion(newRegion);
          playerStateRef.current.currentRegion = newRegion;

          if (onRegionChange) {
            onRegionChange(newRegion);
          }
        }
        break;
      }
    }
  };

  const convertToDetailPanelData = (elementInfo: ElementInfo, floor: number): BSPElementData => {
    const userData = elementInfo.object?.userData || {};

    // Create base data
    const data: BSPElementData = {
      floor,
      name: elementInfo.name,
      group: userData.group,
    };

    // Add floor-specific data
    if (floor <= 2) {
      // Floors 0-2: Pitch Class Sets, Forte Codes, Prime Forms
      data.pitchClassSet = userData.pitchClassSet || 2773; // Default: C major scale
      data.forteCode = userData.forteCode || '7-35';
      data.intervalClassVector = userData.intervalClassVector || [2, 5, 4, 3, 6, 1];
      data.primeForm = userData.primeForm || [0, 1, 3, 5, 6, 8, 10];
    } else if (floor <= 4) {
      // Floors 3-4: Chords and Inversions
      data.chordSymbol = userData.chordSymbol || elementInfo.name;
      data.chordQuality = userData.chordQuality || 'Major';
      data.rootNote = userData.rootNote || 'C';
      data.inversion = userData.inversion || 0;
      data.notes = userData.notes || ['C', 'E', 'G'];
      data.voicing = userData.voicing || [3, 3, 2, 0, 1, 0]; // Default C chord voicing
    } else {
      // Floor 5: Voicings
      data.voicing = userData.voicing || [3, 3, 2, 0, 1, 0];
      data.cagedShape = userData.cagedShape || 'C';
      data.fretRange = userData.fretRange || { min: 0, max: 3 };
      data.difficulty = userData.difficulty || 'Easy';
      data.tabNotation = userData.tabNotation || '6/3 5/3 4/2 3/0 2/1 1/0'; // VexTab format
      data.chordSymbol = userData.chordSymbol || elementInfo.name;
      data.notes = userData.notes || ['C', 'E', 'G', 'C', 'E'];
    }

    return data;
  };

  const detectHoveredElement = () => {
    if (!cameraRef.current || !sceneRef.current) return;

    // Safety check: ensure floor groups exist
    if (!floorGroupsRef.current || floorGroupsRef.current.length === 0) return;
    if (currentFloor < 0 || currentFloor >= floorGroupsRef.current.length) return;

    // Raycast from center of screen (crosshair position)
    const raycaster = raycasterRef.current;
    if (!raycaster) return;

    raycaster.camera = cameraRef.current; // Set camera for sprite raycasting
    raycaster.setFromCamera(new THREE.Vector2(0, 0), cameraRef.current);

    // Get current floor group
    const currentFloorGroup = floorGroupsRef.current[currentFloor];
    if (!currentFloorGroup || !currentFloorGroup.children) return;

    // Intersect with objects in current floor (exclude sprites to avoid errors)
    const nonSpriteChildren = currentFloorGroup.children.filter(obj => !(obj instanceof THREE.Sprite));
    if (nonSpriteChildren.length === 0) {
      setHoveredElement(null);
      setDetailPanelData(null);
      return;
    }

    const intersects = raycaster.intersectObjects(nonSpriteChildren, true);

    if (intersects.length > 0) {
      const firstIntersect = intersects[0];
      const userData = firstIntersect.object.userData;

      // Show info for elements, groups, regions, and partitions
      if (userData && (userData.type === 'element' || userData.type === 'sample' || userData.type === 'group')) {
        const elementInfo: ElementInfo = {
          type: userData.type,
          name: userData.name || 'Unknown',
          tonalityType: userData.tonalityType,
          strategy: userData.strategy,
          depth: userData.depth,
          distance: firstIntersect.distance,
          object: firstIntersect.object,
        };

        setHoveredElement(elementInfo);

        // Show 3D Bracelet Diagram for pitch class sets
        if (userData.pitchClasses && Array.isArray(userData.pitchClasses) && sceneRef.current) {
          // Remove previous bracelet
          if (activeBraceletRef.current) {
            sceneRef.current.remove(activeBraceletRef.current);
            activeBraceletRef.current.traverse((obj) => {
              if (obj instanceof THREE.Mesh) {
                obj.geometry.dispose();
                if (Array.isArray(obj.material)) {
                  obj.material.forEach(m => m.dispose());
                } else {
                  obj.material.dispose();
                }
              }
            });
            activeBraceletRef.current = null;
          }

          // Create new bracelet above the hovered object
          const objectPosition = firstIntersect.object.position;
          const braceletPosition = new THREE.Vector3(
            objectPosition.x,
            objectPosition.y + 5, // Float above object
            objectPosition.z
          );

          const bracelet = create3DBraceletDiagram(userData.pitchClasses, 1.5, braceletPosition);
          sceneRef.current.add(bracelet);
          activeBraceletRef.current = bracelet;
          braceletFadeRef.current.targetOpacity = 1;
        }

        // Create 3D detail card above the hovered object
        if (sceneRef.current && firstIntersect.object) {
          // Remove previous detail card
          if (hoverDetailCardRef.current) {
            sceneRef.current.remove(hoverDetailCardRef.current);
            if (hoverDetailCardRef.current.material.map) {
              hoverDetailCardRef.current.material.map.dispose();
            }
            hoverDetailCardRef.current.material.dispose();
            hoverDetailCardRef.current = null;
          }

          // Create new detail card
          const objectPosition = firstIntersect.object.position;
          const cardPosition = new THREE.Vector3(
            objectPosition.x,
            objectPosition.y + 8,
            objectPosition.z
          );

          const details: string[] = [];
          if (userData.type === 'group') {
            details.push(`Type: Group`);
            if (userData.groupData?.elements) {
              details.push(`${userData.groupData.elements.length} elements`);
            }
            if (userData.floorName) {
              details.push(`Floor: ${userData.floorName}`);
            }
          } else {
            details.push(`Type: ${userData.type}`);
            if (userData.tonalityType) {
              details.push(`Tonality: ${userData.tonalityType}`);
            }
            if (userData.depth !== undefined) {
              details.push(`Depth: ${userData.depth}`);
            }
          }

          const detailCard = createDetailCard(
            elementInfo.name,
            details,
            cardPosition,
            userData.type === 'group' ? 0x00ff00 : 0xffff00,
            0.6
          );

          if (detailCard) {
            hoverDetailCardRef.current = detailCard;
            sceneRef.current.add(detailCard);
          }
        }

        // Only show detail panel for sample elements (not groups or partitions)
        if (userData.type === 'sample' || userData.type === 'element') {
          setDetailPanelData(convertToDetailPanelData(elementInfo, currentFloor));
        } else {
          setDetailPanelData(null);
        }
      } else {
        setHoveredElement(null);
        setDetailPanelData(null);

        // Remove bracelet when not hovering
        if (activeBraceletRef.current && sceneRef.current) {
          sceneRef.current.remove(activeBraceletRef.current);
          activeBraceletRef.current.traverse((obj) => {
            if (obj instanceof THREE.Mesh) {
              obj.geometry.dispose();
              if (Array.isArray(obj.material)) {
                obj.material.forEach(m => m.dispose());
              } else {
                obj.material.dispose();
              }
            }
          });
          activeBraceletRef.current = null;
          braceletFadeRef.current.targetOpacity = 0;
        }

        // Remove detail card when not hovering
        if (hoverDetailCardRef.current && sceneRef.current) {
          sceneRef.current.remove(hoverDetailCardRef.current);
          if (hoverDetailCardRef.current.material.map) {
            hoverDetailCardRef.current.material.map.dispose();
          }
          hoverDetailCardRef.current.material.dispose();
          hoverDetailCardRef.current = null;
        }
      }
    } else {
      setHoveredElement(null);
      setDetailPanelData(null);

      // Remove bracelet when nothing is hovered
      if (activeBraceletRef.current && sceneRef.current) {
        sceneRef.current.remove(activeBraceletRef.current);
        activeBraceletRef.current.traverse((obj) => {
          if (obj instanceof THREE.Mesh) {
            obj.geometry.dispose();
            if (Array.isArray(obj.material)) {
              obj.material.forEach(m => m.dispose());
            } else {
              obj.material.dispose();
            }
          }
        });
        activeBraceletRef.current = null;
        braceletFadeRef.current.targetOpacity = 0;
      }
    }
  };

  const updateFPS = () => {
    // Simple FPS counter
    const now = performance.now();
    const delta = now - (updateFPS as Record<string, number>).lastTime || 0;
    (updateFPS as Record<string, number>).lastTime = now;

    if (delta > 0) {
      const currentFPS = Math.round(1000 / delta);
      setFps(currentFPS);
    }
  };

  // ==================
  // MCP WebSocket Integration
  // ==================

  const setupMCPWebSocket = useCallback(() => {
    try {
      const ws = new WebSocket('ws://localhost:8082');

      ws.onopen = () => {
        console.log('✅ Connected to MCP server');
        mcpWebSocketRef.current = ws;
        // Send initial scene state
        sendSceneState();
      };

      ws.onmessage = (event) => {
        try {
          const command = JSON.parse(event.data);
          handleMCPCommand(command);
        } catch (error) {
          console.error('Failed to parse MCP command:', error);
        }
      };

      ws.onerror = (error) => {
        console.warn('MCP WebSocket error (server may not be running):', error);
      };

      ws.onclose = () => {
        console.log('MCP WebSocket closed');
        mcpWebSocketRef.current = null;
      };
    } catch (error) {
      console.warn('Failed to connect to MCP server:', error);
    }
  }, []);

  const sendSceneState = useCallback(() => {
    if (!mcpWebSocketRef.current || mcpWebSocketRef.current.readyState !== WebSocket.OPEN) {
      return;
    }

    const sceneObjects = Array.from(mcpObjectsRef.current.entries()).map(([id, obj]) => ({
      id,
      type: obj.userData.mcpType || 'unknown',
      position: [obj.position.x, obj.position.y, obj.position.z],
      rotation: [obj.rotation.x, obj.rotation.y, obj.rotation.z],
      scale: [obj.scale.x, obj.scale.y, obj.scale.z],
      color: obj.userData.mcpColor || '#ffffff',
    }));

    mcpWebSocketRef.current.send(JSON.stringify({ data: sceneObjects }));
  }, []);

  const handleMCPCommand = useCallback((command: {
    action: string;
    type?: string;
    position?: number[];
    color?: string;
    id?: string;
    speed?: number;
  }) => {
    if (!sceneRef.current) return;

    const scene = sceneRef.current;

    switch (command.action) {
      case 'addObject': {
        const { type, position, color } = command;
        const id = `mcp_${Date.now()}_${Math.random().toString(36).substr(2, 9)}`;

        let geometry: THREE.BufferGeometry;
        switch (type.toLowerCase()) {
          case 'cube':
          case 'box':
            geometry = new THREE.BoxGeometry(2, 2, 2);
            break;
          case 'sphere':
            geometry = new THREE.SphereGeometry(1, 32, 32);
            break;
          case 'cylinder':
            geometry = new THREE.CylinderGeometry(1, 1, 2, 32);
            break;
          case 'cone':
            geometry = new THREE.ConeGeometry(1, 2, 32);
            break;
          default:
            geometry = new THREE.BoxGeometry(2, 2, 2);
        }

        const material = new THREE.MeshStandardMaterial({
          color: new THREE.Color(color),
          metalness: 0.3,
          roughness: 0.7,
        });

        const mesh = new THREE.Mesh(geometry, material);
        mesh.position.set(position[0], position[1], position[2]);
        mesh.userData = {
          mcpType: type,
          mcpColor: color,
          mcpId: id,
        };

        scene.add(mesh);
        mcpObjectsRef.current.set(id, mesh);

        console.log(`✅ Added ${type} at [${position}] with color ${color}`);
        sendSceneState();
        break;
      }

      case 'moveObject': {
        const { id, position } = command;
        const obj = mcpObjectsRef.current.get(id);
        if (obj) {
          obj.position.set(position[0], position[1], position[2]);
          console.log(`✅ Moved ${id} to [${position}]`);
          sendSceneState();
        } else {
          console.warn(`Object ${id} not found`);
        }
        break;
      }

      case 'removeObject': {
        const { id } = command;
        const obj = mcpObjectsRef.current.get(id);
        if (obj) {
          scene.remove(obj);
          if (obj instanceof THREE.Mesh) {
            obj.geometry.dispose();
            if (obj.material instanceof THREE.Material) {
              obj.material.dispose();
            }
          }
          mcpObjectsRef.current.delete(id);
          console.log(`✅ Removed ${id}`);
          sendSceneState();
        } else {
          console.warn(`Object ${id} not found`);
        }
        break;
      }

      case 'startRotation': {
        const { id, speed } = command;
        const obj = mcpObjectsRef.current.get(id);
        if (obj) {
          obj.userData.rotationSpeed = speed;
          console.log(`✅ Started rotation for ${id} at speed ${speed}`);
        } else {
          console.warn(`Object ${id} not found`);
        }
        break;
      }

      case 'stopRotation': {
        const { id } = command;
        const obj = mcpObjectsRef.current.get(id);
        if (obj) {
          obj.userData.rotationSpeed = 0;
          console.log(`✅ Stopped rotation for ${id}`);
        } else {
          console.warn(`Object ${id} not found`);
        }
        break;
      }

      default:
        console.warn(`Unknown MCP command: ${command.action}`);
    }
  }, [sendSceneState]);

  // ==================
  // Cleanup
  // ==================

  const cleanup = useCallback(() => {
    if (animationIdRef.current) {
      cancelAnimationFrame(animationIdRef.current);
      animationIdRef.current = null;
    }

    // Clean up BSP world geometry first
    if (bspWorldRef.current) {
      bspWorldRef.current.traverse((object) => {
        if (object instanceof THREE.Mesh || object instanceof THREE.InstancedMesh) {
          if (object.geometry) {
            object.geometry.dispose();
          }
          if (object.material) {
            if (Array.isArray(object.material)) {
              object.material.forEach(mat => {
                if (mat.map) mat.map.dispose();
                mat.dispose();
              });
            } else {
              if (object.material.map) object.material.map.dispose();
              object.material.dispose();
            }
          }
        }
        if (object instanceof THREE.Sprite && object.material) {
          if (object.material.map) object.material.map.dispose();
          object.material.dispose();
        }
      });
      bspWorldRef.current = null;
    }

    // Clean up renderer last (WebGPU needs special handling)
    if (rendererRef.current) {
      try {
        rendererRef.current.dispose();
      } catch (e) {
        // Ignore WebGPU cleanup errors
        console.warn('Renderer cleanup warning:', e);
      }
      rendererRef.current = null;
    }

    partitionPlanesRef.current = [];
    instancedMeshesRef.current.clear();
    lodGroupsRef.current.clear();
    floorGroupsRef.current = [];
  }, []);

  // Rebuild floor when scope filter changes
  useEffect(() => {
    if (!bspWorldRef.current || !sceneRef.current) return;

    // Rebuild the BSP world to reflect the new scope filter
    const rebuildWorld = async () => {
      // Clear existing floor groups
      floorGroupsRef.current.forEach(group => {
        bspWorldRef.current?.remove(group);
      });
      floorGroupsRef.current = [];

      // Rebuild the world
      if (bspWorldRef.current) {
        if (bspTreeDataRef.current) {
          await buildRealBSPWorld(bspWorldRef.current, bspTreeDataRef.current);
        } else {
          await buildDemoBSPWorld(bspWorldRef.current);
        }
      }

      console.log(`🔄 Rebuilt BSP world with scope filter:`, scopeFilter);
    };

    rebuildWorld();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [scopeFilter]); // Rebuild when scope filter changes

  // ==================
  // Render
  // ==================

  if (error) {
    return (
      <Box sx={{ p: 2 }}>
        <Typography color="error">Error: {error}</Typography>
      </Box>
    );
  }

  return (
    <Box
      ref={containerRef}
      sx={{
        position: 'relative',
        width,
        height,
        cursor: isPointerLockedRef.current ? 'none' : 'pointer',
        backgroundColor: '#000',
        overflow: 'hidden',
      }}
    >
      {/* Canvas */}
      <canvas
        ref={canvasRef}
        style={{
          display: 'block',
          width: '100%',
          height: '100%',
        }}
      />

      {/* Loading overlay */}
      {isLoading && (
        <Box
          sx={{
            position: 'absolute',
            top: 0,
            left: 0,
            right: 0,
            bottom: 0,
            display: 'flex',
            flexDirection: 'column',
            alignItems: 'center',
            justifyContent: 'center',
            backgroundColor: 'rgba(0, 0, 0, 0.9)',
            color: '#0f0',
            fontFamily: 'monospace',
            gap: 3,
          }}
        >
          <Typography
            variant="h4"
            sx={{
              color: '#0f0',
              fontFamily: 'monospace',
              fontWeight: 'bold',
              textShadow: '0 0 10px #0f0',
            }}
          >
            LOADING BSP TREE
          </Typography>

          <Box sx={{ width: '60%', maxWidth: 500 }}>
            {/* Progress bar container */}
            <Box
              sx={{
                width: '100%',
                height: 30,
                backgroundColor: 'rgba(0, 255, 0, 0.1)',
                border: '2px solid #0f0',
                borderRadius: 1,
                overflow: 'hidden',
                position: 'relative',
              }}
            >
              {/* Progress bar fill */}
              <Box
                sx={{
                  width: `${loadingProgress}%`,
                  height: '100%',
                  backgroundColor: '#0f0',
                  transition: 'width 0.3s ease-out',
                  boxShadow: '0 0 20px #0f0',
                }}
              />

              {/* Progress percentage text */}
              <Typography
                sx={{
                  position: 'absolute',
                  top: '50%',
                  left: '50%',
                  transform: 'translate(-50%, -50%)',
                  color: loadingProgress > 50 ? '#000' : '#0f0',
                  fontFamily: 'monospace',
                  fontWeight: 'bold',
                  fontSize: '14px',
                  textShadow: loadingProgress > 50 ? 'none' : '0 0 5px #0f0',
                }}
              >
                {loadingProgress}%
              </Typography>
            </Box>

            {/* Status message */}
            <Typography
              sx={{
                color: '#0f0',
                fontFamily: 'monospace',
                fontSize: '14px',
                textAlign: 'center',
                mt: 2,
                opacity: 0.8,
              }}
            >
              {loadingStatus}
            </Typography>
          </Box>
        </Box>
      )}

      {/* HUD - CRT-style with scanlines and lime-to-amber gradient */}
      {showHUD && !isLoading && (
        <Paper
          sx={{
            position: 'absolute',
            top: 16,
            left: 16,
            p: 2,
            backgroundColor: 'rgba(0, 0, 0, 0.85)', // Darker for better contrast
            border: '2px solid #0f0',
            boxShadow: '0 0 20px rgba(0, 255, 0, 0.3), inset 0 0 10px rgba(0, 255, 0, 0.1)',
            fontFamily: '"Courier New", monospace',
            minWidth: 300,

            // Scanline overlay effect
            '&::before': {
              content: '""',
              position: 'absolute',
              top: 0,
              left: 0,
              right: 0,
              bottom: 0,
              background: 'repeating-linear-gradient(0deg, rgba(0, 0, 0, 0.15), rgba(0, 0, 0, 0.15) 1px, transparent 1px, transparent 2px)',
              pointerEvents: 'none',
              zIndex: 1,
            },
          }}
        >
          <Stack spacing={1} sx={{ position: 'relative', zIndex: 2 }}>
            <Typography
              variant="h6"
              sx={{
                background: 'linear-gradient(90deg, #0f0 0%, #ff0 100%)',
                WebkitBackgroundClip: 'text',
                WebkitTextFillColor: 'transparent',
                fontFamily: '"Courier New", monospace',
                fontWeight: 'bold',
                textShadow: '0 0 10px rgba(0, 255, 0, 0.5)',
              }}
            >
              BSP DOOM EXPLORER
            </Typography>

            <Box>
              <Typography variant="caption" sx={{ color: '#888' }}>
                Mode:
              </Typography>
              <Typography sx={{ color: bspTreeDataRef.current ? '#0f0' : '#ff0' }}>
                {bspTreeDataRef.current ? 'API Connected' : 'Demo Mode'}
              </Typography>
            </Box>

            <Box>
              <Typography variant="caption" sx={{ color: '#888' }}>
                Renderer:
              </Typography>
              <Typography sx={{ color: '#0f0' }}>
                {isWebGPU ? 'WebGPU' : 'WebGL'}
              </Typography>
            </Box>

            <Box>
              <Typography variant="caption" sx={{ color: '#888' }}>
                FPS:
              </Typography>
              <Typography sx={{ color: fps > 30 ? '#0f0' : '#f00' }}>
                {fps}
              </Typography>
            </Box>

            <Box>
              <Typography variant="caption" sx={{ color: '#888' }}>
                Field of View:
              </Typography>
              <Typography sx={{ color: '#0f0' }}>
                {cameraRef.current?.fov.toFixed(0)}° {cameraRef.current?.fov === 75 ? '(DOOM-like)' : ''}
              </Typography>
            </Box>

            {currentRegion && (
              <Box>
                <Typography variant="caption" sx={{ color: '#888' }}>
                  Current Region:
                </Typography>
                <Chip
                  label={currentRegion.name}
                  size="small"
                  sx={{
                    backgroundColor: TONALITY_COLORS[currentRegion.tonalityType] || '#0f0',
                    color: '#000',
                    fontWeight: 'bold',
                  }}
                />
              </Box>
            )}

            <Box>
              <Typography variant="caption" sx={{ color: '#888' }}>
                Current Floor:
              </Typography>
              <Typography sx={{ color: currentFloor <= 2 ? '#00ffff' : '#ffff00', fontWeight: 'bold' }}>
                {['Pitch Class Sets', 'Forte Codes', 'Prime Forms', 'Chords', 'Inversions', 'Voicings'][currentFloor] || `Level ${currentFloor}`}
              </Typography>
              <Typography variant="caption" sx={{ color: '#666', fontStyle: 'italic' }}>
                {currentFloor <= 2 ? 'Atonal Space' : 'Tonal Interpretation'}
              </Typography>
              {/* PYRAMID INFO */}
              <Typography variant="caption" sx={{ color: '#888', mt: 0.5, display: 'block' }}>
                🔺 Pyramid Structure:
              </Typography>
              <Typography sx={{ color: '#0f0', fontSize: '11px' }}>
                {(() => {
                  const floorElementCounts = [115, 200, 350, 4096, 10000, 100000];
                  const minSize = 50;
                  const maxSize = 200;
                  const logMin = Math.log10(floorElementCounts[0]);
                  const logMax = Math.log10(floorElementCounts[5]);
                  const calculateFloorSize = (elementCount: number): number => {
                    const logCount = Math.log10(elementCount);
                    const normalized = (logCount - logMin) / (logMax - logMin);
                    return minSize + normalized * (maxSize - minSize);
                  };
                  const currentFloorSize = calculateFloorSize(floorElementCounts[currentFloor]);
                  const elementCount = floorElementCounts[currentFloor];
                  return `${currentFloorSize.toFixed(0)}×${currentFloorSize.toFixed(0)}m | ${elementCount.toLocaleString()} elements`;
                })()}
              </Typography>
            </Box>

            {/* Scope Filter Indicator with Breadcrumb */}
            {scopeFilter && (
              <Box sx={{ mt: 1, p: 1, backgroundColor: 'rgba(0, 255, 0, 0.1)', borderRadius: 1, border: '1px solid #0f0' }}>
                <Typography variant="caption" sx={{ color: '#888' }}>
                  🎯 Viewing:
                </Typography>
                <Typography sx={{ color: '#0f0', fontWeight: 'bold', fontSize: '13px', fontFamily: 'monospace' }}>
                  {['Set Classes', 'Forte Codes', 'Prime Forms', 'Pitch Class Sets', 'Inversions', 'Voicings'][scopeFilter.floor]} &gt; {scopeFilter.group}
                </Typography>
                <Stack direction="row" spacing={1} sx={{ mt: 1 }}>
                  <Button
                    size="small"
                    onClick={() => setScopeFilter(null)}
                    sx={{
                      color: '#0f0',
                      borderColor: '#0f0',
                      fontSize: '10px',
                      fontFamily: 'monospace',
                      '&:hover': {
                        backgroundColor: 'rgba(0, 255, 0, 0.1)',
                        borderColor: '#0f0',
                      },
                    }}
                    variant="outlined"
                  >
                    ← Back to Groups
                  </Button>
                  <Button
                    size="small"
                    onClick={() => setScopeFilter(null)}
                    sx={{
                      color: '#ff0',
                      borderColor: '#ff0',
                      fontSize: '10px',
                      fontFamily: 'monospace',
                      '&:hover': {
                        backgroundColor: 'rgba(255, 255, 0, 0.1)',
                        borderColor: '#ff0',
                      },
                    }}
                    variant="outlined"
                  >
                    ✕ Clear
                  </Button>
                </Stack>
              </Box>
            )}

            {treeInfo && (
              <Box>
                <Typography variant="caption" sx={{ color: '#888' }}>
                  BSP Tree Statistics:
                </Typography>
                {treeInfo.rootRegion === 'Demo Root' && (
                  <Typography sx={{ color: '#ff9900', fontSize: '11px', mb: 0.5, fontWeight: 'bold' }}>
                    🎮 DEMO MODE (Backend API not connected)
                  </Typography>
                )}
                <Typography sx={{ color: '#0f0', fontSize: '12px' }}>
                  Regions: {treeInfo.totalRegions.toLocaleString()} | Depth: {treeInfo.maxDepth}
                </Typography>
                <Typography sx={{ color: '#0f0', fontSize: '12px' }}>
                  Root: {treeInfo.rootRegion} | Strategies: {treeInfo.partitionStrategies.join(', ')}
                </Typography>
                <Typography sx={{ color: '#ffff00', fontSize: '11px', mt: 0.5 }}>
                  💡 Instanced Rendering: {enableInstancing ? 'ON' : 'OFF'}
                </Typography>
                <Typography sx={{ color: '#ffff00', fontSize: '11px' }}>
                  💡 LOD System: {enableLOD ? 'ON' : 'OFF'}
                </Typography>
              </Box>
            )}

            {/* Navigation Breadcrumb Trail */}
            {navigationStack.length > 1 && (
              <Box sx={{ mt: 2, pt: 2, borderTop: '1px solid #0f0' }}>
                <Typography variant="caption" sx={{ color: '#888' }}>
                  Navigation Path:
                </Typography>
                <Stack direction="row" spacing={0.5} sx={{ flexWrap: 'wrap', mt: 0.5 }}>
                  {navigationStack.map((room, index) => (
                    <React.Fragment key={index}>
                      <Chip
                        label={room.name}
                        size="small"
                        sx={{
                          backgroundColor: index === navigationStack.length - 1 ? 'rgba(0, 255, 255, 0.2)' : 'rgba(0, 255, 0, 0.1)',
                          color: index === navigationStack.length - 1 ? '#0ff' : '#0f0',
                          borderColor: index === navigationStack.length - 1 ? '#0ff' : '#0f0',
                          fontSize: '10px',
                          fontFamily: 'monospace',
                          height: '20px',
                        }}
                        variant="outlined"
                        onClick={() => {
                          // Navigate back to this room
                          if (index < navigationStack.length - 1) {
                            setNavigationStack(prev => prev.slice(0, index + 1));
                            setCurrentRoom(room.name);
                            if (room.floor !== undefined) {
                              setCurrentFloor(room.floor);
                            }
                          }
                        }}
                      />
                      {index < navigationStack.length - 1 && (
                        <Typography sx={{ color: '#0f0', fontSize: '10px', alignSelf: 'center' }}>
                          →
                        </Typography>
                      )}
                    </React.Fragment>
                  ))}
                </Stack>
                <Typography sx={{ color: '#ffff00', fontSize: '11px', mt: 1 }}>
                  Press Backspace or Esc to go back
                </Typography>
              </Box>
            )}

            <Box sx={{ mt: 2, pt: 2, borderTop: '1px solid #0f0' }}>
              <Typography variant="caption" sx={{ color: '#888' }}>
                Controls:
              </Typography>
              <Typography sx={{ color: '#0f0', fontSize: '11px' }}>
                WASD - Move | Mouse - Look | Space/Shift - Up/Down
              </Typography>
              <Typography sx={{ color: '#00ffff', fontSize: '11px', mt: 0.5 }}>
                Mouse Wheel - Change Floor | Click Door - Enter Room
              </Typography>
              <Typography sx={{ color: '#888', fontSize: '10px', mt: 0.5 }}>
                Click to lock pointer
              </Typography>
            </Box>

            <Box sx={{ mt: 2, pt: 2, borderTop: '1px solid #0f0' }}>
              <Button
                variant={autoNavigate ? 'contained' : 'outlined'}
                size="small"
                onClick={() => setAutoNavigate(!autoNavigate)}
                sx={{
                  color: autoNavigate ? '#000' : '#0f0',
                  backgroundColor: autoNavigate ? '#0f0' : 'transparent',
                  borderColor: '#0f0',
                  fontFamily: 'monospace',
                  fontSize: '11px',
                  '&:hover': {
                    backgroundColor: autoNavigate ? '#0f0' : 'rgba(0, 255, 0, 0.1)',
                    borderColor: '#0f0',
                  },
                }}
              >
                {autoNavigate ? '⏸ Stop Auto-Navigate' : '▶ Auto-Navigate Demo'}
              </Button>
              {autoNavigate && (
                <Typography sx={{ color: '#ffff00', fontSize: '10px', mt: 1 }}>
                  🎬 Auto-navigating through floors...
                </Typography>
              )}
            </Box>
          </Stack>
        </Paper>
      )}

      {/* 3D Floor Navigator Widget - Shows all 6 floors stacked vertically */}
      {!isLoading && (
        <Paper
          sx={{
            position: 'absolute',
            bottom: 16,
            right: 232, // 16px margin + 200px minimap width + 16px gap
            width: 200,
            height: 200,
            backgroundColor: 'rgba(0, 0, 0, 0.9)',
            border: '2px solid #0f0',
            boxShadow: '0 0 20px rgba(0, 255, 0, 0.5), inset 0 0 20px rgba(0, 255, 0, 0.2)',
            p: 1,
            cursor: 'pointer',

            // Scanline overlay effect
            '&::before': {
              content: '""',
              position: 'absolute',
              top: 0,
              left: 0,
              right: 0,
              bottom: 0,
              background: 'repeating-linear-gradient(0deg, rgba(0, 0, 0, 0.15), rgba(0, 0, 0, 0.15) 1px, transparent 1px, transparent 2px)',
              pointerEvents: 'none',
              zIndex: 1,
            },
          }}
          onClick={(e) => {
            // Calculate which floor was clicked based on Y position
            const rect = (e.target as HTMLElement).getBoundingClientRect();
            const y = e.clientY - rect.top;
            const floorIndex = Math.floor((1 - y / rect.height) * 6);
            const clampedFloor = Math.max(0, Math.min(5, floorIndex));
            setCurrentFloor(clampedFloor);
          }}
        >
          <Typography
            variant="caption"
            sx={{
              color: '#0f0',
              fontFamily: '"Courier New", monospace',
              display: 'block',
              textAlign: 'center',
              mb: 0.5,
              position: 'relative',
              zIndex: 2,
            }}
          >
            FLOOR NAVIGATOR
          </Typography>
          <canvas
            ref={floorNavCanvasRef}
            style={{
              width: '100%',
              height: 'calc(100% - 24px)',
              display: 'block',
              position: 'relative',
              zIndex: 2,
            }}
          />
        </Paper>
      )}

      {/* Minimap - Glass effect with parallax depth */}
      {showMinimap && !isLoading && (
        <Paper
          sx={{
            position: 'absolute',
            bottom: 16,
            right: 16,
            width: 200,
            height: 200,
            backgroundColor: 'rgba(0, 0, 0, 0.9)',
            border: '2px solid #0f0',
            boxShadow: '0 0 20px rgba(0, 255, 0, 0.5), inset 0 0 20px rgba(0, 255, 0, 0.2)',
            p: 1,

            // Grid overlay for depth (SUBTLE, THIN lines)
            backgroundImage: 'linear-gradient(rgba(0, 255, 0, 0.05) 1px, transparent 1px), linear-gradient(90deg, rgba(0, 255, 0, 0.05) 1px, transparent 1px)',
            backgroundSize: '20px 20px',
          }}
        >
          <Typography
            variant="caption"
            sx={{
              color: '#0f0',
              fontFamily: 'monospace',
              display: 'block',
              textAlign: 'center',
              mb: 1,
            }}
          >
            MINIMAP
          </Typography>
          <Box
            sx={{
              width: '100%',
              height: 'calc(100% - 24px)',
              backgroundColor: '#000',
              border: '1px solid #0f0',
              position: 'relative',
              overflow: 'hidden',
            }}
          >
            {/* Field of View cone - apex at top, base at player */}
            <Box
              sx={{
                position: 'absolute',
                left: '50%',
                top: '50%',
                width: 0,
                height: 0,
                borderLeft: '40px solid transparent',
                borderRight: '40px solid transparent',
                borderTop: '60px solid rgba(0, 255, 0, 0.2)',
                transform: `translate(-50%, -50%) rotate(${-playerRotation * (180 / Math.PI)}deg)`,
                transformOrigin: 'center center',
                pointerEvents: 'none',
              }}
            />

            {/* Player position indicator */}
            <Box
              sx={{
                position: 'absolute',
                left: '50%',
                top: '50%',
                width: 8,
                height: 8,
                backgroundColor: '#0f0',
                borderRadius: '50%',
                transform: 'translate(-50%, -50%)',
                boxShadow: '0 0 10px #0f0',
                zIndex: 2,
              }}
            />

            {/* Direction indicator (arrow) - apex pointing forward */}
            <Box
              sx={{
                position: 'absolute',
                left: '50%',
                top: '50%',
                width: 0,
                height: 0,
                borderLeft: '5px solid transparent',
                borderRight: '5px solid transparent',
                borderBottom: '14px solid #0f0', // Changed from borderTop to borderBottom (triangle points up)
                transform: `translate(-50%, -50%) translateY(-10px) rotate(${-playerRotation * (180 / Math.PI)}deg)`,
                transformOrigin: 'center center',
                boxShadow: '0 0 8px #0f0',
                zIndex: 3,
              }}
            />
          </Box>
        </Paper>
      )}

      {/* Element Info Panel - Shows detailed information about hovered/selected element */}
      {(hoveredElement || selectedElement) && !isLoading && (
        <ElementInfoPanel
          element={selectedElement || hoveredElement}
          isSelected={!!selectedElement}
        />
      )}

      {/* BSP Element Detail Panel - Shows VexFlow notation/diagrams for hovered elements */}
      {detailPanelData && !isLoading && !isPointerLockedRef.current && (
        <BSPElementDetailPanel
          element={detailPanelData}
          position={{ x: mousePosition.x + 20, y: mousePosition.y + 20 }}
        />
      )}

      {/* Ankh Reticle - 3D Egyptian symbol of life as crosshair */}
      {!isLoading && (
        <AnkhReticle3D hovered={!!hoveredElement} size={60} />
      )}
    </Box>
  );
};

export default BSPDoomExplorer;
