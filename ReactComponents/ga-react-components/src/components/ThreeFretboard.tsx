// @ts-nocheck
import React, { useEffect, useRef, useState } from 'react';
import { Box, Typography, Stack, FormControl, InputLabel, Select, MenuItem, FormControlLabel, Switch, Tooltip, IconButton } from '@mui/material';
import FullscreenIcon from '@mui/icons-material/Fullscreen';
import FullscreenExitIcon from '@mui/icons-material/FullscreenExit';
import * as THREE from 'three';
import { OrbitControls } from 'three/examples/jsm/controls/OrbitControls.js';
import { RGBELoader } from 'three/examples/jsm/loaders/RGBELoader.js';
import { GuitarModelStyle, getGuitarModel, getAllModels, GUITAR_CATEGORIES } from './GuitarModels';
import { loadCapoModel, createFallbackCapo } from '../utils/capoModelLoader';

// Import WebGPU renderer - available in Three.js r163+
import { WebGPURenderer } from 'three/webgpu';

export interface ThreeFretboardPosition {
  string: number;
  fret: number;
  label?: string;
  color?: string;
  emphasized?: boolean;
}

export interface ThreeFretboardConfig {
  fretCount?: number;
  stringCount?: number;
  tuning?: string[];
  showFretNumbers?: boolean;
  showStringLabels?: boolean;
  width?: number;
  height?: number;
  guitarModel?: string;
  capoFret?: number;
  leftHanded?: boolean;
  enableOrbitControls?: boolean; // Allow user to rotate/zoom the 3D view
}

interface ThreeFretboardProps {
  title?: string;
  positions?: ThreeFretboardPosition[];
  config?: ThreeFretboardConfig;
  onPositionClick?: (string: number, fret: number) => void;
}

const DEFAULT_CONFIG: Required<ThreeFretboardConfig> = {
  fretCount: 22,
  stringCount: 6,
  tuning: ['E', 'B', 'G', 'D', 'A', 'E'],
  showFretNumbers: true,
  showStringLabels: true,
  width: 1800,
  height: 600,
  guitarModel: 'electric_fender_strat',
  capoFret: 0,
  leftHanded: false,
  enableOrbitControls: true,
};

