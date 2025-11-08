// @ts-nocheck
import React, { useRef, useEffect, useState } from 'react';
import { Box, Typography } from '@mui/material';
import * as THREE from 'three';
import { WebGPURenderer } from 'three/webgpu';
import { OrbitControls } from 'three/examples/jsm/controls/OrbitControls.js';
import type { GuitarModelStyle, HeadstockStyle } from './GuitarModels';
import { GUITAR_MODELS } from './GuitarModels';

export interface ThreeHeadstockProps {
  title?: string;
  guitarModel?: string;
  headstockStyle?: HeadstockStyle;
  stringCount?: number;
  tuning?: string[];
  leftHanded?: boolean;
  width?: number;
  height?: number;
  enableOrbitControls?: boolean;
  onTuningPegClick?: (stringIndex: number, tuning: string) => void;
}

export const ThreeHeadstock: React.FC<ThreeHeadstockProps> = ({
  title = '3D Guitar Headstock (Three.js + WebGPU)',
  guitarModel = 'electric_fender_strat',
  headstockStyle,
  stringCount = 6,
  tuning = ['E', 'A', 'D', 'G', 'B', 'E'],
  leftHanded = false,
  width = 800,
  height = 600,
  enableOrbitControls = true,
  onTuningPegClick,
}) => {
  const canvasRef = useRef<HTMLCanvasElement>(null);
  const rendererRef = useRef<WebGPURenderer | null>(null);
  const sceneRef = useRef<THREE.Scene | null>(null);
  const cameraRef = useRef<THREE.PerspectiveCamera | null>(null);
  const controlsRef = useRef<OrbitControls | null>(null);
  const headstockGroupRef = useRef<THREE.Group | null>(null);
  
  const [isWebGPU, setIsWebGPU] = useState(false);
  const [error, setError] = useState<string | null>(null);

  // Get guitar style configuration
  const guitarStyle = GUITAR_MODELS[guitarModel] || GUITAR_MODELS['electric_fender_strat'];
  const finalHeadstockStyle = headstockStyle || guitarStyle.headstockStyle || 'electric';

  useEffect(() => {
    let animationId: number;
    let isDisposed = false;

    const initThree = async () => {
      if (!canvasRef.current) return;

      try {
        // Dispose previous renderer
        if (rendererRef.current) {
          rendererRef.current.dispose();
          rendererRef.current = null;
        }

        await new Promise(resolve => setTimeout(resolve, 100));

        const canvas = canvasRef.current;

        // Check WebGPU support
        if (!('gpu' in navigator)) {
          throw new Error('WebGPU is not supported in this browser. Please use Chrome 113+ or Edge 113+');
        }

        // Create WebGPU renderer
        const renderer = new WebGPURenderer({
          canvas,
          antialias: true,
          alpha: true,
          forceWebGL: false,
          samples: 4,
        });
        
        await renderer.init();
        console.log('✅ ThreeHeadstock: Using WebGPU renderer');
        
        setIsWebGPU(true);
        renderer.setSize(width, height);
        renderer.setPixelRatio(Math.min(window.devicePixelRatio * 2, 4));
        renderer.setClearColor(0x2a2a2a, 1);
        rendererRef.current = renderer;

        // Create scene
        const scene = new THREE.Scene();
        scene.background = new THREE.Color(0x2a2a2a);
        sceneRef.current = scene;

        // Create camera
        const camera = new THREE.PerspectiveCamera(45, width / height, 0.1, 1000);
        camera.position.set(0, 8, 15);
        camera.lookAt(0, 0, 0);
        cameraRef.current = camera;

        // Add lighting
        const ambientLight = new THREE.AmbientLight(0x404040, 0.6);
        scene.add(ambientLight);

        const directionalLight = new THREE.DirectionalLight(0xffffff, 1.0);
        directionalLight.position.set(10, 10, 5);
        directionalLight.castShadow = true;
        directionalLight.shadow.mapSize.width = 2048;
        directionalLight.shadow.mapSize.height = 2048;
        scene.add(directionalLight);

        const fillLight = new THREE.DirectionalLight(0x8080ff, 0.3);
        fillLight.position.set(-5, 5, -5);
        scene.add(fillLight);

        // Create orbit controls
        if (enableOrbitControls) {
          const controls = new OrbitControls(camera, canvas);
          controls.enableDamping = true;
          controls.dampingFactor = 0.05;
          controls.target.set(0, 0, 0);
          controls.minDistance = 5;
          controls.maxDistance = 50;
          controlsRef.current = controls;
        }

        // Create headstock group
        const headstockGroup = new THREE.Group();
        scene.add(headstockGroup);
        headstockGroupRef.current = headstockGroup;

        // Create headstock geometry
        createHeadstock(headstockGroup, guitarStyle, finalHeadstockStyle, stringCount, tuning, leftHanded);

        // Add mouse interaction for tuning pegs
        if (onTuningPegClick) {
          setupMouseInteraction(canvas, scene, camera, onTuningPegClick);
        }

        // Animation loop
        const animate = () => {
          if (isDisposed) return;

          animationId = requestAnimationFrame(animate);

          if (controlsRef.current) {
            controlsRef.current.update();
          }

          if (rendererRef.current && sceneRef.current && cameraRef.current) {
            rendererRef.current.render(sceneRef.current, cameraRef.current);
          }
        };

        animate();
        setError(null);

      } catch (err) {
        console.error('ThreeHeadstock initialization failed:', err);
        setError(err instanceof Error ? err.message : 'Failed to initialize 3D renderer');
      }
    };

    initThree();

    return () => {
      isDisposed = true;
      if (animationId) {
        cancelAnimationFrame(animationId);
      }
      if (controlsRef.current) {
        controlsRef.current.dispose();
      }
      if (rendererRef.current) {
        rendererRef.current.dispose();
        rendererRef.current = null;
      }
    };
  }, [guitarModel, finalHeadstockStyle, stringCount, tuning, leftHanded, width, height, enableOrbitControls]);

  if (error) {
    return (
      <Box sx={{ p: 2, border: '1px solid red', borderRadius: 1 }}>
        <Typography variant="h6" color="error" gutterBottom>
          {title}
        </Typography>
        <Typography color="error">
          Error: {error}
        </Typography>
      </Box>
    );
  }

  return (
    <Box sx={{ p: 2 }}>
      {title && (
        <Typography variant="h6" gutterBottom>
          {title} {isWebGPU && '(WebGPU)'}
        </Typography>
      )}
      <canvas
        ref={canvasRef}
        width={width}
        height={height}
        style={{ 
          border: '1px solid #ddd', 
          borderRadius: 4,
          display: 'block',
          maxWidth: '100%',
          height: 'auto'
        }}
      />
      <Typography variant="caption" color="text.secondary" sx={{ mt: 1, display: 'block' }}>
        Model: {guitarStyle.name} | Style: {finalHeadstockStyle} | Strings: {stringCount}
      </Typography>
    </Box>
  );
};

