// src/components/PrimeRadiant/DemerzelRiggedFace.ts
// Loads the facecap.glb model (52 ARKit blend shapes) and applies the
// holographic gold shader. Driven by the same 5-emotion system as DemerzelFace.ts.

import * as THREE from 'three';
import { GLTFLoader } from 'three/examples/jsm/loaders/GLTFLoader.js';

// ---------------------------------------------------------------------------
// Types
// ---------------------------------------------------------------------------

export type RiggedEmotion = 'calm' | 'concerned' | 'thinking' | 'pleased' | 'alert';

interface BlendShapePreset {
  [shapeName: string]: number;
}

// ---------------------------------------------------------------------------
// ARKit expression presets (maps emotion → blend shape weights)
// ---------------------------------------------------------------------------

const PRESETS: Record<RiggedEmotion, BlendShapePreset> = {
  calm: {
    mouthSmile_L: 0.05, mouthSmile_R: 0.05,
  },
  concerned: {
    browInnerUp: 0.4,
    browDown_L: 0.3, browDown_R: 0.3,
    mouthFrown_L: 0.2, mouthFrown_R: 0.2,
    jawOpen: 0.1,
    eyeWide_L: 0.2, eyeWide_R: 0.2,
  },
  thinking: {
    browInnerUp: 0.2,
    browDown_L: 0.15,
    eyeSquint_L: 0.2, eyeSquint_R: 0.2,
    eyeLookUp_L: 0.15, eyeLookUp_R: 0.15,
    mouthPucker: 0.1,
  },
  pleased: {
    browOuterUp_L: 0.15, browOuterUp_R: 0.15,
    mouthSmile_L: 0.5, mouthSmile_R: 0.5,
    cheekSquint_L: 0.3, cheekSquint_R: 0.3,
    jawOpen: 0.1,
  },
  alert: {
    browOuterUp_L: 0.5, browOuterUp_R: 0.5,
    browInnerUp: 0.5,
    mouthFrown_L: 0.1, mouthFrown_R: 0.1,
    jawOpen: 0.2,
    eyeWide_L: 0.4, eyeWide_R: 0.4,
  },
};

// ---------------------------------------------------------------------------
// Holographic shader (matches DemerzelFace.ts gold wireframe aesthetic)
// ---------------------------------------------------------------------------

const holoVert = /* glsl */ `
  varying vec3 vNormal;
  varying vec3 vViewDir;
  varying vec3 vWorldPos;

  void main() {
    vNormal = normalize(normalMatrix * normal);
    vec4 wp = modelMatrix * vec4(position, 1.0);
    vWorldPos = wp.xyz;
    vViewDir = normalize(cameraPosition - wp.xyz);
    gl_Position = projectionMatrix * modelViewMatrix * vec4(position, 1.0);
  }
`;

const holoFrag = /* glsl */ `
  uniform float uTime;
  uniform float uSpeaking;

  varying vec3 vNormal;
  varying vec3 vViewDir;
  varying vec3 vWorldPos;

  void main() {
    float fresnel = pow(1.0 - abs(dot(vNormal, vViewDir)), 2.2);

    float scan = sin(vWorldPos.y * 12.0 - uTime * 2.5) * 0.5 + 0.5;
    scan = pow(scan, 4.0) * 0.25;
    float fine = sin(vWorldPos.y * 50.0 - uTime * 5.0) * 0.5 + 0.5;
    fine = pow(fine, 8.0) * 0.1;

    float flicker = 1.0;
    float seed = fract(sin(floor(uTime * 8.0) * 43758.5453));
    if (seed > 0.93) flicker = 0.3;

    float speak = 1.0 + uSpeaking * sin(uTime * 12.0) * 0.25;

    float alpha = (fresnel * 0.7 + 0.25) * flicker + scan + fine;
    alpha = clamp(alpha, 0.0, 0.95);

    vec3 gold = vec3(1.0, 0.82, 0.1);
    vec3 col = gold * (0.6 + fresnel * 0.8 + scan * 0.4) * speak;
    col += vec3(0.1, 0.05, 0.0) * fresnel; // warm rim

    gl_FragColor = vec4(col, alpha);
  }
`;