export const ThreeFretboard: React.FC<ThreeFretboardProps> = ({
  title = '3D Fretboard (Three.js + WebGPU)',
  positions = [],
  config = {},
  onPositionClick,
}) => {
  const canvasRef = useRef<HTMLCanvasElement>(null);
  const rendererRef = useRef<WebGPURenderer | THREE.WebGLRenderer | null>(null);
  const sceneRef = useRef<THREE.Scene | null>(null);
  const cameraRef = useRef<THREE.PerspectiveCamera | null>(null);
  const controlsRef = useRef<OrbitControls | null>(null);
  const fretboardGroupRef = useRef<THREE.Group | null>(null);
  // const composerRef = useRef<EffectComposer | null>(null);

  const [selectedModel, setSelectedModel] = useState(config.guitarModel || DEFAULT_CONFIG.guitarModel);
  const [capoFret, setCapoFret] = useState(config.capoFret || DEFAULT_CONFIG.capoFret);
  const [isLeftHanded, setIsLeftHanded] = useState(config.leftHanded || DEFAULT_CONFIG.leftHanded);
  const [isWebGPU, setIsWebGPU] = useState(false);
  const [isInitialized, setIsInitialized] = useState(false);
  const [isTransitioning, setIsTransitioning] = useState(false);
  const [needsUpdate, setNeedsUpdate] = useState(0);
  const [isFullscreen, setIsFullscreen] = useState(false);

  const containerRef = useRef<HTMLDivElement>(null);

  const fretboardConfig = { ...DEFAULT_CONFIG, ...config, guitarModel: selectedModel, capoFret, leftHanded: isLeftHanded };
  const {
    fretCount,
    stringCount,
    tuning,
    showFretNumbers,
    showStringLabels,
    width,
    height,
    guitarModel,
    leftHanded,
    enableOrbitControls,
  } = fretboardConfig;

  const guitarStyle = getGuitarModel(guitarModel);

  // Debug logging
  console.log('ThreeFretboard render:', { selectedModel, guitarModel, category: guitarStyle.category, brand: guitarStyle.brand, model: guitarStyle.model });

  // Fullscreen handlers
  const toggleFullscreen = async () => {
    if (!containerRef.current) return;

    try {
      if (!isFullscreen) {
        // Enter fullscreen
        if (containerRef.current.requestFullscreen) {
          await containerRef.current.requestFullscreen();
        }
      } else {
        // Exit fullscreen
        if (document.exitFullscreen) {
          await document.exitFullscreen();
        }
      }
    } catch (error) {
      console.error('Fullscreen error:', error);
    }
  };

  // Listen for fullscreen changes
  useEffect(() => {
    const handleFullscreenChange = () => {
      setIsFullscreen(!!document.fullscreenElement);
    };

    document.addEventListener('fullscreenchange', handleFullscreenChange);
    return () => {
      document.removeEventListener('fullscreenchange', handleFullscreenChange);
    };
  }, []);

  useEffect(() => {
    if (!canvasRef.current) return;

    // Track if this effect is still active
    let isMounted = true;
    let renderer: WebGPURenderer | THREE.WebGLRenderer | null = null;

    const initThree = async () => {
      // Check if component is still mounted
      if (!isMounted) return;

      const canvas = canvasRef.current;
      if (!canvas) return;

      // Reuse existing renderer if available (important for WebGPU!)
      if (rendererRef.current) {
        renderer = rendererRef.current;
        console.log('♻️ ThreeFretboard: Reusing existing renderer');
      } else {
        // Check for WebGPU support
        const hasWebGPU = 'gpu' in navigator;

        // Try WebGPU first if available
        if (hasWebGPU) {
          try {
            renderer = new WebGPURenderer({
              canvas,
              antialias: true,
              alpha: true,
              forceWebGL: false,
              // Maximum antialiasing settings for smooth edges
              samples: 8, // 8x MSAA for WebGPU (maximum quality)
            });

            // Check if still mounted before initializing
            if (!isMounted) {
              renderer.dispose();
              return;
            }

            await renderer.init();

            // Check again after async init
            if (!isMounted) {
              renderer.dispose();
              return;
            }

            console.log('✅ ThreeFretboard: Using WebGPU renderer with 8x MSAA');
            setIsWebGPU(true);
          } catch (error) {
            console.warn('ThreeFretboard: WebGPU initialization failed, falling back to WebGL:', error);
            renderer = null;
          }
        }

        // Fallback to WebGL if WebGPU failed or not available
        if (!renderer) {
          renderer = new THREE.WebGLRenderer({
            canvas,
            antialias: true,
            alpha: true,
          });
          console.log('✅ ThreeFretboard: Using WebGL renderer');
          setIsWebGPU(false);
        }
      }

      setIsWebGPU(true);
      renderer.setSize(width, height);
      // Maximum pixel ratio for crisp rendering and smooth edges
      renderer.setPixelRatio(Math.min(window.devicePixelRatio * 3, 6));
      renderer.setClearColor(0x2a2a2a, 1); // Darker gray background

      // Enable VSM shadows for WebGL renderer
      if (!isWebGPU && renderer instanceof THREE.WebGLRenderer) {
        renderer.shadowMap.enabled = true;
        renderer.shadowMap.type = THREE.VSMShadowMap;
      }

      rendererRef.current = renderer;

      // Create scene
      const scene = new THREE.Scene();
      scene.background = new THREE.Color(0x2a2a2a); // Darker gray background
      sceneRef.current = scene;

      // Create camera - narrower FOV for more realistic perspective
      // Lower FOV reduces distortion and gives better depth perception
      const camera = new THREE.PerspectiveCamera(35, width / height, 0.1, 1000);
      camera.position.set(-12, 18, 40);
      camera.lookAt(0, 0, 0);
      cameraRef.current = camera;

      // Add orbit controls if enabled
      if (enableOrbitControls) {
        const controls = new OrbitControls(camera, canvas);
        controls.enableDamping = true;
        controls.dampingFactor = 0.05;
        controls.target.set(0, 0, 0);
        controls.update();
        controlsRef.current = controls;
      }

      // Configure tone mapping for realistic metallic rendering
      renderer.toneMapping = THREE.ACESFilmicToneMapping;
      renderer.toneMappingExposure = 1.2;

      // Add environment map for realistic metallic reflections on strings
      // Create a studio-quality gradient environment with wider specular range
      // Note: PMREMGenerator uses ShaderMaterial internally which causes warnings in WebGPU
      // We skip PMREM for WebGPU to avoid console warnings

      // Create lightweight environment map (skybox) for metallic reflections
      // Using 512×256 for optimal performance/quality balance
      const envCanvas = document.createElement('canvas');
      envCanvas.width = 512;
      envCanvas.height = 256;
      const envCtx = envCanvas.getContext('2d')!;

      // Studio gradient (dark floor to bright ceiling)
      const gradient = envCtx.createLinearGradient(0, 256, 0, 0);
      gradient.addColorStop(0, '#0a0a0a');
      gradient.addColorStop(0.4, '#3a3a3a');
      gradient.addColorStop(0.7, '#8a8a8a');
      gradient.addColorStop(1, '#e0e0e0');

      envCtx.fillStyle = gradient;
      envCtx.fillRect(0, 0, 512, 256);

      const envTexture = new THREE.CanvasTexture(envCanvas);
      envTexture.mapping = THREE.EquirectangularReflectionMapping;
      envTexture.colorSpace = THREE.SRGBColorSpace;

      // Apply environment map directly (no PMREM for performance)
      scene.environment = envTexture;

      // Add studio-quality lighting optimized for metallic wound strings and frets
      const ambientLight = new THREE.AmbientLight(0xffffff, 0.5); // Reduced for better contrast
      scene.add(ambientLight);

      // Main directional light from above-right
      const directionalLight = new THREE.DirectionalLight(0xffffff, 1.3);
      directionalLight.position.set(15, 30, 15);
      directionalLight.castShadow = true;
      directionalLight.shadow.camera.near = 0.1;
      directionalLight.shadow.camera.far = 100;
      directionalLight.shadow.camera.left = -30;
      directionalLight.shadow.camera.right = 30;
      directionalLight.shadow.camera.top = 30;
      directionalLight.shadow.camera.bottom = -30;
      directionalLight.shadow.mapSize.width = 1024;
      directionalLight.shadow.mapSize.height = 1024;
      directionalLight.shadow.radius = 4;
      directionalLight.shadow.bias = -0.0005;
      scene.add(directionalLight);

      // Warm rim light - creates golden highlights on wound strings
      // Low angle grazing across strings reveals winding texture
      const rimWarm = new THREE.DirectionalLight(0xffe0b0, 1.0);
      rimWarm.position.set(-12, 3, 8); // Low angle from left
      scene.add(rimWarm);

      // Cool rim light - creates bluish highlights on opposite side
      // Typical of studio two-light setup for metallic objects
      const rimCool = new THREE.DirectionalLight(0xa0b0ff, 0.6);
      rimCool.position.set(12, 4, -8); // Low angle from right
      scene.add(rimCool);

      // Front light - critical for making frets shine against dark wood
      // Positioned to catch the polished metal surface of frets
      const frontLight = new THREE.DirectionalLight(0xffffff, 0.8);
      frontLight.position.set(0, 10, 10); // From front, slightly above
      scene.add(frontLight);

      // Fill light from above - neutral gray
      const fillLight = new THREE.DirectionalLight(0x808080, 0.4);
      fillLight.position.set(0, 15, -5);
      scene.add(fillLight);

      // Point light near fretboard for local illumination and metallic reflections
      const pointLight = new THREE.PointLight(0xffffff, 0.6, 20);
      pointLight.position.set(0, 3, 0);
      scene.add(pointLight);

      // WebGPU has built-in MSAA, no need for post-processing

      // Create fretboard group for animations
      const fretboardGroup = new THREE.Group();
      scene.add(fretboardGroup);
      fretboardGroupRef.current = fretboardGroup;

      // Create fretboard geometry and get string meshes for animation
      const stringMeshes = createFretboard(fretboardGroup, guitarStyle, fretCount, stringCount, leftHanded, capoFret, isWebGPU);

      // Add position markers
      if (positions.length > 0) {
        const scaleLength = (guitarStyle.scaleLength || 650) / 10;
        const nutWidth = (guitarStyle.nutWidth || 43) / 10;
        addPositionMarkers(fretboardGroup, positions, fretCount, stringCount, scaleLength, nutWidth);
      }

      // Fade in animation
      fretboardGroup.scale.set(0.8, 0.8, 0.8);
      const startTime = Date.now();
      const fadeInDuration = 500; // ms

      const fadeIn = () => {
        const elapsed = Date.now() - startTime;
        const progress = Math.min(elapsed / fadeInDuration, 1);
        const scale = 0.8 + (0.2 * progress);
        fretboardGroup.scale.set(scale, scale, scale);

        if (progress < 1) {
          requestAnimationFrame(fadeIn);
        }
      };
      fadeIn();

      // Animation loop with subtle string vibration
      const animate = () => {
        // Check if component is still mounted and renderer is valid
        if (!isMounted || !renderer) return;

        if (controlsRef.current) {
          controlsRef.current.update();
        }

        // Animate strings with subtle "breathing" motion
        // Simulates natural micro-vibrations of metallic strings
        const t = performance.now() * 0.002; // Slow time scale
        for (let i = 0; i < stringMeshes.length; i++) {
          const offset = Math.sin(t + i) * 0.02; // Very small amplitude
          stringMeshes[i].rotation.y = offset * 0.05; // Subtle rotation
        }

        try {
          renderer.render(scene, camera);
        } catch (error) {
          // Silently handle render errors during cleanup
          console.warn('ThreeFretboard: Render error (likely during cleanup):', error);
        }
      };

      renderer.setAnimationLoop(animate);
      setIsInitialized(true);
    };

    initThree();

    return () => {
      // Mark as unmounted to prevent async operations from completing
      isMounted = false;

      // Cleanup: Dispose of all geometries and materials to prevent memory leaks and flickering
      if (sceneRef.current) {
        sceneRef.current.traverse((object) => {
          if (object instanceof THREE.Mesh) {
            if (object.geometry) {
              object.geometry.dispose();
            }
            if (object.material) {
              if (Array.isArray(object.material)) {
                object.material.forEach(material => {
                  // Dispose textures in materials
                  if ('map' in material && material.map) material.map.dispose();
                  if ('bumpMap' in material && material.bumpMap) material.bumpMap.dispose();
                  if ('normalMap' in material && material.normalMap) material.normalMap.dispose();
                  material.dispose();
                });
              } else {
                // Dispose textures in material
                if ('map' in object.material && object.material.map) object.material.map.dispose();
                if ('bumpMap' in object.material && object.material.bumpMap) object.material.bumpMap.dispose();
                if ('normalMap' in object.material && object.material.normalMap) object.material.normalMap.dispose();
                object.material.dispose();
              }
            }
          }
        });
        sceneRef.current.clear();
      }
      // Stop animation loop but DON'T dispose renderer (reuse it for better WebGPU compatibility)
      if (rendererRef.current) {
        rendererRef.current.setAnimationLoop(null);
      }
      if (controlsRef.current) {
        controlsRef.current.dispose();
        controlsRef.current = null;
      }
    };
  }, [fretCount, stringCount, tuning, showFretNumbers, showStringLabels, width, height, positions, guitarModel, selectedModel, capoFret, leftHanded, enableOrbitControls]);

  // Cleanup on component unmount
  useEffect(() => {
    return () => {
      // Dispose renderer only on unmount
      if (rendererRef.current) {
        rendererRef.current.setAnimationLoop(null);
        try {
          rendererRef.current.dispose();
          if ('forceContextLoss' in rendererRef.current) {
            (rendererRef.current as THREE.WebGLRenderer).forceContextLoss();
          }
        } catch (error) {
          console.warn('ThreeFretboard: Error during renderer disposal on unmount:', error);
        }
        rendererRef.current = null;
      }
    };
  }, []);

  return (
    <Stack spacing={2} ref={containerRef} sx={{
      bgcolor: isFullscreen ? '#1a1a1a' : 'transparent',
      p: isFullscreen ? 2 : 0,
      height: isFullscreen ? '100vh' : 'auto',
      width: isFullscreen ? '100vw' : 'auto',
    }}>
      {title && (
        <Box sx={{ display: 'flex', alignItems: 'center', gap: 2, justifyContent: 'space-between' }}>
          <Box sx={{ display: 'flex', alignItems: 'center', gap: 2 }}>
            <Typography variant="h6">{title}</Typography>
            {isInitialized && (
              <Typography variant="caption" sx={{
                px: 1.5,
                py: 0.5,
                borderRadius: 1,
                bgcolor: isWebGPU ? 'success.main' : 'warning.main',
                color: 'white',
                fontWeight: 'bold'
              }}>
                {isWebGPU ? '⚡ WebGPU' : '🔧 WebGL'}
              </Typography>
            )}
          </Box>

          {/* Fullscreen Button */}
          <Tooltip title={isFullscreen ? 'Exit Fullscreen' : 'Enter Fullscreen'}>
            <IconButton
              onClick={toggleFullscreen}
              sx={{
                color: 'primary.main',
                '&:hover': { bgcolor: 'rgba(255, 255, 255, 0.1)' }
              }}
            >
              {isFullscreen ? <FullscreenExitIcon /> : <FullscreenIcon />}
            </IconButton>
          </Tooltip>
        </Box>
      )}

      {/* Controls */}
      <Box sx={{ display: 'flex', gap: 2, alignItems: 'center', flexWrap: 'wrap' }}>
        {/* Guitar Type Selector - Simplified */}
        <FormControl sx={{ minWidth: 200 }}>
          <InputLabel id="guitar-type-label">Guitar Type</InputLabel>
          <Select
            labelId="guitar-type-label"
            id="guitar-type-select"
            value={guitarStyle.category}
            label="Guitar Type"
            onChange={(e) => {
              // Find first model of selected category
              const category = e.target.value as keyof typeof GUITAR_CATEGORIES;
              const firstModelId = GUITAR_CATEGORIES[category]?.[0];
              if (firstModelId) {
                setSelectedModel(firstModelId);
              }
            }}
          >
            <MenuItem value="classical">Classical</MenuItem>
            <MenuItem value="acoustic">Acoustic</MenuItem>
            <MenuItem value="electric">Electric</MenuItem>
          </Select>
        </FormControl>

        {/* Capo Position Selector */}
        <FormControl sx={{ minWidth: 150 }}>
          <InputLabel id="capo-position-label">Capo Position</InputLabel>
          <Select
            labelId="capo-position-label"
            id="capo-position-select"
            value={capoFret}
            label="Capo Position"
            onChange={(e) => setCapoFret(Number(e.target.value))}
          >
            <MenuItem value={0}>No Capo</MenuItem>
            {Array.from({ length: Math.min(12, fretCount) }, (_, i) => i + 1).map((fret) => (
              <MenuItem key={fret} value={fret}>
                Fret {fret}
              </MenuItem>
            ))}
          </Select>
        </FormControl>

        {/* Left-Handed Toggle */}
        <FormControlLabel
          control={
            <Switch
              checked={isLeftHanded}
              onChange={(e) => setIsLeftHanded(e.target.checked)}
            />
          }
          label="Left-Handed"
        />
      </Box>

      {/* Canvas */}
      <Box sx={{
        width: '100%',
        height: isFullscreen ? 'calc(100vh - 200px)' : `${height}px`,
        flex: isFullscreen ? 1 : 'none',
        border: '1px solid #333',
        borderRadius: 1,
        overflow: 'hidden',
        bgcolor: '#1a1a1a'
      }}>
        <canvas ref={canvasRef} style={{ display: 'block', width: '100%', height: '100%' }} />
      </Box>

      {/* Info */}
      <Typography variant="caption" sx={{ color: '#666' }}>
        {guitarStyle.brand} {guitarStyle.model} • 3D Rendering • {enableOrbitControls ? 'Drag to rotate, scroll to zoom' : 'Static view'}
      </Typography>
    </Stack>
  );
};