// Helper function to create headstock geometry
function createHeadstock(
  parent: THREE.Group,
  guitarStyle: GuitarModelStyle,
  headstockStyle: HeadstockStyle,
  stringCount: number,
  tuning: string[],
  leftHanded: boolean
): void {
  // Headstock dimensions (in Three.js units, 1 unit ≈ 10mm)
  const nutWidth = (guitarStyle.nutWidth || 43) / 10;
  
  // Create wood texture
  const woodTexture = createWoodTexture();
  
  // Create headstock based on style
  switch (headstockStyle) {
    case 'electric':
      createElectricHeadstock(parent, guitarStyle, nutWidth, stringCount, tuning, leftHanded, woodTexture);
      break;
    case 'acoustic':
      createAcousticHeadstock(parent, guitarStyle, nutWidth, stringCount, tuning, leftHanded, woodTexture);
      break;
    case 'classical':
      createClassicalHeadstock(parent, guitarStyle, nutWidth, stringCount, tuning, leftHanded, woodTexture);
      break;
    default:
      // Default to electric style
      createElectricHeadstock(parent, guitarStyle, nutWidth, stringCount, tuning, leftHanded, woodTexture);
  }
}

// Helper function to create wood texture
function createWoodTexture(): THREE.Texture {
  const canvas = document.createElement('canvas');
  canvas.width = 512;
  canvas.height = 512;
  const ctx = canvas.getContext('2d')!;
  
  // Create wood grain pattern
  const gradient = ctx.createLinearGradient(0, 0, 0, 512);
  gradient.addColorStop(0, '#8B4513');
  gradient.addColorStop(0.3, '#A0522D');
  gradient.addColorStop(0.7, '#8B4513');
  gradient.addColorStop(1, '#654321');
  
  ctx.fillStyle = gradient;
  ctx.fillRect(0, 0, 512, 512);
  
  // Add wood grain lines
  ctx.strokeStyle = 'rgba(139, 69, 19, 0.3)';
  ctx.lineWidth = 1;
  for (let i = 0; i < 20; i++) {
    ctx.beginPath();
    ctx.moveTo(0, i * 25 + Math.random() * 10);
    ctx.lineTo(512, i * 25 + Math.random() * 10);
    ctx.stroke();
  }
  
  const texture = new THREE.CanvasTexture(canvas);
  texture.wrapS = THREE.RepeatWrapping;
  texture.wrapT = THREE.RepeatWrapping;
  texture.repeat.set(2, 2);
  
  return texture;
}

