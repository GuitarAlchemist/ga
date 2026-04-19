/**
 * Ocean Component — Adaptive Quality TSL + WebGPU
 *
 * Tessendorf FFT ocean simulation with adaptive quality, bloom post-processing,
 * underwater camera support, and visual preset switching.
 */

import React, { useEffect, useRef, useCallback, useState } from 'react';
import * as THREE from 'three';
import { WebGPURenderer, PostProcessing } from 'three/webgpu';
import { pass } from 'three/tsl';
import { bloom } from 'three/examples/jsm/tsl/display/BloomNode.js';
import { OrbitControls } from 'three/examples/jsm/controls/OrbitControls.js';
import { GLTFLoader } from 'three/examples/jsm/loaders/GLTFLoader.js';
import { DRACOLoader } from 'three/examples/jsm/loaders/DRACOLoader.js';
import { createOceanTSLMaterial } from './OceanTSL';
import { createOceanFFTMaterial } from './OceanTSLFFT';
import { OceanFFTCompute } from './OceanFFTCompute';
import { detectQualityTier } from './OceanQuality';
import {
  OCEAN_PRESETS, buildSkyTexture, sunDirectionFromPreset,
  MOON_TEX_URL, MOON_DISP_URL, type OceanPresetName,
} from './OceanPresets';

const CAMERA_START = new THREE.Vector3(0, 4.5, 25);
// Target is raised above the water plane so OrbitControls' "look-at-target"
// geometry naturally tilts the default view upward — letting the user drag
// to see the sky/moon. With target at water level (y=0), the camera (which
// must stay above water) could only ever look DOWN at the target, blocking
// any upward look.
const CAMERA_TARGET = new THREE.Vector3(0, 60, -120);
const FFT_PATCH_SIZE = 250;
// Hard floor for camera height — prevents going underwater. Set above the
// peak of the longest Gerstner wave (amp 2.0) plus a small clearance buffer.
const MIN_CAMERA_Y = 2.5;

// Drop a GLB at Apps/ga-client/public/models/warship.glb (e.g. the Imperial
// Warship from sketchfab.com — free, requires login + CC-BY attribution).
// Base-relative so the component works under '/' or under the '/ga/'
// gh-pages base path.
const SHIP_MODEL_URL = `${import.meta.env.BASE_URL}models/warship.glb`;
// Star-destroyer style: park the ship next to the moon in the sky so it's
// trivially findable. Position is recomputed each frame relative to camera
// (same pattern as the moon) so it stays locked at infinity, no parallax.
// NOTE: in Three.js spherical coords, HIGHER azimuth = camera-LEFT. So to
// place the ship to the right of the moon (az 200°), use a smaller azimuth.
const SHIP_AZ_DEG = 194;  // moon is at 200° → ship 6° to the right of moon
const SHIP_EL_DEG = 22;   // moon is at 26° → ship 4° below moon
// 750m keeps the ship close for sharp z-buffer precision AND makes it
// dominate the sky (angular size ~24°, ~5× the moon).
const SHIP_DIST = 750;
const SHIP_HEADING = -Math.PI / 2 + 0.3;
// Angular size at SHIP_DIST: 320m / 750m ≈ 24° — takes up a serious
// chunk of the sky.
const SHIP_TARGET_LONGEST_M = 320;
// The Sketchfab Imperial Warship's longest bbox axis (Y=87.8) is its
// nose-to-tail length, not the ring diameter. Leaving pitch at 0 keeps
// the model vertical with the tail pointing at the ground (world -Y).
// Nested inside shipRoot so yaw drift and spin stack cleanly.
const SHIP_MODEL_PITCH = 0;
// Parallax strength: 0 = fully locked to camera (no parallax, appears
// at infinity), 1 = fully fixed in world space (full parallax). Small
// value gives a subtle depth cue — the ship drifts against the moon
// slightly as the camera orbits, selling it as a closer object than
// the starfield without letting it wander off the sky.
const SHIP_PARALLAX = 0.08;
// Independent spin tracks for each concentric "ring" of the warship.
// Meshes are bucketed by max XZ radius (as a fraction of the ship's
// overall XZ half-extent) into these radius bands, each band rotating
// at a different rad/s. Alternating signs give the hyperspace-windup
// feel while every piece stays in motion — fixes "some pieces stay
// still" by ensuring no bucket has speed 0.
//
// Bucket bands (top→bottom): outer hull / outer ring / inner ring / hub
// Outer hull drifts gently; inner hub whips at ~3.5s/rev.
const SHIP_SPIN_BUCKET_MIN_RADII: readonly number[] = [0.70, 0.40, 0.15];
const SHIP_SPIN_SPEEDS: readonly number[] = [0.05, 0.55, -1.10, 1.80];

