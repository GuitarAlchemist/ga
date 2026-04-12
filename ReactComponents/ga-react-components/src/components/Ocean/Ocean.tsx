/**
 * Ocean Component — TSL + WebGPU
 *
 * Tessendorf-style ocean targeting WebTide visuals:
 * - 8-wave Gerstner with choppy horizontal displacement
 * - Concentrated sun specular on wave facets
 * - Schlick Fresnel: dark foreground → bright reflective horizon
 * - Heavy atmospheric fog blending ocean seamlessly into sky
 * - Physical sky background (gradient fallback for WebGPU)
 * - FPS counter overlay
 *
 * Renderer: WebGPURenderer with async init, WebGL2 fallback.
 */

import React, { useEffect, useRef, useCallback } from 'react';
import * as THREE from 'three';
import { WebGPURenderer } from 'three/webgpu';
import { OrbitControls } from 'three/examples/jsm/controls/OrbitControls.js';
import { createOceanTSLMaterial } from './OceanTSL';

// ── Configuration ────────────────────────────────────────────────────────────

const OCEAN_SIZE = 8000;
const OCEAN_RES  = 512;

// Lower sun for dramatic specular trail
const SUN_ELEVATION_DEG = 22;
const SUN_AZIMUTH_DEG   = 200;

// Camera near water surface, looking toward horizon
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

  const updateFps = useCallback((fps: number) => {
    if (fpsRef.current) {
      fpsRef.current.textContent = `${fps} FPS · WebGPU`;
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

      // ── Scene ──
      const scene = new THREE.Scene();

      // Sky-colored gradient background (works with both renderers)
      // Creates an overcast sky look matching WebTide
      const skyCanvas = document.createElement('canvas');
      skyCanvas.width = 1;
      skyCanvas.height = 512;
      const ctx = skyCanvas.getContext('2d')!;
      const grad = ctx.createLinearGradient(0, 0, 0, 512);
      grad.addColorStop(0.0, '#6a7a90');   // upper sky (blue-grey)
      grad.addColorStop(0.35, '#8a94a2');  // mid sky
      grad.addColorStop(0.50, '#a0a6ae');  // horizon band (bright overcast)
      grad.addColorStop(0.55, '#9aa0aa');  // just below horizon
      grad.addColorStop(1.0, '#606870');   // lower (water reflection)
      ctx.fillStyle = grad;
      ctx.fillRect(0, 0, 1, 512);
      const bgTex = new THREE.CanvasTexture(skyCanvas);
      bgTex.mapping = THREE.EquirectangularReflectionMapping;
      bgTex.colorSpace = THREE.SRGBColorSpace;
      scene.background = bgTex;

      // ── Camera ──
      const camera = new THREE.PerspectiveCamera(55, w / h, 0.5, 20000);
      camera.position.copy(CAMERA_START);

      // ── Renderer (WebGPU only) ──
      const renderer = new WebGPURenderer({
        antialias: true,
        forceWebGL: false,
      });
      await renderer.init();
      if (disposed) { renderer.dispose(); return; }

      renderer.setSize(w, h);
      renderer.setPixelRatio(Math.min(window.devicePixelRatio, 2));
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
        1,
        Math.PI / 2 - sunPhi,
        sunTheta,
      );

      // ── Directional light ──
      const sunLight = new THREE.DirectionalLight(0xfff8f0, 3.0);
      sunLight.position.copy(sunDir.clone().multiplyScalar(2000));
      scene.add(sunLight);
      scene.add(new THREE.AmbientLight(0x304060, 0.1));

      // ── Ocean Mesh ──
      const oceanGeo = new THREE.PlaneGeometry(OCEAN_SIZE, OCEAN_SIZE, OCEAN_RES, OCEAN_RES);
      oceanGeo.rotateX(-Math.PI / 2);

      const { material: oceanMat, uniforms } = createOceanTSLMaterial();
      uniforms.sunDirection.value.copy(sunDir);

      const oceanMesh = new THREE.Mesh(oceanGeo, oceanMat);
      scene.add(oceanMesh);

      // ── Animation + FPS counter ──
      const clock = new THREE.Clock();
      let frameCount = 0;
      let fpsAccum = 0;

      const animate = () => {
        if (disposed) return;
        animationFrameId = requestAnimationFrame(animate);

        const dt = clock.getDelta();
        uniforms.time.value += dt;

        // FPS counter (update every 30 frames)
        frameCount++;
        fpsAccum += dt;
        if (frameCount >= 30) {
          const fps = Math.round(frameCount / fpsAccum);
          updateFps(fps);
          frameCount = 0;
          fpsAccum = 0;
        }

        controls.update();
        renderer.render(scene, camera);
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
  }, [width, height, updateFps]);

  return (
    <div
      ref={containerRef}
      style={{ width: width || '100%', height: height || '100%', overflow: 'hidden', position: 'relative' }}
    >
      {/* FPS overlay */}
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