// Helper function to create realistic wood texture with uniform color and subtle grain
function createWoodTexture(): THREE.CanvasTexture {
  const canvas = document.createElement('canvas');
  canvas.width = 1024;
  canvas.height = 1024;
  const ctx = canvas.getContext('2d')!;

  // Uniform base wood color - rich rosewood/mahogany tone (no gradient stripes)
  ctx.fillStyle = '#4a3525'; // Uniform medium-dark wood color
  ctx.fillRect(0, 0, 1024, 1024);

  // Add subtle wood grain noise for texture
  const imageData = ctx.getImageData(0, 0, 1024, 1024);
  const data = imageData.data;
  for (let i = 0; i < data.length; i += 4) {
    // Subtle noise for natural wood variation
    const noise = (Math.random() - 0.5) * 12;
    data[i] += noise;     // R
    data[i + 1] += noise; // G
    data[i + 2] += noise; // B
  }
  ctx.putImageData(imageData, 0, 0);

  // Add extremely subtle, fine grain lines (horizontal) - barely visible
  for (let i = 0; i < 80; i++) {
    const y = Math.random() * 1024;
    const opacity = 0.02 + Math.random() * 0.04; // Extremely subtle
    const width = 0.2 + Math.random() * 0.4;     // Very thin lines
    const isDark = Math.random() > 0.5;

    ctx.strokeStyle = isDark
      ? `rgba(20, 10, 5, ${opacity})`
      : `rgba(50, 35, 20, ${opacity})`;
    ctx.lineWidth = width;
    ctx.beginPath();

    // Create barely visible wavy grain lines
    const waveAmplitude = 1 + Math.random() * 2; // Minimal waves
    const waveFrequency = 0.02 + Math.random() * 0.01;
    for (let x = 0; x <= 1024; x += 15) {
      const yOffset = Math.sin(x * waveFrequency) * waveAmplitude;
      if (x === 0) {
        ctx.moveTo(x, y + yOffset);
      } else {
        ctx.lineTo(x, y + yOffset);
      }
    }
    ctx.stroke();
  }

  // Add extremely subtle wood pores/texture (tiny dots)
  for (let i = 0; i < 200; i++) {
    const x = Math.random() * 1024;
    const y = Math.random() * 1024;
    const radius = 0.3 + Math.random() * 0.8; // Extremely small

    const poreGradient = ctx.createRadialGradient(x, y, 0, x, y, radius);
    poreGradient.addColorStop(0, 'rgba(15, 8, 4, 0.15)'); // Much more subtle
    poreGradient.addColorStop(1, 'rgba(0, 0, 0, 0)');

    ctx.fillStyle = poreGradient;
    ctx.beginPath();
    ctx.arc(x, y, radius, 0, Math.PI * 2);
    ctx.fill();
  }

  // Add barely visible highlights for subtle depth
  for (let i = 0; i < 40; i++) {
    const y = Math.random() * 1024;
    const opacity = 0.01 + Math.random() * 0.03; // Barely visible
    ctx.strokeStyle = `rgba(90, 65, 40, ${opacity})`;
    ctx.lineWidth = 0.2 + Math.random() * 0.4; // Very thin
    ctx.beginPath();

    // Minimal waves
    const waveAmplitude = 0.5 + Math.random() * 1.5;
    const waveFrequency = 0.025 + Math.random() * 0.015;
    for (let x = 0; x <= 1024; x += 15) {
      const yOffset = Math.sin(x * waveFrequency) * waveAmplitude;
      if (x === 0) {
        ctx.moveTo(x, y + yOffset);
      } else {
        ctx.lineTo(x, y + yOffset);
      }
    }
    ctx.stroke();
  }

  const texture = new THREE.CanvasTexture(canvas);
  texture.wrapS = THREE.RepeatWrapping;
  texture.wrapT = THREE.RepeatWrapping;
  texture.repeat.set(2, 1); // Less repetition for more natural look
  return texture;
}

