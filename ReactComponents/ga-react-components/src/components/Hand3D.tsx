import React, { useEffect, useRef } from 'react';
import * as THREE from 'three';

interface Hand3DProps {
  positions: Array<{ string: number; fret: number }>;
  playabilityScore?: number;
}

// Helper function to create a finger segment with joint
const createFingerSegment = (
  length: number,
  radius: number,
  material: THREE.Material
): THREE.Group => {
  const segment = new THREE.Group();

  // Bone
  const boneGeometry = new THREE.CylinderGeometry(radius * 0.9, radius, length, 12);
  const bone = new THREE.Mesh(boneGeometry, material);
  bone.position.y = length / 2;
  segment.add(bone);

  // Joint sphere at the top
  const jointGeometry = new THREE.SphereGeometry(radius * 1.1, 12, 12);
  const jointMaterial = new THREE.MeshPhongMaterial({
    color: 0xffccaa,
    shininess: 50,
  });
  const joint = new THREE.Mesh(jointGeometry, jointMaterial);
  joint.position.y = length;
  segment.add(joint);

  return segment;
};

export const Hand3D: React.FC<Hand3DProps> = ({ positions, playabilityScore }) => {
  const containerRef = useRef<HTMLDivElement>(null);
  const sceneRef = useRef<THREE.Scene | null>(null);
  const rendererRef = useRef<THREE.WebGLRenderer | null>(null);
  const cameraRef = useRef<THREE.PerspectiveCamera | null>(null);
  const handGroupRef = useRef<THREE.Group | null>(null);

  useEffect(() => {
    if (!containerRef.current) return;

    // Scene setup
    const scene = new THREE.Scene();
    scene.background = new THREE.Color(0x0a0a0a);
    sceneRef.current = scene;

    // Camera setup
    const camera = new THREE.PerspectiveCamera(
      60,
      containerRef.current.clientWidth / containerRef.current.clientHeight,
      0.1,
      1000
    );
    camera.position.set(6, 5, 8);
    camera.lookAt(0, 0, 0);
    cameraRef.current = camera;

    // Renderer setup
    const renderer = new THREE.WebGLRenderer({ antialias: true });
    renderer.setSize(containerRef.current.clientWidth, containerRef.current.clientHeight);
    renderer.shadowMap.enabled = true;
    renderer.shadowMap.type = THREE.PCFSoftShadowMap;
    containerRef.current.appendChild(renderer.domElement);
    rendererRef.current = renderer;

    // Lighting - dramatic setup
    const ambientLight = new THREE.AmbientLight(0x404040, 0.4);
    scene.add(ambientLight);

    const mainLight = new THREE.DirectionalLight(0xffffff, 1.2);
    mainLight.position.set(10, 15, 10);
    mainLight.castShadow = true;
    mainLight.shadow.mapSize.width = 2048;
    mainLight.shadow.mapSize.height = 2048;
    scene.add(mainLight);

    const fillLight = new THREE.DirectionalLight(0x8888ff, 0.4);
    fillLight.position.set(-8, 5, -5);
    scene.add(fillLight);

    const rimLight = new THREE.DirectionalLight(0xffaa88, 0.6);
    rimLight.position.set(0, 5, -10);
    scene.add(rimLight);

    // Create hand group
    const handGroup = new THREE.Group();
    handGroupRef.current = handGroup;
    scene.add(handGroup);

    // Position hand above fretboard in playing position
    handGroup.position.set(0, 2, -2);
    handGroup.rotation.x = Math.PI / 3; // Tilt hand toward fretboard (60 degrees)
    handGroup.rotation.y = -Math.PI / 12; // Slight angle

    // Create realistic palm shape (wider at base, narrower at fingers)
    const palmMaterial = new THREE.MeshPhongMaterial({
      color: 0xffdbac,
      shininess: 30
    });

    // Main palm body (slightly curved)
    const palmShape = new THREE.Shape();
    palmShape.moveTo(-2, -2.5);
    palmShape.lineTo(-2, 2);
    palmShape.quadraticCurveTo(-1.5, 2.5, 0, 2.5);
    palmShape.quadraticCurveTo(1.5, 2.5, 2, 2);
    palmShape.lineTo(2, -2.5);
    palmShape.quadraticCurveTo(1.5, -3, 0, -3);
    palmShape.quadraticCurveTo(-1.5, -3, -2, -2.5);

    const extrudeSettings = {
      depth: 0.8,
      bevelEnabled: true,
      bevelThickness: 0.2,
      bevelSize: 0.2,
      bevelSegments: 3
    };

    const palmGeometry = new THREE.ExtrudeGeometry(palmShape, extrudeSettings);
    const palm = new THREE.Mesh(palmGeometry, palmMaterial);
    palm.rotation.x = Math.PI / 2;
    palm.position.y = -0.4;
    palm.castShadow = true;
    handGroup.add(palm);

    // Calculate fret positions using proper guitar fret formula
    // Formula: distance from nut = scaleLength * (1 - 1 / 2^(fret/12))
    const SCALE_LENGTH = 65; // Scale length in scene units (650mm / 10)
    const NUT_WIDTH = 4.3; // Nut width in scene units (43mm / 10)

    // Helper function to calculate fret position
    const calculateFretPosition = (fretNumber: number): number => {
      if (fretNumber === 0) return 0;
      const ratio = Math.pow(2, fretNumber / 12);
      return SCALE_LENGTH * (1 - 1 / ratio);
    };

    // Create simple fingers that reach to fret positions
    const fingerMaterial = new THREE.MeshPhongMaterial({
      color: 0xffdbac,
      shininess: 40
    });

    // Map each position to a finger
    positions.forEach((pos, index) => {
      if (!pos || pos.fret === undefined) return;

      // Skip open strings (fret 0) - no finger needed
      if (pos.fret === 0) return;

      // Calculate fretboard position
      const stringZ = (pos.string / 5 - 0.5) * NUT_WIDTH * 0.85; // Spread strings across nut width
      const fretPos = calculateFretPosition(pos.fret);
      const prevFretPos = pos.fret > 0 ? calculateFretPosition(pos.fret - 1) : 0;
      const fretX = (fretPos + prevFretPos) / 2 - SCALE_LENGTH / 2; // Position between frets

      // Finger base position on palm (spread across palm width)
      const fingerBaseX = -1.5 + (index * 1.0);
      const fingerBaseZ = 2.5;

      // Create finger pointing down to fret
      const fingerGroup = new THREE.Group();
      fingerGroup.position.set(fingerBaseX, 0, fingerBaseZ);

      // Calculate direction to target
      const targetPos = new THREE.Vector3(fretX, -2, stringZ);
      const startPos = new THREE.Vector3(fingerBaseX, 2, fingerBaseZ - 2);
      const direction = new THREE.Vector3().subVectors(targetPos, startPos);
      const distance = direction.length();

      // Create finger as simple curved segments
      const segment1 = createFingerSegment(distance * 0.4, 0.15, fingerMaterial);
      fingerGroup.add(segment1);

      const segment2 = createFingerSegment(distance * 0.35, 0.13, fingerMaterial);
      segment2.position.y = distance * 0.4;
      segment2.rotation.x = -0.3;
      segment1.add(segment2);

      const segment3 = createFingerSegment(distance * 0.25, 0.11, fingerMaterial);
      segment3.position.y = distance * 0.35;
      segment3.rotation.x = -0.4;
      segment2.add(segment3);

      // Rotate finger group to point toward target
      const angle = Math.atan2(direction.x, direction.z);
      fingerGroup.rotation.y = angle;
      fingerGroup.rotation.x = -Math.PI / 3;

      handGroup.add(fingerGroup);

      // Add crosshair marker on fretboard (only for pressed frets)
      const crosshairGroup = new THREE.Group();
      crosshairGroup.position.set(fretX, -0.35, stringZ);

      const crosshairMaterial = new THREE.MeshBasicMaterial({ color: 0xffff00 });

      // Horizontal line
      const hLineGeom = new THREE.BoxGeometry(0.4, 0.02, 0.02);
      const hLine = new THREE.Mesh(hLineGeom, crosshairMaterial);
      crosshairGroup.add(hLine);

      // Vertical line
      const vLineGeom = new THREE.BoxGeometry(0.02, 0.02, 0.4);
      const vLine = new THREE.Mesh(vLineGeom, crosshairMaterial);
      crosshairGroup.add(vLine);

      // Center dot
      const dotGeom = new THREE.SphereGeometry(0.06, 8, 8);
      const dot = new THREE.Mesh(dotGeom, crosshairMaterial);
      crosshairGroup.add(dot);

      scene.add(crosshairGroup);
    });

    // Create simple thumb
    const thumbGroup = new THREE.Group();
    thumbGroup.position.set(-2.2, 0, 0);
    thumbGroup.rotation.z = Math.PI / 4;
    thumbGroup.rotation.y = Math.PI / 6;

    const thumb1 = createFingerSegment(1.5, 0.18, fingerMaterial);
    thumbGroup.add(thumb1);

    const thumb2 = createFingerSegment(1.3, 0.16, fingerMaterial);
    thumb2.position.y = 1.5;
    thumb2.rotation.x = -0.2;
    thumb1.add(thumb2);

    handGroup.add(thumbGroup);

    // Create fretboard
    const fretboardGroup = new THREE.Group();
    fretboardGroup.position.set(0, -0.5, 0);
    scene.add(fretboardGroup);

    // Calculate fretboard dimensions
    const lastFretPosition = calculateFretPosition(5);
    const nextFretPosition = calculateFretPosition(6);
    const halfFretDistance = (nextFretPosition - lastFretPosition) / 2;
    const fretboardLength = lastFretPosition + halfFretDistance;
    const fretboardTopThickness = 0.4;

    // Create fretboard wood
    const fretboardGeometry = new THREE.BoxGeometry(fretboardLength, fretboardTopThickness, NUT_WIDTH);
    const fretboardMaterial = new THREE.MeshPhongMaterial({
      color: 0x3d2817,
      shininess: 20
    });
    const fretboardMesh = new THREE.Mesh(fretboardGeometry, fretboardMaterial);
    fretboardMesh.position.set(fretboardLength / 2 - SCALE_LENGTH / 2, fretboardTopThickness / 2, 0);
    fretboardGroup.add(fretboardMesh);

    // Create realistic strings with angles at pressed frets
    const stringMaterial = new THREE.LineBasicMaterial({ color: 0xcccccc, linewidth: 2 });
    const fretboardEndX = fretboardLength - SCALE_LENGTH / 2;
    const bodyZoneLength = (nextFretPosition - lastFretPosition) * 3;
    const bridgeX = fretboardEndX + bodyZoneLength;
    const stringHeight = fretboardTopThickness / 2 + 0.2;
    const pressedHeight = fretboardTopThickness / 2 + 0.05;

    for (let i = 0; i < 6; i++) {
      const stringNum = i + 1;
      const stringZ = (stringNum / 5 - 0.5) * NUT_WIDTH * 0.85;

      // Find if this string is pressed
      const pressedPos = positions.find(p => p && p.string === stringNum && p.fret > 0);

      const points = [];

      if (pressedPos) {
        // String is pressed - create segments with angle at fret
        const fretX = calculateFretPosition(pressedPos.fret) - SCALE_LENGTH / 2;

        // Segment 1: Nut to pressed fret (angled down)
        points.push(new THREE.Vector3(-SCALE_LENGTH / 2, stringHeight, stringZ) as any);
        points.push(new THREE.Vector3(fretX, pressedHeight, stringZ) as any);

        // Segment 2: Pressed fret to bridge (continues at lower angle)
        points.push(new THREE.Vector3(fretX, pressedHeight, stringZ) as any);
        points.push(new THREE.Vector3(bridgeX, stringHeight * 0.8, stringZ) as any);
      } else {
        // Open string - straight line from nut to bridge
        points.push(new THREE.Vector3(-SCALE_LENGTH / 2, stringHeight, stringZ) as any);
        points.push(new THREE.Vector3(bridgeX, stringHeight, stringZ) as any);
      }

      const stringGeometry = new THREE.BufferGeometry().setFromPoints(points);
      const stringLine = new THREE.Line(stringGeometry, stringMaterial);
      fretboardGroup.add(stringLine);
    }

    // Nut (white, at fret 0)
    const nutMaterial = new THREE.MeshPhongMaterial({ color: 0xffffff, shininess: 50 });
    const nutGeom = new THREE.BoxGeometry(0.1, 0.4, NUT_WIDTH);
    const nut = new THREE.Mesh(nutGeom, nutMaterial);
    nut.position.set(-SCALE_LENGTH / 2, fretboardTopThickness / 2 + 0.12, 0);
    fretboardGroup.add(nut);

    // Frets 1-5 with proper spacing
    const fretMaterial = new THREE.MeshPhongMaterial({ color: 0xcccccc, shininess: 80 });
    for (let i = 1; i <= 5; i++) {
      const fretPosition = calculateFretPosition(i);
      const fretGeom = new THREE.BoxGeometry(0.05, 0.4, NUT_WIDTH);
      const fret = new THREE.Mesh(fretGeom, fretMaterial);
      fret.position.set(
        fretPosition - SCALE_LENGTH / 2,
        fretboardTopThickness / 2 + 0.12,
        0
      );
      fretboardGroup.add(fret);
    }

    // Add finger position markers on fretboard
    const getScoreColor = (score: number) => {
      if (score > 8000) return 0x4caf50;
      if (score > 5000) return 0xff9800;
      return 0xf44336;
    };

    const markerColor = playabilityScore ? getScoreColor(playabilityScore) : 0x4caf50;
    const markerMaterial = new THREE.MeshPhongMaterial({
      color: markerColor,
      emissive: markerColor,
      emissiveIntensity: 0.3
    });

    positions.forEach(pos => {
      const markerGeom = new THREE.SphereGeometry(0.15, 16, 16);
      const marker = new THREE.Mesh(markerGeom, markerMaterial);
      marker.position.x = -3.5 + (pos.fret * 1.4);
      marker.position.z = 0.75 - ((pos.string - 1) * 0.3); // Flipped: string 1 at +0.75, string 6 at -0.75
      marker.position.y = 0.5;
      fretboardGroup.add(marker);
    });

    // Animation (render only, no movement)
    let animationId: number;
    const animate = () => {
      animationId = requestAnimationFrame(animate);
      renderer.render(scene, camera);
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
      cancelAnimationFrame(animationId);
      if (containerRef.current && renderer.domElement) {
        containerRef.current.removeChild(renderer.domElement);
      }
      renderer.dispose();
    };
  }, [positions, playabilityScore]);

  return <div ref={containerRef} style={{ width: '100%', height: '100%' }} />;
};
