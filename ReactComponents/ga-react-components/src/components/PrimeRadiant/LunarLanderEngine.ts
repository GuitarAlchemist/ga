// src/components/PrimeRadiant/LunarLanderEngine.ts
// Pure TypeScript engine encapsulating the full Apollo LM Descent Simulator.
// Renders Three.js directly into a provided container div — no iframe.

import * as THREE from 'three';
import { OrbitControls } from 'three/examples/jsm/controls/OrbitControls.js';

// ================================================================
//  PUBLIC TYPES
// ================================================================

export interface LanderState {
  altitude: number;
  verticalSpeed: number;
  horizontalSpeed: number;
  rangeToLZ: number;
  fuelPercent: number;
  throttlePercent: number;
  pitchDeg: number;
  rollDeg: number;
  yawDeg: number;
  massKg: number;
  cameraMode: string;
  phase: string;
  missionTime: string;
  contactLight: boolean;
  gameState: GameState;
  calloutText: string;
  cinematicMode: boolean;
  mothershipVisible: boolean;
}

export interface LanderStats {
  vSpeed: string;
  hSpeed: string;
  tilt: string;
  fuel: string;
  time: string;
  distance: string;
}

type GameState = 'waiting' | 'flying' | 'landed' | 'crashed';

type LandedCallback = (success: boolean, stats: LanderStats) => void;

// ================================================================
//  CONSTANTS
// ================================================================

const MOON_GRAVITY   = 1.625;
const DRY_MASS       = 4700;
const FUEL_MASS_INIT = 8200;
const MAX_THRUST     = 45040;
const ISP            = 311;
const G0             = 9.80665;
const PHYSICS_DT     = 1 / 120;
const RCS_TORQUE     = 500;
const RCS_FORCE      = 440;
const THROTTLE_RATE  = 0.20;
const START_ALTITUDE = 500;
const TERRAIN_SIZE   = 2000;
const TERRAIN_SEGS   = 128; // 256 was 65K verts — 128 = 16K, plenty for visual quality
const STAR_COUNT     = 8000;
const EXHAUST_COUNT  = 120;
const DUST_COUNT     = 250;
const RCS_PART_COUNT = 30;

const CAMERA_MODES   = ['ORBIT', 'CHASE', 'COCKPIT', 'SURFACE'] as const;
const SAFE_V_SPEED   = 2.0;
const SAFE_H_SPEED   = 1.0;
const SAFE_TILT_DEG  = 15;
const CONTACT_ALT    = 1.5;
const DUST_START_ALT = 30;
const LANDING_DETECT = 0.5;

// ================================================================
//  INLINE 2D NOISE
// ================================================================

function hash2D(ix: number, iy: number): number {
  let n = ix * 374761393 + iy * 668265263;
  n = (n ^ (n >> 13)) * 1274126177;
  n = n ^ (n >> 16);
  return (n & 0x7fffffff) / 0x7fffffff;
}

function smoothNoise2D(x: number, y: number): number {
  const ix = Math.floor(x);
  const iy = Math.floor(y);
  const fx = x - ix;
  const fy = y - iy;
  const ux = fx * fx * (3 - 2 * fx);
  const uy = fy * fy * (3 - 2 * fy);

  const v00 = hash2D(ix, iy);
  const v10 = hash2D(ix + 1, iy);
  const v01 = hash2D(ix, iy + 1);
  const v11 = hash2D(ix + 1, iy + 1);

  const a = v00 + (v10 - v00) * ux;
  const b = v01 + (v11 - v01) * ux;
  return a + (b - a) * uy;
}

function fbm(x: number, y: number, octaves: number, lacunarity: number, gain: number): number {
  let value = 0, amplitude = 1, frequency = 1, totalAmp = 0;
  for (let i = 0; i < octaves; i++) {
    value    += smoothNoise2D(x * frequency, y * frequency) * amplitude;
    totalAmp += amplitude;
    amplitude *= gain;
    frequency *= lacunarity;
  }
  return value / totalAmp;
}

function smoothstep(edge0: number, edge1: number, x: number): number {
  const t = Math.max(0, Math.min(1, (x - edge1) / (edge0 - edge1)));
  return t * t * (3 - 2 * t);
}

function mulberry32(seed: number): () => number {
  return function () {
    seed |= 0;
    seed = (seed + 0x6d2b79f5) | 0;
    let t = Math.imul(seed ^ (seed >>> 15), 1 | seed);
    t = (t + Math.imul(t ^ (t >>> 7), 61 | t)) ^ t;
    return ((t ^ (t >>> 14)) >>> 0) / 4294967296;
  };
}

// ================================================================
//  LM SIMULATION STATE
// ================================================================

interface LMState {
  pos: THREE.Vector3;
  vel: THREE.Vector3;
  quat: THREE.Quaternion;
  angVel: THREE.Vector3;
  fuel: number;
  throttle: number;
  altitude: number;
  contactLight: boolean;
  startTime: number;
  flightTime: number;
}

// ================================================================
//  PARTICLE POOL HELPERS
// ================================================================

interface ParticleUserData {
  count: number;
  lifetimes: Float32Array;
  maxLife: Float32Array;
  velocities: Float32Array;
  active: Uint8Array;
  nextSlot: number;
}

function createParticlePool(
  scene: THREE.Scene,
  count: number,
  size: number,
  color: number,
  blending?: THREE.Blending,
): THREE.Points {
  const geo = new THREE.BufferGeometry();
  const positions = new Float32Array(count * 3);

  for (let i = 0; i < count; i++) {
    positions[i * 3 + 1] = -99999;
  }

  geo.setAttribute('position', new THREE.BufferAttribute(positions, 3));

  const mat = new THREE.PointsMaterial({
    color,
    size,
    transparent: true,
    opacity: 0.7,
    blending: blending ?? THREE.AdditiveBlending,
    depthWrite: false,
    sizeAttenuation: true,
  });

  const mesh = new THREE.Points(geo, mat);
  mesh.frustumCulled = false;

  mesh.userData = {
    count,
    lifetimes: new Float32Array(count),
    maxLife: new Float32Array(count),
    velocities: new Float32Array(count * 3),
    active: new Uint8Array(count),
    nextSlot: 0,
  } as ParticleUserData;

  scene.add(mesh);
  return mesh;
}

function emitParticle(
  sys: THREE.Points,
  position: THREE.Vector3,
  velocity: THREE.Vector3,
  lifetime: number,
): void {
  const ud = sys.userData as ParticleUserData;
  let slot = -1;
  for (let tries = 0; tries < ud.count; tries++) {
    const idx = ud.nextSlot;
    ud.nextSlot = (ud.nextSlot + 1) % ud.count;
    if (!ud.active[idx] || ud.lifetimes[idx] <= 0) {
      slot = idx;
      break;
    }
  }
  if (slot < 0) {
    slot = ud.nextSlot;
    ud.nextSlot = (ud.nextSlot + 1) % ud.count;
  }

  const pos = (sys.geometry.attributes.position as THREE.BufferAttribute).array as Float32Array;
  ud.active[slot] = 1;
  ud.lifetimes[slot] = lifetime;
  ud.maxLife[slot] = lifetime;
  pos[slot * 3] = position.x;
  pos[slot * 3 + 1] = position.y;
  pos[slot * 3 + 2] = position.z;
  ud.velocities[slot * 3] = velocity.x;
  ud.velocities[slot * 3 + 1] = velocity.y;
  ud.velocities[slot * 3 + 2] = velocity.z;
}

// ================================================================
//  ENGINE CLASS
// ================================================================

export class LunarLanderEngine {
  // Three.js core
  private scene: THREE.Scene;
  private camera: THREE.PerspectiveCamera;
  private renderer: THREE.WebGLRenderer;
  private orbitControls: OrbitControls;
  private clock: THREE.Clock;
  private container: HTMLDivElement;

  // Scene objects
  private lmGroup!: THREE.Group;
  private terrain!: THREE.Mesh;
  private terrainGeom!: THREE.BufferGeometry;
  private shadowLight!: THREE.DirectionalLight;

  // Particle systems
  private exhaustSys!: THREE.Points;
  private dustSys!: THREE.Points;
  private rcsSystemsByName: Record<string, THREE.Points> = {};

  // Audio
  private audioCtx: AudioContext | null = null;
  private engineOsc: OscillatorNode | null = null;
  private engineGain: GainNode | null = null;
  private rcsOsc: OscillatorNode | null = null;
  private rcsGain: GainNode | null = null;

  // Ground Control callouts — real Apollo 11 transcript phrases
  private calloutQueue: { altitude: number; text: string; spoken: boolean }[] = [];
  private lastCalloutTime = 0;

  // Cinematic camera system (GTA V style)
  private cinematicActive = false;
  private cinematicTimer = 0;
  private cinematicAngle = 0;
  private cinematicPhase: 'idle' | 'approach-sweep' | 'low-orbit' | 'landing-dolly' | 'post-land-orbit' = 'idle';
  private cinematicSlowMo = 1.0;
  private prevTimeScale = 1.0;

  // State
  private lm!: LMState;
  private gameState: GameState = 'waiting';
  private cameraMode = 0;
  private prevCameraMode = -1;
  private physicsAccum = 0;
  private animFrameId = 0;
  private running = false;

  // Camera smoothing
  private camSmoothPos = new THREE.Vector3();
  private camSmoothTarget = new THREE.Vector3();
  private camInitialized = false;

  // Landing zone
  private landingZoneCenter = new THREE.Vector3(0, 0, 0);

  // Raycaster
  private altRay = new THREE.Raycaster();
  private readonly downVec = new THREE.Vector3(0, -1, 0);

  // Input
  private keys: Record<string, boolean> = {};

  // LM geometry helpers
  private footpadWorldPositions: THREE.Vector3[] = [];
  private rcsQuadPositions: THREE.Vector3[] = [];
  private readonly engineBellLocalPos = new THREE.Vector3(0, -2.6, 0);

  // Reusable temp vectors (no alloc in loop)
  private readonly _tmpEmitPos = new THREE.Vector3();
  private readonly _tmpEmitVel = new THREE.Vector3();
  private readonly _thrustVec = new THREE.Vector3();
  private readonly _rcsTransVec = new THREE.Vector3();
  private readonly _camDesiredPos = new THREE.Vector3();
  private readonly _camDesiredTarget = new THREE.Vector3();

  // Callbacks
  private landedCallbacks: LandedCallback[] = [];

  // Alien mothership Easter egg
  private mothership: THREE.Group | null = null;
  private mothershipActive = false;
  private mothershipProgress = 0;
  private mothershipTriggered = false;
  private mothershipShouldAppear = false;
  private mothershipLight: THREE.PointLight | null = null;
  private mothershipUndersideLights: THREE.Mesh[] = [];
  private mothershipAltitude = 400;
  private mothershipStartX = 0;
  private mothershipEndX = 0;
  private mothershipCalloutSpoken = false;
  // Mothership audio
  private mothershipOsc: OscillatorNode | null = null;
  private mothershipGain: GainNode | null = null;
  private mothershipOsc2: OscillatorNode | null = null;
  private mothershipGain2: GainNode | null = null;