// Helper function to create micro-normal map for clearcoat layer
// Adds subtle surface variation to soften winding transitions
function createMicroNormalMap(): THREE.CanvasTexture {
  const size = 128;
  const canvas = document.createElement('canvas');
  canvas.width = size;
  canvas.height = size;
  const ctx = canvas.getContext('2d')!;

  // Very subtle variation for clearcoat layer
  for (let y = 0; y < size; y++) {
    const variation = Math.sin(y * 0.2) * 8;
    const val = Math.floor(128 + variation);
    ctx.fillStyle = `rgb(${val}, 128, 255)`;
    ctx.fillRect(0, y, size, 1);
  }

  const texture = new THREE.CanvasTexture(canvas);
  texture.wrapS = THREE.RepeatWrapping;
  texture.wrapT = THREE.RepeatWrapping;
  texture.repeat.set(1, 32);
  texture.needsUpdate = true;

  return texture;
}

// Helper function to create helical winding normal map for wound strings
// Generates a procedural normal map at runtime to avoid JPEG artifacts
// Compatible with both WebGL and WebGPU via MeshPhysicalMaterial
function createWindingNormalMap(): THREE.CanvasTexture {
  const size = 256;
  const canvas = document.createElement('canvas');
  canvas.width = size;
  canvas.height = size;
  const ctx = canvas.getContext('2d')!;

  // Number of winding bands in the texture (will be repeated along string)
  const bands = 32;

  // Generate horizontal bands that simulate the winding ridges
  for (let y = 0; y < size; y++) {
    // Sinusoidal variation for smooth ridges
    const phase = (y / size) * bands * 2 * Math.PI;
    const intensity = Math.sin(phase);

    // Normal map encoding: RGB = (nx, ny, nz) mapped to 0-255
    // For horizontal ridges: mostly pointing up (128, val, 128)
    const val = Math.floor(128 + 64 * intensity); // 64-192 range

    ctx.fillStyle = `rgb(128, ${val}, 128)`;
    ctx.fillRect(0, y, size, 1);
  }

  const texture = new THREE.CanvasTexture(canvas);
  texture.wrapS = THREE.RepeatWrapping;
  texture.wrapT = THREE.RepeatWrapping;

  // Realistic winding density for wound strings
  // For 0.046" string: ~28-30 turns/cm × 65cm ≈ 1900 turns
  // Using ~50 visible wraps for good balance between realism and performance
  texture.repeat.set(1, 50);
  texture.offset.set(0, 0);

  // Enable anisotropic filtering to reduce moiré at grazing angles
  texture.anisotropy = 8; // Will be updated with renderer's max
  texture.minFilter = THREE.LinearMipmapLinearFilter;
  texture.magFilter = THREE.LinearFilter;
  texture.needsUpdate = true;

  return texture;
}