// Electric guitar headstock (6 tuners in line - Fender style)
function createElectricHeadstock(
  parent: THREE.Group,
  guitarStyle: GuitarModelStyle,
  nutWidth: number,
  stringCount: number,
  tuning: string[],
  leftHanded: boolean,
  woodTexture: THREE.Texture
): void {
  // Headstock dimensions
  const headstockLength = nutWidth * 3.5; // Length from nut
  const headstockWidth = nutWidth * 0.8;  // Width
  const headstockThickness = 0.8;         // Thickness

  // Create realistic Fender headstock shape using ExtrudeGeometry
  const headstockShape = createFenderHeadstockShape(headstockLength, headstockWidth);
  const extrudeSettings = {
    depth: headstockThickness,
    bevelEnabled: true,
    bevelThickness: 0.02,
    bevelSize: 0.02,
    bevelSegments: 3,
  };

  const headstockGeometry = new THREE.ExtrudeGeometry(headstockShape, extrudeSettings);
  const headstockMaterial = new THREE.MeshStandardMaterial({
    color: guitarStyle.woodColor,
    map: woodTexture,
    roughness: 0.4,
    metalness: 0.0,
  });

  const headstock = new THREE.Mesh(headstockGeometry, headstockMaterial);
  headstock.position.set(0, -headstockThickness / 2, 0);
  headstock.castShadow = true;
  headstock.receiveShadow = true;
  parent.add(headstock);

  // Add tuning pegs (6 in line) - positioned according to Fender specifications
  // Fender tuning pegs are spaced approximately 0.625" (15.9mm) apart
  const pegSpacing = 1.59; // 15.9mm in our units (1 unit ≈ 10mm)
  const totalPegSpan = pegSpacing * (stringCount - 1);
  const pegStartZ = -totalPegSpan / 2; // Center the pegs

  for (let i = 0; i < stringCount; i++) {
    const pegZ = pegStartZ + i * pegSpacing;
    // Position pegs at the appropriate distance from nut (typical Fender headstock)
    const pegX = -headstockLength * 0.78; // Positioned towards the end of headstock

    createTuningPeg(parent, pegX, 0, pegZ, tuning[i], leftHanded, i);
  }

  // Add guitar strings
  addGuitarStrings(parent, nutWidth, headstockLength, stringCount, tuning, leftHanded);

  // Add headstock logo/brand with better styling
  addFenderLogo(parent, guitarStyle.brand, -headstockLength * 0.3, 0.5, 0);
}

// Acoustic guitar headstock (3x3 tuners - Gibson/Martin style)
function createAcousticHeadstock(
  parent: THREE.Group,
  guitarStyle: GuitarModelStyle,
  nutWidth: number,
  stringCount: number,
  tuning: string[],
  leftHanded: boolean,
  woodTexture: THREE.Texture
): void {
  // Headstock dimensions with angled top
  const headstockLength = nutWidth * 2.8;
  const headstockWidth = nutWidth * 0.9;
  const headstockThickness = 0.8;

  // Create angled headstock shape
  const shape = new THREE.Shape();
  shape.moveTo(0, -headstockWidth / 2);
  shape.lineTo(-headstockLength * 0.8, -headstockWidth / 2);
  shape.lineTo(-headstockLength, -headstockWidth * 0.3);
  shape.lineTo(-headstockLength, headstockWidth * 0.3);
  shape.lineTo(-headstockLength * 0.8, headstockWidth / 2);
  shape.lineTo(0, headstockWidth / 2);
  shape.lineTo(0, -headstockWidth / 2);

  const extrudeSettings = {
    depth: headstockThickness,
    bevelEnabled: false,
  };

  const headstockGeometry = new THREE.ExtrudeGeometry(shape, extrudeSettings);
  const headstockMaterial = new THREE.MeshStandardMaterial({
    color: guitarStyle.woodColor,
    map: woodTexture,
    roughness: 0.4,
    metalness: 0.0,
  });

  const headstock = new THREE.Mesh(headstockGeometry, headstockMaterial);
  headstock.position.set(0, -headstockThickness / 2, 0);
  headstock.castShadow = true;
  headstock.receiveShadow = true;
  parent.add(headstock);

  // Add tuning pegs (3x3 configuration)
  const pegSpacing = nutWidth * 0.6 / 2;
  const pegDepth = -headstockLength * 0.6;

  for (let i = 0; i < Math.min(stringCount, 6); i++) {
    const side = i < 3 ? -1 : 1; // Left side for first 3, right side for last 3
    const pegZ = side * nutWidth * 0.25;
    const pegY = (i % 3 - 1) * pegSpacing;

    createTuningPeg(parent, pegDepth, pegY, pegZ, tuning[i], leftHanded, i);
  }

  // Add headstock logo
  addHeadstockLogo(parent, guitarStyle.brand, -headstockLength * 0.4, 0.5, 0);
}

