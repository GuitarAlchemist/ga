// src/components/PrimeRadiant/DemerzelFaceOverlay.tsx
// Self-contained 3D face widget rendered in a fixed-position mini canvas.
// Positioned next to the Demerzel chat button (top-left HUD area).
// Has its own Three.js renderer, camera, and lighting — independent of the main scene.

import React, { useEffect, useRef } from 'react';
import * as THREE from 'three';
import { GLTFLoader } from 'three/examples/jsm/loaders/GLTFLoader.js';

// ---------------------------------------------------------------------------
// Types
// ---------------------------------------------------------------------------

export type FaceEmotion = 'calm' | 'concerned' | 'thinking' | 'pleased' | 'alert';

interface BlendPreset { [shape: string]: number }

const PRESETS: Record<FaceEmotion, BlendPreset> = {
  calm:      { mouthSmile_L: 0.05, mouthSmile_R: 0.05 },
  concerned: { browInnerUp: 0.4, browDown_L: 0.3, browDown_R: 0.3, mouthFrown_L: 0.2, mouthFrown_R: 0.2, jawOpen: 0.1, eyeWide_L: 0.2, eyeWide_R: 0.2 },
  thinking:  { browInnerUp: 0.2, browDown_L: 0.15, eyeSquint_L: 0.2, eyeSquint_R: 0.2, mouthPucker: 0.1 },
  pleased:   { browOuterUp_L: 0.15, browOuterUp_R: 0.15, mouthSmile_L: 0.5, mouthSmile_R: 0.5, cheekSquint_L: 0.3, cheekSquint_R: 0.3, jawOpen: 0.1 },
  alert:     { browOuterUp_L: 0.5, browOuterUp_R: 0.5, browInnerUp: 0.5, mouthFrown_L: 0.1, mouthFrown_R: 0.1, jawOpen: 0.2, eyeWide_L: 0.4, eyeWide_R: 0.4 },
};

// ---------------------------------------------------------------------------
// Holographic shader
// ---------------------------------------------------------------------------

const vertSrc = /* glsl */ `
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

const fragSrc = /* glsl */ `
  uniform float uTime;
  uniform float uSpeaking;
  varying vec3 vNormal;
  varying vec3 vViewDir;
  varying vec3 vWorldPos;

  void main() {
    float fresnel = pow(1.0 - abs(dot(vNormal, vViewDir)), 2.0);

    // Use view-space Y for scanlines (model-size independent)
    vec4 viewPos = viewMatrix * vec4(vWorldPos, 1.0);
    float scan = pow(sin(viewPos.y * 80.0 - uTime * 3.0) * 0.5 + 0.5, 4.0) * 0.15;

    float flicker = 1.0 - step(0.94, fract(sin(floor(uTime * 8.0) * 43758.5453))) * 0.5;
    float speak = 1.0 + uSpeaking * sin(uTime * 12.0) * 0.2;

    // Bright gold — always visible
    vec3 gold = vec3(1.0, 0.82, 0.1);
    vec3 col = gold * (0.6 + fresnel * 0.5 + scan * 0.3) * speak;
    col += vec3(0.2, 0.1, 0.0) * fresnel;

    float alpha = 0.4 + fresnel * 0.5 + scan;
    alpha *= flicker;
    alpha = clamp(alpha, 0.15, 0.95);

    gl_FragColor = vec4(col, alpha);
  }