// Helper function to create wound string material using MeshPhysicalMaterial
// Compatible with both WebGL and WebGPU - no ShaderMaterial needed!
// Simulates "oiled metallic wire" appearance with clearcoat and sheen
function createWoundStringMaterial(
  normalMap: THREE.Texture,
  microNormalMap: THREE.Texture,
  renderer?: any // THREE.WebGLRenderer or WebGPURenderer
): THREE.Material {
  // Set maximum anisotropy for better quality when viewing at grazing angles
  // This reduces moiré and banding artifacts
  try {
    if (renderer) {
      let maxAniso = 16; // Default fallback

      // Try to get max anisotropy from renderer capabilities
      if ('capabilities' in renderer) {
        const capabilities = (renderer as any).capabilities;
        if (capabilities && typeof capabilities.getMaxAnisotropy === 'function') {
          maxAniso = capabilities.getMaxAnisotropy();
        }
      }

      normalMap.anisotropy = maxAniso;
      microNormalMap.anisotropy = maxAniso;
    }
  } catch (error) {
    // Silently fail - anisotropy is optional
    console.debug('ThreeFretboard: Could not set anisotropy:', error);
  }

  // Use MeshPhysicalMaterial - works with both WebGL and WebGPU
  // Optimized for realistic wound string appearance - studio quality
  return new THREE.MeshPhysicalMaterial({
    color: 0xc9a876,      // Bronze/brass color for wound strings
    metalness: 1.0,       // Fully metallic
    roughness: 0.18,      // Slightly smoother for "oiled" look
    clearcoat: 0.6,       // Stronger varnish layer
    clearcoatRoughness: 0.12,
    clearcoatNormalMap: microNormalMap, // Micro-roughness for softer transitions
    clearcoatNormalScale: new THREE.Vector2(0.05, 0.05),
    sheen: 0.45,          // Secondary reflection for light diffusion along wire
    sheenColor: new THREE.Color(0xd4b078), // Warmer brass-like sheen
    normalMap: normalMap,
    normalScale: new THREE.Vector2(0.08, 0.08), // Very subtle - not "pen spring"
    envMapIntensity: 1.2, // Enhanced environment reflections
  });
}

