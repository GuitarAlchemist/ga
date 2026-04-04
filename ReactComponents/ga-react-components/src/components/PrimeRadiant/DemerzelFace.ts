// src/components/PrimeRadiant/DemerzelFace.ts
// Holographic Demerzel face — expressive, human-like presence in the Prime Radiant
// Foundation-era aesthetic: gold wireframe, scanlines, holographic flicker
// Enhanced with eyebrows, lips, emotional states, micro-expressions

import * as THREE from 'three';
import type { MeshBasicNodeMaterial } from 'three/webgpu';
import { createHolographicMaterialTSL } from './shaders/HolographicTSL';

// ---------------------------------------------------------------------------
// Emotional state — drives all facial expressions
// ---------------------------------------------------------------------------
export type DemerzelEmotion = 'calm' | 'concerned' | 'thinking' | 'pleased' | 'alert';

interface EmotionConfig {
  browRaise: number;       // -1 (furrow) to 1 (raise)
  browTilt: number;        // inner brow angle
  mouthCurve: number;      // -1 (frown) to 1 (smile)
  mouthOpen: number;       // 0 (closed) to 1 (open)
  eyeWiden: number;        // 0.8 (squint) to 1.2 (wide)
  pupilDilate: number;     // 0.8 to 1.5
  headTiltX: number;       // slight nod
  headTiltZ: number;       // slight head tilt
}

const EMOTIONS: Record<DemerzelEmotion, EmotionConfig> = {
  calm:      { browRaise: 0,    browTilt: 0,    mouthCurve: 0.1,  mouthOpen: 0,   eyeWiden: 1,   pupilDilate: 1,   headTiltX: 0,     headTiltZ: 0 },
  concerned: { browRaise: 0.3,  browTilt: 0.4,  mouthCurve: -0.2, mouthOpen: 0.1, eyeWiden: 1.1, pupilDilate: 1.1, headTiltX: 0.05,  headTiltZ: -0.05 },
  thinking:  { browRaise: 0.2,  browTilt: 0.2,  mouthCurve: 0,    mouthOpen: 0,   eyeWiden: 0.9, pupilDilate: 0.9, headTiltX: -0.06, headTiltZ: 0.04 },
  pleased:   { browRaise: 0.15, browTilt: 0,    mouthCurve: 0.5,  mouthOpen: 0.1, eyeWiden: 0.95,pupilDilate: 1.2, headTiltX: 0.03,  headTiltZ: 0 },
  alert:     { browRaise: 0.5,  browTilt: -0.2, mouthCurve: -0.1, mouthOpen: 0.2, eyeWiden: 1.2, pupilDilate: 1.4, headTiltX: 0,     headTiltZ: 0 },
};

// ---------------------------------------------------------------------------
// Holographic shader — wireframe + scanlines + Fresnel glow + flicker
// ---------------------------------------------------------------------------
const holoVertexShader = /* glsl */ `
  varying vec3 vNormal;
  varying vec3 vViewDir;
  varying vec3 vWorldPos;
  varying vec2 vUv;

  void main() {
    vUv = uv;
    vNormal = normalize(normalMatrix * normal);
    vec4 worldPos = modelMatrix * vec4(position, 1.0);
    vWorldPos = worldPos.xyz;
    vViewDir = normalize(cameraPosition - worldPos.xyz);
    gl_Position = projectionMatrix * modelViewMatrix * vec4(position, 1.0);
  }
`;

