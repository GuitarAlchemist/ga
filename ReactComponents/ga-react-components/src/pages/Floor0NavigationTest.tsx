import React, { useEffect, useRef, useState } from 'react';
import { Box, Typography, Paper, CircularProgress, Alert, Button, Stack, Chip } from '@mui/material';
import * as THREE from 'three';
import { OrbitControls } from 'three/examples/jsm/controls/OrbitControls.js';
import { getMusicRoomLoader, FloorLayout } from '../components/BSP/MusicRoomLoader';

/**
 * Floor 0 Navigation Test - End-to-End Demo
 * 
 * This page demonstrates:
 * 1. Loading room data from backend API
 * 2. Rendering rooms as 3D spaces
 * 3. Basic navigation controls
 * 4. Room visualization with corridors
 */

const Floor0NavigationTest: React.FC = () => {
  const containerRef = useRef<HTMLDivElement>(null);
  const sceneRef = useRef<THREE.Scene | null>(null);
  const cameraRef = useRef<THREE.PerspectiveCamera | null>(null);
  const rendererRef = useRef<THREE.WebGLRenderer | null>(null);
  const controlsRef = useRef<OrbitControls | null>(null);
  const animationFrameRef = useRef<number | null>(null);

  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [floorData, setFloorData] = useState<FloorLayout | null>(null);
  const [stats, setStats] = useState({
    rooms: 0,
    corridors: 0,
    categories: 0,
  });

  // Load floor data from backend
  useEffect(() => {
    const loadFloorData = async () => {
      try {
        setLoading(true);
        setError(null);

        const loader = getMusicRoomLoader();
        const data = await loader.loadFloor(0, 100, 42);

        setFloorData(data);
        setStats({
          rooms: data.rooms.length,
          corridors: data.corridors.length,
          categories: data.categories.length,
        });

        console.log('Floor 0 data loaded:', data);
      } catch (err) {
        console.error('Failed to load floor data:', err);
        setError(err instanceof Error ? err.message : 'Failed to load floor data');
      } finally {
        setLoading(false);
      }
    };

    loadFloorData();
  }, []);

  // Initialize Three.js scene
  useEffect(() => {
    if (!containerRef.current || !floorData) return;

    // Create scene
    const scene = new THREE.Scene();
    scene.background = new THREE.Color(0x1a1a2e);
    scene.fog = new THREE.Fog(0x1a1a2e, 50, 200);
    sceneRef.current = scene;

    // Create camera
    const camera = new THREE.PerspectiveCamera(
      75,
      containerRef.current.clientWidth / containerRef.current.clientHeight,
      0.1,
      1000
    );
    camera.position.set(50, 60, 50);
    camera.lookAt(50, 0, 50);
    cameraRef.current = camera;

    // Create renderer
    const renderer = new THREE.WebGLRenderer({ antialias: true });
    renderer.setSize(containerRef.current.clientWidth, containerRef.current.clientHeight);
    renderer.setPixelRatio(window.devicePixelRatio);
    renderer.shadowMap.enabled = true;
    renderer.shadowMap.type = THREE.PCFSoftShadowMap;
    containerRef.current.appendChild(renderer.domElement);
    rendererRef.current = renderer;

    // Add orbit controls
    const controls = new OrbitControls(camera, renderer.domElement);
    controls.enableDamping = true;
    controls.dampingFactor = 0.05;
    controls.maxPolarAngle = Math.PI / 2 - 0.1; // Prevent going below ground
    controls.target.set(50, 0, 50);
    controlsRef.current = controls;

    // Add lights
    const ambientLight = new THREE.AmbientLight(0xffffff, 0.4);
    scene.add(ambientLight);

    const directionalLight = new THREE.DirectionalLight(0xffffff, 0.8);
    directionalLight.position.set(50, 100, 50);
    directionalLight.castShadow = true;
    directionalLight.shadow.camera.left = -100;
    directionalLight.shadow.camera.right = 100;
    directionalLight.shadow.camera.top = 100;
    directionalLight.shadow.camera.bottom = -100;
    directionalLight.shadow.mapSize.width = 2048;
    directionalLight.shadow.mapSize.height = 2048;
    scene.add(directionalLight);

    // Add ground plane
    const groundGeometry = new THREE.PlaneGeometry(200, 200);
    const groundMaterial = new THREE.MeshStandardMaterial({
      color: 0x2a2a3e,
      roughness: 0.8,
      metalness: 0.2,
    });
    const ground = new THREE.Mesh(groundGeometry, groundMaterial);
    ground.rotation.x = -Math.PI / 2;
    ground.receiveShadow = true;
    scene.add(ground);

    // Add grid helper
    const gridHelper = new THREE.GridHelper(200, 100, 0x444444, 0x222222);
    scene.add(gridHelper);

    // Render rooms
    floorData.rooms.forEach((room) => {
      // Create room walls
      const wallHeight = 3;
      const wallThickness = 0.2;

      // Calculate room dimensions from bounds
      const roomWidth = room.bounds.maxX - room.bounds.minX;
      const roomDepth = room.bounds.maxZ - room.bounds.minZ;
      const centerX = room.center.x;
      const centerZ = room.center.z;

      // Use color from room (hex number)
      const color = new THREE.Color(room.color);

      // Floor
      const floorGeometry = new THREE.BoxGeometry(roomWidth, 0.1, roomDepth);
      const floorMaterial = new THREE.MeshStandardMaterial({
        color: color,
        roughness: 0.7,
        metalness: 0.3,
      });
      const floor = new THREE.Mesh(floorGeometry, floorMaterial);
      floor.position.set(centerX, 0.05, centerZ);
      floor.receiveShadow = true;
      scene.add(floor);

      // Walls (4 sides)
      const wallMaterial = new THREE.MeshStandardMaterial({
        color: color.clone().multiplyScalar(0.8),
        roughness: 0.9,
        metalness: 0.1,
        transparent: true,
        opacity: 0.7,
      });

      // North wall (minZ)
      const northWall = new THREE.Mesh(
        new THREE.BoxGeometry(roomWidth, wallHeight, wallThickness),
        wallMaterial
      );
      northWall.position.set(centerX, wallHeight / 2, room.bounds.minZ);
      northWall.castShadow = true;
      scene.add(northWall);

      // South wall (maxZ)
      const southWall = new THREE.Mesh(
        new THREE.BoxGeometry(roomWidth, wallHeight, wallThickness),
        wallMaterial
      );
      southWall.position.set(centerX, wallHeight / 2, room.bounds.maxZ);
      southWall.castShadow = true;
      scene.add(southWall);

      // West wall (minX)
      const westWall = new THREE.Mesh(
        new THREE.BoxGeometry(wallThickness, wallHeight, roomDepth),
        wallMaterial
      );
      westWall.position.set(room.bounds.minX, wallHeight / 2, centerZ);
      westWall.castShadow = true;
      scene.add(westWall);

      // East wall (maxX)
      const eastWall = new THREE.Mesh(
        new THREE.BoxGeometry(wallThickness, wallHeight, roomDepth),
        wallMaterial
      );
      eastWall.position.set(room.bounds.maxX, wallHeight / 2, centerZ);
      eastWall.castShadow = true;
      scene.add(eastWall);

      // Add room label
      const canvas = document.createElement('canvas');
      const context = canvas.getContext('2d')!;
      canvas.width = 512;
      canvas.height = 128;
      context.fillStyle = '#ffffff';
      context.font = 'bold 48px Arial';
      context.textAlign = 'center';
      context.textBaseline = 'middle';
      context.fillText(room.category, 256, 64);

      const texture = new THREE.CanvasTexture(canvas);
      const labelMaterial = new THREE.SpriteMaterial({ map: texture });
      const label = new THREE.Sprite(labelMaterial);
      label.position.set(centerX, wallHeight + 1, centerZ);
      label.scale.set(8, 2, 1);
      scene.add(label);
    });

    // Render corridors
    floorData.corridors.forEach((corridor) => {
      const corridorMaterial = new THREE.MeshStandardMaterial({
        color: 0x4a4a5e,
        roughness: 0.8,
        metalness: 0.2,
      });

      for (let i = 0; i < corridor.points.length - 1; i++) {
        const start = corridor.points[i];
        const end = corridor.points[i + 1];

        const length = Math.sqrt(
          Math.pow(end.x - start.x, 2) + Math.pow(end.z - start.z, 2)
        );
        const angle = Math.atan2(end.z - start.z, end.x - start.x);

        const corridorGeometry = new THREE.BoxGeometry(length, 0.1, corridor.width);
        const corridorMesh = new THREE.Mesh(corridorGeometry, corridorMaterial);
        corridorMesh.position.set(
          (start.x + end.x) / 2,
          0.05,
          (start.z + end.z) / 2
        );
        corridorMesh.rotation.y = -angle;
        corridorMesh.receiveShadow = true;
        scene.add(corridorMesh);
      }
    });

    // Animation loop
    const animate = () => {
      animationFrameRef.current = requestAnimationFrame(animate);
      controls.update();
      renderer.render(scene, camera);
    };
    animate();

    // Handle window resize
    const handleResize = () => {
      if (!containerRef.current || !camera || !renderer) return;
      camera.aspect = containerRef.current.clientWidth / containerRef.current.clientHeight;
      camera.updateProjectionMatrix();
      renderer.setSize(containerRef.current.clientWidth, containerRef.current.clientHeight);
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
    };
  }, [floorData]);

  return (
    <Box sx={{ width: '100%', height: '100vh', display: 'flex', flexDirection: 'column' }}>
      {/* Header */}
      <Paper sx={{ p: 2, borderRadius: 0 }}>
        <Typography variant="h4" gutterBottom>
          🎸 Floor 0 Navigation Test - Set Classes
        </Typography>
        <Typography variant="body2" color="text.secondary" gutterBottom>
          End-to-end demo: Backend API → 3D Visualization → Navigation
        </Typography>

        {/* Stats */}
        {floorData && (
          <Stack direction="row" spacing={1} sx={{ mt: 2 }}>
            <Chip label={`${stats.rooms} Rooms`} color="primary" size="small" />
            <Chip label={`${stats.corridors} Corridors`} color="secondary" size="small" />
            <Chip label={`${stats.categories} Categories`} color="info" size="small" />
            <Chip label={`Seed: ${floorData.seed || 42}`} size="small" />
          </Stack>
        )}
      </Paper>

      {/* 3D Viewport */}
      <Box sx={{ flex: 1, position: 'relative', bgcolor: '#000' }}>
        {loading && (
          <Box
            sx={{
              position: 'absolute',
              top: '50%',
              left: '50%',
              transform: 'translate(-50%, -50%)',
              zIndex: 10,
            }}
          >
            <CircularProgress />
            <Typography sx={{ mt: 2, color: 'white' }}>Loading Floor 0...</Typography>
          </Box>
        )}

        {error && (
          <Box sx={{ p: 2 }}>
            <Alert severity="error">{error}</Alert>
          </Box>
        )}

        <div
          ref={containerRef}
          style={{ width: '100%', height: '100%' }}
          data-testid="floor0-viewport"
        />
      </Box>
    </Box>
  );
};

export default Floor0NavigationTest;