// Classical guitar slotted headstock
function createClassicalHeadstock(
  parent: THREE.Group,
  guitarStyle: GuitarModelStyle,
  nutWidth: number,
  stringCount: number,
  tuning: string[],
  leftHanded: boolean,
  woodTexture: THREE.Texture
): void {
  // Classical headstock extends upward
  const headstockLength = nutWidth * 1.2;
  const headstockHeight = nutWidth * 2.5;
  const headstockThickness = 0.6;

  // Create main headstock body
  const headstockGeometry = new THREE.BoxGeometry(headstockLength, headstockHeight, headstockThickness);
  const headstockMaterial = new THREE.MeshStandardMaterial({
    color: guitarStyle.woodColor,
    map: woodTexture,
    roughness: 0.4,
    metalness: 0.0,
  });

  const headstock = new THREE.Mesh(headstockGeometry, headstockMaterial);
  headstock.position.set(-headstockLength / 2, headstockHeight / 2, 0);
  headstock.castShadow = true;
  headstock.receiveShadow = true;
  parent.add(headstock);

  // Add slots for strings
  const slotSpacing = headstockHeight * 0.8 / (stringCount - 1);
  const slotStartY = headstockHeight * 0.1;

  for (let i = 0; i < stringCount; i++) {
    const slotY = slotStartY + i * slotSpacing;
    const slotX = -headstockLength / 2;

    // Create string slot
    const slotGeometry = new THREE.BoxGeometry(headstockLength * 0.8, 0.1, 0.05);
    const slotMaterial = new THREE.MeshStandardMaterial({ color: 0x1a1a1a });
    const slot = new THREE.Mesh(slotGeometry, slotMaterial);
    slot.position.set(slotX, slotY, 0);
    parent.add(slot);

    // Add tuning peg at the back
    createClassicalTuningPeg(parent, slotX - headstockLength * 0.3, slotY, 0, tuning[i], leftHanded);
  }
}

// Create individual tuning peg with enhanced details
function createTuningPeg(
  parent: THREE.Group,
  x: number,
  y: number,
  z: number,
  note: string,
  leftHanded: boolean,
  stringIndex?: number
): void {
  const pegGroup = new THREE.Group();
  pegGroup.userData = { type: 'tuningPeg', note, position: { x, y, z }, stringIndex };

  // Peg shaft (chrome/nickel finish)
  const shaftGeometry = new THREE.CylinderGeometry(0.08, 0.08, 1.2);
  const shaftMaterial = new THREE.MeshStandardMaterial({
    color: 0x2a2a2a,
    metalness: 0.9,
    roughness: 0.1,
    envMapIntensity: 1.0,
  });
  const shaft = new THREE.Mesh(shaftGeometry, shaftMaterial);
  shaft.rotation.z = Math.PI / 2;
  pegGroup.add(shaft);

  // Tuning button (vintage-style cream/white)
  const buttonGeometry = new THREE.CylinderGeometry(0.15, 0.15, 0.3);
  const buttonMaterial = new THREE.MeshStandardMaterial({
    color: 0xf5f5dc,
    roughness: 0.6,
    metalness: 0.0,
  });
  const button = new THREE.Mesh(buttonGeometry, buttonMaterial);
  button.position.x = leftHanded ? 0.8 : -0.8;
  button.rotation.z = Math.PI / 2;
  button.userData = { clickable: true, note };
  pegGroup.add(button);

  // String post with more detail
  const postGeometry = new THREE.CylinderGeometry(0.05, 0.05, 0.4);
  const post = new THREE.Mesh(postGeometry, shaftMaterial);
  post.position.x = 0.3;
  pegGroup.add(post);

  // Add string winding detail
  const windingGeometry = new THREE.TorusGeometry(0.06, 0.01, 4, 8);
  const winding = new THREE.Mesh(windingGeometry, shaftMaterial);
  winding.position.x = 0.3;
  winding.rotation.x = Math.PI / 2;
  pegGroup.add(winding);

  // Add small screw detail on tuning button
  const screwGeometry = new THREE.CylinderGeometry(0.02, 0.02, 0.05);
  const screwMaterial = new THREE.MeshStandardMaterial({
    color: 0x1a1a1a,
    metalness: 0.8,
    roughness: 0.3,
  });
  const screw = new THREE.Mesh(screwGeometry, screwMaterial);
  screw.position.x = leftHanded ? 0.8 : -0.8;
  screw.rotation.z = Math.PI / 2;
  pegGroup.add(screw);

  pegGroup.position.set(x, y, z);
  parent.add(pegGroup);
}


