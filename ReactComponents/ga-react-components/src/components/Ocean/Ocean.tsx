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
import { createOceanTSLMaterial } from './OceanTSL';
import { createOceanFFTMaterial } from './OceanTSLFFT';
import { OceanFFTCompute } from './OceanFFTCompute';
import { detectQualityTier } from './OceanQuality';
import { OCEAN_PRESETS, buildSkyTexture, sunDirectionFromPreset, type OceanPresetName } from './OceanPresets';

const CAMERA_START = new THREE.Vector3(0, 4.5, 25);
const CAMERA_TARGET = new THREE.Vector3(0, 0, -300);
const FFT_PATCH_SIZE = 250;

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
    display: 'flex',
    gap: 4,
    zIndex: 100,
  }}>
    {(Object.keys(OCEAN_PRESETS) as OceanPresetName[]).map(name => {
      const isActive = name === current;
      return (
        <button
          key={name}
          onClick={() => onSelect(name)}
          style={{
            padding: '6px 12px',
            fontFamily: 'monospace',
            fontSize: '11px',
            fontWeight: 600,
            background: isActive ? 'rgba(0,229,255,0.25)' : 'rgba(0,0,0,0.5)',
            color: isActive ? '#00e5ff' : '#a0c0d0',
            border: `1px solid ${isActive ? '#00e5ff' : 'rgba(255,255,255,0.1)'}`,
            borderRadius: 4,
            cursor: 'pointer',
            textTransform: 'uppercase',
            letterSpacing: '0.5px',
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
      let currentBgTex = buildSkyTexture(OCEAN_PRESETS.stormy);
      scene.background = currentBgTex;

      // ── Camera ──
      const camera = new THREE.PerspectiveCamera(55, w / h, 0.5, 20000);
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
      controls.maxPolarAngle = Math.PI * 0.85;
      controls.minDistance = 1.5;
      controls.maxDistance = 3000;

      // ── Sun + lights ──
      const sunDir = sunDirectionFromPreset(OCEAN_PRESETS.stormy);
      const sunLight = new THREE.DirectionalLight(0xfff8f0, 3.0);
      sunLight.position.copy(sunDir.clone().multiplyScalar(2000));
      scene.add(sunLight);
      scene.add(new THREE.AmbientLight(0x304060, 0.1));

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

        // Swap sky background
        const newBgTex = buildSkyTexture(preset);
        scene.background = newBgTex;
        currentBgTex.dispose();
        currentBgTex = newBgTex;
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

        controls.update();
        if (postProcessing) { postProcessing.render(); } else { renderer.render(scene, camera); }
      };
      animate();

      // ── Resize ──
      const onResize = () => {
        if (!container || disposed) return;
        const rw = width || container.clientWidth;
        const rh = height || container.clientHeight;
        camera.aspect = rw / rh;
        camera.updateProjectionMatrix();
        renderer.setSize(rw, rh);
      };
      window.addEventListener('resize', onResize);

      // ── Cleanup ──
      cleanupRef.current = () => {
        disposed = true;
        applyPresetRef.current = null;
        window.removeEventListener('resize', onResize);
        if (animationFrameId !== null) cancelAnimationFrame(animationFrameId);
        controls.dispose();
        oceanGeo.dispose();
        oceanMat!.dispose();
        currentBgTex.dispose();
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