  // Disposed flag
  private disposed = false;

  // Bound event handlers (for cleanup)
  private boundKeyDown: (e: KeyboardEvent) => void;
  private boundKeyUp: (e: KeyboardEvent) => void;
  private boundBlur: () => void;

  constructor(container: HTMLDivElement) {
    this.container = container;

    // Scene
    this.scene = new THREE.Scene();
    this.scene.background = new THREE.Color(0x000000);

    // Camera
    const w = container.clientWidth || window.innerWidth;
    const h = container.clientHeight || window.innerHeight;
    this.camera = new THREE.PerspectiveCamera(60, w / h, 0.1, 50000);
    this.camera.position.set(40, START_ALTITUDE + 20, 80);

    // Renderer
    this.renderer = new THREE.WebGLRenderer({ antialias: false, powerPreference: 'high-performance' });
    this.renderer.setSize(w, h);
    this.renderer.setPixelRatio(Math.min(window.devicePixelRatio, 1.5));
    this.renderer.shadowMap.enabled = true;
    this.renderer.shadowMap.type = THREE.PCFSoftShadowMap;
    this.renderer.toneMapping = THREE.ACESFilmicToneMapping;
    this.renderer.toneMappingExposure = 0.7;
    container.appendChild(this.renderer.domElement);

    // OrbitControls
    this.orbitControls = new OrbitControls(this.camera, this.renderer.domElement);
    this.orbitControls.enableDamping = true;
    this.orbitControls.dampingFactor = 0.08;
    this.orbitControls.minDistance = 5;
    this.orbitControls.maxDistance = 800;

    // Clock
    this.clock = new THREE.Clock(false);

    // Init LM state
    this.resetLMState();

    this.orbitControls.target.copy(this.lm.pos);

    // Build scene
    this.buildLighting();
    this.buildStarfield();
    this.buildEarth();
    this.buildTerrain();
    this.buildLunarModule();
    this.buildParticleSystems();

    // Input
    this.boundKeyDown = this.onKeyDown.bind(this);
    this.boundKeyUp = this.onKeyUp.bind(this);
    this.boundBlur = this.onBlur.bind(this);

    window.addEventListener('keydown', this.boundKeyDown);
    window.addEventListener('keyup', this.boundKeyUp);
    window.addEventListener('blur', this.boundBlur);

    // Pre-render one frame
    this.renderer.render(this.scene, this.camera);
  }

  // ── Public API ──────────────────────────────────────────────

  start(): void {
    if (this.running) return;
    this.running = true;
    this.gameState = 'flying';
    this.lm.startTime = performance.now();
    this.initAudio();
    this.clock.start();
    this.animate();
  }

  stop(): void {
    this.running = false;
    if (this.animFrameId) {
      cancelAnimationFrame(this.animFrameId);
      this.animFrameId = 0;
    }
  }

  restart(): void {
    this.resetLMState();
    this.gameState = 'flying';
    this.lm.startTime = performance.now();
    this.cameraMode = 0;
    this.camInitialized = false;
    this.physicsAccum = 0;

    // Reset callouts + cinematics
    this.initCallouts();
    this.cinematicActive = false;
    this.cinematicPhase = 'idle';
    this.cinematicSlowMo = 1.0;
    this.lastCalloutText = '';

    // Reset mothership
    this.mothershipActive = false;
    this.mothershipTriggered = false;
    this.mothershipShouldAppear = false;
    this.mothershipProgress = 0;
    this.mothershipCalloutSpoken = false;
    if (this.mothership) this.mothership.visible = false;
    if (this.mothershipGain) this.mothershipGain.gain.value = 0;
    if (this.mothershipGain2) this.mothershipGain2.gain.value = 0;

    this.camera.position.set(40, START_ALTITUDE + 20, 80);
    this.orbitControls.target.copy(this.lm.pos);
    this.orbitControls.update();

    this.clearAllParticles();

    if (!this.running) {
      this.running = true;
      this.clock.start();
      this.animate();
    }
  }

  dispose(): void {
    if (this.disposed) return;
    this.disposed = true;
    this.stop();

    window.removeEventListener('keydown', this.boundKeyDown);
    window.removeEventListener('keyup', this.boundKeyUp);
    window.removeEventListener('blur', this.boundBlur);

    // Audio cleanup (including mothership oscillators)
    if (this.mothershipOsc) { try { this.mothershipOsc.stop(); } catch { /* ignore */ } }
    if (this.mothershipOsc2) { try { this.mothershipOsc2.stop(); } catch { /* ignore */ } }
    this.mothershipOsc = null;
    this.mothershipOsc2 = null;
    this.mothershipGain = null;
    this.mothershipGain2 = null;
    if (this.audioCtx) {
      try { this.audioCtx.close(); } catch { /* ignore */ }
    }

    // Mothership cleanup
    this.mothership = null;
    this.mothershipLight = null;
    this.mothershipUndersideLights = [];

    // Three.js cleanup
    this.renderer.dispose();
    this.orbitControls.dispose();
    this.scene.traverse((obj) => {
      if ((obj as THREE.Mesh).geometry) (obj as THREE.Mesh).geometry.dispose();
      const mat = (obj as THREE.Mesh).material;
      if (mat) {
        if (Array.isArray(mat)) mat.forEach((m) => m.dispose());
        else (mat as THREE.Material).dispose();
      }
    });

    // Remove canvas from DOM
    if (this.renderer.domElement.parentElement) {
      this.renderer.domElement.parentElement.removeChild(this.renderer.domElement);
    }
  }

  resize(width: number, height: number): void {
    this.camera.aspect = width / height;
    this.camera.updateProjectionMatrix();
    this.renderer.setSize(width, height);
  }

  cycleCamera(): void {
    if (this.gameState === 'flying') {
      this.cameraMode = (this.cameraMode + 1) % CAMERA_MODES.length;
    }
  }

  getState(): LanderState {
    const vSpeed = this.lm.vel.y;
    const hSpeed = Math.sqrt(this.lm.vel.x ** 2 + this.lm.vel.z ** 2);
    const fuelPct = (this.lm.fuel / FUEL_MASS_INIT) * 100;
    const distLZ = Math.sqrt(this.lm.pos.x ** 2 + this.lm.pos.z ** 2);
    const euler = new THREE.Euler().setFromQuaternion(this.lm.quat, 'YXZ');
    const pitchDeg = euler.x * 180 / Math.PI;
    const rollDeg = euler.z * 180 / Math.PI;
    const yawDeg = euler.y * 180 / Math.PI;

    let phase: string;
    if (this.lm.fuel <= 0)        phase = 'BINGO FUEL';
    else if (this.lm.altitude < 20)  phase = 'TERMINAL DESCENT';
    else if (this.lm.altitude < 100) phase = 'APPROACH PHASE';
    else if (this.lm.altitude < 300) phase = 'BRAKING PHASE';
    else                              phase = 'DESCENT ORBIT';

    const elapsed = this.gameState === 'flying'
      ? (performance.now() - this.lm.startTime) / 1000
      : 0;
    const mins = Math.floor(elapsed / 60);
    const secs = Math.floor(elapsed % 60);
    const missionTime = `${mins}:${secs < 10 ? '0' : ''}${secs}`;

    return {
      altitude: Math.max(0, this.lm.altitude),
      verticalSpeed: vSpeed,
      horizontalSpeed: hSpeed,
      rangeToLZ: distLZ,
      fuelPercent: fuelPct,
      throttlePercent: this.lm.throttle * 100,
      pitchDeg,
      rollDeg,
      yawDeg,
      massKg: DRY_MASS + this.lm.fuel,
      cameraMode: CAMERA_MODES[this.cameraMode],
      phase,
      missionTime,
      contactLight: this.lm.contactLight,
      gameState: this.gameState,
      calloutText: (performance.now() - this.lastCalloutDisplayTime < 4000) ? this.lastCalloutText : '',
      cinematicMode: this.cinematicActive,
      mothershipVisible: this.mothershipActive,
    };
  }

  onLanded(callback: LandedCallback): void {
    this.landedCallbacks.push(callback);
  }

  // ── LM State Reset ─────────────────────────────────────────

  private resetLMState(): void {
    this.lm = {
      pos: new THREE.Vector3(0, START_ALTITUDE, 0),
      vel: new THREE.Vector3(2.0, -5.0, 0.5),
      quat: new THREE.Quaternion(),
      angVel: new THREE.Vector3(0, 0, 0),
      fuel: FUEL_MASS_INIT,
      throttle: 0,
      altitude: START_ALTITUDE,
      contactLight: false,
      startTime: 0,
      flightTime: 0,
    };
    this.lm.quat.setFromEuler(new THREE.Euler(0.02, 0.1, -0.01, 'YXZ'));
  }

  // ── Audio ───────────────────────────────────────────────────

  private initAudio(): void {
    try {
      this.audioCtx = new (window.AudioContext || (window as any).webkitAudioContext)();
    } catch {
      return;
    }

    // Engine: low rumble
    this.engineOsc = this.audioCtx.createOscillator();
    this.engineOsc.type = 'sawtooth';
    this.engineOsc.frequency.value = 80;

    const engineFilter = this.audioCtx.createBiquadFilter();
    engineFilter.type = 'lowpass';
    engineFilter.frequency.value = 180;
    engineFilter.Q.value = 1;

    this.engineGain = this.audioCtx.createGain();
    this.engineGain.gain.value = 0;

    const vibrato = this.audioCtx.createOscillator();
    vibrato.frequency.value = 7;
    const vibratoGain = this.audioCtx.createGain();
    vibratoGain.gain.value = 3;
    vibrato.connect(vibratoGain);
    vibratoGain.connect(this.engineOsc.frequency);
    vibrato.start();

    this.engineOsc.connect(engineFilter);
    engineFilter.connect(this.engineGain);
    this.engineGain.connect(this.audioCtx.destination);
    this.engineOsc.start();

    // RCS: short hiss
    this.rcsOsc = this.audioCtx.createOscillator();
    this.rcsOsc.type = 'square';
    this.rcsOsc.frequency.value = 180;

    const rcsFilter = this.audioCtx.createBiquadFilter();
    rcsFilter.type = 'bandpass';
    rcsFilter.frequency.value = 350;
    rcsFilter.Q.value = 3;

    this.rcsGain = this.audioCtx.createGain();
    this.rcsGain.gain.value = 0;

    this.rcsOsc.connect(rcsFilter);
    rcsFilter.connect(this.rcsGain);
    this.rcsGain.connect(this.audioCtx.destination);
    this.rcsOsc.start();
  }