function createHoloShaderMaterial(): THREE.ShaderMaterial {
  return new THREE.ShaderMaterial({
    uniforms: {
      uTime: { value: 0 },
      uSpeaking: { value: 0 },
    },
    vertexShader: holoVert,
    fragmentShader: holoFrag,
    transparent: true,
    wireframe: false,
    blending: THREE.AdditiveBlending,
    depthWrite: false,
    side: THREE.DoubleSide,
  });
}

// ---------------------------------------------------------------------------
// State stored in group.userData
// ---------------------------------------------------------------------------

interface RiggedFaceState {
  mesh: THREE.Mesh | null;
  shapeMap: Map<string, number>;  // name → index
  currentWeights: Map<string, number>;
  targetWeights: Map<string, number>;
  emotion: RiggedEmotion;
  emotionTarget: RiggedEmotion;
  lastEmotionChange: number;
  blendProgress: number;
  blinkTimer: number;
  nextBlink: number;
  blinkPhase: number;   // -1 = not blinking, 0..2 = blink cycle
  speaking: boolean;
  speakPhase: number;
  holoMaterial: THREE.ShaderMaterial;
  loaded: boolean;
}

// ---------------------------------------------------------------------------
// Create the rigged face group (loads .glb asynchronously)
// ---------------------------------------------------------------------------

export function createRiggedDemerzelFace(
  glbUrl: string,
  scale: number = 1,
): THREE.Group {
  const group = new THREE.Group();
  group.name = 'demerzel-rigged-face';

  const holoMat = createHoloShaderMaterial();

  const state: RiggedFaceState = {
    mesh: null,
    shapeMap: new Map(),
    currentWeights: new Map(),
    targetWeights: new Map(),
    emotion: 'calm',
    emotionTarget: 'calm',
    lastEmotionChange: 0,
    blendProgress: 0,
    blinkTimer: 0,
    nextBlink: 3 + Math.random() * 3,
    blinkPhase: -1,
    speaking: false,
    speakPhase: 0,
    holoMaterial: holoMat,
    loaded: false,
  };
  group.userData.riggedState = state;

  // Beacon — visible gold sphere so we can confirm positioning even before .glb loads
  const beaconGeo = new THREE.SphereGeometry(1.5, 16, 12);
  const beaconMat = new THREE.MeshBasicMaterial({
    color: 0xffd700,
    transparent: true,
    opacity: 0.6,
    blending: THREE.AdditiveBlending,
    depthWrite: false,
  });
  const beacon = new THREE.Mesh(beaconGeo, beaconMat);
  beacon.name = 'demerzel-beacon';
  group.add(beacon);

  // Load .glb
  const loader = new GLTFLoader();
  loader.load(
    glbUrl,
    (gltf) => {
      const root = gltf.scene;
      root.scale.setScalar(scale);
      // Rotate face to point toward camera (-Z direction in parent space)
      root.rotation.y = Math.PI;

      // Find the mesh with morph targets
      let faceMesh: THREE.Mesh | null = null;
      root.traverse((child) => {
        if (child instanceof THREE.Mesh && child.morphTargetInfluences && child.morphTargetInfluences.length > 0) {
          faceMesh = child;
        }
      });

      if (!faceMesh) {
        console.warn('[DemerzelRiggedFace] No morph targets found in .glb');
        return;
      }

      // Apply holographic material
      faceMesh.material = holoMat;

      // Build shape name → index map
      const dict = (faceMesh as THREE.Mesh).morphTargetDictionary;
      if (dict) {
        for (const [name, idx] of Object.entries(dict)) {
          state.shapeMap.set(name, idx);
          state.currentWeights.set(name, 0);
          state.targetWeights.set(name, 0);
        }
      }

      state.mesh = faceMesh;
      state.loaded = true;
      group.add(root);

      // Remove beacon now that real face is loaded
      const b = group.getObjectByName('demerzel-beacon');
      if (b) group.remove(b);

      // Apply initial emotion
      applyPreset(state, 'calm');

      console.log(
        `[DemerzelRiggedFace] Loaded: ${state.shapeMap.size} blend shapes`,
        [...state.shapeMap.keys()].join(', '),
      );
    },
    undefined,
    (err) => {
      console.warn('[DemerzelRiggedFace] Failed to load .glb:', err);
    },
  );

  return group;
}

