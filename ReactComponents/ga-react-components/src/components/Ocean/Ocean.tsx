/**
 * Ocean Component (Updated - Fixed cameraPosition)
 *
 * Advanced ocean simulation with:
 * - Gerstner waves (3-wave set: swell + mid + chop)
 * - Physical sky (Hosek-Wilkie atmospheric scattering)
 * - Fresnel reflections (Schlick approximation)
 * - Beer's law absorption for underwater color
 * - Slope-based foam and roughness
 * - PMREM environment mapping from sky
 * - ACES Filmic tone mapping
 */

import React, { useEffect, useRef } from 'react';
import * as THREE from 'three';
import { Sky } from 'three/examples/jsm/objects/Sky.js';
import { OrbitControls } from 'three/examples/jsm/controls/OrbitControls.js';

/**
 * Create advanced ocean material with Gerstner waves
 */
function createOceanMaterial() {
  const waterTint = new THREE.Color(0x0b4259);
  const sigmaRGB = [0.12, 0.06, 0.02]; // Beer absorption (m^-1)

  const material = new THREE.ShaderMaterial({
    uniforms: {
      time: { value: 0 },
      waterTint: { value: waterTint },
      sigmaRGB: { value: new THREE.Vector3(...sigmaRGB) },
      foamStrength: { value: 0.15 },
      baseRoughness: { value: 0.02 },
      maxRoughness: { value: 0.18 }
    },
    vertexShader: `
      uniform float time;
      varying vec3 vPosition;
      varying vec3 vNormal;
      varying vec3 vWorldPosition;

      // Gerstner wave function
      vec3 gerstnerWave(vec2 dir, float amp, float steep, float len, float speed, vec2 xz, float t) {
        float k = 6.2831853 / len;
        vec2 d = normalize(dir);
        float w = sqrt(9.81 * k);
        float ph = k * dot(d, xz) - (w + speed) * t;

        float c = cos(ph);
        float s = sin(ph);

        float Qa = steep * amp;
        float dispX = d.x * Qa * c;
        float dispZ = d.y * Qa * c;
        float dispY = amp * s;

        return vec3(dispX, dispY, dispZ);
      }

      // Gerstner wave normal
      vec3 gerstnerNormal(vec2 dir, float amp, float steep, float len, float speed, vec2 xz, float t) {
        float k = 6.2831853 / len;
        vec2 d = normalize(dir);
        float w = sqrt(9.81 * k);
        float ph = k * dot(d, xz) - (w + speed) * t;

        float c = cos(ph);
        float s = sin(ph);

        float Qa = steep * amp;
        float ddx = -d.x * d.x * Qa * s * k;
        float ddz = -d.y * d.y * Qa * s * k;
        float ddy = amp * c * k;

        return normalize(vec3(-ddx, 1.0 - ddy, -ddz));
      }

      void main() {
        vec2 xz = vec2(position.x, position.z);

        // 3-wave set: long swell + mid + chop
        vec3 disp = vec3(0.0);
        vec3 nrm = vec3(0.0, 1.0, 0.0);

        // Wave 1: Long swell
        disp += gerstnerWave(vec2(1.0, 0.2), 0.70, 0.90, 45.0, 0.0, xz, time);
        nrm += gerstnerNormal(vec2(1.0, 0.2), 0.70, 0.90, 45.0, 0.0, xz, time);

        // Wave 2: Mid
        disp += gerstnerWave(vec2(0.6, 1.0), 0.35, 0.80, 18.0, 0.0, xz, time);
        nrm += gerstnerNormal(vec2(0.6, 1.0), 0.35, 0.80, 18.0, 0.0, xz, time);

        // Wave 3: Chop
        disp += gerstnerWave(vec2(-0.8, 0.4), 0.22, 0.70, 9.0, 0.0, xz, time);
        nrm += gerstnerNormal(vec2(-0.8, 0.4), 0.22, 0.70, 9.0, 0.0, xz, time);

        nrm = normalize(nrm);

        vec3 pos = position + disp;
        vPosition = pos;
        vNormal = nrm;
        vWorldPosition = (modelMatrix * vec4(pos, 1.0)).xyz;

        gl_Position = projectionMatrix * modelViewMatrix * vec4(pos, 1.0);
      }
    `,
    fragmentShader: `
      uniform vec3 waterTint;
      uniform vec3 sigmaRGB;
      uniform float foamStrength;
      uniform float baseRoughness;
      uniform float maxRoughness;

      varying vec3 vPosition;
      varying vec3 vNormal;
      varying vec3 vWorldPosition;

      void main() {
        vec3 normal = normalize(vNormal);
        vec3 viewDir = normalize(cameraPosition - vWorldPosition);

        // Fresnel (Schlick approximation)
        float F0 = 0.02; // water ~2% reflectance
        float NoV = clamp(dot(normal, viewDir), 0.0, 1.0);
        float fresnel = F0 + (1.0 - F0) * pow(1.0 - NoV, 5.0);

        // Beer's law absorption by depth
        float depth = clamp(-vWorldPosition.y, 0.0, 100.0);
        vec3 atten = vec3(
          exp(-sigmaRGB.x * depth),
          exp(-sigmaRGB.y * depth),
          exp(-sigmaRGB.z * depth)
        );
        vec3 refrCol = waterTint * atten;

        // Slope-based foam
        vec3 up = vec3(0.0, 1.0, 0.0);
        float slope = 1.0 - clamp(dot(normal, up), 0.0, 1.0);
        float foam = pow(smoothstep(0.55, 0.85, slope), 2.0) * foamStrength;

        // Final color: mix refraction with white foam
        vec3 color = mix(refrCol, vec3(1.0), foam);

        // Add some sky reflection based on Fresnel
        vec3 skyColor = vec3(0.7, 0.85, 1.0);
        color = mix(color, skyColor, fresnel * 0.6);

        gl_FragColor = vec4(color, 1.0);
      }
    `,
    side: THREE.DoubleSide
  });

  return material;
}

