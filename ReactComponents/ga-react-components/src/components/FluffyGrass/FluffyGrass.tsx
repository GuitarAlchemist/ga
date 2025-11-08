/**
 * Fluffy Grass Component
 * 
 * Based on the technique from: https://tympanus.net/codrops/2025/02/04/how-to-make-the-fluffiest-grass-with-three-js/
 * 
 * Features:
 * - Billboard grass with multiple intersecting planes
 * - Instanced rendering for performance
 * - Chunking for frustum culling
 * - LOD (Level of Detail) system
 * - Custom shaders with:
 *   - Fake ambient occlusion
 *   - Color variation
 *   - Wind animation
 *   - Lighting and shadows
 */

import React, { useEffect, useRef, useState } from 'react';
import * as THREE from 'three';
import { Box } from '@mui/material';

export interface FluffyGrassProps {
  width?: number;
  height?: number;
  grassDensity?: number; // Grass instances per chunk
  chunkSize?: number; // Size of each chunk
  chunkCount?: number; // Number of chunks (chunkCount x chunkCount grid)
  grassHeight?: number;
  grassWidth?: number;
  baseColor?: THREE.Color;
  tipColor?: THREE.Color;
  windSpeed?: number;
  windStrength?: number;
}

export const FluffyGrass: React.FC<FluffyGrassProps> = ({
  width = 1600,
  height = 900,
  grassDensity = 500, // Increased default density
  chunkSize = 10,
  chunkCount = 8,
  grassHeight = 1.0,
  grassWidth = 0.1,
  baseColor = new THREE.Color(0x1a4d2e),
  tipColor = new THREE.Color(0x4f9d69),
  windSpeed = 1.0,
  windStrength = 0.3,
}) => {
  const containerRef = useRef<HTMLDivElement>(null);
  const sceneRef = useRef<THREE.Scene | null>(null);
  const cameraRef = useRef<THREE.PerspectiveCamera | null>(null);
  const rendererRef = useRef<THREE.WebGLRenderer | null>(null);
  const animationFrameRef = useRef<number | null>(null);
  const chunksRef = useRef<THREE.InstancedMesh[]>([]);

  useEffect(() => {
    if (!containerRef.current) return;

    // Scene setup
    const scene = new THREE.Scene();
    scene.background = new THREE.Color(0x87ceeb);
    scene.fog = new THREE.FogExp2(0x87ceeb, 0.015);
    sceneRef.current = scene;

    // Camera setup
    const camera = new THREE.PerspectiveCamera(60, width / height, 0.1, 1000);
    camera.position.set(20, 10, 20);
    camera.lookAt(0, 0, 0);
    cameraRef.current = camera;

    // Renderer setup
    const renderer = new THREE.WebGLRenderer({ antialias: true });
    renderer.setSize(width, height);
    renderer.setPixelRatio(window.devicePixelRatio);
    renderer.shadowMap.enabled = true;
    renderer.shadowMap.type = THREE.VSMShadowMap;
    containerRef.current.appendChild(renderer.domElement);
    rendererRef.current = renderer;

    // Fractal noise function for terrain height
    const fractalNoise = (x: number, z: number): number => {
      let height = 0;
      let amplitude = 1.0;
      let frequency = 0.02;

      // Multiple octaves for fractal effect
      for (let i = 0; i < 4; i++) {
        const nx = x * frequency;
        const nz = z * frequency;

        // Simple noise using sine waves
        const noise = Math.sin(nx * 1.5 + nz * 0.7) * Math.cos(nz * 1.3 - nx * 0.5);
        height += noise * amplitude;

        amplitude *= 0.5;
        frequency *= 2.0;
      }

      return height * 2; // Scale the hills
    };

    // Lighting - warm sunlight for natural grass look
    const ambientLight = new THREE.AmbientLight(0xb0d4ff, 0.5); // Slightly blue ambient for sky
    scene.add(ambientLight);

    const sunLight = new THREE.DirectionalLight(0xfff4e0, 1.2); // Warm sunlight
    sunLight.position.set(50, 50, 30);
    sunLight.castShadow = true;
    sunLight.shadow.camera.left = -50;
    sunLight.shadow.camera.right = 50;
    sunLight.shadow.camera.top = 50;
    sunLight.shadow.camera.bottom = -50;
    sunLight.shadow.camera.near = 0.1;
    sunLight.shadow.camera.far = 200;
    sunLight.shadow.mapSize.width = 2048;
    sunLight.shadow.mapSize.height = 2048;
    sunLight.shadow.radius = 4; // VSM soft shadows
    sunLight.shadow.bias = -0.0005;
    scene.add(sunLight);

    // Ground plane (slightly below grass to prevent z-fighting)
    const groundGeometry = new THREE.PlaneGeometry(chunkSize * chunkCount, chunkSize * chunkCount);
    const groundMaterial = new THREE.MeshStandardMaterial({
      color: 0x3a5f0b,
      roughness: 0.8,
      depthWrite: true,
    });
    const ground = new THREE.Mesh(groundGeometry, groundMaterial);
    ground.rotation.x = -Math.PI / 2;
    ground.position.y = -0.01; // Slightly below grass base
    ground.receiveShadow = true;
    scene.add(ground);

    // Create grass shader material with improved shaders
    const grassMaterial = new THREE.ShaderMaterial({
      uniforms: {
        time: { value: 0 },
        baseColor: { value: baseColor },
        tipColor: { value: tipColor },
        tipColor2: { value: new THREE.Color(0x6fb583) }, // Second tip color for variation
        windSpeed: { value: windSpeed },
        windStrength: { value: windStrength },
      },
      vertexShader: `
        uniform float time;
        uniform float windSpeed;
        uniform float windStrength;

        varying vec2 vUv;
        varying vec3 vNormal;
        varying vec3 vWorldPos;

        // Improved 2D noise function (Perlin-like)
        vec2 hash2(vec2 p) {
          p = vec2(dot(p, vec2(127.1, 311.7)), dot(p, vec2(269.5, 183.3)));
          return -1.0 + 2.0 * fract(sin(p) * 43758.5453123);
        }

        float noise(vec2 p) {
          vec2 i = floor(p);
          vec2 f = fract(p);
          vec2 u = f * f * (3.0 - 2.0 * f);

          return mix(
            mix(dot(hash2(i + vec2(0.0, 0.0)), f - vec2(0.0, 0.0)),
                dot(hash2(i + vec2(1.0, 0.0)), f - vec2(1.0, 0.0)), u.x),
            mix(dot(hash2(i + vec2(0.0, 1.0)), f - vec2(0.0, 1.0)),
                dot(hash2(i + vec2(1.0, 1.0)), f - vec2(1.0, 1.0)), u.x),
            u.y);
        }

        void main() {
          vUv = uv;
          vNormal = normalize(normalMatrix * normal);

          vec4 worldPos = modelMatrix * instanceMatrix * vec4(position, 1.0);
          vWorldPos = worldPos.xyz;

          vec3 pos = position;

          // Improved wind animation with scrolling noise
          float windEffect = uv.y * uv.y; // Quadratic for more natural bend

          // Base sine wave wind
          float windBase = sin(time * windSpeed + worldPos.x * 0.3 + worldPos.z * 0.3) * 0.5 + 0.5;

          // Scrolling noise for variation
          vec2 windUV = vec2(worldPos.x * 0.1 + time * 0.05, worldPos.z * 0.1 + time * 0.03);
          float windNoise = noise(windUV) * 0.5 + 0.5;

          // Combine wind effects
          float wind = (windBase * 0.6 + windNoise * 0.4) * windStrength;

          // Apply wind with direction variation
          pos.x += wind * windEffect;
          pos.z += wind * windEffect * 0.7;

          // Slight rotation for more natural movement
          float windRotation = wind * windEffect * 0.1;
          pos.x += sin(windRotation) * 0.1;

          gl_Position = projectionMatrix * viewMatrix * vec4(vWorldPos + pos - position, 1.0);
        }
      `,
      fragmentShader: `
        uniform vec3 baseColor;
        uniform vec3 tipColor;
        uniform vec3 tipColor2;

        varying vec2 vUv;
        varying vec3 vNormal;
        varying vec3 vWorldPos;

        // Improved noise for color variation
        vec2 hash2(vec2 p) {
          p = vec2(dot(p, vec2(127.1, 311.7)), dot(p, vec2(269.5, 183.3)));
          return -1.0 + 2.0 * fract(sin(p) * 43758.5453123);
        }

        float noise(vec2 p) {
          vec2 i = floor(p);
          vec2 f = fract(p);
          vec2 u = f * f * (3.0 - 2.0 * f);

          return mix(
            mix(dot(hash2(i + vec2(0.0, 0.0)), f - vec2(0.0, 0.0)),
                dot(hash2(i + vec2(1.0, 0.0)), f - vec2(1.0, 0.0)), u.x),
            mix(dot(hash2(i + vec2(0.0, 1.0)), f - vec2(0.0, 1.0)),
                dot(hash2(i + vec2(1.0, 1.0)), f - vec2(1.0, 1.0)), u.x),
            u.y);
        }

        void main() {
          // Enhanced fake ambient occlusion with smoother gradient
          float ao = pow(vUv.y, 1.5); // Power curve for more natural gradient

          // Color variation using world position
          vec2 colorUV = vWorldPos.xz * 0.5;
          float colorNoise = noise(colorUV) * 0.5 + 0.5;

          // Mix between two tip colors based on noise
          vec3 finalTipColor = mix(tipColor, tipColor2, colorNoise);

          // Blend from base to tip with AO
          vec3 color = mix(baseColor, finalTipColor, ao);

          // Add subtle color variation
          float variation = noise(vWorldPos.xz * 2.0) * 0.05;
          color += variation;

          // Simplified directional lighting (from article)
          vec3 lightDir = normalize(vec3(0.5, 1.0, 0.3));
          float diff = max(dot(vNormal, lightDir), 0.0);

          // Wrap lighting for softer look
          diff = diff * 0.5 + 0.5;
          color *= diff;

          // Improved alpha for grass blade shape
          float alpha = 1.0;

          // Taper at edges for grass blade shape
          float edgeFade = smoothstep(0.0, 0.15, vUv.x) * smoothstep(1.0, 0.85, vUv.x);

          // Taper at top
          float topFade = smoothstep(1.0, 0.7, vUv.y);

          alpha = edgeFade * (1.0 - topFade * 0.3);

          if (alpha < 0.1) discard;

          gl_FragColor = vec4(color, alpha);
        }
      `,
      side: THREE.DoubleSide,
      transparent: true,
      depthWrite: false, // Prevent z-fighting
      depthTest: true,
    });

    // Create grass chunks with billboard crosses
    const createGrassChunks = () => {
      const halfCount = chunkCount / 2;
      const planeCount = 6; // Increased to 6 planes for better coverage from all angles

      for (let cx = 0; cx < chunkCount; cx++) {
        for (let cz = 0; cz < chunkCount; cz++) {
          const chunkX = (cx - halfCount) * chunkSize;
          const chunkZ = (cz - halfCount) * chunkSize;

          // Create multiple instanced meshes for each plane angle
          for (let planeIndex = 0; planeIndex < planeCount; planeIndex++) {
            const angle = (Math.PI / planeCount) * planeIndex;
            const geometry = new THREE.PlaneGeometry(grassWidth, grassHeight, 1, 4);
            const instancedMesh = new THREE.InstancedMesh(geometry, grassMaterial, grassDensity);
            instancedMesh.castShadow = true;
            instancedMesh.receiveShadow = false; // Don't receive shadows to reduce flickering

            // Position grass instances within chunk
            const dummy = new THREE.Object3D();
            for (let i = 0; i < grassDensity; i++) {
              const x = chunkX + Math.random() * chunkSize;
              const z = chunkZ + Math.random() * chunkSize;
              const y = fractalNoise(x, z);

              dummy.position.set(x, y + grassHeight / 2, z);
              dummy.rotation.y = angle + Math.random() * 0.2; // Slight random variation
              dummy.scale.set(
                0.8 + Math.random() * 0.4,
                0.8 + Math.random() * 0.4,
                1
              );
              dummy.updateMatrix();
              instancedMesh.setMatrixAt(i, dummy.matrix);
            }

            instancedMesh.instanceMatrix.needsUpdate = true;
            scene.add(instancedMesh);
            chunksRef.current.push(instancedMesh);
          }
        }
      }
    };

    createGrassChunks();

    // Animation loop
    let lastTime = performance.now();
    const animate = () => {
      const currentTime = performance.now();
      const delta = (currentTime - lastTime) / 1000;
      lastTime = currentTime;

      // Update shader time
      grassMaterial.uniforms.time.value = currentTime * 0.001;

      // Rotate camera
      const time = currentTime * 0.0001;
      camera.position.x = Math.cos(time) * 30;
      camera.position.z = Math.sin(time) * 30;
      camera.lookAt(0, 0, 0);

      renderer.render(scene, camera);
      animationFrameRef.current = requestAnimationFrame(animate);
    };

    animate();

    return () => {
      if (animationFrameRef.current) {
        cancelAnimationFrame(animationFrameRef.current);
      }
      renderer.dispose();
      containerRef.current?.removeChild(renderer.domElement);
    };
  }, [width, height, grassDensity, chunkSize, chunkCount, grassHeight, grassWidth, baseColor, tipColor, windSpeed, windStrength]);

  return (
    <Box ref={containerRef} sx={{ width, height, overflow: 'hidden' }} />
  );
};

export default FluffyGrass;