// Helper function to create fretboard geometry
function createFretboard(
  parent: THREE.Scene | THREE.Group,
  guitarStyle: GuitarModelStyle,
  fretCount: number,
  stringCount: number,
  leftHanded: boolean,
  capoFret: number = 0,
  isWebGPU: boolean = false
): THREE.Mesh[] {
  // Fretboard dimensions (in Three.js units, 1 unit ≈ 10mm)
  const scaleLength = (guitarStyle.scaleLength || 650) / 10; // Convert mm to units
  const nutWidth = (guitarStyle.nutWidth || 43) / 10;
  const bridgeWidth = (guitarStyle.bridgeWidth || 55) / 10;

  // Create wood texture
  const woodTexture = createWoodTexture();

  // Calculate actual fretboard length (from nut to last fret + half a fret)
  const lastFretPosition = calculateFretPosition3D(fretCount, scaleLength);
  const nextFretPosition = calculateFretPosition3D(fretCount + 1, scaleLength);
  const halfFretDistance = (nextFretPosition - lastFretPosition) / 2;
  const fretboardLength = lastFretPosition + halfFretDistance; // Cut neck 1/2 fret after last fret

  // Create fretboard wood with enhanced PBR materials
  // Use a flat top surface for the playing area
  const fretboardTopThickness = 0.4; // Thinner top surface
  const fretboardGeometry = new THREE.BoxGeometry(fretboardLength, fretboardTopThickness, nutWidth);
  const fretboardMaterial = new THREE.MeshStandardMaterial({
    color: guitarStyle.woodColor,
    map: woodTexture,
    roughness: 0.4,      // Smoother for polished wood look
    metalness: 0.0,
    roughnessMap: woodTexture,
    normalMap: woodTexture, // Add normal mapping for depth
    normalScale: new THREE.Vector2(0.5, 0.5),
    bumpMap: woodTexture,   // Add bump mapping for grain texture
    bumpScale: 0.02,
    // Polygon offset to prevent z-fighting with frets
    polygonOffset: true,
    polygonOffsetFactor: 1,
    polygonOffsetUnits: 4,
  });
  const fretboard = new THREE.Mesh(fretboardGeometry, fretboardMaterial);
  fretboard.position.set(fretboardLength / 2 - scaleLength / 2, fretboardTopThickness / 2, 0);
  fretboard.castShadow = true;
  fretboard.receiveShadow = true;
  parent.add(fretboard);

  // Add rounded back of neck (realistic guitar neck profile)
  // Create a half-ellipse cross-section extruded along the neck length
  // Guitar necks have an elliptical cross-section - wider than they are thick
  const neckWidth = nutWidth * 0.95; // Width (side to side)
  const neckThickness = nutWidth * 0.35; // Thickness (front to back) - flatter profile

  // Create half-ellipse shape for the cross-section
  const ellipseShape = new THREE.Shape();
  const segments = 32;

  // Draw half-ellipse (bottom half, curved part)
  // Start at left edge, draw arc to right edge
  for (let i = 0; i <= segments; i++) {
    const angle = Math.PI + (i / segments) * Math.PI; // π to 2π (bottom half)
    const x = (neckWidth / 2) * Math.cos(angle);
    const y = neckThickness * Math.sin(angle); // Negative Y for bottom half

    if (i === 0) {
      ellipseShape.moveTo(x, y);
    } else {
      ellipseShape.lineTo(x, y);
    }
  }

  // Close the shape with a straight line across the top (flat part that connects to fretboard)
  ellipseShape.lineTo(-neckWidth / 2, 0);

  // Extrude the half-ellipse along the neck length
  const extrudeSettings = {
    depth: fretboardLength,
    bevelEnabled: false,
    steps: 1,
  };

  const neckBackGeometry = new THREE.ExtrudeGeometry(ellipseShape, extrudeSettings);

  // Rotate to align with fretboard (extrusion is along Z, we need it along X)
  // After extrusion, the shape is at Z=0 to Z=fretboardLength
  // We need to rotate and center it
  neckBackGeometry.rotateY(Math.PI / 2);

  // After rotation, the extruded length is now along X-axis
  // Center it so it matches the fretboard position
  // The extrusion starts at 0, so we need to translate it back by half the length
  neckBackGeometry.translate(0, 0, 0);

  // Create separate material for neck back (lighter wood color)
  const neckBackMaterial = new THREE.MeshStandardMaterial({
    color: 0xd4a574, // Lighter maple color for neck
    map: woodTexture,
    roughness: 0.5,
    metalness: 0.0,
    roughnessMap: woodTexture,
    normalMap: woodTexture,
    normalScale: new THREE.Vector2(0.3, 0.3),
    bumpMap: woodTexture,
    bumpScale: 0.015,
  });

  const neckBack = new THREE.Mesh(neckBackGeometry, neckBackMaterial);
  // Position below the fretboard top, with curved part facing down
  // The fretboard top surface is at Y = fretboardTopThickness / 2 = 0.2
  // The fretboard bottom is at Y = -fretboardTopThickness / 2 = -0.2
  // The neck's flat top (Y=0 in the ellipse shape) should align with the fretboard bottom
  // So the neck should be positioned at Y = -fretboardTopThickness / 2
  neckBack.position.set(
    -scaleLength / 2,  // X: align with fretboard start
    -fretboardTopThickness / 2,  // Y: flat part of neck touches fretboard bottom
    0  // Z: centered
  );
  neckBack.castShadow = true;
  neckBack.receiveShadow = true;
  parent.add(neckBack);

  // Add nut (at fret 0) with string slots
  const nutGroup = new THREE.Group();

  // Main nut body - oriented with slots on TOP
  const nutHeight = 0.6;  // Height (Y-axis)
  const nutDepth = 0.3;   // Depth along fretboard (X-axis)
  const nutGeometry = new THREE.BoxGeometry(nutDepth, nutHeight, nutWidth);
  const nutMaterial = new THREE.MeshStandardMaterial({
    color: guitarStyle.nutColor || 0xf5f5dc,
    roughness: 0.8,
    metalness: 0.0,
  });
  const nut = new THREE.Mesh(nutGeometry, nutMaterial);
  nut.castShadow = true;
  nut.receiveShadow = true;
  nutGroup.add(nut);

  // Add subtle string grooves in the nut
  const grooveMaterial = new THREE.MeshStandardMaterial({
    color: 0x2a2a2a, // Slightly darker than nut for subtle grooves
    roughness: 0.85,
    metalness: 0.0,
  });

  // String thicknesses (matches string creation below): high E to low E
  const nutSlotThicknesses = [0.018, 0.022, 0.028, 0.035, 0.045, 0.055];

  for (let i = 0; i < stringCount; i++) {
    const stringZ = (i / (stringCount - 1) - 0.5) * nutWidth * 0.85;

    // Make groove proportional to string thickness
    // Real nut slots are small rounded grooves
    const stringThickness = nutSlotThicknesses[i] || 0.02;
    const grooveWidth = stringThickness * 1.1;   // Just slightly wider than string
    const grooveDepth = stringThickness * 0.8;   // Shallow groove
    const grooveLength = nutDepth;               // Groove length matches nut depth

    // Create a simple box groove (subtle indentation)
    const grooveGeometry = new THREE.BoxGeometry(grooveLength, grooveDepth, grooveWidth);
    const groove = new THREE.Mesh(grooveGeometry, grooveMaterial);

    // Position the groove at the top of the nut, slightly sunken
    groove.position.set(0, nutHeight / 2 - grooveDepth / 3, stringZ);
    groove.castShadow = false; // Don't cast shadows to keep it subtle
    groove.receiveShadow = true;
    nutGroup.add(groove);
  }

  nutGroup.position.set(-scaleLength / 2, 0.5, 0);
  parent.add(nutGroup);

  // Add tuning labels next to the nut
  addTuningLabels(parent, stringCount, scaleLength, nutWidth);

  // Add fret number labels
  addFretNumberLabels(parent, fretCount, scaleLength, nutWidth);

  // Add frets with realistic crowned geometry using CapsuleGeometry
  // CapsuleGeometry gives a perfect "crowned" profile without orientation issues
  console.log(`Creating ${fretCount} frets for fretboard`);
  for (let i = 1; i <= fretCount; i++) {
    const fretPosition = calculateFretPosition3D(i, scaleLength);

    // Frets extend to the edge of the neck
    const fretWidth = nutWidth * 1.0; // Full neck width - extends to edges
    const fretRadius = 0.15; // Larger radius for better visibility (~1.5mm)
    const fretHeightY = 0.12; // Height above fretboard surface (0.8-1.2mm relief)

    // Create fret geometry using CapsuleGeometry for perfect crowned profile
    // CapsuleGeometry: radius, length, capSegments, radialSegments
    // Length is the straight section between the two hemispherical caps
    const capsuleLength = Math.max(0, fretWidth - 2 * fretRadius);
    const fretGeometry = new THREE.CapsuleGeometry(
      fretRadius,      // Radius of the crowned part
      capsuleLength,   // Length of straight section
      8,               // Cap segments (hemispheres at ends)
      24               // More radial segments for smoother appearance
    );

    // CRITICAL: Strings are aligned along X-axis → Frets must be aligned along Z-axis (perpendicular)
    // CapsuleGeometry is created along Y-axis by default
    // Rotate X by 90° to make the capsule length follow Z-axis (across fretboard width)
    fretGeometry.rotateX(Math.PI / 2);
    fretGeometry.computeVertexNormals();

    // Use MeshPhysicalMaterial for "polished metal" appearance
    // Optimized to catch grazing light and stand out against dark wood
    const fretMaterial = new THREE.MeshPhysicalMaterial({
      color: 0xd8d0c0,       // Nickel/champagne color (warm metallic)
      metalness: 1.0,        // Fully metallic
      roughness: 0.28,       // Moderate - not mirror, not matte
      clearcoat: 0.35,       // Stronger clearcoat for polish
      clearcoatRoughness: 0.18,
      envMapIntensity: 1.2,  // Accentuate reflections
      sheen: 0.3,            // Secondary reflection for "golden" look under WebGPU
      sheenColor: new THREE.Color(0xe0cfb0), // Warm golden sheen
      side: THREE.FrontSide,
    });

    const fret = new THREE.Mesh(fretGeometry, fretMaterial);

    // Position along X axis (length of fretboard), raised above fretboard surface
    // Y position provides clear visual separation from dark wood
    fret.position.set(
      fretPosition - scaleLength / 2,  // X: along fretboard
      fretboardTopThickness / 2 + fretHeightY,  // Y: 0.8-1.2mm above fretboard
      0  // Z: centered
    );

    fret.castShadow = true;
    fret.receiveShadow = true;
    parent.add(fret);

    if (i === fretCount) {
      console.log(`Last fret (#${i}) positioned at X=${fretPosition - scaleLength / 2}, Y=${fretboardTopThickness / 2 + fretHeightY}`);
    }
  }

  // Add inlays (position markers)
  const inlayPositions = [3, 5, 7, 9, 15, 17, 19, 21];
  const doubleInlayPositions = [12, 24];

  inlayPositions.forEach(fretNum => {
    if (fretNum <= fretCount) {
      const fretPos = calculateFretPosition3D(fretNum, scaleLength);
      const prevFretPos = calculateFretPosition3D(fretNum - 1, scaleLength);
      const inlayPos = (fretPos + prevFretPos) / 2 - scaleLength / 2;

      // Create flat circular inlay (cylinder with very small height, lying flat)
      const inlayGeometry = new THREE.CylinderGeometry(0.15, 0.15, 0.02, 32);
      // Mother-of-pearl / nacre material with iridescent properties
      const inlayMaterial = new THREE.MeshStandardMaterial({
        color: 0xf5f5dc, // Ivory/beige base color
        roughness: 0.2,
        metalness: 0.3,
        emissive: 0xffffff,
        emissiveIntensity: 0.15,
        envMapIntensity: 1.5, // Enhanced reflections
      });
      const inlay = new THREE.Mesh(inlayGeometry, inlayMaterial);
      inlay.position.set(inlayPos, 0.39, 0); // Embedded flush with fretboard surface
      // No rotation needed - cylinder axis (Y) is already vertical
      parent.add(inlay);
    }
  });

  // Add double inlays at 12th and 24th frets
  doubleInlayPositions.forEach(fretNum => {
    if (fretNum <= fretCount) {
      const fretPos = calculateFretPosition3D(fretNum, scaleLength);
      const prevFretPos = calculateFretPosition3D(fretNum - 1, scaleLength);
      const inlayPos = (fretPos + prevFretPos) / 2 - scaleLength / 2;

      [-0.8, 0.8].forEach(offset => {
        // Create flat circular inlay (cylinder with very small height, lying flat)
        const inlayGeometry = new THREE.CylinderGeometry(0.15, 0.15, 0.02, 32);
        // Mother-of-pearl / nacre material with iridescent properties
        const inlayMaterial = new THREE.MeshStandardMaterial({
          color: 0xf5f5dc, // Ivory/beige base color
          roughness: 0.2,
          metalness: 0.3,
          emissive: 0xffffff,
          emissiveIntensity: 0.15,
          envMapIntensity: 1.5, // Enhanced reflections
        });
        const inlay = new THREE.Mesh(inlayGeometry, inlayMaterial);
        inlay.position.set(inlayPos, 0.39, offset); // Embedded flush with fretboard surface
        // No rotation needed - cylinder axis (Y) is already vertical
        parent.add(inlay);
      });
    }
  });

  // Add strings with realistic gauges (EADGBE standard set)
  // Extend strings from nut to bridge (fretboard + body zone)
  // Gauges in inches: 0.010, 0.013, 0.017, 0.026, 0.036, 0.046
  // Array order: high E (thin) to low E (thick) - matches visual top to bottom
  // Scaled to Three.js units (1 unit ≈ 10mm)
  const stringGauges = [0.010, 0.013, 0.017, 0.026, 0.036, 0.046]; // Inches
  const stringThicknesses = stringGauges.map(g => g * 2.54 / 10); // Convert to Three.js units (cm)

  // Calculate body zone length based on last fret spacing
  const secondToLastFretPosition = calculateFretPosition3D(fretCount - 1, scaleLength);
  const lastFretSpacing = lastFretPosition - secondToLastFretPosition;
  const bodyZoneLength = lastFretSpacing * 3; // 3 times the last fret spacing

  // Fretboard is centered at (fretboardLength / 2 - scaleLength / 2)
  // So it extends from (-scaleLength / 2) to (fretboardLength - scaleLength / 2)
  const fretboardEndX = fretboardLength - scaleLength / 2; // Actual end of fretboard in scene coordinates
  const overlap = 0.1; // Small overlap to ensure seamless connection
  const bodyStartX = fretboardEndX - overlap; // Body starts slightly before fretboard end (overlap)
  const bridgeX = bodyStartX + bodyZoneLength; // Bridge position
  const stringLength = fretboardLength + bodyZoneLength;

  // Create the winding normal maps once (shared by all wound strings)
  const windingNormalMap = createWindingNormalMap();
  const microNormalMap = createMicroNormalMap();

  // Array to store string meshes for animation
  const strings: THREE.Mesh[] = [];

  for (let i = 0; i < stringCount; i++) {
    const stringY = (i / (stringCount - 1) - 0.5) * nutWidth * 0.85;
    const thickness = stringThicknesses[i] || 0.02;
    const isWoundString = i >= 2; // Strings 2-5 are wound (G, D, A, low E)

    // Create string geometry
    // CylinderGeometry creates a cylinder along Y-axis by default
    const stringGeometry = new THREE.CylinderGeometry(
      thickness,
      thickness,
      stringLength,
      isWoundString ? 32 : 16,  // More radial segments for wound strings
      1,  // Height segments
      true  // Open-ended (no caps) for better UVs
    );

    // Compute tangents for proper normal mapping (required for WebGPU)
    stringGeometry.computeTangents();
    stringGeometry.computeVertexNormals();

    let stringMaterial: THREE.Material;

    if (isWoundString) {
      // Use MeshPhysicalMaterial with normal map - works with both WebGL and WebGPU!
      // Studio-quality wound string with clearcoat and micro-normal
      stringMaterial = createWoundStringMaterial(windingNormalMap, microNormalMap);
    } else {
      // Plain steel strings - mirror-like finish
      stringMaterial = new THREE.MeshStandardMaterial({
        color: 0xe0e0e0, // Bright silver for plain strings
        roughness: 0.1,
        metalness: 0.98,
        emissive: 0x404040,
        emissiveIntensity: 0.2,
      });
    }

    const string = new THREE.Mesh(stringGeometry, stringMaterial);
    // String action (height above fretboard) varies by guitar type
    // Electric guitars have lower action than acoustic/classical
    const stringAction = guitarStyle.category === 'electric' ? 0.55 : 0.65;
    string.position.set((stringLength / 2 - scaleLength / 2), stringAction, stringY);
    // Rotate 90° around Z to align cylinder along X-axis (along the fretboard)
    string.rotation.z = Math.PI / 2;
    string.castShadow = true;
    parent.add(string);

    // Store string mesh for animation
    strings.push(string);
  }

  // Add bridge at the end of the body zone (where strings end)
  const bridgeGeometry = new THREE.BoxGeometry(0.5, 0.4, bridgeWidth);
  const bridgeMaterial = new THREE.MeshStandardMaterial({
    color: 0x333333,
    roughness: 0.5,
    metalness: 0.7,
  });
  const bridge = new THREE.Mesh(bridgeGeometry, bridgeMaterial);
  bridge.position.set(bridgeX, 0.3, 0); // Position at end of body zone
  bridge.castShadow = true;
  parent.add(bridge);

  // Add green strum zone rectangle (right hand zone) in the body area
  // Position it right after the neck finishes (at bodyStartX)
  // Use same height as fretboard for seamless connection
  const strumZoneHeight = fretboardTopThickness; // Match fretboard thickness (0.4)
  const strumZoneGeometry = new THREE.BoxGeometry(bodyZoneLength, strumZoneHeight, nutWidth * 1.1);
  const strumZoneMaterial = new THREE.MeshStandardMaterial({
    color: 0x00ff00, // Bright green
    roughness: 0.5,
    metalness: 0.2,
    transparent: true,
    opacity: 0.2, // More transparent
  });
  const strumZone = new THREE.Mesh(strumZoneGeometry, strumZoneMaterial);
  // Position at the center of the body zone, aligned with fretboard top
  // Y position matches fretboard: strumZoneHeight / 2 = fretboardTopThickness / 2 = 0.2
  strumZone.position.set(bodyStartX + bodyZoneLength / 2, strumZoneHeight / 2, 0);
  parent.add(strumZone);

  // Add capo if enabled
  if (capoFret > 0 && capoFret <= fretCount) {
    const capoPosition = calculateFretPosition3D(capoFret, scaleLength);
    const prevCapoPosition = calculateFretPosition3D(capoFret - 1, scaleLength);
    const capoX = (capoPosition + prevCapoPosition) / 2 - scaleLength / 2;

    // Try to load 3D capo model, fallback to geometric capo if it fails
    loadCapoModel({
      modelPath: '/models/guitar-capo.glb',
      scale: 0.1, // Adjust scale as needed for the Sketchfab model
      position: new THREE.Vector3(capoX, 0.8, 0),
      rotation: new THREE.Euler(0, 0, 0), // Adjust rotation if needed
      color: 0x1a1a1a, // Dark color for the capo body
      metalness: 0.8,
      roughness: 0.3
    }).then((capoModel) => {
      // Successfully loaded 3D model
      console.log('✅ 3D capo model loaded successfully');
      parent.add(capoModel);
    }).catch((error) => {
      // Failed to load 3D model, use fallback geometric capo
      console.warn('⚠️ Failed to load 3D capo model, using fallback geometry:', error);
      const fallbackCapo = createFallbackCapo(
        nutWidth,
        new THREE.Vector3(capoX, 0, 0),
        0x1a1a1a
      );
      parent.add(fallbackCapo);
    });
  }

  // Return strings array for animation
  return strings;
}