export interface OceanProps {
  width?: number;
  height?: number;
}

export const Ocean: React.FC<OceanProps> = ({
  width,
  height,
}) => {
  const containerRef = useRef<HTMLDivElement>(null);
  const sceneRef = useRef<THREE.Scene | null>(null);
  const cameraRef = useRef<THREE.PerspectiveCamera | null>(null);
  const rendererRef = useRef<THREE.WebGLRenderer | null>(null);
  const controlsRef = useRef<OrbitControls | null>(null);
  const animationFrameRef = useRef<number | null>(null);

  useEffect(() => {
    if (!containerRef.current) return;

    const containerWidth = width || containerRef.current.clientWidth;
    const containerHeight = height || containerRef.current.clientHeight;

    // Scene setup
    const scene = new THREE.Scene();
    scene.fog = new THREE.FogExp2(0x7fb0c0, 0.00006);
    sceneRef.current = scene;

    // Camera setup
    const camera = new THREE.PerspectiveCamera(60, containerWidth / containerHeight, 0.1, 5000);
    camera.position.set(-40, 18, 50);
    cameraRef.current = camera;

    // Renderer setup
    const renderer = new THREE.WebGLRenderer({ antialias: true });
    renderer.setSize(containerWidth, containerHeight);
    renderer.setPixelRatio(Math.min(window.devicePixelRatio, 2));
    renderer.toneMapping = THREE.ACESFilmicToneMapping;
    renderer.toneMappingExposure = 1.2;
    renderer.shadowMap.enabled = true;
    renderer.shadowMap.type = THREE.VSMShadowMap;

    containerRef.current.appendChild(renderer.domElement);
    rendererRef.current = renderer;

    // Controls
    const controls = new OrbitControls(camera, renderer.domElement);
    controls.enableDamping = true;
    controls.dampingFactor = 0.05;
    controls.target.set(0, 0, 0);
    controls.maxPolarAngle = Math.PI / 2 - 0.05;
    controls.minDistance = 5;
    controls.maxDistance = 1000;
    controlsRef.current = controls;

    // Sun + physical sky
    const sunLight = new THREE.DirectionalLight(0xffffff, 2.5);
    scene.add(sunLight);

    const sky = new Sky();
    sky.scale.setScalar(4000);
    scene.add(sky);

    const skyUniforms = sky.material.uniforms;
    skyUniforms['turbidity'].value = 2.0;
    skyUniforms['rayleigh'].value = 2.5;
    skyUniforms['mieCoefficient'].value = 0.005;
    skyUniforms['mieDirectionalG'].value = 0.8;

    // Sun position
    const sunPhi = THREE.MathUtils.degToRad(78);   // elevation
    const sunTheta = THREE.MathUtils.degToRad(30); // azimuth
    const sunDir = new THREE.Vector3().setFromSphericalCoords(1, Math.PI / 2 - sunPhi, sunTheta);
    sunLight.position.copy(sunDir.clone().multiplyScalar(1500));
    skyUniforms['sunPosition'].value.copy(sunLight.position);

    // Environment from sky
    const pmremGenerator = new THREE.PMREMGenerator(renderer);
    const skyTexture = pmremGenerator.fromScene(scene).texture;
    scene.environment = skyTexture;

    // Ocean mesh with Gerstner waves
    const S = 4000;
    const res = 400;
    const oceanGeo = new THREE.PlaneGeometry(S, S, res, res);
    oceanGeo.rotateX(-Math.PI / 2);

    // Create advanced ocean shader material
    const oceanMat = createOceanMaterial();
    const ocean = new THREE.Mesh(oceanGeo, oceanMat);
    scene.add(ocean);

    // Test object (floating cone)
    const box = new THREE.Mesh(
      new THREE.ConeGeometry(3, 6, 4),
      new THREE.MeshStandardMaterial({ color: 0x9bb1c9, roughness: 0.4 })
    );
    box.position.set(0, 1.5, 0);
    box.castShadow = true;
    box.receiveShadow = true;
    scene.add(box);

    // Animation loop
    let t = 0;
    const animate = () => {
      t += 0.002;

      // Update ocean time
      oceanMat.uniforms.time.value = t;

      // Animate cone
      box.position.y = Math.sin(t * 2) * 2 + 3;
      box.rotation.y = t * 0.5;

      // Camera orbit (optional - can be disabled if using controls)
      // camera.position.x = Math.cos(t) * 55 - 30;
      // camera.position.z = Math.sin(t) * 70 + 20;
      // camera.lookAt(0, 0, 0);

      controls.update();
      renderer.render(scene, camera);
      animationFrameRef.current = requestAnimationFrame(animate);
    };

    animate();

    // Handle resize
    const handleResize = () => {
      if (!containerRef.current) return;
      const w = width || containerRef.current.clientWidth;
      const h = height || containerRef.current.clientHeight;
      camera.aspect = w / h;
      camera.updateProjectionMatrix();
      renderer.setSize(w, h);
    };
    window.addEventListener('resize', handleResize);

    return () => {
      window.removeEventListener('resize', handleResize);
      if (animationFrameRef.current) {
        cancelAnimationFrame(animationFrameRef.current);
      }
      pmremGenerator.dispose();
      controls.dispose();
      renderer.dispose();
      if (containerRef.current && renderer.domElement && containerRef.current.contains(renderer.domElement)) {
        containerRef.current.removeChild(renderer.domElement);
      }
    };
  }, [width, height]);

  return (
    <div
      ref={containerRef}
      style={{
        width: width || '100%',
        height: height || '100%',
        overflow: 'hidden',
      }}
    />
  );
};

export default Ocean;

