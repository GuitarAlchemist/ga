// @ts-nocheck
/**
 * Minimal Three.js Instrument Component
 * 
 * A single component that can render ANY stringed instrument from the YAML database
 * using ThreeJS + WebGPU with adaptive geometry and materials.
 */

import React, { useRef, useEffect, useState, useCallback } from 'react';
import { Box, Typography, Alert } from '@mui/material';
import * as THREE from 'three';
import { WebGPURenderer } from 'three/webgpu';
import { OrbitControls } from 'three/examples/jsm/controls/OrbitControls.js';
import type { InstrumentConfig, FretboardPosition } from '../../types/InstrumentConfig';
import { InstrumentGeometryFactory } from './InstrumentGeometryFactory';
import { InstrumentMaterialFactory } from './InstrumentMaterialFactory';
import { loadCapoModel, createFallbackCapo } from '../../utils/capoModelLoader';

export interface MinimalThreeInstrumentProps {
  // Core configuration
  instrument: InstrumentConfig;
  positions?: FretboardPosition[];
  
  // Rendering options
  renderMode?: '3d-webgl' | '3d-webgpu';
  viewMode?: 'fretboard' | 'headstock' | 'full';
  
  // Display options
  capoFret?: number;
  leftHanded?: boolean;
  showLabels?: boolean;
  showInlays?: boolean;
  showTuningPegs?: boolean;
  
  // Viewport
  width?: number;
  height?: number;
  
  // Controls
  enableOrbitControls?: boolean;
  
  // Callbacks
  onPositionClick?: (string: number, fret: number) => void;
  onPositionHover?: (string: number | null, fret: number | null) => void;
  onReady?: () => void;
  onError?: (error: string) => void;
  
  // UI
  title?: string;
}