const holoFragmentShader = /* glsl */ `
  uniform vec3 uColor;
  uniform float uTime;
  uniform float uOpacity;
  uniform float uSpeaking;

  varying vec3 vNormal;
  varying vec3 vViewDir;
  varying vec3 vWorldPos;
  varying vec2 vUv;

  void main() {
    float fresnel = 1.0 - abs(dot(vNormal, vViewDir));
    fresnel = pow(fresnel, 1.8);

    float scanline = sin((vWorldPos.y * 8.0 - uTime * 2.0)) * 0.5 + 0.5;
    scanline = pow(scanline, 4.0) * 0.3;

    float fineScan = sin(vWorldPos.y * 40.0 - uTime * 4.0) * 0.5 + 0.5;
    fineScan = pow(fineScan, 8.0) * 0.15;

    float flicker = 1.0;
    float flickerSeed = fract(sin(floor(uTime * 8.0) * 43758.5453));
    if (flickerSeed > 0.92) flicker = 0.3;

    float speakPulse = 1.0 + uSpeaking * sin(uTime * 12.0) * 0.3;

    float alpha = (fresnel * 0.7 + 0.3) * uOpacity * flicker;
    alpha += scanline + fineScan;
    alpha = clamp(alpha, 0.0, 1.0);

    vec3 col = uColor * (0.6 + fresnel * 0.6 + scanline * 0.5) * speakPulse;
    col.b += fresnel * 0.15;

    gl_FragColor = vec4(col, alpha);
  }
`;

// ---------------------------------------------------------------------------
// Materials
// ---------------------------------------------------------------------------
function createHoloMaterial(opacity: number = 0.6): MeshBasicNodeMaterial {
  return createHolographicMaterialTSL({
    color: '#FFD700',
    opacity,
    wireframe: true,
  });
}

function createLineMat(opacity: number = 0.7): THREE.LineBasicMaterial {
  return new THREE.LineBasicMaterial({
    color: new THREE.Color('#FFD700'),
    transparent: true,
    opacity,
    blending: THREE.AdditiveBlending,
    depthWrite: false,
  });
}

function makeCurve(points: THREE.Vector3[], segments: number = 16): THREE.Line {
  const curve = new THREE.CubicBezierCurve3(points[0], points[1], points[2], points[3]);
  const geo = new THREE.BufferGeometry().setFromPoints(curve.getPoints(segments));
  return new THREE.Line(geo, createLineMat(0.7));
}

