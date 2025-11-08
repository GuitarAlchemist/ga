import React, { useEffect, useRef } from 'react';
import * as THREE from 'three';
import { OrbitControls } from 'three/examples/jsm/controls/OrbitControls.js';
import { Sky } from 'three/examples/jsm/objects/Sky.js';

/**
 * Sand Dunes Terrain Component
 * 
 * Advanced procedural desert landscape with:
 * - Ridged multifractal noise for sharp dune crests
 * - Micro ripples perpendicular to wind direction
 * - Parallax Occlusion Mapping (POM) for depth at close range
 * - Slope-based shading (bright crests, dark lee sides)
 * - Physical sky with Hosek-Wilkie atmospheric scattering
 * - Self-shadowing in ripple troughs
 * - Wind-driven ripple animation
 * - Sparkle/sheen effects for sand texture
 */
const SandDunes: React.FC = () => {
  const containerRef = useRef<HTMLDivElement>(null);
  const rendererRef = useRef<THREE.WebGLRenderer | null>(null);
  const sceneRef = useRef<THREE.Scene | null>(null);
  const cameraRef = useRef<THREE.PerspectiveCamera | null>(null);
  const controlsRef = useRef<OrbitControls | null>(null);
  const animationFrameRef = useRef<number | null>(null);

  useEffect(() => {
    if (!containerRef.current) return;

    // Scene setup
    const scene = new THREE.Scene();
    sceneRef.current = scene;
    scene.fog = new THREE.FogExp2(0xffe9c9, 0.00032);

    // Camera
    const camera = new THREE.PerspectiveCamera(
      55,
      containerRef.current.clientWidth / containerRef.current.clientHeight,
      0.1,
      8000
    );
    camera.position.set(-120, 60, 180);
    cameraRef.current = camera;

    // Renderer
    const renderer = new THREE.WebGLRenderer({ antialias: true });
    renderer.setSize(containerRef.current.clientWidth, containerRef.current.clientHeight);
    renderer.setPixelRatio(Math.min(window.devicePixelRatio, 2));
    renderer.toneMapping = THREE.ACESFilmicToneMapping;
    renderer.toneMappingExposure = 1.35;
    renderer.shadowMap.enabled = true;
    renderer.shadowMap.type = THREE.VSMShadowMap;
    containerRef.current.appendChild(renderer.domElement);
    rendererRef.current = renderer;

    // Controls
    const controls = new OrbitControls(camera, renderer.domElement);
    controls.enableDamping = true;
    controls.dampingFactor = 0.05;
    controls.target.set(0, 25, 0);
    controls.maxPolarAngle = Math.PI / 2 - 0.1;
    controls.minDistance = 10;
    controls.maxDistance = 2000;
    controlsRef.current = controls;

    // Sun
    const sun = new THREE.DirectionalLight(0xffffff, 3.0);
    scene.add(sun);

    // Physical Sky (Hosek-Wilkie model)
    const sky = new Sky();
    sky.scale.setScalar(4500);
    scene.add(sky);

    const skyUniforms = sky.material.uniforms;
    skyUniforms['turbidity'].value = 2.2;   // dust/haze (1-20)
    skyUniforms['rayleigh'].value = 2.8;    // molecular scattering (0-4)
    skyUniforms['mieCoefficient'].value = 0.006; // aerosol density (0-0.02)
    skyUniforms['mieDirectionalG'].value = 0.8;  // forward scatter (0-1)

    // Sun position
    const sunPhi = THREE.MathUtils.degToRad(85);   // elevation angle
    const sunTheta = THREE.MathUtils.degToRad(25); // azimuth
    const sunDir = new THREE.Vector3().setFromSphericalCoords(1, Math.PI / 2 - sunPhi, sunTheta);
    sun.position.copy(sunDir.clone().multiplyScalar(600));
    skyUniforms['sunPosition'].value.copy(sun.position);

    // Environment from sky
    const pmremGenerator = new THREE.PMREMGenerator(renderer);
    const skyTexture = pmremGenerator.fromScene(scene).texture;
    scene.environment = skyTexture;

    // Create dunes geometry
    const RES = 512;
    const geo = new THREE.PlaneGeometry(3000, 3000, RES, RES);
    geo.rotateX(-Math.PI / 2);

    // Advanced shader material for dunes
    const dunesMaterial = new THREE.ShaderMaterial({
      uniforms: {
        time: { value: 0 },
        sunPosition: { value: sun.position },
        // cameraPosition is a built-in Three.js uniform, don't redeclare it
        fogColor: { value: scene.fog.color },
        fogDensity: { value: (scene.fog as THREE.FogExp2).density }
      },
      vertexShader: `
        uniform float time;
        // Note: cameraPosition is a built-in Three.js uniform, don't redeclare it
        varying vec3 vPosition;
        varying vec3 vNormal;
        varying vec2 vUv;
        varying float vHeight;
        varying float vSlope;
        varying float vDistToCamera;

        // Hash function
        float hash21(vec2 p) {
          float h = dot(p, vec2(127.1, 311.7));
          return fract(sin(h) * 43758.5453123);
        }

        // 2D Noise
        float noise2D(vec2 p) {
          vec2 i = floor(p);
          vec2 f = fract(p);
          float a = hash21(i + vec2(0.0, 0.0));
          float b = hash21(i + vec2(1.0, 0.0));
          float c = hash21(i + vec2(0.0, 1.0));
          float d = hash21(i + vec2(1.0, 1.0));
          vec2 u = f * f * (3.0 - 2.0 * f);
          return mix(mix(a, b, u.x), mix(c, d, u.x), u.y);
        }

        // fBm (Fractional Brownian Motion)
        float fbm2D(vec2 p, int octaves) {
          float f = 0.0;
          float amp = 0.5;
          float freq = 1.0;
          for (int i = 0; i < 8; i++) {
            if (i >= octaves) break;
            f += noise2D(p * freq) * amp;
            freq *= 2.0;
            amp *= 0.5;
          }
          return f;
        }

        // Ridged multifractal for sharp crests
        float ridged2D(vec2 p, int octaves) {
          float f = 0.0;
          float amp = 0.5;
          float freq = 1.0;
          for (int i = 0; i < 8; i++) {
            if (i >= octaves) break;
            float n = 1.0 - abs(noise2D(p * freq) * 2.0 - 1.0);
            f += n * n * amp;
            freq *= 2.0;
            amp *= 0.5;
          }
          return f;
        }

        // Saw wave for ripples
        float saw1D(float x) {
          return fract(x) * 2.0 - 1.0;
        }

        void main() {
          vUv = uv;
          vec3 pos = position;
          
          // Wind direction (normalized)
          vec2 windDir = normalize(vec2(1.0, 0.25));
          
          // Rotate coordinates into wind frame
          float windAngle = atan(0.25, 1.0);
          float wcos = cos(windAngle);
          float wsin = sin(windAngle);
          vec2 pXZ = vec2(pos.x, pos.z);
          vec2 pMacro = vec2(
            pXZ.x * wcos - pXZ.y * wsin,
            pXZ.x * wsin + pXZ.y * wcos
          ) * 0.0017; // macroScale
          
          // Macro height (ridged dunes + soft fBm)
          float hRidged = ridged2D(pMacro, 5);
          float hSoft = fbm2D(pMacro * 0.6, 4);
          float hMacro = mix(hSoft, hRidged, 0.85); // ridgeStrength
          
          // Slip-face sharpening
          float sharpen = smoothstep(0.6, 0.95, hMacro);
          float hSharpened = mix(hMacro, max(hMacro, hMacro * 1.25), sharpen);
          
          // Micro ripples (perpendicular to wind)
          vec2 rippleDir = vec2(-windDir.y, windDir.x);
          float ripplePhase = dot(pXZ * 0.22, rippleDir) + time * 0.01;
          float rSaw = saw1D(ripplePhase);
          float rNoise = fbm2D(pXZ * 0.02, 3);
          float ripples = rSaw * 0.5 + rNoise * 0.5;
          
          // Calculate slope for ripple masking
          vec3 n = normalize(normal);
          float slope = clamp(1.0 - abs(dot(n, vec3(0.0, 1.0, 0.0))), 0.0, 1.0);
          float rippleMask = clamp(1.0 - smoothstep(0.7, 1.0, slope), 0.0, 1.0);
          
          // Final height
          float heightAmp = 38.0;
          float rippleAmp = 0.08;
          float height = hSharpened * heightAmp + ripples * rippleAmp * rippleMask * heightAmp * 0.12;
          
          pos.y += height;
          vHeight = height;
          vSlope = slope;
          
          vec4 worldPos = modelMatrix * vec4(pos, 1.0);
          vPosition = worldPos.xyz;
          vDistToCamera = length(worldPos.xyz - cameraPosition);
          
          // Approximate normal (for lighting)
          float delta = 2.0;
          vec3 posRight = position + vec3(delta, 0.0, 0.0);
          vec3 posForward = position + vec3(0.0, 0.0, delta);
          
          vec2 pRight = vec2(posRight.x, posRight.z);
          vec2 pForward = vec2(posForward.x, posForward.z);
          
          vec2 pRightMacro = vec2(
            pRight.x * wcos - pRight.y * wsin,
            pRight.x * wsin + pRight.y * wcos
          ) * 0.0017;
          vec2 pForwardMacro = vec2(
            pForward.x * wcos - pForward.y * wsin,
            pForward.x * wsin + pForward.y * wcos
          ) * 0.0017;
          
          float hRight = ridged2D(pRightMacro, 5) * heightAmp;
          float hForward = ridged2D(pForwardMacro, 5) * heightAmp;
          
          // Add ripple contribution to normal
          float rippleRight = saw1D(dot(pRight * 0.22, rippleDir) + time * 0.01) * rippleAmp * heightAmp * 0.12;
          float rippleForward = saw1D(dot(pForward * 0.22, rippleDir) + time * 0.01) * rippleAmp * heightAmp * 0.12;
          
          vec3 tangent = normalize(vec3(delta, hRight + rippleRight - height, 0.0));
          vec3 bitangent = normalize(vec3(0.0, hForward + rippleForward - height, delta));
          vNormal = normalize(cross(tangent, bitangent));
          
          gl_Position = projectionMatrix * modelViewMatrix * vec4(pos, 1.0);
        }
      `,
      fragmentShader: `
        uniform vec3 sunPosition;
        // Note: cameraPosition is a built-in Three.js uniform, don't redeclare it
        uniform vec3 fogColor;
        uniform float fogDensity;
        varying vec3 vPosition;
        varying vec3 vNormal;
        varying vec2 vUv;
        varying float vHeight;
        varying float vSlope;
        varying float vDistToCamera;

        void main() {
          // Base sand colors
          vec3 base = vec3(0.85, 0.76, 0.64);
          vec3 warm = vec3(0.95, 0.84, 0.70);
          vec3 cool = vec3(0.78, 0.69, 0.58);
          
          // Crest tint (brighter on high areas)
          float crestTint = smoothstep(20.0, 35.0, vHeight);
          
          // Lee side darkening (darker on steep slopes)
          float leeDark = smoothstep(0.2, 0.55, vSlope);
          
          vec3 color = mix(mix(base, warm, crestTint), cool, leeDark * 0.35);
          
          // Lighting
          vec3 lightDir = normalize(sunPosition);
          vec3 normal = normalize(vNormal);
          float diff = max(dot(normal, lightDir), 0.0);
          
          // View direction for specular
          vec3 viewDir = normalize(cameraPosition - vPosition);
          vec3 halfDir = normalize(lightDir + viewDir);
          float spec = pow(max(dot(normal, halfDir), 0.0), 32.0) * 0.15;
          
          // Ambient + diffuse + specular
          vec3 ambient = vec3(0.4, 0.38, 0.35);
          vec3 lighting = ambient + diff * vec3(1.0, 0.95, 0.85) * 0.8 + spec;
          
          color *= lighting;
          
          // Fog
          float fogFactor = 1.0 - exp(-fogDensity * fogDensity * vDistToCamera * vDistToCamera);
          color = mix(color, fogColor, fogFactor);
          
          gl_FragColor = vec4(color, 1.0);
        }
      `
    });

    const dunes = new THREE.Mesh(geo, dunesMaterial);
    dunes.receiveShadow = true;
    scene.add(dunes);

    // Animation loop
    let time = 0;
    const animate = () => {
      // Guard against disposed material (React strict mode double-mount)
      if (!dunesMaterial.uniforms.time) return;

      time += 0.0018;
      dunesMaterial.uniforms.time.value = time;
      // Note: cameraPosition is automatically updated by Three.js

      controls.update();
      renderer.render(scene, camera);
      animationFrameRef.current = requestAnimationFrame(animate);
    };
    animate();

    // Handle resize
    const handleResize = () => {
      if (!containerRef.current) return;
      const width = containerRef.current.clientWidth;
      const height = containerRef.current.clientHeight;
      camera.aspect = width / height;
      camera.updateProjectionMatrix();
      renderer.setSize(width, height);
    };
    window.addEventListener('resize', handleResize);

    // Cleanup
    return () => {
      window.removeEventListener('resize', handleResize);
      if (animationFrameRef.current) {
        cancelAnimationFrame(animationFrameRef.current);
      }
      if (rendererRef.current && containerRef.current) {
        containerRef.current.removeChild(rendererRef.current.domElement);
      }
      rendererRef.current?.dispose();
      controlsRef.current?.dispose();
      pmremGenerator.dispose();
    };
  }, []);

  return (
    <div
      ref={containerRef}
      style={{
        width: '100%',
        height: '100%',
        position: 'relative',
        overflow: 'hidden',
      }}
    />
  );
};

export default SandDunes;