  private playLandingThud(intensity: number): void {
    if (!this.audioCtx) return;
    const ctx = this.audioCtx;

    const o = ctx.createOscillator();
    o.type = 'sine';
    o.frequency.value = 35 + intensity * 20;
    const g = ctx.createGain();
    g.gain.value = Math.min(0.6, 0.1 + intensity * 0.3);
    g.gain.exponentialRampToValueAtTime(0.001, ctx.currentTime + 0.6);
    o.connect(g);
    g.connect(ctx.destination);
    o.start();
    o.stop(ctx.currentTime + 0.6);

    const o2 = ctx.createOscillator();
    o2.type = 'triangle';
    o2.frequency.value = 60;
    const g2 = ctx.createGain();
    g2.gain.value = Math.min(0.3, intensity * 0.2);
    g2.gain.exponentialRampToValueAtTime(0.001, ctx.currentTime + 0.3);
    o2.connect(g2);
    g2.connect(ctx.destination);
    o2.start();
    o2.stop(ctx.currentTime + 0.3);
  }

  // ── Ground Control Callouts (Apollo 11 transcript) ─────────

  private initCallouts(): void {
    this.calloutQueue = [
      { altitude: 480, text: "Houston, Eagle. We're GO for powered descent.", spoken: false },
      { altitude: 400, text: "Eagle, Houston. You are GO for landing.", spoken: false },
      { altitude: 300, text: "Altitude three hundred. Looking good.", spoken: false },
      { altitude: 200, text: "Eagle looking great. You're GO.", spoken: false },
      { altitude: 150, text: "Altitude one fifty. Descent rate looking good.", spoken: false },
      { altitude: 100, text: "One hundred feet. Three and a half down.", spoken: false },
      { altitude: 75, text: "Seventy five feet. Looking good. Down a half.", spoken: false },
      { altitude: 60, text: "Sixty seconds of fuel remaining.", spoken: false },
      { altitude: 40, text: "Forty feet. Down two and a half. Picking up some dust.", spoken: false },
      { altitude: 30, text: "Thirty feet. Two and a half down. Faint shadow.", spoken: false },
      { altitude: 20, text: "Twenty feet. Kicking up some dust.", spoken: false },
      { altitude: 10, text: "Ten feet. Two and a half down.", spoken: false },
      { altitude: 5, text: "Five feet. Drifting forward just a little.", spoken: false },
      { altitude: 1.5, text: "Contact light.", spoken: false },
    ];
  }

  private checkCallouts(): void {
    if (this.gameState !== 'flying') return;
    const now = performance.now();
    if (now - this.lastCalloutTime < 3000) return; // min 3s between callouts

    for (const c of this.calloutQueue) {
      if (!c.spoken && this.lm.altitude <= c.altitude) {
        c.spoken = true;
        this.lastCalloutTime = now;
        this.speakCallout(c.text);
        break;
      }
    }
  }

  private speakCallout(text: string): void {
    if (typeof window === 'undefined' || !window.speechSynthesis) return;
    const u = new SpeechSynthesisUtterance(text);
    u.rate = 0.95;
    u.pitch = 0.85;
    u.volume = 0.7;
    // Try to find a male English voice
    const voices = window.speechSynthesis.getVoices();
    const maleEn = voices.find(v => v.lang.startsWith('en') && /male|david|james|daniel/i.test(v.name));
    if (maleEn) u.voice = maleEn;
    else {
      const anyEn = voices.find(v => v.lang.startsWith('en'));
      if (anyEn) u.voice = anyEn;
    }
    window.speechSynthesis.cancel();
    window.speechSynthesis.speak(u);

    // Post callout text for HUD display
    this.lastCalloutText = text;
    this.lastCalloutDisplayTime = performance.now();
  }

  private lastCalloutText = '';
  private lastCalloutDisplayTime = 0;

  private speakLandingResult(success: boolean): void {
    if (typeof window === 'undefined' || !window.speechSynthesis) return;
    const text = success
      ? "Houston, Tranquility Base here. The Eagle has landed."
      : "Houston, we've had a problem.";
    setTimeout(() => this.speakCallout(text), 500);
    if (success) {
      setTimeout(() => this.speakCallout("Roger, Tranquility. We copy you on the ground. You got a bunch of guys about to turn blue. We're breathing again. Thanks a lot."), 5000);
    }
  }

  // ── GTA V Style Cinematic Camera ──────────────────────────

  /** Toggle cinematic mode with V key */
  toggleCinematic(): void {
    this.cinematicActive = !this.cinematicActive;
    if (this.cinematicActive) {
      this.cinematicPhase = 'approach-sweep';
      this.cinematicTimer = 0;
      this.cinematicAngle = 0;
    } else {
      this.cinematicPhase = 'idle';
      this.cinematicSlowMo = 1.0;
    }
  }

  get isCinematic(): boolean { return this.cinematicActive; }

  private updateCinematic(dt: number): void {
    if (!this.cinematicActive || this.gameState !== 'flying') return;
    this.cinematicTimer += dt;
    this.cinematicAngle += dt * 0.15;

    const lmPos = this.lm.pos;
    const alt = this.lm.altitude;

    // Auto-switch cinematic phase based on altitude
    if (alt > 200) this.cinematicPhase = 'approach-sweep';
    else if (alt > 50) this.cinematicPhase = 'low-orbit';
    else this.cinematicPhase = 'landing-dolly';

    // Slow-mo ramp as we get close to surface
    this.cinematicSlowMo = alt < 20 ? 0.3 : alt < 50 ? 0.6 : 1.0;

    const cam = this.camera;
    switch (this.cinematicPhase) {
      case 'approach-sweep': {
        // Wide sweeping orbit — dramatic establishing shot
        const dist = 80 + Math.sin(this.cinematicTimer * 0.1) * 30;
        const height = 40 + Math.sin(this.cinematicTimer * 0.15) * 15;
        const angle = this.cinematicAngle;
        cam.position.set(
          lmPos.x + Math.cos(angle) * dist,
          lmPos.y + height,
          lmPos.z + Math.sin(angle) * dist,
        );
        cam.lookAt(lmPos.x, lmPos.y - 5, lmPos.z);
        break;
      }
      case 'low-orbit': {
        // Tighter orbit with dramatic low angles
        const t = this.cinematicTimer;
        const cutIndex = Math.floor(t / 4) % 3; // cut every 4 seconds
        const dist = [35, 20, 50][cutIndex];
        const heightOff = [-5, 0, 15][cutIndex]; // some angles look UP at the LM
        const speedMult = [1, -0.7, 0.5][cutIndex];
        const angle = this.cinematicAngle * speedMult;
        cam.position.set(
          lmPos.x + Math.cos(angle) * dist,
          lmPos.y + heightOff,
          lmPos.z + Math.sin(angle) * dist,
        );
        cam.lookAt(lmPos.x, lmPos.y, lmPos.z);
        break;
      }
      case 'landing-dolly': {
        // Close-up tracking shot — dolly alongside, looking slightly up
        const t = this.cinematicTimer;
        const cutIndex = Math.floor(t / 3) % 4;
        switch (cutIndex) {
          case 0: // Side profile — engine and dust
            cam.position.set(lmPos.x + 12, lmPos.y - 2, lmPos.z + 5);
            cam.lookAt(lmPos.x, lmPos.y - 3, lmPos.z);
            break;
          case 1: // Ground looking up — dramatic low angle
            cam.position.set(lmPos.x + 8, lmPos.y - alt + 1, lmPos.z + 8);
            cam.lookAt(lmPos.x, lmPos.y, lmPos.z);
            break;
          case 2: // Behind and above — descent trajectory shot
            cam.position.set(lmPos.x - 15, lmPos.y + 10, lmPos.z - 10);
            cam.lookAt(lmPos.x, lmPos.y - 5, lmPos.z);
            break;
          case 3: // Extreme close-up on footpads
            cam.position.set(lmPos.x + 4, lmPos.y - alt + 2, lmPos.z + 3);
            cam.lookAt(lmPos.x, lmPos.y - alt + 0.5, lmPos.z);
            break;
        }
        break;
      }
      case 'post-land-orbit': {
        // Slow victory orbit after landing
        const dist = 25;
        const angle = this.cinematicAngle * 0.3;
        cam.position.set(
          lmPos.x + Math.cos(angle) * dist,
          lmPos.y + 8,
          lmPos.z + Math.sin(angle) * dist,
        );
        cam.lookAt(lmPos.x, lmPos.y, lmPos.z);
        break;
      }
    }
  }

  private startPostLandCinematic(): void {
    this.cinematicActive = true;
    this.cinematicPhase = 'post-land-orbit';
    this.cinematicTimer = 0;
    this.cinematicAngle = 0;
  }

  // ── Lighting ────────────────────────────────────────────────

  private buildLighting(): void {
    // Sun — low angle like Apollo landing photos
    this.shadowLight = new THREE.DirectionalLight(0xfff8e8, 3.0);
    this.shadowLight.position.set(300, 180, -400);
    this.shadowLight.castShadow = true;
    this.shadowLight.shadow.mapSize.set(1024, 1024);
    this.shadowLight.shadow.camera.left = -30;
    this.shadowLight.shadow.camera.right = 30;
    this.shadowLight.shadow.camera.top = 30;
    this.shadowLight.shadow.camera.bottom = -30;
    this.shadowLight.shadow.camera.near = 50;
    this.shadowLight.shadow.camera.far = 800;
    this.shadowLight.shadow.bias = -0.001;
    this.scene.add(this.shadowLight);
    this.scene.add(this.shadowLight.target);

    // Earthshine ambient
    const ambient = new THREE.AmbientLight(0x334466, 0.12);
    this.scene.add(ambient);

    // Hemisphere for visual depth
    const hemi = new THREE.HemisphereLight(0x444466, 0x111111, 0.08);
    this.scene.add(hemi);
  }

  // ── Starfield ───────────────────────────────────────────────

  private buildStarfield(): void {
    const positions = new Float32Array(STAR_COUNT * 3);
    const colors = new Float32Array(STAR_COUNT * 3);

    for (let i = 0; i < STAR_COUNT; i++) {
      const theta = Math.random() * Math.PI * 2;
      const phi = Math.acos(2 * Math.random() - 1);
      const r = 20000 + Math.random() * 5000;
      const i3 = i * 3;

      positions[i3] = r * Math.sin(phi) * Math.cos(theta);
      positions[i3 + 1] = r * Math.sin(phi) * Math.sin(theta);
      positions[i3 + 2] = r * Math.cos(phi);

      const brightness = 0.3 + Math.random() * 0.7;
      const temp = Math.random();
      if (temp < 0.08) {
        colors[i3] = brightness * 0.7;
        colors[i3 + 1] = brightness * 0.8;
        colors[i3 + 2] = brightness;
      } else if (temp < 0.15) {
        colors[i3] = brightness;
        colors[i3 + 1] = brightness * 0.6;
        colors[i3 + 2] = brightness * 0.3;
      } else {
        colors[i3] = brightness;
        colors[i3 + 1] = brightness * 0.98;
        colors[i3 + 2] = brightness * 0.95;
      }
    }

    const geo = new THREE.BufferGeometry();
    geo.setAttribute('position', new THREE.BufferAttribute(positions, 3));
    geo.setAttribute('color', new THREE.BufferAttribute(colors, 3));

    const mat = new THREE.PointsMaterial({
      size: 1.5,
      vertexColors: true,
      sizeAttenuation: false,
      transparent: true,
      opacity: 0.85,
    });

    this.scene.add(new THREE.Points(geo, mat));
  }