// Helper function to add tuning labels next to the nut
function addTuningLabels(
  parent: THREE.Scene | THREE.Group,
  stringCount: number,
  scaleLength: number,
  nutWidth: number
) {
  const tuningNotes = ['E', 'B', 'G', 'D', 'A', 'E']; // Standard tuning (high to low)

  for (let i = 0; i < stringCount; i++) {
    const stringY = (i / (stringCount - 1) - 0.5) * nutWidth * 0.85;
    const note = tuningNotes[i] || '';

    // Create a canvas for the text
    const canvas = document.createElement('canvas');
    const context = canvas.getContext('2d')!;
    canvas.width = 64;
    canvas.height = 64;

    // Draw text on canvas
    context.fillStyle = '#ffffff';
    context.font = 'bold 48px Arial';
    context.textAlign = 'center';
    context.textBaseline = 'middle';
    context.fillText(note, 32, 32);

    // Create texture from canvas
    const texture = new THREE.CanvasTexture(canvas);

    // Create sprite with the texture
    const spriteMaterial = new THREE.SpriteMaterial({ map: texture });
    const sprite = new THREE.Sprite(spriteMaterial);
    sprite.scale.set(0.5, 0.5, 1);
    sprite.position.set(-scaleLength / 2 - 0.8, 0.5, stringY);

    parent.add(sprite);
  }
}

