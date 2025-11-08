import React, { useEffect, useRef, useState } from 'react';
import { Box, Typography, Button, Stack, Paper, Chip } from '@mui/material';
import * as THREE from 'three';
import { GLTFLoader } from 'three/examples/jsm/loaders/GLTFLoader.js';
import { OrbitControls } from 'three/examples/jsm/controls/OrbitControls.js';

interface ModelMetadata {
  name: string;
  path: string;
  size: string;
  scale: number;
  position: [number, number, number];
  rotation: [number, number, number];
}

const models: Record<string, ModelMetadata> = {
  // Egyptian Models (NEW!)
  ankh: {
    name: 'Ankh ☥',
    path: '/models/ankh.glb',
    size: '114.93 KB',
    scale: 1.5,
    position: [0, 0, 0],
    rotation: [0, 0, 0],
  },
  stele: {
    name: 'Stele 𓉔',
    path: '/models/stele.glb',
    size: '19.77 KB',
    scale: 1.2,
    position: [0, -0.5, 0],
    rotation: [0, 0, 0],
  },
  scarab: {
    name: 'Scarab 𓆣',
    path: '/models/scarab.glb',
    size: '88.41 KB',
    scale: 1.5,
    position: [0, 0, 0],
    rotation: [0, 0, 0],
  },
  pyramid: {
    name: 'Pyramid 𓉼',
    path: '/models/pyramid.glb',
    size: '7.42 KB',
    scale: 1.0,
    position: [0, -0.5, 0],
    rotation: [0, 0, 0],
  },
  lotus: {
    name: 'Lotus 𓆸',
    path: '/models/lotus.glb',
    size: '199.83 KB',
    scale: 1.2,
    position: [0, 0, 0],
    rotation: [0, 0, 0],
  },

  // Guitar Models
  guitar: {
    name: 'Guitar 1 🎸',
    path: '/models/guitar.glb',
    size: '376.89 KB',
    scale: 1.0,
    position: [0, -0.5, 0],
    rotation: [0, Math.PI / 4, 0],
  },
  guitar2: {
    name: 'Guitar 2 🎸',
    path: '/models/guitar2.glb',
    size: '785.53 KB',
    scale: 1.0,
    position: [0, -0.5, 0],
    rotation: [0, Math.PI / 4, 0],
  },
};