  // ── Earth ───────────────────────────────────────────────────

  private buildEarth(): void {
    const earthGroup = new THREE.Group();

    const earthGeo = new THREE.SphereGeometry(150, 48, 48);
    const earthMat = new THREE.MeshPhongMaterial({
      color: 0x2244aa,
      emissive: 0x112244,
      emissiveIntensity: 0.4,
      shininess: 40,
      specular: 0x333355,
    });
    earthGroup.add(new THREE.Mesh(earthGeo, earthMat));

    const atmosGeo = new THREE.SphereGeometry(156, 48, 48);
    const atmosMat = new THREE.MeshBasicMaterial({
      color: 0x6699ff,
      transparent: true,
      opacity: 0.12,
      side: THREE.BackSide,
    });
    earthGroup.add(new THREE.Mesh(atmosGeo, atmosMat));

    const limbGeo = new THREE.SphereGeometry(152, 48, 48);
    const limbMat = new THREE.MeshBasicMaterial({
      color: 0x88bbff,
      transparent: true,
      opacity: 0.08,
      side: THREE.FrontSide,
    });
    earthGroup.add(new THREE.Mesh(limbGeo, limbMat));

    const dist = 16000;
    const elev = Math.sin((30 * Math.PI) / 180) * dist;
    const horiz = Math.cos((30 * Math.PI) / 180) * dist;
    earthGroup.position.set(horiz * 0.7, elev, -horiz * 0.7);

    this.scene.add(earthGroup);
  }

  // ── Terrain ─────────────────────────────────────────────────

  private buildTerrain(): void {
    this.terrainGeom = new THREE.PlaneGeometry(TERRAIN_SIZE, TERRAIN_SIZE, TERRAIN_SEGS, TERRAIN_SEGS);
    this.terrainGeom.rotateX(-Math.PI / 2);

    const pos = this.terrainGeom.attributes.position as THREE.BufferAttribute;
    const vCount = pos.count;

    // Pre-generate craters
    const craterList: Array<{ x: number; z: number; radius: number; depth: number; rimHeight: number }> = [];
    const rng = mulberry32(42);
    for (let i = 0; i < 45; i++) {
      const cx = (rng() - 0.5) * TERRAIN_SIZE * 0.85;
      const cz = (rng() - 0.5) * TERRAIN_SIZE * 0.85;
      const d = Math.sqrt(cx * cx + cz * cz);
      if (d < 55) continue;
      craterList.push({
        x: cx,
        z: cz,
        radius: 8 + rng() * 70,
        depth: 2 + rng() * 18,
        rimHeight: 0.2 + rng() * 0.4,
      });
    }

    // Sculpt each vertex
    for (let i = 0; i < vCount; i++) {
      const vx = pos.getX(i);
      const vz = pos.getZ(i);

      let h = 0;
      h += fbm(vx * 0.002, vz * 0.002, 5, 2.0, 0.5) * 50 - 25;
      h += fbm(vx * 0.008, vz * 0.008, 4, 2.2, 0.45) * 10;
      h += fbm(vx * 0.03, vz * 0.03, 3, 2.0, 0.5) * 3;
      h += fbm(vx * 0.1, vz * 0.1, 2, 2.0, 0.5) * 0.8;

      for (const c of craterList) {
        const dx = vx - c.x;
        const dz = vz - c.z;
        const dist = Math.sqrt(dx * dx + dz * dz);
        const r = c.radius;
        if (dist < r * 1.4) {
          const t = dist / r;
          if (t < 1.0) {
            h -= c.depth * (1 - t * t);
            if (t < 0.3) h += c.depth * 0.15 * (1 - t / 0.3);
          } else if (t < 1.4) {
            const rimT = (t - 1.0) / 0.4;
            h += c.depth * c.rimHeight * Math.pow(1 - rimT, 2);
          }
        }
      }

      // Flatten landing zone
      const lzDist = Math.sqrt(vx * vx + vz * vz);
      if (lzDist < 50) {
        const blend = smoothstep(50, 20, lzDist);
        h = h * (1 - blend);
      }

      pos.setY(i, h);
    }

    this.terrainGeom.computeVertexNormals();

    // Vertex colors
    const vertColors = new Float32Array(vCount * 3);
    const normals = this.terrainGeom.attributes.normal as THREE.BufferAttribute;

    for (let i = 0; i < vCount; i++) {
      const vx = pos.getX(i);
      const vy = pos.getY(i);
      const vz = pos.getZ(i);
      const ny = normals.getY(i);
      const slope = 1 - ny;

      const n1 = fbm(vx * 0.015, vz * 0.015, 2, 2.0, 0.5);
      const n2 = fbm(vx * 0.05, vz * 0.05, 2, 2.0, 0.5);
      let base = 0.22 + n1 * 0.12 + n2 * 0.05;
      base += vy * 0.002;
      base -= slope * 0.2;
      if (vy < -5) base -= 0.03;
      base = Math.max(0.08, Math.min(0.5, base));

      vertColors[i * 3] = base * 1.03;
      vertColors[i * 3 + 1] = base * 1.0;
      vertColors[i * 3 + 2] = base * 0.93;
    }

    this.terrainGeom.setAttribute('color', new THREE.BufferAttribute(vertColors, 3));

    const terrainMat = new THREE.MeshStandardMaterial({
      vertexColors: true,
      roughness: 0.95,
      metalness: 0.0,
    });

    this.terrain = new THREE.Mesh(this.terrainGeom, terrainMat);
    this.terrain.receiveShadow = true;
    this.scene.add(this.terrain);

    // Landing zone markers
    const markerColor = 0x887733;
    const markerMat = new THREE.MeshBasicMaterial({ color: markerColor });
    const lzY = this.sampleTerrainHeight(0, 0) + 0.08;
    this.landingZoneCenter.y = lzY;

    // Cross arms
    for (let a = 0; a < 4; a++) {
      const arm = new THREE.Mesh(new THREE.BoxGeometry(14, 0.06, 0.3), markerMat);
      arm.rotation.y = (a * Math.PI) / 4;
      arm.position.y = lzY;
      this.scene.add(arm);
    }

    // Concentric circles
    [15, 28].forEach((radius) => {
      const ring = new THREE.Mesh(new THREE.RingGeometry(radius - 0.15, radius + 0.15, 48), markerMat);
      ring.rotation.x = -Math.PI / 2;
      ring.position.y = lzY;
      this.scene.add(ring);
    });

    // Dot markers
    for (let i = 0; i < 8; i++) {
      const angle = (i * Math.PI) / 4;
      const dot = new THREE.Mesh(new THREE.CircleGeometry(0.5, 8), markerMat);
      dot.rotation.x = -Math.PI / 2;
      dot.position.set(Math.cos(angle) * 21, lzY + 0.02, Math.sin(angle) * 21);
      this.scene.add(dot);
    }
  }

  private sampleTerrainHeight(x: number, z: number): number {
    const origin = new THREE.Vector3(x, 1000, z);
    this.altRay.set(origin, this.downVec);
    this.altRay.far = 2000;
    const hits = this.altRay.intersectObject(this.terrain, false);
    return hits.length > 0 ? hits[0].point.y : 0;
  }

  // ── Lunar Module Geometry ───────────────────────────────────