// Helper function to add fret number labels
function addFretNumberLabels(
  parent: THREE.Scene | THREE.Group,
  fretCount: number,
  scaleLength: number,
  nutWidth: number
) {
  // Add labels for all frets from 0 to fretCount
  for (let fretNum = 0; fretNum <= fretCount; fretNum++) {
    let labelX: number;

    if (fretNum === 0) {
      // Position label at the nut (fret 0)
      labelX = -scaleLength / 2;
    } else {
      // Position label at the fret wire
      const fretPosition = calculateFretPosition3D(fretNum, scaleLength);
      labelX = fretPosition - scaleLength / 2;
    }

    // Create a canvas for the text - larger for better visibility
    const canvas = document.createElement('canvas');
    const context = canvas.getContext('2d')!;
    canvas.width = 256;  // Increased from 128
    canvas.height = 128; // Increased from 64

    // Draw text on canvas - larger font
    context.fillStyle = '#ffffff'; // Brighter white for better visibility
    context.font = 'bold 72px Arial'; // Increased from 36px
    context.textAlign = 'center';
    context.textBaseline = 'middle';
    context.fillText(fretNum.toString(), 128, 64); // Adjusted for new canvas size

    // Create texture from canvas
    const texture = new THREE.CanvasTexture(canvas);

    // Create sprite with the texture - larger scale
    const spriteMaterial = new THREE.SpriteMaterial({ map: texture });
    const sprite = new THREE.Sprite(spriteMaterial);
    sprite.scale.set(1.2, 0.6, 1); // Increased from (0.6, 0.3, 1) - 2x larger
    sprite.position.set(labelX, 0.5, nutWidth / 2 + 0.8); // Moved slightly further out

    parent.add(sprite);
  }
}

// Helper function to add position markers
function addPositionMarkers(
  parent: THREE.Scene | THREE.Group,
  positions: ThreeFretboardPosition[],
  fretCount: number,
  stringCount: number,
  scaleLength: number,
  nutWidth: number
) {
  positions.forEach(pos => {
    const { string, fret, label, color, emphasized } = pos;

    // Calculate position
    const fretPos = calculateFretPosition3D(fret, scaleLength);
    const prevFretPos = fret > 0 ? calculateFretPosition3D(fret - 1, scaleLength) : 0;
    const markerX = (fretPos + prevFretPos) / 2 - scaleLength / 2;
    const markerZ = (string / (stringCount - 1) - 0.5) * nutWidth * 0.85;

    // Create marker sphere
    const markerRadius = emphasized ? 0.25 : 0.2;
    const markerGeometry = new THREE.SphereGeometry(markerRadius, 32, 32);
    const markerColor = color ? parseInt(color.replace('#', '0x')) : 0xff6b6b;
    const markerMaterial = new THREE.MeshStandardMaterial({
      color: markerColor,
      roughness: 0.3,
      metalness: 0.2,
      emissive: markerColor,
      emissiveIntensity: emphasized ? 0.3 : 0.2,
    });
    const marker = new THREE.Mesh(markerGeometry, markerMaterial);
    marker.position.set(markerX, 1.0, markerZ);
    marker.castShadow = true;
    parent.add(marker);

    // Add label if provided
    if (label) {
      const canvas = document.createElement('canvas');
      canvas.width = 128;
      canvas.height = 128;
      const ctx = canvas.getContext('2d')!;

      // Draw label background
      ctx.fillStyle = 'rgba(0, 0, 0, 0.7)';
      ctx.fillRect(0, 0, 128, 128);

      // Draw label text
      ctx.fillStyle = 'white';
      ctx.font = 'bold 64px Arial';
      ctx.textAlign = 'center';
      ctx.textBaseline = 'middle';
      ctx.fillText(label, 64, 64);

      const labelTexture = new THREE.CanvasTexture(canvas);
      const labelMaterial = new THREE.SpriteMaterial({
        map: labelTexture,
        transparent: true,
      });
      const labelSprite = new THREE.Sprite(labelMaterial);
      labelSprite.position.set(markerX, 1.5, markerZ);
      labelSprite.scale.set(0.5, 0.5, 1);
      parent.add(labelSprite);
    }
  });
}

// Helper function to calculate fret position in 3D space
function calculateFretPosition3D(fretNumber: number, scaleLength: number): number {
  if (fretNumber === 0) return 0;
  const ratio = Math.pow(2, fretNumber / 12);
  return scaleLength * (1 - 1 / ratio);
}