// ---------------------------------------------------------------------------
// Apply a preset to target weights (all unlisted shapes → 0)
// ---------------------------------------------------------------------------

function applyPreset(state: RiggedFaceState, emotion: RiggedEmotion): void {
  const preset = PRESETS[emotion];
  for (const name of state.shapeMap.keys()) {
    state.targetWeights.set(name, preset[name] ?? 0);
  }
}

// ---------------------------------------------------------------------------
// Update loop — call each frame
// ---------------------------------------------------------------------------

export function updateRiggedDemerzelFace(
  group: THREE.Group,
  time: number,
  _cameraPosition: THREE.Vector3,
  speaking: boolean,
): void {
  const state = group.userData.riggedState as RiggedFaceState | undefined;
  if (!state || !state.loaded || !state.mesh) return;

  const dt = 0.016; // ~60fps assumed; good enough for lerp
  state.speaking = speaking;

  // ── Auto-cycle emotions ──
  if (time - state.lastEmotionChange > 8 + Math.random() * 6) {
    state.emotion = state.emotionTarget;
    const emotions: RiggedEmotion[] = ['calm', 'concerned', 'thinking', 'pleased', 'alert'];
    state.emotionTarget = emotions[Math.floor(Math.random() * emotions.length)];
    state.blendProgress = 0;
    state.lastEmotionChange = time;
    applyPreset(state, state.emotionTarget);
  }

  if (speaking) {
    state.emotionTarget = 'pleased';
    applyPreset(state, 'pleased');
  }

  state.blendProgress = Math.min(1, state.blendProgress + dt * 3);

  // ── Lerp blend shape weights ──
  const influences = state.mesh.morphTargetInfluences!;
  for (const [name, idx] of state.shapeMap) {
    const target = state.targetWeights.get(name) ?? 0;
    const current = state.currentWeights.get(name) ?? 0;
    const next = current + (target - current) * dt * 4;
    state.currentWeights.set(name, next);
    influences[idx] = next;
  }

  // ── Blink ──
  if (state.blinkPhase >= 0) {
    state.blinkPhase += dt * 13;
    let bval: number;
    if (state.blinkPhase < 1) bval = state.blinkPhase;
    else if (state.blinkPhase < 2) bval = 2 - state.blinkPhase;
    else { bval = 0; state.blinkPhase = -1; }

    const blinkL = state.shapeMap.get('eyeBlink_L');
    const blinkR = state.shapeMap.get('eyeBlink_R');
    if (blinkL !== undefined) influences[blinkL] = bval;
    if (blinkR !== undefined) influences[blinkR] = bval;
  } else {
    state.blinkTimer += dt;
    if (state.blinkTimer >= state.nextBlink) {
      state.blinkTimer = 0;
      state.nextBlink = 2.5 + Math.random() * 4;
      state.blinkPhase = 0;
    }
  }

  // ── Speaking jaw ──
  if (speaking) {
    state.speakPhase += dt * 12;
    const jawIdx = state.shapeMap.get('jawOpen');
    if (jawIdx !== undefined) {
      influences[jawIdx] = Math.abs(Math.sin(state.speakPhase)) * 0.4;
    }
  }

  // ── Head movement ──
  group.rotation.y = Math.sin(time * 0.3) * 0.08;
  group.rotation.x = Math.sin(time * 0.2) * 0.04;
  group.rotation.z = Math.sin(time * 0.17) * 0.02;

  // ── Breathing ──
  const breathe = 1 + Math.sin(time * 0.8) * 0.012;
  group.scale.setScalar(breathe);

  // ── Shader uniforms ──
  state.holoMaterial.uniforms.uTime.value = time;
  state.holoMaterial.uniforms.uSpeaking.value = speaking ? 1 : 0;
}

// ---------------------------------------------------------------------------
// Public: set emotion from outside (e.g. from algedonic signals)
// ---------------------------------------------------------------------------

export function setRiggedEmotion(group: THREE.Group, emotion: RiggedEmotion): void {
  const state = group.userData.riggedState as RiggedFaceState | undefined;
  if (!state) return;
  state.emotionTarget = emotion;
  state.blendProgress = 0;
  state.lastEmotionChange = performance.now() / 1000;
  applyPreset(state, emotion);
}
