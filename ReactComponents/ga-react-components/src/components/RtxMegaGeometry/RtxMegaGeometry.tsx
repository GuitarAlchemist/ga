import React, { useEffect, useRef } from 'react';
import { Box } from '@mui/material';
import * as THREE from 'three';
import { OrbitControls } from 'three/examples/jsm/controls/OrbitControls.js';

export interface RtxMegaGeometryProps {
  width?: number;
  height?: number;
  displacement?: number;
  detail?: number;
  adaptiveBands?: boolean;
  wireframe?: boolean;
  autoRotate?: boolean;
}

const hash = (x: number, z: number): number => {
  const v = Math.sin(x * 127.1 + z * 311.7) * 43758.5453123;
  return v - Math.floor(v);
};

const smooth = (value: number): number => value * value * (3 - 2 * value);

const noise2 = (x: number, z: number): number => {
  const ix = Math.floor(x);
  const iz = Math.floor(z);
  const fx = x - ix;
  const fz = z - iz;
  const ux = smooth(fx);
  const uz = smooth(fz);
  const a = hash(ix, iz);
  const b = hash(ix + 1, iz);
  const c = hash(ix, iz + 1);
  const d = hash(ix + 1, iz + 1);
  return THREE.MathUtils.lerp(
    THREE.MathUtils.lerp(a, b, ux),
    THREE.MathUtils.lerp(c, d, ux),
    uz,
  );
};

const ridgeHeight = (x: number, z: number, displacement: number): number => {
  const n1 = noise2(x * 0.09, z * 0.09);
  const n2 = noise2(x * 0.22 + 14.2, z * 0.22 - 3.5);
  const n3 = noise2(x * 0.55 - 7.1, z * 0.55 + 19.4);
  const strata = Math.sin(x * 0.7 + n2 * 4.0) * 0.16 + Math.cos(z * 0.52 + n1 * 5.0) * 0.14;
  const ridges = Math.pow(Math.abs(n1 * 2 - 1), 1.8) * 1.4;
  return (ridges + n2 * 0.9 + n3 * 0.28 + strata) * displacement;
};

const buildPatchGeometry = (
  centerX: number,
  centerZ: number,
  size: number,
  segments: number,
  displacement: number,
): THREE.BufferGeometry => {
  const vertices: number[] = [];
  const colors: number[] = [];
  const indices: number[] = [];
  const color = new THREE.Color();

  for (let z = 0; z <= segments; z++) {
    for (let x = 0; x <= segments; x++) {
      const px = centerX - size / 2 + (x / segments) * size;
      const pz = centerZ - size / 2 + (z / segments) * size;
      const py = ridgeHeight(px, pz, displacement);
      vertices.push(px, py, pz);

      const mineral = noise2(px * 0.18 + 22.0, pz * 0.18);
      const thermal = noise2(px * 0.045 - 3.0, pz * 0.045 + 7.0);
      color
        .setHSL(
          THREE.MathUtils.lerp(0.54, 0.74, mineral),
          THREE.MathUtils.lerp(0.38, 0.78, thermal),
          THREE.MathUtils.clamp(0.22 + py * 0.028 + mineral * 0.22, 0.16, 0.72),
        )
        .convertSRGBToLinear();
      colors.push(color.r, color.g, color.b);
    }
  }

  const row = segments + 1;
  for (let z = 0; z < segments; z++) {
    for (let x = 0; x < segments; x++) {
      const a = z * row + x;
      const b = a + 1;
      const c = a + row;
      const d = c + 1;
      indices.push(a, c, b, b, c, d);
    }
  }

  const geometry = new THREE.BufferGeometry();
  geometry.setAttribute('position', new THREE.Float32BufferAttribute(vertices, 3));
  geometry.setAttribute('color', new THREE.Float32BufferAttribute(colors, 3));
  geometry.setIndex(indices);
  geometry.computeVertexNormals();
  return geometry;
};

const chooseSegments = (distance: number, detail: number, adaptiveBands: boolean): number => {
  const base = adaptiveBands
    ? distance < 10
      ? 56
      : distance < 20
        ? 32
        : 14
    : 32;
  return Math.max(8, Math.round(base * THREE.MathUtils.lerp(0.55, 1.45, detail)));
};