  private buildLunarModule(): void {
    this.lmGroup = new THREE.Group();

    // Material palette
    const kaptonGold = new THREE.MeshStandardMaterial({
      color: 0xc8a832, roughness: 0.55, metalness: 0.75,
      emissive: 0x1a1200, emissiveIntensity: 0.08,
    });
    const kaptonDark = new THREE.MeshStandardMaterial({
      color: 0x998822, roughness: 0.6, metalness: 0.6,
    });
    const silverMetal = new THREE.MeshStandardMaterial({
      color: 0xaaaaaa, roughness: 0.3, metalness: 0.8,
    });
    const grayMetal = new THREE.MeshStandardMaterial({
      color: 0x888888, roughness: 0.5, metalness: 0.6,
    });
    const darkMetal = new THREE.MeshStandardMaterial({
      color: 0x555555, roughness: 0.6, metalness: 0.5,
    });
    const windowMat = new THREE.MeshStandardMaterial({
      color: 0x0a1520, roughness: 0.05, metalness: 0.95,
      emissive: 0x001118, emissiveIntensity: 0.15,
    });
    const legMat = new THREE.MeshStandardMaterial({
      color: 0xbbbbbb, roughness: 0.45, metalness: 0.7,
    });
    const padMat = new THREE.MeshStandardMaterial({
      color: 0x999999, roughness: 0.7, metalness: 0.4,
    });
    const bellMat = new THREE.MeshStandardMaterial({
      color: 0x777777, roughness: 0.2, metalness: 0.9, side: THREE.DoubleSide,
    });

    // ── DESCENT STAGE ──
    const descent = new THREE.Group();
    descent.name = 'descent';

    // Main octagonal body
    const bodyGeo = new THREE.CylinderGeometry(2.2, 2.2, 1.7, 8);
    const body = new THREE.Mesh(bodyGeo, kaptonGold);
    body.castShadow = true;
    descent.add(body);

    // Top rim
    const topRimGeo = new THREE.CylinderGeometry(2.25, 2.25, 0.08, 8);
    const topRim = new THREE.Mesh(topRimGeo, silverMetal);
    topRim.translateY(0.85);
    descent.add(topRim);

    // Bottom rim
    const botRimGeo = new THREE.CylinderGeometry(2.25, 2.25, 0.08, 8);
    const botRim = new THREE.Mesh(botRimGeo, silverMetal);
    botRim.translateY(-0.85);
    descent.add(botRim);

    // Lower skirt
    const skirtGeo = new THREE.CylinderGeometry(2.35, 1.7, 0.7, 8);
    const skirt = new THREE.Mesh(skirtGeo, kaptonDark);
    skirt.position.y = -1.2;
    skirt.castShadow = true;
    descent.add(skirt);

    // Engine bell (DPS)
    const engBellGeo = new THREE.ConeGeometry(0.65, 1.4, 16, 1, true);
    const bell = new THREE.Mesh(engBellGeo, bellMat);
    bell.rotation.x = Math.PI;
    bell.position.y = -2.2;
    descent.add(bell);

    // Inner engine throat
    const throatGeo = new THREE.CylinderGeometry(0.2, 0.35, 0.4, 12, 1, true);
    const throat = new THREE.Mesh(throatGeo, darkMetal);
    throat.position.y = -1.6;
    descent.add(throat);

    // === LANDING LEGS (4) ===
    this.footpadWorldPositions = [];
    for (let i = 0; i < 4; i++) {
      const angle = (i * Math.PI) / 2 + Math.PI / 4;
      const cos = Math.cos(angle);
      const sin = Math.sin(angle);

      const footX = cos * 4.0;
      const footZ = sin * 4.0;
      const footY = -3.0;

      // Primary strut
      const strutLen = new THREE.Vector3(footX - cos * 2.0, footY, footZ - sin * 2.0).length();
      const strut1 = new THREE.Mesh(new THREE.CylinderGeometry(0.045, 0.045, strutLen, 6), legMat);
      const mid1 = new THREE.Vector3((cos * 2.0 + footX) / 2, footY / 2, (sin * 2.0 + footZ) / 2);
      strut1.position.copy(mid1);
      strut1.lookAt(new THREE.Vector3(footX, footY, footZ));
      strut1.rotateX(Math.PI / 2);
      descent.add(strut1);

      // Secondary strut
      const strut2Len = new THREE.Vector3(footX - cos * 1.5, footY - 0.7, footZ - sin * 1.5).length();
      const strut2 = new THREE.Mesh(new THREE.CylinderGeometry(0.035, 0.035, strut2Len, 6), legMat);
      const mid2 = new THREE.Vector3((cos * 1.5 + footX) / 2, (0.7 + footY) / 2, (sin * 1.5 + footZ) / 2);
      strut2.position.copy(mid2);
      strut2.lookAt(new THREE.Vector3(footX, footY, footZ));
      strut2.rotateX(Math.PI / 2);
      descent.add(strut2);

      // Cross-brace
      const braceLen = new THREE.Vector3(cos * 2.0, -0.5, sin * 2.0).distanceTo(
        new THREE.Vector3(footX * 0.7, footY * 0.5, footZ * 0.7),
      );
      const brace = new THREE.Mesh(new THREE.CylinderGeometry(0.02, 0.02, braceLen, 4), legMat);
      brace.position.set(
        (cos * 2.0 + footX * 0.7) / 2,
        (-0.5 + footY * 0.5) / 2,
        (sin * 2.0 + footZ * 0.7) / 2,
      );
      brace.lookAt(new THREE.Vector3(footX * 0.7, footY * 0.5, footZ * 0.7));
      brace.rotateX(Math.PI / 2);
      descent.add(brace);

      // Footpad
      const pad = new THREE.Mesh(new THREE.CylinderGeometry(0.55, 0.55, 0.06, 16), padMat);
      pad.position.set(footX, footY, footZ);
      pad.castShadow = true;
      descent.add(pad);

      this.footpadWorldPositions.push(new THREE.Vector3(footX, footY, footZ));

      // Contact probe
      if (i !== 2) {
        const probe = new THREE.Mesh(new THREE.CylinderGeometry(0.01, 0.01, 1.8, 4), grayMetal);
        probe.position.set(footX, footY - 0.9, footZ);
        descent.add(probe);
      }
    }

    // Thermal panels
    for (let i = 0; i < 8; i++) {
      const angle = (i * Math.PI) / 4 + Math.PI / 8;
      const panelGeo = new THREE.PlaneGeometry(1.6, 1.5);
      const panelMat2 = (i % 2 === 0) ? kaptonGold.clone() : silverMetal.clone();
      panelMat2.side = THREE.DoubleSide;
      const panel = new THREE.Mesh(panelGeo, panelMat2);
      panel.position.set(Math.cos(angle) * 2.25, 0, Math.sin(angle) * 2.25);
      panel.lookAt(new THREE.Vector3(0, 0, 0));
      descent.add(panel);
    }

    // Equipment bay boxes
    for (let i = 0; i < 4; i++) {
      const angle = (i * Math.PI) / 2;
      const box = new THREE.Mesh(new THREE.BoxGeometry(0.8, 0.5, 0.3), grayMetal);
      box.position.set(Math.cos(angle) * 2.0, -0.5, Math.sin(angle) * 2.0);
      box.lookAt(new THREE.Vector3(0, -0.5, 0));
      descent.add(box);
    }

    this.lmGroup.add(descent);

    // ── ASCENT STAGE ──
    const ascent = new THREE.Group();
    ascent.name = 'ascent';

    // Crew cabin
    const cabinGeo = new THREE.BoxGeometry(2.6, 1.9, 2.6);
    const cabin = new THREE.Mesh(cabinGeo, grayMetal);
    cabin.castShadow = true;
    ascent.add(cabin);

    // Cabin edge trim
    const edgeGeo = new THREE.EdgesGeometry(cabinGeo);
    const edgeMat = new THREE.LineBasicMaterial({ color: 0x666666 });
    ascent.add(new THREE.LineSegments(edgeGeo, edgeMat));

    // Thermal wrap on cabin sides
    for (let side = 0; side < 4; side++) {
      const angle = (side * Math.PI) / 2;
      const wrapGeo = new THREE.PlaneGeometry(2.4, 1.7);
      const wrapMat = new THREE.MeshStandardMaterial({
        color: side < 2 ? 0xc8a832 : 0x888888,
        roughness: 0.65, metalness: 0.55, side: THREE.DoubleSide,
      });
      const wrap = new THREE.Mesh(wrapGeo, wrapMat);
      wrap.position.set(Math.cos(angle) * 1.32, 0, Math.sin(angle) * 1.32);
      wrap.lookAt(new THREE.Vector3(0, 0, 0));
      ascent.add(wrap);
    }

    // Mid-section tunnel
    const midGeo = new THREE.CylinderGeometry(0.9, 1.1, 0.7, 8);
    const mid = new THREE.Mesh(midGeo, kaptonDark);
    mid.position.y = -1.3;
    mid.castShadow = true;
    ascent.add(mid);

    // Triangular windows
    for (let side = -1; side <= 1; side += 2) {
      const shape = new THREE.Shape();
      shape.moveTo(-0.22, -0.18);
      shape.lineTo(0.22, -0.18);
      shape.lineTo(0, 0.28);
      shape.closePath();
      const winGeo = new THREE.ShapeGeometry(shape);
      const win = new THREE.Mesh(winGeo, windowMat);
      win.position.set(side * 0.55, 0.15, 1.305);
      ascent.add(win);

      const frameGeo = new THREE.EdgesGeometry(winGeo);
      const frame = new THREE.LineSegments(frameGeo, new THREE.LineBasicMaterial({ color: 0x444444 }));
      frame.position.copy(win.position);
      frame.position.z += 0.002;
      ascent.add(frame);
    }

    // Docking port
    const dockGeo = new THREE.CylinderGeometry(0.38, 0.42, 0.55, 16);
    const dock = new THREE.Mesh(dockGeo, silverMetal);
    dock.position.y = 1.25;
    dock.castShadow = true;
    ascent.add(dock);

    // Docking ring
    const ringGeo = new THREE.TorusGeometry(0.42, 0.04, 8, 20);
    const ring = new THREE.Mesh(ringGeo, silverMetal);
    ring.position.y = 1.55;
    ring.rotation.x = Math.PI / 2;
    ascent.add(ring);

    // Docking target
    const targetGeo = new THREE.RingGeometry(0.08, 0.12, 12);
    const targetMat = new THREE.MeshBasicMaterial({ color: 0xffff00, side: THREE.DoubleSide });
    const target = new THREE.Mesh(targetGeo, targetMat);
    target.position.y = 1.56;
    target.rotation.x = -Math.PI / 2;
    ascent.add(target);

    // S-Band antenna mast
    const mastGeo = new THREE.CylinderGeometry(0.035, 0.035, 2.2, 6);
    const mast = new THREE.Mesh(mastGeo, legMat);
    mast.position.set(1.5, 1.95, 0);
    ascent.add(mast);

    const mastBrace = new THREE.Mesh(new THREE.CylinderGeometry(0.02, 0.02, 1.8, 4), legMat);
    mastBrace.position.set(1.35, 1.4, 0);
    mastBrace.rotation.z = -0.35;
    ascent.add(mastBrace);

    const dishGeo = new THREE.SphereGeometry(0.55, 16, 12, 0, Math.PI * 2, 0, Math.PI * 0.45);
    const dishMat = new THREE.MeshStandardMaterial({
      color: 0xdddddd, roughness: 0.2, metalness: 0.8, side: THREE.DoubleSide,
    });
    const dish = new THREE.Mesh(dishGeo, dishMat);
    dish.position.set(1.5, 3.1, 0);
    dish.rotation.x = Math.PI;
    ascent.add(dish);

    const feedGeo = new THREE.CylinderGeometry(0.02, 0.04, 0.4, 6);
    const feed = new THREE.Mesh(feedGeo, grayMetal);
    feed.position.set(1.5, 2.75, 0);
    ascent.add(feed);

    // VHF antennas
    const antMat = new THREE.MeshBasicMaterial({ color: 0xbbbbbb });
    for (let a = 0; a < 2; a++) {
      const antGeo = new THREE.CylinderGeometry(0.012, 0.012, 2.2, 4);
      const ant = new THREE.Mesh(antGeo, antMat);
      ant.position.set(-1.2, 2.0, 0);
      ant.rotation.z = Math.PI / 4;
      if (a === 1) ant.rotation.y = Math.PI / 2;
      ascent.add(ant);
    }

    const vhfMast = new THREE.Mesh(new THREE.CylinderGeometry(0.025, 0.025, 1.2, 4), legMat);
    vhfMast.position.set(-1.2, 1.35, 0);
    ascent.add(vhfMast);

    // RCS thruster quads
    this.rcsQuadPositions = [];
    const rcsOffsets = [
      { x: 1.55, z: 1.55 },
      { x: -1.55, z: 1.55 },
      { x: -1.55, z: -1.55 },
      { x: 1.55, z: -1.55 },
    ];

    for (const off of rcsOffsets) {
      const housing = new THREE.Mesh(new THREE.BoxGeometry(0.3, 0.3, 0.3), darkMetal);
      housing.position.set(off.x, 0.25, off.z);
      ascent.add(housing);

      for (let n = 0; n < 4; n++) {
        const na = (n * Math.PI) / 2;
        const nozzle = new THREE.Mesh(new THREE.ConeGeometry(0.055, 0.18, 6), silverMetal);
        const nx = off.x + Math.cos(na) * 0.25;
        const nz = off.z + Math.sin(na) * 0.25;
        nozzle.position.set(nx, 0.25, nz);
        nozzle.lookAt(new THREE.Vector3(off.x + Math.cos(na) * 3, 0.25, off.z + Math.sin(na) * 3));
        nozzle.rotateX(Math.PI / 2);
        ascent.add(nozzle);
      }

      this.rcsQuadPositions.push(new THREE.Vector3(off.x, 0.25, off.z));
    }

    // Ascent engine
    const aeBell = new THREE.Mesh(new THREE.ConeGeometry(0.3, 0.5, 12, 1, true), bellMat);
    aeBell.rotation.x = Math.PI;
    aeBell.position.y = -1.6;
    ascent.add(aeBell);

    // Ladder
    const ladderGroup = new THREE.Group();
    const rungMat = new THREE.MeshBasicMaterial({ color: 0x999999 });
    for (let r = 0; r < 8; r++) {
      const rung = new THREE.Mesh(new THREE.BoxGeometry(0.5, 0.03, 0.03), rungMat);
      rung.position.set(0, -r * 0.45, 0);
      ladderGroup.add(rung);
    }
    for (let side = -1; side <= 1; side += 2) {
      const rail = new THREE.Mesh(new THREE.BoxGeometry(0.03, 3.6, 0.03), rungMat);
      rail.position.set(side * 0.25, -1.6, 0);
      ladderGroup.add(rail);
    }
    ladderGroup.position.set(0, 0, 2.65);
    ladderGroup.rotation.x = 0.15;
    descent.add(ladderGroup);

    // Position ascent on top
    ascent.position.y = 2.0;
    this.lmGroup.add(ascent);

    // Place in scene
    this.lmGroup.position.copy(this.lm.pos);
    this.lmGroup.quaternion.copy(this.lm.quat);
    this.scene.add(this.lmGroup);
  }