export interface OceanProps {
  width?: number;
  height?: number;
}

// ── Preset panel component ──────────────────────────────────────────────────

const PresetPanel: React.FC<{
  current: OceanPresetName;
  onSelect: (name: OceanPresetName) => void;
}> = ({ current, onSelect }) => (
  <div style={{
    position: 'absolute',
    top: 12,
    left: 12,
    right: 12,               // allow wrapping into available width on phones
    display: 'flex',
    flexWrap: 'wrap',        // stack buttons onto a second row when cramped
    gap: 6,
    zIndex: 100,
    pointerEvents: 'none',    // let clicks fall through gaps to OrbitControls
  }}>
    {(Object.keys(OCEAN_PRESETS) as OceanPresetName[]).map(name => {
      const isActive = name === current;
      return (
        <button
          key={name}
          onClick={() => onSelect(name)}
          style={{
            // 10×16 padding + 13px font ≈ 40px tall — comfortably clears
            // the 44px touch-target guideline with border included.
            padding: '10px 16px',
            fontFamily: 'monospace',
            fontSize: '13px',
            fontWeight: 600,
            background: isActive ? 'rgba(0,229,255,0.25)' : 'rgba(0,0,0,0.55)',
            color: isActive ? '#00e5ff' : '#a0c0d0',
            border: `1px solid ${isActive ? '#00e5ff' : 'rgba(255,255,255,0.15)'}`,
            borderRadius: 6,
            cursor: 'pointer',
            textTransform: 'uppercase',
            letterSpacing: '0.5px',
            pointerEvents: 'auto',   // buttons themselves still capture taps
            touchAction: 'manipulation',
          }}
        >
          {OCEAN_PRESETS[name].label}
        </button>
      );
    })}
  </div>
);

// ── Main component ──────────────────────────────────────────────────────────