const RtxMegaGeometry: React.FC<RtxMegaGeometryProps> = ({
  width,
  height,
  displacement = 1,
  detail = 0.65,
  adaptiveBands = true,
  wireframe = true,
  autoRotate = true,
}) => {
  const containerRef = useRef<HTMLDivElement>(null);

  useEffect(() => {
    const container = containerRef.current;
    if (!container) return undefined;

    const initialWidth = (width ?? container.clientWidth) || 1280;
    const initialHeight = (height ?? container.clientHeight) || 720;
    const scene = new THREE.Scene();
    scene.background = new THREE.Color(0x05070d);
    scene.fog = new THREE.FogExp2(0x05070d, 0.017);

    const camera = new THREE.PerspectiveCamera(48, initialWidth / initialHeight, 0.1, 320);
    camera.position.set(22, 20, 32);

    const renderer = new THREE.WebGLRenderer({ antialias: true });
    renderer.setPixelRatio(Math.min(window.devicePixelRatio, 1.5));
    renderer.setSize(initialWidth, initialHeight);
    renderer.outputColorSpace = THREE.SRGBColorSpace;
    renderer.toneMapping = THREE.ACESFilmicToneMapping;
    renderer.toneMappingExposure = 1.1;
    container.appendChild(renderer.domElement);

    const controls = new OrbitControls(camera, renderer.domElement);
    controls.enableDamping = true;
    controls.dampingFactor = 0.06;
    controls.target.set(0, 1.8, 0);
    controls.minDistance = 8;
    controls.maxDistance = 95;
    controls.maxPolarAngle = Math.PI * 0.49;

    scene.add(new THREE.HemisphereLight(0x9fd5ff, 0x171017, 1.2));
    const sun = new THREE.DirectionalLight(0xffffff, 2.5);
    sun.position.set(20, 38, 14);
    scene.add(sun);

    const terrain = new THREE.Group();
    scene.add(terrain);

    const material = new THREE.MeshStandardMaterial({
      vertexColors: true,
      roughness: 0.62,
      metalness: 0.28,
      envMapIntensity: 0.7,
    });
    const wireMaterial = new THREE.MeshBasicMaterial({
      color: 0x72f4ff,
      transparent: true,
      opacity: 0.12,
      wireframe: true,
      depthWrite: false,
    });

    const patchSize = 8;
    const patchRadius = 4;
    let triangleCount = 0;

    for (let gx = -patchRadius; gx <= patchRadius; gx++) {
      for (let gz = -patchRadius; gz <= patchRadius; gz++) {
        const centerX = gx * patchSize;
        const centerZ = gz * patchSize;
        const distance = Math.hypot(centerX, centerZ);
        const segments = chooseSegments(distance, detail, adaptiveBands);
        triangleCount += segments * segments * 2;
        const geometry = buildPatchGeometry(centerX, centerZ, patchSize, segments, displacement);
        const mesh = new THREE.Mesh(geometry, material);
        terrain.add(mesh);

        if (wireframe) {
          const wire = new THREE.Mesh(geometry, wireMaterial);
          wire.renderOrder = 2;
          terrain.add(wire);
        }
      }
    }

    const markerMaterial = new THREE.MeshBasicMaterial({ color: 0x58f0ff, transparent: true, opacity: 0.9 });
    const markerGeometry = new THREE.BoxGeometry(0.14, 1, 0.14);
    for (let i = 0; i < 90; i++) {
      const angle = i * 2.3999632297;
      const radius = 4 + Math.sqrt(i) * 2.4;
      const x = Math.cos(angle) * radius;
      const z = Math.sin(angle) * radius;
      const h = ridgeHeight(x, z, displacement);
      const marker = new THREE.Mesh(markerGeometry, markerMaterial);
      marker.position.set(x, h + 0.55, z);
      marker.scale.y = 0.2 + noise2(i * 0.31, i * 0.73) * 1.2;
      terrain.add(marker);
    }

    const clock = new THREE.Clock();
    let raf = 0;
    const animate = () => {
      const t = clock.getElapsedTime();
      if (autoRotate) terrain.rotation.y = t * 0.055;
      markerMaterial.opacity = 0.55 + Math.sin(t * 1.4) * 0.18;
      controls.update();
      renderer.render(scene, camera);
      raf = requestAnimationFrame(animate);
    };
    animate();

    const onResize = () => {
      const w = container.clientWidth;
      const h = container.clientHeight;
      if (!w || !h) return;
      camera.aspect = w / h;
      camera.updateProjectionMatrix();
      renderer.setSize(w, h, false);
    };
    const ro = new ResizeObserver(onResize);
    ro.observe(container);

    container.dataset.triangles = triangleCount.toLocaleString();

    return () => {
      cancelAnimationFrame(raf);
      ro.disconnect();
      controls.dispose();
      terrain.traverse((object) => {
        if (object instanceof THREE.Mesh) {
          object.geometry.dispose();
        }
      });
      material.dispose();
      wireMaterial.dispose();
      markerMaterial.dispose();
      markerGeometry.dispose();
      renderer.dispose();
      if (renderer.domElement.parentNode === container) {
        container.removeChild(renderer.domElement);
      }
    };
  }, [adaptiveBands, autoRotate, detail, displacement, height, width, wireframe]);

  const sx = width !== undefined && height !== undefined
    ? { width, height, overflow: 'hidden' }
    : { width: '100%', height: '100%', overflow: 'hidden' };

  return <Box ref={containerRef} sx={sx} />;
};

export default RtxMegaGeometry;
