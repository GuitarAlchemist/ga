/**
 * Immersive Musical World
 * 
 * A full 3D immersive world where the musical hierarchy becomes a physical landscape.
 * Combines Sunburst3D hierarchy with DOOM-style first-person navigation.
 * 
 * Features:
 * - First-person camera with WASD + mouse look
 * - Concentric floating islands representing hierarchy levels
 * - Portals/bridges connecting nodes
 * - Skybox and atmospheric effects
 * - Interactive 3D objects for each musical concept
 */

import React, { useEffect, useRef, useState, useCallback } from 'react';
import * as THREE from 'three';
import { Box, Typography, Paper, Stack, Chip } from '@mui/material';

export interface MusicalNode {
  name: string;
  value?: number;
  children?: MusicalNode[];
  color?: number;
}

export interface ImmersiveMusicalWorldProps {
  data: MusicalNode;
  width?: number;
  height?: number;
  moveSpeed?: number;
  lookSpeed?: number;
  showHUD?: boolean;
}

interface FlatNode {
  name: string;
  depth: number;
  startAngle: number;
  endAngle: number;
  innerRadius: number;
  outerRadius: number;
  color: number;
  value: number;
  children?: MusicalNode[];
  parent?: string;
}

export const ImmersiveMusicalWorld: React.FC<ImmersiveMusicalWorldProps> = ({
  data,
  width = 1600,
  height = 900,
  moveSpeed = 10.0,
  lookSpeed = 0.002,
  showHUD = true,
}) => {
  const containerRef = useRef<HTMLDivElement>(null);
  const sceneRef = useRef<THREE.Scene | null>(null);
  const cameraRef = useRef<THREE.PerspectiveCamera | null>(null);
  const rendererRef = useRef<THREE.WebGLRenderer | null>(null);
  const animationFrameRef = useRef<number | null>(null);
  
  // Movement state
  const keysPressed = useRef<Set<string>>(new Set());
  const velocity = useRef(new THREE.Vector3());
  const direction = useRef(new THREE.Vector3());
  const euler = useRef(new THREE.Euler(0, 0, 0, 'YXZ'));
  const isPointerLocked = useRef(false);
  
  // Navigation state
  const [currentNode, setCurrentNode] = useState<string>('Root');
  const [breadcrumbs, setBreadcrumbs] = useState<string[]>(['Root']);
  const [fps, setFps] = useState(60);
  
  // Flatten hierarchy for rendering
  const flattenHierarchy = useCallback((
    node: MusicalNode,
    depth: number = 0,
    startAngle: number = 0,
    endAngle: number = Math.PI * 2,
    parent?: string
  ): FlatNode[] => {
    const result: FlatNode[] = [];
    const children = node.children || [];
    const value = node.value || children.reduce((sum, child) => sum + (child.value || 1), 0) || 1;
    
    const innerRadius = depth * 20;
    const outerRadius = (depth + 1) * 20;
    const color = node.color || (0x4488ff + depth * 0x111111);
    
    result.push({
      name: node.name,
      depth,
      startAngle,
      endAngle,
      innerRadius,
      outerRadius,
      color,
      value,
      children: node.children,
      parent,
    });
    
    if (children.length > 0) {
      const totalValue = children.reduce((sum, child) => sum + (child.value || 1), 0);
      let currentAngle = startAngle;
      
      children.forEach((child) => {
        const childValue = child.value || 1;
        const angleSpan = ((endAngle - startAngle) * childValue) / totalValue;
        const childEndAngle = currentAngle + angleSpan;
        
        result.push(...flattenHierarchy(child, depth + 1, currentAngle, childEndAngle, node.name));
        currentAngle = childEndAngle;
      });
    }
    
    return result;
  }, []);

  useEffect(() => {
    if (!containerRef.current) return;

    // Scene setup
    const scene = new THREE.Scene();
    scene.fog = new THREE.FogExp2(0x000033, 0.002);
    sceneRef.current = scene;

    // Camera setup
    const camera = new THREE.PerspectiveCamera(75, width / height, 0.1, 1000);
    camera.position.set(0, 50, 100);
    cameraRef.current = camera;

    // Renderer setup
    const renderer = new THREE.WebGLRenderer({ antialias: true });
    renderer.setSize(width, height);
    renderer.setPixelRatio(window.devicePixelRatio);
    renderer.shadowMap.enabled = true;
    renderer.shadowMap.type = THREE.VSMShadowMap;
    containerRef.current.appendChild(renderer.domElement);
    rendererRef.current = renderer;

    // Skybox
    const skyGeometry = new THREE.SphereGeometry(500, 32, 32);
    const skyMaterial = new THREE.ShaderMaterial({
      uniforms: {
        topColor: { value: new THREE.Color(0x0077ff) },
        bottomColor: { value: new THREE.Color(0x000033) },
        offset: { value: 33 },
        exponent: { value: 0.6 },
      },
      vertexShader: `
        varying vec3 vWorldPosition;
        void main() {
          vec4 worldPosition = modelMatrix * vec4(position, 1.0);
          vWorldPosition = worldPosition.xyz;
          gl_Position = projectionMatrix * modelViewMatrix * vec4(position, 1.0);
        }
      `,
      fragmentShader: `
        uniform vec3 topColor;
        uniform vec3 bottomColor;
        uniform float offset;
        uniform float exponent;
        varying vec3 vWorldPosition;
        void main() {
          float h = normalize(vWorldPosition + offset).y;
          gl_FragColor = vec4(mix(bottomColor, topColor, max(pow(max(h, 0.0), exponent), 0.0)), 1.0);
        }
      `,
      side: THREE.BackSide,
    });
    const sky = new THREE.Mesh(skyGeometry, skyMaterial);
    scene.add(sky);

    // Lighting
    const ambientLight = new THREE.AmbientLight(0x404040, 0.5);
    scene.add(ambientLight);

    const sunLight = new THREE.DirectionalLight(0xffffff, 1.0);
    sunLight.position.set(100, 200, 100);
    sunLight.castShadow = true;
    sunLight.shadow.camera.left = -200;
    sunLight.shadow.camera.right = 200;
    sunLight.shadow.camera.top = 200;
    sunLight.shadow.camera.bottom = -200;
    sunLight.shadow.camera.near = 0.1;
    sunLight.shadow.camera.far = 500;
    sunLight.shadow.mapSize.width = 2048;
    sunLight.shadow.mapSize.height = 2048;
    sunLight.shadow.radius = 4;
    sunLight.shadow.bias = -0.0005;
    scene.add(sunLight);

    // Point lights for atmosphere
    const pointLight1 = new THREE.PointLight(0x00ffff, 1, 100);
    pointLight1.position.set(50, 30, 50);
    scene.add(pointLight1);

    const pointLight2 = new THREE.PointLight(0xff00ff, 1, 100);
    pointLight2.position.set(-50, 30, -50);
    scene.add(pointLight2);

    // Ground plane
    const groundGeometry = new THREE.CircleGeometry(300, 64);
    const groundMaterial = new THREE.MeshStandardMaterial({
      color: 0x001122,
      roughness: 0.8,
      metalness: 0.2,
    });
    const ground = new THREE.Mesh(groundGeometry, groundMaterial);
    ground.rotation.x = -Math.PI / 2;
    ground.position.y = -5;
    ground.receiveShadow = true;
    scene.add(ground);

    // Grid helper
    const gridHelper = new THREE.GridHelper(300, 30, 0x00ff00, 0x003300);
    gridHelper.position.y = -4.9;
    scene.add(gridHelper);

    // Create 3D hierarchy
    const nodes = flattenHierarchy(data);
    const platformGroup = new THREE.Group();
    
    nodes.forEach((node) => {
      const { startAngle, endAngle, innerRadius, outerRadius, depth, color } = node;
      
      // Create platform for this node
      const shape = new THREE.Shape();
      const segments = 32;
      
      // Outer arc
      for (let i = 0; i <= segments; i++) {
        const angle = startAngle + (endAngle - startAngle) * (i / segments);
        const x = Math.cos(angle) * outerRadius;
        const z = Math.sin(angle) * outerRadius;
        if (i === 0) {
          shape.moveTo(x, z);
        } else {
          shape.lineTo(x, z);
        }
      }
      
      // Inner arc (reverse)
      for (let i = segments; i >= 0; i--) {
        const angle = startAngle + (endAngle - startAngle) * (i / segments);
        const x = Math.cos(angle) * innerRadius;
        const z = Math.sin(angle) * innerRadius;
        shape.lineTo(x, z);
      }
      
      shape.closePath();
      
      const extrudeSettings = {
        depth: 2,
        bevelEnabled: true,
        bevelThickness: 0.5,
        bevelSize: 0.5,
        bevelSegments: 3,
      };
      
      const geometry = new THREE.ExtrudeGeometry(shape, extrudeSettings);
      const material = new THREE.MeshStandardMaterial({
        color: color,
        roughness: 0.5,
        metalness: 0.3,
        emissive: color,
        emissiveIntensity: 0.2,
      });
      
      const platform = new THREE.Mesh(geometry, material);
      platform.rotation.x = -Math.PI / 2;
      platform.position.y = depth * 10; // Elevation based on depth
      platform.castShadow = true;
      platform.receiveShadow = true;
      platform.userData = { node };
      
      platformGroup.add(platform);
      
      // Add edge glow
      const edges = new THREE.EdgesGeometry(geometry);
      const lineMaterial = new THREE.LineBasicMaterial({ color: 0x00ffff, linewidth: 2 });
      const wireframe = new THREE.LineSegments(edges, lineMaterial);
      wireframe.rotation.x = -Math.PI / 2;
      wireframe.position.y = depth * 10 + 0.1;
      platformGroup.add(wireframe);
    });
    
    scene.add(platformGroup);

    // Particle system for atmosphere
    const particleCount = 1000;
    const particlesGeometry = new THREE.BufferGeometry();
    const particlePositions = new Float32Array(particleCount * 3);
    
    for (let i = 0; i < particleCount * 3; i += 3) {
      particlePositions[i] = (Math.random() - 0.5) * 400;
      particlePositions[i + 1] = Math.random() * 200;
      particlePositions[i + 2] = (Math.random() - 0.5) * 400;
    }
    
    particlesGeometry.setAttribute('position', new THREE.BufferAttribute(particlePositions, 3));
    
    const particlesMaterial = new THREE.PointsMaterial({
      color: 0x00ffff,
      size: 0.5,
      transparent: true,
      opacity: 0.6,
      blending: THREE.AdditiveBlending,
    });
    
    const particles = new THREE.Points(particlesGeometry, particlesMaterial);
    scene.add(particles);

    // Animation loop
    let lastTime = performance.now();
    let frameCount = 0;
    let fpsTime = 0;

    const animate = () => {
      const currentTime = performance.now();
      const delta = (currentTime - lastTime) / 1000;
      lastTime = currentTime;

      // FPS calculation
      frameCount++;
      fpsTime += delta;
      if (fpsTime >= 1.0) {
        setFps(Math.round(frameCount / fpsTime));
        frameCount = 0;
        fpsTime = 0;
      }

      // Movement
      if (isPointerLocked.current && cameraRef.current) {
        direction.current.set(0, 0, 0);

        if (keysPressed.current.has('w')) direction.current.z -= 1;
        if (keysPressed.current.has('s')) direction.current.z += 1;
        if (keysPressed.current.has('a')) direction.current.x -= 1;
        if (keysPressed.current.has('d')) direction.current.x += 1;
        if (keysPressed.current.has(' ')) direction.current.y += 1;
        if (keysPressed.current.has('shift')) direction.current.y -= 1;

        direction.current.normalize();

        const forward = new THREE.Vector3(0, 0, -1);
        forward.applyQuaternion(cameraRef.current.quaternion);
        forward.y = 0;
        forward.normalize();

        const right = new THREE.Vector3(1, 0, 0);
        right.applyQuaternion(cameraRef.current.quaternion);
        right.y = 0;
        right.normalize();

        velocity.current.set(0, 0, 0);
        velocity.current.addScaledVector(forward, -direction.current.z * moveSpeed * delta);
        velocity.current.addScaledVector(right, direction.current.x * moveSpeed * delta);
        velocity.current.y = direction.current.y * moveSpeed * delta;

        cameraRef.current.position.add(velocity.current);
      }

      // Animate particles
      particles.rotation.y += 0.0001;

      // Render
      if (rendererRef.current && sceneRef.current && cameraRef.current) {
        rendererRef.current.render(sceneRef.current, cameraRef.current);
      }

      animationFrameRef.current = requestAnimationFrame(animate);
    };

    animate();

    // Event handlers
    const handleKeyDown = (e: KeyboardEvent) => {
      keysPressed.current.add(e.key.toLowerCase());
    };

    const handleKeyUp = (e: KeyboardEvent) => {
      keysPressed.current.delete(e.key.toLowerCase());
    };

    const handleMouseMove = (e: MouseEvent) => {
      if (!isPointerLocked.current || !cameraRef.current) return;

      euler.current.setFromQuaternion(cameraRef.current.quaternion);
      euler.current.y -= e.movementX * lookSpeed;
      euler.current.x -= e.movementY * lookSpeed;
      euler.current.x = Math.max(-Math.PI / 2, Math.min(Math.PI / 2, euler.current.x));
      cameraRef.current.quaternion.setFromEuler(euler.current);
    };

    const handlePointerLockChange = () => {
      isPointerLocked.current = document.pointerLockElement === renderer.domElement;
    };

    const handleClick = () => {
      renderer.domElement.requestPointerLock();
    };

    document.addEventListener('keydown', handleKeyDown);
    document.addEventListener('keyup', handleKeyUp);
    document.addEventListener('mousemove', handleMouseMove);
    document.addEventListener('pointerlockchange', handlePointerLockChange);
    containerRef.current.addEventListener('click', handleClick);

    const currentContainer = containerRef.current;

    return () => {
      if (animationFrameRef.current) {
        cancelAnimationFrame(animationFrameRef.current);
      }
      document.removeEventListener('keydown', handleKeyDown);
      document.removeEventListener('keyup', handleKeyUp);
      document.removeEventListener('mousemove', handleMouseMove);
      document.removeEventListener('pointerlockchange', handlePointerLockChange);
      currentContainer?.removeEventListener('click', handleClick);
      renderer.dispose();
      currentContainer?.removeChild(renderer.domElement);
    };
  }, [data, width, height, moveSpeed, lookSpeed, flattenHierarchy]);

  return (
    <Box sx={{ position: 'relative', width, height, overflow: 'hidden' }}>
      <div ref={containerRef} style={{ width: '100%', height: '100%', cursor: 'pointer' }} />
      
      {showHUD && (
        <>
          {/* HUD */}
          <Paper
            sx={{
              position: 'absolute',
              top: 16,
              left: 16,
              p: 2,
              backgroundColor: 'rgba(0, 0, 0, 0.8)',
              color: '#0f0',
              fontFamily: 'monospace',
              border: '2px solid #0f0',
              boxShadow: '0 0 20px rgba(0, 255, 0, 0.5)',
            }}
          >
            <Stack spacing={1}>
              <Typography variant="h6" sx={{ color: '#0f0' }}>
                🌍 IMMERSIVE MUSICAL WORLD
              </Typography>
              <Typography variant="caption" sx={{ color: '#888' }}>
                FPS: {fps}
              </Typography>
              <Typography variant="caption" sx={{ color: '#888' }}>
                Current: {currentNode}
              </Typography>
              <Stack direction="row" spacing={0.5} flexWrap="wrap">
                {breadcrumbs.map((crumb, index) => (
                  <Chip
                    key={index}
                    label={crumb}
                    size="small"
                    sx={{
                      backgroundColor: 'rgba(0, 255, 0, 0.2)',
                      color: '#0f0',
                      borderColor: '#0f0',
                      fontSize: '0.7rem',
                    }}
                    variant="outlined"
                  />
                ))}
              </Stack>
            </Stack>
          </Paper>

          {/* Controls */}
          <Paper
            sx={{
              position: 'absolute',
              bottom: 16,
              left: 16,
              p: 2,
              backgroundColor: 'rgba(0, 0, 0, 0.8)',
              color: '#0f0',
              fontFamily: 'monospace',
              border: '2px solid #0f0',
              boxShadow: '0 0 20px rgba(0, 255, 0, 0.5)',
            }}
          >
            <Stack spacing={0.5}>
              <Typography variant="caption" sx={{ color: '#0f0', fontWeight: 'bold' }}>
                CONTROLS
              </Typography>
              <Typography variant="caption" sx={{ color: '#888' }}>
                WASD - Move | Space/Shift - Up/Down
              </Typography>
              <Typography variant="caption" sx={{ color: '#888' }}>
                Mouse - Look | Click - Lock Pointer
              </Typography>
              <Typography variant="caption" sx={{ color: '#888' }}>
                ESC - Release Pointer
              </Typography>
            </Stack>
          </Paper>
        </>
      )}
    </Box>
  );
};

export default ImmersiveMusicalWorld;

