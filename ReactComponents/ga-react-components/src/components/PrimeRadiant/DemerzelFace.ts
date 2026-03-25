// src/components/PrimeRadiant/DemerzelFace.ts
// Holographic Demerzel face — a floating wireframe presence in the Prime Radiant
// Foundation-era aesthetic: gold wireframe, scanlines, holographic flicker

import * as THREE from 'three';

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
    // Fresnel rim glow — bright at edges like a hologram
    float fresnel = 1.0 - abs(dot(vNormal, vViewDir));
    fresnel = pow(fresnel, 1.8);

    // Scanlines — horizontal lines sweeping upward
    float scanline = sin((vWorldPos.y * 8.0 - uTime * 2.0)) * 0.5 + 0.5;
    scanline = pow(scanline, 4.0) * 0.3;

    // Fine scanlines (higher frequency)
    float fineScan = sin(vWorldPos.y * 40.0 - uTime * 4.0) * 0.5 + 0.5;
    fineScan = pow(fineScan, 8.0) * 0.15;

    // Holographic flicker — pseudo-random based on time
    float flicker = 1.0;
    float flickerSeed = fract(sin(floor(uTime * 8.0) * 43758.5453));
    if (flickerSeed > 0.92) {
      flicker = 0.3; // brief opacity dip
    }

    // Speaking pulse — modulates brightness when talking
    float speakPulse = 1.0 + uSpeaking * sin(uTime * 12.0) * 0.3;

    // Combine
    float alpha = (fresnel * 0.7 + 0.3) * uOpacity * flicker;
    alpha += scanline + fineScan;
    alpha = clamp(alpha, 0.0, 1.0);

    vec3 col = uColor * (0.6 + fresnel * 0.6 + scanline * 0.5) * speakPulse;
    // Slight blue shift at the edges for holographic dispersion
    col.b += fresnel * 0.15;

    gl_FragColor = vec4(col, alpha);
  }