// ---------------------------------------------------------------------------
// Create the Demerzel holographic face — enhanced human-like features
// ---------------------------------------------------------------------------
export function createDemerzelFace(scale: number = 1): THREE.Group {
  const group = new THREE.Group();
  group.userData.isDemerzelFace = true;
  const s = scale;

  const mat = createHoloMaterial(0.55);

  // ── Head — higher detail icosahedron ──
  const headGeo = new THREE.IcosahedronGeometry(3 * s, 3); // detail 3 for smoother
  headGeo.scale(1, 1.3, 0.9);
  const head = new THREE.Mesh(headGeo, mat);
  head.name = 'demerzel-head';
  group.add(head);

  // ── Cheekbones — subtle lateral ridges ──
  const cheekGeo = new THREE.SphereGeometry(0.6 * s, 6, 4);
  cheekGeo.scale(1.4, 0.6, 0.5);
  const cheekMat = createHoloMaterial(0.2);
  const leftCheek = new THREE.Mesh(cheekGeo, cheekMat);
  leftCheek.position.set(-1.2 * s, -0.1 * s, 2.2 * s);
  leftCheek.name = 'demerzel-cheek-l';
  group.add(leftCheek);
  const rightCheek = new THREE.Mesh(cheekGeo.clone(), cheekMat.clone());
  rightCheek.position.set(1.2 * s, -0.1 * s, 2.2 * s);
  rightCheek.name = 'demerzel-cheek-r';
  group.add(rightCheek);

  // ── Eye sockets — slightly recessed rings ──
  const eyeSocketGeo = new THREE.TorusGeometry(0.5 * s, 0.06 * s, 8, 20);
  const eyeSocketMat = createHoloMaterial(0.3);
  eyeSocketMat.wireframe = false;
  const leftSocket = new THREE.Mesh(eyeSocketGeo, eyeSocketMat);
  leftSocket.position.set(-0.8 * s, 0.5 * s, 2.45 * s);
  leftSocket.name = 'demerzel-socket-l';
  group.add(leftSocket);
  const rightSocket = new THREE.Mesh(eyeSocketGeo.clone(), eyeSocketMat.clone());
  rightSocket.position.set(0.8 * s, 0.5 * s, 2.45 * s);
  rightSocket.name = 'demerzel-socket-r';
  group.add(rightSocket);

  // ── Eyes — torus rings ──
  const eyeGeo = new THREE.TorusGeometry(0.35 * s, 0.07 * s, 8, 16);
  const eyeMat = createHoloMaterial(0.85);
  eyeMat.wireframe = false;
  const leftEye = new THREE.Mesh(eyeGeo, eyeMat);
  leftEye.position.set(-0.8 * s, 0.5 * s, 2.5 * s);
  leftEye.rotation.y = 0.15;
  leftEye.name = 'demerzel-eye-left';
  group.add(leftEye);
  const rightEye = new THREE.Mesh(eyeGeo.clone(), eyeMat.clone());
  rightEye.position.set(0.8 * s, 0.5 * s, 2.5 * s);
  rightEye.rotation.y = -0.15;
  rightEye.name = 'demerzel-eye-right';
  group.add(rightEye);

  // ── Iris — larger, expressive ──
  const irisGeo = new THREE.SphereGeometry(0.15 * s, 10, 10);
  const irisMat = new THREE.MeshBasicMaterial({
    color: new THREE.Color('#FFD700'),
    transparent: true, opacity: 0.9,
    blending: THREE.AdditiveBlending, depthWrite: false,
  });
  const leftIris = new THREE.Mesh(irisGeo, irisMat);
  leftIris.position.set(-0.8 * s, 0.5 * s, 2.65 * s);
  leftIris.name = 'demerzel-iris-left';
  group.add(leftIris);
  const rightIris = new THREE.Mesh(irisGeo.clone(), irisMat.clone());
  rightIris.position.set(0.8 * s, 0.5 * s, 2.65 * s);
  rightIris.name = 'demerzel-iris-right';
  group.add(rightIris);

  // ── Eyelids — upper curve lines that animate for expression ──
  const leftUpperLid = makeCurve([
    new THREE.Vector3(-1.15 * s, 0.7 * s, 2.55 * s),
    new THREE.Vector3(-0.9 * s, 0.85 * s, 2.6 * s),
    new THREE.Vector3(-0.7 * s, 0.85 * s, 2.6 * s),
    new THREE.Vector3(-0.45 * s, 0.7 * s, 2.55 * s),
  ]);
  leftUpperLid.name = 'demerzel-lid-ul';
  group.add(leftUpperLid);
  const rightUpperLid = makeCurve([
    new THREE.Vector3(0.45 * s, 0.7 * s, 2.55 * s),
    new THREE.Vector3(0.7 * s, 0.85 * s, 2.6 * s),
    new THREE.Vector3(0.9 * s, 0.85 * s, 2.6 * s),
    new THREE.Vector3(1.15 * s, 0.7 * s, 2.55 * s),
  ]);
  rightUpperLid.name = 'demerzel-lid-ur';
  group.add(rightUpperLid);

  // ── Eyebrows — expressive curves above eyes ──
  const leftBrow = makeCurve([
    new THREE.Vector3(-1.2 * s, 1.0 * s, 2.4 * s),
    new THREE.Vector3(-0.95 * s, 1.15 * s, 2.5 * s),
    new THREE.Vector3(-0.7 * s, 1.15 * s, 2.5 * s),
    new THREE.Vector3(-0.4 * s, 1.05 * s, 2.4 * s),
  ]);
  leftBrow.name = 'demerzel-brow-l';
  group.add(leftBrow);
  const rightBrow = makeCurve([
    new THREE.Vector3(0.4 * s, 1.05 * s, 2.4 * s),
    new THREE.Vector3(0.7 * s, 1.15 * s, 2.5 * s),
    new THREE.Vector3(0.95 * s, 1.15 * s, 2.5 * s),
    new THREE.Vector3(1.2 * s, 1.0 * s, 2.4 * s),
  ]);
  rightBrow.name = 'demerzel-brow-r';
  group.add(rightBrow);

  // ── Forehead lines — appear when thinking ──
  for (let i = 0; i < 3; i++) {
    const y = (1.6 + i * 0.25) * s;
    const line = makeCurve([
      new THREE.Vector3(-0.6 * s, y, 2.5 * s),
      new THREE.Vector3(-0.2 * s, y + 0.05 * s, 2.55 * s),
      new THREE.Vector3(0.2 * s, y + 0.05 * s, 2.55 * s),
      new THREE.Vector3(0.6 * s, y, 2.5 * s),
    ]);
    line.name = `demerzel-forehead-${i}`;
    (line.material as THREE.LineBasicMaterial).opacity = 0; // hidden by default
    group.add(line);
  }

  // ── Nose — bridge + tip ──
  const noseBridge = makeCurve([
    new THREE.Vector3(0, 0.6 * s, 2.6 * s),
    new THREE.Vector3(0.02 * s, 0.3 * s, 2.75 * s),
    new THREE.Vector3(-0.02 * s, 0.0, 2.8 * s),
    new THREE.Vector3(0, -0.15 * s, 2.75 * s),
  ]);
  noseBridge.name = 'demerzel-nose';
  group.add(noseBridge);

  // Nose wings
  const leftNoseWing = makeCurve([
    new THREE.Vector3(-0.15 * s, -0.15 * s, 2.7 * s),
    new THREE.Vector3(-0.25 * s, -0.2 * s, 2.65 * s),
    new THREE.Vector3(-0.2 * s, -0.25 * s, 2.6 * s),
    new THREE.Vector3(0, -0.15 * s, 2.75 * s),
  ], 8);
  leftNoseWing.name = 'demerzel-nosewing-l';
  (leftNoseWing.material as THREE.LineBasicMaterial).opacity = 0.4;
  group.add(leftNoseWing);
  const rightNoseWing = makeCurve([
    new THREE.Vector3(0.15 * s, -0.15 * s, 2.7 * s),
    new THREE.Vector3(0.25 * s, -0.2 * s, 2.65 * s),
    new THREE.Vector3(0.2 * s, -0.25 * s, 2.6 * s),
    new THREE.Vector3(0, -0.15 * s, 2.75 * s),
  ], 8);
  rightNoseWing.name = 'demerzel-nosewing-r';
  (rightNoseWing.material as THREE.LineBasicMaterial).opacity = 0.4;
  group.add(rightNoseWing);

  // ── Upper lip ──
  const upperLip = makeCurve([
    new THREE.Vector3(-0.55 * s, -0.55 * s, 2.55 * s),
    new THREE.Vector3(-0.15 * s, -0.5 * s, 2.65 * s),
    new THREE.Vector3(0.15 * s, -0.5 * s, 2.65 * s),
    new THREE.Vector3(0.55 * s, -0.55 * s, 2.55 * s),
  ]);
  upperLip.name = 'demerzel-lip-upper';
  group.add(upperLip);

  // ── Lower lip ──
  const lowerLip = makeCurve([
    new THREE.Vector3(-0.5 * s, -0.6 * s, 2.55 * s),
    new THREE.Vector3(-0.15 * s, -0.7 * s, 2.6 * s),
    new THREE.Vector3(0.15 * s, -0.7 * s, 2.6 * s),
    new THREE.Vector3(0.5 * s, -0.6 * s, 2.55 * s),
  ]);
  lowerLip.name = 'demerzel-lip-lower';
  group.add(lowerLip);

  // ── Jaw line ──
  const jaw = makeCurve([
    new THREE.Vector3(-1.8 * s, -0.8 * s, 1.5 * s),
    new THREE.Vector3(-1.0 * s, -1.8 * s, 2.0 * s),
    new THREE.Vector3(1.0 * s, -1.8 * s, 2.0 * s),
    new THREE.Vector3(1.8 * s, -0.8 * s, 1.5 * s),
  ], 20);
  jaw.name = 'demerzel-jaw';
  (jaw.material as THREE.LineBasicMaterial).opacity = 0.25;
  group.add(jaw);

  // ── Nasolabial folds (smile lines) — appear when pleased ──
  const leftSmileLine = makeCurve([
    new THREE.Vector3(-0.3 * s, -0.2 * s, 2.6 * s),
    new THREE.Vector3(-0.5 * s, -0.4 * s, 2.55 * s),
    new THREE.Vector3(-0.6 * s, -0.55 * s, 2.5 * s),
    new THREE.Vector3(-0.55 * s, -0.7 * s, 2.45 * s),
  ], 10);
  leftSmileLine.name = 'demerzel-smile-l';
  (leftSmileLine.material as THREE.LineBasicMaterial).opacity = 0;
  group.add(leftSmileLine);
  const rightSmileLine = makeCurve([
    new THREE.Vector3(0.3 * s, -0.2 * s, 2.6 * s),
    new THREE.Vector3(0.5 * s, -0.4 * s, 2.55 * s),
    new THREE.Vector3(0.6 * s, -0.55 * s, 2.5 * s),
    new THREE.Vector3(0.55 * s, -0.7 * s, 2.45 * s),
  ], 10);
  rightSmileLine.name = 'demerzel-smile-r';
  (rightSmileLine.material as THREE.LineBasicMaterial).opacity = 0;
  group.add(rightSmileLine);

  // Store references
  group.userData.parts = {
    head, leftEye, rightEye, leftIris, rightIris,
    leftSocket, rightSocket, leftCheek, rightCheek,
    leftBrow, rightBrow, leftUpperLid, rightUpperLid,
    upperLip, lowerLip, jaw, noseBridge,
    leftSmileLine, rightSmileLine,
    scale: s,
  };

  group.userData.blink = { nextBlink: 3 + Math.random() * 4, blinking: false, blinkStart: 0 };
  group.userData.emotion = 'calm' as DemerzelEmotion;
  group.userData.emotionTarget = 'calm' as DemerzelEmotion;
  group.userData.emotionBlend = 0; // 0-1 transition progress
  group.userData.lastEmotionChange = 0;

  return group;
}