  // ── Particle Systems ────────────────────────────────────────

  private buildParticleSystems(): void {
    this.exhaustSys = createParticlePool(this.scene, EXHAUST_COUNT, 0.7, 0xaaccff, THREE.AdditiveBlending);
    this.dustSys = createParticlePool(this.scene, DUST_COUNT, 2.0, 0x887766, THREE.NormalBlending);
    (this.dustSys.material as THREE.PointsMaterial).opacity = 0.4;

    this.rcsSystemsByName = {
      pitch: createParticlePool(this.scene, RCS_PART_COUNT, 0.35, 0xeeeeff, THREE.AdditiveBlending),
      roll: createParticlePool(this.scene, RCS_PART_COUNT, 0.35, 0xeeeeff, THREE.AdditiveBlending),
      yaw: createParticlePool(this.scene, RCS_PART_COUNT, 0.35, 0xeeeeff, THREE.AdditiveBlending),
      trans: createParticlePool(this.scene, RCS_PART_COUNT, 0.35, 0xeeeeff, THREE.AdditiveBlending),
    };
  }

  private emitEngineExhaust(): void {
    if (this.lm.throttle <= 0.001 || this.lm.fuel <= 0) return;

    const numPerFrame = Math.ceil(this.lm.throttle * 5);
    for (let i = 0; i < numPerFrame; i++) {
      this._tmpEmitPos.copy(this.engineBellLocalPos);
      this.lmGroup.localToWorld(this._tmpEmitPos);

      const spread = 0.4 + (1 - this.lm.throttle) * 0.3;
      this._tmpEmitVel
        .set((Math.random() - 0.5) * spread, -1, (Math.random() - 0.5) * spread)
        .normalize();
      this._tmpEmitVel.applyQuaternion(this.lm.quat);
      const speed = 25 + Math.random() * 35;
      this._tmpEmitVel.multiplyScalar(speed);
      this._tmpEmitVel.add(this.lm.vel);

      emitParticle(this.exhaustSys, this._tmpEmitPos, this._tmpEmitVel, 0.15 + Math.random() * 0.25);
    }
  }

  private emitDustKickup(): void {
    if (this.lm.altitude > DUST_START_ALT || this.lm.throttle <= 0.001 || this.lm.fuel <= 0) return;

    const intensity = this.lm.throttle * Math.pow(1 - this.lm.altitude / DUST_START_ALT, 1.5);
    const count = Math.ceil(intensity * 8);
    const groundY = this.lm.pos.y - this.lm.altitude;

    for (let i = 0; i < count; i++) {
      const angle = Math.random() * Math.PI * 2;
      const r = 0.5 + Math.random() * 4;
      this._tmpEmitPos.set(
        this.lm.pos.x + Math.cos(angle) * r,
        groundY + 0.1 + Math.random() * 0.3,
        this.lm.pos.z + Math.sin(angle) * r,
      );

      const speed = 4 + Math.random() * 18 * intensity;
      this._tmpEmitVel.set(
        Math.cos(angle) * speed + (Math.random() - 0.5) * 2,
        1.5 + Math.random() * 6 * intensity,
        Math.sin(angle) * speed + (Math.random() - 0.5) * 2,
      );

      emitParticle(this.dustSys, this._tmpEmitPos, this._tmpEmitVel, 0.8 + Math.random() * 2.5);
    }
  }

  private emitRCSJets(sysName: string, quadIndex: number, direction: THREE.Vector3): void {
    const sys = this.rcsSystemsByName[sysName];
    if (!sys) return;

    const quadLocal = this.rcsQuadPositions[quadIndex];
    if (!quadLocal) return;

    this._tmpEmitPos.set(quadLocal.x, quadLocal.y + 2.0, quadLocal.z);
    this.lmGroup.localToWorld(this._tmpEmitPos);

    for (let i = 0; i < 2; i++) {
      this._tmpEmitVel
        .set(
          direction.x + (Math.random() - 0.5) * 1.5,
          direction.y + (Math.random() - 0.5) * 1.5,
          direction.z + (Math.random() - 0.5) * 1.5,
        )
        .normalize()
        .multiplyScalar(4 + Math.random() * 6);
      this._tmpEmitVel.applyQuaternion(this.lm.quat);

      emitParticle(sys, this._tmpEmitPos, this._tmpEmitVel, 0.08 + Math.random() * 0.12);
    }
  }

  private tickParticles(dt: number): void {
    const systems = [this.exhaustSys, this.dustSys, ...Object.values(this.rcsSystemsByName)];
    for (const sys of systems) {
      if (!sys) continue;
      const ud = sys.userData as ParticleUserData;
      const pos = (sys.geometry.attributes.position as THREE.BufferAttribute).array as Float32Array;
      const vel = ud.velocities;

      for (let i = 0; i < ud.count; i++) {
        if (!ud.active[i]) continue;
        ud.lifetimes[i] -= dt;
        if (ud.lifetimes[i] <= 0) {
          ud.active[i] = 0;
          pos[i * 3 + 1] = -99999;
          continue;
        }

        pos[i * 3] += vel[i * 3] * dt;
        pos[i * 3 + 1] += vel[i * 3 + 1] * dt;
        pos[i * 3 + 2] += vel[i * 3 + 2] * dt;

        // Gravity on dust
        if (sys === this.dustSys) {
          vel[i * 3 + 1] -= MOON_GRAVITY * 0.4 * dt;
          if (pos[i * 3 + 1] < this.landingZoneCenter.y) {
            vel[i * 3 + 1] = 0;
            vel[i * 3] *= 0.95;
            vel[i * 3 + 2] *= 0.95;
          }
        }
      }

      (sys.geometry.attributes.position as THREE.BufferAttribute).needsUpdate = true;
    }
  }

  private clearAllParticles(): void {
    const systems = [this.exhaustSys, this.dustSys, ...Object.values(this.rcsSystemsByName)];
    for (const sys of systems) {
      if (!sys) continue;
      const ud = sys.userData as ParticleUserData;
      const pos = (sys.geometry.attributes.position as THREE.BufferAttribute).array as Float32Array;
      for (let i = 0; i < ud.count; i++) {
        ud.active[i] = 0;
        ud.lifetimes[i] = 0;
        pos[i * 3 + 1] = -99999;
      }
      (sys.geometry.attributes.position as THREE.BufferAttribute).needsUpdate = true;
    }
  }

  // ── Alien Mothership Easter Egg ──────────────────────────────

  private buildMothership(): THREE.Group {
    const group = new THREE.Group();

    // Hull material — dark metallic with blue emissive edges
    const hullMat = new THREE.MeshStandardMaterial({
      color: 0x1a1a2a,
      metalness: 0.9,
      roughness: 0.2,
      emissive: 0x0a0a1a,
      emissiveIntensity: 0.15,
    });

    // Main hull — elongated disc via LatheGeometry with custom profile
    // Profile: wide center tapering to thin edges, ~200m wide (radius 100), ~40m tall
    const hullProfile: THREE.Vector2[] = [];
    const profileSteps = 24;
    for (let i = 0; i <= profileSteps; i++) {
      const t = i / profileSteps; // 0 = center axis, 1 = outer edge
      const r = t * 100; // radius up to 100m (200m diameter)
      // Height profile: thick at center, tapering to thin at edges
      const thickness = 20 * Math.pow(1 - t * t, 0.6); // max 20m half-height at center
      if (i <= profileSteps / 2) {
        // Top half of profile (going outward along top surface)
        const angle = t;
        hullProfile.push(new THREE.Vector2(r, thickness * (1 - angle * 0.3)));
      }
    }
    // Complete the profile by going back along the bottom
    for (let i = profileSteps / 2; i >= 0; i--) {
      const t = (i / profileSteps) * 2;
      const r = t * 100;
      const thickness = 20 * Math.pow(1 - t * t, 0.6);
      hullProfile.push(new THREE.Vector2(r, -thickness * (1 - t * 0.3)));
    }

    const hullGeo = new THREE.LatheGeometry(hullProfile, 48);
    const hull = new THREE.Mesh(hullGeo, hullMat);
    hull.castShadow = true;
    group.add(hull);

    // Surface detail — concentric ring grooves (torus rings embedded in hull)
    const ringMat = new THREE.MeshStandardMaterial({
      color: 0x222238,
      metalness: 0.95,
      roughness: 0.15,
      emissive: 0x0808ff,
      emissiveIntensity: 0.1,
    });
    const ringRadii = [25, 45, 65, 82];
    for (const rr of ringRadii) {
      const torusGeo = new THREE.TorusGeometry(rr, 0.8, 8, 64);
      const torus = new THREE.Mesh(torusGeo, ringMat);
      torus.rotation.x = Math.PI / 2;
      torus.position.y = 2; // slightly above center
      group.add(torus);
    }

    // Edge glow rings — blue emissive
    const edgeGlowMat = new THREE.MeshStandardMaterial({
      color: 0x0022aa,
      metalness: 0.5,
      roughness: 0.3,
      emissive: 0x1144ff,
      emissiveIntensity: 0.8,
      transparent: true,
      opacity: 0.7,
    });
    const edgeRing1 = new THREE.Mesh(new THREE.TorusGeometry(98, 1.2, 8, 64), edgeGlowMat);
    edgeRing1.rotation.x = Math.PI / 2;
    group.add(edgeRing1);
    const edgeRing2 = new THREE.Mesh(new THREE.TorusGeometry(95, 0.6, 8, 64), edgeGlowMat);
    edgeRing2.rotation.x = Math.PI / 2;
    edgeRing2.position.y = -3;
    group.add(edgeRing2);

    // Underside — grid of small glowing lights
    this.mothershipUndersideLights = [];
    const lightMat = new THREE.MeshBasicMaterial({
      color: 0x88bbff,
      transparent: true,
      opacity: 0.9,
    });
    const lightGeo = new THREE.SphereGeometry(0.8, 6, 6);
    // Create a grid pattern on the underside
    for (let gx = -6; gx <= 6; gx++) {
      for (let gz = -6; gz <= 6; gz++) {
        const dist = Math.sqrt(gx * gx + gz * gz);
        if (dist > 6.5) continue; // circular pattern
        const lightSphere = new THREE.Mesh(lightGeo, lightMat.clone());
        lightSphere.position.set(gx * 10, -15, gz * 10);
        // Store distance from center for wave animation
        lightSphere.userData.distFromCenter = dist;
        group.add(lightSphere);
        this.mothershipUndersideLights.push(lightSphere);
      }
    }

    // Central core — bright emissive sphere underneath (tractor beam look)
    const coreMat = new THREE.MeshBasicMaterial({
      color: 0x44aaff,
      transparent: true,
      opacity: 0.85,
    });
    const core = new THREE.Mesh(new THREE.SphereGeometry(8, 24, 24), coreMat);
    core.position.y = -18;
    group.add(core);

    // Tractor beam cone
    const beamMat = new THREE.MeshBasicMaterial({
      color: 0x2266ff,
      transparent: true,
      opacity: 0.08,
      side: THREE.DoubleSide,
      depthWrite: false,
    });
    const beamGeo = new THREE.ConeGeometry(40, 120, 24, 1, true);
    const beam = new THREE.Mesh(beamGeo, beamMat);
    beam.position.y = -78;
    group.add(beam);

    // Blue PointLight underneath for terrain illumination
    this.mothershipLight = new THREE.PointLight(0x2244ff, 0.4, 600, 1.5);
    this.mothershipLight.position.y = -20;
    group.add(this.mothershipLight);

    // Hide by default
    group.visible = false;
    this.scene.add(group);
    return group;
  }