const Models3DTest: React.FC = () => {
  const canvasRef = useRef<HTMLCanvasElement>(null);
  const sceneRef = useRef<THREE.Scene | null>(null);
  const cameraRef = useRef<THREE.PerspectiveCamera | null>(null);
  const rendererRef = useRef<THREE.WebGLRenderer | null>(null);
  const controlsRef = useRef<OrbitControls | null>(null);
  const currentModelRef = useRef<THREE.Group | null>(null);
  const animationFrameRef = useRef<number | null>(null);

  const [currentModelKey, setCurrentModelKey] = useState<string>('ankh');
  const [isRotating, setIsRotating] = useState(false);
  const [isWireframe, setIsWireframe] = useState(false);
  const [fps, setFps] = useState(0);
  const [modelStats, setModelStats] = useState({ vertices: 0, triangles: 0 });

  // Initialize scene
  useEffect(() => {
    if (!canvasRef.current) return;

    const canvas = canvasRef.current;
    const width = canvas.clientWidth;
    const height = canvas.clientHeight;

    // Scene
    const scene = new THREE.Scene();
    scene.background = new THREE.Color(0x1a1a1a);
    sceneRef.current = scene;

    // Camera
    const camera = new THREE.PerspectiveCamera(45, width / height, 0.1, 1000);
    camera.position.set(0, 2, 5);
    cameraRef.current = camera;

    // Renderer
    const renderer = new THREE.WebGLRenderer({
      canvas,
      antialias: true,
      alpha: true,
    });
    renderer.setSize(width, height);
    renderer.setPixelRatio(window.devicePixelRatio);
    renderer.shadowMap.enabled = true;
    renderer.shadowMap.type = THREE.PCFSoftShadowMap;
    renderer.toneMapping = THREE.ACESFilmicToneMapping;
    renderer.toneMappingExposure = 1.0;
    rendererRef.current = renderer;

    // Controls
    const controls = new OrbitControls(camera, canvas);
    controls.enableDamping = true;
    controls.dampingFactor = 0.05;
    controls.minDistance = 2;
    controls.maxDistance = 20;
    controlsRef.current = controls;

    // Lighting
    setupLighting(scene);

    // Ground plane
    const groundGeometry = new THREE.PlaneGeometry(20, 20);
    const groundMaterial = new THREE.MeshStandardMaterial({
      color: 0x333333,
      roughness: 0.8,
      metalness: 0.2,
    });
    const ground = new THREE.Mesh(groundGeometry, groundMaterial);
    ground.rotation.x = -Math.PI / 2;
    ground.position.y = -1;
    ground.receiveShadow = true;
    scene.add(ground);

    // Grid helper
    const gridHelper = new THREE.GridHelper(20, 20, 0x444444, 0x222222);
    gridHelper.position.y = -0.99;
    scene.add(gridHelper);

    // Animation loop
    let lastTime = performance.now();
    let frameCount = 0;
    let fpsTime = 0;

    const animate = () => {
      animationFrameRef.current = requestAnimationFrame(animate);

      const currentTime = performance.now();
      const deltaTime = currentTime - lastTime;
      lastTime = currentTime;

      // FPS calculation
      frameCount++;
      fpsTime += deltaTime;
      if (fpsTime >= 1000) {
        setFps(Math.round((frameCount * 1000) / fpsTime));
        frameCount = 0;
        fpsTime = 0;
      }

      // Auto-rotation
      if (isRotating && currentModelRef.current) {
        currentModelRef.current.rotation.y += 0.01;
      }

      controls.update();
      renderer.render(scene, camera);
    };

    animate();

    // Handle resize
    const handleResize = () => {
      const newWidth = canvas.clientWidth;
      const newHeight = canvas.clientHeight;
      camera.aspect = newWidth / newHeight;
      camera.updateProjectionMatrix();
      renderer.setSize(newWidth, newHeight);
    };

    window.addEventListener('resize', handleResize);

    return () => {
      window.removeEventListener('resize', handleResize);
      if (animationFrameRef.current) {
        cancelAnimationFrame(animationFrameRef.current);
      }
      renderer.dispose();
      controls.dispose();
    };
  }, []);

  // Load model
  useEffect(() => {
    if (!sceneRef.current) return;

    const scene = sceneRef.current;
    const modelData = models[currentModelKey];

    // Remove previous model
    if (currentModelRef.current) {
      scene.remove(currentModelRef.current);
      currentModelRef.current.traverse((child) => {
        if (child instanceof THREE.Mesh) {
          child.geometry.dispose();
          if (Array.isArray(child.material)) {
            child.material.forEach((mat) => mat.dispose());
          } else {
            child.material.dispose();
          }
        }
      });
    }

    // Load new model
    const loader = new GLTFLoader();
    loader.load(
      modelData.path,
      (gltf) => {
        const model = gltf.scene;
        model.scale.setScalar(modelData.scale);
        model.position.set(...modelData.position);
        model.rotation.set(...modelData.rotation);

        // Calculate stats
        let vertices = 0;
        let triangles = 0;

        model.traverse((child) => {
          if (child instanceof THREE.Mesh) {
            child.castShadow = true;
            child.receiveShadow = true;

            if (child.geometry) {
              const positions = child.geometry.attributes.position;
              if (positions) {
                vertices += positions.count;
                if (child.geometry.index) {
                  triangles += child.geometry.index.count / 3;
                } else {
                  triangles += positions.count / 3;
                }
              }
            }

            // Apply wireframe if enabled
            if (isWireframe) {
              if (Array.isArray(child.material)) {
                child.material.forEach((mat) => (mat.wireframe = true));
              } else {
                child.material.wireframe = true;
              }
            }
          }
        });

        setModelStats({ vertices, triangles: Math.round(triangles) });

        scene.add(model);
        currentModelRef.current = model;
      },
      undefined,
      (error) => {
        console.error('Error loading model:', error);
      }
    );
  }, [currentModelKey, isWireframe]);

  const setupLighting = (scene: THREE.Scene) => {
    // Ambient light
    const ambientLight = new THREE.AmbientLight(0xffffff, 0.4);
    scene.add(ambientLight);

    // Key light (main directional light)
    const keyLight = new THREE.DirectionalLight(0xffffff, 1.5);
    keyLight.position.set(5, 10, 5);
    keyLight.castShadow = true;
    keyLight.shadow.mapSize.width = 2048;
    keyLight.shadow.mapSize.height = 2048;
    keyLight.shadow.camera.near = 0.5;
    keyLight.shadow.camera.far = 50;
    keyLight.shadow.camera.left = -10;
    keyLight.shadow.camera.right = 10;
    keyLight.shadow.camera.top = 10;
    keyLight.shadow.camera.bottom = -10;
    scene.add(keyLight);

    // Fill light
    const fillLight = new THREE.DirectionalLight(0xffffff, 0.5);
    fillLight.position.set(-5, 5, -5);
    scene.add(fillLight);

    // Rim light
    const rimLight = new THREE.DirectionalLight(0xffffff, 0.8);
    rimLight.position.set(0, 5, -10);
    scene.add(rimLight);

    // Point lights for accents
    const pointLight1 = new THREE.PointLight(0x4dabf7, 1.0, 10);
    pointLight1.position.set(3, 3, 3);
    scene.add(pointLight1);

    const pointLight2 = new THREE.PointLight(0xff6b6b, 1.0, 10);
    pointLight2.position.set(-3, 3, -3);
    scene.add(pointLight2);
  };

  const resetCamera = () => {
    if (cameraRef.current && controlsRef.current) {
      cameraRef.current.position.set(0, 2, 5);
      controlsRef.current.target.set(0, 0, 0);
      controlsRef.current.update();
    }
  };

  const cycleModel = () => {
    const keys = Object.keys(models);
    const currentIndex = keys.indexOf(currentModelKey);
    const nextIndex = (currentIndex + 1) % keys.length;
    setCurrentModelKey(keys[nextIndex]);
  };

  return (
    <Box sx={{ width: '100%', height: '100vh', display: 'flex', flexDirection: 'column', bgcolor: '#0a0a0a' }}>
      {/* Header */}
      <Box sx={{ p: 2, bgcolor: '#1a1a1a', borderBottom: '1px solid #333' }}>
        <Typography variant="h4" sx={{ color: '#fff', mb: 1 }}>
          🧊 3D Models Gallery
        </Typography>
        <Typography variant="body2" sx={{ color: '#888' }}>
          Interactive Three.js WebGL scene with Blender models
        </Typography>
      </Box>

      {/* Main content */}
      <Box sx={{ flex: 1, display: 'flex', position: 'relative' }}>
        {/* Canvas */}
        <canvas
          ref={canvasRef}
          style={{
            width: '100%',
            height: '100%',
            display: 'block',
          }}
        />

        {/* Controls overlay */}
        <Paper
          sx={{
            position: 'absolute',
            top: 16,
            left: 16,
            p: 2,
            bgcolor: 'rgba(26, 26, 26, 0.9)',
            backdropFilter: 'blur(10px)',
          }}
        >
          <Stack spacing={1}>
            <Button variant="outlined" size="small" onClick={resetCamera}>
              🎥 Reset Camera
            </Button>
            <Button
              variant={isRotating ? 'contained' : 'outlined'}
              size="small"
              onClick={() => setIsRotating(!isRotating)}
            >
              🔄 {isRotating ? 'Stop' : 'Start'} Rotation
            </Button>
            <Button
              variant={isWireframe ? 'contained' : 'outlined'}
              size="small"
              onClick={() => setIsWireframe(!isWireframe)}
            >
              🔲 Wireframe
            </Button>
            <Button variant="outlined" size="small" onClick={cycleModel}>
              ➡️ Next Model
            </Button>
          </Stack>
        </Paper>

        {/* Info panel */}
        <Paper
          sx={{
            position: 'absolute',
            top: 16,
            right: 16,
            p: 2,
            bgcolor: 'rgba(26, 26, 26, 0.9)',
            backdropFilter: 'blur(10px)',
            minWidth: 200,
          }}
        >
          <Typography variant="subtitle2" sx={{ color: '#fff', mb: 1 }}>
            Model Info
          </Typography>
          <Stack spacing={0.5}>
            <Chip label={models[currentModelKey].name} size="small" color="primary" />
            <Typography variant="caption" sx={{ color: '#888' }}>
              Vertices: {modelStats.vertices.toLocaleString()}
            </Typography>
            <Typography variant="caption" sx={{ color: '#888' }}>
              Triangles: {modelStats.triangles.toLocaleString()}
            </Typography>
            <Typography variant="caption" sx={{ color: '#888' }}>
              Size: {models[currentModelKey].size}
            </Typography>
            <Typography variant="caption" sx={{ color: '#888' }}>
              FPS: {fps}
            </Typography>
          </Stack>
        </Paper>

        {/* Model selector */}
        <Paper
          sx={{
            position: 'absolute',
            bottom: 16,
            left: '50%',
            transform: 'translateX(-50%)',
            p: 1,
            bgcolor: 'rgba(26, 26, 26, 0.9)',
            backdropFilter: 'blur(10px)',
          }}
        >
          <Stack direction="row" spacing={1}>
            {Object.entries(models).map(([key, model]) => (
              <Button
                key={key}
                variant={currentModelKey === key ? 'contained' : 'outlined'}
                size="small"
                onClick={() => setCurrentModelKey(key)}
              >
                {model.name}
              </Button>
            ))}
          </Stack>
        </Paper>
      </Box>
    </Box>
  );
};

export default Models3DTest;