// Create classical tuning peg (different style)
function createClassicalTuningPeg(
  parent: THREE.Group,
  x: number,
  y: number,
  z: number,
  note: string,
  leftHanded: boolean
): void {
  const pegGroup = new THREE.Group();

  // Classical peg body
  const bodyGeometry = new THREE.BoxGeometry(0.6, 0.2, 0.2);
  const bodyMaterial = new THREE.MeshStandardMaterial({
    color: 0x8B4513,
    roughness: 0.4,
    metalness: 0.0,
  });
  const body = new THREE.Mesh(bodyGeometry, bodyMaterial);
  pegGroup.add(body);

  // Peg handle
  const handleGeometry = new THREE.CylinderGeometry(0.08, 0.08, 0.8);
  const handleMaterial = new THREE.MeshStandardMaterial({
    color: 0x654321,
    roughness: 0.5,
    metalness: 0.0,
  });
  const handle = new THREE.Mesh(handleGeometry, handleMaterial);
  handle.rotation.x = Math.PI / 2;
  handle.position.z = leftHanded ? -0.5 : 0.5;
  pegGroup.add(handle);

  pegGroup.position.set(x, y, z);
  parent.add(pegGroup);
}

// Add headstock logo/brand text
function addHeadstockLogo(
  parent: THREE.Group,
  brand: string,
  x: number,
  y: number,
  z: number
): void {
  // For now, just add a simple geometric logo placeholder
  // In a real implementation, you'd load a font and create text geometry
  const logoGeometry = new THREE.BoxGeometry(0.8, 0.1, 0.02);
  const logoMaterial = new THREE.MeshStandardMaterial({
    color: 0xffd700,
    metalness: 0.3,
    roughness: 0.7,
  });
  const logo = new THREE.Mesh(logoGeometry, logoMaterial);
  logo.position.set(x, y, z);
  parent.add(logo);
}

// Create realistic Fender headstock shape
function createFenderHeadstockShape(length: number, width: number): THREE.Shape {
  const shape = new THREE.Shape();

  // Start at the nut end (right side)
  shape.moveTo(0, -width / 2);

  // Bottom edge - slight curve outward
  shape.lineTo(-length * 0.3, -width / 2);
  shape.quadraticCurveTo(-length * 0.5, -width * 0.55, -length * 0.7, -width * 0.5);

  // Transition to headstock end
  shape.lineTo(-length * 0.85, -width * 0.45);
  shape.quadraticCurveTo(-length * 0.95, -width * 0.4, -length, -width * 0.3);

  // Top of headstock (rounded end)
  shape.quadraticCurveTo(-length * 1.02, 0, -length, width * 0.3);

  // Top edge back to nut
  shape.quadraticCurveTo(-length * 0.95, width * 0.4, -length * 0.85, width * 0.45);
  shape.lineTo(-length * 0.7, width * 0.5);
  shape.quadraticCurveTo(-length * 0.5, width * 0.55, -length * 0.3, width / 2);
  shape.lineTo(0, width / 2);

  // Close the shape
  shape.lineTo(0, -width / 2);

  return shape;
}