  private initMothershipAudio(): void {
    if (!this.audioCtx) return;
    const ctx = this.audioCtx;

    // Deep sub-bass rumble — barely audible
    this.mothershipOsc = ctx.createOscillator();
    this.mothershipOsc.type = 'sine';
    this.mothershipOsc.frequency.value = 28;

    const filter = ctx.createBiquadFilter();
    filter.type = 'lowpass';
    filter.frequency.value = 60;
    filter.Q.value = 2;

    this.mothershipGain = ctx.createGain();
    this.mothershipGain.gain.value = 0;

    this.mothershipOsc.connect(filter);
    filter.connect(this.mothershipGain);
    this.mothershipGain.connect(ctx.destination);
    this.mothershipOsc.start();

    // Secondary oscillator for eerie harmonic
    this.mothershipOsc2 = ctx.createOscillator();
    this.mothershipOsc2.type = 'sine';
    this.mothershipOsc2.frequency.value = 42;

    this.mothershipGain2 = ctx.createGain();
    this.mothershipGain2.gain.value = 0;

    const filter2 = ctx.createBiquadFilter();
    filter2.type = 'lowpass';
    filter2.frequency.value = 80;

    this.mothershipOsc2.connect(filter2);
    filter2.connect(this.mothershipGain2);
    this.mothershipGain2.connect(ctx.destination);
    this.mothershipOsc2.start();
  }

  private updateMothership(dt: number): void {
    // Trigger check: 30% chance, once per landing attempt, when altitude < 200
    if (!this.mothershipTriggered && this.gameState === 'flying' && this.lm.altitude < 200) {
      this.mothershipTriggered = true;
      this.mothershipShouldAppear = Math.random() < 0.3;
      if (this.mothershipShouldAppear) {
        this.mothershipActive = true;
        this.mothershipProgress = 0;
        this.mothershipAltitude = 300 + Math.random() * 200; // 300-500m
        // Enter from one side, exit the other (~1200m travel across)
        const direction = Math.random() > 0.5 ? 1 : -1;
        this.mothershipStartX = -600 * direction;
        this.mothershipEndX = 600 * direction;
        this.mothershipCalloutSpoken = false;

        if (!this.mothership) {
          this.mothership = this.buildMothership();
        }
        this.mothership.visible = true;

        // Init audio if needed
        if (!this.mothershipOsc) {
          this.initMothershipAudio();
        }
      }
    }

    if (!this.mothershipActive || !this.mothership) return;

    // Progress: 0 to 1 over ~40 seconds
    this.mothershipProgress += dt / 40;

    if (this.mothershipProgress >= 1) {
      // Done — hide the mothership
      this.mothershipActive = false;
      this.mothership.visible = false;
      if (this.mothershipGain) this.mothershipGain.gain.value = 0;
      if (this.mothershipGain2) this.mothershipGain2.gain.value = 0;
      if (this.mothershipLight) this.mothershipLight.intensity = 0;
      return;
    }

    const p = this.mothershipProgress;

    // Smooth easing for entry/exit
    const easedP = p; // linear traverse is fine — it's huge and slow

    // Position: lerp from start to end
    const x = this.mothershipStartX + (this.mothershipEndX - this.mothershipStartX) * easedP;
    const z = Math.sin(p * Math.PI * 2) * 30; // slight lateral drift
    const groundY = this.sampleTerrainHeight(x, z);
    const y = groundY + this.mothershipAltitude;

    this.mothership.position.set(x, y, z);

    // Slight wobble/rotation
    this.mothership.rotation.y += dt * 0.08; // slow spin
    this.mothership.rotation.x = Math.sin(p * Math.PI * 4) * 0.015; // subtle wobble
    this.mothership.rotation.z = Math.cos(p * Math.PI * 3) * 0.01;

    // Underside lights pulse — wave propagating from center outward
    const time = performance.now() * 0.001;
    for (const light of this.mothershipUndersideLights) {
      const dist = light.userData.distFromCenter as number;
      const wave = Math.sin(time * 3 - dist * 0.8) * 0.5 + 0.5;
      const mat = light.material as THREE.MeshBasicMaterial;
      mat.opacity = 0.3 + wave * 0.7;
      // Color shift in the wave
      const r = 0.3 + wave * 0.2;
      const g = 0.6 + wave * 0.3;
      const b = 1.0;
      mat.color.setRGB(r, g, b);
    }

    // Terrain light intensity — brighter when overhead-ish
    if (this.mothershipLight) {
      const overhead = 1 - Math.abs(p - 0.5) * 2; // peaks at p=0.5
      this.mothershipLight.intensity = 0.15 + overhead * 0.5;
    }

    // Audio — fade in/out, very quiet
    if (this.mothershipGain) {
      const fadeIn = Math.min(1, p * 5);       // fade in over first 20%
      const fadeOut = Math.min(1, (1 - p) * 5); // fade out over last 20%
      const vol = fadeIn * fadeOut * 0.04; // very faint
      this.mothershipGain.gain.value += (vol - this.mothershipGain.gain.value) * 0.1;
    }
    if (this.mothershipGain2) {
      const fadeIn = Math.min(1, p * 5);
      const fadeOut = Math.min(1, (1 - p) * 5);
      const vol = fadeIn * fadeOut * 0.025;
      this.mothershipGain2.gain.value += (vol - this.mothershipGain2.gain.value) * 0.1;
    }

    // Ground control callout when mothership is ~30% through (overhead area)
    if (!this.mothershipCalloutSpoken && p > 0.3) {
      this.mothershipCalloutSpoken = true;
      this.speakCallout("Eagle, Houston... we're seeing something on radar we can't explain. Continue your descent.");
    }
  }

  // ── Physics ─────────────────────────────────────────────────

  private physicsStep(dt: number): void {
    if (this.gameState !== 'flying') return;

    const mass = DRY_MASS + this.lm.fuel;

    // Throttle ramp
    if (this.keys['ShiftLeft'] || this.keys['ShiftRight']) {
      this.lm.throttle = Math.min(1, this.lm.throttle + THROTTLE_RATE * dt);
    }
    if (this.keys['ControlLeft'] || this.keys['ControlRight']) {
      this.lm.throttle = Math.max(0, this.lm.throttle - THROTTLE_RATE * dt);
    }
    if (this.lm.fuel <= 0) this.lm.throttle = 0;

    // RCS rotation
    let rcsAnyRot = false;
    let rcsAnyTrans = false;

    if (!this.keys['Space']) {
      const rcsAngAccel = RCS_TORQUE / mass;
      if (this.keys['ArrowUp']) { this.lm.angVel.x -= rcsAngAccel * dt; rcsAnyRot = true; }
      if (this.keys['ArrowDown']) { this.lm.angVel.x += rcsAngAccel * dt; rcsAnyRot = true; }
      if (this.keys['ArrowLeft']) { this.lm.angVel.z += rcsAngAccel * dt; rcsAnyRot = true; }
      if (this.keys['ArrowRight']) { this.lm.angVel.z -= rcsAngAccel * dt; rcsAnyRot = true; }
    }
    if (this.keys['KeyQ']) { this.lm.angVel.y += RCS_TORQUE / mass * dt; rcsAnyRot = true; }
    if (this.keys['KeyE']) { this.lm.angVel.y -= RCS_TORQUE / mass * dt; rcsAnyRot = true; }

    // RCS translation
    if (this.keys['Space']) {
      this._rcsTransVec.set(0, 0, 0);
      if (this.keys['ArrowUp']) this._rcsTransVec.z = -1;
      if (this.keys['ArrowDown']) this._rcsTransVec.z = 1;
      if (this.keys['ArrowLeft']) this._rcsTransVec.x = -1;
      if (this.keys['ArrowRight']) this._rcsTransVec.x = 1;

      if (this._rcsTransVec.lengthSq() > 0) {
        this._rcsTransVec.normalize().multiplyScalar((RCS_FORCE / mass) * dt);
        this._rcsTransVec.applyQuaternion(this.lm.quat);
        this.lm.vel.add(this._rcsTransVec);
        rcsAnyTrans = true;
      }
    }

    // Angular damping
    const dampFactor = 1 - 2 * dt;
    this.lm.angVel.multiplyScalar(Math.max(0, dampFactor));

    // Integrate rotation
    const halfDt = dt * 0.5;
    const dq = new THREE.Quaternion(
      this.lm.angVel.x * halfDt,
      this.lm.angVel.y * halfDt,
      this.lm.angVel.z * halfDt,
      1,
    );
    dq.normalize();
    this.lm.quat.multiply(dq);
    this.lm.quat.normalize();

    // Main engine thrust
    if (this.lm.throttle > 0.001 && this.lm.fuel > 0) {
      const thrustMag = MAX_THRUST * this.lm.throttle;
      const fuelBurn = (thrustMag / (ISP * G0)) * dt;
      this.lm.fuel = Math.max(0, this.lm.fuel - fuelBurn);

      this._thrustVec.set(0, 1, 0);
      this._thrustVec.applyQuaternion(this.lm.quat);
      this._thrustVec.multiplyScalar((thrustMag / mass) * dt);
      this.lm.vel.add(this._thrustVec);
    }

    // Gravity
    this.lm.vel.y -= MOON_GRAVITY * dt;

    // Semi-implicit Euler position integration
    this.lm.pos.x += this.lm.vel.x * dt;
    this.lm.pos.y += this.lm.vel.y * dt;
    this.lm.pos.z += this.lm.vel.z * dt;

    // Altitude measurement
    this.altRay.set(new THREE.Vector3(this.lm.pos.x, this.lm.pos.y + 100, this.lm.pos.z), this.downVec);
    this.altRay.far = 1000;
    const hits = this.altRay.intersectObject(this.terrain, false);
    if (hits.length > 0) {
      this.lm.altitude = this.lm.pos.y - hits[0].point.y;
    } else {
      this.lm.altitude = this.lm.pos.y;
    }

    // Contact light
    if (this.lm.altitude < CONTACT_ALT && !this.lm.contactLight) {
      this.lm.contactLight = true;
    }

    // Landing detection
    if (this.lm.altitude <= LANDING_DETECT) {
      this.evaluateLanding();
    }

    // Prevent falling through terrain
    if (this.lm.altitude < 0) {
      this.lm.pos.y -= this.lm.altitude;
      this.lm.altitude = 0;
    }

    // Particle emission
    this.emitEngineExhaust();
    this.emitDustKickup();

    // RCS jet particles
    if (rcsAnyRot) {
      if (this.keys['ArrowUp'] || this.keys['ArrowDown']) {
        this.emitRCSJets('pitch', 0, new THREE.Vector3(0, 0, this.keys['ArrowUp'] ? 1 : -1));
        this.emitRCSJets('pitch', 2, new THREE.Vector3(0, 0, this.keys['ArrowUp'] ? -1 : 1));
      }
      if (this.keys['ArrowLeft'] || this.keys['ArrowRight']) {
        this.emitRCSJets('roll', 1, new THREE.Vector3(0, this.keys['ArrowLeft'] ? 1 : -1, 0));
        this.emitRCSJets('roll', 3, new THREE.Vector3(0, this.keys['ArrowLeft'] ? -1 : 1, 0));
      }
      if (this.keys['KeyQ'] || this.keys['KeyE']) {
        this.emitRCSJets('yaw', 0, new THREE.Vector3(this.keys['KeyQ'] ? -1 : 1, 0, 0));
        this.emitRCSJets('yaw', 2, new THREE.Vector3(this.keys['KeyQ'] ? 1 : -1, 0, 0));
      }
    }
    if (rcsAnyTrans) {
      this.emitRCSJets('trans', 0, this._rcsTransVec.clone().negate());
      this.emitRCSJets('trans', 2, this._rcsTransVec.clone());
    }

    // Audio
    if (this.engineGain) {
      const targetGain = this.lm.throttle * 0.18;
      this.engineGain.gain.value += (targetGain - this.engineGain.gain.value) * 0.1;
      if (this.engineOsc) this.engineOsc.frequency.value = 55 + this.lm.throttle * 70;
    }
    if (this.rcsGain) {
      const rcsTarget = (rcsAnyRot || rcsAnyTrans) ? 0.05 : 0;
      this.rcsGain.gain.value += (rcsTarget - this.rcsGain.gain.value) * 0.15;
    }

    // Ground Control callouts
    this.checkCallouts();

    // Cinematic camera (overrides normal camera when active)
    this.updateCinematic(dt);

    // Alien mothership Easter egg
    this.updateMothership(dt);
  }

