/**
 * Ocean Component — Adaptive Quality TSL + WebGPU
 *
 * Auto-detects GPU capability and scales:
 * - Low (tablet/mobile): 192² mesh, 4 waves, no post-processing
 * - Medium (integrated GPU): 384² mesh, 6 waves
 * - High (discrete GPU): 512² mesh, 8 waves, bloom + vignette
 * - Ultra (RTX 4070+/5080): 1024² mesh, 8 waves, strong bloom, HDR specular
 *
 * Post-processing on high/ultra: bloom makes sun specular glow on the water.
 */

import React, { useEffect, useRef, useCallback } from 'react';
import * as THREE from 'three';
import { WebGPURenderer, PostProcessing } from 'three/webgpu';
import { pass } from 'three/tsl';
import { bloom } from 'three/examples/jsm/tsl/display/BloomNode.js';
import { OrbitControls } from 'three/examples/jsm/controls/OrbitControls.js';
import { createOceanTSLMaterial } from './OceanTSL';
import { detectQualityTier } from './OceanQuality';

// ── Scene configuration ──────────────────────────────────────────────────────

const SUN_ELEVATION_DEG = 22;
const SUN_AZIMUTH_DEG   = 200;
const CAMERA_START = new THREE.Vector3(0, 4.5, 25);
const CAMERA_TARGET = new THREE.Vector3(0, 0, -300);

export interface OceanProps {
  width?: number;
  height?: number;
}

export const Ocean: React.FC<OceanProps> = ({ width, height }) => {
  const containerRef = useRef<HTMLDivElement>(null);
  const cleanupRef = useRef<(() => void) | null>(null);
  const fpsRef = useRef<HTMLDivElement>(null);

  const updateHud = useCallback((fps: number, tier: string) => {
    if (fpsRef.current) {
      fpsRef.current.textContent = `${fps} FPS · ${tier}`;
    }
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

      // ── Detect GPU quality tier ──
      const quality = await detectQualityTier();
      console.log(`[Ocean] Quality tier: ${quality.tier} (mesh ${quality.meshRes}², ${quality.waveCount} waves, bloom: ${quality.enableBloom})`);

      // ── Scene ──
      const scene = new THREE.Scene();

      // Overcast sky gradient background
      const skyCanvas = document.createElement('canvas');
      skyCanvas.width = 1;
      skyCanvas.height = 512;
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
      const renderer = new WebGPURenderer({
        antialias: true,
        forceWebGL: false,
      });
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
      controls.maxPolarAngle = Math.PI / 2 - 0.01;
      controls.minDistance = 1.5;
      controls.maxDistance = 3000;

      // ── Sun direction ──
      const sunPhi = THREE.MathUtils.degToRad(SUN_ELEVATION_DEG);
      const sunTheta = THREE.MathUtils.degToRad(SUN_AZIMUTH_DEG);
      const sunDir = new THREE.Vector3().setFromSphericalCoords(
        1, Math.PI / 2 - sunPhi, sunTheta,
      );

      // ── Lights ──
      const sunLight = new THREE.DirectionalLight(0xfff8f0, 3.0);
      sunLight.position.copy(sunDir.clone().multiplyScalar(2000));
      scene.add(sunLight);
      scene.add(new THREE.AmbientLight(0x304060, 0.1));

      // ── Ocean mesh (resolution from quality tier) ──
      const oceanGeo = new THREE.PlaneGeometry(
        quality.oceanSize, quality.oceanSize,
        quality.meshRes, quality.meshRes,
      );
      oceanGeo.rotateX(-Math.PI / 2);

      const { material: oceanMat, uniforms } = createOceanTSLMaterial({
        waveCount: quality.waveCount,
        fogDensity: quality.fogDensity,
        sunSpecExponent: quality.sunSpecExponent,
        sunSpecMultiplier: quality.sunSpecMultiplier,
      });
      uniforms.sunDirection.value.copy(sunDir);

      const oceanMesh = new THREE.Mesh(oceanGeo, oceanMat);
      scene.add(oceanMesh);

      // ── Post-processing (high/ultra only) ──
      let postProcessing: PostProcessing | null = null;

      if (quality.enableBloom) {
        postProcessing = new PostProcessing(renderer);
        const scenePass = pass(scene, camera);
        const scenePassColor = scenePass.getTextureNode('output');

        // Bloom: sun specular highlights glow outward
        const bloomPass = bloom(
          scenePassColor,
          quality.bloomStrength,
          quality.bloomRadius,
          quality.bloomThreshold,
        );

        const outputNode = scenePassColor.add(bloomPass);

        // Vignette: darken edges for cinematic feel
        if (quality.enableVignette) {
          // Simple vignette via post-process is done by darkening the output
          // TSL vignette: darken based on UV distance from center
          // For now, rely on bloom + tone mapping for the cinematic look
          // (full vignette node would need screenUV which we can add later)
        }

        postProcessing.outputNode = outputNode;
      }

      // ── Animation + FPS ──
      const clock = new THREE.Clock();
      let frameCount = 0;
      let fpsAccum = 0;
      const tierLabel = `WebGPU ${quality.tier}${quality.enableBloom ? ' + bloom' : ''}`;

      const animate = () => {
        if (disposed) return;
        animationFrameId = requestAnimationFrame(animate);

        const dt = clock.getDelta();
        uniforms.time.value += dt;

        // FPS
        frameCount++;
        fpsAccum += dt;
        if (frameCount >= 30) {
          updateHud(Math.round(frameCount / fpsAccum), tierLabel);
          frameCount = 0;
          fpsAccum = 0;
        }

        controls.update();

        if (postProcessing) {
          postProcessing.render();
        } else {
          renderer.render(scene, camera);
        }
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
        oceanMat.dispose();
        bgTex.dispose();
        renderer.dispose();
        if (container.contains(renderer.domElement)) {
          container.removeChild(renderer.domElement);
        }
      };
    };

    init();

    return () => {
      cleanupRef.current?.();
    };
  }, [width, height, updateHud]);

  return (
    <div
      ref={containerRef}
      style={{ width: width || '100%', height: height || '100%', overflow: 'hidden', position: 'relative' }}
    >
      <div
        ref={fpsRef}
        style={{
          position: 'absolute',
          top: 8,
          right: 12,
          color: '#a0c0d0',
          fontFamily: 'monospace',
          fontSize: '12px',
          background: 'rgba(0,0,0,0.5)',
          padding: '3px 8px',
          borderRadius: 4,
          pointerEvents: 'none',
          zIndex: 10,
        }}
      />
    </div>
  );
};

export default Ocean;
