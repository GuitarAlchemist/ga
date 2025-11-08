import React, { useEffect, useRef, useState } from 'react';
import './FretboardWithHand.css';
import * as THREE from 'three';
import { OrbitControls } from 'three/examples/jsm/controls/OrbitControls.js';
import { GLTFLoader } from 'three/examples/jsm/loaders/GLTFLoader.js';
import { WebGPURenderer } from 'three/webgpu';
import { BSPApiService, VoicingWithAnalysis } from './BSP/BSPApiService';

export interface FingerPosition {
  finger: number; // 1=index, 2=middle, 3=ring, 4=pinky, 0=thumb
  string: number;
  fret: number;
}

export interface ChordVoicing {
  chordName: string;
  positions: Array<{ string: number; fret: number; finger?: number }>;
  difficulty: string;
}

interface FretboardWithHandProps {
  chordName?: string;
  apiBaseUrl?: string;
  width?: number;
  height?: number;
}

export const FretboardWithHand: React.FC<FretboardWithHandProps> = ({
  chordName = 'G',
  apiBaseUrl = 'https://localhost:7001',
  width = 1200,
  height = 600,
}) => {
  const canvasRef = useRef<HTMLCanvasElement>(null);
  const rendererRef = useRef<WebGPURenderer | THREE.WebGLRenderer | null>(null);
  const sceneRef = useRef<THREE.Scene | null>(null);
  const cameraRef = useRef<THREE.PerspectiveCamera | null>(null);
  const controlsRef = useRef<OrbitControls | null>(null);
  const handModelRef = useRef<THREE.Group | null>(null);
  const fretboardGroupRef = useRef<THREE.Group | null>(null);

  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [voicing, setVoicing] = useState<ChordVoicing | null>(null);
  const [isWebGPU, setIsWebGPU] = useState(false);

  // Fetch chord voicing from API using BSPApiService
  useEffect(() => {
    const fetchVoicing = async () => {
      try {
        setLoading(true);
        setError(null);

        console.log(`🎸 Fetching voicings for chord: ${chordName}`);

        // Use BSPApiService to fetch voicings
        const voicings = await BSPApiService.getVoicingsForChord(chordName, {
          maxDifficulty: 'Easy',
          limit: 1,
        });

        console.log(`✅ Received ${voicings.length} voicing(s) from API`);

        if (voicings && voicings.length > 0) {
          const firstVoicing = voicings[0];

          console.log('📊 Voicing data:', {
            positions: firstVoicing.positions,
            difficulty: firstVoicing.difficulty,
            notes: firstVoicing.notes,
            hasFingerAssignments: firstVoicing.positions.some(p => p.finger !== undefined),
          });

          // Convert API response to our format
          const convertedVoicing: ChordVoicing = {
            chordName: chordName,
            positions: firstVoicing.positions.filter(p => p.fret >= 0), // Filter out muted strings
            difficulty: firstVoicing.difficulty || 'Unknown',
          };

          console.log('🎯 Converted voicing:', convertedVoicing);
          setVoicing(convertedVoicing);
        } else {
          throw new Error('No voicings found for chord');
        }
      } catch (err) {
        console.error('❌ Error fetching voicing:', err);
        setError(err instanceof Error ? err.message : 'Unknown error');

        // Fallback to hardcoded open G chord with finger assignments
        console.log('🔄 Using fallback open G chord');
        setVoicing({
          chordName: 'G',
          positions: [
            { string: 1, fret: 3, finger: 3 }, // High E string, 3rd fret, ring finger
            { string: 2, fret: 2, finger: 2 }, // B string, 2nd fret, middle finger
            { string: 3, fret: 0, finger: 0 }, // G string, open
            { string: 4, fret: 0, finger: 0 }, // D string, open
            { string: 5, fret: 0, finger: 0 }, // A string, open
            { string: 6, fret: 3, finger: 4 }, // Low E string, 3rd fret, pinky
          ],
          difficulty: 'Easy',
        });
      } finally {
        setLoading(false);
      }
    };

    fetchVoicing();
  }, [chordName]);

  // Initialize Three.js scene
  useEffect(() => {
    if (!canvasRef.current || !voicing) return;

    let isMounted = true;
    const canvas = canvasRef.current;

    const initScene = async () => {
      try {
        // Create scene
        const scene = new THREE.Scene();
        scene.background = new THREE.Color(0x1a1a1a);
        sceneRef.current = scene;

        // Create camera
        const camera = new THREE.PerspectiveCamera(45, width / height, 0.1, 1000);
        camera.position.set(0, 15, 30);
        camera.lookAt(0, 0, 0);
        cameraRef.current = camera;

        // Try WebGPU first
        let renderer: WebGPURenderer | THREE.WebGLRenderer;
        const hasWebGPU = 'gpu' in navigator;

        if (hasWebGPU) {
          try {
            renderer = new WebGPURenderer({ canvas, antialias: true, alpha: true });
            await renderer.init();
            if (!isMounted) {
              renderer.dispose();
              return;
            }
            console.log('✅ Using WebGPU renderer');
            setIsWebGPU(true);
          } catch (error) {
            console.warn('WebGPU failed, falling back to WebGL:', error);
            renderer = new THREE.WebGLRenderer({ canvas, antialias: true, alpha: true });
          }
        } else {
          renderer = new THREE.WebGLRenderer({ canvas, antialias: true, alpha: true });
        }

        renderer.setSize(width, height);
        renderer.setPixelRatio(Math.min(window.devicePixelRatio, 2));
        rendererRef.current = renderer;

        // Add lights
        const ambientLight = new THREE.AmbientLight(0xffffff, 0.6);
        scene.add(ambientLight);

        const directionalLight = new THREE.DirectionalLight(0xffffff, 0.8);
        directionalLight.position.set(10, 20, 10);
        scene.add(directionalLight);

        const fillLight = new THREE.DirectionalLight(0x4488ff, 0.3);
        fillLight.position.set(-10, 5, -10);
        scene.add(fillLight);

        // Add orbit controls
        const controls = new OrbitControls(camera, canvas);
        controls.enableDamping = true;
        controls.dampingFactor = 0.05;
        controls.target.set(0, 0, 0);
        controlsRef.current = controls;

        // Create fretboard
        createFretboard(scene, voicing);

        // Load hand model
        await loadHandModel(scene, voicing);

        // Animation loop
        const animate = () => {
          if (!isMounted) return;
          requestAnimationFrame(animate);
          controls.update();
          renderer.render(scene, camera);
        };
        animate();

      } catch (err) {
        console.error('Error initializing scene:', err);
        setError(err instanceof Error ? err.message : 'Failed to initialize 3D scene');
      }
    };

    initScene();

    return () => {
      isMounted = false;
      controlsRef.current?.dispose();
      rendererRef.current?.dispose();
    };
  }, [voicing, width, height]);

  // Create fretboard geometry
  const createFretboard = (scene: THREE.Scene, voicing: ChordVoicing) => {
    const fretboardGroup = new THREE.Group();
    fretboardGroupRef.current = fretboardGroup;

    // Fretboard dimensions
    const fretboardLength = 20;
    const fretboardWidth = 6;
    const numFrets = 5;

    // Fretboard base
    const fretboardGeometry = new THREE.BoxGeometry(fretboardWidth, 0.5, fretboardLength);
    const fretboardMaterial = new THREE.MeshStandardMaterial({
      color: 0x3d2817,
      roughness: 0.8,
      metalness: 0.1,
    });
    const fretboardMesh = new THREE.Mesh(fretboardGeometry, fretboardMaterial);
    fretboardMesh.position.y = -0.25;
    fretboardGroup.add(fretboardMesh);

    // Frets
    const fretMaterial = new THREE.MeshStandardMaterial({ color: 0xc0c0c0, metalness: 0.8 });
    for (let i = 0; i <= numFrets; i++) {
      const fretGeometry = new THREE.CylinderGeometry(0.05, 0.05, fretboardWidth, 8);
      const fret = new THREE.Mesh(fretGeometry, fretMaterial);
      fret.rotation.z = Math.PI / 2;
      fret.position.z = -fretboardLength / 2 + (i * fretboardLength) / numFrets;
      fretboardGroup.add(fret);
    }

    // Strings
    const stringMaterial = new THREE.MeshStandardMaterial({ color: 0xffdd88, metalness: 0.9 });
    for (let i = 0; i < 6; i++) {
      const stringGeometry = new THREE.CylinderGeometry(0.02, 0.02, fretboardLength, 8);
      const string = new THREE.Mesh(stringGeometry, stringMaterial);
      string.rotation.x = Math.PI / 2;
      string.position.x = -fretboardWidth / 2 + (i * fretboardWidth) / 5 + fretboardWidth / 10;
      fretboardGroup.add(string);
    }

    // Position markers for chord with color coding by finger
    const fingerColors = [
      0x888888, // 0 = open (gray)
      0x4CAF50, // 1 = index (green)
      0x2196F3, // 2 = middle (blue)
      0xFF9800, // 3 = ring (orange)
      0xE91E63, // 4 = pinky (pink)
    ];

    voicing.positions.forEach((pos) => {
      if (pos.fret > 0) {
        const fingerColor = (pos.finger !== undefined && pos.finger >= 0 && pos.finger < fingerColors.length)
          ? fingerColors[pos.finger]
          : 0x4dabf7;

        const markerGeometry = new THREE.SphereGeometry(0.3, 16, 16);
        const markerMaterial = new THREE.MeshStandardMaterial({
          color: fingerColor,
          emissive: fingerColor,
          emissiveIntensity: 0.5,
          metalness: 0.3,
          roughness: 0.5,
        });
        const marker = new THREE.Mesh(markerGeometry, markerMaterial);

        const stringX = -fretboardWidth / 2 + ((pos.string - 1) * fretboardWidth) / 5 + fretboardWidth / 10;
        const fretZ = -fretboardLength / 2 + ((pos.fret - 0.5) * fretboardLength) / numFrets;

        marker.position.set(stringX, 0.5, fretZ);
        fretboardGroup.add(marker);
      }
    });

    scene.add(fretboardGroup);
  };

  // Create realistic hand model with proper guitar fretting pose
  const loadHandModel = async (scene: THREE.Scene, voicing: ChordVoicing) => {
    console.log('🖐️ Creating realistic hand with proper fretting pose');

    const fretboardWidth = 6;
    const fretboardLength = 20;
    const numStrings = 6;
    const numFrets = 5;

    const fingerColors = [
      0x888888, // 0 = open (gray)
      0x4CAF50, // 1 = index (green)
      0x2196F3, // 2 = middle (blue)
      0xFF9800, // 3 = ring (orange)
      0xE91E63, // 4 = pinky (pink)
    ];

    // Skin tone for palm and unused fingers
    const skinColor = 0xffdbac;

    // Calculate average fret position for hand placement
    const usedFrets = voicing.positions.filter(p => p.fret > 0).map(p => p.fret);
    const avgFret = usedFrets.length > 0 ? usedFrets.reduce((a, b) => a + b, 0) / usedFrets.length : 2.5;
    const avgString = voicing.positions.filter(p => p.fret > 0).length > 0
      ? voicing.positions.filter(p => p.fret > 0).reduce((sum, p) => sum + p.string, 0) / voicing.positions.filter(p => p.fret > 0).length
      : 3.5;

    // Hand coordinate system:
    // - Palm is BEHIND the neck (positive Z, away from viewer)
    // - Fingers curve AROUND from behind to press on top of fretboard
    // - Thumb is on BACK of neck (even more positive Z)

    const handZ = -fretboardLength / 2 + ((avgFret - 0.5) * fretboardLength) / numFrets;
    const handX = -fretboardWidth / 2 + ((avgString - 1) * fretboardWidth) / 5 + fretboardWidth / 10;

    // Create hand group
    const handGroup = new THREE.Group();

    // Palm - positioned BEHIND the neck (positive Z offset from fretboard)
    const palmWidth = 1.8;
    const palmHeight = 0.6;
    const palmDepth = 2.2;
    const palmZOffset = 0.8; // Much closer to neck - fingers can reach

    const palmGeometry = new THREE.BoxGeometry(palmWidth, palmHeight, palmDepth);
    const palmMaterial = new THREE.MeshStandardMaterial({
      color: skinColor,
      roughness: 0.7,
      metalness: 0.1,
    });
    const palm = new THREE.Mesh(palmGeometry, palmMaterial);
    palm.position.set(0, 0.3, palmZOffset); // Slightly above fretboard, behind neck
    handGroup.add(palm);

    console.log('🖐️ Created palm behind neck');

    // Helper function to create a finger that curves from palm to fret position
    const createFinger = (
      fingerIndex: number,
      palmXOffset: number, // X offset from palm center
      isActive: boolean,
      targetString?: number,
      targetFret?: number
    ) => {
      const fingerColor = isActive ? fingerColors[fingerIndex] : skinColor;

      // Phalanx dimensions - longer to reach frets
      const phalanxLengths = [1.5, 1.2, 0.9]; // Proximal, middle, distal (longer)
      const phalanxRadii = [0.18, 0.16, 0.14];
      const jointRadius = 0.20;

      const phalanxMaterial = new THREE.MeshStandardMaterial({
        color: fingerColor,
        emissive: isActive ? fingerColor : 0x000000,
        emissiveIntensity: isActive ? 0.3 : 0,
        metalness: 0.2,
        roughness: 0.6,
      });

      const jointMaterial = new THREE.MeshStandardMaterial({
        color: fingerColor,
        emissive: isActive ? fingerColor : 0x000000,
        emissiveIntensity: isActive ? 0.35 : 0,
        metalness: 0.25,
        roughness: 0.55,
      });

      // Finger group - will be positioned and rotated as a whole
      const fingerGroup = new THREE.Group();

      // Build finger from base to tip
      let currentY = 0;
      for (let i = 0; i < 3; i++) {
        const phalanxGeometry = new THREE.CylinderGeometry(phalanxRadii[i], phalanxRadii[i], phalanxLengths[i], 16);
        const phalanx = new THREE.Mesh(phalanxGeometry, phalanxMaterial);
        phalanx.position.y = currentY - phalanxLengths[i] / 2;
        fingerGroup.add(phalanx);
        currentY -= phalanxLengths[i];

        const jointGeometry = new THREE.SphereGeometry(jointRadius, 12, 12);
        const joint = new THREE.Mesh(jointGeometry, jointMaterial);
        joint.position.y = currentY;
        fingerGroup.add(joint);
      }

      // Fingertip
      const tipRadius = 0.22;
      const tipGeometry = new THREE.SphereGeometry(tipRadius, 16, 16);
      const tipMaterial = new THREE.MeshStandardMaterial({
        color: fingerColor,
        emissive: isActive ? fingerColor : 0x000000,
        emissiveIntensity: isActive ? 0.4 : 0,
        metalness: 0.3,
        roughness: 0.4,
      });
      const tip = new THREE.Mesh(tipGeometry, tipMaterial);
      tip.position.y = currentY - tipRadius * 0.5;
      fingerGroup.add(tip);

      // Position and orient finger
      if (isActive && targetString !== undefined && targetFret !== undefined) {
        // Calculate ABSOLUTE target position on fretboard (in world coordinates)
        const targetX = -fretboardWidth / 2 + ((targetString - 1) * fretboardWidth) / 5 + fretboardWidth / 10;
        const targetY = 0.1; // Just above fretboard surface
        const targetZ = -fretboardLength / 2 + ((targetFret - 0.5) * fretboardLength) / numFrets;

        // Finger starts from palm edge (in local hand group coordinates)
        const startX = palmXOffset;
        const startY = palm.position.y + palmHeight / 2;
        const startZ = palmZOffset + palmDepth / 2;

        // Calculate total finger length
        const totalFingerLength = phalanxLengths.reduce((a, b) => a + b, 0) + 0.3; // Include tip

        // Calculate vector from palm to target (accounting for hand group offset)
        const deltaX = targetX - handX - startX;
        const deltaY = targetY - startY;
        const deltaZ = targetZ - handZ - startZ;
        const distance = Math.sqrt(deltaX * deltaX + deltaY * deltaY + deltaZ * deltaZ);

        // Position finger at palm
        fingerGroup.position.set(startX, startY, startZ);

        // Calculate rotation to point finger at target
        // Fingers are built along negative Y axis (downward), so we need to rotate them to point at target

        // Calculate angles in spherical coordinates
        const horizontalDist = Math.sqrt(deltaX * deltaX + deltaZ * deltaZ);

        // Rotation around Z axis (roll/tilt left-right)
        fingerGroup.rotation.z = Math.atan2(deltaX, horizontalDist);

        // Rotation around X axis (pitch forward/back)
        // Negative deltaZ means forward (toward viewer), positive means back
        fingerGroup.rotation.x = Math.atan2(-deltaZ, -deltaY) - Math.PI / 2;

        console.log(`  Finger ${fingerIndex}: palm(${startX.toFixed(1)},${startY.toFixed(1)},${startZ.toFixed(1)}) -> target(${targetX.toFixed(1)},${targetY.toFixed(1)},${targetZ.toFixed(1)}) delta(${deltaX.toFixed(1)},${deltaY.toFixed(1)},${deltaZ.toFixed(1)}) dist=${distance.toFixed(1)} fingerLen=${totalFingerLength.toFixed(1)} rotX=${(fingerGroup.rotation.x * 180 / Math.PI).toFixed(1)}° rotZ=${(fingerGroup.rotation.z * 180 / Math.PI).toFixed(1)}°`);
      } else {
        // Relaxed finger
        fingerGroup.position.set(palmXOffset, palm.position.y + palmHeight / 2, palmZOffset + palmDepth / 2);
        fingerGroup.rotation.x = -Math.PI / 6; // Slight forward curve
      }

      return fingerGroup;
    };

    // Create thumb (on back of neck, opposite side from fingers)
    const createThumb = () => {
      const thumbColor = skinColor;
      const thumbLengths = [0.9, 0.7];
      const thumbRadii = [0.20, 0.18];
      const jointRadius = 0.22;

      const thumbMaterial = new THREE.MeshStandardMaterial({
        color: thumbColor,
        roughness: 0.7,
        metalness: 0.1,
      });

      const thumbGroup = new THREE.Group();
      let currentY = 0;

      for (let i = 0; i < 2; i++) {
        const phalanxGeometry = new THREE.CylinderGeometry(thumbRadii[i], thumbRadii[i], thumbLengths[i], 16);
        const phalanx = new THREE.Mesh(phalanxGeometry, thumbMaterial);
        phalanx.position.y = currentY - thumbLengths[i] / 2;
        thumbGroup.add(phalanx);
        currentY -= thumbLengths[i];

        const jointGeometry = new THREE.SphereGeometry(jointRadius, 12, 12);
        const joint = new THREE.Mesh(jointGeometry, thumbMaterial);
        joint.position.y = currentY;
        thumbGroup.add(joint);
      }

      const tipGeometry = new THREE.SphereGeometry(0.24, 16, 16);
      const tip = new THREE.Mesh(tipGeometry, thumbMaterial);
      tip.position.y = currentY - 0.15;
      thumbGroup.add(tip);

      // Thumb on BACK of neck (even further positive Z than palm)
      thumbGroup.position.set(-palmWidth / 3, palm.position.y, palmZOffset + palmDepth / 2 + 0.5);
      thumbGroup.rotation.x = Math.PI / 4; // Point backward/down
      thumbGroup.rotation.z = -Math.PI / 6; // Angle toward center

      return thumbGroup;
    };

    // Track which fingers are used
    const usedFingers = new Set(voicing.positions.filter(p => p.fret > 0 && p.finger).map(p => p.finger));

    // Create all 4 fingers - spread across palm width
    const fingerPositions = [
      { index: 1, palmXOffset: -0.6 },  // Index (leftmost)
      { index: 2, palmXOffset: -0.2 },  // Middle
      { index: 3, palmXOffset: 0.2 },   // Ring
      { index: 4, palmXOffset: 0.6 },   // Pinky (rightmost)
    ];

    fingerPositions.forEach(({ index, palmXOffset }) => {
      const isActive = usedFingers.has(index);
      const pos = voicing.positions.find(p => p.finger === index && p.fret > 0);
      const finger = createFinger(index, palmXOffset, isActive, pos?.string, pos?.fret);
      handGroup.add(finger);
      console.log(`🖐️ Created finger ${index} (${isActive ? 'active' : 'relaxed'}) ${pos ? `at string ${pos.string}, fret ${pos.fret}` : ''}`);
    });

    // Add thumb
    const thumb = createThumb();
    handGroup.add(thumb);
    console.log('🖐️ Created thumb on back of neck');

    // Position entire hand group at calculated position
    handGroup.position.set(handX, 0, handZ);
    scene.add(handGroup);

    console.log(`🖐️ Hand positioned at X=${handX.toFixed(2)}, Z=${handZ.toFixed(2)}`);
  };

  if (loading) {
    return (
      <div className="fretboard-hand__loading" style={{ height }}>
        <div className="fretboard-hand__spinner" aria-label="Loading" />
      </div>
    );
  }

  return (
    <section className="fretboard-hand">
      <header className="fretboard-hand__header">
        <h3>3D Fretboard with Hand - {voicing?.chordName || chordName} Chord</h3>
        <p>
          Difficulty: {voicing?.difficulty ?? 'Unknown'} | Renderer: {isWebGPU ? 'WebGPU' : 'WebGL'}
        </p>
      </header>
      {error && (
        <div className="fretboard-hand__alert" role="status">
          {error} (Using fallback chord)
        </div>
      )}
      <canvas
        ref={canvasRef}
        width={width}
        height={height}
        className="fretboard-hand__canvas"
      />
    </section>
  );
};

export default FretboardWithHand;