// ---------------------------------------------------------------------------
// Lerp helper
// ---------------------------------------------------------------------------
function lerpConfig(a: EmotionConfig, b: EmotionConfig, t: number): EmotionConfig {
  const lerp = (x: number, y: number) => x + (y - x) * t;
  return {
    browRaise: lerp(a.browRaise, b.browRaise),
    browTilt: lerp(a.browTilt, b.browTilt),
    mouthCurve: lerp(a.mouthCurve, b.mouthCurve),
    mouthOpen: lerp(a.mouthOpen, b.mouthOpen),
    eyeWiden: lerp(a.eyeWiden, b.eyeWiden),
    pupilDilate: lerp(a.pupilDilate, b.pupilDilate),
    headTiltX: lerp(a.headTiltX, b.headTiltX),
    headTiltZ: lerp(a.headTiltZ, b.headTiltZ),
  };
}

// ---------------------------------------------------------------------------
// Animate the Demerzel face each frame
// ---------------------------------------------------------------------------
const _faceWorldPos = new THREE.Vector3();

export function updateDemerzelFace(
  group: THREE.Group,
  time: number,
  cameraPosition: THREE.Vector3,
  speaking: boolean,
): void {
  const parts = group.userData.parts as Record<string, THREE.Object3D & { scale: THREE.Vector3 }> & { scale: number } | undefined;
  if (!parts) return;
  const s = parts.scale;

  // ── Emotion system — auto-cycle through emotions ──
  const ud = group.userData;
  if (time - ud.lastEmotionChange > 8 + Math.random() * 6) {
    const emotions: DemerzelEmotion[] = ['calm', 'concerned', 'thinking', 'pleased', 'alert'];
    ud.emotion = ud.emotionTarget;
    ud.emotionTarget = emotions[Math.floor(Math.random() * emotions.length)];
    ud.emotionBlend = 0;
    ud.lastEmotionChange = time;
  }
  // Override: speaking = pleased, alert if health is bad
  if (speaking) ud.emotionTarget = 'pleased';

  ud.emotionBlend = Math.min(1, ud.emotionBlend + 0.015); // smooth 1-2 second blend
  const emo = lerpConfig(
    EMOTIONS[ud.emotion as DemerzelEmotion],
    EMOTIONS[ud.emotionTarget as DemerzelEmotion],
    ud.emotionBlend,
  );

  // ── Head movement ──
  group.rotation.y = Math.sin(time * 0.3) * 0.08 + emo.headTiltZ;
  group.rotation.x = Math.sin(time * 0.2) * 0.04 + emo.headTiltX;
  group.rotation.z = Math.sin(time * 0.17) * 0.02;

  // Breathing
  const breathe = 1 + Math.sin(time * 0.8) * 0.015;
  group.scale.setScalar(breathe);

  // ── Eye tracking ──
  group.getWorldPosition(_faceWorldPos);
  const localDir = group.worldToLocal(cameraPosition.clone()).normalize();
  const irisShift = 0.12 * s;
  const li = parts.leftIris as THREE.Mesh;
  const ri = parts.rightIris as THREE.Mesh;
  li.position.set(-0.8 * s + localDir.x * irisShift, 0.5 * s + localDir.y * irisShift * 0.5, 2.65 * s);
  ri.position.set(0.8 * s + localDir.x * irisShift, 0.5 * s + localDir.y * irisShift * 0.5, 2.65 * s);

  // Pupil dilation
  const pupilScale = emo.pupilDilate;
  li.scale.setScalar(pupilScale);
  ri.scale.setScalar(pupilScale);

  // ── Eye widening ──
  const le = parts.leftEye as THREE.Mesh;
  const re = parts.rightEye as THREE.Mesh;
  le.scale.set(1, emo.eyeWiden, 1);
  re.scale.set(1, emo.eyeWiden, 1);
  (parts.leftSocket as THREE.Mesh).scale.set(1, emo.eyeWiden, 1);
  (parts.rightSocket as THREE.Mesh).scale.set(1, emo.eyeWiden, 1);

  // ── Blink ──
  const blink = group.userData.blink as { nextBlink: number; blinking: boolean; blinkStart: number };
  if (!blink.blinking && time > blink.nextBlink) {
    blink.blinking = true;
    blink.blinkStart = time;
  }
  if (blink.blinking) {
    const elapsed = time - blink.blinkStart;
    if (elapsed < 0.15) {
      const squash = 1 - Math.sin((elapsed / 0.15) * Math.PI) * 0.9;
      le.scale.y = squash * emo.eyeWiden;
      re.scale.y = squash * emo.eyeWiden;
      li.scale.setScalar(squash * pupilScale);
      ri.scale.setScalar(squash * pupilScale);
    } else {
      blink.blinking = false;
      blink.nextBlink = time + 2.5 + Math.random() * 4;
    }
  }

  // ── Eyebrows — raise/furrow/tilt based on emotion ──
  const browRaise = emo.browRaise * 0.15 * s;
  const browTilt = emo.browTilt * 0.1 * s;
  const lb = parts.leftBrow as THREE.Line;
  const rb = parts.rightBrow as THREE.Line;
  // Micro-movement on top of emotion
  const microBrow = Math.sin(time * 0.7) * 0.01 * s;
  lb.position.set(0, browRaise + microBrow + browTilt, 0);
  rb.position.set(0, browRaise + microBrow - browTilt, 0);

  // ── Forehead lines — visible when thinking ──
  const thinkAmount = ud.emotionTarget === 'thinking' ? ud.emotionBlend : (ud.emotion === 'thinking' ? 1 - ud.emotionBlend : 0);
  group.children.forEach((child) => {
    if (child.name.startsWith('demerzel-forehead-')) {
      (child as THREE.Line).material = (child as THREE.Line).material as THREE.LineBasicMaterial;
      ((child as THREE.Line).material as THREE.LineBasicMaterial).opacity = thinkAmount * 0.35;
    }
  });

  // ── Smile lines — visible when pleased ──
  const smileAmount = emo.mouthCurve > 0.2 ? (emo.mouthCurve - 0.2) / 0.3 : 0;
  ((parts.leftSmileLine as THREE.Line).material as THREE.LineBasicMaterial).opacity = Math.min(0.35, smileAmount * 0.35);
  ((parts.rightSmileLine as THREE.Line).material as THREE.LineBasicMaterial).opacity = Math.min(0.35, smileAmount * 0.35);

  // ── Lips — deform based on emotion + speaking ──
  const upperLipGeo = (parts.upperLip as THREE.Line).geometry as THREE.BufferGeometry;
  const lowerLipGeo = (parts.lowerLip as THREE.Line).geometry as THREE.BufferGeometry;
  const upperPos = upperLipGeo.attributes.position as THREE.BufferAttribute;
  const lowerPos = lowerLipGeo.attributes.position as THREE.BufferAttribute;

  const speakAmount = speaking ? 1.0 : 0.0;

  for (let i = 0; i < upperPos.count; i++) {
    const t = i / (upperPos.count - 1);
    const curveOffset = Math.sin(t * Math.PI) * emo.mouthCurve * 0.08 * s;
    const speakOpen = speakAmount * Math.sin(time * 12 + t * 3) * 0.04 * s;
    const openOffset = emo.mouthOpen * 0.05 * s;

    // Upper lip rises with smile, opens with speaking
    upperPos.setY(i, -0.5 * s + curveOffset + speakOpen * 0.3 + openOffset * 0.3);
    // Lower lip drops with open/speaking
    const lowerBase = -0.7 * s + Math.sin(t * Math.PI) * 0.1 * s;
    const lowerSpeak = speakAmount * (Math.sin(time * 8 + t * 5) * 0.06 * s + Math.sin(time * 14 + t * 2) * 0.03 * s);
    lowerPos.setY(i, lowerBase - curveOffset * 0.5 - lowerSpeak - openOffset);
  }
  upperPos.needsUpdate = true;
  lowerPos.needsUpdate = true;

  // ── Upper eyelid position tracks expression ──
  const leftLid = parts.leftUpperLid as THREE.Line;
  const rightLid = parts.rightUpperLid as THREE.Line;
  leftLid.position.y = emo.eyeWiden > 1 ? (emo.eyeWiden - 1) * 0.2 * s : -(1 - emo.eyeWiden) * 0.15 * s;
  rightLid.position.y = leftLid.position.y;

  // ── TSL holographic uniforms ──
  // uTime is handled by TSL's built-in `time` node (auto-updates).
  // uSpeaking is exposed via userData.speakingUniform on each TSL material.
  const speakValue = speaking ? 1.0 : 0.0;
  group.traverse((child) => {
    if (child instanceof THREE.Mesh) {
      const ud = child.material.userData;
      if (ud?.speakingUniform) {
        ud.speakingUniform.value = speakValue;
      }
    }
  });

  // ── Holographic glitch ──
  const glitchSeed = Math.sin(Math.floor(time * 6) * 12345.6789);
  if (glitchSeed > 0.97) {
    group.position.x += (Math.random() - 0.5) * 0.1 * s;
    group.position.y += (Math.random() - 0.5) * 0.05 * s;
  }
}
