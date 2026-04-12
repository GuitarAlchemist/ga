/**
 * Ocean Component — Adaptive Quality TSL + WebGPU
 *
 * Two rendering paths:
 * - FFT (Tessendorf): GPU compute Phillips spectrum → IFFT → displacement textures
 * - Gerstner (fallback): 8-wave analytical waves if FFT compute fails
 *
 * Adaptive quality: scales mesh, waves, and post-processing to GPU capability.
 */

import React, { useEffect, useRef, useCallback } from 'react';
import * as THREE from 'three';
import { WebGPURenderer, PostProcessing } from 'three/webgpu';
import { pass } from 'three/tsl';
import { bloom } from 'three/examples/jsm/tsl/display/BloomNode.js';
import { OrbitControls } from 'three/examples/jsm/controls/OrbitControls.js';
import { createOceanTSLMaterial } from './OceanTSL';
import { createOceanFFTMaterial } from './OceanTSLFFT';
import { OceanFFTCompute } from './OceanFFTCompute';
import { detectQualityTier } from './OceanQuality';

const SUN_ELEVATION_DEG = 15;   // lower sun = longer specular trail on water
const SUN_AZIMUTH_DEG   = 200;
const CAMERA_START = new THREE.Vector3(0, 4.5, 25);
const CAMERA_TARGET = new THREE.Vector3(0, 0, -300);
const FFT_PATCH_SIZE = 250;  // smaller patch = finer wave detail at close range

export interface OceanProps {
  width?: number;
  height?: number;
}

export const Ocean: React.FC<OceanProps> = ({ width, height }) => {
  const containerRef = useRef<HTMLDivElement>(null);
  const cleanupRef = useRef<(() => void) | null>(null);
  const fpsRef = useRef<HTMLDivElement>(null);

  const updateHud = useCallback((fps: number, mode: string) => {
    if (fpsRef.current) fpsRef.current.textContent = `${fps} FPS · ${mode}`;
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
      const skyCanvas = document.createElement('canvas');
      skyCanvas.width = 1; skyCanvas.height = 512;
      const ctx = skyCanvas.getContext('2d')!;
      const grad = ctx.createLinearGradient(0, 0, 0, 512);
      grad.addColorStop(0.0, '#6a7a90');
      grad.addColorStop(0.35, '#8a94a2');
      grad.addColorStop(0.50, '#a0a6ae');
      grad.addColorStop(0.55, '#9aa0aa');
      grad.addColorStop(1.0, '#606870');
      ctx.fillStyle = grad;
      ctx.fillRect(0, 0, 1, 512);
      const bgTex = new THREE.CanvasTexture(skyCanvas);
      bgTex.mapping = THREE.EquirectangularReflectionMapping;
      bgTex.colorSpace = THREE.SRGBColorSpace;
      scene.background = bgTex;

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
      renderer.toneMappingExposure = 0.9;
      container.appendChild(renderer.domElement);

      // ── Controls ──
      const controls = new OrbitControls(camera, renderer.domElement);
      controls.enableDamping = true;
      controls.dampingFactor = 0.05;
      controls.target.copy(CAMERA_TARGET);
      controls.maxPolarAngle = Math.PI * 0.85; // allow underwater view
      controls.minDistance = 1.5;
      controls.maxDistance = 3000;

      // ── Sun ──
      const sunPhi = THREE.MathUtils.degToRad(SUN_ELEVATION_DEG);
      const sunTheta = THREE.MathUtils.degToRad(SUN_AZIMUTH_DEG);
      const sunDir = new THREE.Vector3().setFromSphericalCoords(1, Math.PI / 2 - sunPhi, sunTheta);
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

      // Gerstner fallback
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

        // FFT compute (with crash protection)
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
        window.removeEventListener('resize', onResize);
        if (animationFrameId !== null) cancelAnimationFrame(animationFrameId);
        controls.dispose();
        oceanGeo.dispose();
        oceanMat!.dispose();
        bgTex.dispose();
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
      <div ref={fpsRef} style={{
        position: 'absolute', top: 8, right: 12, color: '#a0c0d0',
        fontFamily: 'monospace', fontSize: '12px', background: 'rgba(0,0,0,0.5)',
        padding: '3px 8px', borderRadius: 4, pointerEvents: 'none', zIndex: 10,
      }} />
    </div>
  );
};

export default Ocean;