`;

// ---------------------------------------------------------------------------
// Create holographic material
// ---------------------------------------------------------------------------
function createHoloMaterial(opacity: number = 0.6): THREE.ShaderMaterial {
  return new THREE.ShaderMaterial({
    uniforms: {
      uTime: { value: 0 },
      uColor: { value: new THREE.Color('#FFD700') },
      uOpacity: { value: opacity },
      uSpeaking: { value: 0.0 },
    },
    vertexShader: holoVertexShader,
    fragmentShader: holoFragmentShader,
    transparent: true,
    wireframe: true,
    blending: THREE.AdditiveBlending,
    depthWrite: false,
    side: THREE.DoubleSide,
  });
}

// ---------------------------------------------------------------------------
// Create the Demerzel holographic face
// ---------------------------------------------------------------------------
export function createDemerzelFace(scale: number = 1): THREE.Group {
  const group = new THREE.Group();
  group.userData.isDemerzelFace = true;

  const mat = createHoloMaterial(0.55);

  // ── Head — elongated icosahedron for that classic wireframe hologram ──
  const headGeo = new THREE.IcosahedronGeometry(3 * scale, 2);
  headGeo.scale(1, 1.3, 0.9);
  const head = new THREE.Mesh(headGeo, mat);
  head.name = 'demerzel-head';
  group.add(head);

  // ── Eyes — two small torus rings ──
  const eyeGeo = new THREE.TorusGeometry(0.4 * scale, 0.08 * scale, 8, 16);
  const eyeMat = createHoloMaterial(0.8);
  eyeMat.wireframe = false; // solid for the eye rings

  const leftEye = new THREE.Mesh(eyeGeo, eyeMat);
  leftEye.position.set(-0.8 * scale, 0.5 * scale, 2.5 * scale);
  leftEye.rotation.y = 0.15; // slight inward tilt
  leftEye.name = 'demerzel-eye-left';
  group.add(leftEye);

  const rightEye = new THREE.Mesh(eyeGeo.clone(), eyeMat.clone());
  rightEye.position.set(0.8 * scale, 0.5 * scale, 2.5 * scale);
  rightEye.rotation.y = -0.15;
  rightEye.name = 'demerzel-eye-right';
  group.add(rightEye);

  // ── Iris/pupil — small spheres inside each eye that will track the camera ──
  const irisGeo = new THREE.SphereGeometry(0.15 * scale, 8, 8);
  const irisMat = new THREE.MeshBasicMaterial({
    color: new THREE.Color('#FFD700'),
    transparent: true,
    opacity: 0.9,
    blending: THREE.AdditiveBlending,
    depthWrite: false,
  });

  const leftIris = new THREE.Mesh(irisGeo, irisMat);
  leftIris.position.set(-0.8 * scale, 0.5 * scale, 2.65 * scale);
  leftIris.name = 'demerzel-iris-left';
  group.add(leftIris);

  const rightIris = new THREE.Mesh(irisGeo.clone(), irisMat.clone());
  rightIris.position.set(0.8 * scale, 0.5 * scale, 2.65 * scale);
  rightIris.name = 'demerzel-iris-right';
  group.add(rightIris);

  // ── Nose — subtle thin cone ──
  const noseGeo = new THREE.ConeGeometry(0.12 * scale, 0.6 * scale, 4);
  const noseMat = createHoloMaterial(0.35);
  const nose = new THREE.Mesh(noseGeo, noseMat);
  nose.position.set(0, 0.05 * scale, 2.7 * scale);
  nose.rotation.x = Math.PI; // point downward
  nose.name = 'demerzel-nose';
  group.add(nose);

  // ── Mouth — cubic bezier curve that deforms when speaking ──
  const mouthCurve = new THREE.CubicBezierCurve3(
    new THREE.Vector3(-0.5 * scale, -0.6 * scale, 2.6 * scale),
    new THREE.Vector3(-0.2 * scale, -0.7 * scale, 2.65 * scale),
    new THREE.Vector3(0.2 * scale, -0.7 * scale, 2.65 * scale),
    new THREE.Vector3(0.5 * scale, -0.6 * scale, 2.6 * scale),
  );
  const mouthPoints = mouthCurve.getPoints(16);
  const mouthGeo = new THREE.BufferGeometry().setFromPoints(mouthPoints);
  const mouthMat = new THREE.LineBasicMaterial({
    color: new THREE.Color('#FFD700'),
    transparent: true,
    opacity: 0.7,
    blending: THREE.AdditiveBlending,
    depthWrite: false,
  });
  const mouth = new THREE.Line(mouthGeo, mouthMat);
  mouth.name = 'demerzel-mouth';
  group.add(mouth);

  // ── Jaw line — subtle wireframe arc ──
  const jawCurve = new THREE.CubicBezierCurve3(
    new THREE.Vector3(-1.8 * scale, -0.8 * scale, 1.5 * scale),
    new THREE.Vector3(-1.0 * scale, -1.8 * scale, 2.0 * scale),
    new THREE.Vector3(1.0 * scale, -1.8 * scale, 2.0 * scale),
    new THREE.Vector3(1.8 * scale, -0.8 * scale, 1.5 * scale),
  );
  const jawPoints = jawCurve.getPoints(20);
  const jawGeo = new THREE.BufferGeometry().setFromPoints(jawPoints);
  const jawMat = new THREE.LineBasicMaterial({
    color: new THREE.Color('#FFD700'),
    transparent: true,
    opacity: 0.25,
    blending: THREE.AdditiveBlending,
    depthWrite: false,
  });
  const jaw = new THREE.Line(jawGeo, jawMat);
  jaw.name = 'demerzel-jaw';
  group.add(jaw);

  // Store references for animation
  group.userData.parts = {
    head,
    leftEye,
    rightEye,
    leftIris,
    rightIris,
    nose,
    mouth,
    jaw,
    mouthScale: scale,
  };

  // Store blink state
  group.userData.blink = {
    nextBlink: 3 + Math.random() * 4,
    blinking: false,
    blinkStart: 0,
  };

  return group;
}

// ---------------------------------------------------------------------------
// Animate the Demerzel face each frame
// ---------------------------------------------------------------------------
const _dir = new THREE.Vector3();
const _faceWorldPos = new THREE.Vector3();

export function updateDemerzelFace(
  group: THREE.Group,
  time: number,
  cameraPosition: THREE.Vector3,
  speaking: boolean,
): void {
  const parts = group.userData.parts as {
    head: THREE.Mesh;
    leftEye: THREE.Mesh;
    rightEye: THREE.Mesh;
    leftIris: THREE.Mesh;
    rightIris: THREE.Mesh;
    nose: THREE.Mesh;
    mouth: THREE.Line;
    jaw: THREE.Line;
    mouthScale: number;
  } | undefined;
  if (!parts) return;

  const s = parts.mouthScale;

  // 1. Gentle idle sway
  group.rotation.y = Math.sin(time * 0.3) * 0.08;
  group.rotation.x = Math.sin(time * 0.2) * 0.04;
  group.rotation.z = Math.sin(time * 0.17) * 0.02;

  // 2. Breathing — subtle scale pulse
  const breathe = 1 + Math.sin(time * 0.8) * 0.015;
  group.scale.setScalar(breathe);

  // 3. Eye tracking — irises look toward camera
  group.getWorldPosition(_faceWorldPos);
  _dir.copy(cameraPosition).sub(_faceWorldPos).normalize();
  // Convert to local space direction
  const localDir = group.worldToLocal(cameraPosition.clone()).normalize();
  const irisShift = 0.12 * s;
  parts.leftIris.position.set(
    -0.8 * s + localDir.x * irisShift,
    0.5 * s + localDir.y * irisShift * 0.5,
    2.65 * s,
  );
  parts.rightIris.position.set(
    0.8 * s + localDir.x * irisShift,
    0.5 * s + localDir.y * irisShift * 0.5,
    2.65 * s,
  );

  // 4. Blink — every 3-7 seconds, briefly squash eyes on Y
  const blink = group.userData.blink as {
    nextBlink: number;
    blinking: boolean;
    blinkStart: number;
  };
  if (!blink.blinking && time > blink.nextBlink) {
    blink.blinking = true;
    blink.blinkStart = time;
  }
  if (blink.blinking) {
    const blinkElapsed = time - blink.blinkStart;
    const blinkDuration = 0.15;
    if (blinkElapsed < blinkDuration) {
      const t = blinkElapsed / blinkDuration;
      // Quick close then open: sin curve 0->1->0
      const squash = 1 - Math.sin(t * Math.PI) * 0.85;
      parts.leftEye.scale.set(1, squash, 1);
      parts.rightEye.scale.set(1, squash, 1);
      parts.leftIris.scale.set(1, squash, 1);
      parts.rightIris.scale.set(1, squash, 1);
    } else {
      parts.leftEye.scale.set(1, 1, 1);
      parts.rightEye.scale.set(1, 1, 1);
      parts.leftIris.scale.set(1, 1, 1);
      parts.rightIris.scale.set(1, 1, 1);
      blink.blinking = false;
      blink.nextBlink = time + 3 + Math.random() * 4;
    }
  }

  // 5. Mouth animation — deform when speaking (or idle micro-movement)
  const mouthGeo = parts.mouth.geometry as THREE.BufferGeometry;
  const mouthPos = mouthGeo.attributes.position as THREE.BufferAttribute;
  const speakAmount = speaking ? 1.0 : 0.0;
  const pointCount = mouthPos.count;

  for (let i = 0; i < pointCount; i++) {
    const t = i / (pointCount - 1); // 0 to 1 along the mouth
    const baseY = -0.6 * s - Math.sin(t * Math.PI) * 0.1 * s; // baseline curve

    // Speaking: open mouth with noise-driven movement
    const speakOffset = speakAmount * (
      Math.sin(time * 12 + t * 3) * 0.08 * s +
      Math.sin(time * 8 + t * 7) * 0.04 * s
    );
    // Idle: very subtle twitch
    const idleOffset = (1 - speakAmount) * Math.sin(time * 1.5 + t * 2) * 0.01 * s;

    mouthPos.setY(i, baseY - speakOffset - idleOffset);
  }
  mouthPos.needsUpdate = true;

  // 6. Update shader uniforms (time, speaking)
  group.traverse((child) => {
    if (child instanceof THREE.Mesh) {
      const mat = child.material as THREE.ShaderMaterial;
      if (mat.uniforms?.uTime) {
        mat.uniforms.uTime.value = time;
      }
      if (mat.uniforms?.uSpeaking) {
        mat.uniforms.uSpeaking.value = speaking ? 1.0 : 0.0;
      }
    }
  });

  // 7. Holographic flicker — occasional full-face opacity dip
  // (Handled in shader, but we can also do a scale glitch)
  const glitchSeed = Math.sin(Math.floor(time * 6) * 12345.6789);
  if (glitchSeed > 0.97) {
    // Positional glitch — tiny jitter
    group.position.x += (Math.random() - 0.5) * 0.1 * s;
    group.position.y += (Math.random() - 0.5) * 0.05 * s;
  }
}