`;

// ---------------------------------------------------------------------------
// Component
// ---------------------------------------------------------------------------

export interface DemerzelFaceOverlayProps {
  /** .glb URL */
  src?: string;
  /** Pixel size of the mini canvas */
  size?: number;
  /** Current emotion override (auto-cycles if omitted) */
  emotion?: FaceEmotion;
  /** Whether Demerzel is speaking */
  speaking?: boolean;
}

export const DemerzelFaceOverlay: React.FC<DemerzelFaceOverlayProps> = ({
  src = '/models/demerzel_face.glb',
  size = 80,
  emotion: emotionProp,
  speaking = false,
}) => {
  const canvasRef = useRef<HTMLCanvasElement>(null);
  const stateRef = useRef<{
    renderer: THREE.WebGLRenderer;
    scene: THREE.Scene;
    camera: THREE.PerspectiveCamera;
    mesh: THREE.Mesh | null;
    shapeMap: Map<string, number>;
    currentWeights: Map<string, number>;
    targetWeights: Map<string, number>;
    holoMat: THREE.ShaderMaterial;
    emotion: FaceEmotion;
    lastEmotionChange: number;
    blinkTimer: number;
    nextBlink: number;
    blinkPhase: number;
    speakPhase: number;
    animId: number;
  } | null>(null);

  useEffect(() => {
    const canvas = canvasRef.current;
    if (!canvas) return;

    // Renderer
    const renderer = new THREE.WebGLRenderer({
      canvas,
      alpha: true,
      antialias: true,
      powerPreference: 'low-power',
    });
    renderer.setSize(size, size);
    renderer.setPixelRatio(Math.min(window.devicePixelRatio, 2));
    renderer.setClearColor(0x0a0e14, 1);

    // Scene
    const scene = new THREE.Scene();

    // Camera — will be repositioned after model loads
    const camera = new THREE.PerspectiveCamera(30, 1, 0.001, 1000);
    camera.position.set(0, 0, 5);
    camera.lookAt(0, 0, 0);

    // Lighting — subtle, the shader is mostly unshaded
    const ambient = new THREE.AmbientLight(0xffd700, 0.3);
    scene.add(ambient);
    const rim = new THREE.PointLight(0xffd700, 1, 5);
    rim.position.set(0.5, 0.5, 0.5);
    scene.add(rim);

    // Shader material
    const holoMat = new THREE.ShaderMaterial({
      uniforms: { uTime: { value: 0 }, uSpeaking: { value: 0 } },
      vertexShader: vertSrc,
      fragmentShader: fragSrc,
      transparent: true,
      blending: THREE.NormalBlending,
      depthWrite: true,
      side: THREE.FrontSide,
    });

    const state = {
      renderer,
      scene,
      camera,
      mesh: null as THREE.Mesh | null,
      shapeMap: new Map<string, number>(),
      currentWeights: new Map<string, number>(),
      targetWeights: new Map<string, number>(),
      holoMat,
      emotion: 'calm' as FaceEmotion,
      lastEmotionChange: performance.now() / 1000,
      blinkTimer: 0,
      nextBlink: 3 + Math.random() * 3,
      blinkPhase: -1,
      speakPhase: 0,
      animId: 0,
    };
    stateRef.current = state;

    // Load face model
    const loader = new GLTFLoader();
    loader.load(src, (gltf) => {
      const root = gltf.scene;

      let faceMesh: THREE.Mesh | null = null;
      root.traverse((child) => {
        if (child instanceof THREE.Mesh && child.morphTargetInfluences?.length) {
          faceMesh = child;
        }
      });

      if (!faceMesh) {
        console.warn('[DemerzelFaceOverlay] No morph targets in .glb');
        return;
      }

      faceMesh.material = holoMat;
      state.mesh = faceMesh;

      const dict = (faceMesh as THREE.Mesh).morphTargetDictionary;
      if (dict) {
        for (const [name, idx] of Object.entries(dict)) {
          state.shapeMap.set(name, idx);
          state.currentWeights.set(name, 0);
          state.targetWeights.set(name, 0);
        }
      }

      scene.add(root);

      // Auto-frame: compute bounding box and position camera to see the face
      const box = new THREE.Box3().setFromObject(root);
      const center = box.getCenter(new THREE.Vector3());
      const bsize = box.getSize(new THREE.Vector3());
      const maxDim = Math.max(bsize.x, bsize.y, bsize.z);
      const dist = maxDim / (2 * Math.tan((camera.fov * Math.PI) / 360));

      camera.position.set(center.x, center.y + maxDim * 0.05, center.z + dist * 0.85);
      camera.lookAt(center.x, center.y + maxDim * 0.05, center.z);
      camera.near = dist * 0.01;
      camera.far = dist * 10;
      camera.updateProjectionMatrix();

      // Move rim light relative to the face
      rim.position.set(center.x + maxDim * 0.5, center.y + maxDim * 0.5, center.z + maxDim * 0.5);

      applyPreset(state, 'calm');
      console.log(`[DemerzelFaceOverlay] Loaded ${state.shapeMap.size} blend shapes, bbox: ${bsize.x.toFixed(3)}x${bsize.y.toFixed(3)}x${bsize.z.toFixed(3)}`);
    });

    // Animation loop
    const animate = () => {
      state.animId = requestAnimationFrame(animate);
      const t = performance.now() / 1000;
      const dt = 0.016;

      // Auto-cycle emotions (unless controlled externally)
      if (!emotionProp && t - state.lastEmotionChange > 8 + Math.random() * 4) {
        const emotions: FaceEmotion[] = ['calm', 'concerned', 'thinking', 'pleased', 'alert'];
        state.emotion = emotions[Math.floor(Math.random() * emotions.length)];
        state.lastEmotionChange = t;
        applyPreset(state, state.emotion);
      }

      // Lerp weights
      if (state.mesh?.morphTargetInfluences) {
        const inf = state.mesh.morphTargetInfluences;
        for (const [name, idx] of state.shapeMap) {
          const target = state.targetWeights.get(name) ?? 0;
          const current = state.currentWeights.get(name) ?? 0;
          const next = current + (target - current) * dt * 4;
          state.currentWeights.set(name, next);
          inf[idx] = next;
        }

        // Blink
        if (state.blinkPhase >= 0) {
          state.blinkPhase += dt * 13;
          const bv = state.blinkPhase < 1 ? state.blinkPhase : state.blinkPhase < 2 ? 2 - state.blinkPhase : 0;
          if (state.blinkPhase >= 2) state.blinkPhase = -1;
          const bl = state.shapeMap.get('eyeBlink_L');
          const br = state.shapeMap.get('eyeBlink_R');
          if (bl !== undefined) inf[bl] = bv;
          if (br !== undefined) inf[br] = bv;
        } else {
          state.blinkTimer += dt;
          if (state.blinkTimer >= state.nextBlink) {
            state.blinkTimer = 0;
            state.nextBlink = 2.5 + Math.random() * 4;
            state.blinkPhase = 0;
          }
        }

        // Speaking
        if (speaking) {
          state.speakPhase += dt * 12;
          const jawIdx = state.shapeMap.get('jawOpen');
          if (jawIdx !== undefined) inf[jawIdx] = Math.abs(Math.sin(state.speakPhase)) * 0.4;
        }
      }

      // Gentle head movement
      scene.rotation.y = Math.sin(t * 0.4) * 0.1;
      scene.rotation.x = Math.sin(t * 0.25) * 0.04;

      // Shader uniforms
      holoMat.uniforms.uTime.value = t;
      holoMat.uniforms.uSpeaking.value = speaking ? 1 : 0;

      renderer.render(scene, camera);
    };
    animate();

    return () => {
      cancelAnimationFrame(state.animId);
      renderer.dispose();
      holoMat.dispose();
      stateRef.current = null;
    };
  // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [src, size]);

  // React to emotion prop changes
  useEffect(() => {
    const s = stateRef.current;
    if (s && emotionProp) {
      s.emotion = emotionProp;
      s.lastEmotionChange = performance.now() / 1000;
      applyPreset(s, emotionProp);
    }
  }, [emotionProp]);

  return (
    <canvas
      ref={canvasRef}
      className="demerzel-face-overlay"
      width={size}
      height={size}
      style={{
        position: 'absolute',
        top: 68,
        left: 52,
        zIndex: 30,
        borderRadius: '12px',
        border: '1px solid rgba(212, 160, 23, 0.35)',
        cursor: 'pointer',
        pointerEvents: 'auto',
      }}
    />
  );
};

// ---------------------------------------------------------------------------

function applyPreset(
  state: { shapeMap: Map<string, number>; targetWeights: Map<string, number> },
  emotion: FaceEmotion,
): void {
  const preset = PRESETS[emotion];
  for (const name of state.shapeMap.keys()) {
    state.targetWeights.set(name, preset[name] ?? 0);
  }
}