export const MinimalThreeInstrument: React.FC<MinimalThreeInstrumentProps> = ({
  instrument,
  positions = [],
  renderMode = '3d-webgpu',
  viewMode = 'fretboard',
  capoFret = 0,
  leftHanded = false,
  showLabels = true,
  showInlays = true,
  showTuningPegs = true,
  width = 1200,
  height = 400,
  enableOrbitControls = true,
  onPositionClick,
  onPositionHover,
  onReady,
  onError,
  title,
}) => {
  const canvasRef = useRef<HTMLCanvasElement>(null);
  const rendererRef = useRef<WebGPURenderer | THREE.WebGLRenderer | null>(null);
  const sceneRef = useRef<THREE.Scene | null>(null);
  const cameraRef = useRef<THREE.PerspectiveCamera | null>(null);
  const controlsRef = useRef<OrbitControls | null>(null);
  const instrumentGroupRef = useRef<THREE.Group | null>(null);
  const positionMarkersRef = useRef<THREE.Group | null>(null);
  const animationIdRef = useRef<number | null>(null);
  const hoveredObjectRef = useRef<THREE.Object3D | null>(null);

  const [isWebGPU, setIsWebGPU] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [isLoading, setIsLoading] = useState(true);
  const [hoveredPosition, setHoveredPosition] = useState<{ string: number; fret: number } | null>(null);

  // Cleanup function
  const cleanup = useCallback(() => {
    if (animationIdRef.current) {
      cancelAnimationFrame(animationIdRef.current);
      animationIdRef.current = null;
    }

    if (controlsRef.current) {
      controlsRef.current.dispose();
      controlsRef.current = null;
    }

    // Dispose renderer FIRST before disposing materials
    // This is critical for WebGPU renderer to avoid "usedTimes" errors
    if (rendererRef.current) {
      // Stop animation loop before disposing
      rendererRef.current.setAnimationLoop(null);
      rendererRef.current.dispose();
      rendererRef.current = null;
    }

    // Now safe to dispose scene objects
    if (sceneRef.current) {
      // Dispose all geometries and materials in the scene
      sceneRef.current.traverse((object) => {
        if (object instanceof THREE.Mesh) {
          // Safely dispose geometry
          if (object.geometry && typeof object.geometry.dispose === 'function') {
            try {
              object.geometry.dispose();
            } catch (e) {
              // Ignore disposal errors
            }
          }
          // Safely dispose material
          if (object.material) {
            try {
              if (Array.isArray(object.material)) {
                object.material.forEach(material => {
                  if (material && typeof material.dispose === 'function') {
                    material.dispose();
                  }
                });
              } else if (typeof object.material.dispose === 'function') {
                object.material.dispose();
              }
            } catch (e) {
              // Ignore disposal errors
            }
          }
        }
      });
      sceneRef.current = null;
    }

    // Clear refs
    instrumentGroupRef.current = null;
    cameraRef.current = null;

    // DON'T clear material cache - materials may be shared across instances
    // and clearing them causes WebGPU "usedTimes" errors
    // InstrumentMaterialFactory.clearCache();
  }, []);

  // Initialize Three.js scene
  const initThree = useCallback(async () => {
    if (!canvasRef.current) return;

    try {
      setIsLoading(true);
      setError(null);

      // Cleanup previous instance
      cleanup();

      const canvas = canvasRef.current;

      // Create renderer
      let renderer: WebGPURenderer | THREE.WebGLRenderer;
      
      if (renderMode === '3d-webgpu' && 'gpu' in navigator) {
        try {
          renderer = new WebGPURenderer({
            canvas,
            antialias: true,
            alpha: true,
            forceWebGL: false,
            samples: 8, // 8x MSAA for maximum quality
          });
          await renderer.init();
          setIsWebGPU(true);
          console.log('✅ MinimalThreeInstrument: Using WebGPU renderer with 8x MSAA');
        } catch (webgpuError) {
          console.warn('WebGPU failed, falling back to WebGL:', webgpuError);
          renderer = new THREE.WebGLRenderer({
            canvas,
            antialias: true,
            alpha: true,
          });
          setIsWebGPU(false);
        }
      } else {
        renderer = new THREE.WebGLRenderer({
          canvas,
          antialias: true,
          alpha: true,
        });
        setIsWebGPU(false);
        console.log('✅ MinimalThreeInstrument: Using WebGL renderer');
      }

      renderer.setSize(width, height);
      renderer.setPixelRatio(Math.min(window.devicePixelRatio * 3, 6));
      renderer.setClearColor(0x2a2a2a, 1);
      renderer.shadowMap.enabled = true;
      renderer.shadowMap.type = THREE.PCFSoftShadowMap;
      rendererRef.current = renderer;

      // Create scene
      const scene = new THREE.Scene();
      scene.background = new THREE.Color(0x2a2a2a);
      sceneRef.current = scene;

      // Create lightweight environment map (skybox) for metallic reflections
      // Using smaller resolution (512×256) for better performance
      const envCanvas = document.createElement('canvas');
      envCanvas.width = 512;
      envCanvas.height = 256;
      const envCtx = envCanvas.getContext('2d')!;

      // Studio environment gradient (dark floor to bright ceiling)
      const gradient = envCtx.createLinearGradient(0, 256, 0, 0);
      gradient.addColorStop(0, '#1a1a1a');
      gradient.addColorStop(0.5, '#5a5a5a');
      gradient.addColorStop(1, '#d0d0d0');

      envCtx.fillStyle = gradient;
      envCtx.fillRect(0, 0, 512, 256);

      const envTexture = new THREE.CanvasTexture(envCanvas);
      envTexture.mapping = THREE.EquirectangularReflectionMapping;
      envTexture.colorSpace = THREE.SRGBColorSpace;

      // Apply environment map (no PMREM for performance)
      scene.environment = envTexture;

      // Create camera
      const camera = new THREE.PerspectiveCamera(45, width / height, 0.1, 1000);
      
      // Position camera based on view mode and instrument
      const cameraPosition = getCameraPosition(instrument, viewMode);
      camera.position.set(cameraPosition.x, cameraPosition.y, cameraPosition.z);
      camera.lookAt(0, 0, 0);
      cameraRef.current = camera;

      // Add lighting
      setupLighting(scene);

      // Create orbit controls
      if (enableOrbitControls) {
        const controls = new OrbitControls(camera, canvas);
        controls.enableDamping = true;
        controls.dampingFactor = 0.05;
        controls.screenSpacePanning = false;
        controls.minDistance = 0.1;
        controls.maxDistance = 10;
        controls.maxPolarAngle = Math.PI;
        controlsRef.current = controls;
      }

      // Create instrument geometry
      await createInstrumentGeometry(scene, instrument, viewMode, leftHanded);

      // Create position markers
      createPositionMarkers(scene, instrument, positions);

      // Create capo if needed
      if (capoFret > 0) {
        createCapo(scene, instrument, capoFret);
      }

      // Start render loop
      const animate = () => {
        animationIdRef.current = requestAnimationFrame(animate);
        
        if (controlsRef.current) {
          controlsRef.current.update();
        }
        
        renderer.render(scene, camera);
      };
      animate();

      setIsLoading(false);
      onReady?.();

    } catch (error) {
      const errorMessage = error instanceof Error ? error.message : 'Unknown error';
      console.error('MinimalThreeInstrument initialization failed:', error);
      setError(errorMessage);
      setIsLoading(false);
      onError?.(errorMessage);
    }
  }, [
    instrument,
    renderMode,
    viewMode,
    leftHanded,
    capoFret,
    width,
    height,
    enableOrbitControls,
    onReady,
    onError,
    cleanup,
  ]);

  // Create instrument geometry
  const createInstrumentGeometry = async (
    scene: THREE.Scene,
    instrument: InstrumentConfig,
    viewMode: string,
    leftHanded: boolean
  ) => {
    try {
      // Remove existing instrument group
      if (instrumentGroupRef.current) {
        scene.remove(instrumentGroupRef.current);
        instrumentGroupRef.current = null;
      }

      const instrumentGroup = new THREE.Group();
      instrumentGroupRef.current = instrumentGroup;

      // Apply left-handed transformation
      if (leftHanded) {
        instrumentGroup.scale.x = -1;
      }

    if (viewMode === 'fretboard' || viewMode === 'full') {
      // Create neck back FIRST (so it renders underneath everything)
      const neckBackGeometry = InstrumentGeometryFactory.createNeckBack(instrument);
      const neckBackMaterial = InstrumentMaterialFactory.createWoodMaterial(
        instrument.woodColor,
        instrument.bodyStyle
      );
      const neckBack = new THREE.Mesh(neckBackGeometry, neckBackMaterial);
      neckBack.castShadow = true;
      neckBack.receiveShadow = true;
      instrumentGroup.add(neckBack);

      // Create fretboard (on top of neck back)
      const fretboardGeometry = InstrumentGeometryFactory.createFretboard(instrument);
      const fretboardMaterial = InstrumentMaterialFactory.createWoodMaterial(
        instrument.woodColor,
        instrument.bodyStyle
      );
      const fretboard = new THREE.Mesh(fretboardGeometry, fretboardMaterial);
      fretboard.castShadow = true;
      fretboard.receiveShadow = true;
      instrumentGroup.add(fretboard);

      // Create truncated body section
      const bodyGeometry = InstrumentGeometryFactory.createTruncatedBody(instrument);
      const bodyMaterial = InstrumentMaterialFactory.createWoodMaterial(
        instrument.woodColor,
        instrument.bodyStyle
      );
      const body = new THREE.Mesh(bodyGeometry, bodyMaterial);
      body.castShadow = true;
      body.receiveShadow = true;
      instrumentGroup.add(body);

      // Create nut with string holes
      const nutGeometries = InstrumentGeometryFactory.createNut(instrument);
      nutGeometries.forEach((geometry, index) => {
        // First geometry is the nut body, rest are holes
        const material = index === 0
          ? InstrumentMaterialFactory.createNutMaterial()
          : InstrumentMaterialFactory.createNutHoleMaterial();
        const nutPart = new THREE.Mesh(geometry, material);
        nutPart.castShadow = true;
        nutPart.receiveShadow = true;
        instrumentGroup.add(nutPart);
      });

      // Create frets
      const fretGeometries = InstrumentGeometryFactory.createFrets(instrument);
      fretGeometries.forEach((geometry) => {
        const material = InstrumentMaterialFactory.createFretMaterial(false);
        const fret = new THREE.Mesh(geometry, material);
        fret.castShadow = true;
        instrumentGroup.add(fret);
      });

      // Create strings
      const stringGeometries = InstrumentGeometryFactory.createStrings(instrument);
      const stringMaterials = InstrumentMaterialFactory.getStringMaterials(instrument);
      stringGeometries.forEach((geometry, index) => {
        const material = stringMaterials[index] || stringMaterials[0];
        const string = new THREE.Mesh(geometry, material);
        string.castShadow = true;
        instrumentGroup.add(string);
      });

      // Create inlays
      if (showInlays) {
        const inlayGeometries = InstrumentGeometryFactory.createInlays(instrument);
        const inlayMaterial = InstrumentMaterialFactory.createInlayMaterial(instrument.bodyStyle);
        inlayGeometries.forEach((geometry) => {
          const inlay = new THREE.Mesh(geometry, inlayMaterial);
          instrumentGroup.add(inlay);
        });
      }

      // Create strumming zone
      const strummingZoneGeometry = InstrumentGeometryFactory.createStrummingZone(instrument);
      const strummingZoneMaterial = InstrumentMaterialFactory.createStrummingZoneMaterial();
      const strummingZone = new THREE.Mesh(strummingZoneGeometry, strummingZoneMaterial);
      strummingZone.renderOrder = 999; // Render on top
      instrumentGroup.add(strummingZone);
    }

    if (viewMode === 'headstock' || viewMode === 'full') {
      // Create headstock
      const headstockGeometry = InstrumentGeometryFactory.createHeadstock(instrument);
      const headstockMaterial = InstrumentMaterialFactory.createWoodMaterial(
        instrument.woodColor,
        instrument.bodyStyle
      );
      const headstock = new THREE.Mesh(headstockGeometry, headstockMaterial);
      headstock.castShadow = true;
      headstock.receiveShadow = true;
      instrumentGroup.add(headstock);

      // Create tuning pegs
      if (showTuningPegs) {
        const pegGeometries = InstrumentGeometryFactory.createTuningPegs(instrument);
        const pegMaterial = InstrumentMaterialFactory.createTuningPegMaterial();
        pegGeometries.forEach((geometry) => {
          const peg = new THREE.Mesh(geometry, pegMaterial);
          peg.castShadow = true;
          instrumentGroup.add(peg);
        });
      }
    }

      scene.add(instrumentGroup);
    } catch (error) {
      console.error('Error creating instrument geometry:', error);
      const errorMessage = error instanceof Error ? error.message : 'Failed to create instrument geometry';
      onError?.(errorMessage);
    }
  };

  // Create position markers
  const createPositionMarkers = (
    scene: THREE.Scene,
    instrument: InstrumentConfig,
    positions: FretboardPosition[]
  ) => {
    if (positionMarkersRef.current) {
      scene.remove(positionMarkersRef.current);
    }

    const markersGroup = new THREE.Group();
    positionMarkersRef.current = markersGroup;

    const fretPositions = InstrumentGeometryFactory.calculateFretPositions(
      instrument.scaleLength,
      instrument.fretCount
    );
    const stringSpacing = InstrumentGeometryFactory.calculateStringSpacing(instrument);

    positions.forEach((pos) => {
      if (pos.fret > instrument.fretCount || pos.string >= instrument.tuning.length) return;

      const fretPos = fretPositions[pos.fret] * 0.001; // Convert to meters
      const nextFretPos = fretPositions[Math.min(pos.fret + 1, fretPositions.length - 1)] * 0.001;
      const midPos = pos.fret === 0 ? fretPos : (fretPos + nextFretPos) / 2;

      const stringPos = stringSpacing.positions[pos.string] * 0.001;

      const radius = pos.emphasized ? 0.008 : 0.006;
      const color = pos.color ? parseInt(pos.color.replace('#', ''), 16) : 0x4DABF7;

      const geometry = new THREE.SphereGeometry(radius, 16, 16);
      const material = InstrumentMaterialFactory.createPositionMarkerMaterial(color, pos.emphasized);
      const marker = new THREE.Mesh(geometry, material);

      // Position marker correctly (X = string position, Y = above fretboard, Z = fret position)
      marker.position.set(stringPos, 0.01, midPos);
      marker.castShadow = true;

      // Add click interaction
      marker.userData = { string: pos.string, fret: pos.fret };

      // Add label if provided
      if (pos.label && showLabels) {
        const labelGeometry = new THREE.PlaneGeometry(0.012, 0.012);
        const canvas = document.createElement('canvas');
        canvas.width = 128;
        canvas.height = 128;
        const context = canvas.getContext('2d')!;

        // Create metallic-looking text with gradient and shadow
        const gradient = context.createLinearGradient(0, 0, 0, 128);
        gradient.addColorStop(0, '#ffffff');
        gradient.addColorStop(0.5, '#d4d4d4');
        gradient.addColorStop(1, '#a0a0a0');

        // Add shadow for depth
        context.shadowColor = 'rgba(0, 0, 0, 0.5)';
        context.shadowBlur = 4;
        context.shadowOffsetX = 2;
        context.shadowOffsetY = 2;

        context.fillStyle = gradient;
        context.font = 'bold 64px Arial';
        context.textAlign = 'center';
        context.textBaseline = 'middle';
        context.fillText(pos.label, 64, 64);

        // Add highlight for metallic effect
        context.shadowColor = 'transparent';
        context.fillStyle = 'rgba(255, 255, 255, 0.6)';
        context.font = 'bold 64px Arial';
        context.fillText(pos.label, 62, 62);

        const texture = new THREE.CanvasTexture(canvas);
        const labelMaterial = new THREE.MeshPhysicalMaterial({
          map: texture,
          transparent: true,
          alphaTest: 0.1,
          metalness: 0.8,
          roughness: 0.2,
          envMapIntensity: 1.2,
        });
        const label = new THREE.Mesh(labelGeometry, labelMaterial);
        label.position.set(stringPos, 0.015, midPos);
        label.lookAt(cameraRef.current?.position || new THREE.Vector3(0, 1, 1));
        markersGroup.add(label);
      }

      markersGroup.add(marker);
    });

    scene.add(markersGroup);
  };

  // Create capo
  const createCapo = (scene: THREE.Scene, instrument: InstrumentConfig, fretNumber: number) => {
    if (fretNumber <= 0) return;

    const { scaleLength, nutWidth } = instrument;
    const fretPositions = InstrumentGeometryFactory.calculateFretPositions(scaleLength, instrument.fretCount);
    const position = fretPositions[fretNumber] * InstrumentGeometryFactory.MM_TO_UNITS;

    // Try to load 3D capo model, fallback to geometric capo if it fails
    loadCapoModel({
      modelPath: '/models/guitar-capo.glb',
      scale: 0.05, // Smaller scale for MinimalThreeInstrument
      position: new THREE.Vector3(0, 0.008, position), // Above strings at fret position
      rotation: new THREE.Euler(0, 0, 0),
      color: 0x2C2C2C, // Dark gray/black to match original material
      metalness: 0.2,
      roughness: 0.4
    }).then((capoModel) => {
      console.log('✅ 3D capo model loaded successfully in MinimalThreeInstrument');
      if (instrumentGroupRef.current) {
        instrumentGroupRef.current.add(capoModel);
      }
    }).catch((error) => {
      console.warn('⚠️ Failed to load 3D capo model in MinimalThreeInstrument, using fallback geometry:', error);

      // Fallback to original geometric capo
      const capoGeometry = InstrumentGeometryFactory.createCapo(instrument, fretNumber);
      const capoMaterial = InstrumentMaterialFactory.createCapoMaterial();
      const capo = new THREE.Mesh(capoGeometry, capoMaterial);
      capo.castShadow = true;

      if (instrumentGroupRef.current) {
        instrumentGroupRef.current.add(capo);
      }
    });
  };

  // Setup lighting
  const setupLighting = (scene: THREE.Scene) => {
    // Ambient light - increased for better base illumination
    const ambientLight = new THREE.AmbientLight(0x606060, 0.5);
    scene.add(ambientLight);

    // Main directional light (key light) - from above-right
    const directionalLight = new THREE.DirectionalLight(0xffffff, 1.0);
    directionalLight.position.set(5, 10, 5);
    directionalLight.castShadow = true;
    directionalLight.shadow.mapSize.width = 2048;
    directionalLight.shadow.mapSize.height = 2048;
    directionalLight.shadow.camera.near = 0.5;
    directionalLight.shadow.camera.far = 50;
    directionalLight.shadow.camera.left = -5;
    directionalLight.shadow.camera.right = 5;
    directionalLight.shadow.camera.top = 5;
    directionalLight.shadow.camera.bottom = -5;
    scene.add(directionalLight);

    // Fill light - from left side
    const fillLight = new THREE.DirectionalLight(0xffffff, 0.4);
    fillLight.position.set(-5, 5, -5);
    scene.add(fillLight);

    // Rim light - from behind/below for edge highlights
    const rimLight = new THREE.DirectionalLight(0xffffff, 0.3);
    rimLight.position.set(0, -5, -10);
    scene.add(rimLight);

    // Additional top light for better fret/string visibility
    const topLight = new THREE.DirectionalLight(0xffffff, 0.6);
    topLight.position.set(0, 15, 0);
    scene.add(topLight);

    // Warm accent light from front-left (for metallic reflections)
    const accentLight = new THREE.DirectionalLight(0xffe0b0, 0.5);
    accentLight.position.set(-8, 6, 10);
    scene.add(accentLight);

    // Cool accent light from front-right (for contrast)
    const coolAccentLight = new THREE.DirectionalLight(0xa0b0ff, 0.3);
    coolAccentLight.position.set(8, 4, 8);
    scene.add(coolAccentLight);

    // Point light near fretboard for local illumination
    const pointLight = new THREE.PointLight(0xffffff, 0.4, 2);
    pointLight.position.set(0, 0.3, 0.3);
    scene.add(pointLight);
  };

  // Get camera position based on instrument and view mode
  const getCameraPosition = (instrument: InstrumentConfig, viewMode: string) => {
    const scaleLength = instrument.scaleLength * 0.001; // Convert to meters
    
    switch (viewMode) {
      case 'headstock':
        return { x: 0, y: 0.3, z: -0.2 };
      case 'full':
        return { x: 0, y: 0.5, z: scaleLength * 0.8 };
      case 'fretboard':
      default:
        return { x: 0, y: 0.4, z: scaleLength * 0.6 };
    }
  };

  // Handle canvas click for position interaction
  const handleCanvasClick = useCallback((event: React.MouseEvent<HTMLCanvasElement>) => {
    if (!onPositionClick || !cameraRef.current || !sceneRef.current) return;

    const canvas = canvasRef.current!;
    const rect = canvas.getBoundingClientRect();
    const x = ((event.clientX - rect.left) / rect.width) * 2 - 1;
    const y = -((event.clientY - rect.top) / rect.height) * 2 + 1;

    const raycaster = new THREE.Raycaster();
    raycaster.setFromCamera(new THREE.Vector2(x, y), cameraRef.current);

    const intersects = raycaster.intersectObjects(
      positionMarkersRef.current?.children || [],
      true
    );

    if (intersects.length > 0) {
      const userData = intersects[0].object.userData;
      if (userData.string !== undefined && userData.fret !== undefined) {
        onPositionClick(userData.string, userData.fret);
      }
    }
  }, [onPositionClick]);

  // Handle canvas hover for position interaction
  const handleCanvasMouseMove = useCallback((event: React.MouseEvent<HTMLCanvasElement>) => {
    if (!cameraRef.current || !sceneRef.current) return;

    const canvas = canvasRef.current!;
    const rect = canvas.getBoundingClientRect();
    const x = ((event.clientX - rect.left) / rect.width) * 2 - 1;
    const y = -((event.clientY - rect.top) / rect.height) * 2 + 1;

    const raycaster = new THREE.Raycaster();
    raycaster.setFromCamera(new THREE.Vector2(x, y), cameraRef.current);

    const intersects = raycaster.intersectObjects(
      positionMarkersRef.current?.children || [],
      true
    );

    if (intersects.length > 0) {
      const userData = intersects[0].object.userData;
      if (userData.string !== undefined && userData.fret !== undefined) {
        // Update hovered object for visual feedback
        if (hoveredObjectRef.current !== intersects[0].object) {
          // Reset previous hovered object
          if (hoveredObjectRef.current && hoveredObjectRef.current instanceof THREE.Mesh) {
            const material = hoveredObjectRef.current.material as THREE.MeshStandardMaterial;
            material.emissive.setHex(0x000000);
            material.emissiveIntensity = 0;
          }

          // Highlight new hovered object
          hoveredObjectRef.current = intersects[0].object;
          if (hoveredObjectRef.current instanceof THREE.Mesh) {
            const material = hoveredObjectRef.current.material as THREE.MeshStandardMaterial;
            material.emissive.setHex(0xffffff);
            material.emissiveIntensity = 0.3;
          }
        }

        // Update state and call callback
        const newHovered = { string: userData.string, fret: userData.fret };
        setHoveredPosition(newHovered);
        onPositionHover?.(userData.string, userData.fret);

        // Change cursor to pointer
        canvas.style.cursor = 'pointer';
      }
    } else {
      // Reset hover state
      if (hoveredObjectRef.current && hoveredObjectRef.current instanceof THREE.Mesh) {
        const material = hoveredObjectRef.current.material as THREE.MeshStandardMaterial;
        material.emissive.setHex(0x000000);
        material.emissiveIntensity = 0;
      }
      hoveredObjectRef.current = null;
      setHoveredPosition(null);
      onPositionHover?.(null, null);

      // Reset cursor
      canvas.style.cursor = enableOrbitControls ? 'grab' : 'default';
    }
  }, [onPositionHover, enableOrbitControls]);

  // Initialize on mount and when dependencies change
  useEffect(() => {
    initThree();
    return cleanup;
  }, [initThree, cleanup]);

  // Update position markers when positions change
  useEffect(() => {
    if (sceneRef.current && !isLoading && instrument) {
      try {
        createPositionMarkers(sceneRef.current, instrument, positions);
      } catch (error) {
        console.error('Error updating position markers:', error);
      }
    }
  }, [positions, instrument, isLoading, showLabels]);

  return (
    <Box>
      {title && (
        <Typography variant="h6" gutterBottom>
          {title}
        </Typography>
      )}
      
      {error && (
        <Alert severity="error" sx={{ mb: 2 }}>
          {error}
        </Alert>
      )}
      
      {isLoading && (
        <Alert severity="info" sx={{ mb: 2 }}>
          Loading {instrument.displayName}...
        </Alert>
      )}
      
      <Box sx={{ position: 'relative' }}>
        <canvas
          ref={canvasRef}
          width={width}
          height={height}
          style={{
            border: '1px solid #ddd',
            borderRadius: 4,
            cursor: enableOrbitControls ? 'grab' : 'default',
          }}
          onClick={handleCanvasClick}
          onMouseMove={handleCanvasMouseMove}
        />

        {/* Renderer info overlay */}
        <Box
          sx={{
            position: 'absolute',
            top: 8,
            right: 8,
            bgcolor: 'rgba(0,0,0,0.7)',
            color: 'white',
            px: 1,
            py: 0.5,
            borderRadius: 1,
            fontSize: '0.75rem',
          }}
        >
          {isWebGPU ? 'WebGPU' : 'WebGL'} | {instrument.tuning.length} strings
        </Box>

        {/* Hover position tooltip */}
        {hoveredPosition && (
          <Box
            sx={{
              position: 'absolute',
              bottom: 8,
              left: 8,
              bgcolor: 'rgba(0,0,0,0.8)',
              color: 'white',
              px: 1.5,
              py: 0.75,
              borderRadius: 1,
              fontSize: '0.875rem',
              pointerEvents: 'none',
            }}
          >
            String {hoveredPosition.string + 1}, Fret {hoveredPosition.fret}
          </Box>
        )}
      </Box>
    </Box>
  );
};

export default MinimalThreeInstrument;