export const Ocean: React.FC<OceanProps> = ({ width, height }) => {
  const containerRef = useRef<HTMLDivElement>(null);
  const cleanupRef = useRef<(() => void) | null>(null);
  const fpsRef = useRef<HTMLDivElement>(null);
  const applyPresetRef = useRef<((name: OceanPresetName) => void) | null>(null);
  const [currentPreset, setCurrentPreset] = useState<OceanPresetName>('stormy');

  const updateHud = useCallback((fps: number, mode: string) => {
    if (fpsRef.current) fpsRef.current.textContent = `${fps} FPS · ${mode}`;
  }, []);

  const handlePresetSelect = useCallback((name: OceanPresetName) => {
    setCurrentPreset(name);
    applyPresetRef.current?.(name);
  }, []);

  useEffect(() => {
    if (!containerRef.current) return;
    let disposed = false;
    let animationFrameId: number | null = null;

    const init = async () => {
      const container = containerRef.current;
      if (!container || disposed) return;

      const w = width || container.clientWidth;
      const h = height || container.clientHeight;

      const quality = await detectQualityTier();
      console.log(`[Ocean] Quality: ${quality.tier}, mesh ${quality.meshRes}², bloom: ${quality.enableBloom}`);

      // ── Scene ──
      const scene = new THREE.Scene();
      // Token disambiguates concurrent sky-load promises so a slow load for
      // an old preset can't clobber a freshly-selected one.
      let lastSkyToken: object;
      let currentBgTex: THREE.Texture;
      const swapWhenReady = (ready: Promise<THREE.Texture>, ownerToken: object) => {
        ready.then(real => {
          if (disposed) { real.dispose(); return; }
          if (lastSkyToken !== ownerToken) { real.dispose(); return; }
          // Swapping the texture object (not just .image) forces the renderer
          // to re-run its equirect→cube conversion.
          scene.background = real;
          currentBgTex.dispose();
          currentBgTex = real;
        }).catch(() => { /* fallback gradient stays in place */ });
      };
      const initialSky = buildSkyTexture(OCEAN_PRESETS.stormy);
      lastSkyToken = initialSky;
      currentBgTex = initialSky.texture;
      scene.background = currentBgTex;
      swapWhenReady(initialSky.ready, initialSky);

      // ── Camera ──
      // near=1.0 (was 0.5) — OrbitControls.minDistance is 1.5 so we never
      // need to see closer than 1m. Pushing near out halves the depth
      // range, which doubles z-buffer precision at every distance — the
      // last remaining source of Sketchfab warship flicker at ~1500m.
      const camera = new THREE.PerspectiveCamera(55, w / h, 1.0, 20000);
      camera.position.copy(CAMERA_START);

      // ── Renderer ──
      const renderer = new WebGPURenderer({ antialias: true, forceWebGL: false });
      await renderer.init();
      if (disposed) { renderer.dispose(); return; }
      renderer.setSize(w, h);
      renderer.setPixelRatio(Math.min(window.devicePixelRatio, quality.pixelRatio));
      renderer.toneMapping = THREE.ACESFilmicToneMapping;
      renderer.toneMappingExposure = OCEAN_PRESETS.stormy.exposure;
      container.appendChild(renderer.domElement);
      // Force canvas below UI overlays — otherwise it covers the preset buttons
      renderer.domElement.style.position = 'absolute';
      renderer.domElement.style.top = '0';
      renderer.domElement.style.left = '0';
      renderer.domElement.style.zIndex = '1';

      // ── Controls ──
      const controls = new OrbitControls(camera, renderer.domElement);
      controls.enableDamping = true;
      controls.dampingFactor = 0.05;
      controls.target.copy(CAMERA_TARGET);
      // Allow free up/down look — Y clamp prevents underwater regardless.
      controls.maxPolarAngle = Math.PI * 0.95;
      controls.minPolarAngle = 0;
      controls.minDistance = 1.5;
      controls.maxDistance = 3000;
      // Pin input bindings explicitly — some browser extensions and
      // overlay tools hijack shift+drag or the contextmenu event,
      // silently breaking OrbitControls' defaults.
      controls.enablePan = true;
      controls.panSpeed = 1.0;
      controls.mouseButtons = {
        LEFT: THREE.MOUSE.ROTATE,
        MIDDLE: THREE.MOUSE.DOLLY,
        RIGHT: THREE.MOUSE.PAN,
      };
      controls.touches = { ONE: THREE.TOUCH.ROTATE, TWO: THREE.TOUCH.DOLLY_PAN };
      // Block the browser context menu on the canvas so right-drag pan
      // isn't eaten on first click.
      renderer.domElement.addEventListener('contextmenu', e => e.preventDefault());

      // ── Sun + lights ──
      const sunDir = sunDirectionFromPreset(OCEAN_PRESETS.stormy);
      const sunLight = new THREE.DirectionalLight(0xfff8f0, 3.0);
      sunLight.position.copy(sunDir.clone().multiplyScalar(2000));
      scene.add(sunLight);
      scene.add(new THREE.AmbientLight(0x304060, 0.1));

      // ── Moon (visible only at night) ─────────────────────────────────────
      const moonLoader = new THREE.TextureLoader();
      const moonMap = moonLoader.load(
        MOON_TEX_URL,
        t => { t.colorSpace = THREE.SRGBColorSpace; t.anisotropy = 8; console.info('[Ocean] Moon texture loaded'); },
        undefined,
        () => console.warn(`[Ocean] Failed to load moon texture at ${MOON_TEX_URL}`),
      );
      const moonBump = moonLoader.load(MOON_DISP_URL);
      const moonMat = new THREE.MeshPhongMaterial({
        map: moonMap,
        bumpMap: moonBump,
        bumpScale: 0.18,
        color: new THREE.Color(0xe8ecf4),       // bright fallback if map 404s
        emissive: new THREE.Color(0x303848),
        emissiveIntensity: 0.7,
        shininess: 2,
        fog: false,
      });
      // Sphere radius scaled with MOON_DIST=4000 — keeps angular size similar
      // to before (radius/dist ratio ≈ 0.04 → ~2.3° angular diameter, ~5× the
      // real moon's 0.5° but reads well visually without dominating the sky).
      const moonMesh = new THREE.Mesh(new THREE.SphereGeometry(160, 64, 64), moonMat);
      moonMesh.visible = false;
      const moonLight = new THREE.DirectionalLight(0xffffff, 1.6);
      moonLight.visible = false;
      scene.add(moonMesh);
      scene.add(moonLight);

      // Dedicated fill-light for the warship. moonLight's target is locked
      // to the moon so it grazes the ship at a shallow angle; without this
      // second light the camera-facing side stays in shadow at night's
      // low exposure. Position updates each frame to follow the camera.
      const shipLight = new THREE.DirectionalLight(0xccd8f0, 2.2);
      shipLight.visible = false;
      scene.add(shipLight);

      // Direction from observer to the moon — recomputed when the preset
      // changes, then re-used every frame to keep the moon at infinity.
      const moonDir = new THREE.Vector3();
      const MOON_DIST = 4000;  // >> wave/scene scale; with per-frame relock,
                                // distance only sets the apparent angular size.

      const positionMoon = (presetName: OceanPresetName) => {
        const isNight = presetName === 'night';
        moonMesh.visible = isNight;
        moonLight.visible = isNight;
        if (!isNight) return;
        moonDir.copy(sunDirectionFromPreset(OCEAN_PRESETS[presetName])).normalize();
        moonLight.target = moonMesh;
        updateMoonPositionForCamera();
      };

      // Fixed sky direction for the ship, computed once
      const shipSkyDir = new THREE.Vector3().setFromSphericalCoords(
        1,
        Math.PI / 2 - THREE.MathUtils.degToRad(SHIP_EL_DEG),
        THREE.MathUtils.degToRad(SHIP_AZ_DEG),
      );

      // Lock moon (and ship) to fixed directions from the camera each frame
      // so they appear at infinity — no parallax, they stay put in the sky.
      const _moonOffset = new THREE.Vector3();
      const _shipOffset = new THREE.Vector3();
      const updateMoonPositionForCamera = () => {
        if (moonMesh.visible) {
          _moonOffset.copy(moonDir).multiplyScalar(MOON_DIST);
          moonMesh.position.copy(camera.position).add(_moonOffset);
          moonLight.position.copy(camera.position);
        }
        if (shipRoot && shipRoot.visible) {
          // Parallax: start from the fully-locked position (camera + dir*dist)
          // then subtract (camera - anchor) * parallax. At parallax=0 the ship
          // tracks the camera exactly (locked at infinity); at parallax=1 it
          // stays anchored at the initial camera pose (full parallax).
          _shipOffset.copy(shipSkyDir).multiplyScalar(SHIP_DIST);
          shipRoot.position.copy(camera.position).add(_shipOffset);
          shipRoot.position.x -= (camera.position.x - CAMERA_START.x) * SHIP_PARALLAX;
          shipRoot.position.y -= (camera.position.y - CAMERA_START.y) * SHIP_PARALLAX;
          shipRoot.position.z -= (camera.position.z - CAMERA_START.z) * SHIP_PARALLAX;
          shipLight.position.copy(camera.position);
        }
      };

      // ── Warship GLB (visible on every preset, parked in the sky) ─────────
      // Attach a DRACOLoader — many Sketchfab exports use Draco mesh
      // compression and can't be parsed without it.
      //
      // Group hierarchy (outer → inner):
      //   shipRoot      — world position + yaw drift
      //   orientGrp     — static pitch so model's Y-axis lies vertical (tail down)
      //   spinGroups[i] — one group per concentric radius band, spun at
      //                   SHIP_SPIN_SPEEDS[i] in animate()
      let shipRoot: THREE.Group | null = null;
      let shipSpinGroups: THREE.Group[] | null = null;
      const dracoLoader = new DRACOLoader();
      dracoLoader.setDecoderPath('https://www.gstatic.com/draco/v1/decoders/');
      const gltfLoader = new GLTFLoader().setDRACOLoader(dracoLoader);
      gltfLoader.load(
        SHIP_MODEL_URL,
        (gltf) => {
          if (disposed) return;
          shipRoot = new THREE.Group();
          const orientGrp = new THREE.Group();
          orientGrp.rotation.x = SHIP_MODEL_PITCH;
          shipRoot.add(orientGrp);
          const spinGroups: THREE.Group[] = [];
          for (let i = 0; i < SHIP_SPIN_SPEEDS.length; i++) {
            const g = new THREE.Group();
            orientGrp.add(g);
            spinGroups.push(g);
          }
          shipSpinGroups = spinGroups;

          // Centre on bounding box so SHIP_POSITION refers to the hull origin
          const box = new THREE.Box3().setFromObject(gltf.scene);
          const center = box.getCenter(new THREE.Vector3());
          const size = box.getSize(new THREE.Vector3());
          gltf.scene.position.sub(center);
          // Start with every mesh in the outermost bucket; the partition
          // pass below re-parents inner meshes to their proper bucket.
          spinGroups[0].add(gltf.scene);
          // Auto-scale: make the longest axis = SHIP_TARGET_LONGEST_M metres
          // so the model is visible regardless of its source units.
          const longest = Math.max(size.x, size.y, size.z) || 1;
          const autoScale = SHIP_TARGET_LONGEST_M / longest;
          shipRoot.scale.setScalar(autoScale);
          // Initial position is overwritten each frame by the camera-relative updater
          shipRoot.rotation.y = SHIP_HEADING;
          shipRoot.visible = true;
          shipLight.visible = true;
          shipLight.target = shipRoot;

          // Radius-banded ring partition. For each mesh compute the MAX
          // XZ distance any bbox corner reaches from the ship's spin
          // axis (NOT the bbox centre — a ring modelled around origin
          // has centre 0,0,0, which wrongly classifies the hull as
          // "inner"). Then assign to the first bucket whose
          // MIN-radius fraction it clears.
          shipRoot.updateMatrixWorld(true);
          const shipXZHalf = Math.max(size.x, size.z) * 0.5 || 1;
          const meshInfos: { mesh: THREE.Mesh; radius: number }[] = [];
          const _tmp = new THREE.Vector3();
          gltf.scene.traverse(o => {
            const m = o as THREE.Mesh;
            if (!m.isMesh) return;
            const bbox = new THREE.Box3().setFromObject(m);
            let maxR = 0;
            for (let i = 0; i < 8; i++) {
              _tmp.set(
                (i & 1) ? bbox.max.x : bbox.min.x,
                (i & 2) ? bbox.max.y : bbox.min.y,
                (i & 4) ? bbox.max.z : bbox.min.z,
              );
              gltf.scene.worldToLocal(_tmp);
              _tmp.sub(center);
              const r = Math.sqrt(_tmp.x * _tmp.x + _tmp.z * _tmp.z);
              if (r > maxR) maxR = r;
            }
            meshInfos.push({ mesh: m, radius: maxR });
          });
          const bucketCounts = new Array(spinGroups.length).fill(0);
          for (const info of meshInfos) {
            const frac = info.radius / shipXZHalf;
            // Find the first band whose min-radius threshold this mesh clears.
            // Meshes below the smallest threshold fall into the innermost bucket.
            let bucketIdx = spinGroups.length - 1;
            for (let i = 0; i < SHIP_SPIN_BUCKET_MIN_RADII.length; i++) {
              if (frac >= SHIP_SPIN_BUCKET_MIN_RADII[i]) { bucketIdx = i; break; }
            }
            if (bucketIdx !== 0) spinGroups[bucketIdx].attach(info.mesh);
            bucketCounts[bucketIdx]++;
          }
          console.info(
            `[Ocean] Ring spin buckets (outer→inner): ${bucketCounts.join(' / ')} meshes, ` +
            `ship XZ half-extent ${shipXZHalf.toFixed(1)}`,
          );
          console.info(
            `[Ocean] Warship loaded — raw bbox ${size.x.toFixed(1)}×${size.y.toFixed(1)}×${size.z.toFixed(1)}, ` +
            `auto-scale ${autoScale.toFixed(4)} (longest → ${SHIP_TARGET_LONGEST_M}m)`,
          );
          // Fog on every material; preserve any existing emissive texture
          // (Sketchfab warships typically have bright-window emissive maps)
          // and nudge intensity so they pop at night's low exposure.
          // polygonOffset nudges coplanar decal/detail surfaces out of
          // z-fight range — a common Sketchfab import artefact.
          //
          // Max anisotropy on every texture map: Sketchfab hulls have
          // busy panel/window textures that shimmer badly at distance
          // without AF. This is the real source of "needs more AA" on
          // imported models — GPU MSAA only anti-aliases geometry
          // edges, not texture minification.
          const maxAniso = renderer.capabilities?.getMaxAnisotropy?.() ?? 16;
          const textureSlots: readonly string[] = [
            'map', 'normalMap', 'roughnessMap', 'metalnessMap',
            'emissiveMap', 'aoMap', 'bumpMap', 'alphaMap',
          ];
          shipRoot.traverse(o => {
            const mesh = o as THREE.Mesh;
            if (!mesh.isMesh) return;
            const mats = Array.isArray(mesh.material) ? mesh.material : [mesh.material];
            for (const mat of mats) {
              const m = mat as THREE.Material & {
                fog?: boolean;
                emissiveIntensity?: number;
                [slot: string]: unknown;
              };
              if (!m) continue;
              m.fog = true;
              if (typeof m.emissiveIntensity === 'number') {
                m.emissiveIntensity = Math.max(m.emissiveIntensity, 1.4);
              }
              m.polygonOffset = true;
              m.polygonOffsetFactor = 2;
              m.polygonOffsetUnits = 2;
              for (const slot of textureSlots) {
                const tex = m[slot] as THREE.Texture | null | undefined;
                if (tex && tex.isTexture) {
                  tex.anisotropy = maxAniso;
                  tex.minFilter = THREE.LinearMipmapLinearFilter;
                  tex.generateMipmaps = true;
                  tex.needsUpdate = true;
                }
              }
            }
          });
          scene.add(shipRoot);
        },
        undefined,
        (err) => {
          console.warn(
            `[Ocean] ⚠ Warship not loaded from ${SHIP_MODEL_URL}.\n` +
            `  Download a GLB (e.g. the Sketchfab Imperial Warship,\n` +
            `  https://sketchfab.com/3d-models/imperial-warship-based-on-the-tv-show-c9aac6b3b4df4b3ba1554bf0622bd3df)\n` +
            `  and save it to Apps/ga-client/public/models/warship.glb.\n` +
            `  (Underlying error:`, err, ')',
          );
        },
      );

      // ── Ocean mesh ──
      const oceanGeo = new THREE.PlaneGeometry(
        quality.oceanSize, quality.oceanSize, quality.meshRes, quality.meshRes,
      );
      oceanGeo.rotateX(-Math.PI / 2);

      // ── Try FFT, fall back to Gerstner ──
      let fftCompute: OceanFFTCompute | null = null;
      let uniforms: { time: { value: number }; sunDirection: { value: THREE.Vector3 } };
      let oceanMat: THREE.Material;
      let mode = 'FFT';

      if (quality.tier === 'high' || quality.tier === 'ultra') {
        try {
          fftCompute = new OceanFFTCompute({
            N: 256,
            patchSize: FFT_PATCH_SIZE,
            windSpeed: 11,
            windDirection: [1, 0.3],
            amplitude: 0.0004,
            choppiness: 2.0,
          });
          const result = createOceanFFTMaterial(
            fftCompute.displacementTex, fftCompute.normalFoamTex, FFT_PATCH_SIZE,
          );
          oceanMat = result.material;
          uniforms = result.uniforms;
          mode = `${quality.tier} FFT`;
        } catch (err) {
          console.warn('[Ocean] FFT init failed, using Gerstner:', err);
          fftCompute = null;
        }
      }

      if (!fftCompute) {
        const result = createOceanTSLMaterial({
          waveCount: quality.waveCount,
          fogDensity: quality.fogDensity,
          sunSpecExponent: quality.sunSpecExponent,
          sunSpecMultiplier: quality.sunSpecMultiplier,
        });
        oceanMat = result.material;
        uniforms = result.uniforms;
        mode = `${quality.tier} Gerstner`;
      }

      uniforms!.sunDirection.value.copy(sunDir);
      const oceanMesh = new THREE.Mesh(oceanGeo, oceanMat!);
      scene.add(oceanMesh);

      // ── Post-processing ──
      let postProcessing: PostProcessing | null = null;
      if (quality.enableBloom) {
        postProcessing = new PostProcessing(renderer);
        const scenePass = pass(scene, camera);
        const scenePassColor = scenePass.getTextureNode('output');
        const bloomPass = bloom(scenePassColor, quality.bloomStrength, quality.bloomRadius, quality.bloomThreshold);
        postProcessing.outputNode = scenePassColor.add(bloomPass);
      }

      // ── Preset application (exposed via ref for UI) ──
      applyPresetRef.current = (name: OceanPresetName) => {
        if (disposed) return;
        const preset = OCEAN_PRESETS[name];
        const newSunDir = sunDirectionFromPreset(preset);
        sunLight.position.copy(newSunDir.clone().multiplyScalar(2000));
        uniforms!.sunDirection.value.copy(newSunDir);
        renderer.toneMappingExposure = preset.exposure;

        // Swap sky background — gradient now, real panorama when it lands
        const sky = buildSkyTexture(preset);
        lastSkyToken = sky;
        currentBgTex.dispose();
        currentBgTex = sky.texture;
        scene.background = currentBgTex;
        swapWhenReady(sky.ready, sky);

        positionMoon(name);
        // Ship stays visible on every preset now — the fill-light follows
        // the camera so it reads well in all sky colours.
      };

      // ── Animation ──
      const clock = new THREE.Clock();
      let frameCount = 0;
      let fpsAccum = 0;
      let fftFailed = false;
      let time = 0;

      const animate = () => {
        if (disposed) return;
        animationFrameId = requestAnimationFrame(animate);
        const dt = clock.getDelta();
        time += dt;
        uniforms!.time.value = time;

        if (fftCompute && !fftFailed) {
          try {
            fftCompute.update(renderer, time);
          } catch (err) {
            console.error('[Ocean] FFT compute error, disabling:', err);
            fftFailed = true;
            mode = `${quality.tier} Gerstner (FFT failed)`;
          }
        }

        frameCount++;
        fpsAccum += dt;
        if (frameCount >= 30) {
          updateHud(Math.round(frameCount / fpsAccum), mode);
          frameCount = 0;
          fpsAccum = 0;
        }

        // Subtle sky-drift + independent concentric-ring spin. Every
        // bucket rotates around the model's own Y axis at its own
        // rad/s, so nested rings appear to move independently
        // (hyperspace windup).
        if (shipRoot && shipRoot.visible) {
          shipRoot.rotation.y = SHIP_HEADING + Math.sin(time * 0.1) * 0.08;
          // Larger z/x wobble (~13°/~9°) so the vertical ship isn't seen
          // perfectly edge-on — reveals the ring faces as moving
          // ellipses, giving the scene real 3D presence.
          shipRoot.rotation.z = Math.sin(time * 0.15 + 0.5) * 0.23;
          shipRoot.rotation.x = Math.sin(time * 0.11 + 1.2) * 0.15;
          if (shipSpinGroups) {
            for (let i = 0; i < shipSpinGroups.length; i++) {
              shipSpinGroups[i].rotation.y = time * SHIP_SPIN_SPEEDS[i];
            }
          }
        }

        controls.update();
        // Forbid underwater: clamp camera Y above the wave peaks. Also keep
        // the orbit target from sliding below water so panning stays stable.
        if (camera.position.y < MIN_CAMERA_Y) camera.position.y = MIN_CAMERA_Y;
        if (controls.target.y < 0) controls.target.y = 0;
        // Re-lock the moon to the camera so it appears at infinity (no parallax).
        updateMoonPositionForCamera();
        if (postProcessing) { postProcessing.render(); } else { renderer.render(scene, camera); }
      };
      animate();

      // ── Resize ──
      // ResizeObserver tracks container size changes that don't fire a
      // window resize event (flex layout shifts, DevTools open/close, etc.)
      const onResize = () => {
        if (!container || disposed) return;
        const rw = Math.max(1, width || container.clientWidth);
        const rh = Math.max(1, height || container.clientHeight);
        camera.aspect = rw / rh;
        camera.updateProjectionMatrix();
        renderer.setSize(rw, rh);
      };
      window.addEventListener('resize', onResize);
      const resizeObserver = new ResizeObserver(onResize);
      resizeObserver.observe(container);

      // ── Cleanup ──
      cleanupRef.current = () => {
        disposed = true;
        applyPresetRef.current = null;
        window.removeEventListener('resize', onResize);
        resizeObserver.disconnect();
        if (animationFrameId !== null) cancelAnimationFrame(animationFrameId);
        controls.dispose();
        oceanGeo.dispose();
        oceanMat!.dispose();
        currentBgTex.dispose();
        moonMesh.geometry.dispose();
        moonMat.dispose();
        moonMap.dispose();
        moonBump.dispose();
        if (shipRoot) {
          shipRoot.traverse(o => {
            const mesh = o as THREE.Mesh;
            if (mesh.isMesh) {
              mesh.geometry?.dispose();
              const m = mesh.material;
              if (Array.isArray(m)) m.forEach(x => x.dispose());
              else m?.dispose();
            }
          });
          scene.remove(shipRoot);
        }
        scene.remove(shipLight);
        dracoLoader.dispose();
        fftCompute?.dispose();
        renderer.dispose();
        if (container.contains(renderer.domElement)) container.removeChild(renderer.domElement);
      };
    };

    init();
    return () => { cleanupRef.current?.(); };
  }, [width, height, updateHud]);

  return (
    <div ref={containerRef} style={{ width: width || '100%', height: height || '100%', overflow: 'hidden', position: 'relative' }}>
      <PresetPanel current={currentPreset} onSelect={handlePresetSelect} />
      <div ref={fpsRef} style={{
        position: 'absolute', top: 12, right: 12, color: '#a0c0d0',
        fontFamily: 'monospace', fontSize: '12px', background: 'rgba(0,0,0,0.5)',
        padding: '6px 10px', borderRadius: 4, pointerEvents: 'none', zIndex: 100,
      }} />
    </div>
  );
};

export default Ocean;