  // ── Landing Evaluation ──────────────────────────────────────

  private evaluateLanding(): void {
    const lmUp = new THREE.Vector3(0, 1, 0).applyQuaternion(this.lm.quat);
    const tiltDeg =
      Math.acos(Math.min(1, Math.max(-1, lmUp.dot(new THREE.Vector3(0, 1, 0))))) * (180 / Math.PI);

    const vSpeed = Math.abs(this.lm.vel.y);
    const hSpeed = Math.sqrt(this.lm.vel.x ** 2 + this.lm.vel.z ** 2);

    const flightSec = (performance.now() - this.lm.startTime) / 1000;
    const distFromLZ = Math.sqrt(this.lm.pos.x ** 2 + this.lm.pos.z ** 2);
    const fuelPct = (this.lm.fuel / FUEL_MASS_INIT) * 100;

    const stats: LanderStats = {
      vSpeed: vSpeed.toFixed(2),
      hSpeed: hSpeed.toFixed(2),
      tilt: tiltDeg.toFixed(1),
      fuel: fuelPct.toFixed(1),
      time: flightSec.toFixed(1),
      distance: distFromLZ.toFixed(1),
    };

    const failures: string[] = [];
    if (vSpeed > SAFE_V_SPEED) failures.push(`VERTICAL SPEED ${vSpeed.toFixed(1)} M/S EXCEEDS ${SAFE_V_SPEED} M/S LIMIT`);
    if (hSpeed > SAFE_H_SPEED) failures.push(`HORIZONTAL SPEED ${hSpeed.toFixed(1)} M/S EXCEEDS ${SAFE_H_SPEED} M/S LIMIT`);
    if (tiltDeg > SAFE_TILT_DEG) failures.push(`TILT ANGLE ${tiltDeg.toFixed(1)} DEG EXCEEDS ${SAFE_TILT_DEG} DEG LIMIT`);

    const success = failures.length === 0;

    // Freeze state
    this.gameState = success ? 'landed' : 'crashed';
    this.lm.vel.set(0, 0, 0);
    this.lm.angVel.set(0, 0, 0);
    this.lm.throttle = 0;

    // Snap to surface
    const groundY = this.lm.pos.y - this.lm.altitude;
    this.lm.pos.y = groundY + 0.1;
    this.lm.altitude = 0.1;

    // Audio + voice callout
    const impactIntensity = Math.min(1, vSpeed / 5);
    this.playLandingThud(impactIntensity);
    if (this.engineGain) this.engineGain.gain.value = 0;
    if (this.rcsGain) this.rcsGain.gain.value = 0;
    this.speakLandingResult(success);

    // GTA V style post-landing cinematic orbit
    this.startPostLandCinematic();

    // Fire callbacks
    for (const cb of this.landedCallbacks) {
      try { cb(success, stats); } catch { /* ignore */ }
    }
  }

  // ── Camera System ───────────────────────────────────────────

  private updateCamera(dt: number): void {
    const t = 1 - Math.pow(0.005, dt);
    const mode = CAMERA_MODES[this.cameraMode];

    if (!this.camInitialized || this.prevCameraMode !== this.cameraMode) {
      this.camInitialized = true;
      this.prevCameraMode = this.cameraMode;
      if (mode !== 'ORBIT') {
        this.camSmoothPos.copy(this.camera.position);
        this.camSmoothTarget.copy(this.lm.pos);
      }
    }

    switch (mode) {
      case 'ORBIT': {
        this.orbitControls.enabled = true;
        this.orbitControls.target.lerp(this.lm.pos, t * 0.5);
        this.orbitControls.update();
        break;
      }
      case 'CHASE': {
        this.orbitControls.enabled = false;
        this._camDesiredPos.set(0, 10, 25);
        this._camDesiredPos.applyQuaternion(this.lm.quat);
        this._camDesiredPos.add(this.lm.pos);

        this._camDesiredTarget.copy(this.lm.pos);

        this.camSmoothPos.lerp(this._camDesiredPos, t);
        this.camSmoothTarget.lerp(this._camDesiredTarget, t);

        this.camera.position.copy(this.camSmoothPos);
        this.camera.lookAt(this.camSmoothTarget);
        break;
      }
      case 'COCKPIT': {
        this.orbitControls.enabled = false;
        this._camDesiredPos.set(0, 2.5, 1.6);
        this._camDesiredPos.applyQuaternion(this.lm.quat);
        this._camDesiredPos.add(this.lm.pos);

        this._camDesiredTarget.set(0, -8, 30);
        this._camDesiredTarget.applyQuaternion(this.lm.quat);
        this._camDesiredTarget.add(this.lm.pos);

        this.camera.position.copy(this._camDesiredPos);
        this.camera.lookAt(this._camDesiredTarget);
        break;
      }
      case 'SURFACE': {
        this.orbitControls.enabled = false;
        const surfY = this.landingZoneCenter.y + 2;
        this._camDesiredPos.set(8, surfY, 45);
        this.camSmoothPos.lerp(this._camDesiredPos, t * 0.3);
        this.camera.position.copy(this.camSmoothPos);
        this.camera.lookAt(this.lm.pos);
        break;
      }
    }
  }

  // ── Shadow Camera Tracking ──────────────────────────────────

  private updateShadowCamera(): void {
    if (!this.shadowLight) return;
    this.shadowLight.target.position.copy(this.lm.pos);
    this.shadowLight.target.updateMatrixWorld();
    this.shadowLight.position.set(
      this.lm.pos.x + 300,
      this.lm.pos.y + 180,
      this.lm.pos.z - 400,
    );
  }

  // ── Input Handling ──────────────────────────────────────────

  private onKeyDown(e: KeyboardEvent): void {
    this.keys[e.code] = true;

    if (e.code === 'KeyC' && this.gameState === 'flying') {
      this.cinematicActive = false; // exit cinematic on camera cycle
      this.cameraMode = (this.cameraMode + 1) % CAMERA_MODES.length;
    }

    if (e.code === 'KeyV') {
      this.toggleCinematic();
    }

    if (e.code === 'KeyR') {
      this.restart();
    }

    if (
      [
        'ArrowUp', 'ArrowDown', 'ArrowLeft', 'ArrowRight',
        'Space', 'ShiftLeft', 'ShiftRight', 'ControlLeft', 'ControlRight',
        'KeyQ', 'KeyE',
      ].includes(e.code)
    ) {
      e.preventDefault();
    }
  }

  private onKeyUp(e: KeyboardEvent): void {
    this.keys[e.code] = false;
  }

  private onBlur(): void {
    for (const k in this.keys) this.keys[k] = false;
  }

  // ── Animation Loop ──────────────────────────────────────────

  private animate = (): void => {
    if (!this.running || this.disposed) return;
    this.animFrameId = requestAnimationFrame(this.animate);

    const rawDt = this.clock.getDelta();
    const dt = Math.min(rawDt, 0.1);

    if (this.gameState === 'flying') {
      this.physicsAccum += dt;
      let steps = 0;
      while (this.physicsAccum >= PHYSICS_DT && steps < 10) {
        this.physicsStep(PHYSICS_DT);
        this.physicsAccum -= PHYSICS_DT;
        steps++;
      }

      this.lmGroup.position.copy(this.lm.pos);
      this.lmGroup.quaternion.copy(this.lm.quat);
    } else if (this.gameState === 'landed' || this.gameState === 'crashed') {
      this.lmGroup.position.copy(this.lm.pos);
      this.lmGroup.quaternion.copy(this.lm.quat);
    }

    this.tickParticles(dt);
    this.updateCamera(dt);
    this.updateShadowCamera();

    this.renderer.render(this.scene, this.camera);
  };
}