// Add guitar strings from nut to tuning pegs
function addGuitarStrings(
  parent: THREE.Group,
  nutWidth: number,
  headstockLength: number,
  stringCount: number,
  tuning: string[],
  leftHanded: boolean
): void {
  // Strings should follow the same path as the tuning pegs
  const pegSpacing = 1.59; // Match the peg spacing exactly
  const totalPegSpan = pegSpacing * (stringCount - 1);
  const stringStartZ = -totalPegSpan / 2; // Center the strings to match pegs

  for (let i = 0; i < stringCount; i++) {
    const stringZ = stringStartZ + i * pegSpacing;
    const pegX = -headstockLength * 0.78; // Match the peg position exactly

    // Create string geometry - from nut (x=0) to tuning peg
    const stringLength = Math.abs(pegX); // Distance from nut to peg
    const stringGeometry = new THREE.CylinderGeometry(0.008, 0.008, stringLength);
    const stringMaterial = new THREE.MeshStandardMaterial({
      color: 0xc0c0c0, // Silver color
      metalness: 0.8,
      roughness: 0.1,
    });

    const string = new THREE.Mesh(stringGeometry, stringMaterial);
    string.rotation.z = Math.PI / 2;
    string.position.set(pegX / 2, 0.05, stringZ); // Centered between nut and peg, slightly above headstock surface
    parent.add(string);
  }
}

// Enhanced Fender logo
function addFenderLogo(
  parent: THREE.Group,
  brand: string,
  x: number,
  y: number,
  z: number
): void {
  // Create a more detailed Fender-style logo
  const logoGroup = new THREE.Group();

  // Main logo plate
  const plateGeometry = new THREE.BoxGeometry(1.2, 0.3, 0.02);
  const plateMaterial = new THREE.MeshStandardMaterial({
    color: 0x2a2a2a, // Dark color for contrast
    metalness: 0.1,
    roughness: 0.8,
  });
  const plate = new THREE.Mesh(plateGeometry, plateMaterial);
  logoGroup.add(plate);

  // Logo text representation (simplified geometric shapes)
  if (brand.toLowerCase().includes('fender')) {
    // Create stylized "F" shape
    const fGeometry = new THREE.BoxGeometry(0.15, 0.2, 0.01);
    const fMaterial = new THREE.MeshStandardMaterial({
      color: 0xffd700, // Gold color
      metalness: 0.3,
      roughness: 0.7,
    });
    const fLetter = new THREE.Mesh(fGeometry, fMaterial);
    fLetter.position.set(-0.3, 0, 0.015);
    logoGroup.add(fLetter);

    // Add horizontal bars for the "F"
    const barGeometry = new THREE.BoxGeometry(0.08, 0.03, 0.01);
    const topBar = new THREE.Mesh(barGeometry, fMaterial);
    topBar.position.set(-0.26, 0.05, 0.015);
    logoGroup.add(topBar);

    const midBar = new THREE.Mesh(barGeometry, fMaterial);
    midBar.position.set(-0.28, -0.02, 0.015);
    logoGroup.add(midBar);
  }

  logoGroup.position.set(x, y, z);
  parent.add(logoGroup);
}



// Setup mouse interaction for tuning pegs
function setupMouseInteraction(
  canvas: HTMLCanvasElement,
  scene: THREE.Scene,
  camera: THREE.PerspectiveCamera,
  onTuningPegClick: (stringIndex: number, tuning: string) => void
): void {
  const raycaster = new THREE.Raycaster();
  const mouse = new THREE.Vector2();

  const handleClick = (event: MouseEvent) => {
    const rect = canvas.getBoundingClientRect();
    mouse.x = ((event.clientX - rect.left) / rect.width) * 2 - 1;
    mouse.y = -((event.clientY - rect.top) / rect.height) * 2 + 1;

    raycaster.setFromCamera(mouse, camera);
    const intersects = raycaster.intersectObjects(scene.children, true);

    for (const intersect of intersects) {
      const object = intersect.object;

      // Check if clicked object is a tuning peg button
      if (object.userData.clickable && object.userData.note) {
        // Find the string index based on the tuning peg
        const pegGroup = object.parent;
        if (pegGroup && pegGroup.userData.type === 'tuningPeg') {
          // Use the stored string index if available, otherwise calculate from position
          const stringIndex = pegGroup.userData.stringIndex ?? 0;
          onTuningPegClick(stringIndex, object.userData.note);

          // Visual feedback - briefly change color
          if (object instanceof THREE.Mesh && object.material instanceof THREE.MeshStandardMaterial) {
            const originalColor = object.material.color.getHex();
            object.material.color.setHex(0xffff00); // Yellow
            setTimeout(() => {
              if (object instanceof THREE.Mesh && object.material instanceof THREE.MeshStandardMaterial) {
                object.material.color.setHex(originalColor);
              }
            }, 200);
          }
        }
        break;
      }
    }
  };

  canvas.addEventListener('click', handleClick);
}

export default ThreeHeadstock;
